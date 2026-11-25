using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 聊天输入框，依赖 Windows 原生富文本编辑控件及系统剪贴板。
    /// 普通文字和表情令牌始终保存在 RichTextBox 原文中，表情图片在原生控件完成
    /// 绘制后按 RichEdit 的真实字符坐标叠绘，不创建覆盖子控件，避免破坏鼠标命中。
    /// </summary>
    internal sealed class ChatInputBox : RichTextBox
    {
        private const int MaxInputLength = 32767;
        private const int PasteMessage = 0x0302;
        private const int ContextMenuMessage = 0x007B;
        private const int PaintMessage = 0x000F;
        private const int PrintMessage = 0x0317;
        private const int PrintClientMessage = 0x0318;
        private const int HorizontalScrollMessage = 0x0114;
        private const int VerticalScrollMessage = 0x0115;
        private const int MouseWheelMessage = 0x020A;
        private const int ExtendedLimitMessage = 0x0435;
        private bool _deferredPaintPosted;
        private bool _trimmingLength;

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        /// <summary>用户明确粘贴资源管理器文件列表时通知宿主。</summary>
        internal event Action<string[]> FilesPasted;
        /// <summary>用户粘贴位图时同步通知宿主，回调返回后输入框释放图片。</summary>
        internal event Action<Image> ImagePasted;

        /// <summary>初始化单行暗色富文本编辑器，保留纯文本输入契约和长度上限。</summary>
        public ChatInputBox()
        {
            BorderStyle = BorderStyle.None;
            DetectUrls = false;
            Multiline = false;
            MaxLength = MaxInputLength;
            WordWrap = false;
            ScrollBars = RichTextBoxScrollBars.None;
            AcceptsTab = false;
            AutoWordSelection = false;
            TextChanged += (sender, e) => InvalidateEmojiPaint();
            SelectionChanged += (sender, e) => InvalidateEmojiPaint();
            FontChanged += (sender, e) => InvalidateEmojiPaint();
            Resize += (sender, e) => InvalidateEmojiPaint();
        }

        /// <summary>句柄创建后再次写入 RichEdit 原生长度上限，防止单行控件默认放大到 Int32 最大值。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SendMessage(Handle, ExtendedLimitMessage, IntPtr.Zero, (IntPtr)MaxInputLength);
        }

        /// <summary>兜底裁剪 RichEdit 偶尔允许超过原生上限的追加路径，保持协议文本长度稳定。</summary>
        protected override void OnTextChanged(EventArgs e)
        {
            if (!_trimmingLength && TextLength > MaxInputLength)
            {
                int caret = Math.Min(MaxInputLength, Math.Max(0, SelectionStart));
                _trimmingLength = true;
                try
                {
                    Text = (Text ?? string.Empty).Substring(0, MaxInputLength);
                    SelectionStart = caret;
                    SelectionLength = 0;
                }
                finally { _trimmingLength = false; }
            }
            base.OnTextChanged(e);
        }

        /// <summary>返回协议与历史使用的原始文本，表情图片绘制不改变底层令牌。</summary>
        internal string GetMessageText()
        {
            return (Text ?? string.Empty).Replace("\uFFFC", string.Empty);
        }

        /// <summary>
        /// 在当前选区插入一枚内置表情。底层只写入可读令牌，图片由自身绘制流程显示。
        /// </summary>
        /// <param name="token">形如 [敲打] 的表情令牌。</param>
        internal void InsertEmoji(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            SelectedText = token;
            InvalidateEmojiPaint();
        }

        /// <summary>刷新原生 RichEdit 完成后的表情叠绘，不引入覆盖窗口。</summary>
        private void InvalidateEmojiPaint()
        {
            if (!IsDisposed && IsHandleCreated) Invalidate();
        }

        /// <summary>在指定绘图上下文中按 RichEdit 客户区坐标绘制可见表情令牌。</summary>
        /// <param name="graphics">已经由原生编辑器绘制过背景、文字和插入符的上下文。</param>
        private void DrawEmojiPaint(Graphics graphics)
        {
            string text = Text ?? string.Empty;
            if (text.Length == 0 || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int selectionStart = Math.Max(0, Math.Min(TextLength, SelectionStart));
            int selectionEnd = SelectionLength > 0
                ? Math.Min(TextLength, selectionStart + SelectionLength)
                : selectionStart;
            Rectangle visible = ClientRectangle;
            graphics.ResetClip();
            graphics.SetClip(visible);

            for (int index = 0; index < text.Length;)
            {
                int close = text.IndexOf(']', index);
                if (text[index] == '[' && close > index + 1 && close - index <= 25)
                {
                    string token = text.Substring(index, close - index + 1);
                    Bitmap image = ChatEmojiPicker.FindImage(token);
                    int end = close + 1;
                    if (image != null)
                    {
                        // 选区和插入符必须由 RichEdit 保持可见，命中令牌时让原生文字自然显示。
                        bool selected = selectionEnd > index && selectionStart < end;
                        bool caretInside = SelectionLength == 0 && selectionStart > index && selectionStart < end;
                        if (!selected && !caretInside)
                        {
                            try
                            {
                                Point start = GetPositionFromCharIndex(index);
                                Point finish = GetPositionFromCharIndex(end);
                                float tokenWidth = finish.X - start.X;
                                float lineHeight = Math.Max(1f, Font.GetHeight(graphics));
                                float imageSize = Math.Min(Math.Max(16f, lineHeight - 1f),
                                    Math.Min(tokenWidth - 2f, ClientSize.Height - 2f));
                                if (tokenWidth > 2f && imageSize >= 8f)
                                {
                                    float y = start.Y + (lineHeight - imageSize) / 2f;
                                    var tokenBounds = new RectangleF(start.X, y, tokenWidth, imageSize);
                                    if (tokenBounds.IntersectsWith(visible))
                                    {
                                        // 只擦除当前令牌的真实 RichEdit 矩形，普通文字和背景不受影响。
                                        using (var background = new SolidBrush(BackColor))
                                            graphics.FillRectangle(background, tokenBounds);
                                        float imageX = start.X + Math.Max(0f, (tokenWidth - imageSize) / 2f);
                                        graphics.DrawImage(image, new RectangleF(imageX, y, imageSize, imageSize));
                                    }
                                }
                            }
                            catch (ArgumentException) { }
                            catch (ExternalException) { }
                        }
                        index = end;
                        continue;
                    }
                }
                index++;
            }
        }

        /// <summary>在原生窗口 DC 上执行表情叠绘，使 PrintWindow 与屏幕绘制使用同一路径。</summary>
        private void DrawEmojiOnWindow()
        {
            if (!IsHandleCreated || IsDisposed) return;
            IntPtr hdc = GetDC(Handle);
            if (hdc == IntPtr.Zero) return;
            try
            {
                using (Graphics graphics = Graphics.FromHdc(hdc)) DrawEmojiPaint(graphics);
            }
            finally { ReleaseDC(Handle, hdc); }
        }

        /// <summary>把叠绘延后到 RichEdit 完成滚动后的消息尾部，避免原生擦除覆盖图片。</summary>
        private void PostDeferredEmojiPaint()
        {
            if (_deferredPaintPosted || !IsHandleCreated || IsDisposed) return;
            _deferredPaintPosted = true;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    _deferredPaintPosted = false;
                    DrawEmojiOnWindow();
                });
            }
            catch (InvalidOperationException) { _deferredPaintPosted = false; }
        }

        /// <summary>系统剪贴板提供图片或文件时接收 Ctrl+V 和 Shift+Insert。</summary>
        /// <param name="msg">当前键盘消息。</param>
        /// <param name="keyData">按键和修饰键。</param>
        /// <returns>粘贴已处理时返回 true。</returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.V) || keyData == (Keys.Shift | Keys.Insert))
            {
                if (TryPasteAttachment()) return true;
                PasteClipboardText();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>处理原生绘制、滚动和粘贴消息，保持纯文本粘贴及表情坐标同步。</summary>
        /// <param name="m">Windows 编辑控件消息。</param>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == PasteMessage)
            {
                if (!TryPasteAttachment()) PasteClipboardText();
                return;
            }
            if (m.Msg == ContextMenuMessage)
            {
                Point location = m.LParam == new IntPtr(-1) ? new Point(0, Height) : PointToClient(Cursor.Position);
                if (ShowContextMenuAt(PointToScreen(location))) return;
            }
            if (m.Msg == PaintMessage)
            {
                base.WndProc(ref m);
                DrawEmojiOnWindow();
                PostDeferredEmojiPaint();
                return;
            }
            if (m.Msg == PrintMessage || m.Msg == PrintClientMessage)
            {
                base.WndProc(ref m);
                if (m.WParam != IntPtr.Zero)
                {
                    using (Graphics graphics = Graphics.FromHdc(m.WParam)) DrawEmojiPaint(graphics);
                }
                return;
            }

            base.WndProc(ref m);
            if (m.Msg == HorizontalScrollMessage || m.Msg == VerticalScrollMessage || m.Msg == MouseWheelMessage)
            {
                InvalidateEmojiPaint();
                Update();
                DrawEmojiOnWindow();
                PostDeferredEmojiPaint();
            }
        }

        /// <summary>在屏幕坐标处显示输入区暗色菜单，供隐藏输入框的外层胶囊复用。</summary>
        /// <param name="screenPoint">菜单左上角屏幕坐标。</param>
        /// <returns>菜单已显示时返回 true。</returns>
        internal bool ShowContextMenuAt(Point screenPoint)
        {
            IDataObject data = ReadClipboard();
            bool hasText = HasClipboardText(data);
            bool hasAttachment = HasClipboardAttachment(data, hasText);
            bool hasSelection = SelectionLength > 0;
            if (!hasText && !hasAttachment && !hasSelection && TextLength == 0) return false;

            var menu = new GrokPopupMenu();
            if (hasText)
                menu.AddItem("粘贴", WeChatIconType.Copy, () => PasteClipboardText(), "Ctrl+V");
            if (hasAttachment)
                menu.AddItem("粘贴附件", WeChatIconType.Folder, () => TryPasteAttachment(), "Ctrl+V");
            if (hasText || hasAttachment) menu.AddSeparator();
            if (hasSelection)
                menu.AddItem("复制", WeChatIconType.Copy, Copy, "Ctrl+C")
                    .AddItem("剪切", WeChatIconType.Scissors, Cut, "Ctrl+X");
            if (TextLength > 0) menu.AddItem("全选", WeChatIconType.User, SelectAll, "Ctrl+A");
            menu.ShowAt(screenPoint, this);
            return true;
        }

        private bool TryPasteAttachment()
        {
            IDataObject data = ReadClipboard();
            if (data == null) return false;
            string[] paths = FilesPasted == null ? null : ReadFileDrop(data);
            if (paths != null)
            {
                FilesPasted(paths);
                return true;
            }
            // 图文并存时优先保留文字，避免 Excel/WPS 单元格被误判为截图。
            if (HasClipboardText(data)) return false;
            if (ImagePasted == null) return false;
            using (Image image = ReadClipboardImage(data))
            {
                if (image == null) return false;
                ImagePasted(image);
                return true;
            }
        }

        private bool HasClipboardAttachment()
        {
            IDataObject data = ReadClipboard();
            if (data == null) return false;
            return HasClipboardAttachment(data, HasClipboardText(data));
        }

        private static bool HasClipboardAttachment(IDataObject data, bool hasText)
        {
            if (data == null || hasText) return false;
            try
            {
                return (data.GetDataPresent(DataFormats.FileDrop, false)) ||
                    (data.GetDataPresent("PNG", false) || data.GetDataPresent(DataFormats.Bitmap));
            }
            catch (ExternalException) { return false; }
        }

        private static bool HasClipboardText(IDataObject data)
        {
            if (data == null) return false;
            try
            {
                if (!data.GetDataPresent(DataFormats.UnicodeText, false) && !data.GetDataPresent(DataFormats.Text, false))
                    return false;
                object value = data.GetDataPresent(DataFormats.UnicodeText, false)
                    ? data.GetData(DataFormats.UnicodeText, false)
                    : data.GetData(DataFormats.Text, false);
                string text = value as string;
                return !string.IsNullOrEmpty(text);
            }
            catch (ExternalException) { return false; }
        }

        /// <summary>只读取 UnicodeText/Text 并以当前输入样式插入，绝不调用 RichEdit RTF 粘贴。</summary>
        private void PasteClipboardText()
        {
            IDataObject data = ReadClipboard();
            if (data == null) return;
            try
            {
                string text = data.GetData(DataFormats.UnicodeText, false) as string ??
                    data.GetData(DataFormats.Text, false) as string;
                if (string.IsNullOrEmpty(text)) return;
                int start = SelectionStart;
                SelectedText = text;
                int inserted = Math.Max(0, Math.Min(text.Length, TextLength - start));
                if (inserted > 0)
                {
                    Select(start, inserted);
                    SelectionFont = Font;
                    SelectionColor = ForeColor;
                }
                Select(Math.Min(TextLength, start + inserted), 0);
                InvalidateEmojiPaint();
            }
            catch (ExternalException) { }
        }

        private IDataObject ReadClipboard()
        {
            if (ReadOnly) return null;
            try
            {
                return Clipboard.GetDataObject();
            }
            catch (ExternalException) { return null; }
        }

        /// <summary>优先读取保留透明度的 PNG，再尝试系统位图。调用方负责释放返回值。</summary>
        internal static Image ReadClipboardImage(IDataObject data)
        {
            if (data == null) return null;
            try
            {
                object png = data.GetData("PNG", false);
                using (var stream = png as Stream ?? (png is byte[] ? new MemoryStream((byte[])png, false) : null))
                {
                    if (stream != null)
                    {
                        if (stream.CanSeek) stream.Position = 0;
                        using (var image = Image.FromStream(stream)) return new Bitmap(image);
                    }
                }
            }
            catch (ArgumentException) { }
            catch (IOException) { }
            catch (ExternalException) { }
            try { return data.GetData(DataFormats.Bitmap) as Image; }
            catch (ExternalException) { return null; }
        }

        /// <summary>仅识别剪贴板原生文件列表格式，文字路径保留其文本含义。</summary>
        internal static string[] ReadFileDrop(IDataObject data)
        {
            try
            {
                var paths = data != null ? data.GetData(DataFormats.FileDrop, false) as string[] : null;
                return paths != null && paths.Length > 0 ? paths : null;
            }
            catch (ExternalException) { return null; }
        }
    }
}
