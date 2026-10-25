using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// Grok Bot 100% 像素级对齐悬浮弹出菜单 (完全废除原生直角 ContextMenuStrip)
    /// 特性：
    /// 1. 16px 圆角无边框窗口 + CS_DROPSHADOW 原生阴影
    /// 2. 44px 舒适行高与 8px 悬停圆角卡片反馈
    /// 3. GDI+ 精细矢量图标绘制，杜绝低质 Emoji 锯齿
    /// 4. 自动屏幕边缘探测防溢出
    /// </summary>
    public class GrokPopupMenu : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        public class MenuItemInfo
        {
            public string Text { get; set; }
            public WeChatIconType IconType { get; set; }
            public Action Action { get; set; }
            public string Hint { get; set; }
            public bool IsSeparator { get; set; }
            public bool IsDestructive { get; set; }
        }

        private readonly List<MenuItemInfo> _items = new List<MenuItemInfo>();
        private int _hoverIndex = -1;
        private const int ITEM_HEIGHT = 44;
        private const int SEPARATOR_HEIGHT = 9;
        private const int PADDING_V = 8;
        private const int PADDING_H = 8;
        private const int MENU_WIDTH = 256;

        public GrokPopupMenu()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(28, 28, 32); // #1C1C20
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Default;
            this.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular);

            this.Deactivate += (s, e) => {
                try { this.Close(); } catch { }
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                cp.ExStyle |= 0x00000080;    // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        public GrokPopupMenu AddItem(string text, WeChatIconType iconType, Action action, string hint = null, bool destructive = false)
        {
            _items.Add(new MenuItemInfo
            {
                Text = text,
                IconType = iconType,
                Action = action,
                Hint = hint,
                IsSeparator = false,
                IsDestructive = destructive
            });
            return this;
        }

        public GrokPopupMenu AddSeparator()
        {
            _items.Add(new MenuItemInfo { IsSeparator = true });
            return this;
        }

        private int CalculateTotalHeight()
        {
            int h = PADDING_V * 2;
            foreach (var item in _items)
            {
                h += item.IsSeparator ? SEPARATOR_HEIGHT : ITEM_HEIGHT;
            }
            return h;
        }

        private Rectangle GetItemBounds(int index)
        {
            int curY = PADDING_V;
            for (int i = 0; i < index; i++)
            {
                curY += _items[i].IsSeparator ? SEPARATOR_HEIGHT : ITEM_HEIGHT;
            }
            int h = _items[index].IsSeparator ? SEPARATOR_HEIGHT : ITEM_HEIGHT;
            return new Rectangle(PADDING_H, curY, this.Width - PADDING_H * 2, h);
        }

        private int GetItemIndexAt(Point pt)
        {
            int curY = PADDING_V;
            for (int i = 0; i < _items.Count; i++)
            {
                int h = _items[i].IsSeparator ? SEPARATOR_HEIGHT : ITEM_HEIGHT;
                Rectangle r = new Rectangle(0, curY, this.Width, h);
                if (r.Contains(pt))
                {
                    if (_items[i].IsSeparator) return -1;
                    return i;
                }
                curY += h;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newHover = GetItemIndexAt(e.Location);
            if (newHover != _hoverIndex)
            {
                _hoverIndex = newHover;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && _hoverIndex >= 0 && _hoverIndex < _items.Count)
            {
                MenuItemInfo item = _items[_hoverIndex];
                if (!item.IsSeparator && item.Action != null)
                {
                    this.Close();
                    try
                    {
                        item.Action();
                    }
                    catch { }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 1. 背景与 1px 微妙描边
            Rectangle fullRect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(28, 28, 32)))
            using (Pen borderPen = new Pen(Color.FromArgb(46, 46, 52), 1f))
            {
                MainForm.DrawRoundedRectangle(g, bgBrush, fullRect, 16);
                MainForm.DrawRoundedRectangle(g, borderPen, fullRect, 16);
            }

            // 2. 绘制菜单项与分割线
            int curY = PADDING_V;
            for (int i = 0; i < _items.Count; i++)
            {
                MenuItemInfo item = _items[i];
                if (item.IsSeparator)
                {
                    using (Pen sepPen = new Pen(Color.FromArgb(44, 44, 50), 1f))
                    {
                        int lineY = curY + SEPARATOR_HEIGHT / 2;
                        g.DrawLine(sepPen, PADDING_H + 8, lineY, this.Width - PADDING_H - 8, lineY);
                    }
                    curY += SEPARATOR_HEIGHT;
                    continue;
                }

                Rectangle itemRect = new Rectangle(PADDING_H, curY, this.Width - PADDING_H * 2, ITEM_HEIGHT);
                bool isHover = (i == _hoverIndex);

                // 悬停高亮圆角卡片 (#2E2E34)
                if (isHover)
                {
                    Color hoverColor = item.IsDestructive ? Color.FromArgb(60, 24, 28) : Color.FromArgb(46, 46, 54);
                    using (SolidBrush hb = new SolidBrush(hoverColor))
                    {
                        MainForm.DrawRoundedRectangle(g, hb, itemRect, 8);
                    }
                }

                // 矢量图标绘制 (18x18, 居中对齐)
                int iconSize = 18;
                Rectangle iconRect = new Rectangle(itemRect.X + 12, itemRect.Y + (ITEM_HEIGHT - iconSize) / 2, iconSize, iconSize);
                Color iconColor;
                if (item.IsDestructive)
                {
                    iconColor = isHover ? Color.FromArgb(255, 90, 100) : Color.FromArgb(235, 75, 85);
                }
                else
                {
                    iconColor = isHover ? Color.FromArgb(250, 250, 252) : Color.FromArgb(160, 160, 168);
                }
                WeChatIcon.Draw(g, item.IconType, iconRect, false, iconColor);

                // 菜单标题文字 (10.0pt, 绝对垂直居中)
                Color textColor;
                if (item.IsDestructive)
                {
                    textColor = isHover ? Color.FromArgb(255, 90, 100) : Color.FromArgb(235, 75, 85);
                }
                else
                {
                    textColor = isHover ? Color.FromArgb(255, 255, 255) : Color.FromArgb(226, 226, 232);
                }

                using (SolidBrush textBrush = new SolidBrush(textColor))
                using (StringFormat sf = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Near,
                    FormatFlags = StringFormatFlags.NoWrap
                })
                {
                    Rectangle textRect = new Rectangle(itemRect.X + 40, itemRect.Y, itemRect.Width - 50, itemRect.Height);
                    g.DrawString(item.Text, this.Font, textBrush, textRect, sf);
                }

                // 副文本 / 快捷提示 (若有)
                if (!string.IsNullOrEmpty(item.Hint))
                {
                    using (SolidBrush hintBrush = new SolidBrush(Color.FromArgb(130, 130, 140)))
                    using (Font hintFont = new Font("Microsoft YaHei UI", 9f))
                    using (StringFormat sfHint = new StringFormat
                    {
                        LineAlignment = StringAlignment.Center,
                        Alignment = StringAlignment.Far,
                        FormatFlags = StringFormatFlags.NoWrap
                    })
                    {
                        Rectangle hintRect = new Rectangle(itemRect.X, itemRect.Y, itemRect.Width - 12, itemRect.Height);
                        g.DrawString(item.Hint, hintFont, hintBrush, hintRect, sfHint);
                    }
                }

                curY += ITEM_HEIGHT;
            }
        }

        public void ShowAt(Point screenPoint)
        {
            this.Size = new Size(MENU_WIDTH, CalculateTotalHeight());

            // 屏幕边缘防溢出处理
            Screen screen = Screen.FromPoint(screenPoint);
            Rectangle workArea = screen.WorkingArea;

            int x = screenPoint.X;
            int y = screenPoint.Y;

            if (x + this.Width > workArea.Right - 8)
            {
                x = workArea.Right - this.Width - 8;
            }
            if (x < workArea.Left + 8)
            {
                x = workArea.Left + 8;
            }

            if (y + this.Height > workArea.Bottom - 8)
            {
                y = screenPoint.Y - this.Height;
                if (y < workArea.Top + 8)
                {
                    y = workArea.Bottom - this.Height - 8;
                }
            }

            this.Location = new Point(x, y);

            try
            {
                IntPtr rgn = CreateRoundRectRgn(0, 0, this.Width + 1, this.Height + 1, 16, 16);
                if (rgn != IntPtr.Zero)
                {
                    this.Region = Region.FromHrgn(rgn);
                }
            }
            catch { }

            this.Show();
        }

        public void Show(Control parent, Point clientPoint)
        {
            Point screenPt = parent != null ? parent.PointToScreen(clientPoint) : clientPoint;
            ShowAt(screenPt);
        }
    }
}
