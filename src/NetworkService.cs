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

            SaveDirectory = DetermineDefaultSaveDirectory();
        }

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
