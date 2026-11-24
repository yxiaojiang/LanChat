using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace LanChat
{
    /// <summary>
    /// 计算聊天行、划选边界和行内绘制尺寸，依赖 GDI+、系统 Emoji 字体及本地导入的表情。
    /// 按原始 UTF-16 文本定位，位图只用于绘制，布局随聊天视图重算。
    /// </summary>
    internal static class ChatTextLayout
    {
        /// <summary>保存视觉行及在原消息中的位置，由聊天视图持有并定位。</summary>
        internal sealed class VisualLine
        {
            internal string Text;
            internal int StartChar;
            internal int Length { get { return Text.Length; } }
            internal RectangleF Bounds;
        }

        /// <summary>查找单个可见文字元素的结束位置，避免折行和划选拆开表情。</summary>
        internal static int NextBoundary(string text, int start)
        {
            int end = start + 1;
            if (text[start] == '[')
            {
                int close = text.IndexOf(']', end, Math.Min(25, text.Length - end));
                if (close >= 0 && ChatEmojiPicker.FindImage(text.Substring(start, close - start + 1)) != null)
                    return close + 1;
            }
            end = ReadElement(text, start);
            while (end + 1 < text.Length && text[end] == '\u200D')
                end = ReadElement(text, end + 1);
            return end;
        }

        private static int ReadElement(string text, int start)
        {
            int end = start + CharLength(text, start);
            int first = CodePoint(text, start);
            if (first >= 0x1F1E6 && first <= 0x1F1FF && end < text.Length)
            {
                int next = CodePoint(text, end);
                if (next >= 0x1F1E6 && next <= 0x1F1FF) end += 2;
            }
            while (end < text.Length)
            {
                int next = CodePoint(text, end);
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(text, end);
                if (category != UnicodeCategory.NonSpacingMark && category != UnicodeCategory.SpacingCombiningMark &&
                    category != UnicodeCategory.EnclosingMark && (next < 0x1F3FB || next > 0x1F3FF) &&
                    (next < 0xE0020 || next > 0xE007F)) break;
                end += CharLength(text, end);
            }
            return end;
        }

        private static int CharLength(string text, int start)
        {
            return char.IsSurrogatePair(text, start) ? 2 : 1;
        }

        private static int CodePoint(string text, int start)
        {
            return char.IsSurrogatePair(text, start) ? char.ConvertToUtf32(text, start) : text[start];
        }

        /// <summary>按可见元素换行，并保留 CRLF 前后的原始消息索引。</summary>
        internal static List<VisualLine> BreakLines(Graphics g, string text, Font font, float maxWidth)
        {
            var lines = new List<VisualLine>();
            if (text.Length == 0) return lines;
            float height = font.GetHeight(g) + GrokTheme.ChatLineSpacingExtra;
            int start = 0;
            while (start <= text.Length)
            {
                int newline = text.IndexOf('\n', start);
                int end = newline < 0 ? text.Length : newline;
                if (newline >= 0 && end > start && text[end - 1] == '\r') end--;
                do
                {
                    int next = Fit(g, text, font, maxWidth, start, end, true);
                    string line = text.Substring(start, next - start);
                    lines.Add(new VisualLine
                    {
                        Text = line, StartChar = start,
                        Bounds = new RectangleF(0, lines.Count * height, (float)Math.Ceiling(Measure(g, line, font)), height)
                    });
                    start = next;
                } while (start < end);
                if (newline < 0) break;
                start = newline + 1;
            }
            return lines;
        }

        private static int Fit(Graphics g, string text, Font font, float maxWidth, int start, int end, bool wrap)
        {
            int position = start;
            int wordBreak = -1;
            while (position < end)
            {
                int next = Math.Min(NextBoundary(text, position), end);
                if (Measure(g, text.Substring(start, next - start), font) > maxWidth)
                    return wrap ? (wordBreak > start ? wordBreak : (position == start ? next : position)) : position;
                if (wrap && char.IsWhiteSpace(text[position])) wordBreak = next;
                position = next;
            }
            return position;
        }

        /// <summary>测量文本和行内表情的像素宽度，与实际绘制共用规则。</summary>
        internal static float Measure(Graphics g, string text, Font font)
        {
            return PaintText(g, text, font, null, 0, 0);
        }

        /// <summary>在指定坐标绘制消息文本和本地表情，返回占用宽度。</summary>
        internal static float Draw(Graphics g, string text, Font font, Brush brush, float x, float y)
        {
            return PaintText(g, text, font, brush, x, y);
        }

        /// <summary>绘制可包含表情的单行预览，按可见元素截断并添加省略号。</summary>
        internal static void DrawSnippet(Graphics g, string text, Font font, Color color, Rectangle bounds)
        {
            if (string.IsNullOrEmpty(text) || bounds.Width <= 0) return;
            text = text.Replace('\r', ' ').Replace('\n', ' ');
            int end = Fit(g, text, font, bounds.Width, 0, text.Length, false);
            if (end < text.Length)
                text = text.Substring(0, Fit(g, text, font, bounds.Width - Measure(g, "…", font), 0, end, false)) + "…";
            using (var brush = new SolidBrush(color))
                Draw(g, text, font, brush, bounds.X, bounds.Y + (bounds.Height - font.GetHeight(g)) / 2f);
        }

        private static float PaintText(Graphics g, string text, Font font, Brush brush, float x, float y)
        {
            if (text.Length == 0) return 0;
            float origin = x;
            Font emojiFont = null;
            using (StringFormat format = StringFormat.GenericTypographic)
            {
                format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                int runStart = 0;
                try
                {
                    for (int i = 0, end; i < text.Length; i = end)
                    {
                        end = NextBoundary(text, i);
                        Bitmap image = text[i] == '[' && end > i + 1 ? ChatEmojiPicker.FindImage(text.Substring(i, end - i)) : null;
                        if (image == null && !IsEmojiElement(text, i, end)) continue;

                        // 保留普通文字的连续排版。表情的基底、修饰符与连接序列共用一次测量和绘制。
                        x += PaintRun(g, text.Substring(runStart, i - runStart), font, brush, x, y, format);
                        if (image != null)
                        {
                            float size = font.GetHeight(g);
                            if (brush != null)
                            {
                                var interpolation = g.InterpolationMode;
                                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                g.DrawImage(image, new RectangleF(x + 1, y, size, size));
                                g.InterpolationMode = interpolation;
                            }
                            x += size + 2;
                        }
                        else
                        {
                            if (emojiFont == null) emojiFont = new Font("Segoe UI Emoji", font.SizeInPoints);
                            x += PaintRun(g, text.Substring(i, end - i), emojiFont, brush, x,
                                y + (font.GetHeight(g) - emojiFont.GetHeight(g)) / 2f, format);
                        }
                        runStart = end;
                    }
                    return x - origin + PaintRun(g, text.Substring(runStart), font, brush, x, y, format);
                }
                finally
                {
                    if (emojiFont != null) emojiFont.Dispose();
                }
            }
        }

        private static bool IsEmojiElement(string text, int start, int end)
        {
            for (int i = start; i < end; i += CharLength(text, i))
            {
                int code = CodePoint(text, i);
                if ((code >= 0x1F000 && code <= 0x1FAFF) ||
                    (code >= 0x2600 && code <= 0x27BF) ||
                    (code >= 0x231A && code <= 0x231B) || (code >= 0x23E9 && code <= 0x23EC) ||
                    code == 0x23F0 || code == 0x23F3 || code == 0x2B50 ||
                    code == 0xFE0F || code == 0x20E3 || code == 0x200D)
                    return true;
            }
            return false;
        }

        private static float PaintRun(Graphics g, string text, Font font, Brush brush, float x, float y, StringFormat format)
        {
            if (text.Length == 0) return 0;
            if (brush != null) g.DrawString(text, font, brush, new PointF(x, y), format);
            return g.MeasureString(text, font, 10000, format).Width;
        }
    }
}
