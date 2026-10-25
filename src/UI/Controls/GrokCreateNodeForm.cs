using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// Grok Bot 100% 像素级对标原生创建新节点/新Bot窗口 (完全对标 CreateNewBot.png)
    /// 包含居中动态吉祥物预览、起名胶囊输入框、14 种有机吉祥物形态选择网格、11 种调色盘与圆角操作按钮
    /// </summary>
    public class GrokCreateNodeForm : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private TextBox _txtName;
        private Button _btnCancel;
        private Button _btnCreate;

        private int _selectedShape = 1; // 默认云朵有机体 (Alist 同款)
        private int _selectedColorIndex = 3; // 默认经典暖橙色 (对标 CreateNewBot.png)
        private bool _isCloseHover = false;

        public string NodeName { get; private set; }
        public int ShapeIndex { get { return _selectedShape; } }
        public Color ChosenColor { get { return _colors[_selectedColorIndex]; } }

        // 11 种标准调色盘颜色 (完全对标 CreateNewBot.png 调色盘圆点)
        private static readonly Color[] _colors = new Color[]
        {
            Color.FromArgb(255, 255, 255), // 白
            Color.FromArgb(147, 98, 61),   // 棕 (对标图中默认选中棕)
            Color.FromArgb(252, 44, 62),   // 红
            Color.FromArgb(255, 107, 34),  // 橙
            Color.FromArgb(255, 160, 26),  // 琥珀黄
            Color.FromArgb(31, 214, 126),  // 亮绿
            Color.FromArgb(25, 191, 160),  // 青
            Color.FromArgb(46, 147, 248),  // 蓝
            Color.FromArgb(144, 90, 251),  // 魅紫
            Color.FromArgb(255, 61, 130),  // 亮粉
            Color.FromArgb(126, 128, 134)  // 烟灰
        };

        public GrokCreateNodeForm(string initialName = "")
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(450, 580);
            this.BackColor = Color.FromArgb(24, 24, 26); // #18181A
            this.DoubleBuffered = true;
            this.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular);

            BuildControls(initialName);
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

        private void BuildControls(string initialName)
        {
            // 给你的 Bot 起个名字 (大圆角暗黑胶囊输入框)
            _txtName = new TextBox
            {
                Text = initialName,
                Location = new Point(48, 187),
                Size = new Size(354, 24),
                BackColor = Color.FromArgb(20, 20, 22),
                ForeColor = Color.FromArgb(240, 240, 245),
                Font = new Font("Microsoft YaHei", 10.5f),
                BorderStyle = BorderStyle.None,
                TextAlign = HorizontalAlignment.Center
            };
            this.Controls.Add(_txtName);

            // 底部取消按钮
            _btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(252, 520),
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
                using (SolidBrush b = new SolidBrush(cancelHover ? Color.FromArgb(50, 50, 56) : Color.FromArgb(36, 36, 40)))
                using (Pen p = new Pen(Color.FromArgb(56, 56, 62), 1f))
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

            // 底部创建按钮
            _btnCreate = new Button
            {
                Text = "创建",
                DialogResult = DialogResult.OK,
                Location = new Point(344, 520),
                Size = new Size(82, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold)
            };
            _btnCreate.FlatAppearance.BorderSize = 0;
            _btnCreate.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnCreate.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool createHover = false;
            _btnCreate.MouseEnter += (s, e) => { createHover = true; _btnCreate.Invalidate(); };
            _btnCreate.MouseLeave += (s, e) => { createHover = false; _btnCreate.Invalidate(); };
            _btnCreate.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, _btnCreate.Width - 1, _btnCreate.Height - 1);
                bool hasText = !string.IsNullOrEmpty(_txtName.Text.Trim());
                Color bg = hasText ? (createHover ? Color.FromArgb(142, 100, 220) : Color.FromArgb(121, 70, 215)) : Color.FromArgb(46, 46, 52);
                Color border = hasText ? Color.FromArgb(150, 110, 230) : Color.FromArgb(58, 58, 64);
                Color tc = hasText ? Color.White : Color.FromArgb(140, 140, 146);

                using (SolidBrush b = new SolidBrush(bg))
                using (Pen p = new Pen(border, 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 17);
                    MainForm.DrawRoundedRectangle(e.Graphics, p, r, 17);
                }
                using (SolidBrush tb = new SolidBrush(tc))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("创建", _btnCreate.Font, tb, new RectangleF(0, 1, _btnCreate.Width, _btnCreate.Height), sf);
                }
            };
            _btnCreate.Click += (s, e) =>
            {
                string text = _txtName.Text.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    GrokMessageBox.Show(this, "请为您的节点起一个名字或输入直连 IP 地址。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                NodeName = text;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(_btnCreate);

            _txtName.TextChanged += (s, e) => _btnCreate.Invalidate();
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

                // 1. 检查形状网格点击 (14 种形态，对标 CreateNewBot.png)
                // Row 0: 5 shapes (y: 236), Row 1: 5 shapes (y: 290), Row 2: 4 shapes (y: 344)
                int shapeIdx = -1;
                int sy0 = 236;
                int sy1 = 290;
                int sy2 = 344;
                int sSize = 42;

                // Row 0
                for (int i = 0; i < 5; i++)
                {
                    int sx = 55 + i * 70;
                    Rectangle sr = new Rectangle(sx - 4, sy0 - 4, sSize + 8, sSize + 8);
                    if (sr.Contains(e.Location)) { shapeIdx = i; break; }
                }
                // Row 1
                if (shapeIdx == -1)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        int sx = 55 + i * 70;
                        Rectangle sr = new Rectangle(sx - 4, sy1 - 4, sSize + 8, sSize + 8);
                        if (sr.Contains(e.Location)) { shapeIdx = 5 + i; break; }
                    }
                }
                // Row 2 (4 shapes centered)
                if (shapeIdx == -1)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int sx = 90 + i * 70;
                        Rectangle sr = new Rectangle(sx - 4, sy2 - 4, sSize + 8, sSize + 8);
                        if (sr.Contains(e.Location)) { shapeIdx = 10 + i; break; }
                    }
                }

                if (shapeIdx != -1)
                {
                    _selectedShape = shapeIdx;
                    Invalidate();
                    return;
                }

                // 2. 检查颜色调色板圆点点击 (11 色，y: 418)
                int cy = 418;
                int cSize = 24;
                int startX = (this.Width - (11 * cSize + 10 * 12)) / 2;
                for (int i = 0; i < _colors.Length; i++)
                {
                    int cx = startX + i * (cSize + 12);
                    Rectangle cr = new Rectangle(cx - 3, cy - 3, cSize + 6, cSize + 6);
                    if (cr.Contains(e.Location))
                    {
                        _selectedColorIndex = i;
                        Invalidate();
                        return;
                    }
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

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. 背景与微光外边框
            using (Pen borderPen = new Pen(Color.FromArgb(46, 46, 50), 1f))
            {
                MainForm.DrawRoundedRectangle(g, borderPen, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 16);
            }

            // 2. 标题 "创建新 Bot" (完全对标 CreateNewBot.png)
            using (Font titleFont = new Font("Microsoft YaHei", 13.5f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(245, 245, 250)))
            {
                g.DrawString("创建新 Bot", titleFont, tb, new PointF(24, 18));
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

            // 4. 居中大号吉祥物 Hero 实时动态预览 (96x96, 完全对标 CreateNewBot.png 顶部棕色云朵)
            int heroSize = 96;
            int heroX = (this.Width - heroSize) / 2;
            int heroY = 64;
            Rectangle heroRect = new Rectangle(heroX, heroY, heroSize, heroSize);
            WeChatIcon.DrawMascotAvatarShape(g, heroRect, _selectedShape, _colors[_selectedColorIndex], false, Color.Transparent);

            // 5. 起名输入胶囊背景框 (高 44px, 圆角 12px)
            Rectangle nameBox = new Rectangle(34, 178, 382, 42);
            using (SolidBrush nb = new SolidBrush(Color.FromArgb(20, 20, 22)))
            using (Pen np = new Pen(_txtName.Focused ? Color.FromArgb(142, 100, 220) : Color.FromArgb(44, 44, 50), 1f))
            {
                MainForm.DrawRoundedRectangle(g, nb, nameBox, 12);
                MainForm.DrawRoundedRectangle(g, np, nameBox, 12);
            }

            if (string.IsNullOrEmpty(_txtName.Text))
            {
                using (Font pf = new Font("Microsoft YaHei", 10.0f, FontStyle.Regular))
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(120, 120, 126)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("给你的 Bot 起个名字 (或直接输入 IP)", pf, pb, nameBox, sf);
                }
            }

            // 6. 14 种有机形态选项选择网格 (三行: 5, 5, 4)
            Color previewColor = _colors[_selectedColorIndex];
            int sy0 = 236;
            int sy1 = 290;
            int sy2 = 344;
            int sSize = 40;

            // 第一行 (0..4)
            for (int i = 0; i < 5; i++)
            {
                int sx = 55 + i * 70;
                DrawShapeOption(g, i, sx, sy0, sSize, previewColor);
            }
            // 第二行 (5..9)
            for (int i = 0; i < 5; i++)
            {
                int sx = 55 + i * 70;
                DrawShapeOption(g, 5 + i, sx, sy1, sSize, previewColor);
            }
            // 第三行 (10..13)
            for (int i = 0; i < 4; i++)
            {
                int sx = 90 + i * 70;
                DrawShapeOption(g, 10 + i, sx, sy2, sSize, previewColor);
            }

            // 7. 调色盘圆点行 (11 色, 完全对标 CreateNewBot.png)
            int cy = 418;
            int cSize = 24;
            int startX = (this.Width - (11 * cSize + 10 * 12)) / 2;
            for (int i = 0; i < _colors.Length; i++)
            {
                int cx = startX + i * (cSize + 12);
                Rectangle cr = new Rectangle(cx, cy, cSize, cSize);

                if (_selectedColorIndex == i)
                {
                    // 选中色外圈大光环
                    using (Pen ringPen = new Pen(Color.FromArgb(180, 180, 190), 1.8f))
                    {
                        g.DrawEllipse(ringPen, cx - 4, cy - 4, cSize + 8, cSize + 8);
                    }
                }

                using (SolidBrush cb = new SolidBrush(_colors[i]))
                {
                    g.FillEllipse(cb, cr);
                }

                // 白色圆点需要细微暗边圈
                if (i == 0)
                {
                    using (Pen wp = new Pen(Color.FromArgb(160, 160, 166), 1f))
                    {
                        g.DrawEllipse(wp, cr);
                    }
                }
            }
        }

        private void DrawShapeOption(Graphics g, int idx, int x, int y, int size, Color color)
        {
            Rectangle r = new Rectangle(x, y, size, size);

            if (_selectedShape == idx)
            {
                // 选中形态外圈双层发光光晕 (对标 CreateNewBot.png 第二行第2个有机形状的外框)
                using (Pen selPen = new Pen(Color.FromArgb(90, 90, 98), 1.8f))
                {
                    Rectangle selRect = new Rectangle(x - 5, y - 5, size + 10, size + 10);
                    MainForm.DrawRoundedRectangle(g, selPen, selRect, 14);
                }
            }

            WeChatIcon.DrawMascotAvatarShape(g, r, idx, color, false, Color.Transparent);
        }
    }
}
