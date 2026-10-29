using System.Drawing;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;

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
        // Capsule placeholder: match Grok readable medium gray (was dim #5E5E66)
        public static readonly Color TextPlaceholder = Color.FromArgb(142, 142, 147); // #8E8E93
        public static readonly Color TextDisabled = Color.FromArgb(74, 74, 80);     // #4A4A50
        public static readonly Color TextMenu = Color.FromArgb(228, 228, 232);      // #E4E4E8
        public static readonly Color TextPill = Color.FromArgb(240, 240, 245);      // #F0F0F5
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
        /// Prefer ClearType before any remaining GDI+ DrawString calls.
        /// </summary>
        public static void PrepareClearType(Graphics g)
        {
            if (g == null) return;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
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

        public static readonly TextFormatFlags UiTextFlags =
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding |
            TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping |
            TextFormatFlags.PreserveGraphicsTranslateTransform;

        public static readonly TextFormatFlags UiTextCenterFlags =
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding |
            TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping |
            TextFormatFlags.PreserveGraphicsTranslateTransform;

        /// <summary>
        /// Draw UI copy via TextRenderer (native Win32 / ClearType), not GDI+ DrawString.
        /// </summary>
        public static void DrawUiText(Graphics g, string text, Font font, Color color, Rectangle bounds, TextFormatFlags flags)
        {
            if (g == null || font == null) return;
            string safe = SanitizeUiText(text);
            if (string.IsNullOrEmpty(safe)) return;
            PrepareClearType(g);
            TextRenderer.DrawText(g, safe, font, bounds, color, flags);
        }

        public static void DrawUiTextCentered(Graphics g, string text, Font font, Color color, Rectangle bounds)
        {
            DrawUiText(g, text, font, color, bounds, UiTextCenterFlags);
        }

        /// <summary>
        /// Left-aligned, vertically centered text starting at x within a full-height band.
        /// </summary>
        public static void DrawUiTextLeft(Graphics g, string text, Font font, Color color, int x, int y, int width, int height)
        {
            Rectangle bounds = new Rectangle(x, y, width, height);
            DrawUiText(g, text, font, color, bounds,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping |
                TextFormatFlags.PreserveGraphicsTranslateTransform);
        }

        public static Size MeasureUiText(Graphics g, string text, Font font)
        {
            string safe = SanitizeUiText(text);
            if (string.IsNullOrEmpty(safe) || font == null) return Size.Empty;
            if (g != null)
                return TextRenderer.MeasureText(g, safe, font, new Size(int.MaxValue, int.MaxValue), UiTextFlags);
            return TextRenderer.MeasureText(safe, font, new Size(int.MaxValue, int.MaxValue), UiTextFlags);
        }
    }
}
