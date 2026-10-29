using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// Grok Bot 100% 像素级对标原生关于窗口 (完全对标 About.png)
    /// 包含圆角悬浮主窗体、居中 Squircle 吉祥物徽章、版本信息与圆角药丸“复制版本信息”按钮
    /// </summary>
    public class GrokAboutForm : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private readonly string _version;
        private readonly string _buildDate;
        private readonly string _copyright;
        private bool _isCloseHover = false;
        private bool _isBtnHover = false;
        private bool _isCopied = false;
        private Timer _copyTimer;

        public GrokAboutForm(string version = "2.0.0")
        {
            _version = version;
            _buildDate = "2026年10月25日";
            _copyright = "版权所有 © 2026 Aritex LanChat";

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(360, 370);
            this.BackColor = Color.FromArgb(28, 28, 31); // #1C1C1F Level1
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Default;

            _copyTimer = new Timer { Interval = 1600 };
            _copyTimer.Tick += (s, e) =>
            {
                _isCopied = false;
                _copyTimer.Stop();
                Invalidate();
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                IntPtr rgn = CreateRoundRectRgn(0, 0, this.Width + 1, this.Height + 1, 16, 16);
                if (rgn != IntPtr.Zero)
                {
                    this.Region = Region.FromHrgn(rgn);
                }
            }
            catch { }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Rectangle closeRect = new Rectangle(this.Width - 38, 14, 24, 24);
                if (closeRect.Contains(e.Location))
                {
                    this.Close();
                    return;
                }

                Rectangle btnRect = new Rectangle((this.Width - 156) / 2, 290, 156, 38);
                if (btnRect.Contains(e.Location))
                {
                    CopyVersionInfo();
                    return;
                }

                ReleaseCapture();
                SendMessage(this.Handle, 0x00A1, (IntPtr)0x0002, IntPtr.Zero);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Rectangle closeRect = new Rectangle(this.Width - 38, 14, 24, 24);
            Rectangle btnRect = new Rectangle((this.Width - 156) / 2, 290, 156, 38);

            bool closeHover = closeRect.Contains(e.Location);
            bool btnHover = btnRect.Contains(e.Location);

            bool needRepaint = false;
            if (closeHover != _isCloseHover)
            {
                _isCloseHover = closeHover;
                needRepaint = true;
            }
            if (btnHover != _isBtnHover)
            {
                _isBtnHover = btnHover;
                needRepaint = true;
            }

            this.Cursor = (closeHover || btnHover) ? Cursors.Hand : Cursors.Default;
            if (needRepaint) Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isCloseHover || _isBtnHover)
            {
                _isCloseHover = false;
                _isBtnHover = false;
                this.Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }

        private void CopyVersionInfo()
        {
            try
            {
                string info = string.Format("LanChat v{0}\n构建于 {1}\n{2}", _version, _buildDate, _copyright);
                Clipboard.SetText(info);
                _isCopied = true;
                _copyTimer.Stop();
                _copyTimer.Start();
                Invalidate();
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // 1. 背景与微光外边框
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(28, 28, 31)))
            using (Pen borderPen = new Pen(Color.FromArgb(44, 44, 50), 1f)) // #2C2C32
            {
                Rectangle totalRect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
                MainForm.DrawRoundedRectangle(g, bgBrush, totalRect, 16);
                MainForm.DrawRoundedRectangle(g, borderPen, totalRect, 16);
            }

            // 2. 右上角极简叉号 (×)
            Rectangle closeRect = new Rectangle(this.Width - 38, 14, 24, 24);
            if (_isCloseHover)
            {
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(46, 46, 50)))
                {
                    g.FillEllipse(hb, closeRect);
                }
            }
            using (Pen cp = new Pen(_isCloseHover ? Color.White : Color.FromArgb(170, 170, 176), 1.5f))
            {
                cp.StartCap = LineCap.Round;
                cp.EndCap = LineCap.Round;
                int pad = 6;
                g.DrawLine(cp, closeRect.X + pad, closeRect.Y + pad, closeRect.Right - pad, closeRect.Bottom - pad);
                g.DrawLine(cp, closeRect.Right - pad, closeRect.Y + pad, closeRect.X + pad, closeRect.Bottom - pad);
            }

            // 3. 居中大号 Squircle 徽章背景 (84x84, 圆角 24px, 完全对标 About.png 顶部立体质感白吉祥物)
            int badgeSize = 84;
            int badgeX = (this.Width - badgeSize) / 2;
            int badgeY = 46;
            Rectangle badgeRect = new Rectangle(badgeX, badgeY, badgeSize, badgeSize);

            using (GraphicsPath bp = new GraphicsPath())
            {
                int r = 24;
                bp.AddArc(badgeRect.X, badgeRect.Y, r * 2, r * 2, 180, 90);
                bp.AddArc(badgeRect.Right - r * 2, badgeRect.Y, r * 2, r * 2, 270, 90);
                bp.AddArc(badgeRect.Right - r * 2, badgeRect.Bottom - r * 2, r * 2, r * 2, 0, 90);
                bp.AddArc(badgeRect.X, badgeRect.Bottom - r * 2, r * 2, r * 2, 90, 90);
                bp.CloseFigure();

                using (SolidBrush bb = new SolidBrush(Color.FromArgb(40, 40, 44)))
                using (Pen bPen = new Pen(Color.FromArgb(56, 56, 62), 1f))
                {
                    g.FillPath(bb, bp);
                    g.DrawPath(bPen, bp);
                }
            }

            // 吉祥物头部自绘 (微白色立体头部 + 倾斜双胶囊黑眼，完全对标 About.png)
            Rectangle mascotRect = new Rectangle(badgeX + 11, badgeY + 11, 62, 62);
            WeChatIcon.DrawMascotAvatarShape(g, mascotRect, 0, Color.FromArgb(240, 240, 244), false, Color.Transparent);

            // 4. 应用主标题 "LanChat" (17pt YaHei Bold White)
            using (Font titleFont = new Font("Microsoft YaHei UI", 18f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(255, 255, 255)))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("LanChat", titleFont, tb, new RectangleF(0, badgeY + badgeSize + 14, this.Width, 30), sf);
            }

            // 5. 版本与构建信息三行文本
            using (Font subFont = new Font("Microsoft YaHei UI", 10.0f, FontStyle.Regular))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(142, 142, 147))) // #8E8E93
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                float textY = badgeY + badgeSize + 52;
                g.DrawString("版本 " + _version, subFont, sb, new RectangleF(0, textY, this.Width, 22), sf);
                g.DrawString("构建于 " + _buildDate, subFont, sb, new RectangleF(0, textY + 24, this.Width, 22), sf);
                g.DrawString(_copyright, subFont, sb, new RectangleF(0, textY + 48, this.Width, 22), sf);
            }

            // 6. 底部药丸按钮: 复制版本信息 (156x38, 圆角 19px, 对标 About.png)
            Rectangle btnRect = new Rectangle((this.Width - 156) / 2, 290, 156, 38);
            Color btnBg = _isCopied ? Color.FromArgb(30, 70, 45) : (_isBtnHover ? Color.FromArgb(46, 46, 52) : Color.FromArgb(40, 40, 44));
            Color btnBorder = _isCopied ? Color.FromArgb(40, 140, 70) : (_isBtnHover ? Color.FromArgb(56, 56, 64) : Color.FromArgb(44, 44, 50));
            string btnText = _isCopied ? "已复制 ✓" : "复制版本信息";

            using (SolidBrush bBrush = new SolidBrush(btnBg))
            using (Pen bPen = new Pen(btnBorder, 1f))
            {
                MainForm.DrawRoundedRectangle(g, bBrush, btnRect, 19);
                MainForm.DrawRoundedRectangle(g, bPen, btnRect, 19);
            }

            using (Font btnFont = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular))
            using (SolidBrush tb = new SolidBrush(_isCopied ? Color.FromArgb(120, 240, 150) : Color.FromArgb(240, 240, 245)))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(btnText, btnFont, tb, btnRect, sf);
            }
        }
    }
}
