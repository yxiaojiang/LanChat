using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text;

namespace LanChat
{
    /// <summary>
    /// GrokBot 全局统一设计令牌与 GDI+ 软抗锯齿渲染中枢 (Docs/GrokBot_UI_Design_System.md)
    /// <para>【职责】全客户端唯一真理来源 (Single Source of Truth)，集中管理暗黑色彩阶梯、文字排版、基准尺寸与平滑自绘工具。</para>
    /// <para>【兼容性】纯 C# 5.0 / .NET Framework 4.0 语法规范，免外部第三方依赖。</para>
    /// </summary>
    public static class GrokTheme
    {
        /// <summary>
        /// 获取当前生效的 UI 界面字体家族名称（读取用户设置，默认回退为 Microsoft YaHei UI）
        /// </summary>
        public static string FontFamily
        {
            get
            {
                return !string.IsNullOrEmpty(AppSettings.UiFontFamily) ? AppSettings.UiFontFamily : "Microsoft YaHei UI";
            }
        }

        // ==========================================
        // 5 级暗黑纵深分层矩阵 (Background Elevation)
        // ==========================================

        /// <summary>Level 0 (5.5% 明度)：最底层哑光基板，用于左侧固定会话栏、设置侧边栏底色 (#0E0E0E)</summary>
        public static readonly Color Level0 = Color.FromArgb(14, 14, 14);

        /// <summary>Level 0.5 (7.1% 明度)：主视口底色，用于主聊天视口、设置主内容区底色 (#121212)</summary>
        public static readonly Color Level05 = Color.FromArgb(18, 18, 18);

        /// <summary>Level 1 (11.4% 明度)：卡片容器底色，用于大设置卡片容器、独立模态窗体底板 (#1C1C1F)</summary>
        public static readonly Color Level1 = Color.FromArgb(28, 28, 31);

        /// <summary>Level 2 (13.3% 明度)：浮动胶囊底色，用于底部主输入胶囊、弹窗搜索框底色 (#202024)</summary>
        public static readonly Color Level2 = Color.FromArgb(32, 32, 36);

        /// <summary>Level 3 (17.0% 明度)：高亮底色，用于次要按钮正常底色、侧边栏激活指示底块 (#28282C)</summary>
        public static readonly Color Level3 = Color.FromArgb(40, 40, 44);

        /// <summary>Level 3 Hot (19.5% 明度)：悬停反馈底色，用于菜单项悬停、按钮悬停提亮底块 (#2E2E34)</summary>
        public static readonly Color Level3Hot = Color.FromArgb(46, 46, 52);

        // ==========================================
        // 边框与微光分割线 (Borders & Separators)
        // ==========================================

        /// <summary>微光细外边框：用于所有模态窗体外沿、输入胶囊外框、卡片外描边 (#2C2C32，线宽 1.0px)</summary>
        public static readonly Color Border = Color.FromArgb(44, 44, 50);

        /// <summary>次级卡片内部分割线：用于卡片行间分割线 (#26262B，线宽 1.0px，左右缩进 20px)</summary>
        public static readonly Color BorderInner = Color.FromArgb(38, 38, 43);

        /// <summary>浮动菜单分割线：用于右键弹出菜单的水平分隔线 (#2C2C34)</summary>
        public static readonly Color Separator = Color.FromArgb(44, 44, 52);

        // ==========================================
        // 文本前景色阶与明度阶梯 (Text Foreground)
        // ==========================================

        /// <summary>主内容前景色 (95% 明度)：用于主输入文本、聊天正文、焦点标题 (#F2F2F6)</summary>
        public static readonly Color TextPrimary = Color.FromArgb(242, 242, 246);

        /// <summary>一级大标题前景色 (100% 明度)：用于弹窗大标题、纯白粗体 (#FFFFFF)</summary>
        public static readonly Color TextTitle = Color.FromArgb(255, 255, 255);

        /// <summary>辅助描述前景色 (57% 明度)：用于副说明、时间戳、次级标签 (#8E8E93)</summary>
        public static readonly Color TextSecondary = Color.FromArgb(142, 142, 147);

        /// <summary>会话副标题前景色：较 Secondary 稍亮的副标题颜色 (#A1A1A6)</summary>
        public static readonly Color TextSubtitle = Color.FromArgb(161, 161, 166);

        /// <summary>占位提示符前景色 (38% 明度)：用于输入框占位符文本 (#5E5E66)</summary>
        public static readonly Color TextPlaceholder = Color.FromArgb(94, 94, 102);

        /// <summary>禁用状态文字前景色 (30% 明度)：用于禁用控件与未就绪状态 (#4A4A50)</summary>
        public static readonly Color TextDisabled = Color.FromArgb(74, 74, 80);

        /// <summary>浮动菜单主要文字前景色 (#E4E4E8)</summary>
        public static readonly Color TextMenu = Color.FromArgb(228, 228, 232);

        /// <summary>药丸胶囊标题前景色：温润明亮白 (#F5F5F7)</summary>
        public static readonly Color TextPill = Color.FromArgb(245, 245, 247);

        /// <summary>卡片主要项前景色 (#F0F0F4)</summary>
        public static readonly Color TextCard = Color.FromArgb(240, 240, 244);

        /// <summary>警示危险操作文字颜色 (#FC4455)</summary>
        public static readonly Color TextDestructive = Color.FromArgb(252, 68, 85);

        /// <summary>警示危险操作悬停高亮文字颜色 (#FF5C6C)</summary>
        public static readonly Color TextDestructiveHot = Color.FromArgb(255, 92, 108);

        // ==========================================
        // 品牌强调色与聊天状态 (Accent & Brand Colors)
        // ==========================================

        /// <summary>Grok 魅紫高光色：用于发送圆钮与高亮状态 (#8E64DC)</summary>
        public static readonly Color AccentPurple = Color.FromArgb(142, 100, 220);

        /// <summary>Grok 魅紫悬停强化色 (#9D75EC)</summary>
        public static readonly Color AccentPurpleHot = Color.FromArgb(157, 117, 236);

        /// <summary>Grok 魅紫核心基准色：用于主品牌色、本人消息气泡与选中态 (#7946D7)</summary>
        public static readonly Color AccentPurpleDeep = Color.FromArgb(121, 70, 215);

        /// <summary>智能天蓝色：用于更新提示文本、在线指示点 (#4D9BFF)</summary>
        public static readonly Color AccentBlue = Color.FromArgb(77, 155, 255);

        /// <summary>天蓝按钮底色 (#116DD2)</summary>
        public static readonly Color AccentBlueBtn = Color.FromArgb(17, 109, 210);

        /// <summary>珊瑚活力红色：用于 Mascot 头像底色与警报 (#FC2C3E)</summary>
        public static readonly Color AccentCoral = Color.FromArgb(252, 44, 62);

        /// <summary>在线翠绿色：用于网络在线指示点与滑动开关开启态 (#1FD67E)</summary>
        public static readonly Color AccentGreen = Color.FromArgb(31, 214, 126);

        /// <summary>会话列表选中态底色 (与本人发送气泡一致：#7946D7)</summary>
        public static readonly Color SessionSelected = Color.FromArgb(121, 70, 215);

        /// <summary>会话列表项鼠标悬停底色 (#222226)</summary>
        public static readonly Color SessionHover = Color.FromArgb(34, 34, 38);

        /// <summary>对方发送的消息气泡底色 (#1E1E21)</summary>
        public static readonly Color PeerBubble = Color.FromArgb(30, 30, 33);

        /// <summary>底部输入框左侧 (+) 附件圆钮底色 (#2A2A2E)</summary>
        public static readonly Color InputPlus = Color.FromArgb(42, 42, 46);

        /// <summary>静止态暗灰图标线条色 (#A0A0A5)</summary>
        public static readonly Color IconMuted = Color.FromArgb(160, 160, 165);

        /// <summary>危险菜单项悬停暗红底色 (#3C181C)</summary>
        public static readonly Color MenuHoverDanger = Color.FromArgb(60, 24, 28);

        // ==========================================
        // 按钮角色状态色彩 (Button Roles & States)
        // ==========================================

        /// <summary>Primary 主按钮常态底色 (#7946D7)</summary>
        public static readonly Color ButtonPrimaryNormal = Color.FromArgb(121, 70, 215);
        /// <summary>Primary 主按钮悬停底色 (#8E64DC)</summary>
        public static readonly Color ButtonPrimaryHover = Color.FromArgb(142, 100, 220);
        /// <summary>Primary 主按钮按压底色</summary>
        public static readonly Color ButtonPrimaryDown = Color.FromArgb(105, 55, 195);

        /// <summary>Secondary 次按钮常态底色 (#28282C)</summary>
        public static readonly Color ButtonSecondaryNormal = Color.FromArgb(40, 40, 44);
        /// <summary>Secondary 次按钮悬停底色</summary>
        public static readonly Color ButtonSecondaryHover = Color.FromArgb(52, 52, 58);
        /// <summary>Secondary 次按钮按压底色</summary>
        public static readonly Color ButtonSecondaryDown = Color.FromArgb(32, 32, 36);

        /// <summary>Destructive 危险按钮常态底色 (#3C181C)</summary>
        public static readonly Color ButtonDestructiveNormal = Color.FromArgb(60, 24, 28);
        /// <summary>Destructive 危险按钮悬停底色</summary>
        public static readonly Color ButtonDestructiveHover = Color.FromArgb(80, 30, 36);
        /// <summary>Destructive 危险按钮按压底色</summary>
        public static readonly Color ButtonDestructiveDown = Color.FromArgb(48, 18, 22);

        // ==========================================
        // 窗口基准尺寸矩阵 (Window Benchmark Sizes)
        // ==========================================

        /// <summary>主窗体标准基准尺寸 (1040 × 720)</summary>
        public static readonly Size SizeMainWindow = new Size(1040, 720);
        /// <summary>全局设置中心标准尺寸 (920 × 640)</summary>
        public static readonly Size SizeSettings = new Size(920, 640);
        /// <summary>全局设置中心大屏舒适尺寸 (1260 × 820)</summary>
        public static readonly Size SizeSettingsLarge = new Size(1260, 820);
        /// <summary>连接节点窗体标准尺寸 (760 × 600)</summary>
        public static readonly Size SizeConnectNode = new Size(760, 600);
        /// <summary>新建节点窗体标准尺寸 (680 × 620)</summary>
        public static readonly Size SizeCreateNode = new Size(680, 620);
        /// <summary>关于窗体标准尺寸 (440 × 560)</summary>
        public static readonly Size SizeAbout = new Size(440, 560);
        /// <summary>意见反馈窗体标准尺寸 (520 × 460)</summary>
        public static readonly Size SizeFeedback = new Size(520, 460);
        /// <summary>软件更新窗体标准尺寸 (520 × 440)</summary>
        public static readonly Size SizeUpdate = new Size(520, 440);
        /// <summary>自定义消息弹窗基准尺寸 (460 × 260)</summary>
        public static readonly Size SizeMessageBox = new Size(460, 260);
        /// <summary>修改昵称窗体标准尺寸 (440 × 260)</summary>
        public static readonly Size SizeChangeNick = new Size(440, 260);

        // ==========================================
        // 控件度量与间距规范 (Metrics & Spacing)
        // ==========================================

        /// <summary>标准操作按钮高度 (36px)</summary>
        public const int ButtonHeightStandard = 36;
        /// <summary>小药丸按钮高度 (32px)</summary>
        public const int ButtonHeightPill = 32;
        /// <summary>按钮文字左右内边距 (18px)</summary>
        public const int ButtonPaddingHorizontal = 18;
        /// <summary>标准按钮圆角半径 (8px)</summary>
        public const int ButtonRadiusStandard = 8;
        /// <summary>药丸按钮全圆角半径 (16px)</summary>
        public const int ButtonRadiusPill = 16;

        /// <summary>模态窗右上角关闭叉尺寸 (28×28px)</summary>
        public const int CloseButtonSize = 28;
        /// <summary>模态窗右上角关闭叉距顶和右的边距 (16px)</summary>
        public const int CloseButtonMargin = 16;

        /// <summary>卡片内部边距 (20px)</summary>
        public const int CardPadding = 20;
        /// <summary>卡片外部间距 (12px)</summary>
        public const int CardMargin = 12;
        /// <summary>弹窗主体内边距 (24px)</summary>
        public const int ModalPadding = 24;
        /// <summary>文字行距额外加量 (5px 呼吸感)</summary>
        public const float LineSpacingExtra = 5f;

        /// <summary>会话列表单行项高度 (72px)</summary>
        public const int SessionItemHeight = 72;
        /// <summary>会话卡片水平外边距 (左右各缩进 17px)</summary>
        public const int SessionCardMarginX = 17;
        /// <summary>会话卡片垂直外边距 (上下各缩进 5px)</summary>
        public const int SessionCardMarginY = 5;
        /// <summary>会话选中卡片圆角半径 (14px)</summary>
        public const int SessionCardRadius = 14;
        /// <summary>会话头像正圆直径 (48px)</summary>
        public const int SessionAvatarSize = 48;
        /// <summary>会话头像距左边缘间距 (30px)</summary>
        public const int SessionAvatarPaddingLeft = 30;

        /// <summary>聊天气泡文字行距额外加量 (5f)</summary>
        public const float ChatLineSpacingExtra = 5f;
        /// <summary>本人发送气泡水平内边距 (24px)</summary>
        public const int ChatBubblePadSelfH = 24;
        /// <summary>本人发送气泡垂直内边距 (18px)</summary>
        public const int ChatBubblePadSelfV = 18;
        /// <summary>对方发送气泡水平内边距 (32px)</summary>
        public const int ChatBubblePadPeerH = 32;
        /// <summary>对方发送气泡垂直内边距 (24px)</summary>
        public const int ChatBubblePadPeerV = 24;
        /// <summary>聊天流文件卡片宽度 (340px)</summary>
        public const int ChatFileCardWidth = 340;
        /// <summary>聊天流文件卡片高度 (96px)</summary>
        public const int ChatFileCardHeight = 96;
        /// <summary>聊天流时间戳文字字号 (9.0pt)</summary>
        public const float ChatTimestampFontSize = 9.0f;

        /// <summary>浮动菜单单项高度 (44px)</summary>
        public const int MenuItemHeight = 44;
        /// <summary>设置卡片内部单行高度 (54px)</summary>
        public const int SettingsRowHeight = 54;
        /// <summary>模态窗体外沿圆角半径 (16px)</summary>
        public const int RadiusModal = 16;
        /// <summary>菜单项悬停圆角半径 (8px)</summary>
        public const int RadiusMenuHover = 8;
        /// <summary>主窗体默认宽度 (1040px)</summary>
        public const int MainWindowW = 1040;
        /// <summary>主窗体默认高度 (720px)</summary>
        public const int MainWindowH = 720;

        // ==========================================
        // 动态排版与字体系统 (Typography)
        // ==========================================

        /// <summary>弹窗一级大标题字体 (18.0pt Bold)</summary>
        public static Font FontTitle { get { return Font(18.0f, FontStyle.Bold); } }
        /// <summary>卡片分组标题字体 (10.5pt Bold)</summary>
        public static Font FontSectionTitle { get { return Font(10.5f, FontStyle.Bold); } }
        /// <summary>主输入与正文字体 (11.0pt Regular)</summary>
        public static Font FontPrimary { get { return Font(11.0f, FontStyle.Regular); } }
        /// <summary>主标题加粗字体 (11.0pt Bold)</summary>
        public static Font FontPrimaryBold { get { return Font(11.0f, FontStyle.Bold); } }
        /// <summary>卡片具体设置项标题字体 (10.5pt Regular)</summary>
        public static Font FontCardItem { get { return Font(10.5f, FontStyle.Regular); } }
        /// <summary>辅助与说明字体 (9.5pt Regular)</summary>
        public static Font FontSecondary { get { return Font(9.5f, FontStyle.Regular); } }
        /// <summary>强调副文本字体 (9.5pt Bold)</summary>
        public static Font FontSecondaryBold { get { return Font(9.5f, FontStyle.Bold); } }
        /// <summary>极小注释与版本号字体 (9.0pt Regular)</summary>
        public static Font FontFootnote { get { return Font(9.0f, FontStyle.Regular); } }
        /// <summary>标准按钮文字字体 (10.0pt Bold)</summary>
        public static Font FontButton { get { return Font(10.0f, FontStyle.Bold); } }
        /// <summary>药丸按钮文字字体 (9.5pt Bold)</summary>
        public static Font FontButtonPill { get { return Font(9.5f, FontStyle.Bold); } }

        /// <summary>根据当前动态字体家族创建指定大小的标准常规字体</summary>
        public static Font Font(float emSize)
        {
            return new Font(FontFamily, emSize, FontStyle.Regular);
        }

        /// <summary>根据当前动态字体家族创建指定大小与字重的字体</summary>
        public static Font Font(float emSize, FontStyle style)
        {
            return new Font(FontFamily, emSize, style);
        }

        // ==========================================
        // GDI+ 软抗锯齿与图形排版工具
        // ==========================================

        /// <summary>
        /// 配置软化灰阶抗锯齿渲染 (TextRenderingHint.AntiAlias)，消除彩色 ClearType 杂色，呈现类似现代 Chromium 的深色模式排版
        /// </summary>
        /// <param name="g">GDI+ 画布</param>
        public static void PrepareSoftText(Graphics g)
        {
            if (g == null) return;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
        }

        /// <summary>绘制圆角矩形描边</summary>
        public static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle bounds, int radius)
        {
            if (g == null || pen == null) return;
            using (GraphicsPath path = RoundedRect(bounds, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        /// <summary>填充圆角矩形内部实体</summary>
        public static void DrawRoundedRectangle(Graphics g, Brush brush, Rectangle bounds, int radius)
        {
            if (g == null || brush == null) return;
            using (GraphicsPath path = RoundedRect(bounds, radius))
            {
                g.FillPath(brush, path);
            }
        }

        /// <summary>构建标准抗锯齿圆角矩形 GraphicsPath</summary>
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// 过滤并剔除在 YaHei UI 字体下容易呈现豆腐块 □ 的 Emoji 代理对与私有区符号
        /// </summary>
        /// <param name="text">原始文本</param>
        /// <returns>安全文本</returns>
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

                // Replacement / empty box / black square
                if (c == '\uFFFD' || c == '\u25A1' || c == '\u25A0' || c == '\u25FB' || c == '\u25FC')
                    continue;

                if (char.IsControl(c))
                    continue;

                sb.Append(c);
            }

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

        /// <summary>通过软抗锯齿安全绘制 UI 文本</summary>
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

        /// <summary>在矩形范围内水平垂直居中绘制文本</summary>
        public static void DrawUiTextCentered(Graphics g, string text, Font font, Color color, Rectangle bounds)
        {
            using (StringFormat sf = CreateUiFormat(StringAlignment.Center, StringAlignment.Center))
            {
                DrawUiText(g, text, font, color, bounds, sf);
            }
        }

        /// <summary>左对齐且垂直居中绘制文本</summary>
        public static void DrawUiTextLeft(Graphics g, string text, Font font, Color color, int x, int y, int width, int height)
        {
            Rectangle bounds = new Rectangle(x, y, width, height);
            using (StringFormat sf = CreateUiFormat(StringAlignment.Near, StringAlignment.Center))
            {
                DrawUiText(g, text, font, color, bounds, sf);
            }
        }

        /// <summary>右对齐且垂直居中绘制文本</summary>
        public static void DrawUiTextRight(Graphics g, string text, Font font, Color color, int x, int y, int width, int height)
        {
            Rectangle bounds = new Rectangle(x, y, width, height);
            using (StringFormat sf = CreateUiFormat(StringAlignment.Far, StringAlignment.Center))
            {
                DrawUiText(g, text, font, color, bounds, sf);
            }
        }

        /// <summary>测量文本在指定字体下的屏幕像素尺寸</summary>
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
