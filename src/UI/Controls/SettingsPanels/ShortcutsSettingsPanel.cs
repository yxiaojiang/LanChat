using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 设置中心 - 快捷键面板
    /// </summary>
    public class ShortcutsSettingsPanel : SettingsPanelBase
    {
        /// <summary>
        /// 初始化快捷键面板
        /// </summary>
        /// <param name="context">全局设置上下文宿主</param>
        public ShortcutsSettingsPanel(SettingsContext context) : base(context)
        {
            BuildComponents();
            AutoFitHeight();
        }

        private void BuildComponents()
        {
            int curY = 4;

            Label lblKey = CreateSectionHeader("快捷操作", curY);
            this.Controls.Add(lblKey);
            curY += 28;

            Panel cardKey = CreateCardPanel(CARD_W, 216);
            cardKey.Location = new Point(0, curY);

            cardKey.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font kf = new Font("Consolas", 9.5f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush kb = new SolidBrush(Color.FromArgb(170, 170, 185)))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    string[] titles = new string[] { "发送消息", "多行换行输入", "快速搜索联系人", "清空当前聊天" };
                    string[] shortcuts = new string[] { "Enter", "Shift + Enter", "Ctrl + F", "Ctrl + L" };

                    for (int i = 0; i < 4; i++)
                    {
                        int y = i * 54;
                        g.DrawString(titles[i], f, tb, new PointF(20, y + (54 - f.GetHeight(g)) / 2f));
                        if (i < 3) g.DrawLine(sep, 16, y + 54, CARD_W - 16, y + 54);

                        // 快捷键芯片徽章
                        SizeF ssz = g.MeasureString(shortcuts[i], kf);
                        int bw = (int)ssz.Width + 16;
                        Rectangle br = new Rectangle(CARD_W - bw - 20, y + (54 - 24) / 2, bw, 24);
                        using (SolidBrush bb = new SolidBrush(ColPillBg))
                        using (Pen bp = new Pen(ColPillBorder, 1f))
                        {
                            MainForm.DrawRoundedRectangle(g, bb, br, 6);
                            MainForm.DrawRoundedRectangle(g, bp, br, 6);
                        }
                        g.DrawString(shortcuts[i], kf, kb, new PointF(br.X + 8, br.Y + (24 - kf.GetHeight(g)) / 2f));
                    }
                }
            };

            this.Controls.Add(cardKey);
        }
    }
}
