using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    #region Custom WeChat Chat View Control

    /// <summary>
    /// Grok / Bot 极简暗黑风格聊天消息自绘流式视图控件
    /// 支持高饱和魅紫自发气泡 (#7946D7)、深灰无头像紧凑堆叠卡片 (#202020)、行内代码胶囊与文件卡片交互
    /// </summary>
    public class WeChatChatView : UserControl
    {
        public Action<ChatMessage, string> OnFileActionClick;

        private List<ChatMessage> _messages = new List<ChatMessage>();
        private readonly VScrollBar _vScrollBar;
        private int _totalHeight = 0;
        private readonly List<MessageRenderItem> _renderItems = new List<MessageRenderItem>();
        private bool _showGroupSenderName = false;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool ShowGroupSenderName
        {
            get { return _showGroupSenderName; }
            set { _showGroupSenderName = value; RecalculateLayout(false); Invalidate(); }
        }

        private class MessageRenderItem
        {
            public ChatMessage Message;
            public Rectangle Bounds;
            public Rectangle BubbleBounds;
            public Rectangle FileOpenBounds;
            public Rectangle FileFolderBounds;
            public bool ShowTimeHeader;
            public bool ShowSenderHeader;
        }

        public WeChatChatView()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = Color.FromArgb(18, 18, 18); // Grok Main Chat #121212
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
            int change = (e.Delta / 120) * -40;
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

            // 自适应居中内容流：大屏/最大化限制在 840px 舒适阅读宽度，两端留出充裕且对等的留白空间
            int maxContentW = 840;
            int streamW = Math.Min(maxContentW, viewWidth - 56);
            if (streamW < 240) streamW = Math.Max(120, viewWidth - 24);
            int streamLeft = (viewWidth - streamW) / 2;
            int streamRight = streamLeft + streamW;

            int currentY = 16;
            DateTime lastTime = DateTime.MinValue;
            string lastSenderKey = null;

            using (Graphics g = this.CreateGraphics())
            using (Font timeFont = new Font("Microsoft YaHei", 9f))
            using (Font msgFont = new Font("Microsoft YaHei", 9.5f))
            {
                for (int i = 0; i < _messages.Count; i++)
                {
                    var msg = _messages[i];
                    bool showTime = false;
                    if ((msg.Timestamp - lastTime).TotalMinutes > 3)
                    {
                        lastTime = msg.Timestamp;
                        showTime = true;
                        currentY += 36;
                    }

                    if (msg.IsSystem)
                    {
                        SizeF sz = g.MeasureString(msg.Content, timeFont, streamW - 40);
                        int h = (int)Math.Ceiling(sz.Height) + 12;
                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, h),
                            BubbleBounds = new Rectangle(streamLeft, currentY, streamW, h),
                            ShowTimeHeader = showTime
                        };
                        _renderItems.Add(r);
                        currentY += h + 8;
                        lastSenderKey = "SYS";
                    }
                    else if (msg.IsFile)
                    {
                        int cardW = Math.Min(streamW, 280);
                        int cardH = 78;
                        int x = msg.IsSelf ? (streamRight - cardW) : streamLeft;
                        bool showSender = !msg.IsSelf && (_showGroupSenderName || msg.IsBroadcast) && lastSenderKey != ("PEER:" + msg.SenderNick);
                        int topOffset = showSender ? 20 : 2;

                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, cardH + topOffset + 6),
                            BubbleBounds = new Rectangle(x, currentY + topOffset, cardW, cardH),
                            FileOpenBounds = new Rectangle(x + 14, currentY + topOffset + 50, 72, 20),
                            FileFolderBounds = new Rectangle(x + 94, currentY + topOffset + 50, 80, 20),
                            ShowTimeHeader = showTime,
                            ShowSenderHeader = showSender
                        };
                        _renderItems.Add(r);
                        currentY += cardH + topOffset + 8;
                        lastSenderKey = msg.IsSelf ? "SELF" : ("PEER:" + msg.SenderNick);
                    }
                    else
                    {
                        // 气泡最大宽度占居中流的 80%，避免短句过度拉伸，长句保留优雅折行
                        int maxTextW = Math.Max(180, (int)(streamW * 0.80));
                        string raw = msg.Content ?? "";
                        string cleanContent = raw.Replace("`", "  ");
                        SizeF sz = g.MeasureString(cleanContent, msgFont, maxTextW);
                        int extraH = (raw.IndexOf('`') >= 0 || raw.IndexOf('\n') >= 0) ? 22 : 0;
                        int bubbleW = Math.Max(52, (int)Math.Ceiling(sz.Width) + 36);
                        if (bubbleW > streamW) bubbleW = streamW;
                        int bubbleH = Math.Max(38, (int)Math.Ceiling(sz.Height) + 20 + extraH);

                        int x = msg.IsSelf ? (streamRight - bubbleW) : streamLeft;
                        string curKey = msg.IsSelf ? "SELF" : ("PEER:" + msg.SenderNick);
                        bool isConsecutive = (curKey == lastSenderKey) && !showTime;
                        bool showSender = !msg.IsSelf && (_showGroupSenderName || msg.IsBroadcast) && !isConsecutive;

                        if (!isConsecutive && i > 0 && !showTime)
                        {
                            currentY += 10;
                        }

                        int topOffset = showSender ? 20 : 0;
                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, bubbleH + topOffset + 4),
                            BubbleBounds = new Rectangle(x, currentY + topOffset, bubbleW, bubbleH),
                            ShowTimeHeader = showTime,
                            ShowSenderHeader = showSender
                        };
                        _renderItems.Add(r);
                        // 紧凑堆叠间距仅 6px，对标 Grok Bot
                        currentY += bubbleH + topOffset + 6;
                        lastSenderKey = curKey;
                    }
                }
            }

            _totalHeight = currentY + 24;

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
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int scrollY = _vScrollBar.Visible ? _vScrollBar.Value : 0;
            int viewWidth = this.Width - (_vScrollBar.Visible ? _vScrollBar.Width : 0);
            if (viewWidth < 200) viewWidth = 200;

            int maxContentW = 840;
            int streamW = Math.Min(maxContentW, viewWidth - 48);
            if (streamW < 240) streamW = Math.Max(120, viewWidth - 24);
            int streamLeft = (viewWidth - streamW) / 2;

            using (Font timeFont = new Font("Microsoft YaHei", 8.5f))
            using (Font msgFont = new Font("Microsoft YaHei", 9.5f))
            using (Font codeFont = new Font("Consolas", 9f))
            using (Font nickFont = new Font("Microsoft YaHei", 8f, FontStyle.Bold))
            using (SolidBrush textLight = new SolidBrush(Color.FromArgb(236, 236, 240)))
            using (SolidBrush selfTextBrush = new SolidBrush(Color.FromArgb(250, 248, 255)))
            using (SolidBrush grayText = new SolidBrush(Color.FromArgb(138, 138, 142)))
            using (SolidBrush nickBrush = new SolidBrush(Color.FromArgb(164, 132, 245)))
            using (SolidBrush otherBubbleBrush = new SolidBrush(Color.FromArgb(31, 31, 33)))   // Grok Dark Card #1F1F21 / #202020
            using (Pen otherBubbleBorder = new Pen(Color.FromArgb(42, 42, 45), 1f))
            using (SolidBrush selfBubbleBrush = new SolidBrush(Color.FromArgb(121, 70, 215)))  // Grok Purple Bubble #7946D7
            {
                foreach (var item in _renderItems)
                {
                    int itemTop = item.Bounds.Top - scrollY;
                    int itemBottom = item.Bounds.Bottom - scrollY;

                    if (itemBottom < -20 || itemTop > this.Height + 20) continue;

                    ChatMessage msg = item.Message;

                    // 居中时间标签（如 "今天 5:51"），对齐居中阅读流
                    if (item.ShowTimeHeader)
                    {
                        string timeStr = FormatGrokTimestamp(msg.Timestamp);
                        SizeF tsz = g.MeasureString(timeStr, timeFont);
                        float tx = streamLeft + (streamW - tsz.Width) / 2f;
                        g.DrawString(timeStr, timeFont, grayText, tx, itemTop - 24);
                    }

                    if (msg.IsSystem)
                    {
                        SizeF sz = g.MeasureString(msg.Content, timeFont, streamW - 40);
                        float sx = streamLeft + (streamW - sz.Width) / 2f;
                        g.DrawString(msg.Content, timeFont, grayText, new RectangleF(sx, itemTop + 2, sz.Width + 4, sz.Height + 4));
                    }
                    else if (msg.IsFile)
                    {
                        Rectangle bb = new Rectangle(item.BubbleBounds.X, item.BubbleBounds.Y - scrollY, item.BubbleBounds.Width, item.BubbleBounds.Height);
                        if (item.ShowSenderHeader)
                        {
                            g.DrawString(msg.SenderNick ?? "节点", nickFont, nickBrush, bb.Left + 4, bb.Top - 16);
                        }

                        MainForm.DrawRoundedRectangle(g, otherBubbleBrush, bb, 12);
                        MainForm.DrawRoundedRectangle(g, otherBubbleBorder, bb, 12);

                        // 紫色文件图标徽章
                        Rectangle badgeRect = new Rectangle(bb.Left + 12, bb.Top + 12, 32, 32);
                        using (SolidBrush badgeBg = new SolidBrush(Color.FromArgb(121, 70, 215)))
                        {
                            MainForm.DrawRoundedRectangle(g, badgeBg, badgeRect, 8);
                        }
                        StringFormat bsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        using (Font ff = new Font("Microsoft YaHei", 7.5f, FontStyle.Bold))
                        {
                            g.DrawString("FILE", ff, selfTextBrush, badgeRect, bsf);
                        }

                        string nameSnippet = msg.FileName ?? "未知文件";
                        if (nameSnippet.Length > 17) nameSnippet = nameSnippet.Substring(0, 16) + "...";
                        using (Font fnFont = new Font("Microsoft YaHei", 9f, FontStyle.Bold))
                        {
                            g.DrawString(nameSnippet, fnFont, textLight, bb.Left + 52, bb.Top + 10);
                        }
                        g.DrawString(FormatFileSize(msg.FileSize), timeFont, grayText, bb.Left + 52, bb.Top + 28);

                        Rectangle bOpen = new Rectangle(item.FileOpenBounds.X, item.FileOpenBounds.Y - scrollY, item.FileOpenBounds.Width, item.FileOpenBounds.Height);
                        Rectangle bFolder = new Rectangle(item.FileFolderBounds.X, item.FileFolderBounds.Y - scrollY, item.FileFolderBounds.Width, item.FileFolderBounds.Height);

                        using (SolidBrush linkBrush = new SolidBrush(Color.FromArgb(168, 136, 250)))
                        {
                            g.DrawString("打开文件", timeFont, linkBrush, bOpen);
                            g.DrawString("打开目录", timeFont, linkBrush, bFolder);
                        }
                    }
                    else
                    {
                        Rectangle bb = new Rectangle(item.BubbleBounds.X, item.BubbleBounds.Y - scrollY, item.BubbleBounds.Width, item.BubbleBounds.Height);

                        if (msg.IsSelf)
                        {
                            // 右侧高饱和紫色圆角气泡 (#7946D7)，14px 大圆角，无头像与尖角，自适应居中流
                            MainForm.DrawRoundedRectangle(g, selfBubbleBrush, bb, 14);
                            Rectangle textRect = new Rectangle(bb.Left + 16, bb.Top + 10, bb.Width - 32, bb.Height - 20);
                            DrawStyledBubbleText(g, msg.Content ?? "", msgFont, codeFont, selfTextBrush, textRect, true);
                        }
                        else
                        {
                            if (item.ShowSenderHeader)
                            {
                                g.DrawString(msg.SenderNick ?? "好友", nickFont, nickBrush, bb.Left + 4, bb.Top - 18);
                            }

                            // 左侧深灰圆角卡片 (#1F1F22)，14px 大圆角，微光边框，无头像与尖角
                            MainForm.DrawRoundedRectangle(g, otherBubbleBrush, bb, 14);
                            MainForm.DrawRoundedRectangle(g, otherBubbleBorder, bb, 14);

                            Rectangle textRect = new Rectangle(bb.Left + 16, bb.Top + 10, bb.Width - 32, bb.Height - 20);
                            DrawStyledBubbleText(g, msg.Content ?? "", msgFont, codeFont, textLight, textRect, false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 渲染气泡文本：支持截图同款行内反引号代码胶囊 (`...`)、路径下划线高亮与圆点列表 (•)
        /// </summary>
        private void DrawStyledBubbleText(Graphics g, string content, Font msgFont, Font codeFont, Brush mainBrush, Rectangle textRect, bool isSelf)
        {
            // 若不包含反引号或特殊路径，直接走原生高质量自动换行绘制
            if (content.IndexOf('`') < 0 && content.IndexOf('@') < 0)
            {
                g.DrawString(content, msgFont, mainBrush, textRect);
                return;
            }

            // 按行解析，支持反引号代码胶囊背景与 @路径 下划线（复刻截图效果）
            string[] lines = content.Replace("\r\n", "\n").Split('\n');
            float curY = textRect.Top;
            float lineHeight = msgFont.GetHeight(g) + 3f;

            using (SolidBrush codeBg = new SolidBrush(isSelf ? Color.FromArgb(98, 52, 185) : Color.FromArgb(42, 42, 46)))
            using (Pen codeBorder = new Pen(isSelf ? Color.FromArgb(145, 98, 230) : Color.FromArgb(58, 58, 64), 1f))
            using (SolidBrush codeFg = new SolidBrush(Color.FromArgb(228, 228, 235)))
            using (Pen underlinePen = new Pen(Color.FromArgb(220, 205, 255), 1f))
            {
                foreach (string rawLine in lines)
                {
                    string line = rawLine;
                    float curX = textRect.Left;

                    // 处理列表圆点前缀 "- " 或 "• "
                    if (!isSelf && (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("• ")))
                    {
                        using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(130, 130, 138)))
                        {
                            g.FillEllipse(dotBrush, curX + 2, curY + 7, 4, 4);
                        }
                        curX += 14;
                        line = line.Substring(2);
                    }

                    int idx = 0;
                    while (idx < line.Length)
                    {
                        int tickStart = line.IndexOf('`', idx);
                        if (tickStart >= 0)
                        {
                            int tickEnd = line.IndexOf('`', tickStart + 1);
                            if (tickEnd > tickStart)
                            {
                                // 先画反引号前的普通文字
                                if (tickStart > idx)
                                {
                                    string pre = line.Substring(idx, tickStart - idx);
                                    SizeF preSz = g.MeasureString(pre, msgFont, 1000, StringFormat.GenericTypographic);
                                    g.DrawString(pre, msgFont, mainBrush, curX, curY);
                                    curX += preSz.Width + 2;
                                }

                                // 画反引号内的圆角代码胶囊（如 `SessionBrief.md`）
                                string codeText = line.Substring(tickStart + 1, tickEnd - tickStart - 1);
                                SizeF codeSz = g.MeasureString(codeText, codeFont, 1000, StringFormat.GenericTypographic);
                                int cw = (int)Math.Ceiling(codeSz.Width) + 10;
                                int ch = (int)Math.Ceiling(codeSz.Height) + 3;
                                Rectangle pillRect = new Rectangle((int)curX, (int)curY + 1, cw, ch);
                                MainForm.DrawRoundedRectangle(g, codeBg, pillRect, 4);
                                MainForm.DrawRoundedRectangle(g, codeBorder, pillRect, 4);
                                g.DrawString(codeText, codeFont, codeFg, curX + 4, curY + 2);
                                curX += cw + 4;

                                idx = tickEnd + 1;
                                continue;
                            }
                        }

                        // 剩余文本（检查是否含 @路径 以绘制截图紫色气泡内的下划线）
                        string rest = line.Substring(idx);
                        int atIdx = rest.IndexOf('@');
                        if (isSelf && atIdx >= 0)
                        {
                            if (atIdx > 0)
                            {
                                string beforeAt = rest.Substring(0, atIdx);
                                SizeF bSz = g.MeasureString(beforeAt, msgFont, 1000, StringFormat.GenericTypographic);
                                g.DrawString(beforeAt, msgFont, mainBrush, curX, curY);
                                curX += bSz.Width;
                            }
                            string afterAt = rest.Substring(atIdx);
                            int spaceIdx = afterAt.IndexOf(' ');
                            string pathPart = spaceIdx > 0 ? afterAt.Substring(0, spaceIdx) : afterAt;
                            SizeF pSz = g.MeasureString(pathPart, msgFont, 1000, StringFormat.GenericTypographic);
                            g.DrawString(pathPart, msgFont, mainBrush, curX, curY);
                            g.DrawLine(underlinePen, curX + 2, curY + pSz.Height + 1, curX + pSz.Width, curY + pSz.Height + 1);
                            curX += pSz.Width + 2;

                            if (spaceIdx > 0)
                            {
                                string tail = afterAt.Substring(spaceIdx);
                                g.DrawString(tail, msgFont, mainBrush, curX, curY);
                            }
                        }
                        else
                        {
                            float availW = Math.Max(40, textRect.Right - curX + 6);
                            SizeF rSz = g.MeasureString(rest, msgFont, (int)availW);
                            g.DrawString(rest, msgFont, mainBrush, new RectangleF(curX, curY, availW, Math.Max(lineHeight, rSz.Height + 6)));
                            if (rSz.Height > lineHeight)
                            {
                                curY += (rSz.Height - lineHeight);
                            }
                        }
                        break;
                    }

                    curY += lineHeight;
                    if (curY > textRect.Bottom + 10) break;
                }
            }
        }

        private static string FormatGrokTimestamp(DateTime dt)
        {
            if (dt.Date == DateTime.Today)
            {
                return string.Format("今天 {0}:{1:D2}", dt.Hour, dt.Minute);
            }
            return dt.ToString("M月d日 H:mm");
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
