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
    /// <summary>
    /// LanChat 桌面客户端主窗体生命周期与核心路由类
    /// <para>【职责】作为主窗体容器，负责窗口生命周期、无边框 DWM 阴影与缩放、Win32 消息路由及 Presenter/Controller 业务组装。</para>
    /// <para>【分部类结构】
    /// 1. MainForm.cs：窗体生命周期、控制器总装、原生 Win32 调用与公共属性路由；
    /// 2. MainForm.Designer.cs：全量控件树构建、响应式布局排版与自绘参数度量；
    /// 3. MainForm.Chat.cs：会话列表自绘调度、文本输入与快捷菜单交互；
    /// 4. MainForm.Files.cs：文件传输拖拽与多文件流式发送调度；
    /// 5. MainForm.Tray.cs：系统托盘悬停预览、未读闪烁与设置中心呼出。
    /// </para>
    /// </summary>
    public partial class MainForm : Form
    {
        // ==========================================
        // Windows 原生 DWM 与 Win32 消息互操作
        // ==========================================

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

        // ==========================================
        // 全局核心常量
        // ==========================================
        /// <summary>当前应用程序版本号</summary>
        public const string APP_VERSION = "2.0.14";
        /// <summary>GitHub 开源仓库标识</summary>
        public const string GITHUB_REPO = "yxiaojiang/LanChat";
        private const string BROADCAST_TARGET = "📢 局域网全员群聊";

        // ==========================================
        // 核心服务与业务控制器依赖
        // ==========================================
        private NetworkService _netService;
        private readonly SessionManager _sessionManager = new SessionManager();
        private SessionPresenter _sessionPresenter;
        private ChatPresenter _chatPresenter;
        private FileTransferController _fileController;

        // 向后兼容包装属性：透明路由至 Presenter 层，保证旧代码调用零破坏
        private string _activeChatKey
        {
            get { return _sessionPresenter != null ? _sessionPresenter.ActiveChatKey : _sessionManager.ActiveSessionKey; }
            set
            {
                _sessionManager.ActiveSessionKey = value;
                if (_sessionPresenter != null) _sessionPresenter.ActiveChatKey = value;
            }
        }

        private PeerInfo _selectedPeer
        {
            get { return _sessionPresenter != null ? _sessionPresenter.SelectedPeer : null; }
            set { if (_sessionPresenter != null) _sessionPresenter.SelectedPeer = value; }
        }

        private int _broadcastUnreadCount
        {
            get { return _sessionPresenter != null ? _sessionPresenter.BroadcastUnreadCount : 0; }
            set { if (_sessionPresenter != null) _sessionPresenter.BroadcastUnreadCount = value; }
        }

        private string _lastBroadcastSnippet
        {
            get { return _chatPresenter != null ? _chatPresenter.LastBroadcastSnippet : ""; }
            set { if (_chatPresenter != null) _chatPresenter.LastBroadcastSnippet = value; }
        }

        private string _lastBroadcastTimeText
        {
            get { return _chatPresenter != null ? _chatPresenter.LastBroadcastTimeText : ""; }
            set { if (_chatPresenter != null) _chatPresenter.LastBroadcastTimeText = value; }
        }

        // 导航状态：0 = 聊天, 1 = 通讯录
        private int _currentNavTab = 0;

        // 系统托盘与生命周期管理
        private NotifyIcon _trayIcon;
        private GrokTrayPreviewPopup _trayPreviewPopup;
        private bool _isExplicitExit = false;
        private bool _hasShownTrayBalloon = false;
        private int _wakeupMessageId = 0;
        private FormResizeMessageFilter _resizeFilter;

        /// <summary>
        /// 初始化主窗体，加载本地设置、初始化无边框缩放过滤器与系统托盘
        /// </summary>
        public MainForm()
        {
            try
            {
                using (Icon ico = WeChatIcon.CreateAppIcon(32))
                {
                    this.Icon = (Icon)ico.Clone();
                }
            }
            catch
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

        /// <summary>
        /// 初始化业务控制器 (MVP架构)，解耦网络层与主窗体 UI
        /// </summary>
        private void InitializeControllers()
        {
            _sessionPresenter = new SessionPresenter(_netService, _sessionManager, this);
            _chatPresenter = new ChatPresenter(_netService, _sessionManager, _sessionPresenter, this);
            _fileController = new FileTransferController(_netService, _sessionManager, this);

            // 1. SessionPresenter 会话状态绑定
            _sessionPresenter.SessionsUpdated += () => RenderSessionList();
            _sessionPresenter.PeerJoinedNotice += (peer) => {
                if (peer != null)
                {
                    AppendChatSystem(string.Format("好友 [{0}] ({1}) 加入了局域网", peer.NickName, peer.IP));
                }
            };
            _sessionPresenter.ActiveSessionChanged += OnActiveSessionChanged;

            // 2. ChatPresenter 聊天收发绑定
            _chatPresenter.IsActivelyViewingPredicate = (msg) => {
                string convKey = msg.IsBroadcast ? SessionManager.BROADCAST_KEY : msg.SenderId;
                return IsActivelyViewing(convKey);
            };
            _chatPresenter.MessageAppended += OnMessageAppended;
            _chatPresenter.MessageNotificationTriggered += (msg, wasActivelyViewing) => OnMessageNotification(msg);
            _chatPresenter.ActiveChatCleared += () => {
                if (_chatView != null) _chatView.ClearMessages();
                if (_listSessions != null) _listSessions.Invalidate();
            };

            // 3. FileTransferController 文件传输绑定
            _fileController.IsActivelyViewingPredicate = (convKey, senderNick) => IsActivelyViewing(convKey);
            _fileController.ProgressChanged += HandleFileProgress;
            _fileController.TransferCompleted += OnFileTransferCompleted;
            _fileController.FileReceived += OnFileReceived;
            _fileController.FileSent += (fileMsg, convKey) => {
                if (_activeChatKey == convKey && _chatView != null)
                {
                    _chatView.AppendMessage(fileMsg);
                }
                if (_listSessions != null) _listSessions.Invalidate();
            };
        }

        /// <summary>扩展窗体创建样式：支持无边框窗口点击任务栏最小化/还原及系统动画</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x00020000 | 0x00080000 | 0x02000000;
                return cp;
            }
        }

        /// <summary>窗体句柄建立：注入 DWM 暗黑主题属性、单实例唤醒消息与任务栏图标</summary>
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

            // 显式向 HWND 句柄发送 WM_SETICON 并写入窗口类图标，防止任务栏显示系统旧缓存图标
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

        /// <summary>窗体初次呈现：重新加固任务栏图标</summary>
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

        /// <summary>主窗体消息循环分发：处理多开唤醒与边缘命中检测</summary>
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

        /// <summary>标题栏鼠标按下：双击切换最大化，单指拖拽移动窗口</summary>
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

        /// <summary>切换窗口最大化与正常窗口大小</summary>
        private void ToggleMaximize()
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
                if (_btnWinMax != null)
                {
                    _btnWinMax.IconType = WeChatIconType.WinRestore;
                    _btnWinMax.TooltipText = "最大化";
                }
            }
            else
            {
                this.MaximizedBounds = Screen.FromHandle(this.Handle).WorkingArea;
                this.WindowState = FormWindowState.Maximized;
                if (_btnWinMax != null)
                {
                    _btnWinMax.IconType = WeChatIconType.WinMax;
                    _btnWinMax.TooltipText = "向下还原";
                }
            }
            Invalidate(true);
        }

        /// <summary>触发 Presenter 重新加载好友列表数据源</summary>
        private void RefreshPeerListUI()
        {
            if (_sessionPresenter != null)
            {
                _sessionPresenter.RefreshSessions();
            }
            else
            {
                RenderSessionList();
            }
        }

        /// <summary>应用搜索框过滤词并刷新会话列表</summary>
        private void ApplyFilterAndRenderList()
        {
            if (_sessionPresenter != null)
            {
                _sessionPresenter.SearchFilter = _txtSearch != null ? _txtSearch.Text : "";
            }
            RenderSessionList();
        }

        /// <summary>在当前视口追加一条系统提示消息</summary>
        /// <param name="text">提示正文</param>
        public void AppendChatSystem(string text)
        {
            if (_chatPresenter != null)
            {
                _chatPresenter.AppendChatSystem(text);
            }
        }

        /// <summary>清空当前活跃会话的聊天历史记录</summary>
        public void ClearCurrentChat()
        {
            if (_chatPresenter != null)
            {
                _chatPresenter.ClearCurrentChat();
            }
        }

        /// <summary>启动 Windows 系统截图工具</summary>
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

        /// <summary>弹出输入框左侧 (+) 附件与快捷功能暗黑菜单</summary>
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

        /// <summary>应用并动态刷新用户偏好的字体家族与字号</summary>
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

        /// <summary>主窗体跨线程安全调度辅助</summary>
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

        #region Helper Drawing Functions

        /// <summary>GDI+ 自绘工具：绘制圆角矩形描边</summary>
        public static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle bounds, int radius)
        {
            using (GraphicsPath path = RoundedRect(bounds, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        /// <summary>GDI+ 自绘工具：填充圆角矩形内部实体</summary>
        public static void DrawRoundedRectangle(Graphics g, Brush brush, Rectangle bounds, int radius)
        {
            using (GraphicsPath path = RoundedRect(bounds, radius))
            {
                g.FillPath(brush, path);
            }
        }

        /// <summary>GDI+ 自绘工具：构建标准圆弧拼接的抗锯齿 GraphicsPath 路径</summary>
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
