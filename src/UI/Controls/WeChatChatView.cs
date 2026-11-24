using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using VisualLine = LanChat.ChatTextLayout.VisualLine;

namespace LanChat
{
    #region Custom WeChat Chat View Control

    /// <summary>
    /// Grok / Bot 极简暗黑风格聊天消息自绘流式视图控件
    /// 支持高饱和魅紫自发气泡 (#7946D7)、深灰无头像紧凑堆叠卡片 (#202020)、行内代码胶囊与文件卡片交互
    /// 完整支持鼠标划选、文本高亮、Ctrl+C 复制与右键快捷菜单复制
    /// </summary>
    public class WeChatChatView : UserControl
    {
        /// <summary>文件卡片操作按钮（打开文件/定位所在目录）点击事件通知</summary>
        public Action<ChatMessage, string> OnFileActionClick;

        private List<ChatMessage> _messages = new List<ChatMessage>();
        private int _totalHeight = 0;
        private int _scrollY = 0;
        private int _maxScroll = 0;
        private bool _isThumbHover = false;
        private bool _isThumbDragging = false;
        private int _dragStartY = 0;
        private int _dragStartScrollY = 0;
        private readonly List<MessageRenderItem> _renderItems = new List<MessageRenderItem>();
        private bool _showGroupSenderName = false;

        // 鼠标选中文本与复制支持
        private MessageRenderItem _selectedItem = null;
        private int _selStart = 0;
        private int _selEnd = 0;
        private int _selAnchor = 0;
        private bool _isSelecting = false;

        /// <summary>
        /// 获取或设置是否在群聊非本人发送气泡上方绘制发件人彩色昵称
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool ShowGroupSenderName
        {
            get { return _showGroupSenderName; }
            set
            {
                if (_showGroupSenderName == value) return;
                _showGroupSenderName = value;
            }
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
            public List<VisualLine> Lines;
        }

        public WeChatChatView()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = GrokTheme.Level05;
            this.AutoScroll = false;
            this.TabStop = true;



            this.MouseWheel += WeChatChatView_MouseWheel;
            this.MouseDown += WeChatChatView_MouseDown;
            this.MouseMove += WeChatChatView_MouseMove;
            this.MouseUp += WeChatChatView_MouseUp;
            this.MouseDoubleClick += WeChatChatView_MouseDoubleClick;
            this.Resize += (s, e) => { RecalculateLayout(true); Invalidate(); };
        }

        /// <summary>
        /// 获取当前生效的聊天消息排版字体（结合用户设置的字体家族与字号大小）
        /// </summary>
        /// <returns>构建的 Font 实例</returns>
        public Font GetMsgFont()
        {
            string fam = !string.IsNullOrEmpty(AppSettings.ChatFontFamily) ? AppSettings.ChatFontFamily : "Microsoft YaHei UI";
            float sz = AppSettings.ChatFontSize >= 8f ? AppSettings.ChatFontSize : 10.5f;
            try
            {
                return new Font(fam, sz, FontStyle.Regular);
            }
            catch
            {
                return new Font("Microsoft YaHei UI", 10.5f, FontStyle.Regular);
            }
        }

        /// <summary>
        /// 响应全局字体设置变更：重新排版气泡折行并触发重绘
        /// </summary>
        public void ApplyFontSettings()
        {
            RecalculateLayout(_scrollY >= _maxScroll);
            Invalidate();
        }

        /// <summary>
        /// 全量加载并切换当前视口展示的消息列表，自动滚动聚焦到底部最新消息
        /// </summary>
        /// <param name="list">消息列表集合</param>
        public void SetMessages(List<ChatMessage> list)
        {
            _messages = list ?? new List<ChatMessage>();
            _selectedItem = null;
            _selStart = 0;
            _selEnd = 0;
            RecalculateLayout(true);
            Invalidate();
        }

        /// <summary>清空当前视口中的所有消息气泡</summary>
        public void ClearMessages()
        {
            SetMessages(new List<ChatMessage>());
        }

        /// <summary>
        /// 在视口尾部追加单条新消息，并平滑滚屏至视口底部
        /// </summary>
        /// <param name="msg">新到达或刚发送的消息</param>
        public void AppendMessage(ChatMessage msg)
        {
            if (_messages != null && !_messages.Contains(msg))
            {
                _messages.Add(msg);
            }
            RecalculateLayout(true);
            Invalidate();
        }

        /// <summary>传输进度只重绘对应文件气泡，保留滚动位置和文本选区。</summary>
        internal void RefreshFileMessage(ChatMessage message)
        {
            foreach (var item in _renderItems)
            {
                if (item.Message != message) continue;
                var bounds = item.BubbleBounds;
                bounds.Offset(0, -_scrollY);
                bounds.Inflate(1, 1);
                Invalidate(bounds);
                return;
            }
        }

        private void WeChatChatView_MouseWheel(object sender, MouseEventArgs e)
        {
            if (_maxScroll <= 0 || this.Height <= 0) return;
            int change = (e.Delta > 0) ? -60 : 60;
            int newVal = _scrollY + change;
            if (newVal < 0) newVal = 0;
            if (newVal > _maxScroll) newVal = _maxScroll;
            if (newVal != _scrollY)
            {
                _scrollY = newVal;
                Invalidate();
            }
        }

        private void WeChatChatView_MouseDown(object sender, MouseEventArgs e)
        {
            if (_maxScroll > 0 && e.X >= this.Width - 12)
            {
                _isThumbDragging = true;
                _dragStartY = e.Y;
                _dragStartScrollY = _scrollY;
                Invalidate();
                return;
            }
            Point pt = new Point(e.X, e.Y + _scrollY);
            this.Focus();

            if (e.Button == MouseButtons.Left)
            {
                // 1. 检查是否点击文件操作按钮
                foreach (var item in _renderItems)
                {
                    if (item.Message.IsFile && item.Message.TransferProgress == null)
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

                // 2. 检查是否点击在消息气泡内开始划选
                MessageRenderItem hitItem = null;
                foreach (var item in _renderItems)
                {
                    if (!item.Message.IsFile && item.BubbleBounds.Contains(pt))
                    {
                        hitItem = item;
                        break;
                    }
                }

                if (hitItem != null)
                {
                    using (Graphics g = this.CreateGraphics())
                    using (Font msgFont = GetMsgFont())
                    {
                        int charIdx = GetCharIndexAtPoint(hitItem, pt, g, msgFont);
                        _selectedItem = hitItem;
                        _selAnchor = charIdx;
                        _selStart = charIdx;
                        _selEnd = charIdx;
                        _isSelecting = true;
                    }
                    Invalidate();
                }
                else
                {
                    if (_selectedItem != null || _selEnd > _selStart)
                    {
                        _selectedItem = null;
                        _selStart = 0;
                        _selEnd = 0;
                        Invalidate();
                    }
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                // 右键弹出复制菜单
                MessageRenderItem hitItem = null;
                foreach (var item in _renderItems)
                {
                    if (!item.Message.IsFile && item.BubbleBounds.Contains(pt))
                    {
                        hitItem = item;
                        break;
                    }
                }

                if (hitItem != null)
                {
                    // 若当前未选中或右键的是另一条消息，自动全选该消息
                    if (_selectedItem != hitItem || _selEnd <= _selStart)
                    {
                        _selectedItem = hitItem;
                        _selStart = 0;
                        _selEnd = hitItem.Message.Content != null ? hitItem.Message.Content.Length : 0;
                        Invalidate();
                    }

                    ShowContextMenuForSelection(this.PointToScreen(e.Location));
                }
            }
        }

        private void WeChatChatView_MouseMove(object sender, MouseEventArgs e)
        {
            bool overThumb = (_maxScroll > 0 && e.X >= this.Width - 12);
            if (overThumb != _isThumbHover)
            {
                _isThumbHover = overThumb;
                Invalidate();
            }

            if (_isThumbDragging && _maxScroll > 0)
            {
                int viewH = this.Height;
                int thumbH = Math.Max(32, (int)((float)viewH / _totalHeight * viewH));
                int availTrack = Math.Max(1, viewH - thumbH);
                int dy = e.Y - _dragStartY;
                int deltaScroll = (int)((float)dy / availTrack * _maxScroll);
                int target = _dragStartScrollY + deltaScroll;
                if (target < 0) target = 0;
                if (target > _maxScroll) target = _maxScroll;
                if (target != _scrollY)
                {
                    _scrollY = target;
                    Invalidate();
                }
                return;
            }

            Point pt = new Point(e.X, e.Y + _scrollY);

            if (_isSelecting && _selectedItem != null)
            {
                using (Graphics g = this.CreateGraphics())
                using (Font msgFont = GetMsgFont())
                {
                    int curChar = GetCharIndexAtPoint(_selectedItem, pt, g, msgFont);
                    _selStart = Math.Min(_selAnchor, curChar);
                    _selEnd = Math.Max(_selAnchor, curChar);
                }
                this.Cursor = Cursors.IBeam;
                Invalidate();
                return;
            }

            // 更新鼠标光标：文本气泡上显示 IBeam，文件按钮显示 Hand
            bool overBubble = false;
            foreach (var item in _renderItems)
            {
                if (item.Message.IsFile)
                {
                    if (item.Message.TransferProgress == null && (item.FileOpenBounds.Contains(pt) || item.FileFolderBounds.Contains(pt)))
                    {
                        this.Cursor = Cursors.Hand;
                        return;
                    }
                }
                else if (item.BubbleBounds.Contains(pt))
                {
                    overBubble = true;
                    break;
                }
            }
            this.Cursor = overBubble ? Cursors.IBeam : Cursors.Default;
        }

        private void WeChatChatView_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isThumbDragging)
            {
                _isThumbDragging = false;
                Invalidate();
            }
            if (e.Button == MouseButtons.Left)
            {
                _isSelecting = false;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isThumbHover)
            {
                _isThumbHover = false;
                Invalidate();
            }
        }

        private void WeChatChatView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Point pt = new Point(e.X, e.Y + _scrollY);
            foreach (var item in _renderItems)
            {
                if (!item.Message.IsFile && item.BubbleBounds.Contains(pt))
                {
                    _selectedItem = item;
                    _selStart = 0;
                    _selEnd = item.Message.Content != null ? item.Message.Content.Length : 0;
                    _selAnchor = 0;
                    Invalidate();
                    break;
                }
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if ((keyData & Keys.Control) == Keys.Control && (keyData & Keys.KeyCode) == Keys.C) return true;
            if ((keyData & Keys.Control) == Keys.Control && (keyData & Keys.KeyCode) == Keys.A) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelectedText();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                SelectAllCurrentMessage();
                e.Handled = true;
            }
        }

        private void ShowContextMenuForSelection(Point screenPt)
        {
            if (_selectedItem == null || _selectedItem.Message == null) return;

            GrokPopupMenu menu = new GrokPopupMenu();
            menu.AddItem("复制", WeChatIconType.Copy, () => CopySelectedText(), "Ctrl+C")
                .AddItem("复制全文", WeChatIconType.Copy, () => CopyFullMessage())
                .AddSeparator()
                .AddItem("全选", WeChatIconType.User, () => SelectAllCurrentMessage(), "Ctrl+A");

            menu.ShowAt(screenPt);
        }

        public void CopySelectedText()
        {
            if (_selectedItem == null || _selectedItem.Message == null) return;
            string content = _selectedItem.Message.Content ?? "";
            if (_selEnd > _selStart && _selStart >= 0 && _selEnd <= content.Length)
            {
                string sel = content.Substring(_selStart, _selEnd - _selStart);
                SafeSetClipboardText(sel);
            }
            else
            {
                SafeSetClipboardText(content);
            }
        }

        public void CopyFullMessage()
        {
            if (_selectedItem == null || _selectedItem.Message == null) return;
            SafeSetClipboardText(_selectedItem.Message.Content ?? "");
        }

        public void SelectAllCurrentMessage()
        {
            if (_selectedItem == null || _selectedItem.Message == null) return;
            _selStart = 0;
            _selEnd = (_selectedItem.Message.Content ?? "").Length;
            Invalidate();
        }

        private static void SafeSetClipboardText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
                try
                {
                    System.Threading.Thread t = new System.Threading.Thread(() => {
                        try { Clipboard.SetText(text); } catch { }
                    });
                    t.SetApartmentState(System.Threading.ApartmentState.STA);
                    t.Start();
                    t.Join(200);
                }
                catch { }
            }
        }

        private int GetCharIndexAtPoint(MessageRenderItem item, Point pt, Graphics g, Font font)
        {
            if (item == null || item.Lines == null || item.Lines.Count == 0) return 0;
            string content = item.Message.Content ?? "";
            if (content.Length == 0) return 0;

            VisualLine targetLine = null;
            if (pt.Y < item.Lines[0].Bounds.Top)
            {
                targetLine = item.Lines[0];
                return targetLine.StartChar;
            }
            if (pt.Y >= item.Lines[item.Lines.Count - 1].Bounds.Bottom)
            {
                targetLine = item.Lines[item.Lines.Count - 1];
                return targetLine.StartChar + targetLine.Length;
            }

            foreach (var line in item.Lines)
            {
                if (pt.Y >= line.Bounds.Top && pt.Y < line.Bounds.Bottom)
                {
                    targetLine = line;
                    break;
                }
            }
            if (targetLine == null) targetLine = item.Lines[item.Lines.Count - 1];

            float relX = pt.X - targetLine.Bounds.Left;
            if (relX <= 0) return targetLine.StartChar;
            if (relX >= targetLine.Bounds.Width) return targetLine.StartChar + targetLine.Length;

            int bestChar = 0;
            float minDiff = float.MaxValue;
            for (int c = 0;; c = ChatTextLayout.NextBoundary(targetLine.Text, c))
            {
                string sub = targetLine.Text.Substring(0, c);
                float w = ChatTextLayout.Measure(g, sub, font);
                float diff = Math.Abs(w - relX);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestChar = c;
                }
                if (c == targetLine.Length) break;
            }
            return targetLine.StartChar + bestChar;
        }

        private void RecalculateLayout(bool scrollToBottom)
        {
            if (this.Width <= 50 || this.Height <= 50) return;

            _renderItems.Clear();
            if (_messages == null || _messages.Count == 0)
            {
                _totalHeight = 0;
                _maxScroll = 0;
                _scrollY = 0;
                return;
            }

            int viewWidth = this.Width;
            if (viewWidth < 200) viewWidth = 200;

            int leftMargin = 28;
            int rightMargin = 28;
            int maxBubbleW = Math.Min(840, Math.Max(200, viewWidth - leftMargin - rightMargin));

            int currentY = 16;
            DateTime lastTime = DateTime.MinValue;
            string lastSenderKey = null;

            using (Graphics g = this.CreateGraphics())
            using (Font timeFont = GrokTheme.Font(GrokTheme.ChatTimestampFontSize))
            using (Font msgFont = GetMsgFont())
            {
                for (int i = 0; i < _messages.Count; i++)
                {
                    var msg = _messages[i];
                    bool showTime = false;
                    if ((msg.Timestamp - lastTime).TotalMinutes > 3)
                    {
                        lastTime = msg.Timestamp;
                        showTime = true;
                        currentY += 40;
                    }

                    if (msg.IsSystem)
                    {
                        SizeF sz = g.MeasureString(msg.Content ?? "", timeFont, maxBubbleW - 40);
                        int h = (int)Math.Ceiling(sz.Height) + 16;
                        int x = (viewWidth - (int)Math.Ceiling(sz.Width) - 40) / 2;
                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, h),
                            BubbleBounds = new Rectangle(x, currentY, (int)Math.Ceiling(sz.Width) + 40, h),
                            ShowTimeHeader = showTime
                        };
                        _renderItems.Add(r);
                        currentY += h + 10;
                        lastSenderKey = "SYS";
                    }
                    else if (msg.IsFile)
                    {
                        int cardW = Math.Min(maxBubbleW, GrokTheme.ChatFileCardWidth);
                        int cardH = GrokTheme.ChatFileCardHeight;
                        int x = msg.IsSelf ? (viewWidth - rightMargin - cardW) : leftMargin;
                        bool showSender = !msg.IsSelf && (_showGroupSenderName || msg.IsBroadcast) && lastSenderKey != ("PEER:" + msg.SenderNick);
                        int topOffset = showSender ? 22 : 2;

                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, cardH + topOffset + 8),
                            BubbleBounds = new Rectangle(x, currentY + topOffset, cardW, cardH),
                            FileOpenBounds = new Rectangle(x + 18, currentY + topOffset + 64, 80, 22),
                            FileFolderBounds = new Rectangle(x + 108, currentY + topOffset + 64, 90, 22),
                            ShowTimeHeader = showTime,
                            ShowSenderHeader = showSender
                        };
                        _renderItems.Add(r);
                        currentY += cardH + topOffset + 10;
                        lastSenderKey = msg.IsSelf ? "SELF" : ("PEER:" + msg.SenderNick);
                    }
                    else
                    {
                        string raw = msg.Content ?? "";
                        int padLeft = msg.IsSelf ? GrokTheme.ChatBubblePadSelfLeft : GrokTheme.ChatBubblePadPeerLeft;
                        int padRight = msg.IsSelf ? GrokTheme.ChatBubblePadSelfRight : GrokTheme.ChatBubblePadPeerRight;
                        int padTop = msg.IsSelf ? GrokTheme.ChatBubblePadSelfTop : GrokTheme.ChatBubblePadPeerTop;
                        int padBottom = msg.IsSelf ? GrokTheme.ChatBubblePadSelfBottom : GrokTheme.ChatBubblePadPeerBottom;

                        int padH = padLeft + padRight;
                        int padV = padTop + padBottom;
                        float availTextW = maxBubbleW - padH;

                        List<VisualLine> lines = ChatTextLayout.BreakLines(g, raw, msgFont, availTextW);

                        float maxLineWidth = 0;
                        foreach (var l in lines)
                        {
                            if (l.Bounds.Width > maxLineWidth) maxLineWidth = l.Bounds.Width;
                        }

                        float lineHeight = msgFont.GetHeight(g) + GrokTheme.ChatLineSpacingExtra;
                        float totalTextH = Math.Max(lineHeight, lines.Count * lineHeight);

                        int minBubbleH = msg.IsSelf ? 34 : 42;
                        int minBubbleW = msg.IsSelf ? 40 : 54;
                        int bubbleW = Math.Max(minBubbleW, (int)Math.Ceiling(maxLineWidth) + padH);
                        if (bubbleW > maxBubbleW) bubbleW = maxBubbleW;
                        int bubbleH = Math.Max(minBubbleH, (int)Math.Ceiling(totalTextH) + padV);

                        int x = msg.IsSelf ? (viewWidth - rightMargin - bubbleW) : leftMargin;
                        string curKey = msg.IsSelf ? "SELF" : ("PEER:" + msg.SenderNick);
                        bool isConsecutive = (curKey == lastSenderKey) && !showTime;
                        bool showSender = !msg.IsSelf && (_showGroupSenderName || msg.IsBroadcast) && !isConsecutive;

                        if (!isConsecutive && i > 0 && !showTime)
                        {
                            currentY += 12;
                        }

                        int topOffset = showSender ? 22 : 0;
                        int textLeft = x + padLeft;
                        int textTop = currentY + topOffset + padTop;

                        for (int li = 0; li < lines.Count; li++)
                        {
                            lines[li].Bounds = new RectangleF(textLeft, textTop + li * lineHeight, lines[li].Bounds.Width, lineHeight);
                        }

                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, bubbleH + topOffset + 6),
                            BubbleBounds = new Rectangle(x, currentY + topOffset, bubbleW, bubbleH),
                            ShowTimeHeader = showTime,
                            ShowSenderHeader = showSender,
                            Lines = lines
                        };
                        _renderItems.Add(r);
                        currentY += bubbleH + topOffset + 10;
                        lastSenderKey = curKey;
                    }
                }
            }

            _totalHeight = currentY + 30;
            _maxScroll = Math.Max(0, _totalHeight - this.Height);

            if (scrollToBottom)
            {
                _scrollY = _maxScroll;
            }
            else if (_scrollY > _maxScroll)
            {
                _scrollY = _maxScroll;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // 压制系统默认底色擦除，在 OnPaint 中统一全画幅双缓冲填充，避免切会话闪烁
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (SolidBrush bgBrush = new SolidBrush(this.BackColor))
            {
                g.FillRectangle(bgBrush, e.ClipRectangle);
            }

            int scrollY = _scrollY;
            int viewWidth = this.Width;
            if (viewWidth < 200) viewWidth = 200;

            using (Font timeFont = GrokTheme.Font(GrokTheme.ChatTimestampFontSize))
            using (Font msgFont = GetMsgFont())
            using (Font codeFont = new Font("Consolas", 10f))
            using (Font nickFont = GrokTheme.FontSecondaryBold)
            using (SolidBrush textLight = new SolidBrush(GrokTheme.TextPrimary))
            using (SolidBrush selfTextBrush = new SolidBrush(Color.FromArgb(250, 248, 255)))
            using (SolidBrush grayText = new SolidBrush(GrokTheme.TextSecondary))
            using (SolidBrush nickBrush = new SolidBrush(GrokTheme.AccentHot))
            using (SolidBrush otherBubbleBrush = new SolidBrush(GrokTheme.PeerBubble))
            using (Pen otherBubbleBorder = new Pen(GrokTheme.Border, 1f))
            using (SolidBrush selfBubbleBrush = new SolidBrush(GrokTheme.SessionSelected))
            using (SolidBrush selHighlightBrush = new SolidBrush(Color.FromArgb(170, 70, 115, 235))) // Text selection highlight
            {
                foreach (var item in _renderItems)
                {
                    int itemTop = item.Bounds.Top - scrollY;
                    int itemBottom = item.Bounds.Bottom - scrollY;

                    if (itemBottom < -20 || itemTop > this.Height + 20) continue;

                    ChatMessage msg = item.Message;

                    // 居中时间标签（如 "今天 5:51"）
                    if (item.ShowTimeHeader)
                    {
                        string timeStr = FormatGrokTimestamp(msg.Timestamp);
                        SizeF tsz = g.MeasureString(timeStr, timeFont);
                        float tx = (viewWidth - tsz.Width) / 2f;
                        g.DrawString(timeStr, timeFont, grayText, tx, itemTop - 28);
                    }

                    if (msg.IsSystem)
                    {
                        SizeF sz = g.MeasureString(msg.Content ?? "", timeFont, viewWidth - 60);
                        float sx = (viewWidth - sz.Width) / 2f;
                        g.DrawString(msg.Content ?? "", timeFont, grayText, new RectangleF(sx, itemTop + 2, sz.Width + 4, sz.Height + 4));
                    }
                    else if (msg.IsFile)
                    {
                        Rectangle bb = new Rectangle(item.BubbleBounds.X, item.BubbleBounds.Y - scrollY, item.BubbleBounds.Width, item.BubbleBounds.Height);
                        if (item.ShowSenderHeader)
                        {
                            g.DrawString(msg.SenderNick ?? "节点", nickFont, nickBrush, bb.Left + 6, bb.Top - 18);
                        }

                        Rectangle open = item.FileOpenBounds;
                        Rectangle folder = item.FileFolderBounds;
                        open.Offset(0, -scrollY);
                        folder.Offset(0, -scrollY);
                        ChatFileCardRenderer.Draw(g, msg, bb, open, folder);
                    }
                    else
                    {
                        Rectangle bb = new Rectangle(item.BubbleBounds.X, item.BubbleBounds.Y - scrollY, item.BubbleBounds.Width, item.BubbleBounds.Height);

                        if (msg.IsSelf)
                        {
                            // 右侧高饱和紫色圆角气泡 (#7946D7)
                            int selfRadius = Math.Min(18, bb.Height / 2);
                            MainForm.DrawRoundedRectangle(g, selfBubbleBrush, bb, selfRadius);
                        }
                        else
                        {
                            if (item.ShowSenderHeader)
                            {
                                g.DrawString(msg.SenderNick ?? "好友", nickFont, nickBrush, bb.Left + 6, bb.Top - 20);
                            }

                            // 左侧深灰圆角卡片 (#1E1E21)
                            MainForm.DrawRoundedRectangle(g, otherBubbleBrush, bb, 24);
                            MainForm.DrawRoundedRectangle(g, otherBubbleBorder, bb, 24);
                        }

                        // 绘制文本行与鼠标划选高亮
                        if (item.Lines != null)
                        {
                            bool hasSelection = (_selectedItem == item && _selEnd > _selStart);

                            foreach (var line in item.Lines)
                            {
                                float lx = line.Bounds.Left;
                                float ly = line.Bounds.Top - scrollY;

                                // 1. 若处于划选范围，先在背景绘制高亮选区矩形
                                if (hasSelection)
                                {
                                    int lineStart = line.StartChar;
                                    int lineEnd = line.StartChar + line.Length;
                                    int overStart = Math.Max(lineStart, _selStart);
                                    int overEnd = Math.Min(lineEnd, _selEnd);

                                    if (overStart < overEnd)
                                    {
                                        string preText = line.Text.Substring(0, overStart - lineStart);
                                        string selPart = line.Text.Substring(overStart - lineStart, overEnd - overStart);

                                        float preW = ChatTextLayout.Measure(g, preText, msgFont);
                                        float selW = ChatTextLayout.Measure(g, selPart, msgFont);

                                        RectangleF selRect = new RectangleF(lx + preW, ly, selW, line.Bounds.Height);
                                        g.FillRectangle(selHighlightBrush, selRect);
                                    }
                                }

                                // 2. 绘制行内文字（支持反引号代码胶囊）
                                DrawLineContent(g, line.Text, lx, ly, msgFont, codeFont, msg.IsSelf ? selfTextBrush : textLight, msg.IsSelf);
                            }
                        }
                    }
                }
            }
        }

        private void DrawLineContent(Graphics g, string lineText, float curX, float curY, Font msgFont, Font codeFont, Brush mainBrush, bool isSelf)
        {
            if (string.IsNullOrEmpty(lineText)) return;

            // 无反引号代码胶囊直接高性能绘制
            if (lineText.IndexOf('`') < 0)
            {
                ChatTextLayout.Draw(g, lineText, msgFont, mainBrush, curX, curY);
                return;
            }

            // 包含代码胶囊段落
            using (SolidBrush codeBg = new SolidBrush(isSelf ? GrokTheme.ButtonPrimaryDown : GrokTheme.InputPlus))
            using (Pen codeBorder = new Pen(isSelf ? GrokTheme.Accent : GrokTheme.Surface(58, 58, 64), 1f))
            using (SolidBrush codeFg = new SolidBrush(Color.FromArgb(228, 228, 235)))
            {
                int idx = 0;
                while (idx < lineText.Length)
                {
                    int tickStart = lineText.IndexOf('`', idx);
                    if (tickStart >= 0)
                    {
                        int tickEnd = lineText.IndexOf('`', tickStart + 1);
                        if (tickEnd > tickStart)
                        {
                            if (tickStart > idx)
                            {
                                string pre = lineText.Substring(idx, tickStart - idx);
                                curX += ChatTextLayout.Draw(g, pre, msgFont, mainBrush, curX, curY);
                            }

                            string codeText = lineText.Substring(tickStart + 1, tickEnd - tickStart - 1);
                            SizeF codeSz = g.MeasureString(codeText, codeFont, 10000, StringFormat.GenericTypographic);
                            int cw = (int)Math.Ceiling(codeSz.Width) + 8;
                            int ch = (int)Math.Ceiling(codeSz.Height) + 2;
                            Rectangle pillRect = new Rectangle((int)curX, (int)curY + 1, cw, ch);
                            MainForm.DrawRoundedRectangle(g, codeBg, pillRect, 4);
                            MainForm.DrawRoundedRectangle(g, codeBorder, pillRect, 4);
                            g.DrawString(codeText, codeFont, codeFg, curX + 4, curY + 2);
                            curX += cw + 4;

                            idx = tickEnd + 1;
                            continue;
                        }
                    }

                    string rest = lineText.Substring(idx);
                    ChatTextLayout.Draw(g, rest, msgFont, mainBrush, curX, curY);
                    break;
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

    }

    #endregion
}
