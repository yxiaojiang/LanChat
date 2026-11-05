using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 设置中心 - 关于与更新面板
    /// </summary>
    public class AboutSettingsPanel : SettingsPanelBase
    {
        /// <summary>
        /// 初始化关于与更新面板
        /// </summary>
        /// <param name="context">全局设置上下文宿主</param>
        public AboutSettingsPanel(SettingsContext context) : base(context)
        {
            BuildComponents();
            AutoFitHeight();
        }

        private void BuildComponents()
        {
            int curY = 4;

            Label lblAbout = CreateSectionHeader("关于产品", curY);
            this.Controls.Add(lblAbout);
            curY += 28;

            Panel cardAbout = CreateCardPanel(CARD_W, 140);
            cardAbout.Location = new Point(0, curY);

            cardAbout.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int bSize = 56;
                Rectangle bRect = new Rectangle(20, 20, bSize, bSize);
                using (SolidBrush bb = new SolidBrush(Color.FromArgb(36, 36, 42)))
                using (Pen bp = new Pen(Color.FromArgb(52, 52, 60), 1f))
                {
                    MainForm.DrawRoundedRectangle(g, bb, bRect, 14);
                    MainForm.DrawRoundedRectangle(g, bp, bRect, 14);
                }
                Rectangle mRect = new Rectangle(28, 28, 40, 40);
                WeChatIcon.DrawMascotAvatarShape(g, mRect, 0, Color.FromArgb(245, 245, 250), false, Color.Transparent);

                using (Font tf = new Font(GrokTheme.FontFamily, 12f, FontStyle.Bold))
                using (Font mf = new Font(GrokTheme.FontFamily, 9.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                {
                    g.DrawString("LanChat 局域网极速通讯", tf, tb, new PointF(90, 20));
                    g.DrawString("版本 v" + MainForm.APP_VERSION + " · WorkBuddy 1:1 对标现代化极简暗黑视效", mf, sb, new PointF(90, 44));
                    g.DrawString("原生高性能零冗余依赖架构 · Copyright © 2026 LanChat Team", sf, sb, new PointF(90, 64));
                }
            };

            Button btnCheck = CreateActionLink("检查更新 >", 96, 28, () =>
            {
                if (Context.OnCheckUpdate != null) Context.OnCheckUpdate();
            });
            btnCheck.Location = new Point(CARD_W - btnCheck.Width - 20, 24);
            cardAbout.Controls.Add(btnCheck);

            Button btnCopyDiag = CreateActionLink("复制诊断信息 >", 116, 28, null);
            btnCopyDiag.Click += (s, e) =>
            {
                try
                {
                    string info = string.Format("LanChat v{0}\nOS: {1}\nMachine: {2}\nCLR: {3}",
                        MainForm.APP_VERSION, Environment.OSVersion, Environment.MachineName, Environment.Version);
                    Clipboard.SetText(info);
                    btnCopyDiag.Text = "已复制 ✓";
                }
                catch { }
            };
            btnCopyDiag.Location = new Point(CARD_W - btnCopyDiag.Width - 20, 70);
            cardAbout.Controls.Add(btnCopyDiag);

            this.Controls.Add(cardAbout);
            curY += 140 + 22;

            Label lblFeedback = CreateSectionHeader("支持与反馈", curY);
            this.Controls.Add(lblFeedback);
            curY += 28;

            Panel cardFk = CreateCardPanel(CARD_W, 64);
            cardFk.Location = new Point(0, curY);

            cardFk.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                {
                    g.DrawString("意见与问题反馈", f, tb, new PointF(20, 14));
                    g.DrawString("帮助我们将 LanChat 打磨得更贴合你的直觉与习惯。", sf, sb, new PointF(20, 34));
                }
            };

            Button btnOpenFk = CreateActionLink("发送反馈 >", 96, 28, () =>
            {
                using (GrokFeedbackForm fk = new GrokFeedbackForm())
                {
                    IWin32Window owner = Context.OwnerWindow ?? this;
                    fk.ShowDialog(owner);
                }
            });
            btnOpenFk.Location = new Point(CARD_W - btnOpenFk.Width - 20, (64 - 32) / 2);
            cardFk.Controls.Add(btnOpenFk);

            this.Controls.Add(cardFk);
        }
    }
}
