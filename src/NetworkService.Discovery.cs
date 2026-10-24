using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LanChat
{
    public partial class NetworkService : IDisposable
    {
        #region UDP Multi-Interface Listeners

        private void InitUdpListeners()
        {
            // 1. Bind to IPAddress.Any
            try
            {
                Socket anySock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                anySock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                anySock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                anySock.Bind(new IPEndPoint(IPAddress.Any, UDP_PORT));

                _udpSockets.Add(anySock);
                new Thread(() => UdpListenLoop(anySock)) { IsBackground = true }.Start();
            }
            catch (Exception ex)
            {
                Log("UDP Any 绑定异常: " + ex.Message);
            }

            // 2. Bind dedicated sockets to physical interfaces
            foreach (var iface in _activeInterfaces)
            {
                try
                {
                    Socket ifaceSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    ifaceSock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    ifaceSock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                    ifaceSock.Bind(new IPEndPoint(iface.IP, UDP_PORT));

                    iface.UdpSocket = ifaceSock;
                    _udpSockets.Add(ifaceSock);

                    new Thread(() => UdpListenLoop(ifaceSock)) { IsBackground = true }.Start();
                }
                catch (Exception ex)
                {
                    Log(string.Format("UDP 绑定到网卡 {0}({1}) 异常: {2}", iface.Name, iface.IP, ex.Message));
                }
            }
        }

        private void UdpListenLoop(object state)
        {
            Socket sock = (Socket)state;
            byte[] buffer = new byte[4096];
            EndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);

            while (_isRunning)
            {
                try
                {
                    int received = sock.ReceiveFrom(buffer, ref remoteEp);
                    if (received <= 0) continue;

                    string msg = Encoding.UTF8.GetString(buffer, 0, received);
                    ProcessUdpPacket(msg, (IPEndPoint)remoteEp);
                }
                catch (SocketException)
                {
                    if (!_isRunning) break;
                }
                catch (Exception ex)
                {
                    Log("UDP 数据接收异常: " + ex.Message);
                }
            }
        }

        private void ProcessUdpPacket(string packet, IPEndPoint remoteEp)
        {
            string[] parts = packet.Split('|');
            if (parts.Length < 2) return;

            string cmd = parts[0];
            string peerId = parts[1];

            if (peerId == LocalId) return;

            if (cmd == "BYE")
            {
                RemovePeer(peerId);
                return;
            }

            if (parts.Length >= 5)
            {
                string nick = parts[2];
                string host = parts[3];
                int port;
                if (!int.TryParse(parts[4], out port)) return;

                IPAddress finalPeerIp = remoteEp.Address;
                if (parts.Length >= 6 && !string.IsNullOrEmpty(parts[5]))
                {
                    IPAddress annIp;
                    if (IPAddress.TryParse(parts[5], out annIp))
                    {
                        if (!IPAddress.IsLoopback(annIp) && (IPAddress.IsLoopback(finalPeerIp) || !IsInAnyLocalSubnet(finalPeerIp)))
                        {
                            finalPeerIp = annIp;
                        }
                    }
                }

                AddOrUpdatePeer(peerId, nick, host, finalPeerIp, port);

                if (cmd == "HELLO")
                {
                    SendAckTo(remoteEp.Address, UDP_PORT);
                    if (!finalPeerIp.Equals(remoteEp.Address))
                    {
                        SendAckTo(finalPeerIp, UDP_PORT);
                    }
                }
            }
            else if (cmd == "PING")
            {
                lock (_peerLock)
                {
                    PeerInfo peer;
                    if (_peers.TryGetValue(peerId, out peer))
                    {
                        peer.LastSeen = DateTime.Now;
                    }
                }
            }
        }

        private void SendAckTo(IPAddress ip, int port)
        {
            try
            {
                IPAddress primaryIp = GetPrimaryPhysicalIp();
                string payload = string.Format("ACK|{0}|{1}|{2}|{3}|{4}", LocalId, NickName, HostName, LocalTcpPort, primaryIp);
                byte[] data = Encoding.UTF8.GetBytes(payload);

                foreach (var sock in _udpSockets)
                {
                    try { sock.SendTo(data, new IPEndPoint(ip, port)); } catch { }
                }
            }
            catch { }
        }

        #endregion

        #region Peer Management & Broadcast

        public List<PeerInfo> GetActivePeers()
        {
            lock (_peerLock)
            {
                return new List<PeerInfo>(_peers.Values);
            }
        }

        public void AddOrUpdatePeer(string peerId, string nick, string host, IPAddress ip, int port)
        {
            if (peerId == LocalId) return;

            bool isNew = false;
            bool isUpdated = false;
            PeerInfo peer;

            lock (_peerLock)
            {
                if (!_peers.TryGetValue(peerId, out peer))
                {
                    peer = new PeerInfo
                    {
                        Id = peerId,
                        NickName = string.IsNullOrEmpty(nick) ? "未知好友" : nick,
                        HostName = string.IsNullOrEmpty(host) ? "LAN-PC" : host,
                        IP = ip,
                        TcpPort = port,
                        LastSeen = DateTime.Now
                    };
                    _peers[peerId] = peer;
                    isNew = true;
                }
                else
                {
                    if (peer.NickName != nick || peer.TcpPort != port || !peer.IP.Equals(ip))
                    {
                        if (!string.IsNullOrEmpty(nick)) peer.NickName = nick;
                        if (!string.IsNullOrEmpty(host)) peer.HostName = host;
                        peer.IP = ip;
                        peer.TcpPort = port;
                        isUpdated = true;
                    }
                    peer.LastSeen = DateTime.Now;
                }
            }

            if (isNew && OnPeerJoined != null)
            {
                new Thread(() => OnPeerJoined(peer)) { IsBackground = true }.Start();
            }
            else if (isUpdated && OnPeerUpdated != null)
            {
                new Thread(() => OnPeerUpdated(peer)) { IsBackground = true }.Start();
            }
        }

        public void BroadcastPresence(string command)
        {
            IPAddress primaryIp = GetPrimaryPhysicalIp();
            string payload = string.Format("{0}|{1}|{2}|{3}|{4}|{5}", command, LocalId, NickName, HostName, LocalTcpPort, primaryIp);
            byte[] data = Encoding.UTF8.GetBytes(payload);

            // 1. Traditional Broadcast on each physical interface
            foreach (var iface in _activeInterfaces)
            {
                Socket sock = iface.UdpSocket ?? (_udpSockets.Count > 0 ? _udpSockets[0] : null);
                if (sock == null) continue;

                try { sock.SendTo(data, new IPEndPoint(iface.BroadcastAddress, UDP_PORT)); } catch { }
                try { sock.SendTo(data, new IPEndPoint(IPAddress.Broadcast, UDP_PORT)); } catch { }
            }

            // 2. Loopback
            if (_udpSockets.Count > 0)
            {
                try { _udpSockets[0].SendTo(data, new IPEndPoint(IPAddress.Loopback, UDP_PORT)); } catch { }
            }

            // 3. Fast Subnet Sweep ONLY on HELLO (Startup or manual refresh), NEVER during 8-second heartbeat!
            if (command == "HELLO" && !_isSweeping)
            {
                _isSweeping = true;
                new Thread(() =>
                {
                    try
                    {
                        DoSubnetSweep(primaryIp, data);
                    }
                    finally
                    {
                        _isSweeping = false;
                    }
                }) { IsBackground = true }.Start();
            }
        }

        private void DoSubnetSweep(IPAddress primaryIp, byte[] udpData)
        {
            foreach (var iface in _activeInterfaces)
            {
                if (iface.IsVirtualOrTun) continue;

                byte[] ipBytes = iface.IP.GetAddressBytes();
                byte[] maskBytes = iface.Mask.GetAddressBytes();

                // Standard /24 subnet (e.g. 192.168.x.x)
                if (maskBytes[0] == 255 && maskBytes[1] == 255 && maskBytes[2] == 255)
                {
                    byte b0 = ipBytes[0];
                    byte b1 = ipBytes[1];
                    byte b2 = ipBytes[2];
                    byte myLast = ipBytes[3];
                    Socket sock = iface.UdpSocket ?? (_udpSockets.Count > 0 ? _udpSockets[0] : null);

                    // Step A: Fast Unicast UDP sweep (Takes ~15ms total!)
                    if (sock != null)
                    {
                        for (int i = 1; i <= 254; i++)
                        {
                            if (i == myLast) continue;
                            IPAddress target = new IPAddress(new byte[] { b0, b1, b2, (byte)i });
                            try { sock.SendTo(udpData, new IPEndPoint(target, UDP_PORT)); } catch { }
                        }
                    }

                    // Step B: Controlled TCP Handshake Sweep (Max 15 concurrent sockets to avoid thread starvation)
                    using (Semaphore throttle = new Semaphore(15, 15))
                    {
                        for (int i = 1; i <= 254; i++)
                        {
                            if (i == myLast) continue;
                            IPAddress target = new IPAddress(new byte[] { b0, b1, b2, (byte)i });

                            throttle.WaitOne();
                            new Thread(() =>
                            {
                                try
                                {
                                    TryTcpHandshake(target);
                                }
                                finally
                                {
                                    try { throttle.Release(); } catch { }
                                }
                            }) { IsBackground = true }.Start();
                        }

                        // Wait up to 1.5s for probes to complete
                        Thread.Sleep(1500);
                    }
                }
            }
        }

        public bool TryTcpHandshake(IPAddress targetIp)
        {
            try
            {
                using (TcpClient client = new TcpClient(GetLocalEndpointForTarget(targetIp)))
                {
                    client.NoDelay = true;
                    var ar = client.BeginConnect(targetIp, TCP_BASE_PORT, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(250)))
                    {
                        return false;
                    }
                    client.EndConnect(ar);

                    using (NetworkStream ns = client.GetStream())
                    using (BinaryWriter bw = new BinaryWriter(ns, Encoding.UTF8))
                    using (BinaryReader br = new BinaryReader(ns, Encoding.UTF8))
                    {
                        bw.Write(MAGIC);
                        bw.Write((byte)1); // PacketType Text
                        bw.Write(true);    // isBroadcast
                        bw.Write(LocalId);
                        bw.Write(NickName);
                        bw.Write("");
                        bw.Write(HANDSHAKE_MSG);
                        bw.Flush();

                        client.ReceiveTimeout = 1200;
                        byte ack = br.ReadByte();
                        if (ack == 1)
                        {
                            AddOrUpdatePeer(targetIp.ToString(), "好友 (" + targetIp + ")", "LAN-PC", targetIp, TCP_BASE_PORT);
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public void ProbeIp(string ipAddressStr, Action<bool, string> callback)
        {
            new Thread(() =>
            {
                try
                {
                    IPAddress ip;
                    if (!IPAddress.TryParse(ipAddressStr, out ip))
                    {
                        if (callback != null) callback(false, "无效的 IPv4 地址: " + ipAddressStr);
                        return;
                    }

                    IPAddress primaryIp = GetPrimaryPhysicalIp();
                    string payload = string.Format("HELLO|{0}|{1}|{2}|{3}|{4}", LocalId, NickName, HostName, LocalTcpPort, primaryIp);
                    byte[] data = Encoding.UTF8.GetBytes(payload);

                    // Send UDP probe
                    foreach (var sock in _udpSockets)
                    {
                        try { sock.SendTo(data, new IPEndPoint(ip, UDP_PORT)); } catch { }
                    }

                    // Send TCP handshake
                    bool ok = TryTcpHandshake(ip);
                    if (ok)
                    {
                        if (callback != null) callback(true, "成功连通 " + ipAddressStr + "！");
                    }
                    else
                    {
                        if (callback != null) callback(false, "无法连接到 " + ipAddressStr + ":38889，请确认对方已打开 LanChat");
                    }
                }
                catch (Exception ex)
                {
                    if (callback != null) callback(false, "探测失败: " + ex.Message);
                }
            }) { IsBackground = true }.Start();
        }

        private void RemovePeer(string peerId)
        {
            bool removed = false;
            lock (_peerLock)
            {
                removed = _peers.Remove(peerId);
            }

            if (removed && OnPeerLeft != null)
            {
                OnPeerLeft(peerId);
            }
        }

        private void HeartbeatLoop()
        {
            while (_isRunning)
            {
                try
                {
                    Thread.Sleep(8000);
                    if (!_isRunning) break;

                    // Send lightweight UDP PING only (no TCP sweep)
                    BroadcastPresence("PING");

                    List<string> expiredPeers = new List<string>();
                    DateTime now = DateTime.Now;

                    lock (_peerLock)
                    {
                        foreach (var kvp in _peers)
                        {
                            if ((now - kvp.Value.LastSeen).TotalSeconds > 25)
                            {
                                expiredPeers.Add(kvp.Key);
                            }
                        }
                    }

                    foreach (var pid in expiredPeers)
                    {
                        RemovePeer(pid);
                    }
                }
                catch { }
            }
        }

        #endregion
    }
}
