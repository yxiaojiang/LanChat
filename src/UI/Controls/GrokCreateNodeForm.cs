using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// GrokBot 100% 像素级对标原生创建新节点窗口 (完全对标 CreateNewBot.png 与 Docs §9)
    /// 继承自 GrokModalFormBase，标准尺寸 680×620 (基准度量)，采用自绘 GrokButton
    /// </summary>
    public class GrokCreateNodeForm : GrokModalFormBase
    {
        private TextBox _txtName;
        private GrokButton _btnCancel;
        private GrokButton _btnCreate;

        private int _selectedShape = 1; // 默认云朵形态
        private int _selectedColorIndex = 3; // 默认暖橙色

        /// <summary>获取新创建节点的名称</summary>
        public string NodeName { get; private set; }
        /// <summary>获取选中的吉祥物外观形态索引</summary>
        public int ShapeIndex { get { return _selectedShape; } }
        /// <summary>获取选中的吉祥物配色</summary>
        public Color ChosenColor { get { return _colors[_selectedColorIndex]; } }

        // 11 种标准调色盘颜色
        private static readonly Color[] _colors = new Color[]
        {
            Color.FromArgb(255, 255, 255), // 白
            Color.FromArgb(147, 98, 61),   // 棕
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

        /// <summary>
        /// 初始化新建节点模态对话框
        /// </summary>
        /// <param name="initialName">初始推荐节点名称</param>
        public GrokCreateNodeForm(string initialName = "")
        {
            this.Size = GrokTheme.SizeCreateNode; // 680 x 620
            this.ShowCloseButton = true;
            this.Font = GrokTheme.FontSecondary;

            BuildControls(initialName);
        }

        private void BuildControls(string initialName)
        {
            int inputW = 440;
            int inputX = (this.ClientSize.Width - inputW) / 2;

            _txtName = new TextBox
            {
                Text = initialName,
                Location = new Point(inputX + 10, 182),
                Size = new Size(inputW - 20, 26),
                BackColor = GrokTheme.Level0,
                ForeColor = GrokTheme.TextPrimary,
                Font = GrokTheme.FontPrimary,
                BorderStyle = BorderStyle.None,
                TextAlign = HorizontalAlignment.Center
            };
            _txtName.GotFocus += (s, e) => this.Invalidate();
            _txtName.LostFocus += (s, e) => this.Invalidate();
            _txtName.TextChanged += (s, e) => this.Invalidate();
            this.Controls.Add(_txtName);

            // 底部操作按钮
            _btnCancel = new GrokButton("取消", GrokButtonRole.Secondary, DialogResult.Cancel)
            {
                Location = new Point(446, 552),
                Size = new Size(96, GrokTheme.ButtonHeightStandard)
            };
            this.Controls.Add(_btnCancel);

            _btnCreate = new GrokButton("创建节点", GrokButtonRole.Primary)
            {
                Location = new Point(552, 552),
                Size = new Size(96, GrokTheme.ButtonHeightStandard)
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

            this.AcceptButton = _btnCreate;
            this.CancelButton = _btnCancel;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 1. 检查形状网格点击 (14 种形态)
                int shapeIdx = -1;
                int sSize = 46;
                int sStep = 80;
                int sy0 = 236;
                int sy1 = 296;
                int sy2 = 356;
                int startX0 = (this.Width - (5 * sSize + 4 * (sStep - sSize))) / 2;
                int startX2 = (this.Width - (4 * sSize + 3 * (sStep - sSize))) / 2;

                // Row 0
                for (int i = 0; i < 5; i++)
                {
                    int sx = startX0 + i * sStep;
                    Rectangle sr = new Rectangle(sx - 4, sy0 - 4, sSize + 8, sSize + 8);
                    if (sr.Contains(e.Location)) { shapeIdx = i; break; }
                }
                // Row 1
                if (shapeIdx == -1)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        int sx = startX0 + i * sStep;
                        Rectangle sr = new Rectangle(sx - 4, sy1 - 4, sSize + 8, sSize + 8);
                        if (sr.Contains(e.Location)) { shapeIdx = 5 + i; break; }
                    }
                }
                // Row 2 (4 shapes centered)
                if (shapeIdx == -1)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int sx = startX2 + i * sStep;
                        Rectangle sr = new Rectangle(sx - 4, sy2 - 4, sSize + 8, sSize + 8);
                        if (sr.Contains(e.Location)) { shapeIdx = 10 + i; break; }
                    }
                }

                if (shapeIdx != -1)
                {
                    _selectedShape = shapeIdx;
                    this.Invalidate();
                    return;
                }

                // 2. 检查调色板圆点点击 (11 色)
                int cy = 440;
                int cSize = 26;
                int cGap = 14;
                int totalColorW = 11 * cSize + 10 * cGap;
                int startColorX = (this.Width - totalColorW) / 2;
                for (int i = 0; i < _colors.Length; i++)
                {
                    int cx = startColorX + i * (cSize + cGap);
                    Rectangle cr = new Rectangle(cx - 3, cy - 3, cSize + 6, cSize + 6);
                    if (cr.Contains(e.Location))
                    {
                        _selectedColorIndex = i;
                        this.Invalidate();
                        return;
                    }
                }
            }

            base.OnMouseDown(e);
        }

        protected override void OnPaintContent(Graphics g)
        {
            // 1. 标题 "创建新节点" (18.0pt Bold #FFFFFF)
            using (Font titleFont = GrokTheme.FontTitle)
            using (SolidBrush tb = new SolidBrush(GrokTheme.TextTitle))
            {
                g.DrawString("创建新节点", titleFont, tb, new PointF(30, 20));
            }

            // 2. 居中大号吉祥物 Hero 实时动态预览 (96x96)
            int heroSize = 96;
            int heroX = (this.Width - heroSize) / 2;
            int heroY = 60;
            Rectangle heroRect = new Rectangle(heroX, heroY, heroSize, heroSize);
            WeChatIcon.DrawMascotAvatarShape(g, heroRect, _selectedShape, _colors[_selectedColorIndex], false, Color.Transparent);

            // 3. 起名输入胶囊背景框 (高 42px, 圆角 12px)
            int inputW = 440;
            int inputX = (this.Width - inputW) / 2;
            Rectangle nameBox = new Rectangle(inputX, 174, inputW, 42);
            using (SolidBrush nb = new SolidBrush(GrokTheme.Level0))
            using (Pen np = new Pen((_txtName != null && _txtName.Focused) ? GrokTheme.AccentPurple : GrokTheme.Border, 1f))
            {
                GrokTheme.DrawRoundedRectangle(g, nb, nameBox, 12);
                GrokTheme.DrawRoundedRectangle(g, np, nameBox, 12);
            }

            if (_txtName != null && string.IsNullOrEmpty(_txtName.Text))
            {
                using (Font pf = GrokTheme.FontCardItem)
                using (SolidBrush pb = new SolidBrush(GrokTheme.TextPlaceholder))
                {
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("给你的节点起一个名字 (或直接输入 IP)", pf, pb, nameBox, sf);
                    }
                }
            }

            // 4. 绘制 14 种形态选择网格
            int sSize = 46;
            int sStep = 80;
            int sy0 = 236;
            int sy1 = 296;
            int sy2 = 356;
            int startX0 = (this.Width - (5 * sSize + 4 * (sStep - sSize))) / 2;
            int startX2 = (this.Width - (4 * sSize + 3 * (sStep - sSize))) / 2;

            for (int i = 0; i < 14; i++)
            {
                int sx, sy;
                if (i < 5)
                {
                    sx = startX0 + i * sStep;
                    sy = sy0;
                }
                else if (i < 10)
                {
                    sx = startX0 + (i - 5) * sStep;
                    sy = sy1;
                }
                else
                {
                    sx = startX2 + (i - 10) * sStep;
                    sy = sy2;
                }

                Rectangle sr = new Rectangle(sx, sy, sSize, sSize);
                bool isSelected = (_selectedShape == i);

                // 底块背景
                using (SolidBrush sb = new SolidBrush(isSelected ? GrokTheme.Level3Hot : GrokTheme.Level2))
                using (Pen sp = new Pen(isSelected ? GrokTheme.AccentPurple : GrokTheme.Border, isSelected ? 1.5f : 1f))
                {
                    GrokTheme.DrawRoundedRectangle(g, sb, sr, 10);
                    GrokTheme.DrawRoundedRectangle(g, sp, sr, 10);
                }

                // 内部小吉祥物预览
                Rectangle inner = new Rectangle(sx + 7, sy + 7, sSize - 14, sSize - 14);
                WeChatIcon.DrawMascotAvatarShape(g, inner, i, isSelected ? _colors[_selectedColorIndex] : Color.FromArgb(170, 170, 178), false, Color.Transparent);
            }

            // 5. 绘制 11 色调色盘
            int cy = 440;
            int cSize = 26;
            int cGap = 14;
            int totalColorW = 11 * cSize + 10 * cGap;
            int startColorX = (this.Width - totalColorW) / 2;

            for (int i = 0; i < _colors.Length; i++)
            {
                int cx = startColorX + i * (cSize + cGap);
                Rectangle cr = new Rectangle(cx, cy, cSize, cSize);
                bool isSelected = (_selectedColorIndex == i);

                using (SolidBrush cb = new SolidBrush(_colors[i]))
                {
                    g.FillEllipse(cb, cr);
                }

                if (isSelected)
                {
                    Rectangle ring = new Rectangle(cx - 3, cy - 3, cSize + 6, cSize + 6);
                    using (Pen rp = new Pen(GrokTheme.AccentPurple, 2f))
                    {
                        g.DrawEllipse(rp, ring);
                    }
                }
                else
                {
                    using (Pen cp = new Pen(Color.FromArgb(50, 50, 56), 1f))
                    {
                        g.DrawEllipse(cp, cr);
                    }
                }
            }
        }
    }
}
