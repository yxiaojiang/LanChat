using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LanChat
{
    public partial class NetworkService : IDisposable
    {
        // 每次显式刷新优先询问已有地址，等待短暂应答窗口后再确认原在线节点是否掉线。
        private IPAddress[] _knownAddresses = new IPAddress[0];
        private PeerInfo[] _peersToCheck = new PeerInfo[0];
        private DateTime _refreshStarted;
        private int _refreshVersion;
        private bool _checkingPresence;
        private NetworkInterfaceDetail[] _sweepingInterfaces;

        #region UDP 多网卡监听

        private Socket BindUdpListener(IPAddress address)
        {
            Socket socket = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                socket.Bind(new IPEndPoint(address, UDP_PORT));
                new Thread(() => UdpListenLoop(socket)) { IsBackground = true }.Start();
                return socket;
            }
            catch (Exception ex)
            {
                if (socket != null) socket.Close();
                Log("UDP 绑定到 " + address + " 失败: " + ex.Message);
                return null;
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
                    if (received <= 0 || !_isRunning) continue;

                    string msg = Encoding.UTF8.GetString(buffer, 0, received);
                    ProcessUdpPacket(msg, (IPEndPoint)remoteEp);
                }
                catch (ObjectDisposedException) { break; }
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
                        peer.LastSeen = DateTime.UtcNow;
                    }
                }
            }
        }

        private void SendAckTo(IPAddress ip, int port)
        {
            var interfaces = _activeInterfaces;
            SendDatagram(GetDiscoverySocket(ip, interfaces), CreatePresence("ACK", interfaces), ip, port);
        }

        private Socket GetDiscoverySocket(IPAddress ip, NetworkInterfaceDetail[] interfaces)
        {
            if (!IPAddress.IsLoopback(ip))
            {
                Socket virtualSocket = null;
                foreach (var adapter in interfaces)
                {
                    if (adapter.UdpSocket == null || !IsInSameSubnet(adapter.IP, ip, adapter.Mask)) continue;
                    if (!adapter.IsVirtualOrTun) return adapter.UdpSocket;
                    virtualSocket = adapter.UdpSocket;
                }
                if (virtualSocket != null) return virtualSocket;
            }

            return _udpAnySocket;
        }

        private static void SendDatagram(Socket socket, byte[] data, IPAddress ip, int port)
        {
            if (socket == null) return;
            try { socket.SendTo(data, new IPEndPoint(ip, port)); }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }

        #endregion

        #region Peer Management & Broadcast

        /// <summary>
        /// 获取当前所有存活在线的局域网对端节点副本列表
        /// </summary>
        /// <returns>活跃对端列表</returns>
        public List<PeerInfo> GetActivePeers()
        {
            lock (_peerLock)
            {
                return new List<PeerInfo>(_peers.Values);
            }
        }

        /// <summary>
        /// 添加新在线对端或更新已存在对端的心跳、IP与端口
        /// </summary>
        /// <param name="peerId">对端节点唯一标识</param>
        /// <param name="nick">对端最新昵称</param>
        /// <param name="host">对端主机名</param>
        /// <param name="ip">对端 IPv4 地址</param>
        /// <param name="port">对端 TCP 监听端口</param>
        public void AddOrUpdatePeer(string peerId, string nick, string host, IPAddress ip, int port)
        {
            UpdatePeer(peerId, nick, host, ip, port);
        }

        // 文字帧没有监听端口和主机名，空端口表示只更新连接提供的信息。
        private void UpdatePeer(string peerId, string nick, string host, IPAddress ip, int? announcedPort)
        {
            if (peerId == LocalId) return;

            bool isNew = false;
            bool isUpdated = false;
            PeerInfo peer;
            List<string> removed = null;

            lock (_peerLock)
            {
                string addressId = ip.ToString();
                if (peerId == addressId)
                {
                    // 匿名确认不能替该地址已有的真实身份更新时间，也不需要再加入地址占位。
                    if (_peers.Values.Any(known => known.Id != addressId && ip.Equals(known.IP))) return;
                }

                if (!_peers.TryGetValue(peerId, out peer))
                {
                    peer = new PeerInfo
                    {
                        Id = peerId,
                        NickName = string.IsNullOrEmpty(nick) ? "未知好友" : nick,
                        HostName = string.IsNullOrEmpty(host) ? "LAN-PC" : host,
                        IP = ip,
                        TcpPort = announcedPort ?? TCP_BASE_PORT,
                        LastSeen = DateTime.UtcNow
                    };
                    _peers[peerId] = peer;
                    isNew = true;
                }
                else
                {
                    if ((!string.IsNullOrEmpty(nick) && peer.NickName != nick) ||
                        (announcedPort.HasValue && (peer.TcpPort != announcedPort.Value ||
                            (!string.IsNullOrEmpty(host) && peer.HostName != host))) ||
                        !peer.IP.Equals(ip))
                    {
                        if (!string.IsNullOrEmpty(nick)) peer.NickName = nick;
                        if (announcedPort.HasValue)
                        {
                            if (!string.IsNullOrEmpty(host)) peer.HostName = host;
                            peer.TcpPort = announcedPort.Value;
                        }
                        peer.IP = ip;
                        isUpdated = true;
                    }
                    peer.LastSeen = DateTime.UtcNow;
                }

                if (peerId != addressId)
                {
                    // 公告端口确认同一监听端点已换身份。文字帧未携带端口，只能归并地址占位。
                    foreach (var old in _peers.Values)
                    {
                        if (old == peer || !ip.Equals(old.IP)) continue;
                        if (announcedPort.HasValue ? old.TcpPort != announcedPort.Value : old.Id != addressId) continue;
                        if (removed == null) removed = new List<string>();
                        removed.Add(old.Id);
                        peer.UnreadCount += old.UnreadCount;
                    }
                    if (removed != null)
                        foreach (var id in removed) _peers.Remove(id);
                }
            }

            if (removed != null)
            {
                NotifyPeersLeft(removed);
                isNew = false;
                isUpdated = true;
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

        /// <summary>广播本机状态。HELLO 同时发起好友刷新和后台子网探测。</summary>
        /// <param name="command">现有 UDP 状态指令，如 HELLO、PING 或 BYE。</param>
        public void BroadcastPresence(string command)
        {
            if (command == "HELLO")
            {
                RefreshPeers(null);
                return;
            }

            lock (_discoveryLock)
            {
                if (_isRunning) SendPresence(command, _activeInterfaces, null);
            }
        }

        internal void RefreshPeers(IEnumerable<IPAddress> knownAddresses)
        {
            lock (_discoveryLock)
            {
                if (!_isRunning) return;
                // 手动刷新也重试发现和绑定，以覆盖地址通知漏报、延迟和瞬时绑定失败。
                RebindInterfaces();
                if (knownAddresses != null) _knownAddresses = knownAddresses.Distinct().ToArray();

                var interfaces = _activeInterfaces;
                _refreshVersion++;
                _refreshStarted = DateTime.UtcNow;
                _peersToCheck = GetActivePeers().ToArray();
                var targets = _peersToCheck.Select(peer => peer.IP)
                    .Concat(_knownAddresses.Where(ip => IPAddress.IsLoopback(ip) || IsInAnyLocalSubnet(ip)))
                    .Distinct();
                SendPresence("HELLO", interfaces, targets);

                if (_peersToCheck.Length > 0 && !_checkingPresence)
                    _presenceCheckTimer.Change(300, Timeout.Infinite);

                if (!EnableSubnetSweep) return;

                // 快速刷新总会立即发包。重复刷新同一网段时，后台兜底扫描共用一轮。
                if (_sweepingInterfaces == interfaces) return;
                _sweepingInterfaces = interfaces;
                new Thread(() => SweepInterfaces(interfaces)) { IsBackground = true }.Start();
            }
        }

        private byte[] CreatePresence(string command, NetworkInterfaceDetail[] interfaces)
        {
            return Encoding.UTF8.GetBytes(string.Format("{0}|{1}|{2}|{3}|{4}|{5}",
                command, LocalId, NickName, HostName, LocalTcpPort, GetPrimaryPhysicalIp(interfaces)));
        }

        private void SendPresence(string command, NetworkInterfaceDetail[] interfaces, IEnumerable<IPAddress> targets)
        {
            byte[] data = CreatePresence(command, interfaces);
            if (targets != null)
            {
                foreach (var target in targets)
                    SendDatagram(GetDiscoverySocket(target, interfaces), data, target, UDP_PORT);
            }

            foreach (var adapter in interfaces)
            {
                var socket = adapter.UdpSocket ?? _udpAnySocket;
                SendDatagram(socket, data, adapter.BroadcastAddress, UDP_PORT);
                SendDatagram(socket, data, IPAddress.Broadcast, UDP_PORT);
            }
            SendDatagram(_udpAnySocket, data, IPAddress.Loopback, UDP_PORT);
        }

        private void CheckRefreshedPeers(object state)
        {
            DateTime started;
            int version;
            PeerInfo[] peers;
            NetworkInterfaceDetail[] interfaces;
            lock (_discoveryLock)
            {
                if (!_isRunning || _checkingPresence) return;
                _checkingPresence = true;
                started = _refreshStarted;
                version = _refreshVersion;
                interfaces = _activeInterfaces;
                lock (_peerLock)
                    peers = _peersToCheck.Where(peer => peer.LastSeen < started).ToArray();
            }

            try
            {
                if (peers.Length == 0) return;

                byte[] retry = CreatePresence("HELLO", interfaces);
                foreach (var peer in peers)
                    SendDatagram(GetDiscoverySocket(peer.IP, interfaces), retry, peer.IP, UDP_PORT);

                // 使用已公告的端口复核未回应的好友。给第二轮 UDP 回包留出独立的应答窗口。
                var wait = System.Diagnostics.Stopwatch.StartNew();
                RunTcpProbes(peers.Length, 16, interfaces,
                    index => TryTcpHandshake(peers[index].IP, peers[index].TcpPort, interfaces, peers[index]));
                int remaining = 650 - (int)wait.ElapsedMilliseconds;
                if (remaining > 0) Thread.Sleep(remaining);

                List<string> removed;
                lock (_discoveryLock)
                {
                    if (!_isRunning || version != _refreshVersion || interfaces != _activeInterfaces) return;
                    removed = RemoveExpiredPeers(peers, started);
                }
                NotifyPeersLeft(removed);
            }
            finally
            {
                lock (_discoveryLock)
                {
                    _checkingPresence = false;
                    if (_isRunning && version != _refreshVersion)
                        _presenceCheckTimer.Change(300, Timeout.Infinite);
                }
            }
        }

        private void SweepInterfaces(NetworkInterfaceDetail[] interfaces)
        {
            try
            {
                var targets = new HashSet<IPAddress>();
                var localAddresses = new HashSet<IPAddress>(interfaces.Select(adapter => adapter.IP));
                byte[] data = CreatePresence("HELLO", interfaces);
                foreach (var adapter in interfaces)
                {
                    if (adapter.IsVirtualOrTun) continue;
                    byte[] ip = adapter.IP.GetAddressBytes();
                    byte[] mask = adapter.Mask.GetAddressBytes();
                    if (mask[0] != 255 || mask[1] != 255 || mask[2] != 255) continue;

                    // 只探测当前接口实际覆盖的 /24 或更小子网，跳过网络地址、广播地址和本机。
                    int first = (ip[3] & mask[3]) + 1;
                    int last = (ip[3] | (255 ^ mask[3])) - 1;
                    var socket = adapter.UdpSocket ?? _udpAnySocket;
                    for (int i = first; i <= last; i++)
                    {
                        if (!_isRunning || interfaces != _activeInterfaces) return;
                        var target = new IPAddress(new byte[] { ip[0], ip[1], ip[2], (byte)i });
                        if (localAddresses.Contains(target)) continue;
                        targets.Add(target);
                        SendDatagram(socket, data, target, UDP_PORT);
                    }
                }

                // 优先让单播 UDP 返回完整身份，TCP 只补探尚未发现的地址。
                if (targets.Count == 0) return;
                Thread.Sleep(300);
                if (!_isRunning || interfaces != _activeInterfaces) return;
                lock (_peerLock)
                {
                    foreach (var peer in _peers.Values) targets.Remove(peer.IP);
                }
                var missing = targets.ToArray();
                RunTcpProbes(missing.Length, 32, interfaces,
                    index => TryTcpHandshake(missing[index], TCP_BASE_PORT, interfaces, null));
            }
            finally
            {
                lock (_discoveryLock)
                {
                    if (_sweepingInterfaces == interfaces) _sweepingInterfaces = null;
                }
            }
        }

        private void RunTcpProbes(int count, int concurrency, NetworkInterfaceDetail[] interfaces, Action<int> probe)
        {
            // 固定数量的后台线程消费探测目标，避免每个 IP 新建线程或等待固定的扫描收尾时间。
            int next = -1;
            var workers = new Thread[Math.Min(count, concurrency)];
            for (int i = 0; i < workers.Length; i++)
            {
                workers[i] = new Thread(() =>
                {
                    while (_isRunning && interfaces == _activeInterfaces)
                    {
                        int index = Interlocked.Increment(ref next);
                        if (index >= count) return;
                        probe(index);
                    }
                }) { IsBackground = true };
                workers[i].Start();
            }
            foreach (var worker in workers) worker.Join();
        }

        /// <summary>
        /// 主动向指定目标 IP 发起 TCP 握手验证，确认对端是否为可互通的 LanChat 客户端
        /// </summary>
        /// <param name="targetIp">目标 IPv4 地址</param>
        /// <returns>握手是否成功</returns>
        public bool TryTcpHandshake(IPAddress targetIp)
        {
            return TryTcpHandshake(targetIp, TCP_BASE_PORT, _activeInterfaces, null);
        }

        private bool TryTcpHandshake(IPAddress targetIp, int port, NetworkInterfaceDetail[] interfaces, PeerInfo expected)
        {
            try
            {
                using (TcpClient client = new TcpClient(GetLocalEndpointForTarget(targetIp)))
                {
                    client.NoDelay = true;
                    var ar = client.BeginConnect(targetIp, port, null, null);
                    using (var connectWait = ar.AsyncWaitHandle)
                    {
                        if (!connectWait.WaitOne(250)) return false;
                        client.EndConnect(ar);
                    }

                    using (NetworkStream ns = client.GetStream())
                    using (BinaryWriter bw = new BinaryWriter(ns, Encoding.UTF8))
                    using (BinaryReader br = new BinaryReader(ns, Encoding.UTF8))
                    {
                        bw.Write(MAGIC);
                        bw.Write((byte)1); // 文字帧
                        bw.Write(true);    // 广播标记
                        bw.Write(LocalId);
                        bw.Write(NickName);
                        bw.Write("");
                        bw.Write(HANDSHAKE_MSG);
                        bw.Flush();

                        client.ReceiveTimeout = 1200;
                        byte ack = br.ReadByte();
                        if (ack == 1)
                        {
                            lock (_discoveryLock)
                            {
                                if (!_isRunning || interfaces != _activeInterfaces) return false;
                                if (expected == null)
                                {
                                    AddOrUpdatePeer(targetIp.ToString(), "好友 (" + targetIp + ")", "LAN-PC", targetIp, port);
                                }
                                else
                                {
                                    lock (_peerLock)
                                    {
                                        PeerInfo current;
                                        // 确认字节不含身份。旧快照或已有其他身份的端点不能据此续期。
                                        if (!_peers.TryGetValue(expected.Id, out current) || current != expected ||
                                            !targetIp.Equals(current.IP) || current.TcpPort != port ||
                                            _peers.Values.Any(other => other != current &&
                                                other.TcpPort == port && targetIp.Equals(other.IP))) return false;
                                        current.LastSeen = DateTime.UtcNow;
                                    }
                                }
                            }
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 异步主动探测指定 IP 地址，结合 UDP 探测与 TCP 握手双通道连通
        /// </summary>
        /// <param name="ipAddressStr">输入的 IPv4 字符串</param>
        /// <param name="callback">连通结果回调 (success, message)</param>
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

                    var interfaces = _activeInterfaces;
                    SendDatagram(GetDiscoverySocket(ip, interfaces), CreatePresence("HELLO", interfaces), ip, UDP_PORT);

                    // TCP 握手保留现有的手动连通性判断。
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

        /// <summary>
        /// 从内存活跃节点列表中移除指定对端
        /// </summary>
        /// <param name="peerId">对端标识</param>
        public void RemovePeer(string peerId)
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

                    NotifyPeersLeft(RemoveExpiredPeers(GetActivePeers(), DateTime.UtcNow.AddSeconds(-25)));
                }
                catch { }
            }
        }

        private List<string> RemoveExpiredPeers(IEnumerable<PeerInfo> candidates, DateTime cutoff)
        {
            var removed = new List<string>();
            lock (_peerLock)
            {
                foreach (var candidate in candidates)
                {
                    PeerInfo current;
                    if (_peers.TryGetValue(candidate.Id, out current) && current == candidate && current.LastSeen < cutoff)
                    {
                        _peers.Remove(candidate.Id);
                        removed.Add(candidate.Id);
                    }
                }
            }
            return removed;
        }

        private void NotifyPeersLeft(List<string> removed)
        {
            var notify = OnPeerLeft;
            if (notify == null) return;
            foreach (string id in removed) notify(id);
        }

        #endregion
    }
}
