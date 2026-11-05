using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// GrokBot 100% 像素级对标原生关于窗口 (完全对标 About.png 与 Docs §9)
    /// 继承自 GrokModalFormBase，标准尺寸 440×560 (杜绝复制按钮裁切)，采用自绘 GrokButton
    /// </summary>
    public class GrokAboutForm : GrokModalFormBase
    {
        private readonly string _version;
        private readonly string _buildDate;
        private readonly string _copyright;
        private GrokButton _btnCopy;
        private Timer _copyTimer;

        public GrokAboutForm(string version = "2.0.0")
        {
            _version = version;
            _buildDate = "2026年10月25日";
            _copyright = "版权所有 © 2026 ChaseFraser LanChat";

            this.Size = GrokTheme.SizeAbout; // 440 x 560
            this.ShowCloseButton = true;
            this.Font = GrokTheme.FontSecondary;

            BuildControls();

            _copyTimer = new Timer { Interval = 1600 };
            _copyTimer.Tick += (s, e) =>
            {
                if (_btnCopy != null)
                {
                    _btnCopy.Text = "复制版本信息";
                    _btnCopy.Role = GrokButtonRole.Secondary;
                }
                _copyTimer.Stop();
            };
        }

        private void BuildControls()
        {
            // 复制版本信息按钮 (药丸型自绘 GrokButton，位于底部舒适居中位置，杜绝任何裁切)
            int btnW = 180;
            int btnH = 38;
            int btnX = (this.ClientSize.Width - btnW) / 2;
            int btnY = 472;

            _btnCopy = new GrokButton("复制版本信息", GrokButtonRole.Secondary)
            {
                IsPill = true,
                Location = new Point(btnX, btnY),
                Size = new Size(btnW, btnH)
            };
            _btnCopy.Click += (s, e) => CopyVersionInfo();
            this.Controls.Add(_btnCopy);
        }

        private void CopyVersionInfo()
        {
            try
            {
                string info = string.Format("LanChat v{0}\n构建于 {1}\n{2}\n架构：纯原生 C# 5.0 / .NET Framework 4.0 零依赖", _version, _buildDate, _copyright);
                Clipboard.SetText(info);
                _btnCopy.Text = "已复制 ✓";
                _btnCopy.Role = GrokButtonRole.Primary;
                _copyTimer.Stop();
                _copyTimer.Start();
            }
            catch { }
        }

        protected override void OnPaintContent(Graphics g)
        {
            // 1. 顶部居中大号 Squircle 吉祥物徽章 (96x96, 圆角 26px)
            int badgeSize = 96;
            int badgeX = (this.Width - badgeSize) / 2;
            int badgeY = 56;
            Rectangle badgeRect = new Rectangle(badgeX, badgeY, badgeSize, badgeSize);

            using (SolidBrush bb = new SolidBrush(GrokTheme.Level3))
            using (Pen bPen = new Pen(GrokTheme.Border, 1f))
            {
                GrokTheme.DrawRoundedRectangle(g, bb, badgeRect, 26);
                GrokTheme.DrawRoundedRectangle(g, bPen, badgeRect, 26);
            }

            // 吉祥物头部自绘
            Rectangle mascotRect = new Rectangle(badgeX + 14, badgeY + 14, 68, 68);
            WeChatIcon.DrawMascotAvatarShape(g, mascotRect, 0, Color.FromArgb(240, 240, 244), false, Color.Transparent);

            // 2. 主标题 "LanChat" (18.0pt Bold White)
            using (Font titleFont = GrokTheme.FontTitle)
            using (SolidBrush tb = new SolidBrush(GrokTheme.TextTitle))
            {
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("LanChat", titleFont, tb, new RectangleF(0, badgeY + badgeSize + 12, this.Width, 32), sf);
                }
            }

            // 3. 副标题
            using (Font subFont = GrokTheme.FontSecondary)
            using (SolidBrush sb = new SolidBrush(GrokTheme.TextSecondary))
            {
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("极速局域网点对点互联 · 纯原生暗黑桌面终端", subFont, sb, new RectangleF(0, badgeY + badgeSize + 48, this.Width, 24), sf);
                }
            }

            // 4. 版本详情卡片 (368x156, 圆角 12px, Level2 背景)
            int cardW = 368;
            int cardH = 156;
            int cardX = (this.Width - cardW) / 2;
            int cardY = 280;
            Rectangle cardRect = new Rectangle(cardX, cardY, cardW, cardH);

            using (SolidBrush cb = new SolidBrush(GrokTheme.Level2))
            using (Pen cp = new Pen(GrokTheme.Border, 1f))
            {
                GrokTheme.DrawRoundedRectangle(g, cb, cardRect, 12);
                GrokTheme.DrawRoundedRectangle(g, cp, cardRect, 12);
            }

            // 卡片内部四行键值信息
            int lineY = cardY + 16;
            int lineH = 30;
            DrawDetailRow(g, "客户端版本", "v" + _version, cardX + 20, lineY, cardW - 40);
            DrawDetailRow(g, "系统架构", ".NET 4.0 / C# 5.0 (零外部依赖)", cardX + 20, lineY + lineH, cardW - 40);
            DrawDetailRow(g, "构建时间", _buildDate, cardX + 20, lineY + lineH * 2, cardW - 40);
            DrawDetailRow(g, "版权所有", "© 2026 ChaseFraser", cardX + 20, lineY + lineH * 3, cardW - 40);
        }

        private void DrawDetailRow(Graphics g, string label, string value, int x, int y, int w)
        {
            using (Font lf = GrokTheme.FontSecondary)
            using (SolidBrush lb = new SolidBrush(GrokTheme.TextSecondary))
            {
                g.DrawString(label, lf, lb, new PointF(x, y));
            }

            using (Font vf = GrokTheme.FontSecondaryBold)
            using (SolidBrush vb = new SolidBrush(GrokTheme.TextPrimary))
            {
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near })
                {
                    g.DrawString(value, vf, vb, new RectangleF(x, y, w, 24), sf);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_copyTimer != null)
                {
                    _copyTimer.Dispose();
                    _copyTimer = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
