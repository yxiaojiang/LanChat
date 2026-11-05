using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LanChat
{
    /// <summary>
    /// 跨平台核心网络通信引擎（门面 Facade）
    /// 统一协调网卡发现、UDP 局域网探测、TCP 消息与 64KB 流式分块传输
    /// </summary>
    public partial class NetworkService : IDisposable
    {
        public const int UDP_PORT = 38888;
        public const int TCP_BASE_PORT = 38889;
        public const string HANDSHAKE_MSG = "[系统自动握手]";
        private static readonly byte[] MAGIC = new byte[] { 0x4C, 0x41, 0x4E, 0x43 }; // "LANC"

        // =========================================================================
        // 公共属性与网络配置
        // =========================================================================

        /// <summary>本机实例唯一 GUID 标识</summary>
        public string LocalId { get; private set; }

        /// <summary>本机用户当前展示昵称</summary>
        public string NickName { get; set; }

        /// <summary>本机计算机名</summary>
        public string HostName { get; private set; }

        /// <summary>本机 TCP 实际监听端口</summary>
        public int LocalTcpPort { get; private set; }

        /// <summary>接收文件默认保存物理路径</summary>
        public string SaveDirectory { get; set; }

        /// <summary>是否免确认自动接收来自局域网对端的文件</summary>
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

        // =========================================================================
        // 核心网络事件总线
        // =========================================================================

        /// <summary>新对端节点加入或首次发现事件</summary>
        public event Action<PeerInfo> OnPeerJoined;

        /// <summary>对端节点状态或心跳更新事件</summary>
        public event Action<PeerInfo> OnPeerUpdated;

        /// <summary>对端节点下线或心跳超时离开事件</summary>
        public event Action<string> OnPeerLeft; // PeerId

        /// <summary>收到来自局域网单聊或全员广播消息事件</summary>
        public event Action<ChatMessage> OnMessageReceived;

        /// <summary>文件分块传输进度变化事件</summary>
        public event Action<FileTransferProgress> OnFileProgress;

        /// <summary>文件接收顺利完成事件 (发送者昵称, 文件名, 本地完整路径, 字节大小)</summary>
        public event Action<string, string, string, long> OnFileReceived;

        /// <summary>网络引擎底层诊断日志回调</summary>
        public event Action<string> OnLogMessage;

        /// <summary>
        /// 初始化网络服务引擎
        /// </summary>
        /// <param name="nickName">初始昵称</param>
        public NetworkService(string nickName)
        {
            LocalId = Guid.NewGuid().ToString("N");
            NickName = string.IsNullOrEmpty(nickName) ? Environment.UserName : nickName;
            HostName = Environment.MachineName;
            AutoAcceptFiles = true;

            SaveDirectory = DetermineDefaultSaveDirectory();
        }

        /// <summary>
        /// 自动推导可用的文件默认接收存储目录（优先当前目录 ReceivedFiles，权限不足降级用户下载目录）
        /// </summary>
        /// <returns>可写的物理目录路径</returns>
        public static string DetermineDefaultSaveDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string preferredDir = Path.Combine(baseDir, "ReceivedFiles");

            // Test if the application base directory is writable
            bool canWriteBaseDir = false;
            try
            {
                if (!Directory.Exists(preferredDir))
                {
                    Directory.CreateDirectory(preferredDir);
                }
                string probeFile = Path.Combine(preferredDir, ".write_test_" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probeFile, "test");
                File.Delete(probeFile);
                canWriteBaseDir = true;
            }
            catch
            {
                canWriteBaseDir = false;
            }

            if (canWriteBaseDir)
            {
                return preferredDir;
            }

            // Fallback for installed mode (Program Files, etc.): use user's Downloads or Documents
            try
            {
                string userDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "LanChat_Received");
                if (!Directory.Exists(userDownloads))
                {
                    Directory.CreateDirectory(userDownloads);
                }
                return userDownloads;
            }
            catch
            {
                string docsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LanChat", "ReceivedFiles");
                if (!Directory.Exists(docsDir))
                {
                    Directory.CreateDirectory(docsDir);
                }
                return docsDir;
            }
        }

        /// <summary>
        /// 启动网络引擎：发现物理网卡、绑定 TCP/UDP 监听套接字、启动心跳与广播
        /// </summary>
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

        /// <summary>
        /// 释放所有网络资源并广播 BYE 离线下线通知
        /// </summary>
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
