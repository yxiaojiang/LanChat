using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// WorkBuddy 1:1 像素级对齐现代化暗黑全局设置窗口 (纯导航与视口容器)
    /// </summary>
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

        private readonly SettingsContext _context;
        private const int DIALOG_W = 1260, DIALOG_H = 820, SIDEBAR_W = 270;
        private const int CARD_W = SettingsPanelBase.CARD_W;

        private Button _btnClose;
        private TextBox _txtSidebarSearch;
        private string _searchFilter = "";
        private DoubleBufferedPanel _viewportPanel;
        private DoubleBufferedPanel _scrollContainer;
        private readonly SettingsPanelBase[] _tabPages = new SettingsPanelBase[8];

        private int _scrollOffset = 0, _maxScroll = 0;
        private bool _isThumbHover = false, _isThumbDragging = false;
        private int _dragStartY = 0, _dragStartOffset = 0;

        public class NavItem
        {
            public int Id; public string Title; public WeChatIconType Icon; public string Group;
            public NavItem(int id, string title, WeChatIconType icon, string group) { Id = id; Title = title; Icon = icon; Group = group; }
        }

        private static readonly NavItem[] _allNavItems = new NavItem[]
        {
            new NavItem(0, "通用", WeChatIconType.Settings, "设置"),
            new NavItem(1, "个人主页", WeChatIconType.User, "设置"),
            new NavItem(2, "外观", WeChatIconType.Emoji, "设置"),
            new NavItem(3, "快捷键", WeChatIconType.Menu, "设置"),
            new NavItem(4, "文件传输", WeChatIconType.Folder, "功能"),
            new NavItem(5, "聊天记录", WeChatIconType.Clear, "功能"),
            new NavItem(6, "设备连接", WeChatIconType.Contacts, "网络与系统"),
            new NavItem(7, "关于与更新", WeChatIconType.Refresh, "关于")
        };

        private int _activeTabId = 0, _hoverTabId = -1;
        private static readonly Color ColWindowBg = GrokTheme.Level05, ColSidebarBg = GrokTheme.Level0;
        private static readonly Color ColCardBorder = GrokTheme.Border, ColNavActive = GrokTheme.Level3;
        private static readonly Color ColNavHover = GrokTheme.Level2, ColGroupLabel = GrokTheme.TextPlaceholder;

        public GrokSettingsDialog(NetworkService netService, Action onCheckUpdate, Action onShowChangeNick, Action onOpenFolder, int initialTab = 0)
            : this(netService, onCheckUpdate, onShowChangeNick, onOpenFolder, initialTab, null) { }

        public GrokSettingsDialog(NetworkService netService, Action onCheckUpdate, Action onShowChangeNick, Action onOpenFolder, int initialTab, Action onFontChanged)
        {
            _context = new SettingsContext(netService, onCheckUpdate, onShowChangeNick, onOpenFolder, onFontChanged, this);
            _activeTabId = (initialTab < 0 || initialTab > 7) ? 0 : initialTab;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            int scrW = Screen.PrimaryScreen.WorkingArea.Width, scrH = Screen.PrimaryScreen.WorkingArea.Height;
            this.Size = new Size(Math.Min(DIALOG_W, scrW - 40), Math.Min(DIALOG_H, scrH - 40));
            this.BackColor = ColWindowBg;
            this.DoubleBuffered = true;
            this.Font = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular);
            BuildWindowComponents();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                cp.Style |= 0x02000000;      // WS_CLIPCHILDREN
                cp.ExStyle |= 0x02000000;    // WS_EX_COMPOSITED
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                IntPtr rgn = CreateRoundRectRgn(0, 0, this.Width + 1, this.Height + 1, 14, 14);
                if (rgn != IntPtr.Zero) this.Region = Region.FromHrgn(rgn);
                if (_txtSidebarSearch != null && _txtSidebarSearch.IsHandleCreated)
                    SendMessageCue(_txtSidebarSearch.Handle, 0x1501, 1, "搜索设置项");
            }
            catch { }
        }

        private void BuildWindowComponents()
        {
            _btnClose = new Button { Size = new Size(GrokTheme.CloseButtonSize, GrokTheme.CloseButtonSize), Location = new Point(this.Width - GrokTheme.CloseButtonMargin - GrokTheme.CloseButtonSize, GrokTheme.CloseButtonMargin), FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnClose.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool closeHover = false;
            _btnClose.MouseEnter += (s, e) => { closeHover = true; _btnClose.Invalidate(); };
            _btnClose.MouseLeave += (s, e) => { closeHover = false; _btnClose.Invalidate(); };
            _btnClose.Click += (s, e) => this.Close();
            _btnClose.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, _btnClose.Width, _btnClose.Height);
                if (closeHover) { using (SolidBrush hb = new SolidBrush(GrokTheme.Level3Hot)) GrokTheme.DrawRoundedRectangle(e.Graphics, hb, r, 6); }
                WeChatIcon.DrawDismissIcon(e.Graphics, r, closeHover ? GrokTheme.TextTitle : GrokTheme.TextSecondary);
            };
            this.Controls.Add(_btnClose);
            _btnClose.BringToFront();

            int searchCapsuleW = SIDEBAR_W - 32;
            _txtSidebarSearch = new TextBox { Location = new Point(50, 25), Size = new Size(searchCapsuleW - 46, 20), BackColor = Color.FromArgb(30, 30, 34), ForeColor = GrokTheme.TextPrimary, BorderStyle = BorderStyle.None, Font = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular) };
            _txtSidebarSearch.HandleCreated += (s, e) => { try { SendMessageCue(_txtSidebarSearch.Handle, 0x1501, 1, "搜索设置项"); } catch { } };
            _txtSidebarSearch.GotFocus += (s, e) => Invalidate(new Rectangle(16, 18, searchCapsuleW, 34));
            _txtSidebarSearch.LostFocus += (s, e) => Invalidate(new Rectangle(16, 18, searchCapsuleW, 34));
            _txtSidebarSearch.TextChanged += (s, e) => { _searchFilter = _txtSidebarSearch.Text.Trim(); Invalidate(new Rectangle(0, 0, SIDEBAR_W, this.Height)); };
            this.Controls.Add(_txtSidebarSearch);

            int vpW = this.Width - SIDEBAR_W - 74, vpH = this.Height - 44;
            _viewportPanel = new DoubleBufferedPanel { Location = new Point(SIDEBAR_W + 20, 22), Size = new Size(vpW, vpH), BackColor = ColWindowBg, AutoScroll = false };
            _viewportPanel.Paint += ViewportPanel_Paint;
            _viewportPanel.MouseDown += ViewportPanel_MouseDown;
            _viewportPanel.MouseMove += ViewportPanel_MouseMove;
            _viewportPanel.MouseUp += ViewportPanel_MouseUp;
            _viewportPanel.MouseWheel += (s, e) => HandleScrollWheel(e.Delta);

            _scrollContainer = new DoubleBufferedPanel { Location = new Point(0, 0), Size = new Size(CARD_W, 600), BackColor = ColWindowBg };
            _scrollContainer.MouseWheel += (s, e) => HandleScrollWheel(e.Delta);
            _viewportPanel.Controls.Add(_scrollContainer);
            this.Controls.Add(_viewportPanel);
            this.MouseWheel += (s, e) => HandleScrollWheel(e.Delta);

            SwitchToTab(_activeTabId);
        }

        private void HandleScrollWheel(int delta)
        {
            if (_maxScroll <= 0) return;
            int newOffset = Math.Max(0, Math.Min(_maxScroll, _scrollOffset + (delta > 0 ? -48 : 48)));
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

            if (_tabPages[targetTabId] == null)
            {
                _scrollContainer.SuspendLayout();
                SettingsPanelBase page = CreateTabPage(targetTabId);
                _tabPages[targetTabId] = page;
                _scrollContainer.Controls.Add(page);
                _scrollContainer.ResumeLayout(false);
            }

            for (int i = 0; i < 8; i++)
            {
                if (_tabPages[i] != null) _tabPages[i].Visible = (i == targetTabId);
            }

            int pageH = _tabPages[targetTabId].Height;
            _scrollContainer.Size = new Size(CARD_W, pageH);
            _maxScroll = Math.Max(0, pageH - _viewportPanel.Height);
            _viewportPanel.Invalidate();
            Invalidate(new Rectangle(0, 0, SIDEBAR_W, this.Height));
        }

        private SettingsPanelBase CreateTabPage(int tabId)
        {
            switch (tabId)
            {
                case 1: return new ProfileSettingsPanel(_context);
                case 2: return new AppearanceSettingsPanel(_context);
                case 3: return new ShortcutsSettingsPanel(_context);
                case 4: return new FileTransferSettingsPanel(_context);
                case 5: return new ChatHistorySettingsPanel(_context);
                case 6: return new NetworkDeviceSettingsPanel(_context);
                case 7: return new AboutSettingsPanel(_context);
                default: return new GeneralSettingsPanel(_context);
            }
        }

        #region 自绘浮动极简暗黑滚动条 (Zero White Scrollbar)

        private void ViewportPanel_Paint(object sender, PaintEventArgs e)
        {
            if (_maxScroll <= 0) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int viewH = _viewportPanel.Height, totalH = _scrollContainer.Height;
            int thumbH = Math.Max(32, (int)((float)viewH / totalH * viewH));
            int thumbY = (int)((float)_scrollOffset / _maxScroll * (viewH - thumbH));
            int barW = (_isThumbHover || _isThumbDragging) ? 5 : 4;
            int barX = _viewportPanel.Width - barW - 2;

            Rectangle thumbRect = new Rectangle(barX, thumbY, barW, thumbH);
            Color thumbCol = (_isThumbHover || _isThumbDragging) ? Color.FromArgb(90, 90, 100) : Color.FromArgb(52, 52, 60);
            using (SolidBrush sb = new SolidBrush(thumbCol)) MainForm.DrawRoundedRectangle(g, sb, thumbRect, barW / 2);
        }

        private void ViewportPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (_maxScroll <= 0 || e.X < _viewportPanel.Width - 10) return;
            _isThumbDragging = true;
            _dragStartY = e.Y;
            _dragStartOffset = _scrollOffset;
            _viewportPanel.Invalidate();
        }

        private void ViewportPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_maxScroll <= 0) return;
            bool hover = (e.X >= _viewportPanel.Width - 10);
            if (hover != _isThumbHover) { _isThumbHover = hover; _viewportPanel.Invalidate(); }

            if (_isThumbDragging)
            {
                int dy = e.Y - _dragStartY, viewH = _viewportPanel.Height;
                int thumbH = Math.Max(32, (int)((float)viewH / _scrollContainer.Height * viewH));
                int availTrack = Math.Max(1, viewH - thumbH);
                int target = Math.Max(0, Math.Min(_maxScroll, _dragStartOffset + (int)((float)dy / availTrack * _maxScroll)));
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
            if (_isThumbDragging) { _isThumbDragging = false; _viewportPanel.Invalidate(); }
        }

        #endregion

        #region Navigation, Sidebar & Window Paint

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (new Rectangle(16, 18, SIDEBAR_W - 32, 34).Contains(e.Location))
                _txtSidebarSearch.Focus();

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

            if (e.Y < 48 && e.X > SIDEBAR_W && e.X < this.Width - 56)
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0x00A1, (IntPtr)0x0002, IntPtr.Zero);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int oldHover = _hoverTabId;
            _hoverTabId = (e.X < SIDEBAR_W && e.Y > 58) ? HitTestNavItem(e.Location) : -1;
            if (_hoverTabId != oldHover) Invalidate(new Rectangle(0, 58, SIDEBAR_W, this.Height - 58));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) this.Close();
        }

        private int HitTestNavItem(Point pt)
        {
            int curY = 68;
            string lastGroup = null;
            for (int i = 0; i < _allNavItems.Length; i++)
            {
                var item = _allNavItems[i];
                if (!string.IsNullOrEmpty(_searchFilter) && item.Title.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (item.Group != lastGroup) { lastGroup = item.Group; curY += 28; }
                if (new Rectangle(16, curY, SIDEBAR_W - 32, 38).Contains(pt)) return item.Id;
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

            using (SolidBrush sideBrush = new SolidBrush(ColSidebarBg)) g.FillRectangle(sideBrush, 0, 0, SIDEBAR_W, this.Height);
            using (Pen borderPen = new Pen(ColCardBorder, 1f)) MainForm.DrawRoundedRectangle(g, borderPen, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 14);

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
                    g.DrawString("搜索设置项", phFont, phBrush, new PointF(50, 26));
            }

            int curY = 68;
            string lastGroup = null;
            using (Font grpFont = new Font(GrokTheme.FontFamily, 9.0f, FontStyle.Bold))
            using (Font itemFont = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
            using (Font itemActiveFont = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Bold))
            using (SolidBrush grpBrush = new SolidBrush(ColGroupLabel))
            {
                for (int i = 0; i < _allNavItems.Length; i++)
                {
                    var item = _allNavItems[i];
                    if (!string.IsNullOrEmpty(_searchFilter) && item.Title.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    if (item.Group != lastGroup)
                    {
                        lastGroup = item.Group;
                        g.DrawString(item.Group, grpFont, grpBrush, new PointF(24, curY + 6));
                        curY += 28;
                    }

                    Rectangle itemR = new Rectangle(16, curY, SIDEBAR_W - 32, 38);
                    Color itemBg = (item.Id == _activeTabId) ? ColNavActive : (item.Id == _hoverTabId ? ColNavHover : Color.Empty);
                    if (itemBg != Color.Empty)
                    {
                        using (SolidBrush b = new SolidBrush(itemBg)) MainForm.DrawRoundedRectangle(g, b, itemR, 8);
                    }

                    Color ic = (item.Id == _activeTabId) ? Color.White : (item.Id == _hoverTabId ? Color.FromArgb(210, 210, 220) : Color.FromArgb(120, 120, 134));
                    WeChatIcon.Draw(g, item.Icon, new Rectangle(itemR.X + 14, itemR.Y + 11, 16, 16), false, ic);

                    Color tc = (item.Id == _activeTabId) ? Color.White : (item.Id == _hoverTabId ? Color.FromArgb(230, 230, 238) : Color.FromArgb(150, 150, 164));
                    Font tf = (item.Id == _activeTabId) ? itemActiveFont : itemFont;
                    using (SolidBrush tb = new SolidBrush(tc))
                        g.DrawString(item.Title, tf, tb, new PointF(itemR.X + 42, itemR.Y + (38 - tf.GetHeight(g)) / 2f));

                    curY += 40;
                }
            }
        }

        #endregion
    }
}
