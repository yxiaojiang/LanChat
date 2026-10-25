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

        // Input Pill Controls
        private Panel _toolPanel;
        private WeChatIconButton _btnToolEmoji;
        private WeChatIconButton _btnToolSendFile;
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
            this.Size = new Size(1024, 560);
            this.MinimumSize = new Size(820, 480);
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
            _lblSendTip = new Label { Visible = false };
            _btnSend = new Button { Visible = false };
            _toolPanel = new Panel { Visible = false };
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

            // 2.4 Bottom Full-Width Pill Input Bar (自适应横跨全宽，左空 28px 右空 28px 底空 24px，高 68px 大胶囊)
            _bottomSendPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 96,
                BackColor = Color.FromArgb(18, 18, 18),
                Padding = new Padding(28, 4, 28, 24)
            };

            _inputPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 18)
            };
            _inputPanel.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle pillRect = new Rectangle(0, 0, _inputPanel.Width - 1, _inputPanel.Height - 1);
                int radius = Math.Max(8, _inputPanel.Height / 2);
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(32, 32, 34))) // #202022
                using (Pen border = new Pen(Color.FromArgb(44, 44, 48), 1f))
                {
                    DrawRoundedRectangle(g, bg, pillRect, radius);
                    DrawRoundedRectangle(g, border, pillRect, radius);
                }

                // Draw placeholder when input is empty
                if (string.IsNullOrEmpty(_txtInput.Text))
                {
                    using (Font pf = new Font("Microsoft YaHei", 10.5f))
                    using (SolidBrush pb = new SolidBrush(Color.FromArgb(120, 120, 130)))
                    {
                        float py = (_inputPanel.Height - pf.GetHeight(g)) / 2f;
                        g.DrawString(_inputPlaceholder, pf, pb, 62, py);
                    }
                }
            };
            _inputPanel.Cursor = Cursors.IBeam;
            _inputPanel.MouseDown += (s, e) => {
                _txtInput.Visible = true;
                _txtInput.Focus();
                _inputPanel.Invalidate();
            };

            // Left circular [+] button inside input pill (Sends file / Quick menu) (32x32)
            _btnToolSendFile = new WeChatIconButton(WeChatIconType.Add, "发送文件 (右键打开表情/清屏菜单)", 32)
            {
                Location = new Point(20, 18),
                IsCircle = true,
                FillColor = Color.FromArgb(46, 46, 50),
                HoverFillColor = Color.FromArgb(64, 64, 70),
                IconColor = Color.FromArgb(205, 205, 212)
            };
            _btnToolSendFile.Click += BtnSendFile_Click;
            _btnToolSendFile.MouseUp += (s, e) => {
                if (e.Button == MouseButtons.Right)
                {
                    ShowInputPlusContextMenu();
                }
            };
            _btnToolEmoji = _btnToolSendFile;

            // Single/clean borderless input box inside pill
            _txtInput = new TextBox
            {
                Multiline = false,
                Font = new Font("Microsoft YaHei", 10.5f),
                BackColor = Color.FromArgb(32, 32, 34),
                ForeColor = Color.FromArgb(238, 238, 242),
                BorderStyle = BorderStyle.None,
                Location = new Point(62, 22),
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
            _txtInput.DragEnter += MainForm_DragEnter;
            _txtInput.DragDrop += MainForm_DragDrop;

            // Right mic / folder icon inside pill (32x32)
            _btnToolFolder = new WeChatIconButton(WeChatIconType.Mic, "打开接收文件夹 (右键切换自动接收)", 32)
            {
                IsCircle = true,
                FillColor = Color.Transparent,
                HoverFillColor = Color.FromArgb(46, 46, 52),
                IconColor = Color.FromArgb(155, 155, 164)
            };
            _btnToolFolder.Click += (s, e) => OpenReceivedFolder();
            _btnToolFolder.MouseUp += (s, e) => {
                if (e.Button == MouseButtons.Right && _netService != null)
                {
                    _netService.AutoAcceptFiles = !_netService.AutoAcceptFiles;
                    AppendChatSystem(_netService.AutoAcceptFiles ? "⚡ 已开启自动接收文件" : "⏸ 已暂停自动接收文件");
                }
            };

            // Far-right signature Purple SoundWave / Send circle button (42x42, #8E64DC)
            _btnSendWave = new WeChatIconButton(WeChatIconType.SoundWave, "发送消息 (Enter)", 42)
            {
                IsCircle = true,
                FillColor = Color.FromArgb(142, 100, 220),
                HoverFillColor = Color.FromArgb(160, 118, 238),
                IconColor = Color.White
            };
            _btnSendWave.Click += BtnSend_Click;

            _inputPanel.Resize += (s, e) => {
                int w = _inputPanel.Width;
                int h = _inputPanel.Height;
                int waveSize = 42;
                _btnSendWave.Size = new Size(waveSize, waveSize);
                _btnSendWave.Location = new Point(w - waveSize - 14, (h - waveSize) / 2);

                int micSize = 32;
                _btnToolFolder.Size = new Size(micSize, micSize);
                _btnToolFolder.Location = new Point(w - waveSize - 14 - micSize - 14, (h - micSize) / 2);

                int plusSize = 32;
                _btnToolSendFile.Size = new Size(plusSize, plusSize);
                _btnToolSendFile.Location = new Point(20, (h - plusSize) / 2);

                _txtInput.Location = new Point(62, (h - _txtInput.Height) / 2);
                _txtInput.Width = Math.Max(60, _btnToolFolder.Left - 72);
                _inputPanel.Invalidate();
            };

            _inputPanel.Controls.Add(_btnToolSendFile);
            _inputPanel.Controls.Add(_txtInput);
            _inputPanel.Controls.Add(_btnToolFolder);
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

            // 4. 右侧输入胶囊横向满铺（左 28px 右 28px 底 24px，完美对标 Grok Bot Alis.png 与 Alis2.png）
            if (_bottomSendPanel != null)
            {
                _bottomSendPanel.Padding = new Padding(28, 4, 28, 24);
                _bottomSendPanel.Height = 96;
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

        private void ShowInputPlusContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new WeChatDarkMenuRenderer();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(32, 32, 34);
            menu.ForeColor = Color.FromArgb(235, 235, 235);

            menu.Items.Add("📁 选择并发送文件...", null, (s, e) => BtnSendFile_Click(s, e));
            menu.Items.Add("😊 插入 Emoji 表情...", null, (s, e) => ShowEmojiPopup());
            menu.Items.Add("📂 打开接收文件夹", null, (s, e) => OpenReceivedFolder());
            menu.Items.Add("🧹 清空当前对话记录", null, (s, e) => ClearCurrentChat());

            menu.Show(_btnToolSendFile, new Point(0, -menu.PreferredSize.Height - 4));
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
