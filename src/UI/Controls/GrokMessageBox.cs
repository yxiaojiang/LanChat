using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// Grok Bot 现代暗黑清爽风格统一弹窗提示框 (彻底取代系统生硬白底 MessageBox)
    /// </summary>
    public class GrokMessageBox : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private readonly MessageBoxIcon _iconType;
        private readonly string _title;
        private readonly string _message;
        private bool _isCloseHover = false;

        public GrokMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            _message = message ?? string.Empty;
            _title = string.IsNullOrEmpty(title) ? "提示" : title;
            _iconType = icon;

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(24, 24, 26); // #18181A
            this.DoubleBuffered = true;
            this.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular);

            BuildLayout(buttons);
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

        private void BuildLayout(MessageBoxButtons buttons)
        {
            // 计算文本合适尺寸
            int baseW = 420;
            int maxTextW = baseW - 88;
            Size textSize;
            using (Graphics g = this.CreateGraphics())
            using (Font f = new Font("Microsoft YaHei", 9.5f))
            {
                SizeF sz = g.MeasureString(_message, f, maxTextW);
                textSize = new Size((int)Math.Ceiling(sz.Width), (int)Math.Ceiling(sz.Height));
            }

            int contentH = Math.Max(textSize.Height + 10, 50);
            int formH = Math.Max(180, 50 + contentH + 60);
            this.Size = new Size(baseW, formH);

            // 文本区域
            Label lbl = new Label
            {
                Text = _message,
                Location = new Point(68, 52),
                Size = new Size(maxTextW, contentH),
                ForeColor = Color.FromArgb(228, 228, 234),
                BackColor = Color.Transparent,
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular)
            };
            this.Controls.Add(lbl);

            // 按钮容器
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(16, 8, 20, 8),
                BackColor = Color.Transparent
            };

            switch (buttons)
            {
                case MessageBoxButtons.YesNo:
                    Button btnNo = CreatePillButton("否", DialogResult.No, false);
                    Button btnYes = CreatePillButton("是", DialogResult.Yes, true);
                    flow.Controls.Add(btnNo);
                    flow.Controls.Add(btnYes);
                    this.AcceptButton = btnYes;
                    this.CancelButton = btnNo;
                    break;
                case MessageBoxButtons.OKCancel:
                    Button btnCancel = CreatePillButton("取消", DialogResult.Cancel, false);
                    Button btnOk2 = CreatePillButton("确定", DialogResult.OK, true);
                    flow.Controls.Add(btnCancel);
                    flow.Controls.Add(btnOk2);
                    this.AcceptButton = btnOk2;
                    this.CancelButton = btnCancel;
                    break;
                case MessageBoxButtons.YesNoCancel:
                    Button btnCancel2 = CreatePillButton("取消", DialogResult.Cancel, false);
                    Button btnNo2 = CreatePillButton("否", DialogResult.No, false);
                    Button btnYes2 = CreatePillButton("是", DialogResult.Yes, true);
                    flow.Controls.Add(btnCancel2);
                    flow.Controls.Add(btnNo2);
                    flow.Controls.Add(btnYes2);
                    this.AcceptButton = btnYes2;
                    this.CancelButton = btnCancel2;
                    break;
                default: // OK
                    Button btnOk = CreatePillButton("确定", DialogResult.OK, true);
                    flow.Controls.Add(btnOk);
                    this.AcceptButton = btnOk;
                    break;
            }

            this.Controls.Add(flow);
        }

        private Button CreatePillButton(string text, DialogResult dr, bool isPrimary)
        {
            Button btn = new Button
            {
                Text = text,
                DialogResult = dr,
                Size = new Size(82, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold),
                Margin = new Padding(6, 0, 0, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;

            Color normalBg = isPrimary ? Color.FromArgb(121, 70, 215) : Color.FromArgb(44, 44, 48);
            Color hoverBg = isPrimary ? Color.FromArgb(142, 100, 220) : Color.FromArgb(58, 58, 64);
            Color textColor = isPrimary ? Color.White : Color.FromArgb(220, 220, 225);
            bool isHover = false;

            btn.MouseEnter += (s, e) => { isHover = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { isHover = false; btn.Invalidate(); };

            btn.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (SolidBrush b = new SolidBrush(isHover ? hoverBg : normalBg))
                using (Pen p = new Pen(isPrimary ? Color.FromArgb(142, 100, 220) : Color.FromArgb(60, 60, 66), 1f))
                {
                    MainForm.DrawRoundedRectangle(g, b, r, 16);
                    MainForm.DrawRoundedRectangle(g, p, r, 16);
                }
                using (SolidBrush tb = new SolidBrush(textColor))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(btn.Text, btn.Font, tb, new RectangleF(0, 1, btn.Width, btn.Height), sf);
                }
            };

            return btn;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.Y < 42)
            {
                Rectangle closeRect = new Rectangle(this.Width - 36, 12, 22, 22);
                if (closeRect.Contains(e.Location))
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                    return;
                }
                ReleaseCapture();
                SendMessage(this.Handle, 0x00A1, (IntPtr)0x0002, IntPtr.Zero);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Rectangle closeRect = new Rectangle(this.Width - 36, 12, 22, 22);
            bool hover = closeRect.Contains(e.Location);
            if (hover != _isCloseHover)
            {
                _isCloseHover = hover;
                Invalidate(new Rectangle(this.Width - 42, 6, 36, 36));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 边框
            using (Pen borderPen = new Pen(Color.FromArgb(46, 46, 50), 1f))
            {
                MainForm.DrawRoundedRectangle(g, borderPen, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 16);
            }

            // 顶部标题
            using (Font titleFont = new Font("Microsoft YaHei", 10.5f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(240, 240, 245)))
            {
                g.DrawString(_title, titleFont, tb, new PointF(22, 14));
            }

            // 右上角关闭叉叉
            Rectangle closeRect = new Rectangle(this.Width - 36, 12, 22, 22);
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
                int pad = 6;
                g.DrawLine(cp, closeRect.X + pad, closeRect.Y + pad, closeRect.Right - pad, closeRect.Bottom - pad);
                g.DrawLine(cp, closeRect.Right - pad, closeRect.Y + pad, closeRect.X + pad, closeRect.Bottom - pad);
            }

            // 左侧状态矢量图标
            DrawStatusIcon(g, new Rectangle(22, 54, 34, 34));
        }

        private void DrawStatusIcon(Graphics g, Rectangle r)
        {
            switch (_iconType)
            {
                case MessageBoxIcon.Error:
                    using (SolidBrush eb = new SolidBrush(Color.FromArgb(252, 44, 62)))
                    {
                        g.FillEllipse(eb, r);
                    }
                    using (Pen ep = new Pen(Color.White, 2.2f))
                    {
                        ep.StartCap = LineCap.Round;
                        ep.EndCap = LineCap.Round;
                        int pad = 9;
                        g.DrawLine(ep, r.X + pad, r.Y + pad, r.Right - pad, r.Bottom - pad);
                        g.DrawLine(ep, r.Right - pad, r.Y + pad, r.X + pad, r.Bottom - pad);
                    }
                    break;
                case MessageBoxIcon.Warning:
                    using (SolidBrush wb = new SolidBrush(Color.FromArgb(255, 160, 26)))
                    {
                        using (GraphicsPath wp = new GraphicsPath())
                        {
                            wp.AddLine(r.X + r.Width / 2f, r.Y + 2, r.Right - 2, r.Bottom - 2);
                            wp.AddLine(r.Right - 2, r.Bottom - 2, r.X + 2, r.Bottom - 2);
                            wp.CloseFigure();
                            g.FillPath(wb, wp);
                        }
                    }
                    using (SolidBrush exb = new SolidBrush(Color.FromArgb(24, 24, 26)))
                    {
                        g.FillRectangle(exb, r.X + r.Width / 2f - 1.2f, r.Y + 12, 2.4f, 9f);
                        g.FillEllipse(exb, r.X + r.Width / 2f - 1.5f, r.Y + 23, 3f, 3f);
                    }
                    break;
                case MessageBoxIcon.Question:
                    WeChatIcon.DrawMascotAvatarShape(g, r, 0, Color.FromArgb(144, 90, 251), false, Color.Transparent);
                    break;
                default: // Information
                    WeChatIcon.DrawMascotAvatarShape(g, r, 0, Color.FromArgb(77, 155, 255), false, Color.Transparent);
                    break;
            }
        }

        #region Static Helpers

        public static DialogResult Show(string text)
        {
            return Show(null, text, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static DialogResult Show(string text, string title)
        {
            return Show(null, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static DialogResult Show(string text, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(null, text, title, buttons, icon);
        }

        public static DialogResult Show(IWin32Window owner, string text, string title = "提示", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Information)
        {
            using (GrokMessageBox box = new GrokMessageBox(text, title, buttons, icon))
            {
                Form parentForm = owner as Form ?? Form.ActiveForm;
                if (parentForm != null && parentForm.Visible)
                {
                    box.StartPosition = FormStartPosition.CenterParent;
                    return box.ShowDialog(parentForm);
                }
                else
                {
                    box.StartPosition = FormStartPosition.CenterScreen;
                    return box.ShowDialog();
                }
            }
        }

        #endregion
    }
}
