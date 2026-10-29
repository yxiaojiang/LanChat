using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// Grok Bot 100% 像素级对标原生发送反馈窗口 (完全对标 SendFK.png)
    /// </summary>
    public class GrokFeedbackForm : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private TextBox _txtFeedback;
        private bool _includeDiag = true;
        private bool _isCloseHover = false;
        private Button _btnCancel;
        private Button _btnSubmit;

        public GrokFeedbackForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(520, 460); // Docs §9 Feedback 520x460
            this.BackColor = Color.FromArgb(28, 28, 31); // #1C1C1F Level1
            this.DoubleBuffered = true;
            this.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

            BuildControls();
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

        private void BuildControls()
        {
            // 多行反馈输入框
            _txtFeedback = new TextBox
            {
                Multiline = true,
                Location = new Point(28, 96),
                Size = new Size(464, 230),
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = Color.FromArgb(242, 242, 246),
                Font = new Font("Microsoft YaHei UI", 10.0f),
                BorderStyle = BorderStyle.None
            };
            this.Controls.Add(_txtFeedback);

            // 底部取消按钮
            _btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(310, 400),
                Size = new Size(82, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular)
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnCancel.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool cancelHover = false;
            _btnCancel.MouseEnter += (s, e) => { cancelHover = true; _btnCancel.Invalidate(); };
            _btnCancel.MouseLeave += (s, e) => { cancelHover = false; _btnCancel.Invalidate(); };
            _btnCancel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, _btnCancel.Width - 1, _btnCancel.Height - 1);
                using (SolidBrush b = new SolidBrush(cancelHover ? Color.FromArgb(50, 50, 56) : Color.FromArgb(38, 38, 42)))
                using (Pen p = new Pen(Color.FromArgb(60, 60, 68), 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 17);
                    MainForm.DrawRoundedRectangle(e.Graphics, p, r, 17);
                }
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(220, 220, 225)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("取消", _btnCancel.Font, tb, new RectangleF(0, 1, _btnCancel.Width, _btnCancel.Height), sf);
                }
            };
            _btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(_btnCancel);

            // 底部发送反馈按钮
            _btnSubmit = new Button
            {
                Text = "发送反馈",
                Location = new Point(402, 400),
                Size = new Size(92, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold)
            };
            _btnSubmit.FlatAppearance.BorderSize = 0;
            _btnSubmit.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnSubmit.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool submitHover = false;
            _btnSubmit.MouseEnter += (s, e) => { submitHover = true; _btnSubmit.Invalidate(); };
            _btnSubmit.MouseLeave += (s, e) => { submitHover = false; _btnSubmit.Invalidate(); };
            _btnSubmit.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, _btnSubmit.Width - 1, _btnSubmit.Height - 1);
                bool hasText = !string.IsNullOrEmpty(_txtFeedback.Text.Trim());
                Color bg = hasText ? (submitHover ? Color.FromArgb(142, 100, 220) : Color.FromArgb(121, 70, 215)) : Color.FromArgb(46, 46, 52);
                Color border = hasText ? Color.FromArgb(150, 110, 230) : Color.FromArgb(60, 60, 66);
                Color tc = hasText ? Color.White : Color.FromArgb(140, 140, 145);

                using (SolidBrush b = new SolidBrush(bg))
                using (Pen p = new Pen(border, 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 17);
                    MainForm.DrawRoundedRectangle(e.Graphics, p, r, 17);
                }
                using (SolidBrush tb = new SolidBrush(tc))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("发送反馈", _btnSubmit.Font, tb, new RectangleF(0, 1, _btnSubmit.Width, _btnSubmit.Height), sf);
                }
            };
            _btnSubmit.Click += (s, e) =>
            {
                string content = _txtFeedback.Text.Trim();
                if (string.IsNullOrEmpty(content))
                {
                    GrokMessageBox.Show(this, "请先输入您的反馈或问题描述。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                GrokMessageBox.Show(this, "感谢您的宝贵反馈！我们将持续精进优化 LanChat。", "反馈成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            };
            this.Controls.Add(_btnSubmit);

            _txtFeedback.TextChanged += (s, e) => _btnSubmit.Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Rectangle closeRect = new Rectangle(this.Width - 36, 14, 22, 22);
                if (closeRect.Contains(e.Location))
                {
                    this.Close();
                    return;
                }

                // 点击 Checkbox 切换
                Rectangle chkRect = new Rectangle(28, 352, 220, 24);
                if (chkRect.Contains(e.Location))
                {
                    _includeDiag = !_includeDiag;
                    Invalidate(chkRect);
                    return;
                }

                if (e.Y < 48)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, 0x00A1, (IntPtr)0x0002, IntPtr.Zero);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Rectangle closeRect = new Rectangle(this.Width - 36, 14, 22, 22);
            bool hover = closeRect.Contains(e.Location);
            if (hover != _isCloseHover)
            {
                _isCloseHover = hover;
                Invalidate(closeRect);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. 背景与外边框
            using (Pen borderPen = new Pen(Color.FromArgb(44, 44, 50), 1f)) // #2C2C32
            {
                MainForm.DrawRoundedRectangle(g, borderPen, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 16);
            }

            // 2. 标题 "发送反馈" (18.0pt 加粗明亮白)
            using (Font titleFont = new Font("Microsoft YaHei UI", 18f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(250, 250, 255)))
            {
                g.DrawString("发送反馈", titleFont, tb, new PointF(26, 18));
            }

            // 3. 右上角关闭按钮 (×)
            Rectangle closeRect = new Rectangle(this.Width - 36, 14, 22, 22);
            if (_isCloseHover)
            {
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(48, 48, 54)))
                {
                    g.FillEllipse(hb, closeRect);
                }
            }
            using (Pen cp = new Pen(_isCloseHover ? Color.White : Color.FromArgb(160, 160, 166), 1.5f))
            {
                cp.StartCap = LineCap.Round;
                cp.EndCap = LineCap.Round;
                int pad = 5;
                g.DrawLine(cp, closeRect.X + pad, closeRect.Y + pad, closeRect.Right - pad, closeRect.Bottom - pad);
                g.DrawLine(cp, closeRect.Right - pad, closeRect.Y + pad, closeRect.X + pad, closeRect.Bottom - pad);
            }

            // 4. 副标题 (完全对标 SendFK.png)
            using (Font subFont = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(142, 142, 147))) // #8E8E93
            {
                g.DrawString("我们重视所有反馈，无论是错误、想法还是问题", subFont, sb, new PointF(28, 58));
            }

            // 5. 文本框容器边框与占位提示
            Rectangle boxRect = new Rectangle(22, 90, 476, 212);
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(18, 18, 18)))
            using (Pen bp = new Pen(Color.FromArgb(44, 44, 50), 1f))
            {
                MainForm.DrawRoundedRectangle(g, bb, boxRect, 10);
                MainForm.DrawRoundedRectangle(g, bp, boxRect, 10);
            }

            if (string.IsNullOrEmpty(_txtFeedback.Text))
            {
                using (Font pf = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular))
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(110, 110, 116)))
                {
                    g.DrawString("问题描述与预期结果", pf, pb, new PointF(30, 98));
                }
            }

            // 6. Checkbox: "包含当前诊断与网络状态" (完全对标 SendFK.png 蓝底白勾)
            Rectangle chkBox = new Rectangle(28, 354, 18, 18);
            if (_includeDiag)
            {
                using (SolidBrush cb = new SolidBrush(Color.FromArgb(17, 109, 210)))
                {
                    MainForm.DrawRoundedRectangle(g, cb, chkBox, 4);
                }
                using (Pen cp = new Pen(Color.White, 2f))
                {
                    cp.StartCap = LineCap.Round;
                    cp.EndCap = LineCap.Round;
                    g.DrawLines(cp, new Point[] {
                        new Point(chkBox.X + 4, chkBox.Y + 9),
                        new Point(chkBox.X + 8, chkBox.Y + 13),
                        new Point(chkBox.X + 14, chkBox.Y + 5)
                    });
                }
            }
            else
            {
                using (Pen cp = new Pen(Color.FromArgb(80, 80, 88), 1.5f))
                {
                    MainForm.DrawRoundedRectangle(g, cp, chkBox, 4);
                }
            }

            using (Font lblFont = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular))
            using (SolidBrush lb = new SolidBrush(Color.FromArgb(220, 220, 225)))
            {
                g.DrawString("包含当前节点与网络诊断信息", lblFont, lb, new PointF(54, 354));
            }
        }
    }
}
