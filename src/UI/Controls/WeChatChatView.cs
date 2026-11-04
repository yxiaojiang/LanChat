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
    /// 完整支持鼠标划选、文本高亮、Ctrl+C 复制与右键快捷菜单复制
    /// </summary>
    public class WeChatChatView : UserControl
    {
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

        private class VisualLine
        {
            public string Text;
            public int StartChar;
            public int Length;
            public RectangleF Bounds;
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
            this.BackColor = Color.FromArgb(18, 18, 18); // Grok Main Chat #121212
            this.AutoScroll = false;
            this.TabStop = true;



            this.MouseWheel += WeChatChatView_MouseWheel;
            this.MouseDown += WeChatChatView_MouseDown;
            this.MouseMove += WeChatChatView_MouseMove;
            this.MouseUp += WeChatChatView_MouseUp;
            this.MouseDoubleClick += WeChatChatView_MouseDoubleClick;
            this.Resize += (s, e) => { RecalculateLayout(true); Invalidate(); };
        }

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

        public void ApplyFontSettings()
        {
            RecalculateLayout(true);
            Invalidate();
        }

        public void SetMessages(List<ChatMessage> list)
        {
            _messages = list ?? new List<ChatMessage>();
            _selectedItem = null;
            _selStart = 0;
            _selEnd = 0;
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
                    if (item.FileOpenBounds.Contains(pt) || item.FileFolderBounds.Contains(pt))
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
            for (int c = 0; c <= targetLine.Length; c++)
            {
                string sub = targetLine.Text.Substring(0, c);
                float w = g.MeasureString(sub, font, 10000, StringFormat.GenericTypographic).Width;
                float diff = Math.Abs(w - relX);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestChar = c;
                }
            }
            return targetLine.StartChar + bestChar;
        }

        private static List<VisualLine> BreakTextIntoVisualLines(Graphics g, string content, Font font, float maxWidth, float startX, float startY)
        {
            List<VisualLine> lines = new List<VisualLine>();
            if (string.IsNullOrEmpty(content))
            {
                return lines;
            }

            float lineHeight = font.GetHeight(g) + 5f;
            float curY = startY;
            int globalIndex = 0;

            string[] paragraphs = content.Replace("\r\n", "\n").Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                string para = paragraphs[p];
                if (para.Length == 0)
                {
                    lines.Add(new VisualLine
                    {
                        Text = "",
                        StartChar = globalIndex,
                        Length = 0,
                        Bounds = new RectangleF(startX, curY, 0, lineHeight)
                    });
                    curY += lineHeight;
                    globalIndex += 1;
                    continue;
                }

                int pOffset = 0;
                while (pOffset < para.Length)
                {
                    int count = 1;
                    int bestBreak = -1;

                    while (pOffset + count <= para.Length)
                    {
                        string sub = para.Substring(pOffset, count);
                        SizeF sz = g.MeasureString(sub, font, 10000, StringFormat.GenericTypographic);
                        if (sz.Width > maxWidth && count > 1)
                        {
                            if (bestBreak > 0)
                            {
                                count = bestBreak;
                            }
                            else
                            {
                                count = count - 1;
                            }
                            break;
                        }

                        if (pOffset + count < para.Length && char.IsWhiteSpace(para[pOffset + count]))
                        {
                            bestBreak = count + 1;
                        }

                        count++;
                    }

                    if (pOffset + count > para.Length)
                    {
                        count = para.Length - pOffset;
                    }

                    string lineStr = para.Substring(pOffset, count);
                    SizeF lineSz = g.MeasureString(lineStr, font, 10000, StringFormat.GenericTypographic);

                    lines.Add(new VisualLine
                    {
                        Text = lineStr,
                        StartChar = globalIndex + pOffset,
                        Length = count,
                        Bounds = new RectangleF(startX, curY, (float)Math.Ceiling(lineSz.Width), lineHeight)
                    });

                    pOffset += count;
                    curY += lineHeight;
                }

                globalIndex += para.Length + 1;
            }

            return lines;
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
            using (Font timeFont = new Font("Microsoft YaHei UI", 9.0f))
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
                        int cardW = Math.Min(maxBubbleW, 340);
                        int cardH = 88;
                        int x = msg.IsSelf ? (viewWidth - rightMargin - cardW) : leftMargin;
                        bool showSender = !msg.IsSelf && (_showGroupSenderName || msg.IsBroadcast) && lastSenderKey != ("PEER:" + msg.SenderNick);
                        int topOffset = showSender ? 22 : 2;

                        MessageRenderItem r = new MessageRenderItem
                        {
                            Message = msg,
                            Bounds = new Rectangle(0, currentY, viewWidth, cardH + topOffset + 8),
                            BubbleBounds = new Rectangle(x, currentY + topOffset, cardW, cardH),
                            FileOpenBounds = new Rectangle(x + 18, currentY + topOffset + 56, 80, 22),
                            FileFolderBounds = new Rectangle(x + 108, currentY + topOffset + 56, 90, 22),
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
                        int padH = msg.IsSelf ? 48 : 52;
                        int padV = msg.IsSelf ? 36 : 40;
                        float availTextW = maxBubbleW - padH;

                        List<VisualLine> lines = BreakTextIntoVisualLines(g, raw, msgFont, availTextW, 0, 0);

                        float maxLineWidth = 0;
                        foreach (var l in lines)
                        {
                            if (l.Bounds.Width > maxLineWidth) maxLineWidth = l.Bounds.Width;
                        }

                        float lineHeight = msgFont.GetHeight(g) + 5f;
                        float totalTextH = Math.Max(lineHeight, lines.Count * lineHeight);

                        int bubbleW = Math.Max(64, (int)Math.Ceiling(maxLineWidth) + padH);
                        if (bubbleW > maxBubbleW) bubbleW = maxBubbleW;
                        int bubbleH = Math.Max(48, (int)Math.Ceiling(totalTextH) + padV);

                        int x = msg.IsSelf ? (viewWidth - rightMargin - bubbleW) : leftMargin;
                        string curKey = msg.IsSelf ? "SELF" : ("PEER:" + msg.SenderNick);
                        bool isConsecutive = (curKey == lastSenderKey) && !showTime;
                        bool showSender = !msg.IsSelf && (_showGroupSenderName || msg.IsBroadcast) && !isConsecutive;

                        if (!isConsecutive && i > 0 && !showTime)
                        {
                            currentY += 12;
                        }

                        int topOffset = showSender ? 22 : 0;
                        int textLeft = msg.IsSelf ? (x + 24) : (x + 26);
                        int textTop = msg.IsSelf ? (currentY + topOffset + 18) : (currentY + topOffset + 20);

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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int scrollY = _scrollY;
            int viewWidth = this.Width;
            if (viewWidth < 200) viewWidth = 200;

            using (Font timeFont = new Font("Microsoft YaHei UI", 9.0f))
            using (Font msgFont = GetMsgFont())
            using (Font codeFont = new Font("Consolas", 10f))
            using (Font nickFont = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold))
            using (SolidBrush textLight = new SolidBrush(Color.FromArgb(242, 242, 246))) // #F2F2F6
            using (SolidBrush selfTextBrush = new SolidBrush(Color.FromArgb(250, 248, 255)))
            using (SolidBrush grayText = new SolidBrush(Color.FromArgb(142, 142, 147))) // #8E8E93
            using (SolidBrush nickBrush = new SolidBrush(Color.FromArgb(164, 132, 245)))
            using (SolidBrush otherBubbleBrush = new SolidBrush(Color.FromArgb(30, 30, 33)))   // Peer bubble #1E1E21
            using (Pen otherBubbleBorder = new Pen(Color.FromArgb(44, 44, 50), 1f)) // #2C2C32
            using (SolidBrush selfBubbleBrush = new SolidBrush(Color.FromArgb(121, 70, 215)))  // Grok Purple Bubble #7946D7
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

                        MainForm.DrawRoundedRectangle(g, otherBubbleBrush, bb, 20);
                        MainForm.DrawRoundedRectangle(g, otherBubbleBorder, bb, 20);

                        // 紫色文件图标徽章
                        Rectangle badgeRect = new Rectangle(bb.Left + 16, bb.Top + 16, 36, 36);
                        using (SolidBrush badgeBg = new SolidBrush(Color.FromArgb(121, 70, 215)))
                        {
                            MainForm.DrawRoundedRectangle(g, badgeBg, badgeRect, 10);
                        }
                        WeChatIcon.Draw(g, WeChatIconType.Box, new Rectangle(badgeRect.Left + 8, badgeRect.Top + 8, 20, 20), false, Color.White);

                        using (Font fnFont = new Font("Microsoft YaHei UI", 10.0f, FontStyle.Bold))
                        {
                            string nameSnippet = msg.FileName ?? "未知文件";
                            if (nameSnippet.Length > 28) nameSnippet = nameSnippet.Substring(0, 25) + "...";
                            g.DrawString(nameSnippet, fnFont, textLight, bb.Left + 62, bb.Top + 14);
                        }
                        g.DrawString(FormatFileSize(msg.FileSize), timeFont, grayText, bb.Left + 62, bb.Top + 38);

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
                            // 右侧高饱和紫色圆角气泡 (#7946D7)
                            MainForm.DrawRoundedRectangle(g, selfBubbleBrush, bb, 24);
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

                                        float preW = preText.Length > 0 ? g.MeasureString(preText, msgFont, 10000, StringFormat.GenericTypographic).Width : 0f;
                                        float selW = g.MeasureString(selPart, msgFont, 10000, StringFormat.GenericTypographic).Width;

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
                g.DrawString(lineText, msgFont, mainBrush, new PointF(curX, curY), StringFormat.GenericTypographic);
                return;
            }

            // 包含代码胶囊段落
            using (SolidBrush codeBg = new SolidBrush(isSelf ? Color.FromArgb(98, 52, 185) : Color.FromArgb(42, 42, 46)))
            using (Pen codeBorder = new Pen(isSelf ? Color.FromArgb(145, 98, 230) : Color.FromArgb(58, 58, 64), 1f))
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
                                SizeF preSz = g.MeasureString(pre, msgFont, 10000, StringFormat.GenericTypographic);
                                g.DrawString(pre, msgFont, mainBrush, new PointF(curX, curY), StringFormat.GenericTypographic);
                                curX += preSz.Width;
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
                    g.DrawString(rest, msgFont, mainBrush, new PointF(curX, curY), StringFormat.GenericTypographic);
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
