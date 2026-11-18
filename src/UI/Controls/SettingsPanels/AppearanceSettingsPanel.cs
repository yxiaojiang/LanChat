using System;
using System.Drawing;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 外观设置面板，负责选择并保存本地主题、强调色和两组字体。
    /// 由设置窗口创建和销毁，通过 SettingsContext 通知宿主即时应用外观。
    /// </summary>
    public class AppearanceSettingsPanel : SettingsPanelBase
    {
        private static readonly string[] ThemeNames = { "深色 (Grok Dark)", "石墨深色", "午夜蓝" };
        private static readonly string[] AccentNames = { "魅紫", "湖蓝", "薄荷绿", "珊瑚红", "琥珀橙" };
        private static readonly string[] FontFamilies = { "Microsoft YaHei UI", "Segoe UI", "SimSun", "SimHei", "KaiTi", "Arial", "Consolas" };
        private static readonly string[] FontNames = { "微软雅黑", "Segoe UI", "宋体", "黑体", "楷体", "Arial", "Consolas" };
        private static readonly string[] SizeLabels = { "小", "默认", "中", "大", "特大" };
        private const int PickerWidth = 190;
        private const int RowHeight = GrokTheme.SettingsRowHeight;

        /// <summary>创建外观设置面板并读取当前设置以初始化控件。</summary>
        /// <param name="context">设置窗口提供的上下文及外观应用回调。</param>
        public AppearanceSettingsPanel(SettingsContext context) : base(context)
        {
            Controls.Add(CreateSectionHeader("外观偏好", 4));
            Panel palette = BuildCard(32, new[] { "主题模式", "强调色" }, new string[2]);
            Button theme = AddPicker(palette, 0, ThemeNames[AppSettings.ThemeMode]);
            theme.Click += (s, e) => ShowPalette(theme, ThemeNames, AppSettings.ThemeMode, false);
            Button accent = AddPicker(palette, 1, GetAccentLabel(AppSettings.AccentPreset));
            accent.Click += (s, e) => ShowPalette(accent, AccentNames, AppSettings.AccentPreset, true);

            int fontTop = palette.Bottom + 22;
            Controls.Add(CreateSectionHeader("字体与排版", fontTop));
            Panel fonts = BuildCard(fontTop + 28,
                new[] { "主界面字体样式", "主界面字体大小", "聊天框字体样式", "聊天框字体大小" },
                new[] { null, "缩放会话列表、标题、搜索和输入文字。", null, "调整聊天消息气泡正文排版字号。" });
            AddFontControls(fonts, 0, false, new[] { 9.0f, 10.0f, 11.0f, 12.0f, 14.0f });
            AddFontControls(fonts, 2, true, new[] { 9.5f, 10.5f, 11.5f, 13.0f, 15.0f });
            AutoFitHeight();
        }

        // 设置行沿用设计系统的 54px 高度和 22px 左右留白。
        private Panel BuildCard(int top, string[] titles, string[] descriptions)
        {
            Panel card = CreateCardPanel(CARD_W, titles.Length * RowHeight);
            card.Top = top;
            card.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Paint += (s, e) =>
            {
                using (Font titleFont = GrokTheme.FontSectionTitle)
                using (Font descriptionFont = GrokTheme.FontSecondary)
                using (Pen divider = new Pen(ColCardDivider, 1f))
                {
                    for (int i = 0; i < titles.Length; i++)
                    {
                        int y = i * RowHeight;
                        string description = descriptions[i];
                        if (i > 0) e.Graphics.DrawLine(divider, 20, y, card.Width - 20, y);
                        int textWidth = Math.Max(20, card.Width - (description == null ? PickerWidth : 280) - 60);
                        GrokTheme.DrawUiTextLeft(e.Graphics, titles[i], titleFont, ColTextPrimary,
                            22, y + (description == null ? 0 : 7), textWidth, description == null ? RowHeight : 24);
                        if (description != null)
                            GrokTheme.DrawUiTextLeft(e.Graphics, description, descriptionFont, ColTextSecondary, 22, y + 29, textWidth, 20);
                    }
                }
            };
            Controls.Add(card);
            return card;
        }

        private Button AddPicker(Panel card, int row, string label)
        {
            Button picker = CreateDropdownPill(label, PickerWidth, 32, null);
            picker.Location = new Point(CARD_W - picker.Width - 22, row * RowHeight + (RowHeight - picker.Height) / 2);
            picker.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            card.Controls.Add(picker);
            return picker;
        }

        private static string GetAccentLabel(int preset)
        {
            Color swatch = GrokTheme.GetAccentSwatch(preset);
            return string.Format("{0} (#{1:X6})", AccentNames[preset], swatch.ToArgb() & 0xffffff);
        }

        private void ShowPalette(Button anchor, string[] names, int current, bool isAccent)
        {
            var menu = new GrokPopupMenu();
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                string label = isAccent ? GetAccentLabel(index) : names[index];
                Color swatch = isAccent ? GrokTheme.GetAccentSwatch(index) : Color.Empty;
                menu.AddItem(label, WeChatIconType.None, () =>
                {
                    if (index == current) return;
                    if (isAccent) AppSettings.AccentPreset = index;
                    else AppSettings.ThemeMode = index;
                    anchor.Text = label;
                    ApplySettings();
                }, index == current ? "✓" : null, swatch: swatch);
            }
            ShowPicker(menu, anchor);
        }

        private void AddFontControls(Panel card, int row, bool isChat, float[] sizes)
        {
            string family = isChat ? AppSettings.ChatFontFamily : AppSettings.UiFontFamily;
            string label = DisplayFontName(family);
            Button picker = AddPicker(card, row, label);
            picker.Click += (s, e) => ShowFontPicker(picker, isChat);

            var slider = new SteppedSlider(SizeLabels, sizes, isChat ? AppSettings.ChatFontSize : AppSettings.UiFontSize, value =>
            {
                if (isChat) AppSettings.ChatFontSize = value;
                else AppSettings.UiFontSize = value;
                ApplySettings();
            });
            slider.AccessibleName = isChat ? "聊天框字体大小" : "主界面字体大小";
            slider.Location = new Point(CARD_W - slider.Width - 22, (row + 1) * RowHeight + (RowHeight - slider.Height) / 2);
            slider.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            slider.VisibleChanged += (s, e) => {
                if (slider.Visible) slider.SetCurrentValue(isChat ? AppSettings.ChatFontSize : AppSettings.UiFontSize);
            };
            card.Controls.Add(slider);
        }

        private static string DisplayFontName(string family)
        {
            for (int i = 0; i < FontFamilies.Length; i++)
            {
                if (string.Equals(family, FontFamilies[i], StringComparison.OrdinalIgnoreCase)) return FontNames[i];
            }
            return family;
        }

        private void ShowFontPicker(Button anchor, bool isChat)
        {
            var menu = new GrokPopupMenu();
            string current = isChat ? AppSettings.ChatFontFamily : AppSettings.UiFontFamily;
            int count = isChat ? FontFamilies.Length : FontFamilies.Length - 1;
            for (int i = 0; i < count; i++)
            {
                string family = FontFamilies[i];
                string label = FontNames[i];
                // 系统未安装的字体会静默回退，选择列表仅提供实际可用的字体。
                try
                {
                    using (var installed = new FontFamily(family))
                    {
                        if (!installed.IsStyleAvailable(FontStyle.Regular)) continue;
                    }
                }
                catch (ArgumentException) { continue; }

                bool selected = string.Equals(current, family, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(current, label, StringComparison.OrdinalIgnoreCase);
                menu.AddItem(label, WeChatIconType.None, () =>
                {
                    if (selected) return;
                    if (isChat) AppSettings.ChatFontFamily = family;
                    else AppSettings.UiFontFamily = family;
                    anchor.Text = label;
                    ApplySettings();
                }, selected ? "✓" : null);
            }
            ShowPicker(menu, anchor);
        }

        private void ShowPicker(GrokPopupMenu menu, Button anchor)
        {
            // 模态设置窗口持有菜单，确保菜单能接收前台输入。
            menu.ShowAt(anchor.PointToScreen(new Point(0, anchor.Height + 4)), Context.OwnerWindow);
        }

        private void ApplySettings()
        {
            AppSettings.Save();
            if (Context.OnAppearanceChanged != null) Context.OnAppearanceChanged();
            Invalidate(true);
        }
    }
}
