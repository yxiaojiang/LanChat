using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace LanChat
{
    public class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const string BROADCAST_TARGET = "📢 局域网全员群聊";
        private NetworkService _netService;

        // Chat histories by conversation key: "BROADCAST" for group, peerId for private
        private readonly Dictionary<string, List<ChatMessage>> _chatHistories = new Dictionary<string, List<ChatMessage>>();
        private string _activeChatKey = "BROADCAST";
        private PeerInfo _selectedPeer = null;

        // UI Layout Panels
        private Panel _sidebarPanel;
        private Panel _sessionsPanel;
        private Panel _chatPanel;

        // Sidebar Controls
        private Button _btnAvatar;
        private WeChatNavButton _btnNavChat;
        private WeChatNavButton _btnNavContacts;
        private WeChatNavButton _btnNavFolder;
        private WeChatNavButton _btnNavRefresh;
        private WeChatNavButton _btnNavAddIp;
        private WeChatNavButton _btnNavMenu;

        // Sessions Column Controls
        private Panel _searchHeaderPanel;
        private Panel _searchBoxContainer;
        private TextBox _txtSearch;
        private WeChatIconButton _btnAddSession;
        private ListBox _listSessions;
        private List<object> _allSessionItems = new List<object>();

        // Chat Header Controls
        private Panel _chatHeader;
        private Label _lblChatTitle;
        private Label _lblChatSubtitle;
        private WeChatIconButton _btnHeaderRefresh;
        private WeChatIconButton _btnHeaderAddIp;
        private WeChatIconButton _btnHeaderFolder;
        private WeChatIconButton _btnHeaderClear;

        // File Progress Banner
        private Panel _progressPanel;
        private Label _lblProgressInfo;
        private ProgressBar _progressBar;

        // Chat History View
        private WeChatChatView _chatView;

        // Input Toolbar Controls
        private Panel _toolPanel;
        private WeChatIconButton _btnToolEmoji;
        private WeChatIconButton _btnToolSendFile;
        private WeChatIconButton _btnToolFolder;
        private WeChatIconButton _btnToolClear;
        private Label _lblAutoAccept;

        // Input Area Controls
        private Panel _inputPanel;
        private TextBox _txtInput;
        private Panel _bottomSendPanel;
        private Label _lblSendTip;
        private Button _btnSend;

        // Navigation state: 0 = Chat, 1 = Contacts
        private int _currentNavTab = 0;

        // System Tray
        private NotifyIcon _trayIcon;
        private bool _isExplicitExit = false;
        private bool _hasShownTrayBalloon = false;

        public MainForm()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
                else
                {
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    if (File.Exists(exePath))
                    {
                        Icon exeIcon = Icon.ExtractAssociatedIcon(exePath);
                        if (exeIcon != null) this.Icon = exeIcon;
                    }
                }
            }
            catch { }

            InitializeComponent();
            InitializeTrayIcon();
            InitializeNetwork();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                // Enable Windows native dark mode title bar (Windows 10 1809+ / Windows 11)
                int darkMode = 1;
                DwmSetWindowAttribute(this.Handle, 20, ref darkMode, sizeof(int));
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "LanChat";
            this.Size = new Size(1024, 680);
            this.MinimumSize = new Size(860, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular);
            this.BackColor = Color.FromArgb(30, 30, 31);
            this.DoubleBuffered = true;
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            // ==========================================
            // 1. Left Sidebar (Dock / Rail) - 54px Width
            // ==========================================
            _sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 54,
                BackColor = Color.FromArgb(37, 37, 38) // WeChat #252526
            };

            // Top Avatar (36x36 rounded square)
            _btnAvatar = new Button
            {
                Size = new Size(36, 36),
                Location = new Point(9, 14),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _btnAvatar.FlatAppearance.BorderSize = 0;
            _btnAvatar.Paint += BtnAvatar_Paint;
            _btnAvatar.Click += BtnAvatar_Click;

            // Nav: Chat (Active by default, WeChat green solid speech bubble)
            _btnNavChat = new WeChatNavButton(WeChatIconType.Chat, "聊天", true)
            {
                Location = new Point(9, 66)
            };
            _btnNavChat.Click += (s, e) => SwitchNavTab(0);

            // Nav: Contacts (Outlined person + list card lines, exactly matching screenshot)
            _btnNavContacts = new WeChatNavButton(WeChatIconType.Contacts, "通讯录", false)
            {
                Location = new Point(9, 114)
            };
            _btnNavContacts.Click += (s, e) => SwitchNavTab(1);

            // Nav: Received Folder
            _btnNavFolder = new WeChatNavButton(WeChatIconType.Folder, "接收文件夹", false)
            {
                Location = new Point(9, 162)
            };
            _btnNavFolder.Click += (s, e) => OpenReceivedFolder();

            // Nav: Refresh / Radar
            _btnNavRefresh = new WeChatNavButton(WeChatIconType.Refresh, "刷新好友", false)
            {
                Location = new Point(9, 210)
            };
            _btnNavRefresh.Click += (s, e) => RefreshPeersWithNotice();

            // Nav: Add IP
            _btnNavAddIp = new WeChatNavButton(WeChatIconType.Add, "添加好友 IP", false)
            {
                Location = new Point(9, 258)
            };
            _btnNavAddIp.Click += (s, e) => BtnAddIp_Click(s, e);

            // Nav: Menu / Settings at bottom (3 parallel lines)
            _btnNavMenu = new WeChatNavButton(WeChatIconType.Menu, "设置与更多", false)
            {
                Location = new Point(9, _sidebarPanel.Height - 48),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            _btnNavMenu.Click += BtnSettings_Click;

            _sidebarPanel.Controls.AddRange(new Control[] {
                _btnAvatar, _btnNavChat, _btnNavContacts, _btnNavFolder, _btnNavRefresh, _btnNavAddIp, _btnNavMenu
            });

            // ==========================================
            // 2. Middle Sessions Column - 250px Width
            // ==========================================
            _sessionsPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = Color.FromArgb(47, 47, 48) // WeChat list #2f2f30
            };

            // Top Search Bar Panel (Height: 52px)
            _searchHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(47, 47, 48),
                Padding = new Padding(12, 12, 12, 12)
            };

            _searchBoxContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(38, 38, 40),
                Padding = new Padding(8, 4, 8, 4)
            };
            _searchBoxContainer.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(Color.FromArgb(56, 56, 58), 1f))
                {
                    DrawRoundedRectangle(e.Graphics, pen, new Rectangle(0, 0, _searchBoxContainer.Width - 1, _searchBoxContainer.Height - 1), 4);
                }
                // Draw vector magnifying glass icon
                WeChatIcon.DrawSearchIcon(e.Graphics, new Rectangle(6, 6, 14, 14), Color.FromArgb(118, 118, 120));
            };

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Right,
                Width = _searchBoxContainer.Width - 30,
                BackColor = Color.FromArgb(38, 38, 40),
                ForeColor = Color.FromArgb(220, 220, 220),
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei", 9f),
                Margin = new Padding(0)
            };
            _searchBoxContainer.Resize += (s, e) => {
                _txtSearch.Width = Math.Max(50, _searchBoxContainer.Width - 30);
                _txtSearch.Location = new Point(26, (_searchBoxContainer.Height - _txtSearch.Height) / 2);
            };
            _txtSearch.TextChanged += TxtSearch_TextChanged;
            _searchBoxContainer.Controls.Add(_txtSearch);

            _btnAddSession = new WeChatIconButton(WeChatIconType.Add, "添加好友 IP / 扫描", 26)
            {
                Dock = DockStyle.Right,
                Width = 26,
                Margin = new Padding(6, 0, 0, 0)
            };
            _btnAddSession.Click += BtnAddSession_Click;

            Panel searchWrap = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            searchWrap.Controls.Add(_searchBoxContainer);

            Panel addWrap = new Panel { Dock = DockStyle.Right, Width = 32, Padding = new Padding(6, 0, 0, 0), BackColor = Color.Transparent };
            addWrap.Controls.Add(_btnAddSession);

            _searchHeaderPanel.Controls.Add(searchWrap);
            _searchHeaderPanel.Controls.Add(addWrap);

            // Sessions ListBox (Height: 64px per item, OwnerDrawn)
            _listSessions = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(47, 47, 48),
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei", 9.5f),
                ItemHeight = 64,
                DrawMode = DrawMode.OwnerDrawFixed,
                IntegralHeight = false
            };
            _listSessions.DrawItem += ListSessions_DrawItem;
            _listSessions.SelectedIndexChanged += ListSessions_SelectedIndexChanged;

            _sessionsPanel.Controls.Add(_listSessions);
            _sessionsPanel.Controls.Add(_searchHeaderPanel);

            // ==========================================
            // 3. Right Chat Panel - WeChat Dark Mode
            // ==========================================
            _chatPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 31) // WeChat main chat #1e1e1f
            };

            // Top Header Bar (Height: 52px)
            _chatHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(30, 30, 31),
                Padding = new Padding(18, 8, 16, 8)
            };
            _chatHeader.Paint += (s, e) => {
                using (Pen pen = new Pen(Color.FromArgb(40, 40, 42)))
                {
                    e.Graphics.DrawLine(pen, 0, _chatHeader.Height - 1, _chatHeader.Width, _chatHeader.Height - 1);
                }
            };

            _lblChatTitle = new Label
            {
                Text = BROADCAST_TARGET,
                Font = new Font("Microsoft YaHei", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(236, 236, 236),
                AutoSize = true,
                Location = new Point(18, 9)
            };

            _lblChatSubtitle = new Label
            {
                Text = "局域网全员广播消息通道",
                Font = new Font("Microsoft YaHei", 8.5f),
                ForeColor = Color.FromArgb(120, 120, 124),
                AutoSize = true,
                Location = new Point(19, 30)
            };

            FlowLayoutPanel headerActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0)
            };

            _btnHeaderRefresh = new WeChatIconButton(WeChatIconType.Refresh, "刷新局域网好友", 28);
            _btnHeaderRefresh.Click += (s, e) => RefreshPeersWithNotice();

            _btnHeaderAddIp = new WeChatIconButton(WeChatIconType.Add, "添加好友 IP", 28);
            _btnHeaderAddIp.Click += (s, e) => BtnAddIp_Click(s, e);

            _btnHeaderFolder = new WeChatIconButton(WeChatIconType.Folder, "打开接收文件夹", 28);
            _btnHeaderFolder.Click += (s, e) => OpenReceivedFolder();

            _btnHeaderClear = new WeChatIconButton(WeChatIconType.Clear, "清屏", 28);
            _btnHeaderClear.Click += (s, e) => ClearCurrentChat();

            headerActions.Controls.AddRange(new Control[] {
                _btnHeaderRefresh, _btnHeaderAddIp, _btnHeaderFolder, _btnHeaderClear
            });

            _chatHeader.Controls.Add(_lblChatTitle);
            _chatHeader.Controls.Add(_lblChatSubtitle);
            _chatHeader.Controls.Add(headerActions);

            // Collapsible Transfer Progress Banner
            _progressPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = Color.FromArgb(36, 36, 38),
                Visible = false,
                Padding = new Padding(18, 6, 18, 6)
            };
            _lblProgressInfo = new Label
            {
                Text = "准备传输...",
                Font = new Font("Microsoft YaHei", 8.5f),
                ForeColor = Color.FromArgb(7, 193, 96),
                AutoSize = true,
                Location = new Point(18, 6)
            };
            _progressBar = new ProgressBar
            {
                Location = new Point(18, 25),
                Height = 10,
                Width = 500,
                Style = ProgressBarStyle.Continuous
            };
            _progressPanel.Controls.Add(_lblProgressInfo);
            _progressPanel.Controls.Add(_progressBar);
            _progressPanel.Resize += (s, e) => {
                _progressBar.Width = Math.Max(100, _progressPanel.Width - 36);
            };

            // Custom-Drawn WeChat Chat History View
            _chatView = new WeChatChatView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 31)
            };
            _chatView.OnFileActionClick += HandleFileActionClick;

            // Input Toolbar (Emoji, Send File, Folder, Clear)
            _toolPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                BackColor = Color.FromArgb(30, 30, 31),
                Padding = new Padding(16, 4, 16, 4)
            };
            _toolPanel.Paint += (s, e) => {
                using (Pen pen = new Pen(Color.FromArgb(40, 40, 42)))
                {
                    e.Graphics.DrawLine(pen, 0, 0, _toolPanel.Width, 0);
                }
            };

            _btnToolEmoji = new WeChatIconButton(WeChatIconType.Emoji, "表情", 28);
            _btnToolEmoji.Click += (s, e) => ShowEmojiPopup();

            _btnToolSendFile = new WeChatIconButton(WeChatIconType.Folder, "发送文件", 28);
            _btnToolSendFile.Click += (s, e) => BtnSendFile_Click(s, e);

            _btnToolFolder = new WeChatIconButton(WeChatIconType.Box, "接收目录", 28);
            _btnToolFolder.Click += (s, e) => OpenReceivedFolder();

            _btnToolClear = new WeChatIconButton(WeChatIconType.Clear, "清屏", 28);
            _btnToolClear.Click += (s, e) => ClearCurrentChat();

            _lblAutoAccept = new Label
            {
                Text = "⚡ 自动接收: 开",
                ForeColor = Color.FromArgb(7, 193, 96),
                Font = new Font("Microsoft YaHei", 8.5f),
                AutoSize = true,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 6, 8, 0)
            };
            _lblAutoAccept.Click += (s, e) => {
                _netService.AutoAcceptFiles = !_netService.AutoAcceptFiles;
                _lblAutoAccept.Text = _netService.AutoAcceptFiles ? "⚡ 自动接收: 开" : "⏸ 自动接收: 关";
                _lblAutoAccept.ForeColor = _netService.AutoAcceptFiles ? Color.FromArgb(7, 193, 96) : Color.FromArgb(140, 140, 140);
            };

            FlowLayoutPanel toolFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            toolFlow.Controls.AddRange(new Control[] {
                _btnToolEmoji, _btnToolSendFile, _btnToolFolder, _btnToolClear
            });

            _toolPanel.Controls.Add(toolFlow);
            _toolPanel.Controls.Add(_lblAutoAccept);

            // Multiline Text Input Area
            _inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 88,
                BackColor = Color.FromArgb(30, 30, 31),
                Padding = new Padding(18, 4, 18, 4)
            };

            _txtInput = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.None,
                Font = new Font("Microsoft YaHei", 10f),
                BackColor = Color.FromArgb(30, 30, 31),
                ForeColor = Color.FromArgb(230, 230, 230),
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                AllowDrop = true
            };
            _txtInput.KeyDown += TxtInput_KeyDown;
            _txtInput.TextChanged += TxtInput_TextChanged;
            _txtInput.DragEnter += MainForm_DragEnter;
            _txtInput.DragDrop += MainForm_DragDrop;

            _inputPanel.Controls.Add(_txtInput);

            // Bottom Action Bar (Send Button & Tip)
            _bottomSendPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                BackColor = Color.FromArgb(30, 30, 31),
                Padding = new Padding(18, 4, 18, 8)
            };

            _lblSendTip = new Label
            {
                Text = "按 Enter 发送，Ctrl+Enter 换行",
                Font = new Font("Microsoft YaHei", 8f),
                ForeColor = Color.FromArgb(90, 90, 92),
                AutoSize = true,
                Location = new Point(18, 12)
            };

            _btnSend = new Button
            {
                Text = "发送 (S)",
                Size = new Size(74, 28),
                Location = new Point(_bottomSendPanel.Width - 92, 4),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(42, 42, 44),
                ForeColor = Color.FromArgb(120, 120, 120),
                Font = new Font("Microsoft YaHei", 9f),
                Cursor = Cursors.Hand
            };
            _btnSend.FlatAppearance.BorderSize = 0;
            _btnSend.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(_btnSend.BackColor))
                {
                    DrawRoundedRectangle(e.Graphics, b, new Rectangle(0, 0, _btnSend.Width - 1, _btnSend.Height - 1), 4);
                }
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using (SolidBrush tb = new SolidBrush(_btnSend.ForeColor))
                {
                    e.Graphics.DrawString(_btnSend.Text, _btnSend.Font, tb, new Rectangle(0, 0, _btnSend.Width, _btnSend.Height), sf);
                }
            };
            _btnSend.Click += BtnSend_Click;

            _bottomSendPanel.Controls.Add(_lblSendTip);
            _bottomSendPanel.Controls.Add(_btnSend);

            // Assemble Panels
            _chatPanel.Controls.Add(_chatView);
            _chatPanel.Controls.Add(_progressPanel);
            _chatPanel.Controls.Add(_chatHeader);
            _chatPanel.Controls.Add(_toolPanel);
            _chatPanel.Controls.Add(_inputPanel);
            _chatPanel.Controls.Add(_bottomSendPanel);

            this.Controls.Add(_chatPanel);
            this.Controls.Add(_sessionsPanel);
            this.Controls.Add(_sidebarPanel);

            this.FormClosing += MainForm_FormClosing;
        }

        #region Network & Init

        private void InitializeNetwork()
        {
            try
            {
                string initialNick = Environment.UserName;
                _netService = new NetworkService(initialNick);
                _netService.OnPeerJoined += (p) => SafeInvoke(() => PeerJoined(p));
                _netService.OnPeerUpdated += (p) => SafeInvoke(() => PeerUpdated(p));
                _netService.OnPeerLeft += (pid) => SafeInvoke(() => PeerLeft(pid));
                _netService.OnMessageReceived += (msg) => SafeInvoke(() => HandleReceivedMessage(msg));
                _netService.OnFileProgress += (prog) => SafeInvoke(() => HandleFileProgress(prog));
                _netService.OnFileReceived += (nick, fn, path, sz) => SafeInvoke(() => HandleFileReceived(nick, fn, path, sz));
                _netService.OnLogMessage += (log) => { };

                _netService.Start();

                IPAddress primaryIp = _netService.GetPrimaryPhysicalIp();
                _btnAvatar.Invalidate();

                AppendChatSystem("LanChat 启动成功！已开始在局域网内搜索好友...");
                AppendChatSystem(string.Format("物理网卡绑定 IP: {0}:{1}", primaryIp, _netService.LocalTcpPort));
                AppendChatSystem(string.Format("文件接收目录: {0}", _netService.SaveDirectory));

                RefreshPeerListUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "启动网络服务失败: " + ex.Message, "网络错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SwitchNavTab(int tabIndex)
        {
            _currentNavTab = tabIndex;
            _btnNavChat.IsActive = (tabIndex == 0);
            _btnNavContacts.IsActive = (tabIndex == 1);
            RefreshPeerListUI();
        }

        private void BtnAvatar_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Rounded rectangle avatar (radius 4px)
            Rectangle rect = new Rectangle(0, 0, 35, 35);
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(7, 193, 96)))
            {
                DrawRoundedRectangle(e.Graphics, brush, rect, 4);
            }

            // Initials
            string nick = _netService != null ? _netService.NickName : Environment.UserName;
            string initial = string.IsNullOrEmpty(nick) ? "我" : nick.Substring(0, 1).ToUpperInvariant();
            using (Font f = new Font("Microsoft YaHei", 11f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(initial, f, textBrush, rect, sf);
            }

            // Green online indicator dot
            Rectangle dotRect = new Rectangle(24, 24, 9, 9);
            using (SolidBrush dotBg = new SolidBrush(Color.FromArgb(37, 37, 38)))
            using (SolidBrush dotFg = new SolidBrush(Color.FromArgb(7, 193, 96)))
            {
                e.Graphics.FillEllipse(dotBg, new Rectangle(23, 23, 11, 11));
                e.Graphics.FillEllipse(dotFg, dotRect);
            }
        }

        private void BtnAvatar_Click(object sender, EventArgs e)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new WeChatDarkMenuRenderer();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(44, 44, 46);
            menu.ForeColor = Color.FromArgb(235, 235, 235);

            string myNick = _netService != null ? _netService.NickName : Environment.UserName;
            IPAddress ip = _netService != null ? _netService.GetPrimaryPhysicalIp() : IPAddress.Loopback;
            int port = _netService != null ? _netService.LocalTcpPort : 38889;

            menu.Items.Add(new ToolStripMenuItem("👤 我的昵称: " + myNick) { Enabled = false });
            menu.Items.Add(new ToolStripMenuItem(string.Format("🌐 本机地址: {0}:{1}", ip, port)) { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("✏️ 修改昵称...", null, (s, ev) => ShowChangeNickDialog());
            menu.Items.Add("📂 打开接收文件夹", null, (s, ev) => OpenReceivedFolder());

            menu.Show(_btnAvatar, new Point(_btnAvatar.Width + 4, 0));
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new WeChatDarkMenuRenderer();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.BackColor = Color.FromArgb(44, 44, 46);
            menu.ForeColor = Color.FromArgb(235, 235, 235);

            menu.Items.Add("✏️ 修改我的昵称...", null, (s, ev) => ShowChangeNickDialog());
            var autoItem = new ToolStripMenuItem("⚡ 自动接收文件") { Checked = _netService != null && _netService.AutoAcceptFiles };
            autoItem.Click += (s, ev) => {
                _netService.AutoAcceptFiles = !_netService.AutoAcceptFiles;
                _lblAutoAccept.Text = _netService.AutoAcceptFiles ? "⚡ 自动接收: 开" : "⏸ 自动接收: 关";
                _lblAutoAccept.ForeColor = _netService.AutoAcceptFiles ? Color.FromArgb(7, 193, 96) : Color.FromArgb(140, 140, 140);
            };
            menu.Items.Add(autoItem);
            menu.Items.Add("📁 打开接收文件夹", null, (s, ev) => OpenReceivedFolder());
            menu.Items.Add("🧹 清空当前聊天记录", null, (s, ev) => ClearCurrentChat());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("ℹ️ 关于 LanChat (WeChat 风格版)", null, (s, ev) => {
                MessageBox.Show(this, "LanChat v2.0\n局域网秒级极速通讯与文件互传\nUI: WeChat Desktop 风格 (高精度微距对标)\n零外部运行环境依赖", "关于 LanChat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("🚪 退出 LanChat", null, (s, ev) => {
                _isExplicitExit = true;
                this.Close();
            });

            menu.Show(_btnNavMenu, new Point(_btnNavMenu.Width + 4, -80));
        }

        private void ShowChangeNickDialog()
        {
            string currentNick = _netService != null ? _netService.NickName : Environment.UserName;
            using (Form prompt = new Form())
            {
                if (this.Icon != null) prompt.Icon = this.Icon;
                prompt.Text = "修改我的昵称";
                prompt.Size = new Size(360, 160);
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
                prompt.BackColor = Color.FromArgb(44, 44, 46);
                prompt.ForeColor = Color.White;

                Label lbl = new Label { Text = "请输入新的局域网昵称:", Location = new Point(16, 16), AutoSize = true };
                TextBox txt = new TextBox { Text = currentNick, Location = new Point(18, 42), Width = 308, BackColor = Color.FromArgb(32, 32, 34), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
                Button btnOk = new Button { Text = "保存", Location = new Point(156, 78), Size = new Size(80, 28), DialogResult = DialogResult.OK, BackColor = Color.FromArgb(7, 193, 96), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                Button btnCancel = new Button { Text = "取消", Location = new Point(246, 78), Size = new Size(80, 28), DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(60, 60, 62), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };

                prompt.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                prompt.AcceptButton = btnOk;
                prompt.CancelButton = btnCancel;

                if (prompt.ShowDialog(this) == DialogResult.OK)
                {
                    string newNick = txt.Text.Trim();
                    if (!string.IsNullOrEmpty(newNick) && _netService != null)
                    {
                        _netService.NickName = newNick;
                        _netService.BroadcastPresence("HELLO");
                        _btnAvatar.Invalidate();
                        AppendChatSystem("您的昵称已更改为: " + newNick);
                    }
                }
            }
        }

        private void RefreshPeersWithNotice()
        {
            if (_netService != null)
            {
                _netService.BroadcastPresence("HELLO");
                AppendChatSystem("已向局域网广播并扫描同网段好友...");
            }
        }

        private void OpenReceivedFolder()
        {
            try
            {
                if (_netService != null)
                {
                    if (!Directory.Exists(_netService.SaveDirectory))
                    {
                        Directory.CreateDirectory(_netService.SaveDirectory);
                    }
                    Process.Start("explorer.exe", _netService.SaveDirectory);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开文件夹失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Peer & Sessions Management

        private void PeerJoined(PeerInfo peer)
        {
            RefreshPeerListUI();
            AppendChatSystem(string.Format("发现好友上线: {0} ({1} - {2})", peer.NickName, peer.HostName, peer.IP));
        }

        private void PeerUpdated(PeerInfo peer)
        {
            RefreshPeerListUI();
        }

        private void PeerLeft(string peerId)
        {
            RefreshPeerListUI();
        }

        private void RefreshPeerListUI()
        {
            var peers = _netService != null ? _netService.GetActivePeers() : new List<PeerInfo>();

            _allSessionItems.Clear();
            _allSessionItems.Add(BROADCAST_TARGET);

            foreach (var p in peers)
            {
                _allSessionItems.Add(p);
            }

            ApplyFilterAndRenderList();
        }

        private void ApplyFilterAndRenderList()
        {
            string filter = _txtSearch.Text.Trim().ToLowerInvariant();

            object currentSelected = _listSessions.SelectedItem;
            _listSessions.BeginUpdate();
            _listSessions.Items.Clear();

            foreach (var item in _allSessionItems)
            {
                if (string.IsNullOrEmpty(filter))
                {
                    _listSessions.Items.Add(item);
                }
                else
                {
                    if (item is string)
                    {
                        if (((string)item).ToLowerInvariant().Contains(filter))
                            _listSessions.Items.Add(item);
                    }
                    else if (item is PeerInfo)
                    {
                        PeerInfo p = (PeerInfo)item;
                        string nick = (p.NickName ?? "").ToLowerInvariant();
                        string ip = (p.IP != null ? p.IP.ToString() : "").ToLowerInvariant();
                        string host = (p.HostName ?? "").ToLowerInvariant();
                        if (nick.Contains(filter) || ip.Contains(filter) || host.Contains(filter))
                        {
                            _listSessions.Items.Add(p);
                        }
                    }
                }
            }

            // Restore selection or select 0
            if (currentSelected != null)
            {
                int index = -1;
                for (int i = 0; i < _listSessions.Items.Count; i++)
                {
                    if (_listSessions.Items[i].ToString() == currentSelected.ToString())
                    {
                        index = i;
                        break;
                    }
                }
                if (index >= 0) _listSessions.SelectedIndex = index;
                else if (_listSessions.Items.Count > 0) _listSessions.SelectedIndex = 0;
            }
            else if (_listSessions.Items.Count > 0)
            {
                _listSessions.SelectedIndex = 0;
            }

            _listSessions.EndUpdate();
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            ApplyFilterAndRenderList();
        }

        private void BtnAddSession_Click(object sender, EventArgs e)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new WeChatDarkMenuRenderer();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(44, 44, 46);
            menu.ForeColor = Color.FromArgb(235, 235, 235);

            menu.Items.Add("➕ 手动添加好友 IP...", null, (s, ev) => BtnAddIp_Click(s, ev));
            menu.Items.Add("🔄 扫描局域网好友", null, (s, ev) => RefreshPeersWithNotice());
            menu.Items.Add("📂 打开接收文件夹", null, (s, ev) => OpenReceivedFolder());

            menu.Show(_btnAddSession, new Point(0, _btnAddSession.Height + 2));
        }

        private void ListSessions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_listSessions.SelectedItem == null) return;

            if (_listSessions.SelectedItem is string && (string)_listSessions.SelectedItem == BROADCAST_TARGET)
            {
                _selectedPeer = null;
                _activeChatKey = "BROADCAST";
                int count = _netService != null ? _netService.GetActivePeers().Count : 0;
                _lblChatTitle.Text = string.Format("📢 局域网全员群聊 ({0}人在线)", count);
                _lblChatSubtitle.Text = "局域网全员广播消息通道";
            }
            else if (_listSessions.SelectedItem is PeerInfo)
            {
                _selectedPeer = (PeerInfo)_listSessions.SelectedItem;
                _activeChatKey = _selectedPeer.Id;
                _selectedPeer.UnreadCount = 0;
                _lblChatTitle.Text = string.Format("👤 {0}", _selectedPeer.NickName);
                _lblChatSubtitle.Text = string.Format("{0} - {1}:{2}", _selectedPeer.HostName, _selectedPeer.IP, _selectedPeer.TcpPort);
            }

            SwitchActiveChatView();
            _listSessions.Invalidate();
        }

        private void SwitchActiveChatView()
        {
            List<ChatMessage> list;
            lock (_chatHistories)
            {
                if (!_chatHistories.TryGetValue(_activeChatKey, out list))
                {
                    list = new List<ChatMessage>();
                    _chatHistories[_activeChatKey] = list;
                }
            }
            _chatView.SetMessages(list);
        }

        private void ListSessions_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _listSessions.Items.Count) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            // WeChat Green selection highlight as in screenshot: #07c160
            Color bgColor = isSelected ? Color.FromArgb(7, 193, 96) : Color.FromArgb(47, 47, 48);

            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
            }

            object item = _listSessions.Items[e.Index];
            Rectangle r = e.Bounds;

            if (item is string)
            {
                // Group Chat item avatar
                Rectangle avatarRect = new Rectangle(r.Left + 12, r.Top + 12, 40, 40);
                using (SolidBrush avBrush = new SolidBrush(isSelected ? Color.FromArgb(4, 150, 74) : Color.FromArgb(32, 128, 70)))
                {
                    DrawRoundedRectangle(e.Graphics, avBrush, avatarRect, 4);
                }

                using (Font iconFont = new Font("Microsoft YaHei", 12f))
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("📢", iconFont, textBrush, avatarRect, sf);
                }

                // Name
                using (Font titleFont = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold))
                using (SolidBrush titleBrush = new SolidBrush(Color.White))
                {
                    int onlineCount = _netService != null ? _netService.GetActivePeers().Count : 0;
                    string title = string.Format("局域网全员群聊 ({0})", onlineCount);
                    e.Graphics.DrawString(title, titleFont, titleBrush, new PointF(r.Left + 60, r.Top + 14));
                }

                // Last message snippet
                string snippet = "点击发送全网广播消息";
                lock (_chatHistories)
                {
                    List<ChatMessage> list;
                    if (_chatHistories.TryGetValue("BROADCAST", out list) && list.Count > 0)
                    {
                        ChatMessage last = list[list.Count - 1];
                        snippet = last.SenderNick + ": " + (last.IsFile ? "[文件] " + last.FileName : last.Content);
                    }
                }

                using (Font subFont = new Font("Microsoft YaHei", 8.5f))
                using (SolidBrush subBrush = new SolidBrush(isSelected ? Color.FromArgb(220, 248, 230) : Color.FromArgb(145, 145, 145)))
                {
                    if (snippet.Length > 20) snippet = snippet.Substring(0, 19) + "...";
                    e.Graphics.DrawString(snippet, subFont, subBrush, new PointF(r.Left + 60, r.Top + 36));
                }
            }
            else if (item is PeerInfo)
            {
                PeerInfo peer = (PeerInfo)item;

                // Round Avatar (radius 4px)
                Rectangle avatarRect = new Rectangle(r.Left + 12, r.Top + 12, 40, 40);
                Color avColor = GetAvatarColor(peer.NickName);
                using (SolidBrush avBrush = new SolidBrush(avColor))
                {
                    DrawRoundedRectangle(e.Graphics, avBrush, avatarRect, 4);
                }

                string initial = string.IsNullOrEmpty(peer.NickName) ? "友" : peer.NickName.Substring(0, 1).ToUpperInvariant();
                using (Font f = new Font("Microsoft YaHei", 11f, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(initial, f, textBrush, avatarRect, sf);
                }

                // Online green dot
                Rectangle dotRect = new Rectangle(r.Left + 42, r.Top + 42, 10, 10);
                using (SolidBrush dotBg = new SolidBrush(bgColor))
                using (SolidBrush dotFg = new SolidBrush(Color.FromArgb(7, 193, 96)))
                {
                    e.Graphics.FillEllipse(dotBg, new Rectangle(r.Left + 41, r.Top + 41, 12, 12));
                    e.Graphics.FillEllipse(dotFg, dotRect);
                }

                // Peer Nickname
                using (Font titleFont = new Font("Microsoft YaHei", 9.5f, isSelected ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush titleBrush = new SolidBrush(Color.White))
                {
                    e.Graphics.DrawString(peer.NickName ?? "未知用户", titleFont, titleBrush, new PointF(r.Left + 60, r.Top + 13));
                }

                // Time or IP on right
                using (Font timeFont = new Font("Microsoft YaHei", 8f))
                using (SolidBrush timeBrush = new SolidBrush(isSelected ? Color.FromArgb(220, 245, 220) : Color.FromArgb(130, 130, 130)))
                {
                    string timeStr = peer.LastMessageTime != DateTime.MinValue ? peer.LastMessageTime.ToString("MM/dd") : "";
                    if (!string.IsNullOrEmpty(timeStr))
                    {
                        e.Graphics.DrawString(timeStr, timeFont, timeBrush, new PointF(r.Right - 46, r.Top + 14));
                    }
                }

                // Peer Snippet
                string snippet = peer.LastMessage;
                if (string.IsNullOrEmpty(snippet))
                {
                    snippet = string.Format("{0} ({1})", peer.HostName, peer.IP);
                }
                using (Font subFont = new Font("Microsoft YaHei", 8.5f))
                using (SolidBrush subBrush = new SolidBrush(isSelected ? Color.FromArgb(220, 248, 230) : Color.FromArgb(145, 145, 145)))
                {
                    if (snippet.Length > 22) snippet = snippet.Substring(0, 21) + "...";
                    e.Graphics.DrawString(snippet, subFont, subBrush, new PointF(r.Left + 60, r.Top + 36));
                }
            }

            // Divider line
            using (Pen divPen = new Pen(Color.FromArgb(40, 40, 42)))
            {
                e.Graphics.DrawLine(divPen, r.Left + 60, r.Bottom - 1, r.Right, r.Bottom - 1);
            }
        }

        private Color GetAvatarColor(string name)
        {
            if (string.IsNullOrEmpty(name)) return Color.FromArgb(59, 130, 246);
            int hash = Math.Abs(name.GetHashCode());
            Color[] colors = new Color[]
            {
                Color.FromArgb(59, 130, 246),  // Blue
                Color.FromArgb(139, 92, 246),  // Purple
                Color.FromArgb(236, 72, 153),  // Pink
                Color.FromArgb(16, 185, 129),  // Emerald
                Color.FromArgb(245, 158, 11),  // Amber
                Color.FromArgb(14, 165, 233),  // Sky
                Color.FromArgb(168, 85, 247)   // Violet
            };
            return colors[hash % colors.Length];
        }

        #endregion

        #region Messaging & Input

        private void TxtInput_TextChanged(object sender, EventArgs e)
        {
            bool hasText = !string.IsNullOrEmpty(_txtInput.Text.Trim());
            _btnSend.BackColor = hasText ? Color.FromArgb(7, 193, 96) : Color.FromArgb(42, 42, 44);
            _btnSend.ForeColor = hasText ? Color.White : Color.FromArgb(120, 120, 120);
            _btnSend.Invalidate();
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift && !e.Control)
            {
                e.SuppressKeyPress = true;
                SendMessage();
            }
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            SendMessage();
        }

        private void SendMessage()
        {
            string text = _txtInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _txtInput.Clear();

            string myNick = _netService != null ? _netService.NickName : Environment.UserName;

            if (_selectedPeer == null)
            {
                // Broadcast
                var peers = _netService != null ? _netService.GetActivePeers() : new List<PeerInfo>();
                ChatMessage msg = new ChatMessage
                {
                    SenderId = _netService != null ? _netService.LocalId : "",
                    SenderNick = myNick,
                    TargetId = null,
                    Content = text,
                    Timestamp = DateTime.Now,
                    IsSelf = true,
                    IsBroadcast = true
                };

                AddMessageToHistory("BROADCAST", msg);

                new System.Threading.Thread(() => {
                    foreach (var peer in peers)
                    {
                        _netService.SendTextMessage(peer, text, true);
                    }
                }) { IsBackground = true }.Start();
            }
            else
            {
                // Send to selected peer
                PeerInfo target = _selectedPeer;
                ChatMessage msg = new ChatMessage
                {
                    SenderId = _netService != null ? _netService.LocalId : "",
                    SenderNick = myNick,
                    TargetId = target.Id,
                    Content = text,
                    Timestamp = DateTime.Now,
                    IsSelf = true,
                    IsBroadcast = false
                };

                target.LastMessage = text;
                target.LastMessageTime = DateTime.Now;
                AddMessageToHistory(target.Id, msg);

                new System.Threading.Thread(() => {
                    bool ok = _netService.SendTextMessage(target, text, false);
                    if (!ok)
                    {
                        SafeInvoke(() => AppendChatSystem(string.Format("向 {0} 发送私聊消息失败，对方可能已离线", target.NickName)));
                    }
                }) { IsBackground = true }.Start();
            }

            _listSessions.Invalidate();
        }

        private void HandleReceivedMessage(ChatMessage msg)
        {
            string convKey = msg.IsBroadcast ? "BROADCAST" : msg.SenderId;

            // Update peer last message info
            if (!msg.IsBroadcast && _netService != null)
            {
                var peers = _netService.GetActivePeers();
                foreach (var p in peers)
                {
                    if (p.Id == msg.SenderId)
                    {
                        p.LastMessage = msg.Content;
                        p.LastMessageTime = msg.Timestamp;
                        break;
                    }
                }
            }

            AddMessageToHistory(convKey, msg);
            _listSessions.Invalidate();

            // 若窗体隐藏在系统托盘中，显示微信风格气泡提醒
            if (!this.Visible && _trayIcon != null)
            {
                try
                {
                    string preview = msg.Content ?? "";
                    if (preview.Length > 36) preview = preview.Substring(0, 36) + "...";
                    string title = msg.IsBroadcast ? string.Format("[📢 广播] {0}", msg.SenderNick) : string.Format("来自 {0} 的新消息", msg.SenderNick);
                    _trayIcon.ShowBalloonTip(2000, title, preview, ToolTipIcon.Info);
                }
                catch { }
            }
        }

        private void AddMessageToHistory(string convKey, ChatMessage msg)
        {
            lock (_chatHistories)
            {
                List<ChatMessage> list;
                if (!_chatHistories.TryGetValue(convKey, out list))
                {
                    list = new List<ChatMessage>();
                    _chatHistories[convKey] = list;
                }
                list.Add(msg);
            }

            if (_activeChatKey == convKey)
            {
                _chatView.AppendMessage(msg);
            }
        }

        private void AppendChatSystem(string text)
        {
            ChatMessage msg = new ChatMessage
            {
                SenderNick = "系统",
                Content = text,
                Timestamp = DateTime.Now,
                IsSystem = true
            };

            // Add to current active chat
            AddMessageToHistory(_activeChatKey, msg);
        }

        private void ClearCurrentChat()
        {
            lock (_chatHistories)
            {
                if (_chatHistories.ContainsKey(_activeChatKey))
                {
                    _chatHistories[_activeChatKey].Clear();
                }
            }
            SwitchActiveChatView();
        }

        #endregion

        #region Emoji Popup

        private void ShowEmojiPopup()
        {
            Form popup = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                Size = new Size(290, 200),
                BackColor = Color.FromArgb(44, 44, 46)
            };

            Point screenPoint = _btnToolEmoji.PointToScreen(new Point(0, -popup.Height - 4));
            popup.Location = screenPoint;

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(6),
                BackColor = Color.FromArgb(44, 44, 46)
            };

            string[] emojis = new string[]
            {
                "😊", "😃", "😄", "😁", "😆", "😅", "😂", "🤣", "😇", "😉",
                "😋", "😍", "🥰", "😘", "😜", "🤔", "🤫", "🤐", "😴", "😷",
                "🥳", "🤩", "🥺", "😭", "😱", "😨", "😰", "😡", "🤬", "🤡",
                "👍", "👎", "👏", "🙏", "🤝", "🎉", "🔥", "❤️", "💯", "✨",
                "💪", "✌️", "🖐️", "✋", "👊", "☕", "💻", "📱", "🍺", "🎁"
            };

            foreach (string em in emojis)
            {
                Button btn = new Button
                {
                    Text = em,
                    Size = new Size(32, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI Emoji", 11f),
                    Margin = new Padding(2),
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(60, 60, 64);
                btn.MouseLeave += (s, e) => btn.BackColor = Color.Transparent;
                btn.Click += (s, e) => {
                    _txtInput.Paste(em);
                    popup.Close();
                    _txtInput.Focus();
                };
                flow.Controls.Add(btn);
            }

            popup.Controls.Add(flow);
            popup.Deactivate += (s, e) => popup.Close();
            popup.Show(this);
        }

        #endregion

        #region File Transfer & Drag Drop

        private void BtnSendFile_Click(object sender, EventArgs e)
        {
            if (_selectedPeer == null)
            {
                var peers = _netService != null ? _netService.GetActivePeers() : new List<PeerInfo>();
                if (peers.Count == 0)
                {
                    MessageBox.Show(this, "局域网内暂无其他在线好友，请等待好友加入或点击【➕ 添加IP】！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (OpenFileDialog ofd = new OpenFileDialog { Title = "选择要发送的文件", Multiselect = true })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        PromptPeerAndSendFiles(ofd.FileNames);
                    }
                }
            }
            else
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Title = "选择要发送给 " + _selectedPeer.NickName + " 的文件", Multiselect = true })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        SendFilesToPeer(_selectedPeer, ofd.FileNames);
                    }
                }
            }
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            if (_selectedPeer != null)
            {
                SendFilesToPeer(_selectedPeer, files);
            }
            else
            {
                PromptPeerAndSendFiles(files);
            }
        }

        private void PromptPeerAndSendFiles(string[] filePaths)
        {
            var peers = _netService != null ? _netService.GetActivePeers() : new List<PeerInfo>();
            if (peers.Count == 0)
            {
                MessageBox.Show(this, "当前无在线好友，无法发送文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (peers.Count == 1)
            {
                SendFilesToPeer(peers[0], filePaths);
                return;
            }

            using (Form picker = new Form())
            {
                picker.Text = "选择接收文件的成员";
                picker.Size = new Size(360, 260);
                picker.StartPosition = FormStartPosition.CenterParent;
                picker.FormBorderStyle = FormBorderStyle.FixedDialog;
                picker.MaximizeBox = false;
                picker.MinimizeBox = false;
                picker.BackColor = Color.FromArgb(44, 44, 46);
                picker.ForeColor = Color.White;

                ListBox lb = new ListBox
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Microsoft YaHei", 9.5f),
                    BackColor = Color.FromArgb(32, 32, 34),
                    ForeColor = Color.White
                };
                foreach (var p in peers) lb.Items.Add(p);
                lb.SelectedIndex = 0;

                Button btnOk = new Button { Text = "确定发送", Dock = DockStyle.Bottom, Height = 36, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(7, 193, 96), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                picker.Controls.Add(lb);
                picker.Controls.Add(btnOk);

                if (picker.ShowDialog(this) == DialogResult.OK && lb.SelectedItem is PeerInfo)
                {
                    SendFilesToPeer((PeerInfo)lb.SelectedItem, filePaths);
                }
            }
        }

        private void SendFilesToPeer(PeerInfo peer, string[] filePaths)
        {
            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath)) continue;

                string fileName = Path.GetFileName(filePath);
                long fileSize = new FileInfo(filePath).Length;

                ChatMessage fileMsg = new ChatMessage
                {
                    SenderNick = _netService != null ? _netService.NickName : Environment.UserName,
                    Content = "[发送文件] " + fileName,
                    Timestamp = DateTime.Now,
                    IsSelf = true,
                    IsFile = true,
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = fileSize
                };

                peer.LastMessage = "[文件] " + fileName;
                peer.LastMessageTime = DateTime.Now;
                AddMessageToHistory(peer.Id, fileMsg);

                _progressPanel.Visible = true;
                _progressBar.Value = 0;
                _lblProgressInfo.Text = string.Format("正在发送 {0} 至 {1}...", fileName, peer.NickName);

                _netService.SendFileAsync(peer, filePath,
                    prog => SafeInvoke(() => HandleFileProgress(prog)),
                    (success, reason) => SafeInvoke(() => {
                        _progressPanel.Visible = false;
                        if (success)
                        {
                            AppendChatSystem(string.Format("文件 {0} 发送成功！", fileName));
                        }
                        else
                        {
                            AppendChatSystem(string.Format("文件 {0} 发送失败: {1}", fileName, reason));
                        }
                    })
                );
            }
            _listSessions.Invalidate();
        }

        private void HandleFileProgress(FileTransferProgress prog)
        {
            _progressPanel.Visible = true;
            _progressBar.Value = prog.ProgressPercentage;

            string action = prog.IsSending ? "发送给 " + prog.PeerNick : "来自 " + prog.PeerNick + " 接收";
            _lblProgressInfo.Text = string.Format("[{0}] {1} ({2}/{3}) - {4:F1} MB/s [{5}%]",
                action, prog.FileName,
                FormatFileSize(prog.TransferredBytes),
                FormatFileSize(prog.TotalBytes),
                prog.SpeedMBps,
                prog.ProgressPercentage);

            if (prog.IsCompleted || prog.IsFailed)
            {
                _progressPanel.Visible = false;
            }
        }

        private void HandleFileReceived(string senderNick, string fileName, string localPath, long size)
        {
            _progressPanel.Visible = false;

            ChatMessage fileMsg = new ChatMessage
            {
                SenderNick = senderNick,
                Content = "[接收文件] " + fileName,
                Timestamp = DateTime.Now,
                IsSelf = false,
                IsFile = true,
                FileName = fileName,
                FilePath = localPath,
                FileSize = size
            };

            // Find peer
            string convKey = _activeChatKey;
            if (_netService != null)
            {
                var peers = _netService.GetActivePeers();
                foreach (var p in peers)
                {
                    if (p.NickName == senderNick)
                    {
                        convKey = p.Id;
                        p.LastMessage = "[文件] " + fileName;
                        p.LastMessageTime = DateTime.Now;
                        break;
                    }
                }
            }

            AddMessageToHistory(convKey, fileMsg);
            _listSessions.Invalidate();

            // 若窗体隐藏在系统托盘中，显示文件接收气泡提醒
            if (!this.Visible && _trayIcon != null)
            {
                try
                {
                    _trayIcon.ShowBalloonTip(2500, "收到新文件", string.Format("来自 {0}: {1}", senderNick, fileName), ToolTipIcon.Info);
                }
                catch { }
            }
        }

        private void HandleFileActionClick(ChatMessage msg, string action)
        {
            try
            {
                if (action == "OPEN_FILE")
                {
                    if (File.Exists(msg.FilePath))
                    {
                        Process.Start(msg.FilePath);
                    }
                    else
                    {
                        MessageBox.Show(this, "文件不存在或已被移动: " + msg.FilePath, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else if (action == "OPEN_FOLDER")
                {
                    string dir = Path.GetDirectoryName(msg.FilePath);
                    if (Directory.Exists(dir))
                    {
                        Process.Start("explorer.exe", "/select,\"" + msg.FilePath + "\"");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
        }

        #endregion

        #region Actions

        private void BtnAddIp_Click(object sender, EventArgs e)
        {
            using (Form inputForm = new Form())
            {
                if (this.Icon != null) inputForm.Icon = this.Icon;
                inputForm.Text = "手动添加好友 IP (直连握手)";
                inputForm.Size = new Size(390, 165);
                inputForm.StartPosition = FormStartPosition.CenterParent;
                inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                inputForm.MaximizeBox = false;
                inputForm.MinimizeBox = false;
                inputForm.BackColor = Color.FromArgb(44, 44, 46);
                inputForm.ForeColor = Color.White;

                Label lblPrompt = new Label
                {
                    Text = "请输入对方计算机的 IPv4 地址 (例如 192.168.255.147):",
                    Location = new Point(16, 16),
                    AutoSize = true,
                    Font = new Font("Microsoft YaHei", 9f)
                };

                TextBox txtIp = new TextBox
                {
                    Location = new Point(18, 42),
                    Width = 338,
                    Font = new Font("Microsoft YaHei", 10f),
                    BackColor = Color.FromArgb(32, 32, 34),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                Button btnOk = new Button
                {
                    Text = "连接握手",
                    Location = new Point(180, 78),
                    Size = new Size(85, 30),
                    DialogResult = DialogResult.OK,
                    BackColor = Color.FromArgb(7, 193, 96),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };

                Button btnCancel = new Button
                {
                    Text = "取消",
                    Location = new Point(275, 78),
                    Size = new Size(80, 30),
                    DialogResult = DialogResult.Cancel,
                    BackColor = Color.FromArgb(60, 60, 62),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };

                inputForm.Controls.AddRange(new Control[] { lblPrompt, txtIp, btnOk, btnCancel });
                inputForm.AcceptButton = btnOk;
                inputForm.CancelButton = btnCancel;

                if (inputForm.ShowDialog(this) == DialogResult.OK)
                {
                    string ipStr = txtIp.Text.Trim();
                    if (!string.IsNullOrEmpty(ipStr) && _netService != null)
                    {
                        AppendChatSystem("正在主动向 " + ipStr + " 发起 TCP 直连握手...");
                        _netService.ProbeIp(ipStr, (success, msg) => {
                            SafeInvoke(() => AppendChatSystem(msg));
                        });
                    }
                }
            }
        }

        #region System Tray & Lifecycle

        private void InitializeTrayIcon()
        {
            try
            {
                _trayIcon = new NotifyIcon();
                _trayIcon.Icon = this.Icon ?? SystemIcons.Application;
                _trayIcon.Text = "LanChat 局域网通讯";
                _trayIcon.Visible = true;

                // 左键单击或双击还原主界面
                _trayIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        RestoreFromTray();
                    }
                };
                _trayIcon.DoubleClick += (s, e) =>
                {
                    RestoreFromTray();
                };
                _trayIcon.BalloonTipClicked += (s, e) =>
                {
                    RestoreFromTray();
                };

                // WeChat Dark 风格暗黑托盘右键菜单
                ContextMenuStrip trayMenu = new ContextMenuStrip();
                trayMenu.Renderer = new WeChatDarkMenuRenderer();
                trayMenu.ShowImageMargin = false;
                trayMenu.ShowCheckMargin = false;
                trayMenu.BackColor = Color.FromArgb(44, 44, 46);
                trayMenu.ForeColor = Color.FromArgb(235, 235, 235);

                ToolStripMenuItem itemShow = new ToolStripMenuItem("显示主界面");
                itemShow.Font = new Font(this.Font, FontStyle.Bold);
                itemShow.Click += (s, e) => RestoreFromTray();
                trayMenu.Items.Add(itemShow);

                trayMenu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem itemExit = new ToolStripMenuItem("退出 LanChat");
                itemExit.Click += (s, e) =>
                {
                    _isExplicitExit = true;
                    this.Close();
                };
                trayMenu.Items.Add(itemExit);

                _trayIcon.ContextMenuStrip = trayMenu;
            }
            catch { }
        }

        private void RestoreFromTray()
        {
            if (!this.Visible)
            {
                this.Show();
            }
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            this.Activate();
            this.BringToFront();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 用户点击右上角关闭按钮（X）且非显式退出：最小化到托盘
            if (!_isExplicitExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();

                if (!_hasShownTrayBalloon && _trayIcon != null)
                {
                    _hasShownTrayBalloon = true;
                    try
                    {
                        _trayIcon.ShowBalloonTip(2000, "LanChat 已最小化到托盘", "程序已转入后台运行。双击托盘图标重新打开，右键可真正退出。", ToolTipIcon.Info);
                    }
                    catch { }
                }
                return;
            }

            // 显式退出或系统关机：安全释放托盘与网络资源
            if (_trayIcon != null)
            {
                try
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                catch { }
                _trayIcon = null;
            }

            if (_netService != null)
            {
                _netService.Dispose();
            }
        }

        #endregion

        private void SafeInvoke(Action action)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch { }
        }

        #endregion

        #region Helper Drawing Functions

        public static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle bounds, int radius)
        {
            using (GraphicsPath path = RoundedRect(bounds, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        public static void DrawRoundedRectangle(Graphics g, Brush brush, Rectangle bounds, int radius)
        {
            using (GraphicsPath path = RoundedRect(bounds, radius))
            {
                g.FillPath(brush, path);
            }
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        #endregion
    }

    #region Vector Icon Definitions & Renderer

    public enum WeChatIconType
    {
        Chat,
        Contacts,
        Folder,
        Box,
        Refresh,
        Add,
        Menu,
        Emoji,
        Scissors,
        Clear,
        Search,
        Call,
        More
    }

    public static class WeChatIcon
    {
        public static void Draw(Graphics g, WeChatIconType type, Rectangle bounds, bool active, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            switch (type)
            {
                case WeChatIconType.Chat:
                    DrawChatIcon(g, bounds, active, color);
                    break;
                case WeChatIconType.Contacts:
                    DrawContactsIcon(g, bounds, color);
                    break;
                case WeChatIconType.Folder:
                    DrawFolderIcon(g, bounds, color);
                    break;
                case WeChatIconType.Box:
                    DrawBoxIcon(g, bounds, color);
                    break;
                case WeChatIconType.Refresh:
                    DrawRefreshIcon(g, bounds, color);
                    break;
                case WeChatIconType.Add:
                    DrawAddIcon(g, bounds, color);
                    break;
                case WeChatIconType.Menu:
                    DrawMenuIcon(g, bounds, color);
                    break;
                case WeChatIconType.Emoji:
                    DrawEmojiIcon(g, bounds, color);
                    break;
                case WeChatIconType.Scissors:
                    DrawScissorsIcon(g, bounds, color);
                    break;
                case WeChatIconType.Clear:
                    DrawClearIcon(g, bounds, color);
                    break;
                case WeChatIconType.Search:
                    DrawSearchIcon(g, bounds, color);
                    break;
                case WeChatIconType.Call:
                    DrawCallIcon(g, bounds, color);
                    break;
                case WeChatIconType.More:
                    DrawMoreIcon(g, bounds, color);
                    break;
            }
        }

        // 1. WeChat Chat speech bubble icon (solid filled green when active, outlined otherwise)
        public static void DrawChatIcon(Graphics g, Rectangle r, bool active, Color color)
        {
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            float w = r.Width * 0.72f;
            float h = r.Height * 0.62f;

            using (GraphicsPath path = new GraphicsPath())
            {
                // Oval body
                path.AddArc(cx - w / 2f, cy - h / 2f, w, h, 200, 310);
                // Bottom left tail
                path.AddLine(cx - w * 0.38f, cy + h * 0.42f, cx - w * 0.54f, cy + h * 0.64f);
                path.AddLine(cx - w * 0.54f, cy + h * 0.64f, cx - w * 0.18f, cy + h * 0.46f);
                path.CloseFigure();

                if (active)
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(0, 163, 97)))
                    {
                        g.FillPath(b, path);
                    }
                }
                else
                {
                    using (Pen p = new Pen(color, 1.8f))
                    {
                        p.LineJoin = LineJoin.Round;
                        g.DrawPath(p, path);
                    }
                }
            }
        }

        // 2. WeChat Contacts icon (person profile + 3 horizontal lines, exactly per uploaded screenshot)
        public static void DrawContactsIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;

                float x0 = r.X + r.Width * 0.18f;
                float y0 = r.Y + r.Height * 0.18f;
                float s = r.Width * 0.64f;

                // Head circle
                g.DrawEllipse(p, x0 + s * 0.18f, y0 + s * 0.04f, s * 0.34f, s * 0.34f);

                // Shoulders arc
                g.DrawArc(p, x0 + s * 0.02f, y0 + s * 0.46f, s * 0.66f, s * 0.42f, 180, 180);

                // 3 horizontal contact lines on the right
                float lx1 = x0 + s * 0.76f;
                float lx2 = x0 + s * 1.05f;
                g.DrawLine(p, lx1, y0 + s * 0.16f, lx2, y0 + s * 0.16f);
                g.DrawLine(p, lx1, y0 + s * 0.36f, lx2, y0 + s * 0.36f);
                g.DrawLine(p, lx1, y0 + s * 0.56f, lx2 - s * 0.08f, y0 + s * 0.56f);
            }
        }

        // 3. Folder icon
        public static void DrawFolderIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.7f))
            {
                p.LineJoin = LineJoin.Round;
                float x = r.X + r.Width * 0.16f;
                float y = r.Y + r.Height * 0.22f;
                float w = r.Width * 0.68f;
                float h = r.Height * 0.56f;

                GraphicsPath path = new GraphicsPath();
                path.AddLine(x, y + 3, x + w * 0.35f, y + 3);
                path.AddLine(x + w * 0.42f, y + 6, x + w, y + 6);
                path.AddLine(x + w, y + h, x, y + h);
                path.CloseFigure();
                g.DrawPath(p, path);
            }
        }

        // 4. Cube / Box icon
        public static void DrawBoxIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.6f))
            {
                p.LineJoin = LineJoin.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float w = r.Width * 0.32f;
                float h = r.Height * 0.22f;

                PointF top = new PointF(cx, cy - h * 1.5f);
                PointF topRight = new PointF(cx + w, cy - h * 0.5f);
                PointF botRight = new PointF(cx + w, cy + h * 0.9f);
                PointF bot = new PointF(cx, cy + h * 1.8f);
                PointF botLeft = new PointF(cx - w, cy + h * 0.9f);
                PointF topLeft = new PointF(cx - w, cy - h * 0.5f);
                PointF center = new PointF(cx, cy);

                g.DrawPolygon(p, new PointF[] { top, topRight, botRight, bot, botLeft, topLeft });
                g.DrawLine(p, center, top);
                g.DrawLine(p, center, botRight);
                g.DrawLine(p, center, botLeft);
            }
        }

        // 5. Refresh / Radar icon (clockwise double arc arrows)
        public static void DrawRefreshIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float sz = r.Width * 0.32f;

                RectangleF rf = new RectangleF(cx - sz, cy - sz, sz * 2, sz * 2);
                g.DrawArc(p, rf, 40, 120);
                g.DrawArc(p, rf, 220, 120);

                // Arrows
                g.DrawLine(p, cx + sz * 0.7f, cy + sz * 0.3f, cx + sz * 0.7f, cy + sz * 0.8f);
                g.DrawLine(p, cx + sz * 0.7f, cy + sz * 0.8f, cx + sz * 0.2f, cy + sz * 0.8f);

                g.DrawLine(p, cx - sz * 0.7f, cy - sz * 0.3f, cx - sz * 0.7f, cy - sz * 0.8f);
                g.DrawLine(p, cx - sz * 0.7f, cy - sz * 0.8f, cx - sz * 0.2f, cy - sz * 0.8f);
            }
        }

        // 6. Plus / Add icon
        public static void DrawAddIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float len = r.Width * 0.22f;

                g.DrawLine(p, cx - len, cy, cx + len, cy);
                g.DrawLine(p, cx, cy - len, cx, cy + len);
            }
        }

        // 7. Menu Hamburger icon (3 horizontal lines)
        public static void DrawMenuIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 2.0f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float len = r.Width * 0.28f;

                g.DrawLine(p, cx - len, cy - 5, cx + len, cy - 5);
                g.DrawLine(p, cx - len, cy, cx + len, cy);
                g.DrawLine(p, cx - len, cy + 5, cx + len, cy + 5);
            }
        }

        // 8. Emoji smiley icon
        public static void DrawEmojiIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float rad = r.Width * 0.35f;

                // Face circle
                g.DrawEllipse(p, cx - rad, cy - rad, rad * 2, rad * 2);

                // Eyes
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.FillEllipse(b, cx - rad * 0.45f, cy - rad * 0.35f, 2.2f, 2.2f);
                    g.FillEllipse(b, cx + rad * 0.25f, cy - rad * 0.35f, 2.2f, 2.2f);
                }

                // Smile curve
                g.DrawArc(p, cx - rad * 0.5f, cy - rad * 0.2f, rad, rad * 0.8f, 20, 140);
            }
        }

        // 9. Scissors / Screenshot icon
        public static void DrawScissorsIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.5f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;

                // Two blades
                g.DrawLine(p, cx - 6, cy - 7, cx + 6, cy + 6);
                g.DrawLine(p, cx - 6, cy + 7, cx + 6, cy - 6);

                // Two handles
                g.DrawEllipse(p, cx - 9, cy - 9, 5, 5);
                g.DrawEllipse(p, cx - 9, cy + 4, 5, 5);
            }
        }

        // 10. Clear / Broom icon
        public static void DrawClearIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;

                // Handle
                g.DrawLine(p, cx + 5, cy - 6, cx - 1, cy);
                // Bristles
                g.DrawLine(p, cx - 5, cy + 5, cx + 3, cy - 1);
                g.DrawLine(p, cx - 6, cy + 6, cx - 1, cy + 7);
                g.DrawLine(p, cx - 6, cy + 6, cx - 7, cy + 2);
            }
        }

        // 11. Search Magnifying Glass
        public static void DrawSearchIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.5f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float x = r.X + 2;
                float y = r.Y + 2;
                g.DrawEllipse(p, x, y, 9, 9);
                g.DrawLine(p, x + 7.5f, y + 7.5f, x + 12f, y + 12f);
            }
        }

        // 12. Call Handset Icon
        public static void DrawCallIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.6f))
            {
                p.LineJoin = LineJoin.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                GraphicsPath path = new GraphicsPath();
                path.AddArc(cx - 6, cy - 6, 12, 12, 190, 80);
                g.DrawPath(p, path);
            }
        }

        // 13. More Dots Icon (···)
        public static void DrawMoreIcon(Graphics g, Rectangle r, Color color)
        {
            using (SolidBrush b = new SolidBrush(color))
            {
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                g.FillEllipse(b, cx - 6, cy - 1.5f, 3, 3);
                g.FillEllipse(b, cx - 1.5f, cy - 1.5f, 3, 3);
                g.FillEllipse(b, cx + 3f, cy - 1.5f, 3, 3);
            }
        }
    }

    #endregion

    #region Custom WeChat Navigation Button

    public class WeChatNavButton : Control
    {
        private WeChatIconType _iconType;
        private bool _isActive;
        private bool _isHovered;
        private bool _isPressed;
        private string _tooltipText;

        public WeChatIconType IconType
        {
            get { return _iconType; }
            set { _iconType = value; Invalidate(); }
        }

        public bool IsActive
        {
            get { return _isActive; }
            set { _isActive = value; Invalidate(); }
        }

        public string TooltipText
        {
            get { return _tooltipText; }
            set { _tooltipText = value; }
        }

        public WeChatNavButton(WeChatIconType iconType, string tooltip, bool active = false)
        {
            _iconType = iconType;
            _tooltipText = tooltip;
            _isActive = active;

            this.Size = new Size(36, 36);
            this.Cursor = Cursors.Hand;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
            WeChatDarkTooltip.ShowTooltip(this, _tooltipText);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
            WeChatDarkTooltip.HideTooltip();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _isPressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background hover rounded rectangle (#303031, exactly per screenshot)
            if (_isHovered && !_isActive)
            {
                using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(48, 48, 49)))
                {
                    MainForm.DrawRoundedRectangle(g, hoverBg, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 6);
                }
            }
            else if (_isPressed)
            {
                using (SolidBrush pressBg = new SolidBrush(Color.FromArgb(40, 40, 41)))
                {
                    MainForm.DrawRoundedRectangle(g, pressBg, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 6);
                }
            }

            // Determine Icon color
            Color iconColor;
            if (_isActive)
            {
                iconColor = Color.FromArgb(0, 163, 97); // WeChat Green
            }
            else if (_isHovered)
            {
                iconColor = Color.FromArgb(240, 240, 240); // Bright white
            }
            else
            {
                iconColor = Color.FromArgb(152, 152, 152); // Muted grey #989898
            }

            Rectangle iconRect = new Rectangle(0, 0, this.Width, this.Height);
            WeChatIcon.Draw(g, _iconType, iconRect, _isActive, iconColor);
        }
    }

    #endregion

    #region Custom WeChat Icon Button (For Header & Toolbar)

    public class WeChatIconButton : Control
    {
        private WeChatIconType _iconType;
        private bool _isHovered;
        private bool _isPressed;
        private string _tooltipText;

        public WeChatIconButton(WeChatIconType iconType, string tooltip, int size = 28)
        {
            _iconType = iconType;
            _tooltipText = tooltip;

            this.Size = new Size(size, size);
            this.Cursor = Cursors.Hand;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
            if (!string.IsNullOrEmpty(_tooltipText))
            {
                WeChatDarkTooltip.ShowTooltip(this, _tooltipText);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
            WeChatDarkTooltip.HideTooltip();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_isPressed)
            {
                using (SolidBrush pressBg = new SolidBrush(Color.FromArgb(60, 60, 64)))
                {
                    MainForm.DrawRoundedRectangle(g, pressBg, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 4);
                }
            }
            else if (_isHovered)
            {
                using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(50, 50, 52)))
                {
                    MainForm.DrawRoundedRectangle(g, hoverBg, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 4);
                }
            }

            Color iconColor = (_isHovered || _isPressed) ? Color.FromArgb(240, 240, 240) : Color.FromArgb(145, 145, 148);
            WeChatIcon.Draw(g, _iconType, new Rectangle(0, 0, this.Width, this.Height), false, iconColor);
        }
    }

    #endregion

    #region Custom WeChat Dark Menu Renderer

    public class WeChatDarkMenuColorTable : ProfessionalColorTable
    {
        private readonly Color _bg = Color.FromArgb(44, 44, 46);
        private readonly Color _border = Color.FromArgb(60, 60, 64);
        private readonly Color _selected = Color.FromArgb(60, 60, 64);
        private readonly Color _separator = Color.FromArgb(55, 55, 58);

        public override Color ToolStripDropDownBackground { get { return _bg; } }
        public override Color ImageMarginGradientBegin { get { return _bg; } }
        public override Color ImageMarginGradientMiddle { get { return _bg; } }
        public override Color ImageMarginGradientEnd { get { return _bg; } }
        public override Color ImageMarginRevealedGradientBegin { get { return _bg; } }
        public override Color ImageMarginRevealedGradientMiddle { get { return _bg; } }
        public override Color ImageMarginRevealedGradientEnd { get { return _bg; } }
        public override Color MenuBorder { get { return _border; } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return _selected; } }
        public override Color MenuItemSelectedGradientBegin { get { return _selected; } }
        public override Color MenuItemSelectedGradientEnd { get { return _selected; } }
        public override Color MenuItemPressedGradientBegin { get { return _selected; } }
        public override Color MenuItemPressedGradientMiddle { get { return _selected; } }
        public override Color MenuItemPressedGradientEnd { get { return _selected; } }
        public override Color CheckBackground { get { return _selected; } }
        public override Color CheckSelectedBackground { get { return _selected; } }
        public override Color CheckPressedBackground { get { return _selected; } }
        public override Color SeparatorDark { get { return _separator; } }
        public override Color SeparatorLight { get { return _bg; } }
    }

    public class WeChatDarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public WeChatDarkMenuRenderer() : base(new WeChatDarkMenuColorTable())
        {
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.FromArgb(235, 235, 235) : Color.FromArgb(125, 125, 128);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.FromArgb(200, 200, 200);
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = e.ImageRectangle;
            using (Pen p = new Pen(Color.FromArgb(7, 193, 96), 2.2f))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawLines(p, new Point[] {
                    new Point(r.Left + 3, r.Top + r.Height / 2),
                    new Point(r.Left + r.Width / 2 - 1, r.Bottom - 4),
                    new Point(r.Right - 3, r.Top + 4)
                });
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(44, 44, 46)))
            {
                e.Graphics.FillRectangle(b, e.AffectedBounds);
            }
        }
    }

    #endregion

    #region Custom WeChat Dark Tooltip

    public static class WeChatDarkTooltip
    {
        private static Form _tipForm;
        private static Label _lblText;

        public static void ShowTooltip(Control parent, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (_tipForm == null)
            {
                _tipForm = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    BackColor = Color.FromArgb(59, 59, 61), // #3b3b3d per screenshot
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(8, 5, 8, 5)
                };

                _lblText = new Label
                {
                    ForeColor = Color.FromArgb(225, 225, 229), // #e1e1e5
                    Font = new Font("Microsoft YaHei", 8.5f),
                    AutoSize = true,
                    BackColor = Color.Transparent
                };
                _tipForm.Controls.Add(_lblText);

                _tipForm.Paint += (s, e) => {
                    using (Pen p = new Pen(Color.FromArgb(75, 75, 78)))
                    {
                        e.Graphics.DrawRectangle(p, 0, 0, _tipForm.Width - 1, _tipForm.Height - 1);
                    }
                };
            }

            _lblText.Text = text;
            Point screenPt = parent.PointToScreen(new Point(parent.Width + 8, (parent.Height - 24) / 2));
            _tipForm.Location = screenPt;
            _tipForm.Show();
        }

        public static void HideTooltip()
        {
            if (_tipForm != null && _tipForm.Visible)
            {
                _tipForm.Hide();
            }
        }
    }

    #endregion

    #region Custom WeChat Chat View Control

    public class WeChatChatView : UserControl
    {
        public Action<ChatMessage, string> OnFileActionClick;

        private List<ChatMessage> _messages = new List<ChatMessage>();
        private readonly VScrollBar _vScrollBar;
        private int _totalHeight = 0;
        private readonly List<MessageRenderItem> _renderItems = new List<MessageRenderItem>();

        private class MessageRenderItem
        {
            public ChatMessage Message;
            public Rectangle Bounds;
            public Rectangle BubbleBounds;
            public Rectangle AvatarBounds;
            public Rectangle FileOpenBounds;
            public Rectangle FileFolderBounds;
        }

        public WeChatChatView()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = Color.FromArgb(30, 30, 31);
            this.AutoScroll = false;

            _vScrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                Width = 8,
                Visible = false
            };
            _vScrollBar.Scroll += (s, e) => { Invalidate(); };
            this.Controls.Add(_vScrollBar);

            this.MouseWheel += WeChatChatView_MouseWheel;
            this.MouseDown += WeChatChatView_MouseDown;
            this.Resize += (s, e) => { RecalculateLayout(true); Invalidate(); };
        }

        public void SetMessages(List<ChatMessage> list)
        {
            _messages = list ?? new List<ChatMessage>();
            RecalculateLayout(true);
            Invalidate();
        }

        public void AppendMessage(ChatMessage msg)
        {
            if (_messages != null && !_messages.Contains(msg))
            {
                _messages.Add(msg);
            }
            RecalculateLayout(true);
            Invalidate();
        }

        private void WeChatChatView_MouseWheel(object sender, MouseEventArgs e)
        {
            if (!_vScrollBar.Visible) return;
            int change = (e.Delta / 120) * -36;
            int newVal = _vScrollBar.Value + change;
            if (newVal < _vScrollBar.Minimum) newVal = _vScrollBar.Minimum;
            if (newVal > _vScrollBar.Maximum - _vScrollBar.LargeChange + 1) newVal = Math.Max(0, _vScrollBar.Maximum - _vScrollBar.LargeChange + 1);
            _vScrollBar.Value = newVal;
            Invalidate();
        }

        private void WeChatChatView_MouseDown(object sender, MouseEventArgs e)
        {
            Point pt = new Point(e.X, e.Y + _vScrollBar.Value);
            foreach (var item in _renderItems)
            {
                if (item.Message.IsFile)
                {
                    if (item.FileOpenBounds.Contains(pt))
                    {
                        if (OnFileActionClick != null) OnFileActionClick(item.Message, "OPEN_FILE");
                        return;
                    }
                    if (item.FileFolderBounds.Contains(pt))
                    {
                        if (OnFileActionClick != null) OnFileActionClick(item.Message, "OPEN_FOLDER");
                        return;
                    }
                }
            }
        }

        private void RecalculateLayout(bool scrollToBottom)
        {
            _renderItems.Clear();
            if (_messages == null || _messages.Count == 0)
            {
                _totalHeight = 0;
                _vScrollBar.Visible = false;
                return;
            }

            int viewWidth = this.Width - (_vScrollBar.Visible ? _vScrollBar.Width : 0);
            if (viewWidth < 200) viewWidth = 200;

            int currentY = 16;
            DateTime lastTime = DateTime.MinValue;

            using (Graphics g = this.CreateGraphics())
            using (Font timeFont = new Font("Microsoft YaHei", 8f))
            using (Font msgFont = new Font("Microsoft YaHei", 9.5f))
            {
                foreach (var msg in _messages)
                {
                    // Time header every 3 minutes
                    if ((msg.Timestamp - lastTime).TotalMinutes > 3)
                    {
                        lastTime = msg.Timestamp;
                        currentY += 24;
                    }

                    if (msg.IsSystem)
                    {
                        // Centered system tip
                        SizeF sz = g.MeasureString(msg.Content, timeFont, viewWidth - 80);
                        int h = (int)Math.Ceiling(sz.Height) + 12;
                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, h)
                        };
                        _renderItems.Add(r);
                        currentY += h + 8;
                    }
                    else if (msg.IsFile)
                    {
                        // File card (Width ~230px, Height ~74px)
                        int cardW = 230;
                        int cardH = 74;
                        int x = msg.IsSelf ? (viewWidth - cardW - 54) : 54;
                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, cardH + 18),
                            BubbleBounds = new Rectangle(x, currentY + 12, cardW, cardH),
                            AvatarBounds = msg.IsSelf ? new Rectangle(viewWidth - 44, currentY + 12, 34, 34) : new Rectangle(14, currentY + 12, 34, 34),
                            FileOpenBounds = new Rectangle(x + 10, currentY + 12 + 48, 64, 20),
                            FileFolderBounds = new Rectangle(x + 80, currentY + 12 + 48, 74, 20)
                        };
                        _renderItems.Add(r);
                        currentY += cardH + 24;
                    }
                    else
                    {
                        // Regular bubble message
                        int maxTextW = Math.Max(120, (int)(viewWidth * 0.65));
                        SizeF sz = g.MeasureString(msg.Content, msgFont, maxTextW);
                        int bubbleW = Math.Max(40, (int)Math.Ceiling(sz.Width) + 22);
                        int bubbleH = Math.Max(32, (int)Math.Ceiling(sz.Height) + 14);

                        int x = msg.IsSelf ? (viewWidth - bubbleW - 54) : 54;
                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, bubbleH + 18),
                            BubbleBounds = new Rectangle(x, currentY + 10, bubbleW, bubbleH),
                            AvatarBounds = msg.IsSelf ? new Rectangle(viewWidth - 44, currentY + 10, 34, 34) : new Rectangle(14, currentY + 10, 34, 34)
                        };
                        _renderItems.Add(r);
                        currentY += bubbleH + 20;
                    }
                }
            }

            _totalHeight = currentY + 20;

            if (_totalHeight > this.Height)
            {
                _vScrollBar.Visible = true;
                _vScrollBar.Maximum = _totalHeight;
                _vScrollBar.LargeChange = this.Height;
                if (scrollToBottom)
                {
                    _vScrollBar.Value = Math.Max(0, _vScrollBar.Maximum - _vScrollBar.LargeChange + 1);
                }
            }
            else
            {
                _vScrollBar.Visible = false;
                _vScrollBar.Value = 0;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int scrollY = _vScrollBar.Visible ? _vScrollBar.Value : 0;
            int viewWidth = this.Width - (_vScrollBar.Visible ? _vScrollBar.Width : 0);

            using (Font timeFont = new Font("Microsoft YaHei", 8f))
            using (Font msgFont = new Font("Microsoft YaHei", 9.5f))
            using (Font nickFont = new Font("Microsoft YaHei", 8f))
            using (SolidBrush textLight = new SolidBrush(Color.FromArgb(230, 230, 230)))
            using (SolidBrush textDark = new SolidBrush(Color.FromArgb(10, 10, 10)))
            using (SolidBrush grayText = new SolidBrush(Color.FromArgb(120, 120, 120)))
            using (SolidBrush otherBubbleBrush = new SolidBrush(Color.FromArgb(44, 44, 46))) // WeChat other bubble #2c2c2e
            using (SolidBrush selfBubbleBrush = new SolidBrush(Color.FromArgb(7, 193, 96)))    // WeChat green bubble #07c160
            {
                DateTime lastTime = DateTime.MinValue;

                foreach (var item in _renderItems)
                {
                    int itemTop = item.Bounds.Top - scrollY;
                    int itemBottom = item.Bounds.Bottom - scrollY;

                    if (itemBottom < 0 || itemTop > this.Height) continue;

                    ChatMessage msg = item.Message;

                    // Centered Time Header
                    if ((msg.Timestamp - lastTime).TotalMinutes > 3)
                    {
                        lastTime = msg.Timestamp;
                        string timeStr = msg.Timestamp.ToString("M月d日 HH:mm");
                        SizeF tsz = g.MeasureString(timeStr, timeFont);
                        float tx = (viewWidth - tsz.Width) / 2f;
                        g.DrawString(timeStr, timeFont, grayText, tx, itemTop - 18);
                    }

                    if (msg.IsSystem)
                    {
                        // Centered system notification
                        SizeF sz = g.MeasureString(msg.Content, timeFont, viewWidth - 80);
                        float sx = (viewWidth - sz.Width) / 2f;
                        g.DrawString(msg.Content, timeFont, grayText, new RectangleF(sx, itemTop + 4, sz.Width + 4, sz.Height + 4));
                    }
                    else if (msg.IsFile)
                    {
                        // Avatar (rounded rectangle radius 4px)
                        Rectangle av = new Rectangle(item.AvatarBounds.X, item.AvatarBounds.Y - scrollY, item.AvatarBounds.Width, item.AvatarBounds.Height);
                        DrawAvatar(g, msg.SenderNick, av, msg.IsSelf);

                        // File Card (matte dark #2c2c2e)
                        Rectangle bb = new Rectangle(item.BubbleBounds.X, item.BubbleBounds.Y - scrollY, item.BubbleBounds.Width, item.BubbleBounds.Height);
                        MainForm.DrawRoundedRectangle(g, otherBubbleBrush, bb, 4);

                        // File badge icon
                        Rectangle badgeRect = new Rectangle(bb.Left + 10, bb.Top + 10, 32, 32);
                        using (SolidBrush badgeBg = new SolidBrush(Color.FromArgb(30, 58, 138))) // Deep Blue
                        {
                            MainForm.DrawRoundedRectangle(g, badgeBg, badgeRect, 4);
                        }
                        StringFormat bsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("FILE", new Font("Microsoft YaHei", 7.5f, FontStyle.Bold), textLight, badgeRect, bsf);

                        // Name & Size
                        string nameSnippet = msg.FileName ?? "未知文件";
                        if (nameSnippet.Length > 16) nameSnippet = nameSnippet.Substring(0, 15) + "...";
                        g.DrawString(nameSnippet, new Font("Microsoft YaHei", 9f, FontStyle.Bold), textLight, bb.Left + 48, bb.Top + 9);
                        g.DrawString(MainForm_FormatFileSize(msg.FileSize), new Font("Microsoft YaHei", 8f), grayText, bb.Left + 48, bb.Top + 27);

                        // Action links
                        Rectangle bOpen = new Rectangle(item.FileOpenBounds.X, item.FileOpenBounds.Y - scrollY, item.FileOpenBounds.Width, item.FileOpenBounds.Height);
                        Rectangle bFolder = new Rectangle(item.FileFolderBounds.X, item.FileFolderBounds.Y - scrollY, item.FileFolderBounds.Width, item.FileFolderBounds.Height);

                        using (SolidBrush linkBrush = new SolidBrush(Color.FromArgb(7, 193, 96)))
                        {
                            g.DrawString("打开文件", timeFont, linkBrush, bOpen);
                            g.DrawString("打开目录", timeFont, linkBrush, bFolder);
                        }
                    }
                    else
                    {
                        // Avatar
                        Rectangle av = new Rectangle(item.AvatarBounds.X, item.AvatarBounds.Y - scrollY, item.AvatarBounds.Width, item.AvatarBounds.Height);
                        DrawAvatar(g, msg.SenderNick, av, msg.IsSelf);

                        // Bubble
                        Rectangle bb = new Rectangle(item.BubbleBounds.X, item.BubbleBounds.Y - scrollY, item.BubbleBounds.Width, item.BubbleBounds.Height);

                        if (msg.IsSelf)
                        {
                            // Self WeChat Green bubble (#07c160)
                            MainForm.DrawRoundedRectangle(g, selfBubbleBrush, bb, 4);

                            // Triangle pointing right towards avatar
                            Point[] tri = new Point[]
                            {
                                new Point(bb.Right, bb.Top + 10),
                                new Point(bb.Right + 5, bb.Top + 15),
                                new Point(bb.Right, bb.Top + 20)
                            };
                            g.FillPolygon(selfBubbleBrush, tri);

                            // Text inside green bubble (crisp dark text as in screenshot)
                            Rectangle textRect = new Rectangle(bb.Left + 11, bb.Top + 7, bb.Width - 22, bb.Height - 14);
                            g.DrawString(msg.Content, msgFont, textDark, textRect);
                        }
                        else
                        {
                            // Nickname on other side
                            g.DrawString(msg.SenderNick ?? "好友", nickFont, grayText, bb.Left, bb.Top - 14);

                            // Other Dark Gray bubble (#2c2c2e)
                            MainForm.DrawRoundedRectangle(g, otherBubbleBrush, bb, 4);

                            // Triangle pointing left towards avatar
                            Point[] tri = new Point[]
                            {
                                new Point(bb.Left, bb.Top + 10),
                                new Point(bb.Left - 5, bb.Top + 15),
                                new Point(bb.Left, bb.Top + 20)
                            };
                            g.FillPolygon(otherBubbleBrush, tri);

                            // Text inside other bubble
                            Rectangle textRect = new Rectangle(bb.Left + 11, bb.Top + 7, bb.Width - 22, bb.Height - 14);
                            g.DrawString(msg.Content, msgFont, textLight, textRect);
                        }
                    }
                }
            }
        }

        private void DrawAvatar(Graphics g, string nick, Rectangle rect, bool isSelf)
        {
            Color avColor = isSelf ? Color.FromArgb(7, 193, 96) : Color.FromArgb(59, 130, 246);
            using (SolidBrush avBrush = new SolidBrush(avColor))
            {
                MainForm.DrawRoundedRectangle(g, avBrush, rect, 4);
            }

            string initial = string.IsNullOrEmpty(nick) ? (isSelf ? "我" : "友") : nick.Substring(0, 1).ToUpperInvariant();
            using (Font f = new Font("Microsoft YaHei", 10f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(initial, f, textBrush, rect, sf);
            }
        }

        private static string MainForm_FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
        }
    }

    #endregion
}
