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

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        public static bool ReleaseCaptureWin32() { return ReleaseCapture(); }

        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        public static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        public static extern IntPtr SendMessageWin32(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessagePtr(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const string BROADCAST_TARGET = "📢 局域网全员群聊";
        private NetworkService _netService;

        // Session and chat history coordination service
        private readonly SessionManager _sessionManager = new SessionManager();
        private string _activeChatKey
        {
            get { return _sessionManager.ActiveSessionKey; }
            set { _sessionManager.ActiveSessionKey = value; }
        }

        private PeerInfo _selectedPeer = null;
        private int _broadcastUnreadCount = 0;

        // UI Layout Panels
        private Panel _sidebarPanel;
        private Panel _sessionsPanel;
        private Panel _chatPanel;

        // Left Column Hero & Bottom Controls
        private Control _btnAvatar;
        private Panel _heroProfilePanel;
        private Panel _leftBottomBar;
        private Button _btnBottomUser;
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
        private WeChatIconButton _btnSearchClear;
        private FormResizeMessageFilter _resizeFilter;
        private WeChatIconButton _btnSearchToggle;
        private SessionListBox _listSessions;
        private List<object> _allSessionItems = new List<object>();
        private int _hoverSessionIndex = -1;

        // Chat Header Controls (Floating Pill + Window Buttons)
        private Panel _chatHeader;
        private Label _lblChatTitle;
        private Label _lblChatSubtitle;
        private WeChatIconButton _btnWinMin;
        private WeChatIconButton _btnWinMax;
        private WeChatIconButton _btnWinClose;
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

        // Input Capsule Controls (GrokBot 64px floating pill — Docs §4.2 / §8)
        private Panel _toolPanel; // 兼容占位（方案 A 工具栏已移除）
        private WeChatIconButton _btnToolEmoji; // 兼容：指向胶囊内 (+) 以便 Emoji 弹层锚定
        private WeChatIconButton _btnToolBox; // 兼容占位
        private WeChatIconButton _btnToolSendFile; // 胶囊内 (+) 附件圆钮
        private WeChatIconButton _btnToolScissors; // 兼容占位
        private WeChatIconButton _btnToolMic; // 兼容：指向胶囊内 Mic
        private WeChatIconButton _btnToolCall; // 兼容占位
        private WeChatIconButton _btnToolFolder; // 胶囊内 Mic（历史字段名复用）
        private WeChatIconButton _btnToolClear;
        private Label _lblAutoAccept;

        private Panel _inputPanel;
        private TextBox _txtInput;
        private Panel _bottomSendPanel;
        private Label _lblSendTip;
        private Button _btnSend; // 兼容占位（发送改由 _btnSendWave）
        private WeChatIconButton _btnSendWave; // 魅紫 ↑ 发送圆钮
        private string _inputPlaceholder = "给 局域网全员群聊 发消息";

        // Navigation state: 0 = Chat, 1 = Contacts
        private int _currentNavTab = 0;

        // Constants
        public const string APP_VERSION = "2.0.11";
        public const string GITHUB_REPO = "yxiaojiang/LanChat";

        // System Tray & Single Instance
        private NotifyIcon _trayIcon;
        private GrokTrayPreviewPopup _trayPreviewPopup;
        private string _lastBroadcastSnippet;
        private string _lastBroadcastTimeText;
        private bool _isExplicitExit = false;
        private bool _hasShownTrayBalloon = false;
        [DllImport("user32.dll", EntryPoint = "SetClassLong")]
        private static extern uint SetClassLong32(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
        private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetClassLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetClassLong32(hWnd, nIndex, (uint)dwNewLong.ToInt32()));
        }

        private int _wakeupMessageId = 0;

        public MainForm()
        {
            try
            {
                // 1. 优先从程序集嵌入资源加载官方应用图标（保证安装到任何其他电脑上都100%存在且一致）
                using (Stream resStream = typeof(MainForm).Assembly.GetManifestResourceStream("LanChat.app.ico"))
                {
                    if (resStream != null)
                    {
                        this.Icon = new Icon(resStream);
                    }
                }
            }
            catch { }

            if (this.Icon == null)
            {
                try
                {
                    string[] possibleIconPaths = new string[]
                    {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "app.ico"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "assets", "app.ico"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "assets", "app.ico")
                    };
                    foreach (string p in possibleIconPaths)
                    {
                        if (File.Exists(p))
                        {
                            try { this.Icon = new Icon(p); break; } catch { }
                        }
                    }
                    if (this.Icon == null)
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
            }

            // 2. 最终兜底：内存动态自绘 Mascot 图标，坚决杜绝 Windows 原生红黄蓝图标
            if (this.Icon == null)
            {
                this.Icon = WeChatIcon.CreateAppIcon(32);
            }

            // 加载持久化设置 (保证升级覆盖后昵称与偏好不丢失)
            AppSettings.Load();
            _sessionManager.LoadPersistedHistories();

            InitializeComponent();
            ApplyFontSettings();
            _resizeFilter = new FormResizeMessageFilter(this);
            Application.AddMessageFilter(_resizeFilter);
            this.FormClosed += (s, e) => {
                if (_resizeFilter != null) { Application.RemoveMessageFilter(_resizeFilter); _resizeFilter = null; }
            };
            InitializeTrayIcon();
            InitializeNetwork();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // 支持无边框窗口点击任务栏最小化/还原及系统动画 (WS_MINIMIZEBOX | WS_SYSMENU)
                cp.Style |= 0x00020000 | 0x00080000;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int darkMode = 1;
                DwmSetWindowAttribute(this.Handle, 20, ref darkMode, sizeof(int));
            }
            catch { }

            try
            {
                _wakeupMessageId = Program.RegisterWindowMessage(Program.WakeupMessageName);
            }
            catch { }

            // 彻底解决任务栏图标显示旧图标问题：显式向 HWND 句柄发送 WM_SETICON 并写入窗口类图标
            if (this.Icon != null)
            {
                try
                {
                    SendMessagePtr(this.Handle, 0x0080 /* WM_SETICON */, (IntPtr)0 /* ICON_SMALL */, this.Icon.Handle);
                    SendMessagePtr(this.Handle, 0x0080 /* WM_SETICON */, (IntPtr)1 /* ICON_BIG */, this.Icon.Handle);
                    SetClassLongPtr(this.Handle, -14 /* GCLP_HICON */, this.Icon.Handle);
                    SetClassLongPtr(this.Handle, -34 /* GCLP_HICONSM */, this.Icon.Handle);
                }
                catch { }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (this.Icon != null)
            {
                try
                {
                    SendMessagePtr(this.Handle, 0x0080, (IntPtr)0, this.Icon.Handle);
                    SendMessagePtr(this.Handle, 0x0080, (IntPtr)1, this.Icon.Handle);
                }
                catch { }
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (_wakeupMessageId != 0 && m.Msg == _wakeupMessageId)
            {
                RestoreFromTray();
                return;
            }

            // WM_NCHITTEST: 支持无边框窗口四周与四角拖拽缩放大小
            if (m.Msg == 0x0084 && this.WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if ((int)m.Result == 1) // HTCLIENT
                {
                    Point pt = this.PointToClient(Cursor.Position);
                    int grip = 6;
                    if (pt.X < grip && pt.Y < grip) { m.Result = (IntPtr)13; return; } // HTTOPLEFT
                    if (pt.X > this.Width - grip && pt.Y < grip) { m.Result = (IntPtr)14; return; } // HTTOPRIGHT
                    if (pt.X < grip && pt.Y > this.Height - grip) { m.Result = (IntPtr)16; return; } // HTBOTTOMLEFT
                    if (pt.X > this.Width - grip && pt.Y > this.Height - grip) { m.Result = (IntPtr)17; return; } // HTBOTTOMRIGHT
                    if (pt.X < grip) { m.Result = (IntPtr)10; return; } // HTLEFT
                    if (pt.X > this.Width - grip) { m.Result = (IntPtr)11; return; } // HTRIGHT
                    if (pt.Y < grip) { m.Result = (IntPtr)12; return; } // HTTOP
                    if (pt.Y > this.Height - grip) { m.Result = (IntPtr)15; return; } // HTBOTTOM
                }
                return;
            }

            base.WndProc(ref m);
        }

        private void DragWindow_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (e.Clicks == 2)
                {
                    ToggleMaximize();
                    return;
                }
                ReleaseCapture();
                SendMessage(this.Handle, 0x00A1, 0x0002, 0);
            }
        }

        private void ToggleMaximize()
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
                if (_btnWinMax != null)
                {
                    _btnWinMax.IconType = WeChatIconType.WinMax;
                    _btnWinMax.TooltipText = "最大化";
                }
            }
            else
            {
                this.MaximizedBounds = Screen.FromHandle(this.Handle).WorkingArea;
                this.WindowState = FormWindowState.Maximized;
                if (_btnWinMax != null)
                {
                    _btnWinMax.IconType = WeChatIconType.WinRestore;
                    _btnWinMax.TooltipText = "向下还原";
                }
            }
            Invalidate(true);
        }

        private void InitializeComponent()
        {
            this.Text = "LanChat";
            this.ShowIcon = true;
            this.ShowInTaskbar = true;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(1040, 720); // GrokBot Main 1040x720
            this.MinimumSize = new Size(880, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            this.BackColor = Color.FromArgb(18, 18, 18); // Grok Main Dark #121212
            this.DoubleBuffered = true;
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            // 保留兼容字段实例，避免其余分部方法空引用
            _sidebarPanel = new Panel { Visible = false, Width = 0 };
            _btnNavChat = new WeChatNavButton(WeChatIconType.Chat, "聊天", true);
            _btnNavContacts = new WeChatNavButton(WeChatIconType.Contacts, "通讯录", false);
            _btnNavFolder = new WeChatNavButton(WeChatIconType.Folder, "接收文件夹", false);
            _btnNavRefresh = new WeChatNavButton(WeChatIconType.Refresh, "刷新好友", false);
            _btnNavAddIp = new WeChatNavButton(WeChatIconType.Add, "添加好友 IP", false);
            _btnNavMenu = new WeChatNavButton(WeChatIconType.Menu, "设置与更多", false);
            _lblAutoAccept = new Label { Visible = false };
            _lblChatSubtitle = new Label { Visible = false };
            _lblSendTip = new Label { Visible = false };
            _btnSend = new Button { Visible = false };
            _toolPanel = new Panel { Visible = false };
            _btnToolBox = new WeChatIconButton(WeChatIconType.Box, "", 28) { Visible = false };
            _btnToolScissors = new WeChatIconButton(WeChatIconType.Scissors, "", 28) { Visible = false };
            _btnToolCall = new WeChatIconButton(WeChatIconType.Call, "", 28) { Visible = false };
            _btnHeaderRefresh = new WeChatIconButton(WeChatIconType.Refresh, "", 28) { Visible = false };
            _btnHeaderAddIp = new WeChatIconButton(WeChatIconType.Add, "", 28) { Visible = false };
            _btnHeaderFolder = new WeChatIconButton(WeChatIconType.Folder, "", 28) { Visible = false };
            _btnHeaderClear = new WeChatIconButton(WeChatIconType.Clear, "", 28) { Visible = false };
            _btnToolClear = new WeChatIconButton(WeChatIconType.Clear, "", 28) { Visible = false };

            // ==========================================
            // 1. Left Sidebar Column (420px Fixed, #0E0E0E, exact Grok Bot layout)
            // ==========================================
            _sessionsPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 420,
                BackColor = Color.FromArgb(14, 14, 14) // #0E0E0E
            };

                        // 1.1 Top Search Action Row (Height: 60px) - Search Bar & Button occupy whole row
            _searchHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(14, 14, 14)
            };
            _searchHeaderPanel.MouseDown += DragWindow_MouseDown;

            _searchBoxContainer = new Panel
            {
                Location = new Point(16, 9),
                Size = new Size(330, 42),
                BackColor = Color.FromArgb(32, 32, 36), // #202024
                Visible = true
            };
            _searchBoxContainer.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(Color.FromArgb(44, 44, 50), 1f)) // #2C2C32
                {
                    DrawRoundedRectangle(e.Graphics, pen, new Rectangle(0, 0, _searchBoxContainer.Width - 1, _searchBoxContainer.Height - 1), 21);
                }
            };

            _txtSearch = new TextBox
            {
                Location = new Point(14, 10),
                Width = 270,
                BackColor = Color.FromArgb(32, 32, 36),
                ForeColor = Color.FromArgb(242, 242, 246), // #F2F2F6
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei UI", 10.0f)
            };
            _txtSearch.TextChanged += TxtSearch_TextChanged;
            _txtSearch.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Escape)
                {
                    _txtSearch.Clear();
                }
            };

            // 搜索框小叉清除按钮 (×)：24x24，输入框有内容时浮现
            _btnSearchClear = new WeChatIconButton(WeChatIconType.Dismiss, "清除搜索内容", 24)
            {
                IsCircle = true,
                FillColor = Color.Transparent,
                HoverFillColor = Color.FromArgb(48, 48, 54),
                IconColor = Color.FromArgb(142, 142, 147),
                Visible = false,
                Cursor = Cursors.Hand
            };
            _btnSearchClear.Click += (s, e) => {
                _txtSearch.Clear();
                _txtSearch.Focus();
            };

            _searchBoxContainer.Controls.Add(_txtSearch);
            _searchBoxContainer.Controls.Add(_btnSearchClear);
            _searchBoxContainer.Resize += (s, e) => {
                int clearSize = 24;
                if (_btnSearchClear != null)
                {
                    _btnSearchClear.Location = new Point(_searchBoxContainer.Width - clearSize - 10, (_searchBoxContainer.Height - clearSize) / 2);
                }
                if (_txtSearch != null)
                {
                    int rightMargin = (_btnSearchClear != null && _btnSearchClear.Visible) ? (clearSize + 16) : 28;
                    _txtSearch.Width = Math.Max(40, _searchBoxContainer.Width - 14 - rightMargin);
                    _txtSearch.Location = new Point(14, (_searchBoxContainer.Height - _txtSearch.Height) / 2);
                }
            };

            // Top circular Search button at right (46x46, 2x enlarged icon)
            _btnSearchToggle = new WeChatIconButton(WeChatIconType.Search, "搜索会话", 46)
            {
                Location = new Point(358, 7),
                IsCircle = true,
                FillColor = Color.FromArgb(32, 32, 36), // #202024 Level2
                HoverFillColor = Color.FromArgb(46, 46, 52), // #2E2E34
                IconColor = Color.FromArgb(200, 200, 205)
            };
            _btnSearchToggle.Click += (s, e) => {
                _txtSearch.Focus();
                ApplyFilterAndRenderList();
            };

            _searchHeaderPanel.Controls.Add(_searchBoxContainer);
            _searchHeaderPanel.Controls.Add(_btnSearchToggle);

            // 1.2 Center Hero Profile Section (82x82 Red Mascot Avatar + Blue Dot + Centered Nickname)
            _heroProfilePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 175,
                BackColor = Color.FromArgb(14, 14, 14),
                Cursor = Cursors.Default
            };
            _heroProfilePanel.Paint += BtnAvatar_Paint;
            // 自己的头像设为不可点击
            _btnAvatar = _heroProfilePanel;

            // 1.3 Bottom 3-Part Action Bar ([用户圆钮] [连接节点胶囊] [↑蓝圆钮]) (Height: 84px)
            _leftBottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 84,
                BackColor = Color.FromArgb(14, 14, 14)
            };

            // Left circular user badge (54x54)
            _btnBottomUser = new Button
            {
                Location = new Point(24, 6),
                Size = new Size(54, 54),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _btnBottomUser.FlatAppearance.BorderSize = 0;
            _btnBottomUser.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnBottomUser.FlatAppearance.MouseDownBackColor = Color.Transparent;
            _btnBottomUser.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                GrokTheme.PrepareSoftText(e.Graphics);
                Rectangle r = new Rectangle(0, 0, _btnBottomUser.Width - 1, _btnBottomUser.Height - 1);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 44))) // #28282C Level3
                using (Pen p = new Pen(Color.FromArgb(44, 44, 50), 1f)) // #2C2C32
                {
                    e.Graphics.FillEllipse(b, r);
                    e.Graphics.DrawEllipse(p, r);
                }
                string nick = _netService != null && !string.IsNullOrEmpty(_netService.NickName) ? _netService.NickName : Environment.UserName;
                nick = GrokTheme.SanitizeUiText(nick);
                if (string.IsNullOrEmpty(nick)) nick = "?";
                if (nick.Length > 2) nick = nick.Substring(0, 2);
                // Bold 9.5 + soft AA + TextPill — fuller badge glyph like Grok user chip
                using (Font f = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold))
                {
                    GrokTheme.DrawUiTextCentered(e.Graphics, nick, f, GrokTheme.TextPill,
                        new Rectangle(0, 0, _btnBottomUser.Width, _btnBottomUser.Height));
                }
            };
                        _btnBottomUser.Click += BtnSettings_Click;
            _btnBottomUser.MouseUp += (s, e) => {
                if (e.Button == MouseButtons.Right)
                {
                    ShowSettingsDialog(0);
                }
            };

            _leftBottomBar.Controls.Add(_btnBottomUser);

            // 1.4 Sessions ListBox (Height: 72px denser Grok rows, OwnerDrawn double-buffered)
            _listSessions = new SessionListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(14, 14, 14),
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 11.0f),
                ItemHeight = 72,
                DrawMode = DrawMode.OwnerDrawFixed,
                IntegralHeight = false
            };
            _listSessions.DrawItem += ListSessions_DrawItem;
            _listSessions.SelectedIndexChanged += ListSessions_SelectedIndexChanged;
            _listSessions.MouseUp += ListSessions_MouseUp;
            _listSessions.MouseMove += ListSessions_MouseMove;
            _listSessions.MouseLeave += ListSessions_MouseLeave;

            _sessionsPanel.Controls.Add(_listSessions);
            _sessionsPanel.Controls.Add(_leftBottomBar);
            _sessionsPanel.Controls.Add(_heroProfilePanel);
            _sessionsPanel.Controls.Add(_searchHeaderPanel);

            // ==========================================
            // 2. Right Chat Panel (Grok Deep Dark #121212)
            // ==========================================
            _chatPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 18) // #121212
            };

            // 2.1 Top Header with Centered Floating Pill + Frameless Window Buttons
            _chatHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 96,
                BackColor = Color.FromArgb(18, 18, 18)
            };
            _chatHeader.MouseDown += DragWindow_MouseDown;

            _lblChatTitle = new Label
            {
                Text = "局域网全员群聊",
                Visible = false
            };

            _chatHeader.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                GrokTheme.PrepareSoftText(g);

                string pillTitle = GrokTheme.SanitizeUiText(_lblChatTitle.Text ?? "局域网全员群聊");
                using (Font pillFont = new Font("Microsoft YaHei UI", 20.0f, FontStyle.Bold))
                {
                    Size tsz = GrokTheme.MeasureUiText(g, pillTitle, pillFont);
                    int pillH = 76;
                    int pillW = Math.Max(240, tsz.Width + 106);
                    int pillX = (_chatHeader.Width - pillW) / 2;
                    int pillY = (_chatHeader.Height - pillH) / 2;

                    Rectangle pillRect = new Rectangle(pillX, pillY, pillW, pillH);
                    using (SolidBrush pillBg = new SolidBrush(Color.FromArgb(32, 32, 34)))
                    using (Pen pillBorder = new Pen(Color.FromArgb(46, 46, 50), 1f))
                    {
                        DrawRoundedRectangle(g, pillBg, pillRect, 38);
                        DrawRoundedRectangle(g, pillBorder, pillRect, 38);
                    }

                    // Draw mascot avatar inside floating pill (48x48, 整体放大两倍)
                    int avSize = 48;
                    int avX = pillX + 20;
                    int avY = pillY + (pillH - avSize) / 2;
                    Rectangle avRect = new Rectangle(avX, avY, avSize, avSize);
                    Color avColor = _selectedPeer == null ? Color.FromArgb(144, 90, 251) : Color.FromArgb(246, 148, 18);
                    WeChatIcon.DrawMascotAvatar(g, avRect, avColor, false, false, Color.Transparent);

                    GrokTheme.DrawUiTextLeft(g, pillTitle, pillFont, GrokTheme.TextPill,
                        pillX + 80, pillY, pillW - 96, pillH);
                }
            };
            _chatHeader.Resize += (s, e) => _chatHeader.Invalidate();

            // Window control buttons at top-right (— □ ×) (恢复为标准 56x64 尺寸，紧贴右上角)
            Panel winCtrlPanel = new Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(_chatHeader.Width - 168, 0),
                Size = new Size(168, 64),
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _btnWinMin = new WeChatIconButton(WeChatIconType.WinMin, "最小化", 56)
            {
                Location = new Point(0, 0),
                Size = new Size(56, 64),
                IconColor = Color.FromArgb(205, 205, 210),
                HoverFillColor = Color.FromArgb(55, 55, 58),
                IsCircle = false
            };
            _btnWinMin.Click += (s, e) => { this.WindowState = FormWindowState.Minimized; };

            _btnWinMax = new WeChatIconButton(WeChatIconType.WinMax, "最大化", 56)
            {
                Location = new Point(56, 0),
                Size = new Size(56, 64),
                IconColor = Color.FromArgb(205, 205, 210),
                HoverFillColor = Color.FromArgb(55, 55, 58),
                IsCircle = false
            };
            _btnWinMax.Click += (s, e) => ToggleMaximize();

            _btnWinClose = new WeChatIconButton(WeChatIconType.WinClose, "关闭", 56)
            {
                Location = new Point(112, 0),
                Size = new Size(56, 64),
                IconColor = Color.FromArgb(205, 205, 210),
                IsWindowClose = true,
                IsCircle = false
            };
            _btnWinClose.Click += (s, e) => this.Close();

            winCtrlPanel.Controls.Add(_btnWinMin);
            winCtrlPanel.Controls.Add(_btnWinMax);
            winCtrlPanel.Controls.Add(_btnWinClose);
            _chatHeader.Controls.Add(winCtrlPanel);

            // 2.2 Collapsible Transfer Progress Banner
            _progressPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(24, 24, 26),
                Visible = false,
                Padding = new Padding(18, 6, 18, 6)
            };
            _lblProgressInfo = new Label
            {
                Text = "准备传输...",
                Font = new Font("Microsoft YaHei UI", 9.0f),
                ForeColor = Color.FromArgb(142, 142, 147), // #8E8E93 secondary
                AutoSize = true,
                Location = new Point(18, 5)
            };
            _progressBar = new ProgressBar
            {
                Location = new Point(18, 24),
                Height = 10,
                Width = 500,
                Style = ProgressBarStyle.Continuous
            };
            _progressPanel.Controls.Add(_lblProgressInfo);
            _progressPanel.Controls.Add(_progressBar);
            _progressPanel.Resize += (s, e) => {
                _progressBar.Width = Math.Max(100, _progressPanel.Width - 36);
            };

            // 2.3 Grok-Style Chat History View
            _chatView = new WeChatChatView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 18),
                ShowGroupSenderName = true
            };
            _chatView.OnFileActionClick += HandleFileActionClick;

            // 2.4 Bottom Floating Input Capsule (高 64px / 圆角 32px / 左右 28px / 底 22px，对齐 Docs §4.2 §8 与 Alis.png)
            _bottomSendPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 90, // 上 4 + 胶囊 64 + 底 22
                BackColor = Color.FromArgb(18, 18, 18), // #121212 与聊天视口同色，呈浮动感
                Padding = new Padding(28, 4, 28, 22)
            };

            _inputPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 18)
            };
            _inputPanel.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                GrokTheme.PrepareSoftText(g);
                Rectangle pillRect = new Rectangle(0, 0, _inputPanel.Width - 1, _inputPanel.Height - 1);
                int radius = Math.Max(8, _inputPanel.Height / 2); // 64→32 全圆角胶囊
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(32, 32, 36))) // #202024
                using (Pen border = new Pen(Color.FromArgb(44, 44, 50), 1f)) // #2C2C32
                {
                    DrawRoundedRectangle(g, bg, pillRect, radius);
                    DrawRoundedRectangle(g, border, pillRect, radius);
                }

                // 空输入时绘制占位符（11.0 Regular #5E5E66 + soft AA，对标 Grok 胶囊 muted）
                if (_txtInput != null && string.IsNullOrEmpty(_txtInput.Text))
                {
                    using (Font pf = new Font("Microsoft YaHei UI", 11.0f, FontStyle.Regular))
                    {
                        GrokTheme.DrawUiTextLeft(g, _inputPlaceholder, pf, GrokTheme.TextPlaceholder,
                            64, 0, Math.Max(40, _inputPanel.Width - 64 - 56), _inputPanel.Height);
                    }
                }
            };
            _inputPanel.Cursor = Cursors.IBeam;
            _inputPanel.MouseDown += (s, e) => {
                _txtInput.Visible = true;
                _txtInput.Focus();
                _inputPanel.Invalidate();
            };
            _inputPanel.AllowDrop = true;
            _inputPanel.DragEnter += MainForm_DragEnter;
            _inputPanel.DragDrop += MainForm_DragDrop;

            // 左侧 (+) 附件圆钮：36x36，x=14，bg #2A2A2E / hover #38383E
            _btnToolSendFile = new WeChatIconButton(WeChatIconType.Add, "附件与快捷操作 (点击打开菜单)", 36)
            {
                Location = new Point(14, 14),
                IsCircle = true,
                FillColor = Color.FromArgb(42, 42, 46), // #2A2A2E
                HoverFillColor = Color.FromArgb(56, 56, 62), // #38383E
                IconColor = Color.FromArgb(160, 160, 165) // #A0A0A5
            };
            _btnToolSendFile.Click += (s, e) => ShowInputPlusContextMenu();
            _btnToolSendFile.MouseUp += (s, e) => {
                if (e.Button == MouseButtons.Right)
                {
                    ShowInputPlusContextMenu();
                }
            };
            _btnToolEmoji = _btnToolSendFile; // Emoji 弹层锚定到 (+)

            // 单行无边框输入框（11.0pt #F2F2F6，起点 x≈64，垂直居中）
            _txtInput = new TextBox
            {
                Multiline = false,
                Font = new Font("Microsoft YaHei UI", 11.0f, FontStyle.Regular),
                BackColor = Color.FromArgb(32, 32, 36), // #202024
                ForeColor = Color.FromArgb(242, 242, 246), // #F2F2F6
                BorderStyle = BorderStyle.None,
                Location = new Point(64, 21),
                Width = 500,
                Visible = false,
                AllowDrop = true
            };
            _txtInput.KeyDown += TxtInput_KeyDown;
            _txtInput.TextChanged += TxtInput_TextChanged;
            _txtInput.LostFocus += (s, e) => {
                if (string.IsNullOrEmpty(_txtInput.Text)) _txtInput.Visible = false;
                _inputPanel.Invalidate();
            };
            _txtInput.GotFocus += (s, e) => {
                _txtInput.Visible = true;
                _inputPanel.Invalidate();
            };
            _txtInput.DragEnter += MainForm_DragEnter;
            _txtInput.DragDrop += MainForm_DragDrop;

            // 语音按钮已去除，保留占位以兼容原有字段
            _btnToolFolder = new WeChatIconButton(WeChatIconType.Mic, "", 32) { Visible = false };
            _btnToolMic = _btnToolFolder;

            // 最右侧魅紫 ↑ 发送圆钮：44x44，#8E64DC / hover #9D75EC，距右缘 10px
            _btnSendWave = new WeChatIconButton(WeChatIconType.UpArrow, "发送消息 (Enter)", 44)
            {
                IsCircle = true,
                FillColor = Color.FromArgb(142, 100, 220), // #8E64DC
                HoverFillColor = Color.FromArgb(157, 117, 236), // #9D75EC
                IconColor = Color.White,
                IsActive = false
            };
            _btnSendWave.Click += BtnSend_Click;

            _inputPanel.Resize += (s, e) => {
                int w = _inputPanel.Width;
                int h = _inputPanel.Height;

                int sendSize = 44;
                _btnSendWave.Size = new Size(sendSize, sendSize);
                _btnSendWave.Location = new Point(w - sendSize - 10, (h - sendSize) / 2);

                // 左侧 (+) 附件圆钮：38x38，垂直居中
                int plusSize = 38;
                _btnToolSendFile.Size = new Size(plusSize, plusSize);
                _btnToolSendFile.Location = new Point(13, (h - plusSize) / 2);

                // 输入框起点在 (+) 之后，终点延伸至发送圆钮左侧（语音图标已去除）
                _txtInput.Location = new Point(60, (h - _txtInput.Height) / 2);
                _txtInput.Width = Math.Max(60, _btnSendWave.Left - _txtInput.Left - 16);
                _inputPanel.Invalidate();
            };

            _inputPanel.Controls.Add(_btnToolSendFile);
            _inputPanel.Controls.Add(_txtInput);
            _inputPanel.Controls.Add(_btnSendWave);
            _bottomSendPanel.Controls.Add(_inputPanel);

            // Assemble Panels
            _chatPanel.Controls.Add(_chatView);
            _chatPanel.Controls.Add(_progressPanel);
            _chatPanel.Controls.Add(_chatHeader);
            _chatPanel.Controls.Add(_bottomSendPanel);

            this.Controls.Add(_chatPanel);
            this.Controls.Add(_sessionsPanel);

            this.Resize += (s, e) => UpdateResponsiveLayout();
            _chatPanel.Resize += (s, e) => UpdateResponsiveLayout();
            this.Activated += MainForm_Activated;
            this.MouseDown += (s, e) => ClearActiveChatUnread();
            _chatView.MouseDown += (s, e) => ClearActiveChatUnread();
            _txtInput.MouseDown += (s, e) => ClearActiveChatUnread();
            _txtInput.Enter += (s, e) => ClearActiveChatUnread();
            _chatHeader.MouseDown += (s, e) => ClearActiveChatUnread();
            _listSessions.MouseDown += (s, e) => ClearActiveChatUnread();
            UpdateResponsiveLayout();

            this.Shown += (s, e) => {
                UpdateResponsiveLayout();
                _chatView.Focus();
            };
            this.FormClosing += MainForm_FormClosing;
        }

        private void UpdateResponsiveLayout()
        {
            if (this.Width <= 200 || this.Height <= 200) return;

            if (_btnWinMax != null)
            {
                if (this.WindowState == FormWindowState.Maximized)
                {
                    _btnWinMax.IconType = WeChatIconType.WinRestore;
                    _btnWinMax.TooltipText = "向下还原";
                }
                else
                {
                    _btnWinMax.IconType = WeChatIconType.WinMax;
                    _btnWinMax.TooltipText = "最大化";
                }
            }

            // 1. 左侧边栏宽度在正常桌面尺寸下严格固定为 420px（对标 Grok Bot 原生两张截图无论最大化还是缩小均固定 420px）
            int targetSidebarW = 420;
            if (this.Width < 850)
            {
                targetSidebarW = Math.Max(260, Math.Min(420, (int)(this.Width * 0.35f)));
            }
            if (_sessionsPanel != null && _sessionsPanel.Width != targetSidebarW)
            {
                _sessionsPanel.Width = targetSidebarW;
            }

                        // 2. 顶部搜索栏与右侧搜索按钮占据整行（比例适合，自适应排版）
            if (_searchHeaderPanel != null && _btnSearchToggle != null)
            {
                int btnSize = 46;
                int rightMargin = 16;
                int leftMargin = 16;
                int gap = 10;
                int topY = (_searchHeaderPanel.Height - btnSize) / 2;

                _btnSearchToggle.Size = new Size(btnSize, btnSize);
                _btnSearchToggle.Location = new Point(_searchHeaderPanel.Width - rightMargin - btnSize, topY);

                if (_searchBoxContainer != null)
                {
                    int boxH = 42;
                    int boxY = (_searchHeaderPanel.Height - boxH) / 2;
                    int boxW = Math.Max(120, _searchHeaderPanel.Width - leftMargin - rightMargin - btnSize - gap);
                    _searchBoxContainer.Location = new Point(leftMargin, boxY);
                    _searchBoxContainer.Size = new Size(boxW, boxH);

                    int clearSize = 24;
                    if (_btnSearchClear != null)
                    {
                        _btnSearchClear.Location = new Point(_searchBoxContainer.Width - clearSize - 10, (_searchBoxContainer.Height - clearSize) / 2);
                    }

                    if (_txtSearch != null)
                    {
                        int rightPad = (_btnSearchClear != null && _btnSearchClear.Visible) ? (clearSize + 16) : 28;
                        _txtSearch.Width = Math.Max(40, _searchBoxContainer.Width - 14 - rightPad);
                        _txtSearch.Location = new Point(14, (_searchBoxContainer.Height - _txtSearch.Height) / 2);
                    }
                }
            }

            // 3. 底部控制按钮定位 (54px 用户头像圆钮，移除蓝色向上箭头)
            if (_leftBottomBar != null && _btnBottomUser != null)
            {
                _btnBottomUser.Location = new Point(24, 6);
                _btnBottomUser.Size = new Size(54, 54);
            }

            // 4. 右侧 GrokBot 浮动输入胶囊（高 64 / 左右 28 / 底 22，对齐 Docs §4.2）
            if (_bottomSendPanel != null)
            {
                _bottomSendPanel.Padding = new Padding(28, 4, 28, 22);
                _bottomSendPanel.Height = 90; // 4 + 64 + 22
                if (_inputPanel != null) _inputPanel.Invalidate();
                _bottomSendPanel.Invalidate();
            }

            if (_heroProfilePanel != null)
            {
                _heroProfilePanel.Invalidate();
            }
            if (_chatHeader != null)
            {
                _chatHeader.Invalidate();
            }
            if (_leftBottomBar != null)
            {
                _leftBottomBar.Invalidate();
            }
        }

        private void TriggerScreenshot()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
            }
            catch
            {
                try
                {
                    Process.Start("snippingtool.exe");
                }
                catch (Exception ex)
                {
                    GrokMessageBox.Show(this, "无法启动屏幕截图工具: " + ex.Message, "截图提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ShowInputPlusContextMenu()
        {
            GrokPopupMenu menu = new GrokPopupMenu();
            menu.AddItem("选择并发送文件...", WeChatIconType.Folder, () => BtnSendFile_Click(this, EventArgs.Empty))
                .AddItem("插入 Emoji 表情...", WeChatIconType.Emoji, () => ShowEmojiPopup())
                .AddItem("打开接收文件夹", WeChatIconType.Folder, () => OpenReceivedFolder())
                .AddSeparator()
                .AddItem("清空当前对话记录", WeChatIconType.Clear, () => ClearCurrentChat(), null, true);

            menu.Show(_btnToolSendFile, new Point(0, -210));
        }

        #region SafeInvoke & Helpers

        public void ApplyFontSettings()
        {
            try
            {
                string uiFam = !string.IsNullOrEmpty(AppSettings.UiFontFamily) ? AppSettings.UiFontFamily : "Microsoft YaHei UI";
                float uiSz = AppSettings.UiFontSize >= 8f ? AppSettings.UiFontSize : 10.0f;
                this.Font = new Font(uiFam, uiSz, FontStyle.Regular);

                if (_txtInput != null)
                {
                    _txtInput.Font = new Font(uiFam, Math.Max(10f, uiSz + 1f), FontStyle.Regular);
                }
                if (_txtSearch != null)
                {
                    _txtSearch.Font = new Font(uiFam, uiSz, FontStyle.Regular);
                }

                if (_chatView != null)
                {
                    _chatView.ApplyFontSettings();
                }

                if (_listSessions != null)
                {
                    _listSessions.Invalidate();
                }

                this.Invalidate(true);
            }
            catch { }
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

    #region Form Native Edge Resize Message Filter

    /// <summary>
    /// 全局消息过滤器：拦截无边框窗口四周 8px 边缘的鼠标拖拽消息，
    /// 通过 Windows 原生 WM_SYSCOMMAND (SC_SIZE) 实现丝滑四周与四角鼠标拖拽缩放
    /// </summary>
    internal class FormResizeMessageFilter : IMessageFilter
    {
        private readonly MainForm _form;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_SETCURSOR = 0x0020;
        private const int WM_SYSCOMMAND = 0x0112;

        private const int SC_SIZE_LEFT = 0xF001;
        private const int SC_SIZE_RIGHT = 0xF002;
        private const int SC_SIZE_TOP = 0xF003;
        private const int SC_SIZE_TOPLEFT = 0xF004;
        private const int SC_SIZE_TOPRIGHT = 0xF005;
        private const int SC_SIZE_BOTTOM = 0xF006;
        private const int SC_SIZE_BOTTOMLEFT = 0xF007;
        private const int SC_SIZE_BOTTOMRIGHT = 0xF008;

        public FormResizeMessageFilter(MainForm form)
        {
            _form = form;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (_form == null || _form.IsDisposed || !_form.Visible) return false;
            if (_form.WindowState != FormWindowState.Normal) return false;

            if (m.Msg == WM_MOUSEMOVE || m.Msg == WM_LBUTTONDOWN || m.Msg == WM_SETCURSOR)
            {
                Point screenPt = Cursor.Position;
                Rectangle bounds = _form.Bounds;
                if (!bounds.Contains(screenPt)) return false;

                int grip = 8;
                bool left = screenPt.X < bounds.Left + grip;
                bool right = screenPt.X >= bounds.Right - grip;
                bool top = screenPt.Y < bounds.Top + grip;
                bool bottom = screenPt.Y >= bounds.Bottom - grip;

                if (!left && !right && !top && !bottom) return false;

                Cursor cursor = Cursors.Default;
                int dir = 0;

                if (left && top) { cursor = Cursors.SizeNWSE; dir = SC_SIZE_TOPLEFT; }
                else if (right && top) { cursor = Cursors.SizeNESW; dir = SC_SIZE_TOPRIGHT; }
                else if (left && bottom) { cursor = Cursors.SizeNESW; dir = SC_SIZE_BOTTOMLEFT; }
                else if (right && bottom) { cursor = Cursors.SizeNWSE; dir = SC_SIZE_BOTTOMRIGHT; }
                else if (left) { cursor = Cursors.SizeWE; dir = SC_SIZE_LEFT; }
                else if (right) { cursor = Cursors.SizeWE; dir = SC_SIZE_RIGHT; }
                else if (top) { cursor = Cursors.SizeNS; dir = SC_SIZE_TOP; }
                else if (bottom) { cursor = Cursors.SizeNS; dir = SC_SIZE_BOTTOM; }

                if (dir != 0)
                {
                    Cursor.Current = cursor;
                    if (m.Msg == WM_LBUTTONDOWN)
                    {
                        MainForm.ReleaseCaptureWin32();
                        MainForm.SendMessageWin32(_form.Handle, WM_SYSCOMMAND, (IntPtr)dir, IntPtr.Zero);
                        return true;
                    }
                    if (m.Msg == WM_SETCURSOR)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    #endregion
}
