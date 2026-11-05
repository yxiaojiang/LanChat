using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 设置中心 - 外观面板 (主题模式、强调色、界面与聊天字体排版)
    /// </summary>
    public class AppearanceSettingsPanel : SettingsPanelBase
    {
        public AppearanceSettingsPanel(SettingsContext context) : base(context)
        {
            BuildComponents();
            AutoFitHeight();
        }

        private void BuildComponents()
        {
            int curY = 4;

            Label lblApp = CreateSectionHeader("外观偏好", curY);
            this.Controls.Add(lblApp);
            curY += 28;

            Panel cardTheme = CreateCardPanel(CARD_W, 108);
            cardTheme.Location = new Point(0, curY);

            cardTheme.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    g.DrawString("主题模式", f, tb, new PointF(20, (54 - f.GetHeight(g)) / 2f));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    g.DrawString("强调色", f, tb, new PointF(20, 54 + (54 - f.GetHeight(g)) / 2f));
                }
            };

            Button btnTheme = CreateDropdownPill("深色 (Grok Dark)  ∨", 150, 28, null);
            btnTheme.Location = new Point(CARD_W - btnTheme.Width - 20, (54 - 32) / 2);
            cardTheme.Controls.Add(btnTheme);

            Button btnAccent = CreateDropdownPill("魅紫 (#8E64DC)  ∨", 150, 28, null);
            btnAccent.Location = new Point(CARD_W - btnAccent.Width - 20, 54 + (54 - 32) / 2);
            cardTheme.Controls.Add(btnAccent);

            this.Controls.Add(cardTheme);
            curY += 108 + 22;

            Label lblFontSec = CreateSectionHeader("字体与排版", curY);
            this.Controls.Add(lblFontSec);
            curY += 28;

            Panel cardFont = CreateCardPanel(CARD_W, 216);
            cardFont.Location = new Point(0, curY);

            cardFont.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    // Row 1: 主界面字体样式
                    g.DrawString("主界面字体样式", f, tb, new PointF(20, (54 - f.GetHeight(g)) / 2f));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2: 主界面字体大小
                    g.DrawString("主界面字体大小", f, tb, new PointF(20, 54 + 9));
                    g.DrawString("缩放主窗体、按钮、侧边栏文字比例。", sf, sb, new PointF(20, 54 + 29));
                    g.DrawLine(sep, 16, 108, CARD_W - 16, 108);

                    // Row 3: 聊天框字体样式
                    g.DrawString("聊天框字体样式", f, tb, new PointF(20, 108 + (54 - f.GetHeight(g)) / 2f));
                    g.DrawLine(sep, 16, 162, CARD_W - 16, 162);

                    // Row 4: 聊天框字体大小
                    g.DrawString("聊天框字体大小", f, tb, new PointF(20, 162 + 9));
                    g.DrawString("调整聊天消息气泡正文排版字号。", sf, sb, new PointF(20, 162 + 29));
                }
            };

            // Row 1 主界面字体下拉
            Button btnUiFont = CreateDropdownPill(GetUiFontDisplayName() + "  ∨", 150, 28, null);
            btnUiFont.Click += (s, e) => ShowUiFontPicker(btnUiFont);
            btnUiFont.Location = new Point(CARD_W - btnUiFont.Width - 20, (54 - 32) / 2);
            cardFont.Controls.Add(btnUiFont);

            // Row 2 主界面字体滑块
            string[] uiLabels = new string[] { "小", "默认", "中", "大", "特大" };
            float[] uiSizes = new float[] { 9.0f, 10.0f, 11.0f, 12.0f, 14.0f };
            SteppedSlider sliderUi = new SteppedSlider(uiLabels, uiSizes, AppSettings.UiFontSize, (val) =>
            {
                AppSettings.UiFontSize = val;
                AppSettings.Save();
                if (Context.OnFontChanged != null) Context.OnFontChanged();
            });
            sliderUi.Location = new Point(CARD_W - sliderUi.Width - 20, 54 + (54 - 34) / 2);
            cardFont.Controls.Add(sliderUi);

            // Row 3 聊天框字体下拉
            Button btnChatFont = CreateDropdownPill(GetChatFontDisplayName() + "  ∨", 150, 28, null);
            btnChatFont.Click += (s, e) => ShowChatFontPicker(btnChatFont);
            btnChatFont.Location = new Point(CARD_W - btnChatFont.Width - 20, 108 + (54 - 32) / 2);
            cardFont.Controls.Add(btnChatFont);

            // Row 4 聊天框字体滑块
            string[] chatLabels = new string[] { "小", "默认", "中", "大", "特大" };
            float[] chatSizes = new float[] { 9.5f, 10.5f, 11.5f, 13.0f, 15.0f };
            SteppedSlider sliderChat = new SteppedSlider(chatLabels, chatSizes, AppSettings.ChatFontSize, (val) =>
            {
                AppSettings.ChatFontSize = val;
                AppSettings.Save();
                if (Context.OnFontChanged != null) Context.OnFontChanged();
            });
            sliderChat.Location = new Point(CARD_W - sliderChat.Width - 20, 162 + (54 - 34) / 2);
            cardFont.Controls.Add(sliderChat);

            this.Controls.Add(cardFont);
        }

        private string GetUiFontDisplayName()
        {
            string f = AppSettings.UiFontFamily;
            if (string.IsNullOrEmpty(f) || f == "Microsoft YaHei UI") return "微软雅黑";
            return f;
        }

        private string GetChatFontDisplayName()
        {
            string f = AppSettings.ChatFontFamily;
            if (string.IsNullOrEmpty(f) || f == "Microsoft YaHei UI") return "微软雅黑";
            return f;
        }

        private void ShowUiFontPicker(Button anchor)
        {
            Point pt = anchor.PointToScreen(new Point(0, anchor.Height + 4));
            GrokPopupMenu menu = new GrokPopupMenu();
            string[] fontNames = new string[] { "Microsoft YaHei UI", "Segoe UI", "宋体", "黑体", "楷体", "Arial" };
            string[] fontLabels = new string[] { "微软雅黑 (默认)", "Segoe UI", "宋体 (SimSun)", "黑体 (SimHei)", "楷体 (KaiTi)", "Arial" };
            for (int i = 0; i < fontNames.Length; i++)
            {
                string fName = fontNames[i];
                string fLabel = fontLabels[i];
                bool isCur = string.Equals(AppSettings.UiFontFamily, fName, StringComparison.OrdinalIgnoreCase);
                menu.AddItem(fLabel, WeChatIconType.None, () =>
                {
                    AppSettings.UiFontFamily = fName;
                    AppSettings.Save();
                    anchor.Text = (fName == "Microsoft YaHei UI" ? "微软雅黑" : fName) + "  ∨";
                    anchor.Invalidate();
                    if (Context.OnFontChanged != null) Context.OnFontChanged();
                }, isCur ? "✓" : "");
            }
            menu.ShowAt(pt);
        }

        private void ShowChatFontPicker(Button anchor)
        {
            Point pt = anchor.PointToScreen(new Point(0, anchor.Height + 4));
            GrokPopupMenu menu = new GrokPopupMenu();
            string[] fontNames = new string[] { "Microsoft YaHei UI", "Segoe UI", "Consolas", "宋体", "黑体", "楷体", "Arial" };
            string[] fontLabels = new string[] { "微软雅黑 (默认)", "Segoe UI", "Consolas (代码风)", "宋体 (SimSun)", "黑体 (SimHei)", "楷体 (KaiTi)", "Arial" };
            for (int i = 0; i < fontNames.Length; i++)
            {
                string fName = fontNames[i];
                string fLabel = fontLabels[i];
                bool isCur = string.Equals(AppSettings.ChatFontFamily, fName, StringComparison.OrdinalIgnoreCase);
                menu.AddItem(fLabel, WeChatIconType.None, () =>
                {
                    AppSettings.ChatFontFamily = fName;
                    AppSettings.Save();
                    anchor.Text = (fName == "Microsoft YaHei UI" ? "微软雅黑" : fName) + "  ∨";
                    anchor.Invalidate();
                    if (Context.OnFontChanged != null) Context.OnFontChanged();
                }, isCur ? "✓" : "");
            }
            menu.ShowAt(pt);
        }
    }
}
