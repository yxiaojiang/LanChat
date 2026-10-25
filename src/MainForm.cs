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
        private static extern bool ReleaseCapture();

        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        public static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessagePtr(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const string BROADCAST_TARGET = "📢 局域网全员群聊";
        private const string NEW_CONNECTION_ITEM = "__NEW_CONNECTION__";
        private NetworkService _netService;

        // Session and chat history coordination service
        private readonly SessionManager _sessionManager = new SessionManager();
        private string _activeChatKey
        {
            get { return _sessionManager.ActiveSessionKey; }
            set { _sessionManager.ActiveSessionKey = value; }
        }

        private PeerInfo _selectedPeer = null;

        // UI Layout Panels
        private Panel _sidebarPanel;
        private Panel _sessionsPanel;
        private Panel _chatPanel;

        // Left Column Hero & Bottom Controls
        private Control _btnAvatar;
        private Panel _heroProfilePanel;
        private Panel _leftBottomBar;
        private Button _btnBottomUser;
        private Button _btnBottomConnect;
        private WeChatIconButton _btnBottomScan;
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
        private WeChatIconButton _btnSearchToggle;
        private WeChatIconButton _btnAddSession;
        private ListBox _listSessions;
        private List<object> _allSessionItems = new List<object>();

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

        // Input Controls (WeChat Tall Input & 6-Button Toolbar)
        private Panel _toolPanel;
        private WeChatIconButton _btnToolEmoji;
        private WeChatIconButton _btnToolBox;
        private WeChatIconButton _btnToolSendFile;
        private WeChatIconButton _btnToolScissors;
        private WeChatIconButton _btnToolMic;
        private WeChatIconButton _btnToolCall;
        private WeChatIconButton _btnToolFolder;
        private WeChatIconButton _btnToolClear;
        private Label _lblAutoAccept;

        private Panel _inputPanel;
        private TextBox _txtInput;
        private Panel _bottomSendPanel;
        private Label _lblSendTip;
        private Button _btnSend;
        private WeChatIconButton _btnSendWave;
        private string _inputPlaceholder = "给 局域网全员群聊 发消息";

        // Navigation state: 0 = Chat, 1 = Contacts
        private int _currentNavTab = 0;

        // Constants
        public const string APP_VERSION = "2.0.0";
        public const string GITHUB_REPO = "yxiaojiang/LanChat";

        // System Tray & Single Instance
        private NotifyIcon _trayIcon;
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

            InitializeComponent();
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
            this.Size = new Size(1024, 620);
            this.MinimumSize = new Size(820, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular);
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

            // 1.1 Top Search & Add Action Row (Height: 58px)
            _searchHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 58,
                BackColor = Color.FromArgb(14, 14, 14)
            };
            _searchHeaderPanel.MouseDown += DragWindow_MouseDown;

            _searchBoxContainer = new Panel
            {
                Location = new Point(20, 11),
                Size = new Size(290, 36),
                BackColor = Color.FromArgb(28, 28, 30),
                Visible = false
            };
            _searchBoxContainer.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(Color.FromArgb(52, 52, 56), 1f))
                {
                    DrawRoundedRectangle(e.Graphics, pen, new Rectangle(0, 0, _searchBoxContainer.Width - 1, _searchBoxContainer.Height - 1), 18);
                }
            };

            _txtSearch = new TextBox
            {
                Location = new Point(12, 8),
                Width = 260,
                BackColor = Color.FromArgb(28, 28, 30),
                ForeColor = Color.FromArgb(230, 230, 235),
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei", 9.5f)
            };
            _txtSearch.TextChanged += TxtSearch_TextChanged;
            _txtSearch.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Escape)
                {
                    _txtSearch.Clear();
                    _searchBoxContainer.Visible = false;
                }
            };
            _searchBoxContainer.Controls.Add(_txtSearch);

            // Top circular Search button at right (36x36)
            _btnSearchToggle = new WeChatIconButton(WeChatIconType.Search, "搜索会话", 36)
            {
                Location = new Point(322, 11),
                IsCircle = true,
                FillColor = Color.FromArgb(28, 28, 30),
                HoverFillColor = Color.FromArgb(46, 46, 50),
                IconColor = Color.FromArgb(205, 205, 210)
            };
            _btnSearchToggle.Click += (s, e) => {
                _searchBoxContainer.Visible = !_searchBoxContainer.Visible;
                if (_searchBoxContainer.Visible) _txtSearch.Focus();
                else _txtSearch.Clear();
            };

            // Top circular Plus button at right (36x36)
            _btnAddSession = new WeChatIconButton(WeChatIconType.Add, "新建连接 / 更多操作", 36)
            {
                Location = new Point(368, 11),
                IsCircle = true,
                FillColor = Color.FromArgb(28, 28, 30),
                HoverFillColor = Color.FromArgb(46, 46, 50),
                IconColor = Color.FromArgb(220, 220, 225)
            };
            _btnAddSession.Click += BtnAddSession_Click;

            _searchHeaderPanel.Controls.Add(_searchBoxContainer);
            _searchHeaderPanel.Controls.Add(_btnSearchToggle);
            _searchHeaderPanel.Controls.Add(_btnAddSession);

            // 1.2 Center Hero Profile Section (82x82 Red Mascot Avatar + Blue Dot + Centered Nickname)
            _heroProfilePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 175,
                BackColor = Color.FromArgb(14, 14, 14),
                Cursor = Cursors.Hand
            };
            _heroProfilePanel.Paint += BtnAvatar_Paint;
            _heroProfilePanel.Click += BtnAvatar_Click;
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
                Rectangle r = new Rectangle(0, 0, _btnBottomUser.Width - 1, _btnBottomUser.Height - 1);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(48, 48, 48))) // #303030
                using (Pen p = new Pen(Color.FromArgb(64, 64, 68), 1f))
                {
                    e.Graphics.FillEllipse(b, r);
                    e.Graphics.DrawEllipse(p, r);
                }
                string nick = _netService != null && !string.IsNullOrEmpty(_netService.NickName) ? _netService.NickName : Environment.UserName;
                if (nick.Length > 2) nick = nick.Substring(0, 2);
                using (Font f = new Font("Microsoft YaHei", 10f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(215, 215, 220)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(nick, f, tb, new RectangleF(0, 1, _btnBottomUser.Width, _btnBottomUser.Height), sf);
                }
            };
            _btnBottomUser.Click += BtnSettings_Click;

            // Middle pill button "连接节点" (240x54, radius 27, 对标 Alis.png 底部中间胶囊按钮)
            _btnBottomConnect = new Button
            {
                Location = new Point(90, 6),
                Size = new Size(240, 54),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _btnBottomConnect.FlatAppearance.BorderSize = 0;
            _btnBottomConnect.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnBottomConnect.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool connHover = false;
            _btnBottomConnect.MouseEnter += (s, e) => { connHover = true; _btnBottomConnect.Invalidate(); };
            _btnBottomConnect.MouseLeave += (s, e) => { connHover = false; _btnBottomConnect.Invalidate(); };
            _btnBottomConnect.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, _btnBottomConnect.Width - 1, _btnBottomConnect.Height - 1);
                using (SolidBrush b = new SolidBrush(connHover ? Color.FromArgb(42, 42, 46) : Color.FromArgb(31, 31, 31))) // #1F1F1F
                using (Pen p = new Pen(connHover ? Color.FromArgb(64, 64, 70) : Color.FromArgb(46, 46, 50), 1f))
                {
                    DrawRoundedRectangle(e.Graphics, b, r, 27);
                    DrawRoundedRectangle(e.Graphics, p, r, 27);
                }
                using (Font f = new Font("Microsoft YaHei", 10.5f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(240, 240, 245)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("连接节点", f, tb, new RectangleF(0, 1, _btnBottomConnect.Width, _btnBottomConnect.Height), sf);
                }
            };
            _btnBottomConnect.Click += BtnConnectNode_Click;

            // Right blue circular up-arrow scan button (54x54, #116DD2)
            _btnBottomScan = new WeChatIconButton(WeChatIconType.UpArrow, "扫描并广播刷新局域网节点", 54)
            {
                Location = new Point(341, 6),
                IsCircle = true,
                FillColor = Color.FromArgb(17, 109, 210),
                HoverFillColor = Color.FromArgb(32, 128, 232),
                IconColor = Color.White
            };
            _btnBottomScan.Click += (s, e) => RefreshPeersWithNotice();

            _leftBottomBar.Controls.Add(_btnBottomUser);
            _leftBottomBar.Controls.Add(_btnBottomConnect);
            _leftBottomBar.Controls.Add(_btnBottomScan);

            // 1.4 Sessions ListBox (Height: 86px per item, OwnerDrawn, #0E0E0E)
            _listSessions = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(14, 14, 14),
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei", 10.5f),
                ItemHeight = 86,
                DrawMode = DrawMode.OwnerDrawFixed,
                IntegralHeight = false
            };
            _listSessions.DrawItem += ListSessions_DrawItem;
            _listSessions.SelectedIndexChanged += ListSessions_SelectedIndexChanged;
            _listSessions.MouseUp += ListSessions_MouseUp;

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
                Height = 64,
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

                string pillTitle = _lblChatTitle.Text ?? "局域网全员群聊";
                using (Font pillFont = new Font("Microsoft YaHei", 10.5f, FontStyle.Bold))
                {
                    SizeF tsz = g.MeasureString(pillTitle, pillFont);
                    int pillW = Math.Max(120, (int)Math.Ceiling(tsz.Width) + 54);
                    int pillH = 38;
                    int pillX = (_chatHeader.Width - pillW) / 2;
                    int pillY = (_chatHeader.Height - pillH) / 2;

                    Rectangle pillRect = new Rectangle(pillX, pillY, pillW, pillH);
                    using (SolidBrush pillBg = new SolidBrush(Color.FromArgb(32, 32, 34)))
                    using (Pen pillBorder = new Pen(Color.FromArgb(46, 46, 50), 1f))
                    {
                        DrawRoundedRectangle(g, pillBg, pillRect, 19);
                        DrawRoundedRectangle(g, pillBorder, pillRect, 19);
                    }

                    // Draw mini mascot avatar inside floating pill
                    Rectangle avRect = new Rectangle(pillX + 10, pillY + 7, 24, 24);
                    Color avColor = _selectedPeer == null ? Color.FromArgb(144, 90, 251) : Color.FromArgb(246, 148, 18);
                    WeChatIcon.DrawMascotAvatar(g, avRect, avColor, false, false, Color.Transparent);

                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(242, 242, 246)))
                    {
                        float ty = pillY + (pillH - pillFont.GetHeight(g)) / 2f;
                        g.DrawString(pillTitle, pillFont, tb, pillX + 40, ty);
                    }
                }
            };
            _chatHeader.Resize += (s, e) => _chatHeader.Invalidate();

            // Window control buttons at top-right (— □ ×) (Grok Bot 56x64 矩形无缝标题栏按钮，对标 Alis3.png)
            Panel winCtrlPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 168,
                Height = 64,
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
                Font = new Font("Microsoft YaHei", 9f),
                ForeColor = Color.FromArgb(164, 132, 245),
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

            // 2.4 Bottom WeChat-Style Tall Input Area (Height: 156px, #161618, Grok Purple Send Button)
            _bottomSendPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 156,
                BackColor = Color.FromArgb(22, 22, 24)
            };
            _inputPanel = _bottomSendPanel; // 兼容指针
            _bottomSendPanel.Paint += (s, e) => {
                using (Pen p = new Pen(Color.FromArgb(38, 38, 42), 1f))
                {
                    e.Graphics.DrawLine(p, 0, 0, _bottomSendPanel.Width, 0);
                }
            };

            // 2.4.1 Top Input Toolbar (Height: 34px, 6 Action Buttons)
            _toolPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.Transparent
            };

            _btnToolEmoji = new WeChatIconButton(WeChatIconType.Emoji, "选择表情 (Emoji)", 28)
            {
                Location = new Point(16, 3),
                IsCircle = false,
                IconColor = Color.FromArgb(160, 160, 168),
                HoverFillColor = Color.FromArgb(44, 44, 48)
            };
            _btnToolEmoji.Click += (s, e) => ShowEmojiPopup();

            _btnToolBox = new WeChatIconButton(WeChatIconType.Box, "聊天记录与收藏", 28)
            {
                Location = new Point(48, 3),
                IsCircle = false,
                IconColor = Color.FromArgb(160, 160, 168),
                HoverFillColor = Color.FromArgb(44, 44, 48)
            };
            _btnToolBox.Click += (s, e) => OpenReceivedFolder();

            _btnToolSendFile = new WeChatIconButton(WeChatIconType.Folder, "发送文件与图片", 28)
            {
                Location = new Point(80, 3),
                IsCircle = false,
                IconColor = Color.FromArgb(160, 160, 168),
                HoverFillColor = Color.FromArgb(44, 44, 48)
            };
            _btnToolSendFile.Click += (s, e) => BtnSendFile_Click(this, EventArgs.Empty);
            _btnToolFolder = _btnToolSendFile;

            _btnToolScissors = new WeChatIconButton(WeChatIconType.Scissors, "屏幕截图 (Alt+A)", 28)
            {
                Location = new Point(112, 3),
                IsCircle = false,
                IconColor = Color.FromArgb(160, 160, 168),
                HoverFillColor = Color.FromArgb(44, 44, 48)
            };
            _btnToolScissors.Click += (s, e) => TriggerScreenshot();

            _btnToolMic = new WeChatIconButton(WeChatIconType.Mic, "语音消息 (右键切换自动接收)", 28)
            {
                Location = new Point(144, 3),
                IsCircle = false,
                IconColor = Color.FromArgb(160, 160, 168),
                HoverFillColor = Color.FromArgb(44, 44, 48)
            };
            _btnToolMic.Click += (s, e) => {
                GrokMessageBox.Show(this, "语音录制组件已就绪，长按可录制局域网语音短消息。", "语音功能", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            _btnToolMic.MouseUp += (s, e) => {
                if (e.Button == MouseButtons.Right && _netService != null)
                {
                    _netService.AutoAcceptFiles = !_netService.AutoAcceptFiles;
                    AppendChatSystem(_netService.AutoAcceptFiles ? "⚡ 已开启自动接收文件" : "⏸ 已暂停自动接收文件");
                }
            };

            _btnToolCall = new WeChatIconButton(WeChatIconType.Call, "语音与视频通话", 28)
            {
                Location = new Point(176, 3),
                IsCircle = false,
                IconColor = Color.FromArgb(160, 160, 168),
                HoverFillColor = Color.FromArgb(44, 44, 48)
            };
            _btnToolCall.Click += (s, e) => {
                GrokMessageBox.Show(this, "正在向节点发起音视频呼叫...", "语音通话", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            _btnToolClear = new WeChatIconButton(WeChatIconType.Clear, "", 28) { Visible = false };
            _btnSendWave = new WeChatIconButton(WeChatIconType.UpArrow, "", 28) { Visible = false };

            _toolPanel.Controls.Add(_btnToolEmoji);
            _toolPanel.Controls.Add(_btnToolBox);
            _toolPanel.Controls.Add(_btnToolSendFile);
            _toolPanel.Controls.Add(_btnToolScissors);
            _toolPanel.Controls.Add(_btnToolMic);
            _toolPanel.Controls.Add(_btnToolCall);

            // 2.4.2 Multiline Text Input Box
            _txtInput = new TextBox
            {
                Multiline = true,
                Location = new Point(18, 38),
                Width = 500,
                Height = 78,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(22, 22, 24),
                ForeColor = Color.FromArgb(242, 242, 246),
                Font = new Font("Microsoft YaHei", 10.5f, FontStyle.Regular),
                ScrollBars = ScrollBars.Vertical,
                AllowDrop = true,
                Visible = true
            };
            _txtInput.KeyDown += TxtInput_KeyDown;
            _txtInput.TextChanged += TxtInput_TextChanged;
            _txtInput.DragEnter += MainForm_DragEnter;
            _txtInput.DragDrop += MainForm_DragDrop;

            // 2.4.3 Bottom Action Controls (Tip + Grok Purple Send Button)
            _btnSend = new Button
            {
                Size = new Size(82, 30),
                Location = new Point(500, 118),
                FlatStyle = FlatStyle.Flat,
                Text = "发送(&S)",
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            _btnSend.FlatAppearance.BorderSize = 0;
            _btnSend.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnSend.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool sendHover = false;
            bool sendPress = false;
            _btnSend.MouseEnter += (s, e) => { sendHover = true; _btnSend.Invalidate(); };
            _btnSend.MouseLeave += (s, e) => { sendHover = false; sendPress = false; _btnSend.Invalidate(); };
            _btnSend.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left) { sendPress = true; _btnSend.Invalidate(); }
            };
            _btnSend.MouseUp += (s, e) => {
                sendPress = false; _btnSend.Invalidate();
            };
            _btnSend.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color bgCol = sendPress ? Color.FromArgb(124, 82, 202) : (sendHover ? Color.FromArgb(157, 117, 236) : Color.FromArgb(142, 100, 220));
                Rectangle r = new Rectangle(0, 0, _btnSend.Width - 1, _btnSend.Height - 1);
                using (SolidBrush b = new SolidBrush(bgCol))
                {
                    DrawRoundedRectangle(e.Graphics, b, r, 4);
                }
                using (Font f = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("发送(S)", f, tb, new RectangleF(0, 0, _btnSend.Width, _btnSend.Height), sf);
                }
            };
            _btnSend.Click += BtnSend_Click;

            _lblSendTip = new Label
            {
                Text = "按下 Enter 发送，Ctrl+Enter 换行",
                AutoSize = true,
                Font = new Font("Microsoft YaHei", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(104, 104, 114),
                Location = new Point(300, 124)
            };

            _bottomSendPanel.Controls.Add(_toolPanel);
            _bottomSendPanel.Controls.Add(_txtInput);
            _bottomSendPanel.Controls.Add(_lblSendTip);
            _bottomSendPanel.Controls.Add(_btnSend);

            // Assemble Panels
            _chatPanel.Controls.Add(_chatView);
            _chatPanel.Controls.Add(_progressPanel);
            _chatPanel.Controls.Add(_chatHeader);
            _chatPanel.Controls.Add(_bottomSendPanel);

            this.Controls.Add(_chatPanel);
            this.Controls.Add(_sessionsPanel);

            this.Resize += (s, e) => UpdateResponsiveLayout();
            _chatPanel.Resize += (s, e) => UpdateResponsiveLayout();
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

            // 2. 顶部搜索栏与右侧圆钮动态定位 (36x36)
            if (_searchHeaderPanel != null && _btnSearchToggle != null && _btnAddSession != null)
            {
                _btnAddSession.Location = new Point(_searchHeaderPanel.Width - 52, 11);
                _btnSearchToggle.Location = new Point(_searchHeaderPanel.Width - 98, 11);

                if (_searchBoxContainer != null)
                {
                    _searchBoxContainer.Width = Math.Max(60, _searchHeaderPanel.Width - 130);
                    _searchBoxContainer.Location = new Point(20, 11);
                    if (_txtSearch != null)
                    {
                        _txtSearch.Width = Math.Max(40, _searchBoxContainer.Width - 24);
                        _txtSearch.Location = new Point(12, (_searchBoxContainer.Height - _txtSearch.Height) / 2);
                    }
                }
            }

            // 3. 底部三联栏控制按钮定位 (54px 高圆钮与胶囊)
            if (_leftBottomBar != null && _btnBottomUser != null && _btnBottomConnect != null && _btnBottomScan != null)
            {
                _btnBottomUser.Location = new Point(24, 6);
                _btnBottomUser.Size = new Size(54, 54);
                _btnBottomScan.Location = new Point(341, 6);
                _btnBottomScan.Size = new Size(54, 54);
                _btnBottomConnect.Location = new Point(90, 6);
                _btnBottomConnect.Size = new Size(240, 54);
            }

            // 4. 右侧微信高输入面板 (156px) 控件自适应定位
            if (_bottomSendPanel != null)
            {
                _bottomSendPanel.Height = 156;
                if (_toolPanel != null)
                {
                    _toolPanel.Width = _bottomSendPanel.Width;
                }
                if (_txtInput != null)
                {
                    _txtInput.Location = new Point(18, 38);
                    _txtInput.Width = Math.Max(100, _bottomSendPanel.Width - 36);
                    _txtInput.Height = Math.Max(40, _bottomSendPanel.Height - 38 - 42);
                }
                if (_btnSend != null)
                {
                    _btnSend.Location = new Point(_bottomSendPanel.Width - 82 - 20, _bottomSendPanel.Height - 30 - 8);
                    if (_lblSendTip != null)
                    {
                        _lblSendTip.Location = new Point(_btnSend.Left - _lblSendTip.Width - 12, _btnSend.Top + (_btnSend.Height - _lblSendTip.Height) / 2);
                    }
                }
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
