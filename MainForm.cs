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
        private readonly object _uiLock = new object();

        // Chat histories by conversation key: "" or "BROADCAST" for group, peerId for private
        private readonly Dictionary<string, List<ChatMessage>> _chatHistories = new Dictionary<string, List<ChatMessage>>();
        private string _activeChatKey = "BROADCAST";
        private PeerInfo _selectedPeer = null;

        // UI Layout Panels
        private Panel _sidebarPanel;
        private Panel _sessionsPanel;
        private Panel _chatPanel;

        // Sidebar Controls
        private Button _btnAvatar;
        private Button _btnNavChat;
        private Button _btnNavContacts;
        private Button _btnNavFolder;
        private Button _btnNavRefresh;
        private Button _btnNavAddIp;
        private Button _btnNavSettings;

        // Sessions Column Controls
        private Panel _searchHeaderPanel;
        private TextBox _txtSearch;
        private Button _btnAddSession;
        private ListBox _listSessions;
        private List<object> _allSessionItems = new List<object>();

        // Chat Header Controls
        private Panel _chatHeader;
        private Label _lblChatTitle;
        private Label _lblChatSubtitle;
        private Button _btnChatRefresh;
        private Button _btnChatFolder;
        private Button _btnChatAddIp;
        private Button _btnChatClear;

        // File Progress Banner
        private Panel _progressPanel;
        private Label _lblProgressInfo;
        private ProgressBar _progressBar;

        // Chat History View
        private WeChatChatView _chatView;

        // Input Toolbar Controls
        private Panel _toolPanel;
        private Button _btnToolEmoji;
        private Button _btnToolSendFile;
        private Button _btnToolFolder;
        private Button _btnToolClear;
        private Label _lblAutoAccept;

        // Input Area Controls
        private Panel _inputPanel;
        private TextBox _txtInput;
        private Panel _bottomSendPanel;
        private Label _lblSendTip;
        private Button _btnSend;

        // Navigation state: 0 = Chat, 1 = Contacts
        private int _currentNavTab = 0;

        public MainForm()
        {
            InitializeComponent();
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
            this.Size = new Size(1000, 680);
            this.MinimumSize = new Size(840, 540);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular);
            this.BackColor = Color.FromArgb(30, 30, 31);
            this.DoubleBuffered = true;
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            // 1. Left Sidebar (Dock / Rail)
            _sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 58,
                BackColor = Color.FromArgb(32, 32, 34)
            };

            // Avatar at top
            _btnAvatar = new Button
            {
                Size = new Size(40, 40),
                Location = new Point(9, 14),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _btnAvatar.FlatAppearance.BorderSize = 0;
            _btnAvatar.Paint += BtnAvatar_Paint;
            _btnAvatar.Click += BtnAvatar_Click;

            // Nav: Chat
            _btnNavChat = CreateNavButton("💬", 64, "聊天", true);
            _btnNavChat.Click += (s, e) => SwitchNavTab(0);

            // Nav: Contacts
            _btnNavContacts = CreateNavButton("👥", 116, "通讯录", false);
            _btnNavContacts.Click += (s, e) => SwitchNavTab(1);

            // Nav: Received Folder
            _btnNavFolder = CreateNavButton("📁", 168, "接收文件夹", false);
            _btnNavFolder.Click += (s, e) => OpenReceivedFolder();

            // Nav: Refresh
            _btnNavRefresh = CreateNavButton("🔄", 220, "刷新好友", false);
            _btnNavRefresh.Click += (s, e) => RefreshPeersWithNotice();

            // Nav: Add IP
            _btnNavAddIp = CreateNavButton("➕", 272, "添加好友 IP", false);
            _btnNavAddIp.Click += (s, e) => BtnAddIp_Click(s, e);

            // Nav: Settings at bottom
            _btnNavSettings = new Button
            {
                Text = "≡",
                Dock = DockStyle.Bottom,
                Height = 48,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font("Microsoft YaHei", 14f, FontStyle.Bold),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _btnNavSettings.FlatAppearance.BorderSize = 0;
            _btnNavSettings.MouseEnter += (s, e) => _btnNavSettings.ForeColor = Color.White;
            _btnNavSettings.MouseLeave += (s, e) => _btnNavSettings.ForeColor = Color.FromArgb(160, 160, 160);
            _btnNavSettings.Click += BtnSettings_Click;

            _sidebarPanel.Controls.Add(_btnAvatar);
            _sidebarPanel.Controls.Add(_btnNavChat);
            _sidebarPanel.Controls.Add(_btnNavContacts);
            _sidebarPanel.Controls.Add(_btnNavFolder);
            _sidebarPanel.Controls.Add(_btnNavRefresh);
            _sidebarPanel.Controls.Add(_btnNavAddIp);
            _sidebarPanel.Controls.Add(_btnNavSettings);

            // 2. Middle Sessions / Contact List
            _sessionsPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 260,
                BackColor = Color.FromArgb(47, 47, 48)
            };

            // Search and Add header
            _searchHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(47, 47, 48),
                Padding = new Padding(12, 12, 12, 12)
            };

            Panel searchBoxContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(37, 37, 38),
                Padding = new Padding(8, 6, 8, 6)
            };
            searchBoxContainer.Paint += (s, e) => {
                using (Pen pen = new Pen(Color.FromArgb(60, 60, 62)))
                {
                    DrawRoundedRectangle(e.Graphics, pen, new Rectangle(0, 0, searchBoxContainer.Width - 1, searchBoxContainer.Height - 1), 6);
                }
            };

            Label lblSearchIcon = new Label
            {
                Text = "🔍",
                Dock = DockStyle.Left,
                Width = 24,
                ForeColor = Color.FromArgb(140, 140, 140),
                Font = new Font("Microsoft YaHei", 8.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(37, 37, 38),
                ForeColor = Color.FromArgb(220, 220, 220),
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei", 9.5f)
            };
            _txtSearch.TextChanged += TxtSearch_TextChanged;

            searchBoxContainer.Controls.Add(_txtSearch);
            searchBoxContainer.Controls.Add(lblSearchIcon);

            _btnAddSession = new Button
            {
                Text = "+",
                Dock = DockStyle.Right,
                Width = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(37, 37, 38),
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Microsoft YaHei", 12f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            _btnAddSession.FlatAppearance.BorderSize = 0;
            _btnAddSession.Click += BtnAddSession_Click;

            Panel searchWrap = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            searchWrap.Controls.Add(searchBoxContainer);

            Panel addWrap = new Panel
            {
                Dock = DockStyle.Right,
                Width = 38,
                Padding = new Padding(6, 0, 0, 0),
                BackColor = Color.Transparent
            };
            addWrap.Controls.Add(_btnAddSession);

            _searchHeaderPanel.Controls.Add(searchWrap);
            _searchHeaderPanel.Controls.Add(addWrap);

            // Sessions ListBox
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

            // 3. Right Chat Panel
            _chatPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 31)
            };

            // Chat Header
            _chatHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(30, 30, 31),
                Padding = new Padding(16, 8, 16, 8)
            };
            _chatHeader.Paint += (s, e) => {
                using (Pen pen = new Pen(Color.FromArgb(42, 42, 44)))
                {
                    e.Graphics.DrawLine(pen, 0, _chatHeader.Height - 1, _chatHeader.Width, _chatHeader.Height - 1);
                }
            };

            _lblChatTitle = new Label
            {
                Text = BROADCAST_TARGET,
                Font = new Font("Microsoft YaHei", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(240, 240, 240),
                AutoSize = true,
                Location = new Point(16, 8)
            };

            _lblChatSubtitle = new Label
            {
                Text = "局域网全员广播消息通道",
                Font = new Font("Microsoft YaHei", 8.5f),
                ForeColor = Color.FromArgb(138, 138, 142),
                AutoSize = true,
                Location = new Point(17, 30)
            };

            FlowLayoutPanel chatActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            _btnChatRefresh = CreateChatHeaderButton("🔄 刷新", (s, e) => RefreshPeersWithNotice());
            _btnChatAddIp = CreateChatHeaderButton("➕ 添加IP", (s, e) => BtnAddIp_Click(s, e));
            _btnChatFolder = CreateChatHeaderButton("📂 文件夹", (s, e) => OpenReceivedFolder());
            _btnChatClear = CreateChatHeaderButton("🧹 清屏", (s, e) => ClearCurrentChat());

            chatActions.Controls.AddRange(new Control[] { _btnChatRefresh, _btnChatAddIp, _btnChatFolder, _btnChatClear });

            _chatHeader.Controls.Add(_lblChatTitle);
            _chatHeader.Controls.Add(_lblChatSubtitle);
            _chatHeader.Controls.Add(chatActions);

            // Progress Panel (Collapsible)
            _progressPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(38, 38, 41),
                Visible = false,
                Padding = new Padding(16, 6, 16, 6)
            };
            _lblProgressInfo = new Label
            {
                Text = "准备传输...",
                Font = new Font("Microsoft YaHei", 8.5f),
                ForeColor = Color.FromArgb(7, 193, 96),
                AutoSize = true,
                Location = new Point(16, 6)
            };
            _progressBar = new ProgressBar
            {
                Location = new Point(16, 26),
                Height = 12,
                Width = 500,
                Style = ProgressBarStyle.Continuous
            };
            _progressPanel.Controls.Add(_lblProgressInfo);
            _progressPanel.Controls.Add(_progressBar);
            _progressPanel.Resize += (s, e) => {
                _progressBar.Width = Math.Max(100, _progressPanel.Width - 32);
            };

            // Chat View (Custom double-buffered WeChat chat history renderer)
            _chatView = new WeChatChatView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 31)
            };
            _chatView.OnFileActionClick += HandleFileActionClick;

            // Input Toolbar
            _toolPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                BackColor = Color.FromArgb(30, 30, 31),
                Padding = new Padding(12, 4, 12, 4)
            };
            _toolPanel.Paint += (s, e) => {
                using (Pen pen = new Pen(Color.FromArgb(42, 42, 44)))
                {
                    e.Graphics.DrawLine(pen, 0, 0, _toolPanel.Width, 0);
                }
            };

            _btnToolEmoji = CreateToolButton("😊 表情", (s, e) => ShowEmojiPopup());
            _btnToolSendFile = CreateToolButton("📁 发送文件", (s, e) => BtnSendFile_Click(s, e));
            _btnToolFolder = CreateToolButton("📂 接收目录", (s, e) => OpenReceivedFolder());
            _btnToolClear = CreateToolButton("🧹 清屏", (s, e) => ClearCurrentChat());

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
                _lblAutoAccept.ForeColor = _netService.AutoAcceptFiles ? Color.FromArgb(7, 193, 96) : Color.FromArgb(160, 160, 160);
            };

            FlowLayoutPanel toolFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            toolFlow.Controls.AddRange(new Control[] { _btnToolEmoji, _btnToolSendFile, _btnToolFolder, _btnToolClear });

            _toolPanel.Controls.Add(toolFlow);
            _toolPanel.Controls.Add(_lblAutoAccept);

            // Input Text Box
            _inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 90,
                BackColor = Color.FromArgb(30, 30, 31),
                Padding = new Padding(16, 4, 16, 4)
            };

            _txtInput = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Microsoft YaHei", 10f),
                BackColor = Color.FromArgb(30, 30, 31),
                ForeColor = Color.FromArgb(236, 236, 236),
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                AllowDrop = true
            };
            _txtInput.KeyDown += TxtInput_KeyDown;
            _txtInput.TextChanged += TxtInput_TextChanged;
            _txtInput.DragEnter += MainForm_DragEnter;
            _txtInput.DragDrop += MainForm_DragDrop;

            _inputPanel.Controls.Add(_txtInput);

            // Bottom Send Panel
            _bottomSendPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                BackColor = Color.FromArgb(30, 30, 31),
                Padding = new Padding(16, 4, 16, 8)
            };

            _lblSendTip = new Label
            {
                Text = "按 Enter 发送，Ctrl+Enter 换行",
                Font = new Font("Microsoft YaHei", 8f),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoSize = true,
                Location = new Point(16, 12)
            };

            _btnSend = new Button
            {
                Text = "发送 (S)",
                Size = new Size(82, 30),
                Location = new Point(_bottomSendPanel.Width - 98, 4),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(44, 44, 46),
                ForeColor = Color.FromArgb(140, 140, 140),
                Font = new Font("Microsoft YaHei", 9f),
                Cursor = Cursors.Hand
            };
            _btnSend.FlatAppearance.BorderSize = 0;
            _btnSend.Click += BtnSend_Click;

            _bottomSendPanel.Controls.Add(_lblSendTip);
            _bottomSendPanel.Controls.Add(_btnSend);

            // Assemble Right Chat Panel
            _chatPanel.Controls.Add(_chatView);
            _chatPanel.Controls.Add(_progressPanel);
            _chatPanel.Controls.Add(_chatHeader);
            _chatPanel.Controls.Add(_toolPanel);
            _chatPanel.Controls.Add(_inputPanel);
            _chatPanel.Controls.Add(_bottomSendPanel);

            // Assemble MainForm
            this.Controls.Add(_chatPanel);
            this.Controls.Add(_sessionsPanel);
            this.Controls.Add(_sidebarPanel);

            this.FormClosing += MainForm_FormClosing;
        }

        private Button CreateNavButton(string icon, int top, string tooltipText, bool active)
        {
            Button btn = new Button
            {
                Text = icon,
                Size = new Size(58, 46),
                Location = new Point(0, top),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = active ? Color.FromArgb(7, 193, 96) : Color.FromArgb(160, 160, 160),
                Font = new Font("Segoe UI Emoji", 14f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;

            ToolTip tt = new ToolTip();
            tt.SetToolTip(btn, tooltipText);

            btn.MouseEnter += (s, e) => {
                if (btn.ForeColor != Color.FromArgb(7, 193, 96))
                    btn.ForeColor = Color.White;
            };
            btn.MouseLeave += (s, e) => {
                if (btn != GetActiveNavButton())
                    btn.ForeColor = Color.FromArgb(160, 160, 160);
            };

            return btn;
        }

        private Button GetActiveNavButton()
        {
            return _currentNavTab == 0 ? _btnNavChat : _btnNavContacts;
        }

        private Button CreateChatHeaderButton(string text, EventHandler onClick)
        {
            Button btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(44, 44, 46),
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Microsoft YaHei", 8.5f),
                Cursor = Cursors.Hand,
                Margin = new Padding(4, 4, 4, 4)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => {
                btn.BackColor = Color.FromArgb(58, 58, 60);
                btn.ForeColor = Color.White;
            };
            btn.MouseLeave += (s, e) => {
                btn.BackColor = Color.FromArgb(44, 44, 46);
                btn.ForeColor = Color.FromArgb(200, 200, 200);
            };
            btn.Click += onClick;
            return btn;
        }

        private Button CreateToolButton(string text, EventHandler onClick)
        {
            Button btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(170, 170, 170),
                Font = new Font("Microsoft YaHei", 8.5f),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.ForeColor = Color.White;
            btn.MouseLeave += (s, e) => btn.ForeColor = Color.FromArgb(170, 170, 170);
            btn.Click += onClick;
            return btn;
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
            _btnNavChat.ForeColor = tabIndex == 0 ? Color.FromArgb(7, 193, 96) : Color.FromArgb(160, 160, 160);
            _btnNavContacts.ForeColor = tabIndex == 1 ? Color.FromArgb(7, 193, 96) : Color.FromArgb(160, 160, 160);
            RefreshPeerListUI();
        }

        private void BtnAvatar_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw rounded avatar circle/box
            Rectangle rect = new Rectangle(2, 2, 34, 34);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(rect);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(7, 193, 96)))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }

            // Draw user initials
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

            // Draw online green dot
            Rectangle dotRect = new Rectangle(26, 26, 9, 9);
            using (SolidBrush dotBg = new SolidBrush(Color.FromArgb(32, 32, 34)))
            using (SolidBrush dotFg = new SolidBrush(Color.FromArgb(7, 193, 96)))
            {
                e.Graphics.FillEllipse(dotBg, new Rectangle(25, 25, 11, 11));
                e.Graphics.FillEllipse(dotFg, dotRect);
            }
        }

        private void BtnAvatar_Click(object sender, EventArgs e)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Color.FromArgb(44, 44, 46);
            menu.ForeColor = Color.White;

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
            menu.BackColor = Color.FromArgb(44, 44, 46);
            menu.ForeColor = Color.White;

            menu.Items.Add("✏️ 修改我的昵称...", null, (s, ev) => ShowChangeNickDialog());
            var autoItem = new ToolStripMenuItem("⚡ 自动接收文件") { Checked = _netService != null && _netService.AutoAcceptFiles };
            autoItem.Click += (s, ev) => {
                _netService.AutoAcceptFiles = !_netService.AutoAcceptFiles;
                _lblAutoAccept.Text = _netService.AutoAcceptFiles ? "⚡ 自动接收: 开" : "⏸ 自动接收: 关";
                _lblAutoAccept.ForeColor = _netService.AutoAcceptFiles ? Color.FromArgb(7, 193, 96) : Color.FromArgb(160, 160, 160);
            };
            menu.Items.Add(autoItem);
            menu.Items.Add("📁 打开接收文件夹", null, (s, ev) => OpenReceivedFolder());
            menu.Items.Add("🧹 清空当前聊天记录", null, (s, ev) => ClearCurrentChat());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("ℹ️ 关于 LanChat (WeChat 风格版)", null, (s, ev) => {
                MessageBox.Show(this, "LanChat v1.0\n局域网秒级极速通讯与文件互传\nUI: WeChat Desktop 风格\n无任何外部运行环境依赖", "关于 LanChat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

            menu.Show(_btnNavSettings, new Point(_btnNavSettings.Width + 4, -80));
        }

        private void ShowChangeNickDialog()
        {
            string currentNick = _netService != null ? _netService.NickName : Environment.UserName;
            using (Form prompt = new Form())
            {
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
            AppendChatSystem(string.Format("好友上线: {0} ({1} - {2})", peer.NickName, peer.HostName, peer.IP));
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
            menu.BackColor = Color.FromArgb(44, 44, 46);
            menu.ForeColor = Color.White;

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
                // Group Chat item
                Rectangle avatarRect = new Rectangle(r.Left + 12, r.Top + 12, 40, 40);
                using (SolidBrush avBrush = new SolidBrush(isSelected ? Color.FromArgb(4, 150, 74) : Color.FromArgb(32, 128, 70)))
                {
                    DrawRoundedRectangle(e.Graphics, avBrush, avatarRect, 6);
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
                using (SolidBrush subBrush = new SolidBrush(isSelected ? Color.FromArgb(230, 255, 230) : Color.FromArgb(145, 145, 145)))
                {
                    if (snippet.Length > 20) snippet = snippet.Substring(0, 19) + "...";
                    e.Graphics.DrawString(snippet, subFont, subBrush, new PointF(r.Left + 60, r.Top + 36));
                }
            }
            else if (item is PeerInfo)
            {
                PeerInfo peer = (PeerInfo)item;

                // Round Avatar
                Rectangle avatarRect = new Rectangle(r.Left + 12, r.Top + 12, 40, 40);
                Color avColor = GetAvatarColor(peer.NickName);
                using (SolidBrush avBrush = new SolidBrush(avColor))
                {
                    DrawRoundedRectangle(e.Graphics, avBrush, avatarRect, 6);
                }

                string initial = string.IsNullOrEmpty(peer.NickName) ? "友" : peer.NickName.Substring(0, 1).ToUpperInvariant();
                using (Font f = new Font("Microsoft YaHei", 11f, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(initial, f, textBrush, avatarRect, sf);
                }

                // Online indicator
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
                    string timeStr = peer.LastMessageTime != DateTime.MinValue ? peer.LastMessageTime.ToString("HH:mm") : "";
                    if (!string.IsNullOrEmpty(timeStr))
                    {
                        e.Graphics.DrawString(timeStr, timeFont, timeBrush, new PointF(r.Right - 48, r.Top + 14));
                    }
                }

                // Peer Snippet
                string snippet = peer.LastMessage;
                if (string.IsNullOrEmpty(snippet))
                {
                    snippet = string.Format("{0} ({1})", peer.HostName, peer.IP);
                }
                using (Font subFont = new Font("Microsoft YaHei", 8.5f))
                using (SolidBrush subBrush = new SolidBrush(isSelected ? Color.FromArgb(230, 255, 230) : Color.FromArgb(145, 145, 145)))
                {
                    if (snippet.Length > 22) snippet = snippet.Substring(0, 21) + "...";
                    e.Graphics.DrawString(snippet, subFont, subBrush, new PointF(r.Left + 60, r.Top + 36));
                }
            }

            // Divider line
            using (Pen divPen = new Pen(Color.FromArgb(40, 40, 42)))
            {
                e.Graphics.DrawLine(divPen, r.Left + 12, r.Bottom - 1, r.Right, r.Bottom - 1);
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
            _btnSend.BackColor = hasText ? Color.FromArgb(7, 193, 96) : Color.FromArgb(44, 44, 46);
            _btnSend.ForeColor = hasText ? Color.White : Color.FromArgb(140, 140, 140);
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

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_netService != null)
            {
                _netService.Dispose();
            }
        }

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

            // top left
            path.AddArc(arc, 180, 90);

            // top right
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // bottom right
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // bottom left
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        #endregion
    }

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
                Width = 10,
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
            int change = (e.Delta / 120) * -30;
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
            using (Font nickFont = new Font("Microsoft YaHei", 8.5f))
            {
                foreach (var msg in _messages)
                {
                    // Time header every 3 minutes
                    if ((msg.Timestamp - lastTime).TotalMinutes > 3)
                    {
                        lastTime = msg.Timestamp;
                        currentY += 24; // Space for time
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
                        // File card
                        int cardW = 240;
                        int cardH = 80;
                        int x = msg.IsSelf ? (viewWidth - cardW - 56) : 56;
                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, cardH + 20),
                            BubbleBounds = new Rectangle(x, currentY + 14, cardW, cardH),
                            AvatarBounds = msg.IsSelf ? new Rectangle(viewWidth - 46, currentY + 14, 34, 34) : new Rectangle(14, currentY + 14, 34, 34),
                            FileOpenBounds = new Rectangle(x + 12, currentY + 14 + 50, 70, 22),
                            FileFolderBounds = new Rectangle(x + 90, currentY + 14 + 50, 80, 22)
                        };
                        _renderItems.Add(r);
                        currentY += cardH + 28;
                    }
                    else
                    {
                        // Regular bubble message
                        int maxTextW = Math.Max(120, (int)(viewWidth * 0.65));
                        SizeF sz = g.MeasureString(msg.Content, msgFont, maxTextW);
                        int bubbleW = Math.Max(42, (int)Math.Ceiling(sz.Width) + 24);
                        int bubbleH = Math.Max(34, (int)Math.Ceiling(sz.Height) + 16);

                        int x = msg.IsSelf ? (viewWidth - bubbleW - 56) : 56;
                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, bubbleH + 20),
                            BubbleBounds = new Rectangle(x, currentY + 14, bubbleW, bubbleH),
                            AvatarBounds = msg.IsSelf ? new Rectangle(viewWidth - 46, currentY + 14, 34, 34) : new Rectangle(14, currentY + 14, 34, 34)
                        };
                        _renderItems.Add(r);
                        currentY += bubbleH + 24;
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
            using (SolidBrush textLight = new SolidBrush(Color.FromArgb(236, 236, 236)))
            using (SolidBrush textDark = new SolidBrush(Color.FromArgb(17, 17, 17)))
            using (SolidBrush grayText = new SolidBrush(Color.FromArgb(140, 140, 140)))
            using (SolidBrush otherBubbleBrush = new SolidBrush(Color.FromArgb(44, 44, 46))) // Dark WeChat bubble
            using (SolidBrush selfBubbleBrush = new SolidBrush(Color.FromArgb(7, 193, 96)))    // WeChat Green bubble
            using (Pen otherBubblePen = new Pen(Color.FromArgb(52, 52, 54)))
            {
                DateTime lastTime = DateTime.MinValue;

                foreach (var item in _renderItems)
                {
                    int itemTop = item.Bounds.Top - scrollY;
                    int itemBottom = item.Bounds.Bottom - scrollY;

                    if (itemBottom < 0 || itemTop > this.Height) continue;

                    ChatMessage msg = item.Message;

                    // Time header
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
                        // Centered tip
                        SizeF sz = g.MeasureString(msg.Content, timeFont, viewWidth - 80);
                        float sx = (viewWidth - sz.Width) / 2f;
                        g.DrawString(msg.Content, timeFont, grayText, new RectangleF(sx, itemTop + 4, sz.Width + 4, sz.Height + 4));
                    }
                    else if (msg.IsFile)
                    {
                        // Draw Avatar
                        Rectangle av = new Rectangle(item.AvatarBounds.X, item.AvatarBounds.Y - scrollY, item.AvatarBounds.Width, item.AvatarBounds.Height);
                        DrawAvatar(g, msg.SenderNick, av, msg.IsSelf);

                        // Draw File Card Bubble
                        Rectangle bb = new Rectangle(item.BubbleBounds.X, item.BubbleBounds.Y - scrollY, item.BubbleBounds.Width, item.BubbleBounds.Height);
                        MainForm.DrawRoundedRectangle(g, otherBubbleBrush, bb, 6);
                        MainForm.DrawRoundedRectangle(g, otherBubblePen, bb, 6);

                        // File Icon & Details
                        Rectangle iconRect = new Rectangle(bb.Left + 12, bb.Top + 12, 32, 32);
                        g.DrawString("📄", new Font("Segoe UI Emoji", 16f), textLight, iconRect);

                        string nameSnippet = msg.FileName ?? "未知文件";
                        if (nameSnippet.Length > 16) nameSnippet = nameSnippet.Substring(0, 15) + "...";
                        g.DrawString(nameSnippet, new Font("Microsoft YaHei", 9f, FontStyle.Bold), textLight, bb.Left + 48, bb.Top + 10);
                        g.DrawString(MainForm_FormatFileSize(msg.FileSize), new Font("Microsoft YaHei", 8f), grayText, bb.Left + 48, bb.Top + 28);

                        // Buttons
                        Rectangle bOpen = new Rectangle(item.FileOpenBounds.X, item.FileOpenBounds.Y - scrollY, item.FileOpenBounds.Width, item.FileOpenBounds.Height);
                        Rectangle bFolder = new Rectangle(item.FileFolderBounds.X, item.FileFolderBounds.Y - scrollY, item.FileFolderBounds.Width, item.FileFolderBounds.Height);

                        using (SolidBrush btnBrush = new SolidBrush(Color.FromArgb(60, 60, 64)))
                        {
                            MainForm.DrawRoundedRectangle(g, btnBrush, bOpen, 4);
                            MainForm.DrawRoundedRectangle(g, btnBrush, bFolder, 4);
                        }
                        StringFormat bsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("打开文件", timeFont, textLight, bOpen, bsf);
                        g.DrawString("打开目录", timeFont, textLight, bFolder, bsf);
                    }
                    else
                    {
                        // Draw Avatar
                        Rectangle av = new Rectangle(item.AvatarBounds.X, item.AvatarBounds.Y - scrollY, item.AvatarBounds.Width, item.AvatarBounds.Height);
                        DrawAvatar(g, msg.SenderNick, av, msg.IsSelf);

                        // Draw Bubble
                        Rectangle bb = new Rectangle(item.BubbleBounds.X, item.BubbleBounds.Y - scrollY, item.BubbleBounds.Width, item.BubbleBounds.Height);

                        if (msg.IsSelf)
                        {
                            // Self WeChat Green bubble
                            MainForm.DrawRoundedRectangle(g, selfBubbleBrush, bb, 6);

                            // Triangle pointing right
                            Point[] tri = new Point[]
                            {
                                new Point(bb.Right, bb.Top + 12),
                                new Point(bb.Right + 5, bb.Top + 17),
                                new Point(bb.Right, bb.Top + 22)
                            };
                            g.FillPolygon(selfBubbleBrush, tri);

                            // Text
                            Rectangle textRect = new Rectangle(bb.Left + 12, bb.Top + 8, bb.Width - 24, bb.Height - 16);
                            g.DrawString(msg.Content, msgFont, textLight, textRect);
                        }
                        else
                        {
                            // Nickname on other side
                            g.DrawString(msg.SenderNick ?? "好友", nickFont, grayText, bb.Left, bb.Top - 14);

                            // Other Dark Gray bubble
                            MainForm.DrawRoundedRectangle(g, otherBubbleBrush, bb, 6);
                            MainForm.DrawRoundedRectangle(g, otherBubblePen, bb, 6);

                            // Triangle pointing left
                            Point[] tri = new Point[]
                            {
                                new Point(bb.Left, bb.Top + 12),
                                new Point(bb.Left - 5, bb.Top + 17),
                                new Point(bb.Left, bb.Top + 22)
                            };
                            g.FillPolygon(otherBubbleBrush, tri);

                            // Text
                            Rectangle textRect = new Rectangle(bb.Left + 12, bb.Top + 8, bb.Width - 24, bb.Height - 16);
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
                MainForm.DrawRoundedRectangle(g, avBrush, rect, 6);
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
