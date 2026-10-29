using System.Drawing;
using System.Drawing.Text;
using System.Text;

namespace LanChat
{
    /// <summary>
    /// Shared GrokBot design tokens (Docs/GrokBot_UI_Design_System.md).
    /// C# 5 / .NET Framework 4.0 friendly — readonly Color fields, no C# 6+ syntax.
    /// </summary>
    public static class GrokTheme
    {
        public const string FontFamily = "Microsoft YaHei UI";

        // Background elevation
        public static readonly Color Level0 = Color.FromArgb(14, 14, 14);       // #0E0E0E
        public static readonly Color Level05 = Color.FromArgb(18, 18, 18);      // #121212
        public static readonly Color Level1 = Color.FromArgb(28, 28, 31);       // #1C1C1F
        public static readonly Color Level2 = Color.FromArgb(32, 32, 36);       // #202024
        public static readonly Color Level3 = Color.FromArgb(40, 40, 44);       // #28282C
        public static readonly Color Level3Hot = Color.FromArgb(46, 46, 52);    // #2E2E34

        // Borders
        public static readonly Color Border = Color.FromArgb(44, 44, 50);       // #2C2C32
        public static readonly Color BorderInner = Color.FromArgb(38, 38, 43);  // #26262B
        public static readonly Color Separator = Color.FromArgb(44, 44, 52);    // #2C2C34

        // Text
        public static readonly Color TextPrimary = Color.FromArgb(242, 242, 246);   // #F2F2F6
        public static readonly Color TextTitle = Color.FromArgb(255, 255, 255);     // #FFFFFF
        public static readonly Color TextSecondary = Color.FromArgb(142, 142, 147); // #8E8E93
        public static readonly Color TextSubtitle = Color.FromArgb(161, 161, 166);   // #A1A1A6 brighter session subtitle
        // Capsule placeholder: dimmer muted gray (closer to Grok Electron muted)
        public static readonly Color TextPlaceholder = Color.FromArgb(94, 94, 102); // #5E5E66
        public static readonly Color TextDisabled = Color.FromArgb(74, 74, 80);     // #4A4A50
        public static readonly Color TextMenu = Color.FromArgb(228, 228, 232);      // #E4E4E8
        public static readonly Color TextPill = Color.FromArgb(245, 245, 247);      // #F5F5F7 warmer/brighter pill white
        public static readonly Color TextCard = Color.FromArgb(240, 240, 244);      // #F0F0F4
        public static readonly Color TextDestructive = Color.FromArgb(252, 68, 85); // #FC4455
        public static readonly Color TextDestructiveHot = Color.FromArgb(255, 92, 108);

        // Accents
        public static readonly Color AccentPurple = Color.FromArgb(142, 100, 220);  // #8E64DC
        public static readonly Color AccentPurpleHot = Color.FromArgb(157, 117, 236);
        public static readonly Color AccentPurpleDeep = Color.FromArgb(121, 70, 215); // #7946D7
        public static readonly Color AccentBlue = Color.FromArgb(77, 155, 255);     // #4D9BFF
        public static readonly Color AccentBlueBtn = Color.FromArgb(17, 109, 210);  // #116DD2
        public static readonly Color AccentCoral = Color.FromArgb(252, 44, 62);     // #FC2C3E
        public static readonly Color AccentGreen = Color.FromArgb(31, 214, 126);    // #1FD67E

        // Chat / selection surfaces
        public static readonly Color SessionSelected = Color.FromArgb(40, 40, 44);  // #28282C
        public static readonly Color SessionHover = Color.FromArgb(34, 34, 38);     // #222226 soft hover card
        public static readonly Color PeerBubble = Color.FromArgb(30, 30, 33);       // #1E1E21
        public static readonly Color InputPlus = Color.FromArgb(42, 42, 46);        // #2A2A2E
        public static readonly Color IconMuted = Color.FromArgb(160, 160, 165);     // #A0A0A5
        public static readonly Color MenuHoverDanger = Color.FromArgb(60, 24, 28);  // #3C181C

        // Layout metrics
        public const int MenuItemHeight = 44;
        public const int SettingsRowHeight = 54;
        public const int RadiusModal = 16;
        public const int RadiusMenuHover = 8;
        public const int MainWindowW = 1040;
        public const int MainWindowH = 720;

        public static Font Font(float emSize)
        {
            return new Font(FontFamily, emSize, FontStyle.Regular);
        }

        public static Font Font(float emSize, FontStyle style)
        {
            return new Font(FontFamily, emSize, style);
        }

        /// <summary>
        /// Soft grayscale AA for dark UI (closer to Chromium/Electron than ClearType).
        /// </summary>
        public static void PrepareSoftText(Graphics g)
        {
            if (g == null) return;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
        }

        /// <summary>
        /// Strip chars that commonly paint as tofu □ under YaHei UI
        /// (emoji surrogate pairs, private-use, replacement, box glyphs, controls).
        /// </summary>
        public static string SanitizeUiText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            StringBuilder sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                        i++;
                    continue;
                }
                if (char.IsLowSurrogate(c))
                    continue;

                // Private Use Area
                if (c >= 0xE000 && c <= 0xF8FF)
                    continue;

                // Replacement / empty box / black square often seen as tofu
                if (c == '\uFFFD' || c == '\u25A1' || c == '\u25A0' || c == '\u25FB' || c == '\u25FC')
                    continue;

                if (char.IsControl(c))
                    continue;

                sb.Append(c);
            }

            // Collapse runs of spaces left by stripped glyphs (e.g. "给 📢 名" → "给  名")
            string cleaned = sb.ToString();
            while (cleaned.IndexOf("  ") >= 0)
                cleaned = cleaned.Replace("  ", " ");
            return cleaned.Trim();
        }

        private static StringFormat CreateUiFormat(StringAlignment align, StringAlignment lineAlign)
        {
            StringFormat sf = new StringFormat(StringFormatFlags.NoWrap);
            sf.Alignment = align;
            sf.LineAlignment = lineAlign;
            sf.Trimming = StringTrimming.EllipsisCharacter;
            sf.HotkeyPrefix = HotkeyPrefix.None;
            return sf;
        }

        /// <summary>
        /// Draw UI copy via GDI+ DrawString + grayscale AntiAlias (not ClearType/TextRenderer).
        /// </summary>
        public static void DrawUiText(Graphics g, string text, Font font, Color color, Rectangle bounds, StringFormat format)
        {
            if (g == null || font == null) return;
            string safe = SanitizeUiText(text);
            if (string.IsNullOrEmpty(safe)) return;
            PrepareSoftText(g);
            using (SolidBrush brush = new SolidBrush(color))
            {
                if (format != null)
                    g.DrawString(safe, font, brush, bounds, format);
                else
                    g.DrawString(safe, font, brush, bounds);
            }
        }

        public static void DrawUiTextCentered(Graphics g, string text, Font font, Color color, Rectangle bounds)
        {
            using (StringFormat sf = CreateUiFormat(StringAlignment.Center, StringAlignment.Center))
            {
                DrawUiText(g, text, font, color, bounds, sf);
            }
        }

        /// <summary>
        /// Left-aligned, vertically centered text starting at x within a full-height band.
        /// </summary>
        public static void DrawUiTextLeft(Graphics g, string text, Font font, Color color, int x, int y, int width, int height)
        {
            Rectangle bounds = new Rectangle(x, y, width, height);
            using (StringFormat sf = CreateUiFormat(StringAlignment.Near, StringAlignment.Center))
            {
                DrawUiText(g, text, font, color, bounds, sf);
            }
        }

        public static Size MeasureUiText(Graphics g, string text, Font font)
        {
            string safe = SanitizeUiText(text);
            if (string.IsNullOrEmpty(safe) || font == null) return Size.Empty;

            if (g != null)
            {
                PrepareSoftText(g);
                SizeF sz = g.MeasureString(safe, font);
                return Size.Ceiling(sz);
            }

            using (Bitmap bmp = new Bitmap(1, 1))
            using (Graphics tg = Graphics.FromImage(bmp))
            {
                PrepareSoftText(tg);
                SizeF sz = tg.MeasureString(safe, font);
                return Size.Ceiling(sz);
            }
        }
    }
}
