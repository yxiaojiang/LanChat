using System.Drawing;

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
        public static readonly Color TextPlaceholder = Color.FromArgb(94, 94, 102); // #5E5E66
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
    }
}
