using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// WorkBuddy 1:1 像素级对齐现代化暗黑全局设置窗口
    /// 特性：
    /// 1. 彻底废除 Windows 原生白色滚动条，采用自绘极简暗黑浮动滑动条 (Zero White Scrollbar)
    /// 2. 左侧分类检索侧边栏（搜索框、设置/功能/系统/关于分组、8px 圆角药丸选项卡）
    /// 3. 右侧轻量优雅小标题（11pt 常规/权限/外观分类）、12px 圆角哑光深灰卡片流
    /// 4. 完美对标 WorkBuddy 的 iOS 风格薄荷绿滑动开关 (Toggle Switch)、字体刻度滑块 (Stepped Slider) 与紧凑药丸下拉框
    /// </summary>
    /// <summary>
    /// 全局双缓冲无闪烁容器面板 (支持子树硬件加速双缓冲与 WS_CLIPCHILDREN)
    /// </summary>
    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            this.DoubleBuffered = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x02000000; // WS_CLIPCHILDREN
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0014) // WM_ERASEBKGND: 彻底压制背景擦除
            {
                m.Result = (System.IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }
    }

    public class GrokSettingsDialog : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessageCue(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

        // 外部委托
        private readonly NetworkService _netService;
        private readonly Action _onCheckUpdate;
        private readonly Action _onShowChangeNick;
        private readonly Action _onOpenFolder;
        private readonly Action _onFontChanged;

        // 布局尺寸 (扩大 1.5 倍，大气宽阔对齐 WorkBuddy)
        private const int DIALOG_W = 1260;
        private const int DIALOG_H = 820;
        private const int SIDEBAR_W = 270;
        private const int CARD_W = 900;
        private Button _btnClose;

        // 视图与自绘无白边滚动容器
        private DoubleBufferedPanel _viewportPanel;
        private DoubleBufferedPanel _scrollContainer;
        // 8 个 Tab 的缓存面板 (避免反复销毁与重建控件造成的瀑布流闪烁)
        private readonly Panel[] _tabPages = new Panel[8];
        private int _scrollOffset = 0;
        private int _maxScroll = 0;
        private bool _isThumbHover = false;
        private bool _isThumbDragging = false;
        private int _dragStartY = 0;
        private int _dragStartOffset = 0;

        // 侧边栏搜索框
        private TextBox _txtSidebarSearch;
        private string _searchFilter = "";

        // 导航分组与项定义
        public class NavItem
        {
            public int Id;
            public string Title;
            public WeChatIconType Icon;
            public string Group;

            public NavItem(int id, string title, WeChatIconType icon, string group)
            {
                Id = id;
                Title = title;
                Icon = icon;
                Group = group;
            }
        }

        private readonly List<NavItem> _allNavItems = new List<NavItem>();
        private int _activeTabId = 0;
        private int _hoverTabId = -1;

        // 颜色设计令牌 (100% 对齐 GrokTheme 设计系统)
        private static readonly Color ColWindowBg = GrokTheme.Level05;       // #121212 主视口
        private static readonly Color ColSidebarBg = GrokTheme.Level0;       // #0E0E0E 侧边栏
        private static readonly Color ColCardBg = GrokTheme.Level1;          // #1C1C1F 卡片底色
        private static readonly Color ColCardBorder = GrokTheme.Border;      // #2C2C32 微细边框
        private static readonly Color ColCardDivider = GrokTheme.BorderInner;// #26262B 行间细线
        private static readonly Color ColTextPrimary = GrokTheme.TextPrimary;// #F2F2F6 主要字
        private static readonly Color ColTextSecondary = GrokTheme.TextSecondary;// #8E8E93 次要字
        private static readonly Color ColSectionHeader = GrokTheme.TextSubtitle;
        private static readonly Color ColGroupLabel = GrokTheme.TextPlaceholder;
        private static readonly Color ColNavActive = GrokTheme.Level3;       // #28282C 激活项
        private static readonly Color ColNavHover = GrokTheme.Level2;        // #202024 悬停项
        private static readonly Color ColMintGreen = GrokTheme.AccentGreen;  // #1FD67E 翠绿开
        private static readonly Color ColSwitchOff = GrokTheme.TextDisabled; // #4A4A50 关闭灰
        private static readonly Color ColPillBg = GrokTheme.Level2;          // #202024 胶囊底
        private static readonly Color ColPillBorder = GrokTheme.Border;      // #2C2C32 胶囊框

        public GrokSettingsDialog(
            NetworkService netService,
            Action onCheckUpdate,
            Action onShowChangeNick,
            Action onOpenFolder,
            int initialTab = 0)
            : this(netService, onCheckUpdate, onShowChangeNick, onOpenFolder, initialTab, null)
        {
        }

        public GrokSettingsDialog(
            NetworkService netService,
            Action onCheckUpdate,
            Action onShowChangeNick,
            Action onOpenFolder,
            int initialTab,
            Action onFontChanged)
        {
            _netService = netService;
            _onCheckUpdate = onCheckUpdate;
            _onShowChangeNick = onShowChangeNick;
            _onOpenFolder = onOpenFolder;
            _onFontChanged = onFontChanged;

            if (initialTab < 0 || initialTab > 7) _activeTabId = 0;
            else _activeTabId = initialTab;

            InitializeNavItems();

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            int scrW = Screen.PrimaryScreen.WorkingArea.Width;
            int scrH = Screen.PrimaryScreen.WorkingArea.Height;
            int targetW = Math.Min(DIALOG_W, scrW - 40);
            int targetH = Math.Min(DIALOG_H, scrH - 40);
            this.Size = new Size(targetW, targetH);
            this.BackColor = ColWindowBg;
            this.DoubleBuffered = true;
            this.Font = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular);

            BuildWindowComponents();
        }

        private void InitializeNavItems()
        {
            _allNavItems.Clear();
            // 分组 1: 设置
            _allNavItems.Add(new NavItem(0, "通用", WeChatIconType.Settings, "设置"));
            _allNavItems.Add(new NavItem(1, "个人主页", WeChatIconType.User, "设置"));
            _allNavItems.Add(new NavItem(2, "外观", WeChatIconType.Emoji, "设置"));
            _allNavItems.Add(new NavItem(3, "快捷键", WeChatIconType.Menu, "设置"));

            // 分组 2: 功能
            _allNavItems.Add(new NavItem(4, "文件传输", WeChatIconType.Folder, "功能"));
            _allNavItems.Add(new NavItem(5, "聊天记录", WeChatIconType.Clear, "功能"));

            // 分组 3: 系统与网络
            _allNavItems.Add(new NavItem(6, "设备连接", WeChatIconType.Contacts, "网络与系统"));

            // 分组 4: 关于
            _allNavItems.Add(new NavItem(7, "关于与更新", WeChatIconType.Refresh, "关于"));
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                cp.Style |= 0x02000000;      // WS_CLIPCHILDREN: 杜绝父级擦除背景导致的重绘闪烁
                cp.ExStyle |= 0x02000000;    // WS_EX_COMPOSITED: 强制全窗体及所有子控件由系统后台双缓冲离屏原子合成，彻底消除从上至下的瀑布流刷新
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                IntPtr rgn = CreateRoundRectRgn(0, 0, this.Width + 1, this.Height + 1, 14, 14);
                if (rgn != IntPtr.Zero)
                {
                    this.Region = Region.FromHrgn(rgn);
                }
                if (_txtSidebarSearch != null && _txtSidebarSearch.IsHandleCreated)
                {
                    SendMessageCue(_txtSidebarSearch.Handle, 0x1501, 1, "搜索设置项");
                }
            }
            catch { }
        }

        private void BuildWindowComponents()
        {
            // 1. 右上角独立关闭按钮控件 (对齐 GrokTheme 标准右上角自绘关闭叉)
            _btnClose = new Button
            {
                Size = new Size(GrokTheme.CloseButtonSize, GrokTheme.CloseButtonSize),
                Location = new Point(this.Width - GrokTheme.CloseButtonMargin - GrokTheme.CloseButtonSize, GrokTheme.CloseButtonMargin),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnClose.FlatAppearance.MouseDownBackColor = Color.Transparent;

            bool closeHover = false;
            _btnClose.MouseEnter += (s, e) => { closeHover = true; _btnClose.Invalidate(); };
            _btnClose.MouseLeave += (s, e) => { closeHover = false; _btnClose.Invalidate(); };
            _btnClose.Click += (s, e) => this.Close();

            _btnClose.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, _btnClose.Width, _btnClose.Height);
                if (closeHover)
                {
                    using (SolidBrush hb = new SolidBrush(GrokTheme.Level3Hot))
                    {
                        GrokTheme.DrawRoundedRectangle(g, hb, r, 6);
                    }
                }
                Color iconColor = closeHover ? GrokTheme.TextTitle : GrokTheme.TextSecondary;
                WeChatIcon.DrawDismissIcon(g, r, iconColor);
            };
            this.Controls.Add(_btnClose);
            _btnClose.BringToFront();

            // 2. 左侧顶部搜索框 (对标 WorkBuddy: [ Q 搜索设置项 ])
            int searchCapsuleW = SIDEBAR_W - 32;
            _txtSidebarSearch = new TextBox
            {
                Location = new Point(50, 25),
                Size = new Size(searchCapsuleW - 46, 20),
                BackColor = Color.FromArgb(30, 30, 34),
                ForeColor = ColTextPrimary,
                BorderStyle = BorderStyle.None,
                Font = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular)
            };
            _txtSidebarSearch.HandleCreated += (s, e) =>
            {
                try { SendMessageCue(_txtSidebarSearch.Handle, 0x1501, 1, "搜索设置项"); } catch { }
            };
            _txtSidebarSearch.GotFocus += (s, e) => Invalidate(new Rectangle(16, 18, searchCapsuleW, 34));
            _txtSidebarSearch.LostFocus += (s, e) => Invalidate(new Rectangle(16, 18, searchCapsuleW, 34));
            _txtSidebarSearch.TextChanged += (s, e) =>
            {
                _searchFilter = _txtSidebarSearch.Text.Trim();
                Invalidate(new Rectangle(0, 0, SIDEBAR_W, this.Height));
            };
            this.Controls.Add(_txtSidebarSearch);

            // 3. 右侧主视口 Panel (纯暗黑底色，禁用原生 Win32 白色滚动条，预留右上角关闭按钮区域不被遮挡)
            int vpW = this.Width - SIDEBAR_W - 74;
            int vpH = this.Height - 44;
            _viewportPanel = new DoubleBufferedPanel
            {
                Location = new Point(SIDEBAR_W + 20, 22),
                Size = new Size(vpW, vpH),
                BackColor = ColWindowBg,
                AutoScroll = false // 彻底杜绝 Windows 灰白滚动条
            };
            _viewportPanel.Paint += ViewportPanel_Paint;
            _viewportPanel.MouseDown += ViewportPanel_MouseDown;
            _viewportPanel.MouseMove += ViewportPanel_MouseMove;
            _viewportPanel.MouseUp += ViewportPanel_MouseUp;
            _viewportPanel.MouseWheel += (s, e) => HandleScrollWheel(e.Delta);

            // 4. 内部滚动容器 (双缓冲无闪烁)
            _scrollContainer = new DoubleBufferedPanel
            {
                Location = new Point(0, 0),
                Size = new Size(CARD_W, 600),
                BackColor = ColWindowBg
            };
            _scrollContainer.MouseWheel += (s, e) => HandleScrollWheel(e.Delta);
            _viewportPanel.Controls.Add(_scrollContainer);
            this.Controls.Add(_viewportPanel);

            this.MouseWheel += (s, e) => HandleScrollWheel(e.Delta);

            SwitchToTab(_activeTabId);
        }

        private void HandleScrollWheel(int delta)
        {
            if (_maxScroll <= 0) return;
            int change = (delta > 0 ? -48 : 48);
            int newOffset = _scrollOffset + change;
            if (newOffset < 0) newOffset = 0;
            if (newOffset > _maxScroll) newOffset = _maxScroll;
            if (newOffset != _scrollOffset)
            {
                _scrollOffset = newOffset;
                _scrollContainer.Location = new Point(0, -_scrollOffset);
                _viewportPanel.Invalidate();
            }
        }

        private void SwitchToTab(int targetTabId)
        {
            if (targetTabId < 0 || targetTabId >= 8) targetTabId = 0;
            _activeTabId = targetTabId;
            _scrollOffset = 0;
            _scrollContainer.Location = new Point(0, 0);

            // 1. 如果目标 Tab 面板尚未创建，首次懒加载构建并挂载到滚动容器中
            if (_tabPages[targetTabId] == null)
            {
                _scrollContainer.SuspendLayout();
                Panel page = CreateTabPage(targetTabId);
                _tabPages[targetTabId] = page;
                _scrollContainer.Controls.Add(page);
                _scrollContainer.ResumeLayout(false);
            }

            // 2. 毫秒级可见性切换 (杜绝任何控件销毁/句柄重建，瞬间呈现)
            for (int i = 0; i < 8; i++)
            {
                if (_tabPages[i] != null)
                {
                    _tabPages[i].Visible = (i == targetTabId);
                }
            }

            // 3. 动态设定滚动高度
            int pageH = _tabPages[targetTabId].Height;
            _scrollContainer.Size = new Size(CARD_W, pageH);
            _maxScroll = Math.Max(0, pageH - _viewportPanel.Height);

            _viewportPanel.Invalidate();
            Invalidate(new Rectangle(0, 0, SIDEBAR_W, this.Height));
        }

        private Panel CreateTabPage(int tabId)
        {
            DoubleBufferedPanel page = new DoubleBufferedPanel
            {
                Location = new Point(0, 0),
                Size = new Size(CARD_W, 600),
                BackColor = ColWindowBg
            };

            switch (tabId)
            {
                case 0:
                    BuildGeneralTab(page);
                    break;
                case 1:
                    BuildProfileTab(page);
                    break;
                case 2:
                    BuildAppearanceTab(page);
                    break;
                case 3:
                    BuildShortcutsTab(page);
                    break;
                case 4:
                    BuildFileTransferTab(page);
                    break;
                case 5:
                    BuildChatHistoryTab(page);
                    break;
                case 6:
                    BuildNetworkDeviceTab(page);
                    break;
                case 7:
                    BuildAboutTab(page);
                    break;
                default:
                    BuildGeneralTab(page);
                    break;
            }

            int maxY = 0;
            foreach (Control c in page.Controls)
            {
                if (c.Bottom > maxY) maxY = c.Bottom;
            }
            page.Height = maxY + 24;
            return page;
        }

        #region WorkBuddy Tab 0: 通用 (常规 + 权限与偏好，100% 对齐图一)

        private void BuildGeneralTab(Panel page)
        {
            int curY = 4;

            // 1. 分类小标题: "常规" (对标 WorkBuddy 顶部小标题)
            Label lblRegular = CreateSectionHeader("常规", curY);
            page.Controls.Add(lblRegular);
            curY += 28;

            // Card 1: 常规卡片 (高 216px: 语言、文件打开、字体大小、默认打开)
            Panel card1 = CreateCardPanel(CARD_W, 216);
            card1.Location = new Point(0, curY);

            card1.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    // Row 1: 语言 (y=0..54)
                    g.DrawString("语言", f, tb, new PointF(20, (54 - f.GetHeight(g)) / 2f));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2: 链接打开方式 (y=54..108)
                    g.DrawString("链接打开方式", f, tb, new PointF(20, 54 + 9));
                    g.DrawString("设置 LanChat 对话中链接的默认打开策略。", sf, sb, new PointF(20, 54 + 29));
                    g.DrawLine(sep, 16, 108, CARD_W - 16, 108);

                    // Row 3: 字体大小 (y=108..162)
                    g.DrawString("字体大小", f, tb, new PointF(20, 108 + (54 - f.GetHeight(g)) / 2f));
                    g.DrawLine(sep, 16, 162, CARD_W - 16, 162);

                    // Row 4: 默认打开方式 (y=162..216)
                    g.DrawString("默认文件接收目录", f, tb, new PointF(20, 162 + 9));
                    string dir = !string.IsNullOrEmpty(AppSettings.SaveDirectory) ? AppSettings.SaveDirectory : (_netService != null ? _netService.SaveDirectory : "ReceivedFiles");
                    string shortDir = dir.Length > 44 ? "..." + dir.Substring(dir.Length - 41) : dir;
                    g.DrawString(shortDir, sf, sb, new PointF(20, 162 + 29));
                }
            };

            // Row 1 语言下拉胶囊
            Button btnLang = CreateDropdownPill("跟随系统 (简体中文)  ∨", 156, 28, null);
            btnLang.Click += (s, e) => ShowLanguagePicker(btnLang);
            btnLang.Location = new Point(CARD_W - btnLang.Width - 20, (54 - 32) / 2);
            card1.Controls.Add(btnLang);

            // Row 2 链接打开方式下拉胶囊
            Button btnLink = CreateDropdownPill("智能打开  ∨", 112, 28, null);
            btnLink.Click += (s, e) => ShowLinkOpenPicker(btnLink);
            btnLink.Location = new Point(CARD_W - btnLink.Width - 20, 54 + (54 - 32) / 2);
            card1.Controls.Add(btnLink);

            // Row 3 字体大小滑块 (100% 对齐 WorkBuddy 小/默认/大 轨道滑块)
            string[] fontLabels = new string[] { "小", "默认", "中", "大", "特大" };
            float[] fontSizes = new float[] { 9.0f, 10.0f, 11.0f, 12.0f, 14.0f };
            SteppedSlider sliderUiFont = new SteppedSlider(fontLabels, fontSizes, AppSettings.UiFontSize, (val) =>
            {
                AppSettings.UiFontSize = val;
                AppSettings.Save();
                if (_onFontChanged != null) _onFontChanged();
            });
            sliderUiFont.Location = new Point(CARD_W - sliderUiFont.Width - 20, 108 + (54 - 34) / 2);
            card1.Controls.Add(sliderUiFont);

            // Row 4 默认接收目录更改与打开按钮
            Button btnChangeDir = CreateActionLink("更改 >", 72, 28, () =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "请选择文件接收保存目录";
                    string cur = !string.IsNullOrEmpty(AppSettings.SaveDirectory) ? AppSettings.SaveDirectory : (_netService != null ? _netService.SaveDirectory : "ReceivedFiles");
                    fbd.SelectedPath = cur;
                    if (fbd.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(fbd.SelectedPath))
                    {
                        AppSettings.SaveDirectory = fbd.SelectedPath;
                        if (_netService != null) _netService.SaveDirectory = fbd.SelectedPath;
                        AppSettings.Save();
                        card1.Invalidate();
                    }
                }
            });
            btnChangeDir.Location = new Point(CARD_W - btnChangeDir.Width - 140, 162 + (54 - 32) / 2);
            card1.Controls.Add(btnChangeDir);

            Button btnOpenFolder = CreateActionLink("打开文件夹 >", 110, 28, () =>
            {
                if (_onOpenFolder != null) _onOpenFolder();
            });
            btnOpenFolder.Location = new Point(CARD_W - btnOpenFolder.Width - 20, 162 + (54 - 32) / 2);
            card1.Controls.Add(btnOpenFolder);

            page.Controls.Add(card1);
            curY += 216 + 22;

            // 2. 分类小标题: "权限与偏好" (对标 WorkBuddy 第二块)
            Label lblPerm = CreateSectionHeader("权限与偏好", curY);
            page.Controls.Add(lblPerm);
            curY += 28;

            // Card 2: 权限与开关卡片 (高 216px: 锁屏运行、自动接收、开机自启、网络连接)
            Panel card2 = CreateCardPanel(CARD_W, 216);
            card2.Location = new Point(0, curY);

            card2.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    // Row 1: 局域网消息免打扰 (y=0..54)
                    g.DrawString("托盘未读悬浮预览", f, tb, new PointF(20, 9));
                    g.DrawString("新消息闪烁时鼠标悬停在托盘图标上显示发送人与摘要浮窗。", sf, sb, new PointF(20, 29));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2: 自动接收好友文件 (y=54..108)
                    g.DrawString("自动接收好友文件", f, tb, new PointF(20, 54 + 9));
                    g.DrawString("局域网好友发送文件时自动保存到默认目录，无需手动逐一确认。", sf, sb, new PointF(20, 54 + 29));
                    g.DrawLine(sep, 16, 108, CARD_W - 16, 108);

                    // Row 3: 运行时防止休眠 (y=108..162)
                    g.DrawString("后台网络驻留运行", f, tb, new PointF(20, 108 + 9));
                    g.DrawString("关闭主窗口后保持在系统托盘静默运行，随时响应局域网呼叫。", sf, sb, new PointF(20, 108 + 29));
                    g.DrawLine(sep, 16, 162, CARD_W - 16, 162);

                    // Row 4: 网络代理 / 传输模式 (y=162..216)
                    g.DrawString("网络协议通道", f, tb, new PointF(20, 162 + 9));
                    g.DrawString("物理网卡直连通信，多节点广播穿透加速。", sf, sb, new PointF(20, 162 + 29));
                }
            };

            // Row 1 托盘悬浮预览开关 (WorkBuddy 风格 iOS 胶囊薄荷绿开关)
            ModernToggleSwitch swTray = new ModernToggleSwitch(true, (val) => { });
            swTray.Location = new Point(CARD_W - swTray.Width - 24, (54 - 22) / 2);
            card2.Controls.Add(swTray);

            // Row 2 自动接收文件开关 (100% 对齐薄荷绿)
            bool autoAccept = _netService != null ? _netService.AutoAcceptFiles : AppSettings.AutoAcceptFiles;
            ModernToggleSwitch swAutoAccept = new ModernToggleSwitch(autoAccept, (val) =>
            {
                if (_netService != null) _netService.AutoAcceptFiles = val;
                AppSettings.AutoAcceptFiles = val;
                AppSettings.Save();
            });
            swAutoAccept.Location = new Point(CARD_W - swAutoAccept.Width - 24, 54 + (54 - 22) / 2);
            card2.Controls.Add(swAutoAccept);

            // Row 3 后台驻留开关
            ModernToggleSwitch swKeepAlive = new ModernToggleSwitch(true, (val) => { });
            swKeepAlive.Location = new Point(CARD_W - swKeepAlive.Width - 24, 108 + (54 - 22) / 2);
            card2.Controls.Add(swKeepAlive);

            // Row 4 协议模式下拉胶囊
            Button btnProtocol = CreateDropdownPill("直接连接 (LAN)  ∨", 136, 28, null);
            btnProtocol.Location = new Point(CARD_W - btnProtocol.Width - 20, 162 + (54 - 32) / 2);
            card2.Controls.Add(btnProtocol);

            page.Controls.Add(card2);
            curY += 216 + 22;

            // 3. 分类小标题: "支持与关于" (满足用户需求：接收目录、自动接收、支持关于统统收口至通用设置)
            Label lblSupport = CreateSectionHeader("支持与关于", curY);
            page.Controls.Add(lblSupport);
            curY += 28;

            Panel card3 = CreateCardPanel(CARD_W, 108);
            card3.Location = new Point(0, curY);

            card3.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    // Row 1: 关于与更新
                    g.DrawString("关于 LanChat", f, tb, new PointF(20, 9));
                    g.DrawString("版本 v" + MainForm.APP_VERSION + " · 局域网极速通讯与协同传输", sf, sb, new PointF(20, 29));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2: 意见与反馈
                    g.DrawString("意见与问题反馈", f, tb, new PointF(20, 54 + 9));
                    g.DrawString("帮助我们将 LanChat 打磨得更贴合你的直觉与习惯。", sf, sb, new PointF(20, 54 + 29));
                }
            };

            Button btnCheckUpdate = CreateActionLink("检查更新 >", 96, 28, () =>
            {
                if (_onCheckUpdate != null) _onCheckUpdate();
            });
            btnCheckUpdate.Location = new Point(CARD_W - btnCheckUpdate.Width - 20, (54 - 32) / 2);
            card3.Controls.Add(btnCheckUpdate);

            Button btnSendFk = CreateActionLink("发送反馈 >", 96, 28, () =>
            {
                using (GrokFeedbackForm fk = new GrokFeedbackForm())
                {
                    fk.ShowDialog(this);
                }
            });
            btnSendFk.Location = new Point(CARD_W - btnSendFk.Width - 20, 54 + (54 - 32) / 2);
            card3.Controls.Add(btnSendFk);

            page.Controls.Add(card3);
        }

        #endregion

        #region WorkBuddy Tab 1: 个人主页 (对标个人信息)

        private void BuildProfileTab(Panel page)
        {
            int curY = 4;

            Label lblProfile = CreateSectionHeader("个人名片", curY);
            page.Controls.Add(lblProfile);
            curY += 28;

            Panel cardUser = CreateCardPanel(CARD_W, 82);
            cardUser.Location = new Point(0, curY);

            int avSize = 48;
            Rectangle avRect = new Rectangle(20, (82 - avSize) / 2, avSize, avSize);
            cardUser.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                WeChatIcon.DrawMascotAvatarShape(g, avRect, 0, Color.FromArgb(252, 44, 62), true, ColCardBg);

                using (Font nf = new Font(GrokTheme.FontFamily, 11.0f, FontStyle.Bold))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush nb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                {
                    string nick = _netService != null ? _netService.NickName : Environment.UserName;
                    g.DrawString(nick, nf, nb, new PointF(80, 20));
                    g.DrawString("主机: " + Environment.MachineName + " · 局域网在线", sf, sb, new PointF(80, 46));
                }
            };

            Button btnEditNick = CreateActionLink("修改昵称 >", 96, 28, () =>
            {
                if (_onShowChangeNick != null)
                {
                    _onShowChangeNick();
                    cardUser.Invalidate();
                }
            });
            btnEditNick.Location = new Point(CARD_W - btnEditNick.Width - 20, (82 - 32) / 2);
            cardUser.Controls.Add(btnEditNick);

            page.Controls.Add(cardUser);
            curY += 82 + 22;

            Label lblIdentity = CreateSectionHeader("设备标识", curY);
            page.Controls.Add(lblIdentity);
            curY += 28;

            Panel cardDev = CreateCardPanel(CARD_W, 108);
            cardDev.Location = new Point(0, curY);

            cardDev.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    g.DrawString("节点识别码 (Node UUID)", f, tb, new PointF(20, 9));
                    string id = _netService != null ? _netService.LocalId : "0000-0000";
                    g.DrawString(id, sf, sb, new PointF(20, 29));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    g.DrawString("操作系统环境", f, tb, new PointF(20, 54 + 9));
                    g.DrawString(Environment.OSVersion.VersionString + " (" + (Environment.Is64BitOperatingSystem ? "64位" : "32位") + ")", sf, sb, new PointF(20, 54 + 29));
                }
            };

            Button btnCopyId = CreateActionLink("复制 ID >", 86, 28, null);
            btnCopyId.Click += (s, e) =>
            {
                try
                {
                    string id = _netService != null ? _netService.LocalId : "";
                    Clipboard.SetText(id);
                    btnCopyId.Text = "已复制 ✓";
                }
                catch { }
            };
            btnCopyId.Location = new Point(CARD_W - btnCopyId.Width - 20, (54 - 32) / 2);
            cardDev.Controls.Add(btnCopyId);

            page.Controls.Add(cardDev);
        }

        #endregion

        #region WorkBuddy Tab 2: 外观 (主题、强调色与双滑块字体排版)

        private void BuildAppearanceTab(Panel page)
        {
            int curY = 4;

            Label lblApp = CreateSectionHeader("外观偏好", curY);
            page.Controls.Add(lblApp);
            curY += 28;

            Panel cardTheme = CreateCardPanel(CARD_W, 108);
            cardTheme.Location = new Point(0, curY);

            cardTheme.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    g.DrawString("主题模式", f, tb, new PointF(20, (54 - f.GetHeight(g)) / 2f));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    g.DrawString("强调色", f, tb, new PointF(20, 54 + (54 - f.GetHeight(g)) / 2f));
                }
            };

            Button btnTheme = CreateDropdownPill("深色 (Grok Dark)  ∨", 150, 28, null);
            btnTheme.Location = new Point(CARD_W - btnTheme.Width - 20, (54 - 32) / 2);
            cardTheme.Controls.Add(btnTheme);

            Button btnAccent = CreateDropdownPill("魅紫 (#8E64DC)  ∨", 150, 28, null);
            btnAccent.Location = new Point(CARD_W - btnAccent.Width - 20, 54 + (54 - 32) / 2);
            cardTheme.Controls.Add(btnAccent);

            page.Controls.Add(cardTheme);
            curY += 108 + 22;

            Label lblFontSec = CreateSectionHeader("字体与排版", curY);
            page.Controls.Add(lblFontSec);
            curY += 28;

            Panel cardFont = CreateCardPanel(CARD_W, 216);
            cardFont.Location = new Point(0, curY);

            cardFont.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    // Row 1: 主界面字体样式
                    g.DrawString("主界面字体样式", f, tb, new PointF(20, (54 - f.GetHeight(g)) / 2f));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2: 主界面字体大小 (对标 WorkBuddy 滑块)
                    g.DrawString("主界面字体大小", f, tb, new PointF(20, 54 + 9));
                    g.DrawString("缩放主窗体、按钮、侧边栏文字比例。", sf, sb, new PointF(20, 54 + 29));
                    g.DrawLine(sep, 16, 108, CARD_W - 16, 108);

                    // Row 3: 聊天框字体样式
                    g.DrawString("聊天框字体样式", f, tb, new PointF(20, 108 + (54 - f.GetHeight(g)) / 2f));
                    g.DrawLine(sep, 16, 162, CARD_W - 16, 162);

                    // Row 4: 聊天框字体大小 (对标 WorkBuddy 滑块)
                    g.DrawString("聊天框字体大小", f, tb, new PointF(20, 162 + 9));
                    g.DrawString("调整聊天消息气泡正文排版字号。", sf, sb, new PointF(20, 162 + 29));
                }
            };

            // Row 1 主界面字体下拉
            Button btnUiFont = CreateDropdownPill(GetUiFontDisplayName() + "  ∨", 150, 28, null);
            btnUiFont.Click += (s, e) => ShowUiFontPicker(btnUiFont);
            btnUiFont.Location = new Point(CARD_W - btnUiFont.Width - 20, (54 - 32) / 2);
            cardFont.Controls.Add(btnUiFont);

            // Row 2 主界面字体滑块 (WorkBuddy 风格 SteppedSlider)
            string[] uiLabels = new string[] { "小", "默认", "中", "大", "特大" };
            float[] uiSizes = new float[] { 9.0f, 10.0f, 11.0f, 12.0f, 14.0f };
            SteppedSlider sliderUi = new SteppedSlider(uiLabels, uiSizes, AppSettings.UiFontSize, (val) =>
            {
                AppSettings.UiFontSize = val;
                AppSettings.Save();
                if (_onFontChanged != null) _onFontChanged();
            });
            sliderUi.Location = new Point(CARD_W - sliderUi.Width - 20, 54 + (54 - 34) / 2);
            cardFont.Controls.Add(sliderUi);

            // Row 3 聊天框字体下拉
            Button btnChatFont = CreateDropdownPill(GetChatFontDisplayName() + "  ∨", 150, 28, null);
            btnChatFont.Click += (s, e) => ShowChatFontPicker(btnChatFont);
            btnChatFont.Location = new Point(CARD_W - btnChatFont.Width - 20, 108 + (54 - 32) / 2);
            cardFont.Controls.Add(btnChatFont);

            // Row 4 聊天框字体滑块 (WorkBuddy 风格 SteppedSlider)
            string[] chatLabels = new string[] { "小", "默认", "中", "大", "特大" };
            float[] chatSizes = new float[] { 9.5f, 10.5f, 11.5f, 13.0f, 15.0f };
            SteppedSlider sliderChat = new SteppedSlider(chatLabels, chatSizes, AppSettings.ChatFontSize, (val) =>
            {
                AppSettings.ChatFontSize = val;
                AppSettings.Save();
                if (_onFontChanged != null) _onFontChanged();
            });
            sliderChat.Location = new Point(CARD_W - sliderChat.Width - 20, 162 + (54 - 34) / 2);
            cardFont.Controls.Add(sliderChat);

            page.Controls.Add(cardFont);
        }

        #endregion

        #region WorkBuddy Tab 3: 快捷键

        private void BuildShortcutsTab(Panel page)
        {
            int curY = 4;

            Label lblKey = CreateSectionHeader("快捷操作", curY);
            page.Controls.Add(lblKey);
            curY += 28;

            Panel cardKey = CreateCardPanel(CARD_W, 216);
            cardKey.Location = new Point(0, curY);

            cardKey.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font kf = new Font("Consolas", 9.5f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush kb = new SolidBrush(Color.FromArgb(170, 170, 185)))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    string[] titles = new string[] { "发送消息", "多行换行输入", "快速搜索联系人", "清空当前聊天" };
                    string[] shortcuts = new string[] { "Enter", "Shift + Enter", "Ctrl + F", "Ctrl + L" };

                    for (int i = 0; i < 4; i++)
                    {
                        int y = i * 54;
                        g.DrawString(titles[i], f, tb, new PointF(20, y + (54 - f.GetHeight(g)) / 2f));
                        if (i < 3) g.DrawLine(sep, 16, y + 54, CARD_W - 16, y + 54);

                        // 快捷键芯片徽章
                        SizeF ssz = g.MeasureString(shortcuts[i], kf);
                        int bw = (int)ssz.Width + 16;
                        Rectangle br = new Rectangle(CARD_W - bw - 20, y + (54 - 24) / 2, bw, 24);
                        using (SolidBrush bb = new SolidBrush(ColPillBg))
                        using (Pen bp = new Pen(ColPillBorder, 1f))
                        {
                            MainForm.DrawRoundedRectangle(g, bb, br, 6);
                            MainForm.DrawRoundedRectangle(g, bp, br, 6);
                        }
                        g.DrawString(shortcuts[i], kf, kb, new PointF(br.X + 8, br.Y + (24 - kf.GetHeight(g)) / 2f));
                    }
                }
            };

            page.Controls.Add(cardKey);
        }

        #endregion

        #region WorkBuddy Tab 4: 文件传输

        private void BuildFileTransferTab(Panel page)
        {
            int curY = 4;

            Label lblFile = CreateSectionHeader("文件偏好", curY);
            page.Controls.Add(lblFile);
            curY += 28;

            Panel cardFile = CreateCardPanel(CARD_W, 162);
            cardFile.Location = new Point(0, curY);

            string curDir = !string.IsNullOrEmpty(AppSettings.SaveDirectory) ? AppSettings.SaveDirectory : (_netService != null ? _netService.SaveDirectory : "ReceivedFiles");
            bool autoAccept = _netService != null ? _netService.AutoAcceptFiles : AppSettings.AutoAcceptFiles;

            cardFile.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    // Row 1
                    g.DrawString("自动接收好友文件", f, tb, new PointF(20, 9));
                    g.DrawString("局域网内接收文件时无需手动逐一确认保存。", sf, sb, new PointF(20, 29));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2
                    g.DrawString("接收文件默认存储目录", f, tb, new PointF(20, 54 + 9));
                    string shortDir = curDir.Length > 44 ? "..." + curDir.Substring(curDir.Length - 41) : curDir;
                    g.DrawString(shortDir, sf, sb, new PointF(20, 54 + 29));
                    g.DrawLine(sep, 16, 108, CARD_W - 16, 108);

                    // Row 3
                    g.DrawString("64KB 分块极速传输校验", f, tb, new PointF(20, 108 + 9));
                    g.DrawString("纯原生 LANC 协议流式流转，零中转零依赖。", sf, sb, new PointF(20, 108 + 29));
                }
            };

            ModernToggleSwitch swAuto = new ModernToggleSwitch(autoAccept, (val) =>
            {
                if (_netService != null) _netService.AutoAcceptFiles = val;
                AppSettings.AutoAcceptFiles = val;
                AppSettings.Save();
            });
            swAuto.Location = new Point(CARD_W - swAuto.Width - 24, (54 - 22) / 2);
            cardFile.Controls.Add(swAuto);

            Button btnChangeDir = CreateActionLink("更改 >", 72, 28, () =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "请选择文件接收保存目录";
                    fbd.SelectedPath = curDir;
                    if (fbd.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(fbd.SelectedPath))
                    {
                        AppSettings.SaveDirectory = fbd.SelectedPath;
                        if (_netService != null) _netService.SaveDirectory = fbd.SelectedPath;
                        AppSettings.Save();
                        cardFile.Invalidate();
                    }
                }
            });
            btnChangeDir.Location = new Point(CARD_W - btnChangeDir.Width - 140, 54 + (54 - 32) / 2);
            cardFile.Controls.Add(btnChangeDir);

            Button btnOpenDir = CreateActionLink("打开文件夹 >", 110, 28, () =>
            {
                if (_onOpenFolder != null) _onOpenFolder();
            });
            btnOpenDir.Location = new Point(CARD_W - btnOpenDir.Width - 20, 54 + (54 - 32) / 2);
            cardFile.Controls.Add(btnOpenDir);

            ModernToggleSwitch swChunk = new ModernToggleSwitch(true, null);
            swChunk.Location = new Point(CARD_W - swChunk.Width - 24, 108 + (54 - 22) / 2);
            cardFile.Controls.Add(swChunk);

            page.Controls.Add(cardFile);
        }

        #endregion

        #region WorkBuddy Tab 5: 聊天记录 (自动保存、存储路径与管理)

        private void BuildChatHistoryTab(Panel page)
        {
            int curY = 4;

            Label lblHist = CreateSectionHeader("聊天记录管理", curY);
            page.Controls.Add(lblHist);
            curY += 28;

            Panel cardHist = CreateCardPanel(CARD_W, 162);
            cardHist.Location = new Point(0, curY);

            cardHist.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    // Row 1
                    g.DrawString("聊天记录存储目录", f, tb, new PointF(20, 9));
                    string curHDir = ChatHistoryService.StorageDirectory;
                    string shortHDir = curHDir.Length > 44 ? "..." + curHDir.Substring(curHDir.Length - 41) : curHDir;
                    g.DrawString(shortHDir, sf, sb, new PointF(20, 29));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2
                    g.DrawString("自动加载历史消息", f, tb, new PointF(20, 54 + 9));
                    g.DrawString("重启或更新覆盖安装后，自动读取历史会话与消息流。", sf, sb, new PointF(20, 54 + 29));
                    g.DrawLine(sep, 16, 108, CARD_W - 16, 108);

                    // Row 3
                    g.DrawString("彻底清空所有本地聊天记录", f, tb, new PointF(20, 108 + 9));
                    g.DrawString("彻底清除本机保存的所有广播与好友聊天历史文件。", sf, sb, new PointF(20, 108 + 29));
                }
            };

            Button btnChangeHist = CreateActionLink("更改 >", 72, 28, () =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "请选择聊天记录存储路径 (系统将自动迁移已有历史记录)";
                    fbd.SelectedPath = ChatHistoryService.StorageDirectory;
                    if (fbd.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(fbd.SelectedPath))
                    {
                        ChatHistoryService.ChangeStorageDirectory(fbd.SelectedPath, true);
                        cardHist.Invalidate();
                    }
                }
            });
            btnChangeHist.Location = new Point(CARD_W - btnChangeHist.Width - 140, (54 - 32) / 2);
            cardHist.Controls.Add(btnChangeHist);

            Button btnOpenHist = CreateActionLink("打开文件夹 >", 110, 28, () =>
            {
                try { Process.Start("explorer.exe", ChatHistoryService.StorageDirectory); } catch { }
            });
            btnOpenHist.Location = new Point(CARD_W - btnOpenHist.Width - 20, (54 - 32) / 2);
            cardHist.Controls.Add(btnOpenHist);

            ModernToggleSwitch swAutoLoad = new ModernToggleSwitch(true, null);
            swAutoLoad.Location = new Point(CARD_W - swAutoLoad.Width - 24, 54 + (54 - 22) / 2);
            cardHist.Controls.Add(swAutoLoad);

            Button btnClrHist = CreateActionLink("立即清空 >", 96, 28, null, true);
            btnClrHist.Click += (s, e) =>
            {
                if (GrokMessageBox.Show(this, "确定要彻底清空本机所有本地聊天记录吗？此操作将删除所有往期历史且无法撤销。", "清空记录确认", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    ChatHistoryService.ClearAllHistories();
                    btnClrHist.Text = "已清空 ✓";
                }
            };
            btnClrHist.Location = new Point(CARD_W - btnClrHist.Width - 20, 108 + (54 - 32) / 2);
            cardHist.Controls.Add(btnClrHist);

            page.Controls.Add(cardHist);
        }

        #endregion

        #region WorkBuddy Tab 6: 网络与设备

        private void BuildNetworkDeviceTab(Panel page)
        {
            int curY = 4;

            Label lblNet = CreateSectionHeader("物理网络与设备", curY);
            page.Controls.Add(lblNet);
            curY += 28;

            Panel cardNet = CreateCardPanel(CARD_W, 162);
            cardNet.Location = new Point(0, curY);

            string ipStr = _netService != null ? _netService.GetPrimaryPhysicalIp().ToString() : "127.0.0.1";
            int port = _netService != null ? _netService.LocalTcpPort : 38889;

            cardNet.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    // Row 1
                    g.DrawString("物理以太网 / Wi-Fi IP 绑定", f, tb, new PointF(20, 9));
                    g.DrawString("IP: " + ipStr + ":" + port + "  ·  UDP 38888 广播服务就绪", sf, sb, new PointF(20, 29));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2
                    g.DrawString("TUN / TAP 虚拟网卡穿透过滤", f, tb, new PointF(20, 54 + 9));
                    g.DrawString("智能识别并过滤 Clash / WireGuard 虚拟网卡，防止广播风暴。", sf, sb, new PointF(20, 54 + 29));
                    g.DrawLine(sep, 16, 108, CARD_W - 16, 108);

                    // Row 3
                    g.DrawString("多网卡并发单播探测", f, tb, new PointF(20, 108 + 9));
                    g.DrawString("在局域网复杂交换机拓扑下并发向物理子网各节点单播探测。", sf, sb, new PointF(20, 108 + 29));
                }
            };

            Button btnStatus = CreateDropdownPill("● 局域网在线  ∨", 126, 28, null);
            btnStatus.Location = new Point(CARD_W - btnStatus.Width - 20, (54 - 32) / 2);
            cardNet.Controls.Add(btnStatus);

            ModernToggleSwitch swTun = new ModernToggleSwitch(true, null);
            swTun.Location = new Point(CARD_W - swTun.Width - 24, 54 + (54 - 22) / 2);
            cardNet.Controls.Add(swTun);

            ModernToggleSwitch swUnicast = new ModernToggleSwitch(true, null);
            swUnicast.Location = new Point(CARD_W - swUnicast.Width - 24, 108 + (54 - 22) / 2);
            cardNet.Controls.Add(swUnicast);

            page.Controls.Add(cardNet);
        }

        #endregion

        #region WorkBuddy Tab 7: 关于与更新

        private void BuildAboutTab(Panel page)
        {
            int curY = 4;

            Label lblAbout = CreateSectionHeader("关于产品", curY);
            page.Controls.Add(lblAbout);
            curY += 28;

            Panel cardAbout = CreateCardPanel(CARD_W, 140);
            cardAbout.Location = new Point(0, curY);

            cardAbout.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int bSize = 56;
                Rectangle bRect = new Rectangle(20, 20, bSize, bSize);
                using (SolidBrush bb = new SolidBrush(Color.FromArgb(36, 36, 42)))
                using (Pen bp = new Pen(Color.FromArgb(52, 52, 60), 1f))
                {
                    MainForm.DrawRoundedRectangle(g, bb, bRect, 14);
                    MainForm.DrawRoundedRectangle(g, bp, bRect, 14);
                }
                Rectangle mRect = new Rectangle(28, 28, 40, 40);
                WeChatIcon.DrawMascotAvatarShape(g, mRect, 0, Color.FromArgb(245, 245, 250), false, Color.Transparent);

                using (Font tf = new Font(GrokTheme.FontFamily, 12f, FontStyle.Bold))
                using (Font mf = new Font(GrokTheme.FontFamily, 9.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                {
                    g.DrawString("LanChat 局域网极速通讯", tf, tb, new PointF(90, 20));
                    g.DrawString("版本 v" + MainForm.APP_VERSION + " · WorkBuddy 1:1 对标现代化极简暗黑视效", mf, sb, new PointF(90, 44));
                    g.DrawString("原生高性能零冗余依赖架构 · Copyright © 2026 LanChat Team", sf, sb, new PointF(90, 64));
                }
            };

            Button btnCheck = CreateActionLink("检查更新 >", 96, 28, () =>
            {
                if (_onCheckUpdate != null) _onCheckUpdate();
            });
            btnCheck.Location = new Point(CARD_W - btnCheck.Width - 20, 24);
            cardAbout.Controls.Add(btnCheck);

            Button btnCopyDiag = CreateActionLink("复制诊断信息 >", 116, 28, null);
            btnCopyDiag.Click += (s, e) =>
            {
                try
                {
                    string info = string.Format("LanChat v{0}\nOS: {1}\nMachine: {2}\nCLR: {3}",
                        MainForm.APP_VERSION, Environment.OSVersion, Environment.MachineName, Environment.Version);
                    Clipboard.SetText(info);
                    btnCopyDiag.Text = "已复制 ✓";
                }
                catch { }
            };
            btnCopyDiag.Location = new Point(CARD_W - btnCopyDiag.Width - 20, 70);
            cardAbout.Controls.Add(btnCopyDiag);

            page.Controls.Add(cardAbout);
            curY += 140 + 22;

            Label lblFeedback = CreateSectionHeader("支持与反馈", curY);
            page.Controls.Add(lblFeedback);
            curY += 28;

            Panel cardFk = CreateCardPanel(CARD_W, 64);
            cardFk.Location = new Point(0, curY);

            cardFk.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                {
                    g.DrawString("意见与问题反馈", f, tb, new PointF(20, 14));
                    g.DrawString("帮助我们将 LanChat 打磨得更贴合你的直觉与习惯。", sf, sb, new PointF(20, 34));
                }
            };

            Button btnOpenFk = CreateActionLink("发送反馈 >", 96, 28, () =>
            {
                using (GrokFeedbackForm fk = new GrokFeedbackForm())
                {
                    fk.ShowDialog(this);
                }
            });
            btnOpenFk.Location = new Point(CARD_W - btnOpenFk.Width - 20, (64 - 32) / 2);
            cardFk.Controls.Add(btnOpenFk);

            page.Controls.Add(cardFk);
        }

        #endregion

        #region 自绘浮动极简暗黑滚动条 (Zero White Scrollbar)

        private void ViewportPanel_Paint(object sender, PaintEventArgs e)
        {
            if (_maxScroll <= 0) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int viewH = _viewportPanel.Height;
            int totalH = _scrollContainer.Height;
            int thumbH = Math.Max(32, (int)((float)viewH / totalH * viewH));
            int thumbY = (int)((float)_scrollOffset / _maxScroll * (viewH - thumbH));

            int barW = (_isThumbHover || _isThumbDragging) ? 5 : 4;
            int barX = _viewportPanel.Width - barW - 2;

            Rectangle thumbRect = new Rectangle(barX, thumbY, barW, thumbH);
            Color thumbCol = (_isThumbHover || _isThumbDragging) ? Color.FromArgb(90, 90, 100) : Color.FromArgb(52, 52, 60);

            using (SolidBrush sb = new SolidBrush(thumbCol))
            {
                MainForm.DrawRoundedRectangle(g, sb, thumbRect, barW / 2);
            }
        }

        private void ViewportPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (_maxScroll <= 0) return;
            int barX = _viewportPanel.Width - 10;
            if (e.X >= barX)
            {
                _isThumbDragging = true;
                _dragStartY = e.Y;
                _dragStartOffset = _scrollOffset;
                _viewportPanel.Invalidate();
            }
        }

        private void ViewportPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_maxScroll <= 0) return;
            int barX = _viewportPanel.Width - 10;
            bool hover = (e.X >= barX);
            if (hover != _isThumbHover)
            {
                _isThumbHover = hover;
                _viewportPanel.Invalidate();
            }

            if (_isThumbDragging)
            {
                int dy = e.Y - _dragStartY;
                int viewH = _viewportPanel.Height;
                int totalH = _scrollContainer.Height;
                int thumbH = Math.Max(32, (int)((float)viewH / totalH * viewH));
                int availTrack = Math.Max(1, viewH - thumbH);

                int deltaOffset = (int)((float)dy / availTrack * _maxScroll);
                int target = _dragStartOffset + deltaOffset;
                if (target < 0) target = 0;
                if (target > _maxScroll) target = _maxScroll;

                if (target != _scrollOffset)
                {
                    _scrollOffset = target;
                    _scrollContainer.Location = new Point(0, -_scrollOffset);
                    _viewportPanel.Invalidate();
                }
            }
        }

        private void ViewportPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isThumbDragging)
            {
                _isThumbDragging = false;
                _viewportPanel.Invalidate();
            }
        }

        #endregion

        #region Helper Component Factories (UI 细节对标 WorkBuddy)

        private Label CreateSectionHeader(string text, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(4, y),
                Size = new Size(300, 24),
                Font = new Font(GrokTheme.FontFamily, 11.0f, FontStyle.Regular),
                ForeColor = ColSectionHeader,
                BackColor = ColWindowBg
            };
        }

        private Panel CreateCardPanel(int w, int h)
        {
            Panel p = new Panel
            {
                Size = new Size(w, h),
                BackColor = ColWindowBg
            };
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, w - 1, h - 1);
                using (SolidBrush b = new SolidBrush(ColCardBg))
                using (Pen pen = new Pen(ColCardBorder, 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 12);
                    MainForm.DrawRoundedRectangle(e.Graphics, pen, r, 12);
                }
            };
            return p;
        }

        private Button CreateDropdownPill(string text, int minW, int h, Action onClick)
        {
            int btnH = (h > 0) ? h : 32;
            Button btn = new Button
            {
                Text = text,
                Height = btnH,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColCardBg,
                Cursor = Cursors.Hand,
                Font = GrokTheme.FontSecondary
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ColCardBg;
            btn.FlatAppearance.MouseDownBackColor = ColCardBg;

            // 预留舒适边距 (文字与外边框至少 16px 间隙，右侧留出 16px 箭头位置，杜绝任何折行)
            string cleanLabel = text.Replace("  ∨", "").Trim();
            using (Graphics gTemp = this.CreateGraphics())
            {
                SizeF sz = gTemp.MeasureString(cleanLabel, btn.Font);
                btn.Width = Math.Max(minW, (int)sz.Width + 56);
            }

            bool hover = false;
            btn.MouseEnter += (s, e) => { hover = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { hover = false; btn.Invalidate(); };
            if (onClick != null) btn.Click += (s, e) => onClick();

            btn.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (SolidBrush bgB = new SolidBrush(ColCardBg))
                {
                    g.FillRectangle(bgB, 0, 0, btn.Width, btn.Height);
                }

                Rectangle r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (SolidBrush b = new SolidBrush(hover ? GrokTheme.Level3Hot : ColPillBg))
                using (Pen p = new Pen(hover ? Color.FromArgb(70, 70, 82) : ColPillBorder, 1f))
                {
                    GrokTheme.DrawRoundedRectangle(g, b, r, 7);
                    GrokTheme.DrawRoundedRectangle(g, p, r, 7);
                }

                // 绘制明亮文字 (WorkBuddy 规范纯白高对比度 #FFFFFF，左留 16px 呼吸间距，杜绝折行)
                string curLabel = btn.Text.Replace("  ∨", "").Trim();
                using (SolidBrush tb = new SolidBrush(GrokTheme.TextTitle))
                {
                    float ty = (btn.Height - btn.Font.GetHeight(g)) / 2f;
                    g.DrawString(curLabel, btn.Font, tb, new PointF(16, ty));
                }

                // 绘制右侧下拉箭头 (∨)，右侧留出 14px 舒适间距
                using (Font arrowFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                using (SolidBrush arrowBrush = new SolidBrush(hover ? GrokTheme.TextTitle : GrokTheme.TextSecondary))
                {
                    float ay = (btn.Height - arrowFont.GetHeight(g)) / 2f;
                    g.DrawString("∨", arrowFont, arrowBrush, new PointF(btn.Width - 22, ay));
                }
            };
            return btn;
        }

        private Button CreateActionLink(string text, int minW, int h, Action onClick, bool isDanger = false)
        {
            int btnH = (h > 0) ? h : 32;
            Button btn = new Button
            {
                Text = text,
                Height = btnH,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColCardBg,
                Cursor = Cursors.Hand,
                Font = GrokTheme.FontSecondary
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ColCardBg;
            btn.FlatAppearance.MouseDownBackColor = ColCardBg;

            using (Graphics gTemp = this.CreateGraphics())
            {
                SizeF sz = gTemp.MeasureString(text, btn.Font);
                btn.Width = Math.Max(minW, (int)sz.Width + 24);
            }

            bool hover = false;
            btn.MouseEnter += (s, e) => { hover = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { hover = false; btn.Invalidate(); };
            if (onClick != null) btn.Click += (s, e) => onClick();

            btn.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (SolidBrush bgB = new SolidBrush(ColCardBg))
                {
                    g.FillRectangle(bgB, 0, 0, btn.Width, btn.Height);
                }

                Color tc = isDanger
                    ? (hover ? GrokTheme.TextDestructiveHot : GrokTheme.TextDestructive)
                    : (hover ? GrokTheme.TextTitle : GrokTheme.TextSubtitle);

                using (SolidBrush tb = new SolidBrush(tc))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    g.DrawString(btn.Text, btn.Font, tb, new RectangleF(0, 0, btn.Width - 6, btn.Height), sf);
                }
            };
            return btn;
        }

        #endregion

        #region iOS / WorkBuddy 现代滑动开关 (Toggle Switch)

        public class ModernToggleSwitch : Control
        {
            private bool _checked = false;
            private readonly Action<bool> _onChanged;

            public bool Checked
            {
                get { return _checked; }
                set { if (_checked != value) { _checked = value; Invalidate(); } }
            }

            public ModernToggleSwitch(bool isChecked, Action<bool> onChanged)
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
                this.Size = new Size(40, 22);
                this.Cursor = Cursors.Hand;
                this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = ColCardBg;
                _checked = isChecked;
                _onChanged = onChanged;
                this.Click += (s, e) =>
                {
                    _checked = !_checked;
                    Invalidate();
                    if (_onChanged != null) _onChanged(_checked);
                };
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (SolidBrush bgB = new SolidBrush(ColCardBg))
                {
                    g.FillRectangle(bgB, 0, 0, this.Width, this.Height);
                }

                Color bg = _checked ? ColMintGreen : ColSwitchOff;
                Rectangle pill = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
                using (SolidBrush bb = new SolidBrush(bg))
                {
                    MainForm.DrawRoundedRectangle(g, bb, pill, 11);
                }

                int knobSize = 16;
                int knobX = _checked ? (this.Width - knobSize - 3) : 3;
                int knobY = (this.Height - knobSize) / 2;
                Rectangle knobRect = new Rectangle(knobX, knobY, knobSize, knobSize);

                using (SolidBrush kb = new SolidBrush(Color.White))
                {
                    g.FillEllipse(kb, knobRect);
                }
            }
        }

        #endregion

        #region WorkBuddy 刻度轨道滑块 (Stepped Slider)

        public class SteppedSlider : Control
        {
            private readonly string[] _labels;
            private readonly float[] _values;
            private int _selectedIndex;
            private readonly Action<float> _onValueChanged;
            private bool _isDragging = false;

            public SteppedSlider(string[] labels, float[] values, float currentValue, Action<float> onValueChanged)
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
                this.Size = new Size(280, 34);
                this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = ColCardBg;
                this.Cursor = Cursors.Hand;
                _labels = labels;
                _values = values;
                _onValueChanged = onValueChanged;

                _selectedIndex = 0;
                float minDiff = float.MaxValue;
                for (int i = 0; i < values.Length; i++)
                {
                    float diff = Math.Abs(values[i] - currentValue);
                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        _selectedIndex = i;
                    }
                }

                this.MouseDown += (s, e) => { _isDragging = true; HandleClick(e.X); };
                this.MouseMove += (s, e) => { if (_isDragging) HandleClick(e.X); };
                this.MouseUp += (s, e) => { _isDragging = false; };
            }

            private void HandleClick(int mouseX)
            {
                int count = _labels.Length;
                int trackPad = 12;
                float trackW = this.Width - trackPad * 2;
                int bestIdx = 0;
                float bestDist = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    float x = trackPad + (trackW / (count - 1)) * i;
                    float dist = Math.Abs(mouseX - x);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIdx = i;
                    }
                }

                if (bestIdx != _selectedIndex)
                {
                    _selectedIndex = bestIdx;
                    Invalidate();
                    if (_onValueChanged != null && _selectedIndex < _values.Length)
                    {
                        _onValueChanged(_values[_selectedIndex]);
                    }
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (SolidBrush bgB = new SolidBrush(ColCardBg))
                {
                    g.FillRectangle(bgB, 0, 0, this.Width, this.Height);
                }

                int count = _labels.Length;
                int trackPad = 12;
                int railY = 8;
                float trackW = this.Width - trackPad * 2;

                // 轨道线
                using (Pen rp = new Pen(Color.FromArgb(50, 50, 58), 2f))
                {
                    g.DrawLine(rp, trackPad, railY, this.Width - trackPad, railY);
                }

                // 刻度与文字
                using (Pen tp = new Pen(Color.FromArgb(70, 70, 80), 2f))
                using (Font lf = new Font("Microsoft YaHei UI", 8.0f, FontStyle.Regular))
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(120, 120, 132)))
                using (SolidBrush activeText = new SolidBrush(Color.FromArgb(235, 235, 242)))
                {
                    for (int i = 0; i < count; i++)
                    {
                        float x = trackPad + (trackW / (count - 1)) * i;
                        g.DrawLine(tp, x, railY - 3, x, railY + 3);

                        string lbl = _labels[i];
                        SizeF sz = g.MeasureString(lbl, lf);
                        float tx = x - sz.Width / 2f;
                        float ty = railY + 6;
                        g.DrawString(lbl, lf, (i == _selectedIndex) ? activeText : textBrush, tx, ty);
                    }
                }

                // 滑块钮 (圆点)
                float thumbX = trackPad + (trackW / (count - 1)) * _selectedIndex;
                int knobSize = 12;
                Rectangle knobRect = new Rectangle((int)thumbX - knobSize / 2, railY - knobSize / 2, knobSize, knobSize);

                using (SolidBrush kb = new SolidBrush(Color.FromArgb(210, 210, 220)))
                using (Pen kp = new Pen(Color.FromArgb(24, 24, 28), 1.5f))
                {
                    g.FillEllipse(kb, knobRect);
                    g.DrawEllipse(kp, knobRect);
                }
            }
        }

        #endregion

        #region Font Pickers Helpers

        private string GetUiFontDisplayName()
        {
            string f = AppSettings.UiFontFamily;
            if (string.IsNullOrEmpty(f) || f == "Microsoft YaHei UI") return "微软雅黑";
            return f;
        }

        private string GetChatFontDisplayName()
        {
            string f = AppSettings.ChatFontFamily;
            if (string.IsNullOrEmpty(f) || f == "Microsoft YaHei UI") return "微软雅黑";
            return f;
        }

        private void ShowLanguagePicker(Button anchor)
        {
            Point pt = anchor.PointToScreen(new Point(0, anchor.Height + 4));
            GrokPopupMenu menu = new GrokPopupMenu();
            menu.AddItem("跟随系统 (简体中文)", WeChatIconType.None, () => { anchor.Text = "跟随系统 (简体中文)  ∨"; }, "✓")
                .AddItem("English (US)", WeChatIconType.None, () => { anchor.Text = "English (US)  ∨"; })
                .AddItem("繁体中文 (中国香港)", WeChatIconType.None, () => { anchor.Text = "繁体中文  ∨"; });
            menu.ShowAt(pt);
        }

        private void ShowLinkOpenPicker(Button anchor)
        {
            Point pt = anchor.PointToScreen(new Point(0, anchor.Height + 4));
            GrokPopupMenu menu = new GrokPopupMenu();
            menu.AddItem("智能打开 (默认浏览器)", WeChatIconType.None, () => { anchor.Text = "智能打开  ∨"; }, "✓")
                .AddItem("内置极速预览", WeChatIconType.None, () => { anchor.Text = "内置极速预览  ∨"; });
            menu.ShowAt(pt);
        }

        private void ShowUiFontPicker(Button anchor)
        {
            Point pt = anchor.PointToScreen(new Point(0, anchor.Height + 4));
            GrokPopupMenu menu = new GrokPopupMenu();
            string[] fontNames = new string[] { "Microsoft YaHei UI", "Segoe UI", "宋体", "黑体", "楷体", "Arial" };
            string[] fontLabels = new string[] { "微软雅黑 (默认)", "Segoe UI", "宋体 (SimSun)", "黑体 (SimHei)", "楷体 (KaiTi)", "Arial" };
            for (int i = 0; i < fontNames.Length; i++)
            {
                string fName = fontNames[i];
                string fLabel = fontLabels[i];
                bool isCur = string.Equals(AppSettings.UiFontFamily, fName, StringComparison.OrdinalIgnoreCase);
                menu.AddItem(fLabel, WeChatIconType.None, () =>
                {
                    AppSettings.UiFontFamily = fName;
                    AppSettings.Save();
                    anchor.Text = (fName == "Microsoft YaHei UI" ? "微软雅黑" : fName) + "  ∨";
                    anchor.Invalidate();
                    if (_onFontChanged != null) _onFontChanged();
                }, isCur ? "✓" : "");
            }
            menu.ShowAt(pt);
        }

        private void ShowChatFontPicker(Button anchor)
        {
            Point pt = anchor.PointToScreen(new Point(0, anchor.Height + 4));
            GrokPopupMenu menu = new GrokPopupMenu();
            string[] fontNames = new string[] { "Microsoft YaHei UI", "Segoe UI", "Consolas", "宋体", "黑体", "楷体", "Arial" };
            string[] fontLabels = new string[] { "微软雅黑 (默认)", "Segoe UI", "Consolas (代码风)", "宋体 (SimSun)", "黑体 (SimHei)", "楷体 (KaiTi)", "Arial" };
            for (int i = 0; i < fontNames.Length; i++)
            {
                string fName = fontNames[i];
                string fLabel = fontLabels[i];
                bool isCur = string.Equals(AppSettings.ChatFontFamily, fName, StringComparison.OrdinalIgnoreCase);
                menu.AddItem(fLabel, WeChatIconType.None, () =>
                {
                    AppSettings.ChatFontFamily = fName;
                    AppSettings.Save();
                    anchor.Text = (fName == "Microsoft YaHei UI" ? "微软雅黑" : fName) + "  ∨";
                    anchor.Invalidate();
                    if (_onFontChanged != null) _onFontChanged();
                }, isCur ? "✓" : "");
            }
            menu.ShowAt(pt);
        }

        #endregion

        #region Navigation, Sidebar & Window Paint

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                // 侧边栏搜索框胶囊激活
                Rectangle searchRect = new Rectangle(16, 18, SIDEBAR_W - 32, 34);
                if (searchRect.Contains(e.Location))
                {
                    _txtSidebarSearch.Focus();
                }

                // 侧边栏选项卡点击
                if (e.X < SIDEBAR_W && e.Y > 58)
                {
                    int clickedId = HitTestNavItem(e.Location);
                    if (clickedId >= 0 && clickedId != _activeTabId)
                    {
                        _activeTabId = clickedId;
                        SwitchToTab(clickedId);
                        return;
                    }
                }

                // 顶部拖拽窗口
                if (e.Y < 48 && e.X > SIDEBAR_W && e.X < this.Width - 56)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, 0x00A1, (IntPtr)0x0002, IntPtr.Zero);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int oldHover = _hoverTabId;
            _hoverTabId = -1;
            if (e.X < SIDEBAR_W && e.Y > 58)
            {
                _hoverTabId = HitTestNavItem(e.Location);
            }
            if (_hoverTabId != oldHover)
            {
                Invalidate(new Rectangle(0, 58, SIDEBAR_W, this.Height - 58));
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }

        private int HitTestNavItem(Point pt)
        {
            int curY = 68;
            string lastGroup = null;

            foreach (var item in _allNavItems)
            {
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    if (!item.Title.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()))
                    {
                        continue;
                    }
                }

                if (item.Group != lastGroup)
                {
                    lastGroup = item.Group;
                    curY += 28; // 分组标题高
                }

                Rectangle r = new Rectangle(16, curY, SIDEBAR_W - 32, 38);
                if (r.Contains(pt))
                {
                    return item.Id;
                }
                curY += 40;
            }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 1. 左侧侧边栏背景 (#161619)
            using (SolidBrush sideBrush = new SolidBrush(ColSidebarBg))
            {
                g.FillRectangle(sideBrush, 0, 0, SIDEBAR_W, this.Height);
            }

            // 2. 外部微妙细圆角边框 (#26262B)
            using (Pen borderPen = new Pen(ColCardBorder, 1f))
            {
                MainForm.DrawRoundedRectangle(g, borderPen, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 14);
            }

            // 3. 搜索框胶囊背景 [ Q 搜索设置项 ]
            int searchCapsuleW = SIDEBAR_W - 32;
            Rectangle searchCapsule = new Rectangle(16, 18, searchCapsuleW, 34);
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(30, 30, 34)))
            using (Pen sp = new Pen(ColCardBorder, 1f))
            {
                MainForm.DrawRoundedRectangle(g, sb, searchCapsule, 8);
                MainForm.DrawRoundedRectangle(g, sp, searchCapsule, 8);
            }
            WeChatIcon.Draw(g, WeChatIconType.Search, new Rectangle(28, 27, 16, 16), false, Color.FromArgb(110, 110, 124));
            if (string.IsNullOrEmpty(_searchFilter) && (_txtSidebarSearch == null || !_txtSidebarSearch.Focused))
            {
                using (Font phFont = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (SolidBrush phBrush = new SolidBrush(Color.FromArgb(110, 110, 124)))
                {
                    g.DrawString("搜索设置项", phFont, phBrush, new PointF(50, 26));
                }
            }

            // 4. 左侧分类与导航选项卡列表 (对标 WorkBuddy)
            int curY = 68;
            string lastGroup = null;

            using (Font grpFont = new Font(GrokTheme.FontFamily, 9.0f, FontStyle.Bold))
            using (Font itemFont = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
            using (Font itemActiveFont = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Bold))
            using (SolidBrush grpBrush = new SolidBrush(ColGroupLabel))
            {
                foreach (var item in _allNavItems)
                {
                    if (!string.IsNullOrEmpty(_searchFilter))
                    {
                        if (!item.Title.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()))
                        {
                            continue;
                        }
                    }

                    // 分组标题 (如 "设置", "功能", "网络与系统", "关于")
                    if (item.Group != lastGroup)
                    {
                        lastGroup = item.Group;
                        g.DrawString(item.Group, grpFont, grpBrush, new PointF(24, curY + 6));
                        curY += 28;
                    }

                    Rectangle itemR = new Rectangle(16, curY, SIDEBAR_W - 32, 38);

                    if (item.Id == _activeTabId)
                    {
                        using (SolidBrush ab = new SolidBrush(ColNavActive))
                        {
                            MainForm.DrawRoundedRectangle(g, ab, itemR, 8);
                        }
                    }
                    else if (item.Id == _hoverTabId)
                    {
                        using (SolidBrush hb = new SolidBrush(ColNavHover))
                        {
                            MainForm.DrawRoundedRectangle(g, hb, itemR, 8);
                        }
                    }

                    // 图标与文字
                    Color ic = (item.Id == _activeTabId) ? Color.White : (item.Id == _hoverTabId ? Color.FromArgb(210, 210, 220) : Color.FromArgb(120, 120, 134));
                    WeChatIcon.Draw(g, item.Icon, new Rectangle(itemR.X + 14, itemR.Y + 11, 16, 16), false, ic);

                    Color tc = (item.Id == _activeTabId) ? Color.White : (item.Id == _hoverTabId ? Color.FromArgb(230, 230, 238) : Color.FromArgb(150, 150, 164));
                    Font tf = (item.Id == _activeTabId) ? itemActiveFont : itemFont;
                    using (SolidBrush tb = new SolidBrush(tc))
                    {
                        g.DrawString(item.Title, tf, tb, new PointF(itemR.X + 42, itemR.Y + (38 - tf.GetHeight(g)) / 2f));
                    }

                    curY += 40;
                }
            }


        }

        #endregion
    }
}
