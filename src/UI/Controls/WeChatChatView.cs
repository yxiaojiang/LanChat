using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    #region Custom WeChat Chat View Control

    /// <summary>
    /// 微信暗黑风格聊天消息自绘流式视图控件
    /// 支持微信绿自发气泡、暗黑好友气泡、文件卡片交互、头像胶囊与防越界平滑滚动
    /// </summary>
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
                Width = 8,
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
            if (!_vScrollBar.Visible || this.Height <= 0) return;
            int change = (e.Delta / 120) * -36;
            int newVal = _vScrollBar.Value + change;
            int maxAllowed = Math.Max(0, _vScrollBar.Maximum - Math.Max(1, _vScrollBar.LargeChange) + 1);
            if (newVal > maxAllowed) newVal = maxAllowed;
            if (newVal < _vScrollBar.Minimum) newVal = _vScrollBar.Minimum;
            if (newVal > _vScrollBar.Maximum) newVal = _vScrollBar.Maximum;
            try
            {
                _vScrollBar.Value = newVal;
            }
            catch { }
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
            if (this.Width <= 50 || this.Height <= 50) return;

            _renderItems.Clear();
            if (_messages == null || _messages.Count == 0)
            {
                _totalHeight = 0;
                _vScrollBar.Visible = false;
                try { _vScrollBar.Value = 0; } catch { }
                return;
            }

            int viewWidth = this.Width - (_vScrollBar.Visible ? _vScrollBar.Width : 0);
            if (viewWidth < 200) viewWidth = 200;

            int currentY = 16;
            DateTime lastTime = DateTime.MinValue;

            using (Graphics g = this.CreateGraphics())
            using (Font timeFont = new Font("Microsoft YaHei", 8f))
            using (Font msgFont = new Font("Microsoft YaHei", 9.5f))
            {
                foreach (var msg in _messages)
                {
                    // Time header every 3 minutes
                    if ((msg.Timestamp - lastTime).TotalMinutes > 3)
                    {
                        lastTime = msg.Timestamp;
                        currentY += 24;
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
                        // File card (Width ~230px, Height ~74px)
                        int cardW = 230;
                        int cardH = 74;
                        int x = msg.IsSelf ? (viewWidth - cardW - 54) : 54;
                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, cardH + 18),
                            BubbleBounds = new Rectangle(x, currentY + 12, cardW, cardH),
                            AvatarBounds = msg.IsSelf ? new Rectangle(viewWidth - 44, currentY + 12, 34, 34) : new Rectangle(14, currentY + 12, 34, 34),
                            FileOpenBounds = new Rectangle(x + 10, currentY + 12 + 48, 64, 20),
                            FileFolderBounds = new Rectangle(x + 80, currentY + 12 + 48, 74, 20)
                        };
                        _renderItems.Add(r);
                        currentY += cardH + 24;
                    }
                    else
                    {
                        // Regular bubble message
                        int maxTextW = Math.Max(120, (int)(viewWidth * 0.65));
                        SizeF sz = g.MeasureString(msg.Content, msgFont, maxTextW);
                        int bubbleW = Math.Max(40, (int)Math.Ceiling(sz.Width) + 22);
                        int bubbleH = Math.Max(32, (int)Math.Ceiling(sz.Height) + 14);

                        int x = msg.IsSelf ? (viewWidth - bubbleW - 54) : 54;
                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, bubbleH + 18),
                            BubbleBounds = new Rectangle(x, currentY + 10, bubbleW, bubbleH),
                            AvatarBounds = msg.IsSelf ? new Rectangle(viewWidth - 44, currentY + 10, 34, 34) : new Rectangle(14, currentY + 10, 34, 34)
                        };
                        _renderItems.Add(r);
                        currentY += bubbleH + 20;
                    }
                }
            }

            _totalHeight = currentY + 20;

            if (_totalHeight > this.Height && this.Height > 0)
            {
                _vScrollBar.Visible = true;
                _vScrollBar.Minimum = 0;
                _vScrollBar.Maximum = _totalHeight;
                _vScrollBar.LargeChange = Math.Max(1, this.Height);
                if (scrollToBottom)
                {
                    int maxAllowed = Math.Max(0, _vScrollBar.Maximum - _vScrollBar.LargeChange + 1);
                    int targetVal = Math.Min(_vScrollBar.Maximum, Math.Max(_vScrollBar.Minimum, maxAllowed));
                    try
                    {
                        _vScrollBar.Value = targetVal;
                    }
                    catch { }
                }
            }
            else
            {
                _vScrollBar.Visible = false;
                try
                {
                    _vScrollBar.Value = 0;
                }
                catch { }
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
            using (SolidBrush textLight = new SolidBrush(Color.FromArgb(230, 230, 230)))
            using (SolidBrush textDark = new SolidBrush(Color.FromArgb(10, 10, 10)))
            using (SolidBrush grayText = new SolidBrush(Color.FromArgb(120, 120, 120)))
            using (SolidBrush otherBubbleBrush = new SolidBrush(Color.FromArgb(44, 44, 46))) // WeChat other bubble #2c2c2e
            using (SolidBrush selfBubbleBrush = new SolidBrush(Color.FromArgb(7, 193, 96)))    // WeChat green bubble #07c160
            {
                DateTime lastTime = DateTime.MinValue;

                foreach (var item in _renderItems)
                {
                    int itemTop = item.Bounds.Top - scrollY;
                    int itemBottom = item.Bounds.Bottom - scrollY;

                    if (itemBottom < 0 || itemTop > this.Height) continue;

                    ChatMessage msg = item.Message;

                    // Centered Time Header
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
                        // Centered system notification
                        SizeF sz = g.MeasureString(msg.Content, timeFont, viewWidth - 80);
                        float sx = (viewWidth - sz.Width) / 2f;
                        g.DrawString(msg.Content, timeFont, grayText, new RectangleF(sx, itemTop + 4, sz.Width + 4, sz.Height + 4));
                    }
                    else if (msg.IsFile)
                    {
                        // Avatar (rounded rectangle radius 4px)
                        Rectangle av = new Rectangle(item.AvatarBounds.X, item.AvatarBounds.Y - scrollY, item.AvatarBounds.Width, item.AvatarBounds.Height);
                        DrawAvatar(g, msg.SenderNick, av, msg.IsSelf);

                        // File Card (matte dark #2c2c2e)
                        Rectangle bb = new Rectangle(item.BubbleBounds.X, item.BubbleBounds.Y - scrollY, item.BubbleBounds.Width, item.BubbleBounds.Height);
                        MainForm.DrawRoundedRectangle(g, otherBubbleBrush, bb, 4);

                        // File badge icon
                        Rectangle badgeRect = new Rectangle(bb.Left + 10, bb.Top + 10, 32, 32);
                        using (SolidBrush badgeBg = new SolidBrush(Color.FromArgb(30, 58, 138))) // Deep Blue
                        {
                            MainForm.DrawRoundedRectangle(g, badgeBg, badgeRect, 4);
                        }
                        StringFormat bsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("FILE", new Font("Microsoft YaHei", 7.5f, FontStyle.Bold), textLight, badgeRect, bsf);

                        // Name & Size
                        string nameSnippet = msg.FileName ?? "未知文件";
                        if (nameSnippet.Length > 16) nameSnippet = nameSnippet.Substring(0, 15) + "...";
                        g.DrawString(nameSnippet, new Font("Microsoft YaHei", 9f, FontStyle.Bold), textLight, bb.Left + 48, bb.Top + 9);
                        g.DrawString(FormatFileSize(msg.FileSize), new Font("Microsoft YaHei", 8f), grayText, bb.Left + 48, bb.Top + 27);

                        // Action links
                        Rectangle bOpen = new Rectangle(item.FileOpenBounds.X, item.FileOpenBounds.Y - scrollY, item.FileOpenBounds.Width, item.FileOpenBounds.Height);
                        Rectangle bFolder = new Rectangle(item.FileFolderBounds.X, item.FileFolderBounds.Y - scrollY, item.FileFolderBounds.Width, item.FileFolderBounds.Height);

                        using (SolidBrush linkBrush = new SolidBrush(Color.FromArgb(7, 193, 96)))
                        {
                            g.DrawString("打开文件", timeFont, linkBrush, bOpen);
                            g.DrawString("打开目录", timeFont, linkBrush, bFolder);
                        }
                    }
                    else
                    {
                        // Avatar
                        Rectangle av = new Rectangle(item.AvatarBounds.X, item.AvatarBounds.Y - scrollY, item.AvatarBounds.Width, item.AvatarBounds.Height);
                        DrawAvatar(g, msg.SenderNick, av, msg.IsSelf);

                        // Bubble
                        Rectangle bb = new Rectangle(item.BubbleBounds.X, item.BubbleBounds.Y - scrollY, item.BubbleBounds.Width, item.BubbleBounds.Height);

                        if (msg.IsSelf)
                        {
                            // Self WeChat Green bubble (#07c160)
                            MainForm.DrawRoundedRectangle(g, selfBubbleBrush, bb, 4);

                            // Triangle pointing right towards avatar
                            Point[] tri = new Point[]
                            {
                                new Point(bb.Right, bb.Top + 10),
                                new Point(bb.Right + 5, bb.Top + 15),
                                new Point(bb.Right, bb.Top + 20)
                            };
                            g.FillPolygon(selfBubbleBrush, tri);

                            // Text inside green bubble (crisp dark text as in screenshot)
                            Rectangle textRect = new Rectangle(bb.Left + 11, bb.Top + 7, bb.Width - 22, bb.Height - 14);
                            g.DrawString(msg.Content, msgFont, textDark, textRect);
                        }
                        else
                        {
                            // Nickname on other side
                            g.DrawString(msg.SenderNick ?? "好友", nickFont, grayText, bb.Left, bb.Top - 14);

                            // Other Dark Gray bubble (#2c2c2e)
                            MainForm.DrawRoundedRectangle(g, otherBubbleBrush, bb, 4);

                            // Triangle pointing left towards avatar
                            Point[] tri = new Point[]
                            {
                                new Point(bb.Left, bb.Top + 10),
                                new Point(bb.Left - 5, bb.Top + 15),
                                new Point(bb.Left, bb.Top + 20)
                            };
                            g.FillPolygon(otherBubbleBrush, tri);

                            // Text inside other bubble
                            Rectangle textRect = new Rectangle(bb.Left + 11, bb.Top + 7, bb.Width - 22, bb.Height - 14);
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
                MainForm.DrawRoundedRectangle(g, avBrush, rect, 4);
            }

            string initial = string.IsNullOrEmpty(nick) ? (isSelf ? "我" : "友") : nick.Substring(0, 1).ToUpperInvariant();
            using (Font f = new Font("Microsoft YaHei", 10f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(initial, f, textBrush, rect, sf);
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
        }
    }

    #endregion
}
