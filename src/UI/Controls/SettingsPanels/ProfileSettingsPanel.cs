using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 设置中心 - 个人主页与设备名片面板
    /// </summary>
    public class ProfileSettingsPanel : SettingsPanelBase
    {
        public ProfileSettingsPanel(SettingsContext context) : base(context)
        {
            BuildComponents();
            AutoFitHeight();
        }

        private void BuildComponents()
        {
            int curY = 4;

            Label lblProfile = CreateSectionHeader("个人名片", curY);
            this.Controls.Add(lblProfile);
            curY += 28;

            Panel cardUser = CreateCardPanel(CARD_W, 82);
            cardUser.Location = new Point(0, curY);

            int avSize = 48;
            Rectangle avRect = new Rectangle(20, (82 - avSize) / 2, avSize, avSize);
            cardUser.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                WeChatIcon.DrawMascotAvatarShape(g, avRect, 0, Color.FromArgb(252, 44, 62), true, ColCardBg);

                using (Font nf = new Font(GrokTheme.FontFamily, 11.0f, FontStyle.Bold))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush nb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                {
                    string nick = Context.NetService != null ? Context.NetService.NickName : Environment.UserName;
                    g.DrawString(nick, nf, nb, new PointF(80, 20));
                    g.DrawString("主机: " + Environment.MachineName + " · 局域网在线", sf, sb, new PointF(80, 46));
                }
            };

            Button btnEditNick = CreateActionLink("修改昵称 >", 96, 28, () =>
            {
                if (Context.OnShowChangeNick != null)
                {
                    Context.OnShowChangeNick();
                    cardUser.Invalidate();
                }
            });
            btnEditNick.Location = new Point(CARD_W - btnEditNick.Width - 20, (82 - 32) / 2);
            cardUser.Controls.Add(btnEditNick);

            this.Controls.Add(cardUser);
            curY += 82 + 22;

            Label lblIdentity = CreateSectionHeader("设备标识", curY);
            this.Controls.Add(lblIdentity);
            curY += 28;

            Panel cardDev = CreateCardPanel(CARD_W, 108);
            cardDev.Location = new Point(0, curY);

            cardDev.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    g.DrawString("节点识别码 (Node UUID)", f, tb, new PointF(20, 9));
                    string id = Context.NetService != null ? Context.NetService.LocalId : "0000-0000";
                    g.DrawString(id, sf, sb, new PointF(20, 29));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    g.DrawString("操作系统环境", f, tb, new PointF(20, 54 + 9));
                    g.DrawString(Environment.OSVersion.VersionString + " (" + (Environment.Is64BitOperatingSystem ? "64位" : "32位") + ")", sf, sb, new PointF(20, 54 + 29));
                }
            };

            Button btnCopyId = CreateActionLink("复制 ID >", 86, 28, null);
            btnCopyId.Click += (s, e) =>
            {
                try
                {
                    string id = Context.NetService != null ? Context.NetService.LocalId : "";
                    Clipboard.SetText(id);
                    btnCopyId.Text = "已复制 ✓";
                }
                catch { }
            };
            btnCopyId.Location = new Point(CARD_W - btnCopyId.Width - 20, (54 - 32) / 2);
            cardDev.Controls.Add(btnCopyId);

            this.Controls.Add(cardDev);
        }
    }
}
