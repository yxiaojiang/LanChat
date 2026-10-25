using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// Grok Bot 现代暗黑清爽风格昵称修改窗口
    /// </summary>
    public class GrokChangeNickForm : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private TextBox _txtNick;
        private bool _isCloseHover = false;
        private Button _btnCancel;
        private Button _btnSave;
        public string NewNickName { get; private set; }

        public GrokChangeNickForm(string currentNick)
        {
            NewNickName = currentNick ?? string.Empty;

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(420, 250);
            this.BackColor = Color.FromArgb(24, 24, 26); // #18181A
            this.DoubleBuffered = true;
            this.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular);

            BuildControls(currentNick);
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

        private void BuildControls(string currentNick)
        {
            _txtNick = new TextBox
            {
                Text = currentNick,
                Location = new Point(36, 126),
                Size = new Size(348, 26),
                BackColor = Color.FromArgb(16, 16, 18),
                ForeColor = Color.FromArgb(240, 240, 245),
                Font = new Font("Microsoft YaHei", 10.5f),
                BorderStyle = BorderStyle.None
            };
            this.Controls.Add(_txtNick);

            // 底部取消按钮
            _btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(216, 188),
                Size = new Size(82, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular)
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
            _btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(_btnCancel);

            // 底部保存按钮
            _btnSave = new Button
            {
                Text = "保存",
                DialogResult = DialogResult.OK,
                Location = new Point(308, 188),
                Size = new Size(84, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold)
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnSave.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool saveHover = false;
            _btnSave.MouseEnter += (s, e) => { saveHover = true; _btnSave.Invalidate(); };
            _btnSave.MouseLeave += (s, e) => { saveHover = false; _btnSave.Invalidate(); };
            _btnSave.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, _btnSave.Width - 1, _btnSave.Height - 1);
                using (SolidBrush b = new SolidBrush(saveHover ? Color.FromArgb(142, 100, 220) : Color.FromArgb(121, 70, 215)))
                using (Pen p = new Pen(Color.FromArgb(142, 100, 220), 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 17);
                    MainForm.DrawRoundedRectangle(e.Graphics, p, r, 17);
                }
                using (SolidBrush tb = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("保存", _btnSave.Font, tb, new RectangleF(0, 1, _btnSave.Width, _btnSave.Height), sf);
                }
            };
            _btnSave.Click += (s, e) =>
            {
                string text = _txtNick.Text.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    GrokMessageBox.Show(this, "昵称不能为空，请输入有效的名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                NewNickName = text;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(_btnSave);

            this.AcceptButton = _btnSave;
            this.CancelButton = _btnCancel;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Rectangle closeRect = new Rectangle(this.Width - 36, 14, 22, 22);
                if (closeRect.Contains(e.Location))
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                    return;
                }

                if (e.Y < 46)
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

            // 1. 背景边框
            using (Pen borderPen = new Pen(Color.FromArgb(46, 46, 50), 1f))
            {
                MainForm.DrawRoundedRectangle(g, borderPen, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 16);
            }

            // 2. 标题 "修改我的昵称"
            using (Font titleFont = new Font("Microsoft YaHei", 12f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(245, 245, 250)))
            {
                g.DrawString("修改我的昵称", titleFont, tb, new PointF(24, 18));
            }

            // 3. 右上角极简叉号 (×)
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

            // 4. 吉祥物头像微标 (38x38) 与提示
            Rectangle avRect = new Rectangle(26, 58, 38, 38);
            WeChatIcon.DrawMascotAvatarShape(g, avRect, 0, Color.FromArgb(252, 44, 62), true, Color.FromArgb(24, 24, 26));

            using (Font tipFont = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(170, 170, 176)))
            {
                g.DrawString("在局域网内向其他节点展示您的身份名称:", tipFont, sb, new PointF(74, 68));
            }

            // 5. 输入框暗黑容器 (高 40px, 圆角 8px)
            Rectangle boxRect = new Rectangle(24, 118, 372, 42);
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(16, 16, 18)))
            using (Pen bp = new Pen(_txtNick.Focused ? Color.FromArgb(142, 100, 220) : Color.FromArgb(46, 46, 52), 1f))
            {
                MainForm.DrawRoundedRectangle(g, bb, boxRect, 8);
                MainForm.DrawRoundedRectangle(g, bp, boxRect, 8);
            }
        }
    }
}
