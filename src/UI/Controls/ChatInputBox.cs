using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 聊天输入框，依赖 Windows 原生多行富文本编辑控件及系统剪贴板。
    /// 原文及交互由 RichEdit 保存，文字、表情和光标合成到同一离屏缓冲。
    /// 控件负责格式化表情占位和编辑，随窗口销毁释放闪烁定时器。
    /// </summary>
    internal sealed class ChatInputBox : RichTextBox
    {
        private const int MaxInputLength = 32767;
        private const int PasteMessage = 0x0302;
        private const int ContextMenuMessage = 0x007B;
        private const int PaintMessage = 0x000F;
        private const int EraseBackgroundMessage = 0x0014;
        private const int PrintMessage = 0x0317;
        private const int PrintClientMessage = 0x0318;
        private const int PrintClient = 0x0004;
        private const int SetRedrawMessage = 0x000B;
        private const int SetFocusMessage = 0x0007;
        private const int LeftButtonDownMessage = 0x0201;
        private const int LeftButtonUpMessage = 0x0202;
        private const int KeyDownMessage = 0x0100;
        private const int CharMessage = 0x0102;
        private const int HorizontalScrollMessage = 0x0114;
        private const int VerticalScrollMessage = 0x0115;
        private const int MouseWheelMessage = 0x020A;
        private const int ExtendedLimitMessage = 0x0435;
        private const int GetLangOptionsMessage = 0x0479;
        private const int SetLangOptionsMessage = 0x0478;
        private const int SetParaFormatMessage = 0x0447;
        private const int ImeStartCompositionMessage = 0x010D;
        private const int ImeEndCompositionMessage = 0x010E;
        private const int ImfAutoFont = 0x0002;
        private const int ImfAutoFontSizeAdjust = 0x0010;
        private const int ImfDualFont = 0x0080;
        private const int ParaFormatLinespacing = 0x00000100;
        private const byte LineSpacingAtLeast = 3;
        private const int ResizeMessage = 0x0005;
        private const int SetRectNoPaintMessage = 0x00B4;
        private const int TextPaddingTop = 10;
        private const float EmojiAdvancePadding = 2f;
        private const float EmojiMinimumSize = 16f;
        private const float EmojiVerticalInset = 4f;
        private const int EmojiScale = 2;
        // 窗口类由系统目录的 RichEdit 注册，模块在进程退出时由系统卸载。
        private static readonly IntPtr RichEditModule = LoadLibrary(Path.Combine(Environment.SystemDirectory, "msftedit.dll"));
        // 字体宽度随字号和 DPI 变化，图片透明边界只需按位图计算一次。
        private readonly Dictionary<string, float> _emojiFontSizes = new Dictionary<string, float>();
        private readonly Dictionary<Bitmap, Rectangle> _emojiContentBounds = new Dictionary<Bitmap, Rectangle>();
        private bool _trimmingLength;
        private bool _normalizingEmojiTokens;
        private bool _imeComposing;
        private bool _normalizationPending;
        private readonly Timer _caretTimer;
        private bool _caretVisible = true;
        private Rectangle _lastCaretRect = Rectangle.Empty;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeParaFormat2
        {
            public int Size;
            public int Mask;
            public short Numbering;
            public short Effects;
            public int StartIndent;
            public int RightIndent;
            public int Offset;
            public short Alignment;
            public short TabCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] Tabs;
            public int SpaceBefore;
            public int SpaceAfter;
            public int LineSpacing;
            public short Style;
            public byte LineSpacingRule;
            public byte OutlineLevel;
            public short ShadingWeight;
            public short ShadingStyle;
            public short NumberingStart;
            public short NumberingStyle;
            public short NumberingTab;
            public short BorderSpace;
            public short BorderWidth;
            public short Borders;
        }


        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, ref NativeRect lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, ref NativeParaFormat2 lParam);


        [DllImport("user32.dll")]
        private static extern bool HideCaret(IntPtr hWnd);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string fileName);

        /// <summary>用户明确粘贴资源管理器文件列表时通知宿主。</summary>
        internal event Action<string[]> FilesPasted;
        /// <summary>用户粘贴位图时同步通知宿主，回调返回后输入框释放图片。</summary>
        internal event Action<Image> ImagePasted;

        /// <summary>初始化多行暗色富文本编辑器，保留纯文本输入契约和长度上限。</summary>
        public ChatInputBox()
        {
            BorderStyle = BorderStyle.None;
            DetectUrls = false;
            Multiline = true;
            MaxLength = MaxInputLength;
            WordWrap = true;
            ScrollBars = RichTextBoxScrollBars.None;
            AcceptsTab = false;
            AutoWordSelection = false;
            // 关闭 RichEdit 自动字体/双字体回退，防止中文输入时被切换为宋体
            LanguageOption = RichTextBoxLanguageOptions.AutoKeyboard;
            _caretTimer = new Timer { Interval = 530 };
            _caretTimer.Tick += (sender, e) =>
            {
                if (!Focused || SelectionLength > 0 || ReadOnly)
                {
                    _caretVisible = false;
                    _caretTimer.Stop();
                    InvalidateLastCaretRect();
                    return;
                }
                _caretVisible = !_caretVisible;
                HideNativeCaret();
                // 光标两种状态共用缓冲绘制。失效区域只覆盖光标，避免每次闪烁重刷输入区。
                InvalidateLastCaretRect();
            };
            TextChanged += (sender, e) =>
            {
                ResetCaretBlink();
                InvalidateEmojiPaint();
            };
            SelectionChanged += (sender, e) =>
            {
                if (_normalizingEmojiTokens) return;
                ResetCaretBlink();
                InvalidateEmojiPaint();
            };
            GotFocus += (sender, e) =>
            {
                HideNativeCaret();
                ResetCaretBlink();
                InvalidateEmojiPaint();
            };
            LostFocus += (sender, e) =>
            {
                _caretTimer.Stop();
                _caretVisible = false;
                InvalidateLastCaretRect();
            };
            FontChanged += (sender, e) =>
            {
                _emojiFontSizes.Clear();
                ApplyNativeTextRect();
                RequestEmojiTokenNormalization();
                InvalidateEmojiPaint();
            };
            Resize += (sender, e) =>
            {
                ApplyNativeTextRect();
                InvalidateEmojiPaint();
            };
        }

        /// <summary>释放自定义光标闪烁定时器资源。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _caretTimer != null)
            {
                _caretTimer.Stop();
                _caretTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>选用系统 RichEdit 4.1，以支持把原生客户区绘制到输入框的离屏 DC。</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                if (RichEditModule != IntPtr.Zero) parameters.ClassName = "RICHEDIT50W";
                return parameters;
            }
        }

        /// <summary>句柄创建后再次写入 RichEdit 原生长度上限，并禁用自动字体切换以保持老版本字体一致。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SendMessage(Handle, ExtendedLimitMessage, IntPtr.Zero, (IntPtr)MaxInputLength);
            int langOptions = unchecked((int)SendMessage(Handle, GetLangOptionsMessage, IntPtr.Zero, IntPtr.Zero).ToInt64());
            langOptions &= ~(ImfAutoFont | ImfDualFont | ImfAutoFontSizeAdjust);
            SendMessage(Handle, SetLangOptionsMessage, IntPtr.Zero, (IntPtr)langOptions);
            _emojiFontSizes.Clear();
            ApplyNativeTextRect();
            RequestEmojiTokenNormalization();
        }

        /// <summary>
        /// 固定 RichEdit 的文字格式区域，给多行输入保留稳定的上、下内边距。
        /// 原生控件如果自行重算格式区域，插入表情令牌后可能让整行视觉上移到顶部。
        /// </summary>
        private void ApplyNativeTextRect()
        {
            if (!IsHandleCreated || IsDisposed || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            NativeRect rect = new NativeRect
            {
                Left = 0,
                Top = TextPaddingTop,
                Right = Math.Max(1, ClientSize.Width),
                Bottom = Math.Max(TextPaddingTop + 1, ClientSize.Height - 4)
            };
            SendMessage(Handle, SetRectNoPaintMessage, IntPtr.Zero, ref rect);
        }

        /// <summary>
        /// 根据内置表情图片调整原始令牌的内联宽度，并统一段落行距。
        /// </summary>
        private void RequestEmojiTokenNormalization()
        {
            if (_imeComposing)
            {
                _normalizationPending = true;
                return;
            }
            NormalizeEmojiTokenFormatting();
        }

        /// <summary>统一正文段落行距，并只压缩有效表情令牌，避免失效令牌继承压缩字体。</summary>
        private void NormalizeEmojiTokenFormatting()
        {
            if (RichEditModule == IntPtr.Zero || _normalizingEmojiTokens || _imeComposing ||
                !IsHandleCreated || IsDisposed || TextLength == 0) return;

            string text = Text ?? string.Empty;
            int savedStart = Math.Max(0, Math.Min(TextLength, SelectionStart));
            int savedLength = Math.Max(0, Math.Min(SelectionLength, TextLength - savedStart));
            _normalizingEmojiTokens = true;
            bool redrawDisabled = false;
            try
            {
                // 格式化期间冻结原生重绘，避免每个 Select/SelectionFont 产生中间帧和闪烁。
                SendMessage(Handle, SetRedrawMessage, IntPtr.Zero, IntPtr.Zero);
                redrawDisabled = true;

                ApplyMinimumParagraphLineSpacing();

                using (Graphics measure = CreateGraphics())
                {
                    int imageSize = GetEmojiImageSize();
                    int advancePadding = (int)Math.Ceiling(EmojiAdvancePadding * DeviceDpi / 96f);
                    int plainStart = 0;
                    for (int index = 0; index < text.Length; index++)
                    {
                        int last;
                        string token;
                        if (ReadEmoji(text, index, out last, out token) == null) continue;
                        NormalizePlainFontRange(plainStart, index - plainStart);
                        Select(index, token.Length);
                        Bitmap image = ChatEmojiPicker.FindImage(token);
                        int targetAdvance = GetEmojiDisplayWidth(image, imageSize) + advancePadding;
                        float size = GetEmojiFontSize(token, measure, targetAdvance);
                        using (var emojiFont = new Font(Font.FontFamily, size, Font.Style))
                        {
                            using (Font selectionFont = SelectionFont)
                            {
                                if (!IsSameFont(selectionFont, emojiFont)) SelectionFont = emojiFont;
                            }
                            // 不能给令牌设置上移量。RichEdit 会据此增大整行升部，使普通文字上下跳动。
                            if (SelectionCharOffset != 0) SelectionCharOffset = 0;
                        }
                        plainStart = last + 1;
                        index = last;
                    }
                    NormalizePlainFontRange(plainStart, TextLength - plainStart);
                }
            }
            finally
            {
                int restoredStart = Math.Max(0, Math.Min(TextLength, savedStart));
                Select(restoredStart, Math.Min(savedLength, TextLength - restoredStart));
                if (savedLength == 0)
                    RestorePlainFormatting();
                if (redrawDisabled)
                {
                    SendMessage(Handle, SetRedrawMessage, (IntPtr)1, IntPtr.Zero);
                }
                _normalizingEmojiTokens = false;
                if (redrawDisabled) Invalidate();
            }
        }

        /// <summary>比较字号、字体族和样式，避免无意义的格式重排。</summary>
        private static bool IsSameFont(Font value, Font target)
        {
            return value != null && string.Equals(value.FontFamily.Name, target.FontFamily.Name, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(value.SizeInPoints - target.SizeInPoints) < 0.05f && value.Style == target.Style;
        }

        /// <summary>恢复有效表情令牌之间的普通文字区间，修复编辑造成的字体污染。</summary>
        private void NormalizePlainFontRange(int start, int length)
        {
            if (length <= 0) return;
            Select(start, length);
            RestorePlainFormatting();
        }

        /// <summary>清除表情尺寸与基线偏移，保持正文及后续插入文字的原有样式。</summary>
        private void RestorePlainFormatting()
        {
            using (Font selectionFont = SelectionFont)
            {
                // 混合字号选区返回 null，必须连同可能混合的表情偏移一起复原。
                if (!IsSameFont(selectionFont, Font))
                {
                    SelectionFont = Font;
                    SelectionCharOffset = 0;
                }
                else if (SelectionCharOffset != 0) SelectionCharOffset = 0;
            }
        }

        /// <summary>按图片宽度查找能提供足够 RichEdit 字符宽度的最小字号。</summary>
        private float GetEmojiFontSize(string token, Graphics measure, int targetWidth)
        {
            float size;
            if (_emojiFontSizes.TryGetValue(token, out size)) return size;

            // 原生 GDI 的字符宽度在每个物理像素处跳变，使用相同的像素高度逐级查找。
            for (int pixels = 1; ; pixels++)
            {
                size = pixels * 72f / measure.DpiY;
                using (var font = new Font(Font.FontFamily, size, Font.Style))
                {
                    if (TextRenderer.MeasureText(measure, token, font, Size.Empty, TextFormatFlags.NoPadding).Width >= targetWidth)
                        break;
                }
            }
            _emojiFontSizes[token] = size;
            return size;
        }

        /// <summary>给所有段落设置不低于正文行高的最小行距，防止纯表情行坍缩。</summary>
        private void ApplyMinimumParagraphLineSpacing()
        {
            float lineHeight;
            using (Graphics measureGraphics = CreateGraphics())
                lineHeight = Math.Max(1f, Font.GetHeight(measureGraphics));
            int lineSpacingTwips = Math.Max(1, (int)Math.Ceiling(lineHeight * 1440f / DeviceDpi));
            var format = new NativeParaFormat2
            {
                Size = Marshal.SizeOf(typeof(NativeParaFormat2)),
                Mask = ParaFormatLinespacing,
                LineSpacing = lineSpacingTwips,
                LineSpacingRule = LineSpacingAtLeast,
                Tabs = new int[32]
            };
            // RichEdit 只会把段落格式应用到当前选区覆盖的段落，必须覆盖全文才能修复非首行纯表情段。
            Select(0, TextLength);
            SendMessage(Handle, SetParaFormatMessage, IntPtr.Zero, ref format);
        }

        /// <summary>保留原字号和 DPI 缩放关系，将输入区图片边长放大到原来的两倍。</summary>
        private int GetEmojiImageSize()
        {
            float scale = DeviceDpi / 96f;
            return (int)Math.Round(EmojiScale * Math.Max(EmojiMinimumSize * scale, Font.GetHeight(DeviceDpi) - EmojiVerticalInset * scale));
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
            RequestEmojiTokenNormalization();
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
        }

        /// <summary>刷新原生 RichEdit 完成后的表情叠绘，不引入覆盖窗口。</summary>
        private void InvalidateEmojiPaint()
        {
            if (!IsDisposed && IsHandleCreated) Invalidate();
        }

        /// <summary>隐藏 Windows 原生反色细线光标，改用主题色竖条光标。</summary>
        private void HideNativeCaret()
        {
            if (RichEditModule != IntPtr.Zero && IsHandleCreated && !IsDisposed) HideCaret(Handle);
        }

        /// <summary>输入、移动或获焦时重置光标闪烁状态为立即可见。</summary>
        private void ResetCaretBlink()
        {
            if (IsDisposed || !Focused || SelectionLength > 0 || ReadOnly) return;
            _caretVisible = true;
            _caretTimer.Stop();
            _caretTimer.Start();
        }

        /// <summary>仅对上次绘制的主题光标矩形做局部失效，交由 RichEdit 干净重绘底层文本。</summary>
        private void InvalidateLastCaretRect()
        {
            if (_lastCaretRect.IsEmpty) return;
            Rectangle invalidRect = Rectangle.Inflate(_lastCaretRect, 1, 1);
            if (!IsDisposed && IsHandleCreated) Invalidate(invalidRect);
        }

        /// <summary>把 RichEdit 的文字与图片、光标绘制到同一离屏缓冲。</summary>
        private void PaintInputFrame(Graphics graphics)
        {
            graphics.Clear(BackColor);
            IntPtr hdc = graphics.GetHdc();
            try
            {
                HideNativeCaret();
                var print = Message.Create(Handle, PrintClientMessage, hdc, (IntPtr)PrintClient);
                base.WndProc(ref print);
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
                HideNativeCaret();
            }

            Rectangle visible = new Rectangle(0, 0, ClientSize.Width,
                Math.Max(0, ClientSize.Height - 2));
            if (visible.Width <= 0 || visible.Height <= 0) return;
            graphics.SetClip(visible);

            using (var native = new RichEditPaint(Handle))
            {
                DrawEmojiPaint(graphics, visible, native);
                DrawThemeCaret(graphics, visible, native);
            }
        }

        /// <summary>读取当前索引处的有效表情，未命中时保留原来的文字。</summary>
        private static Bitmap ReadEmoji(string text, int index, out int last, out string token)
        {
            last = index;
            token = null;
            if (text[index] != '[') return null;
            last = text.IndexOf(']', index + 1, Math.Min(25, text.Length - index - 1));
            if (last <= index + 1) return null;
            token = text.Substring(index, last - index + 1);
            return ChatEmojiPicker.FindImage(token);
        }

        /// <summary>按 RichEdit 行基线绘制表情，保留原生选区及令牌内插入符的编辑反馈。</summary>
        private void DrawEmojiPaint(Graphics graphics, Rectangle visible, RichEditPaint native)
        {
            string text = Text ?? string.Empty;
            int selectionStart = Math.Max(0, Math.Min(TextLength, SelectionStart));
            int selectionEnd = Math.Min(TextLength, selectionStart + SelectionLength);
            int imageSize = GetEmojiImageSize();

            for (int index = 0; index < text.Length; index++)
            {
                int close;
                string token;
                Bitmap image = ReadEmoji(text, index, out close, out token);
                if (image == null) continue;
                bool selected = selectionEnd > selectionStart && selectionEnd > index && selectionStart <= close;
                DrawEmojiRun(graphics, visible, native, image, index, close, imageSize, selected);
                index = close;
            }
        }

        /// <summary>覆盖令牌的原生字形，并把 PNG 可见内容的底边放在文字基线上。</summary>
        private void DrawEmojiRun(Graphics graphics, Rectangle visible, RichEditPaint native,
            Bitmap image, int start, int last, int imageSize, bool selected)
        {
            Point first = GetPositionFromCharIndex(start);
            if (first.Y >= visible.Bottom) return;
            Point after = GetPositionFromCharIndex(last + 1);
            Point row = first;
            Rectangle largest = Rectangle.Empty;
            int imageBaseline = 0;

            // 原生控件允许在中文令牌内部折行，逐行擦除令牌，保留换行后的普通文字。
            using (var background = new SolidBrush(selected && (Focused || !HideSelection) ? SystemColors.Highlight : BackColor))
            {
                int rowStart = start;
                for (int index = start + 1; index <= last + 1; index++)
                {
                    Point next = index <= last ? GetPositionFromCharIndex(index) : after;
                    if (next.Y == row.Y && index <= last) continue;
                    int baseline, bottom;
                    if (native.TryGetLineBounds(rowStart, out baseline, out bottom))
                    {
                        int right = next.Y == row.Y && next.X > row.X ? next.X : visible.Right;
                        var occupied = Rectangle.FromLTRB(row.X, row.Y, right, bottom);
                        if (occupied.IntersectsWith(visible))
                        {
                            graphics.FillRectangle(background, occupied);
                            if (occupied.Width > largest.Width)
                            {
                                largest = occupied;
                                imageBaseline = baseline;
                            }
                        }
                    }
                    row = next;
                    rowStart = index;
                }
            }

            if (largest.Width <= 0) return;
            Rectangle source = GetEmojiContentBounds(image);
            int width = GetEmojiDisplayWidth(image, imageSize);
            int height = imageSize;
            int x = largest.X + Math.Max(0, (largest.Width - width) / 2);
            // 微软雅黑的中文可见笔画略低于基线，贴齐基线下方一个逻辑像素。
            int visibleBottom = imageBaseline + (int)Math.Round(2f * DeviceDpi / 96f);
            var destination = new Rectangle(x, visibleBottom - height, width, height);
            GraphicsState state = graphics.Save();
            // 图片需要伸到行框上方，才能完整呈现两倍高度。限制在输入区客户区内，
            // 不再按 RichEdit 的字符行框裁切图片，否则上半部分会被截掉。
            graphics.SetClip(visible, CombineMode.Intersect);
            graphics.DrawImage(image, destination, source, GraphicsUnit.Pixel);
            graphics.Restore(state);
        }

        /// <summary>缓存表情图片的可见像素范围，透明留白不参与文字底边对齐。</summary>
        private Rectangle GetEmojiContentBounds(Bitmap image)
        {
            Rectangle bounds;
            if (_emojiContentBounds.TryGetValue(image, out bounds)) return bounds;
            int left = image.Width, right = 0, top = image.Height, bottom = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (image.GetPixel(x, y).A == 0) continue;
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x + 1);
                    bottom = Math.Max(bottom, y + 1);
                }
            }
            bounds = left < right ? Rectangle.FromLTRB(left, top, right, bottom) : new Rectangle(Point.Empty, image.Size);
            _emojiContentBounds[image] = bounds;
            return bounds;
        }

        /// <summary>按图片非透明内容的宽高比计算实际可见表情尺寸。</summary>
        private int GetEmojiDisplayWidth(Bitmap image, int contentHeight)
        {
            Rectangle source = GetEmojiContentBounds(image);
            return Math.Max(1, (int)Math.Round(contentHeight * (double)source.Width / Math.Max(1, source.Height)));
        }

        /// <summary>在无选区且聚焦时绘制跟随当前主题强调色的 2px 竖条光标。</summary>
        private void DrawThemeCaret(Graphics graphics, Rectangle visible, RichEditPaint native)
        {
            if (!Focused || SelectionLength > 0 || ReadOnly || !_caretVisible) return;
            try
            {
                int caretIndex = Math.Max(0, Math.Min(TextLength, SelectionStart));
                Point pos = GetPositionFromCharIndex(caretIndex);
                float lineHeight = Math.Max(16f, Font.GetHeight(graphics));
                int caretHeight = Math.Max(14, (int)Math.Round(lineHeight - 2f));
                int caretWidth = 2;
                int x = Math.Max(1, Math.Min(visible.Right - caretWidth - 1, pos.X));
                int baseline, bottom;
                int y = native.TryGetLineBounds(caretIndex, out baseline, out bottom)
                    ? baseline - caretHeight + 3
                    : pos.Y;
                var caretRect = new Rectangle(x, y, caretWidth, caretHeight);
                if (!caretRect.IntersectsWith(visible)) return;

                // 整数像素对齐保证 2px 主题色竖条锐利饱和，并记录矩形供熄灭周期局部失效
                _lastCaretRect = caretRect;
                using (var brush = new SolidBrush(GrokTheme.Accent))
                {
                    graphics.FillRectangle(brush, caretRect);
                }
            }
            catch (ArgumentException) { }
            catch (ExternalException) { }
        }

        /// <summary>系统剪贴板提供图片或文件时接收 Ctrl+V 和 Shift+Insert，并在退格/删除时整枚删除内置表情令牌。</summary>
        /// <param name="msg">当前键盘消息。</param>
        /// <param name="keyData">按键和修饰键。</param>
        /// <returns>粘贴或整枚表情删除已处理时返回 true。</returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.V) || keyData == (Keys.Shift | Keys.Insert))
            {
                if (TryPasteAttachment()) return true;
                PasteClipboardText();
                return true;
            }
            if ((keyData == Keys.Back || keyData == (Keys.Shift | Keys.Back)) && TryDeleteAdjacentEmojiToken(true))
                return true;
            if (keyData == Keys.Delete && TryDeleteAdjacentEmojiToken(false))
                return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// 当无选区且光标紧邻内置表情令牌（如 [微笑]）时，按退格或删除键直接整枚删除，
        /// 避免只删掉右括号导致退化显示为未闭合的文本令牌。
        /// </summary>
        /// <param name="backward">true 表示退格向前删，false 表示 Delete 向后删。</param>
        /// <returns>命中内置表情令牌并完成整枚删除时返回 true。</returns>
        private bool TryDeleteAdjacentEmojiToken(bool backward)
        {
            if (ReadOnly || SelectionLength > 0) return false;
            string text = Text ?? string.Empty;
            int caret = SelectionStart;
            if (backward)
            {
                if (caret <= 0 || caret > text.Length || text[caret - 1] != ']') return false;
                int minOpen = Math.Max(0, caret - 26);
                for (int open = caret - 2; open >= minOpen; open--)
                {
                    char c = text[open];
                    if (c == ']' || c == '\r' || c == '\n') break;
                    if (c == '[')
                    {
                        if (caret - open > 2)
                        {
                            string token = text.Substring(open, caret - open);
                            if (ChatEmojiPicker.FindImage(token) != null)
                            {
                                DeleteTokenRange(open, caret - open);
                                return true;
                            }
                        }
                        break;
                    }
                }
                return false;
            }

            if (caret < 0 || caret >= text.Length || text[caret] != '[') return false;
            int close = text.IndexOf(']', caret + 1);
            if (close > caret + 1 && close - caret <= 25)
            {
                string token = text.Substring(caret, close - caret + 1);
                if (ChatEmojiPicker.FindImage(token) != null)
                {
                    DeleteTokenRange(caret, close - caret + 1);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 冻结 RichEdit 重绘后一次性选中并删除表情令牌，避免露出中间态的 [名称] 选中文本。
        /// </summary>
        private void DeleteTokenRange(int start, int length)
        {
            if (IsHandleCreated && !IsDisposed)
                SendMessage(Handle, SetRedrawMessage, IntPtr.Zero, IntPtr.Zero);
            try
            {
                Select(start, length);
                SelectedText = string.Empty;
            }
            finally
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    SendMessage(Handle, SetRedrawMessage, (IntPtr)1, IntPtr.Zero);
                    Invalidate();
                    Update();
                }
            }
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
            if (m.Msg == ImeStartCompositionMessage)
                _imeComposing = true;
            if (m.Msg == ContextMenuMessage)
            {
                Point location = m.LParam == new IntPtr(-1) ? new Point(0, Height) : PointToClient(Cursor.Position);
                if (ShowContextMenuAt(PointToScreen(location))) return;
            }
            if (RichEditModule != IntPtr.Zero && m.Msg == EraseBackgroundMessage)
            {
                // 背景与文字在缓冲区内统一重画，避免窗口先出现只有背景的一帧。
                m.Result = (IntPtr)1;
                return;
            }
            if (RichEditModule != IntPtr.Zero && (m.Msg == PaintMessage || (m.WParam != IntPtr.Zero &&
                (m.Msg == PrintClientMessage || (m.Msg == PrintMessage && (m.LParam.ToInt64() & PrintClient) != 0)))))
            {
                if (!_normalizingEmojiTokens && !Disposing && !IsDisposed)
                    RichEditPaint.Render(m.HWnd, m.WParam, ClientRectangle, PaintInputFrame);
                return;
            }

            base.WndProc(ref m);
            if (m.Msg == SetFocusMessage || m.Msg == LeftButtonDownMessage || m.Msg == LeftButtonUpMessage ||
                m.Msg == KeyDownMessage || m.Msg == CharMessage)
            {
                HideNativeCaret();
            }
            if (m.Msg == LeftButtonUpMessage)
            {
                int x = (short)(m.LParam.ToInt64() & 0xFFFF);
                int y = (short)(m.LParam.ToInt64() >> 16);
                SnapCaretOutsideEmoji(new Point(x, y));
            }
            if (m.Msg == ResizeMessage) ApplyNativeTextRect();
            if (m.Msg == HorizontalScrollMessage || m.Msg == VerticalScrollMessage || m.Msg == MouseWheelMessage)
                InvalidateEmojiPaint();
            if (m.Msg == ImeEndCompositionMessage)
            {
                _imeComposing = false;
                if (_normalizationPending)
                {
                    _normalizationPending = false;
                    NormalizeEmojiTokenFormatting();
                }
            }
        }

        /// <summary>把鼠标落在表情内部产生的字符插入点收拢到表情左侧或右侧。</summary>
        /// <param name="location">输入框客户区内的鼠标落点。</param>
        private void SnapCaretOutsideEmoji(Point location)
        {
            if (SelectionLength != 0 || _normalizingEmojiTokens) return;
            string text = Text ?? string.Empty;
            int caret = Math.Max(0, Math.Min(TextLength, SelectionStart));
            int tokenStart = -1;
            int tokenEnd = -1;
            for (int index = 0; index < text.Length; index++)
            {
                int close;
                string token;
                if (ReadEmoji(text, index, out close, out token) == null) continue;
                if (caret > index && caret <= close)
                {
                    tokenStart = index;
                    tokenEnd = close + 1;
                    break;
                }
                index = close;
            }
            if (tokenStart < 0) return;

            Point start = GetPositionFromCharIndex(tokenStart);
            Point end = GetPositionFromCharIndex(tokenEnd);
            int midpoint = end.Y == start.Y && end.X > start.X
                ? start.X + (end.X - start.X) / 2
                : start.X;
            Select(location.X < midpoint ? tokenStart : tokenEnd, 0);
            HideNativeCaret();
            ResetCaretBlink();
            InvalidateEmojiPaint();
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
                // SelectedText 触发统一格式化，粘贴的表情沿用宽度和基线，普通文字恢复正文字体。
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
