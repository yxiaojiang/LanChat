using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LanChat
{
    public partial class MainForm : Form
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

        // Constants
        public const string APP_VERSION = "2.0.0";
        public const string GITHUB_REPO = "yxiaojiang/LanChat";

        // System Tray & Single Instance
        private NotifyIcon _trayIcon;
        private bool _isExplicitExit = false;
        private bool _hasShownTrayBalloon = false;
        private int _wakeupMessageId = 0;

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

            try
            {
                _wakeupMessageId = Program.RegisterWindowMessage(Program.WakeupMessageName);
            }
            catch { }
        }

        protected override void WndProc(ref Message m)
        {
            if (_wakeupMessageId != 0 && m.Msg == _wakeupMessageId)
            {
                // 收到已有实例唤醒消息：立即从托盘唤出并前置
                RestoreFromTray();
                return;
            }
            base.WndProc(ref m);
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

        #region SafeInvoke & Helpers

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
}
