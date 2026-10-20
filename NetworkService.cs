using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LanChat
{
    public class NetworkInterfaceDetail
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public IPAddress IP { get; set; }
        public IPAddress Mask { get; set; }
        public IPAddress BroadcastAddress { get; set; }
        public bool IsVirtualOrTun { get; set; }
        public Socket UdpSocket { get; set; }
    }

    public class NetworkService : IDisposable
    {
        public const int UDP_PORT = 38888;
        public const int TCP_BASE_PORT = 38889;
        public const string HANDSHAKE_MSG = "[系统自动握手]";
        private static readonly byte[] MAGIC = new byte[] { 0x4C, 0x41, 0x4E, 0x43 }; // "LANC"

        public string LocalId { get; private set; }
        public string NickName { get; set; }
        public string HostName { get; private set; }
        public int LocalTcpPort { get; private set; }
        public string SaveDirectory { get; set; }
        public bool AutoAcceptFiles { get; set; }

        private readonly List<NetworkInterfaceDetail> _activeInterfaces = new List<NetworkInterfaceDetail>();
        private readonly List<Socket> _udpSockets = new List<Socket>();
        private TcpListener _tcpListener;
        private Thread _tcpListenThread;
        private Thread _heartbeatThread;
        private volatile bool _isRunning;
        private volatile bool _isSweeping;

        private readonly Dictionary<string, PeerInfo> _peers = new Dictionary<string, PeerInfo>();
        private readonly object _peerLock = new object();

        // Events
        public event Action<PeerInfo> OnPeerJoined;
        public event Action<PeerInfo> OnPeerUpdated;
        public event Action<string> OnPeerLeft; // PeerId
        public event Action<ChatMessage> OnMessageReceived;
        public event Action<FileTransferProgress> OnFileProgress;
        public event Action<string, string, string, long> OnFileReceived; // senderNick, fileName, localPath, size
        public event Action<string> OnLogMessage;

        public NetworkService(string nickName)
        {
            LocalId = Guid.NewGuid().ToString("N");
            NickName = string.IsNullOrEmpty(nickName) ? Environment.UserName : nickName;
            HostName = Environment.MachineName;
            AutoAcceptFiles = true;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            SaveDirectory = Path.Combine(baseDir, "ReceivedFiles");
            if (!Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }
        }

        public void Start()
        {
            _isRunning = true;

            // 1. Discover active network adapters (prioritize physical over TUN/VPN)
            DiscoverInterfaces();

            // 2. Initialize TCP Listener
            InitTcpListener();

            // 3. Initialize Multi-Interface UDP Listeners
            InitUdpListeners();

            // 4. Start Heartbeat thread
            _heartbeatThread = new Thread(HeartbeatLoop) { IsBackground = true };
            _heartbeatThread.Start();

            // 5. Initial broadcast & fast subnet sweep
            BroadcastPresence("HELLO");
        }

        #region Interface Discovery

        private void DiscoverInterfaces()
        {
            _activeInterfaces.Clear();

            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    bool isVirt = IsVirtualOrTun(ni);
                    var ipProps = ni.GetIPProperties();

                    foreach (var u in ipProps.UnicastAddresses)
                    {
                        if (u.Address.AddressFamily == AddressFamily.InterNetwork && u.IPv4Mask != null)
                        {
                            byte[] ipBytes = u.Address.GetAddressBytes();
                            byte[] maskBytes = u.IPv4Mask.GetAddressBytes();
                            byte[] bcastBytes = new byte[4];
                            for (int i = 0; i < 4; i++)
                            {
                                bcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                            }

                            _activeInterfaces.Add(new NetworkInterfaceDetail
                            {
                                Name = ni.Name,
                                Description = ni.Description,
                                IP = u.Address,
                                Mask = u.IPv4Mask,
                                BroadcastAddress = new IPAddress(bcastBytes),
                                IsVirtualOrTun = isVirt
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("网卡探测异常: " + ex.Message);
            }
        }

        public static bool IsVirtualOrTun(NetworkInterface ni)
        {
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) return true;

            string desc = (ni.Description ?? "").ToLowerInvariant();
            string name = (ni.Name ?? "").ToLowerInvariant();

            if (desc.Contains("tun") || desc.Contains("tap") || desc.Contains("vpn") ||
                desc.Contains("virtual") || desc.Contains("wsl") || desc.Contains("hyper-v") ||
                desc.Contains("vmware") || desc.Contains("vbox") || desc.Contains("pseudo") ||
                name.Contains("tun") || name.Contains("tap") || name.Contains("vpn"))
            {
                return true;
            }

            try
            {
                byte[] mac = ni.GetPhysicalAddress().GetAddressBytes();
                if (mac == null || mac.Length == 0) return true;
            }
            catch
            {
                return true;
            }

            return false;
        }

        public IPAddress GetPrimaryPhysicalIp()
        {
            foreach (var iface in _activeInterfaces)
            {
                if (!iface.IsVirtualOrTun)
                {
                    return iface.IP;
                }
            }

            if (_activeInterfaces.Count > 0)
            {
                return _activeInterfaces[0].IP;
            }

            return IPAddress.Loopback;
        }

        public IPEndPoint GetLocalEndpointForTarget(IPAddress targetIp)
        {
            if (IPAddress.IsLoopback(targetIp))
            {
                return new IPEndPoint(IPAddress.Any, 0);
            }

            // 1. Check if target is on the same subnet as any of our physical interfaces
            foreach (var iface in _activeInterfaces)
            {
                if (!iface.IsVirtualOrTun && IsInSameSubnet(iface.IP, targetIp, iface.Mask))
                {
                    return new IPEndPoint(iface.IP, 0);
                }
            }

            // 2. Try any physical interface
            foreach (var iface in _activeInterfaces)
            {
                if (!iface.IsVirtualOrTun)
                {
                    return new IPEndPoint(iface.IP, 0);
                }
            }

            // 3. Fallback
            return new IPEndPoint(IPAddress.Any, 0);
        }

        public static bool IsInSameSubnet(IPAddress ip1, IPAddress ip2, IPAddress mask)
        {
            if (ip1 == null || ip2 == null || mask == null) return false;
            if (ip1.AddressFamily != AddressFamily.InterNetwork || ip2.AddressFamily != AddressFamily.InterNetwork) return false;

            byte[] b1 = ip1.GetAddressBytes();
            byte[] b2 = ip2.GetAddressBytes();
            byte[] m = mask.GetAddressBytes();
            if (b1.Length != 4 || b2.Length != 4 || m.Length != 4) return false;

            for (int i = 0; i < 4; i++)
            {
                if ((b1[i] & m[i]) != (b2[i] & m[i])) return false;
            }
            return true;
        }

        #endregion

        #region TCP Listener

        private void InitTcpListener()
        {
            for (int port = TCP_BASE_PORT; port < TCP_BASE_PORT + 50; port++)
            {
                try
                {
                    _tcpListener = new TcpListener(IPAddress.Any, port);
                    _tcpListener.Start();
                    LocalTcpPort = port;
                    break;
                }
                catch
                {
                    // Try next port
                }
            }

            if (_tcpListener == null)
            {
                throw new Exception("无法绑定任何可用的 TCP 端口（38889-38939）");
            }

            _tcpListenThread = new Thread(TcpListenLoop) { IsBackground = true };
            _tcpListenThread.Start();
        }

        #endregion

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

        private bool IsInAnyLocalSubnet(IPAddress ip)
        {
            foreach (var iface in _activeInterfaces)
            {
                if (!iface.IsVirtualOrTun && IsInSameSubnet(iface.IP, ip, iface.Mask))
                {
                    return true;
                }
            }
            return false;
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

        #region TCP Communication & File Transfer

        private void TcpListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _tcpListener.AcceptTcpClient();
                    client.NoDelay = true; // Instant packet dispatch (Nagle off)

                    // Dedicated thread for incoming connections: 0ms queue delay!
                    new Thread(() => HandleIncomingTcpClient(client)) { IsBackground = true }.Start();
                }
                catch (SocketException)
                {
                    if (!_isRunning) break;
                }
                catch (Exception ex)
                {
                    Log("TCP 监听错误: " + ex.Message);
                }
            }
        }

        private void HandleIncomingTcpClient(TcpClient client)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    byte[] magic = reader.ReadBytes(4);
                    if (magic.Length < 4 || magic[0] != MAGIC[0] || magic[1] != MAGIC[1] || magic[2] != MAGIC[2] || magic[3] != MAGIC[3])
                    {
                        return;
                    }

                    byte packetType = reader.ReadByte();

                    if (packetType == 1) // Text Message
                    {
                        bool isBroadcast = reader.ReadBoolean();
                        string senderId = reader.ReadString();
                        string senderNick = reader.ReadString();
                        string targetId = reader.ReadString();
                        string content = reader.ReadString();

                        IPAddress remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address;

                        // Auto learn peer immediately!
                        AddOrUpdatePeer(senderId, senderNick, "LAN-Host", remoteIp, TCP_BASE_PORT);

                        // Confirm immediately
                        writer.Write((byte)1);
                        writer.Flush();

                        // If it's just an internal handshake ping, don't show in chat log
                        if (content == HANDSHAKE_MSG)
                        {
                            return;
                        }

                        var msg = new ChatMessage
                        {
                            SenderId = senderId,
                            SenderNick = senderNick,
                            TargetId = targetId,
                            Content = content,
                            Timestamp = DateTime.Now,
                            IsSelf = false,
                            IsBroadcast = isBroadcast
                        };

                        if (OnMessageReceived != null)
                        {
                            OnMessageReceived(msg);
                        }
                    }
                    else if (packetType == 2) // File Transfer Request
                    {
                        string transferId = reader.ReadString();
                        string senderId = reader.ReadString();
                        string senderNick = reader.ReadString();
                        string fileName = reader.ReadString();
                        long fileSize = reader.ReadInt64();

                        if (!AutoAcceptFiles)
                        {
                            writer.Write((byte)0);
                            writer.Flush();
                            return;
                        }

                        writer.Write((byte)1);
                        writer.Flush();

                        string safeFileName = Path.GetFileName(fileName);
                        string localPath = GetUniqueFilePath(SaveDirectory, safeFileName);
                        string tempPath = localPath + ".downloading";

                        var progress = new FileTransferProgress
                        {
                            TransferId = transferId,
                            FileName = safeFileName,
                            TotalBytes = fileSize,
                            TransferredBytes = 0,
                            IsSending = false,
                            PeerNick = senderNick,
                            LocalPath = localPath
                        };

                        bool success = false;
                        byte[] chunk = new byte[65536];
                        long receivedBytes = 0;
                        DateTime lastTime = DateTime.Now;
                        long lastBytes = 0;

                        using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            while (receivedBytes < fileSize)
                            {
                                int toRead = (int)Math.Min(chunk.Length, fileSize - receivedBytes);
                                int read = stream.Read(chunk, 0, toRead);
                                if (read <= 0) break;

                                fs.Write(chunk, 0, read);
                                receivedBytes += read;

                                DateTime now = DateTime.Now;
                                double elapsed = (now - lastTime).TotalSeconds;
                                if (elapsed >= 0.2 || receivedBytes == fileSize)
                                {
                                    double speed = (receivedBytes - lastBytes) / (elapsed * 1024.0 * 1024.0);
                                    lastTime = now;
                                    lastBytes = receivedBytes;

                                    progress.TransferredBytes = receivedBytes;
                                    progress.SpeedMBps = speed;
                                    if (OnFileProgress != null)
                                    {
                                        OnFileProgress(progress);
                                    }
                                }
                            }

                            if (receivedBytes == fileSize)
                            {
                                success = true;
                            }
                        }

                        if (success)
                        {
                            if (File.Exists(localPath)) File.Delete(localPath);
                            File.Move(tempPath, localPath);

                            progress.IsCompleted = true;
                            progress.TransferredBytes = fileSize;
                            if (OnFileProgress != null)
                            {
                                OnFileProgress(progress);
                            }

                            if (OnFileReceived != null)
                            {
                                OnFileReceived(senderNick, safeFileName, localPath, fileSize);
                            }

                            writer.Write((byte)1);
                            writer.Flush();
                        }
                        else
                        {
                            if (File.Exists(tempPath)) File.Delete(tempPath);
                            progress.IsFailed = true;
                            progress.ErrorMessage = "传输中断";
                            if (OnFileProgress != null)
                            {
                                OnFileProgress(progress);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("TCP 客户端处理异常: " + ex.Message);
            }
        }

        public bool SendTextMessage(PeerInfo peer, string message, bool isBroadcast)
        {
            try
            {
                IPEndPoint localEp = GetLocalEndpointForTarget(peer.IP);
                using (TcpClient client = new TcpClient(localEp))
                {
                    client.NoDelay = true; // Disable Nagle algorithm for instant delivery
                    var result = client.BeginConnect(peer.IP, peer.TcpPort, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
                    if (!success) return false;
                    client.EndConnect(result);

                    using (NetworkStream stream = client.GetStream())
                    using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                    using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                    {
                        writer.Write(MAGIC);
                        writer.Write((byte)1); // PacketType Text
                        writer.Write(isBroadcast);
                        writer.Write(LocalId);
                        writer.Write(NickName);
                        writer.Write(isBroadcast ? "" : peer.Id);
                        writer.Write(message);
                        writer.Flush();

                        client.ReceiveTimeout = 2000;
                        byte ack = reader.ReadByte();
                        return ack == 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("向 {0} 发送消息失败: {1}", peer.DisplayText, ex.Message));
                return false;
            }
        }

        public void SendFileAsync(PeerInfo peer, string filePath, Action<FileTransferProgress> progressCallback, Action<bool, string> completionCallback)
        {
            new Thread(() =>
            {
                string transferId = Guid.NewGuid().ToString("N");
                string fileName = Path.GetFileName(filePath);
                FileInfo fi = new FileInfo(filePath);
                long fileSize = fi.Length;

                var progress = new FileTransferProgress
                {
                    TransferId = transferId,
                    FileName = fileName,
                    TotalBytes = fileSize,
                    TransferredBytes = 0,
                    IsSending = true,
                    PeerNick = peer.NickName,
                    LocalPath = filePath
                };

                try
                {
                    IPEndPoint localEp = GetLocalEndpointForTarget(peer.IP);
                    using (TcpClient client = new TcpClient(localEp))
                    {
                        client.NoDelay = true;
                        var result = client.BeginConnect(peer.IP, peer.TcpPort, null, null);
                        bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(4));
                        if (!success)
                        {
                            if (completionCallback != null) completionCallback(false, "连接对方超时");
                            return;
                        }
                        client.EndConnect(result);

                        using (NetworkStream stream = client.GetStream())
                        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                        using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                        {
                            writer.Write(MAGIC);
                            writer.Write((byte)2); // PacketType FileOffer
                            writer.Write(transferId);
                            writer.Write(LocalId);
                            writer.Write(NickName);
                            writer.Write(fileName);
                            writer.Write(fileSize);
                            writer.Flush();

                            client.ReceiveTimeout = 10000;
                            byte accept = reader.ReadByte();
                            if (accept != 1)
                            {
                                if (completionCallback != null) completionCallback(false, "对方拒绝或未自动接收文件");
                                return;
                            }

                            byte[] buffer = new byte[65536];
                            long sentBytes = 0;
                            DateTime lastTime = DateTime.Now;
                            long lastBytes = 0;

                            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                int read;
                                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    stream.Write(buffer, 0, read);
                                    sentBytes += read;

                                    DateTime now = DateTime.Now;
                                    double elapsed = (now - lastTime).TotalSeconds;
                                    if (elapsed >= 0.2 || sentBytes == fileSize)
                                    {
                                        double speed = (sentBytes - lastBytes) / (elapsed * 1024.0 * 1024.0);
                                        lastTime = now;
                                        lastBytes = sentBytes;

                                        progress.TransferredBytes = sentBytes;
                                        progress.SpeedMBps = speed;
                                        if (progressCallback != null) progressCallback(progress);
                                    }
                                }
                            }
                            stream.Flush();

                            byte doneConfirm = reader.ReadByte();
                            if (doneConfirm == 1)
                            {
                                progress.IsCompleted = true;
                                progress.TransferredBytes = fileSize;
                                if (progressCallback != null) progressCallback(progress);
                                if (completionCallback != null) completionCallback(true, "发送完成");
                            }
                            else
                            {
                                if (completionCallback != null) completionCallback(false, "接收方保存失败");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    progress.IsFailed = true;
                    progress.ErrorMessage = ex.Message;
                    if (progressCallback != null) progressCallback(progress);
                    if (completionCallback != null) completionCallback(false, "发送失败: " + ex.Message);
                }
            }) { IsBackground = true }.Start();
        }

        #endregion

        private static string GetUniqueFilePath(string folder, string fileName)
        {
            string destination = Path.Combine(folder, fileName);
            if (!File.Exists(destination)) return destination;

            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            int count = 1;

            while (File.Exists(destination))
            {
                destination = Path.Combine(folder, string.Format("{0} ({1}){2}", nameWithoutExt, count, ext));
                count++;
            }

            return destination;
        }

        private void Log(string msg)
        {
            if (OnLogMessage != null)
            {
                OnLogMessage(msg);
            }
        }

        public void Dispose()
        {
            _isRunning = false;

            try
            {
                BroadcastPresence("BYE");
            }
            catch { }

            foreach (var sock in _udpSockets)
            {
                try
                {
                    sock.Close();
                }
                catch { }
            }

            try
            {
                if (_tcpListener != null) _tcpListener.Stop();
            }
            catch { }
        }
    }
}
