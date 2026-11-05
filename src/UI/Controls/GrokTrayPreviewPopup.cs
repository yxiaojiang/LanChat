using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 托盘未读消息预览条目数据模型
    /// </summary>
    public class TrayUnreadItem
    {
        /// <summary>会话唯一键 (如广播键或节点标识)</summary>
        public string ConvKey { get; set; }
        /// <summary>显示标题 (联系人昵称或群聊名)</summary>
        public string Title { get; set; }
        /// <summary>未读消息条数</summary>
        public int UnreadCount { get; set; }
        /// <summary>最新一条消息内容摘要文本</summary>
        public string LatestSnippet { get; set; }
        /// <summary>格式化后的时间戳字符串</summary>
        public string TimeText { get; set; }
        /// <summary>是否为广播群聊</summary>
        public bool IsBroadcast { get; set; }
        /// <summary>对端节点实体信息</summary>
        public PeerInfo Peer { get; set; }
        /// <summary>头像主题色</summary>
        public Color AvatarColor { get; set; }
        /// <summary>头像吉祥物几何形状索引</summary>
        public int AvatarShape { get; set; }
    }

    /// <summary>
    /// GrokBot 风格托盘未读消息悬浮预览卡片
    /// 对标 QQ/微信 桌面端托盘闪烁时鼠标悬停展示多联系人未读消息列表与摘要
    /// </summary>
    public class GrokTrayPreviewPopup : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        private static extern bool DeleteObject(IntPtr hObject);

        private readonly List<TrayUnreadItem> _items = new List<TrayUnreadItem>();
        private int _hoverIndex = -1;
        private Action<TrayUnreadItem> _onItemClick;

        private const int CARD_WIDTH = 320;
        private const int HEADER_HEIGHT = 40;
        private const int ROW_HEIGHT = 56;
        private const int FOOTER_HEIGHT = 28;
        private const int CORNER_RADIUS = 12;

        private Timer _dismissTimer;
        private Point _anchorPos;
        private int _mouseAwayTicks = 0;

        // =========================================================================
        // 构造函数与初始化
        // =========================================================================

        /// <summary>
        /// 初始化托盘悬浮未读卡片窗体
        /// </summary>
        public GrokTrayPreviewPopup()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(28, 28, 32); // #1C1C20
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Default;
            this.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

            _dismissTimer = new Timer();
            _dismissTimer.Interval = 150;
            _dismissTimer.Tick += DismissTimer_Tick;

            this.Deactivate += (s, e) => {
                try { this.Hide(); } catch { }
            };
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                cp.ExStyle |= 0x00000080;    // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000;    // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000008;    // WS_EX_TOPMOST
                return cp;
            }
        }

        private void DismissTimer_Tick(object sender, EventArgs e)
        {
            if (!this.Visible)
            {
                _dismissTimer.Stop();
                return;
            }

            Point cursor = Cursor.Position;
            // 鼠标位于预览窗体内部
            Rectangle formBounds = this.Bounds;
            formBounds.Inflate(8, 8);
            if (formBounds.Contains(cursor))
            {
                _mouseAwayTicks = 0;
                return;
            }

            // 鼠标仍位于托盘图标附近 (半径 35px 内)
            int dx = Math.Abs(cursor.X - _anchorPos.X);
            int dy = Math.Abs(cursor.Y - _anchorPos.Y);
            if (dx <= 35 && dy <= 35)
            {
                _mouseAwayTicks = 0;
                return;
            }

            // 鼠标移开超过 300ms 自动关闭
            _mouseAwayTicks++;
            if (_mouseAwayTicks >= 2)
            {
                _dismissTimer.Stop();
                this.Hide();
            }
        }

        /// <summary>
        /// 更新未读消息列表并在托盘图标附近弹出预览卡片
        /// </summary>
        public void ShowPreview(Point trayPoint, List<TrayUnreadItem> items, Action<TrayUnreadItem> onItemClick)
        {
            if (items == null || items.Count == 0)
            {
                this.Hide();
                return;
            }

            _anchorPos = trayPoint;
            _items.Clear();
            // 最多显示 6 条，避免超出纵向屏幕
            int takeCount = Math.Min(items.Count, 6);
            for (int i = 0; i < takeCount; i++)
            {
                _items.Add(items[i]);
            }
            _onItemClick = onItemClick;
            _hoverIndex = -1;
            _mouseAwayTicks = 0;

            int totalH = HEADER_HEIGHT + (_items.Count * ROW_HEIGHT) + FOOTER_HEIGHT;
            this.Size = new Size(CARD_WIDTH, totalH);

            // 设置圆角 Region
            IntPtr rgn = CreateRoundRectRgn(0, 0, this.Width, this.Height, CORNER_RADIUS, CORNER_RADIUS);
            if (rgn != IntPtr.Zero)
            {
                this.Region = Region.FromHrgn(rgn);
                DeleteObject(rgn);
            }

            // 智能屏幕坐标计算与边界夹紧 (Multi-monitor Aware)
            Screen screen = Screen.FromPoint(trayPoint) ?? Screen.PrimaryScreen;
            Rectangle work = screen.WorkingArea;

            int targetX = trayPoint.X - (CARD_WIDTH / 2);
            int targetY = trayPoint.Y - totalH - 12;

            // 针对任务栏置顶或左/右放置的情况智能翻转
            if (targetY < work.Top + 8)
            {
                targetY = trayPoint.Y + 28;
            }

            // 边界约束
            if (targetX + CARD_WIDTH > work.Right - 8) targetX = work.Right - CARD_WIDTH - 8;
            if (targetX < work.Left + 8) targetX = work.Left + 8;
            if (targetY + totalH > work.Bottom - 8) targetY = work.Bottom - totalH - 8;
            if (targetY < work.Top + 8) targetY = work.Top + 8;

            this.Location = new Point(targetX, targetY);
            if (!this.Visible)
            {
                this.Show();
            }
            this.Invalidate();
            _dismissTimer.Start();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int oldHover = _hoverIndex;
            _hoverIndex = -1;

            if (e.Y >= HEADER_HEIGHT && e.Y < this.Height - FOOTER_HEIGHT)
            {
                int itemIdx = (e.Y - HEADER_HEIGHT) / ROW_HEIGHT;
                if (itemIdx >= 0 && itemIdx < _items.Count)
                {
                    _hoverIndex = itemIdx;
                }
            }

            if (_hoverIndex != oldHover)
            {
                this.Cursor = (_hoverIndex >= 0) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                this.Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_hoverIndex >= 0 && _hoverIndex < _items.Count)
                {
                    var item = _items[_hoverIndex];
                    this.Hide();
                    if (_onItemClick != null)
                    {
                        try { _onItemClick(item); } catch { }
                    }
                }
                else if (e.Y >= this.Height - FOOTER_HEIGHT && _items.Count > 0)
                {
                    var item = _items[0];
                    this.Hide();
                    if (_onItemClick != null)
                    {
                        try { _onItemClick(item); } catch { }
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int totalUnread = 0;
            foreach (var it in _items) totalUnread += it.UnreadCount;

            // 1. 顶部 Header
            Rectangle headerRect = new Rectangle(0, 0, this.Width, HEADER_HEIGHT);
            using (SolidBrush headerBg = new SolidBrush(Color.FromArgb(24, 24, 27))) // #18181B
            {
                g.FillRectangle(headerBg, headerRect);
            }

            // 标题图标与文本
            using (Font titleFont = new Font("Microsoft YaHei UI", 10.0f, FontStyle.Bold))
            using (SolidBrush textWhite = new SolidBrush(Color.FromArgb(242, 242, 246)))
            {
                g.DrawString("未读消息", titleFont, textWhite, new PointF(14, 10));
            }

            // 右侧总未读徽标
            string badgeText = string.Format("{0} 条未读", totalUnread);
            using (Font badgeFont = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold))
            using (SolidBrush purpleBg = new SolidBrush(Color.FromArgb(142, 100, 220))) // #8E64DC
            using (SolidBrush whiteText = new SolidBrush(Color.White))
            {
                SizeF bSize = g.MeasureString(badgeText, badgeFont);
                int bW = (int)bSize.Width + 12;
                int bH = 20;
                Rectangle bRect = new Rectangle(this.Width - bW - 14, (HEADER_HEIGHT - bH) / 2, bW, bH);
                MainForm.DrawRoundedRectangle(g, purpleBg, bRect, bH / 2);
                g.DrawString(badgeText, badgeFont, whiteText, bRect.X + 6, bRect.Y + 2);
            }

            // 分割线
            using (Pen sepPen = new Pen(Color.FromArgb(40, 40, 44), 1f))
            {
                g.DrawLine(sepPen, 0, HEADER_HEIGHT - 1, this.Width, HEADER_HEIGHT - 1);
            }

            // 2. 未读会话列表项
            int curY = HEADER_HEIGHT;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                Rectangle rowRect = new Rectangle(6, curY + 2, this.Width - 12, ROW_HEIGHT - 4);

                // 悬停背景高亮
                if (i == _hoverIndex)
                {
                    using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(40, 40, 45))) // #28282D
                    {
                        MainForm.DrawRoundedRectangle(g, hoverBg, rowRect, 8);
                    }
                }

                // 头像绘制 (36x36 位于左侧)
                int avSize = 36;
                Rectangle avRect = new Rectangle(14, curY + (ROW_HEIGHT - avSize) / 2, avSize, avSize);
                if (item.IsBroadcast)
                {
                    using (SolidBrush bBg = new SolidBrush(Color.FromArgb(142, 100, 220)))
                    {
                        g.FillEllipse(bBg, avRect);
                    }
                    Rectangle hornRect = new Rectangle(avRect.X + 8, avRect.Y + 8, 20, 20);
                    WeChatIcon.Draw(g, WeChatIconType.Chat, hornRect, false, Color.White);
                }
                else
                {
                    Color avColor = (item.AvatarColor != Color.Empty && item.AvatarColor.A > 0) ? item.AvatarColor : Color.FromArgb(246, 148, 18);
                    WeChatIcon.DrawMascotAvatarShape(g, avRect, item.AvatarShape, avColor, false, Color.FromArgb(28, 28, 32));
                }

                // 文本区域
                int textX = 58;
                int textW = this.Width - textX - 60;

                // 昵称 + 未读数量
                string name = item.Title;
                if (name.Length > 9) name = name.Substring(0, 8) + "...";

                using (Font nameFont = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold))
                using (Font pillFont = new Font("Microsoft YaHei UI", 8.0f, FontStyle.Bold))
                using (SolidBrush nameBrush = new SolidBrush(Color.FromArgb(240, 240, 244)))
                using (SolidBrush pillBrush = new SolidBrush(Color.FromArgb(142, 100, 220)))
                {
                    g.DrawString(name, nameFont, nameBrush, new PointF(textX, curY + 9));
                    SizeF nSize = g.MeasureString(name, nameFont);
                    string countText = string.Format("[{0}条]", item.UnreadCount);
                    g.DrawString(countText, pillFont, pillBrush, new PointF(textX + nSize.Width + 2, curY + 11));
                }

                // 时间
                if (!string.IsNullOrEmpty(item.TimeText))
                {
                    using (Font timeFont = new Font("Microsoft YaHei UI", 8.0f))
                    using (SolidBrush timeBrush = new SolidBrush(Color.FromArgb(115, 115, 122)))
                    {
                        g.DrawString(item.TimeText, timeFont, timeBrush, new PointF(this.Width - 52, curY + 11));
                    }
                }

                // 最新消息摘要
                string snippet = item.LatestSnippet ?? "";
                if (snippet.Length > 20) snippet = snippet.Substring(0, 19) + "...";
                using (Font snipFont = new Font("Microsoft YaHei UI", 8.5f))
                using (SolidBrush snipBrush = new SolidBrush(Color.FromArgb(142, 142, 147)))
                {
                    g.DrawString(snippet, snipFont, snipBrush, new PointF(textX, curY + 31));
                }

                // 行底极细分割线
                if (i < _items.Count - 1)
                {
                    using (Pen rowSep = new Pen(Color.FromArgb(36, 36, 40), 1f))
                    {
                        g.DrawLine(rowSep, textX, curY + ROW_HEIGHT - 1, this.Width - 14, curY + ROW_HEIGHT - 1);
                    }
                }

                curY += ROW_HEIGHT;
            }

            // 3. 底部 Footer
            Rectangle footerRect = new Rectangle(0, this.Height - FOOTER_HEIGHT, this.Width, FOOTER_HEIGHT);
            using (SolidBrush footerBg = new SolidBrush(Color.FromArgb(24, 24, 27)))
            {
                g.FillRectangle(footerBg, footerRect);
            }
            using (Pen sepPen = new Pen(Color.FromArgb(40, 40, 44), 1f))
            {
                g.DrawLine(sepPen, 0, this.Height - FOOTER_HEIGHT, this.Width, this.Height - FOOTER_HEIGHT);
            }

            using (Font footFont = new Font("Microsoft YaHei UI", 8.5f))
            using (SolidBrush footBrush = new SolidBrush(Color.FromArgb(142, 142, 147)))
            {
                string footText = "点击任意条目立即查看 ›";
                SizeF fSize = g.MeasureString(footText, footFont);
                g.DrawString(footText, footFont, footBrush, (this.Width - fSize.Width) / 2, this.Height - FOOTER_HEIGHT + 6);
            }

            // 4. 外边框 (1px 柔和暗黑微光轮廓)
            using (Pen borderPen = new Pen(Color.FromArgb(54, 54, 60), 1f))
            {
                MainForm.DrawRoundedRectangle(g, borderPen, new Rectangle(0, 0, this.Width - 1, this.Height - 1), CORNER_RADIUS);
            }
        }
    }
}
