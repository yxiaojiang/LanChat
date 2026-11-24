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

        // 监听线程和出站请求共享已绑定的网卡快照，切网时整体替换。
        private volatile NetworkInterfaceDetail[] _activeInterfaces = new NetworkInterfaceDetail[0];
        private Socket _udpAnySocket;
        private readonly object _discoveryLock = new object();
        private Timer _networkChangeTimer;
        private Timer _presenceCheckTimer;
        private TcpListener _tcpListener;
        private Thread _tcpListenThread;
        private Thread _heartbeatThread;
        private volatile bool _isRunning;

        private readonly Dictionary<string, PeerInfo> _peers = new Dictionary<string, PeerInfo>();
        private readonly object _peerLock = new object();
        // Unix 的 File.Move 先检查目标再重命名。串行提交避免同一进程的两路接收越过检查。
        private static readonly object _receiveCommitLock = new object();

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

        /// <summary>文件接收并落盘完成事件，提供发送者、连接地址和本地文件信息。</summary>
        public event Action<ChatMessage> OnFileReceived;

        /// <summary>网络引擎底层诊断日志回调</summary>
        public event Action<string> OnLogMessage;

        /// <summary>
        /// 初始化网络服务引擎
        /// </summary>
        /// <param name="nickName">初始昵称</param>
        public NetworkService(string nickName) : this(nickName, null)
        {
        }

        /// <summary>初始化通信引擎，沿用调用方选定的数据目录。</summary>
        /// <param name="nickName">初始昵称。</param>
        /// <param name="saveDirectory">文件接收目录，空值使用平台原有默认位置。</param>
        public NetworkService(string nickName, string saveDirectory)
        {
            LocalId = Guid.NewGuid().ToString("N");
            NickName = string.IsNullOrEmpty(nickName) ? Environment.UserName : nickName;
            HostName = Environment.MachineName;
            AutoAcceptFiles = true;

            SaveDirectory = saveDirectory ?? DetermineDefaultSaveDirectory();
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
            Start(null);
        }

        internal void Start(IEnumerable<IPAddress> knownAddresses)
        {
            lock (_discoveryLock)
            {
                _isRunning = true;
                InitTcpListener();
                _presenceCheckTimer = new Timer(CheckRefreshedPeers);
                try
                {
                    _networkChangeTimer = new Timer(RebindAfterNetworkChange);
                    NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
                }
                catch (Exception ex)
                {
                    Log("监听网卡变化失败: " + ex.Message);
                }
            }

            _heartbeatThread = new Thread(HeartbeatLoop) { IsBackground = true };
            _heartbeatThread.Start();

            RefreshPeers(knownAddresses);
        }

        private static string CommitReceivedFile(string temporary, string folder, string fileName)
        {
            string destination = Path.Combine(folder, fileName);

            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            lock (_receiveCommitLock)
            {
                for (int count = 1; ; count++)
                {
                    try
                    {
                        File.Move(temporary, destination);
                        return destination;
                    }
                    catch (IOException)
                    {
                        if (!File.Exists(destination) && !Directory.Exists(destination)) throw;
                        destination = Path.Combine(folder, string.Format("{0} ({1}){2}", nameWithoutExt, count, ext));
                    }
                }
            }
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
            lock (_discoveryLock)
            {
                if (!_isRunning) return;
                _isRunning = false;
                if (_networkChangeTimer != null)
                {
                    // 网卡事件注销失败时仍须释放计时器和通信套接字。
                    try { NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged; }
                    catch { }
                    _networkChangeTimer.Dispose();
                }
                if (_presenceCheckTimer != null) _presenceCheckTimer.Dispose();
                SendPresence("BYE", _activeInterfaces, null);
                foreach (var adapter in _activeInterfaces)
                    if (adapter.UdpSocket != null) adapter.UdpSocket.Close();
                if (_udpAnySocket != null) _udpAnySocket.Close();
            }

            try
            {
                if (_tcpListener != null) _tcpListener.Stop();
            }
            catch { }
        }
    }
}
