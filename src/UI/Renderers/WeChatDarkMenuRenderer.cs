using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    #region Custom WeChat Dark Menu Renderer

    /// <summary>
    /// 微信暗黑风格菜单调色板（纯暗黑 #2c2c2e 背景与 #3c3c40 高亮选中态）
    /// </summary>
    public class WeChatDarkMenuColorTable : ProfessionalColorTable
    {
        private readonly Color _bg = Color.FromArgb(28, 28, 31);
        private readonly Color _border = Color.FromArgb(44, 44, 50);
        private readonly Color _selected = Color.FromArgb(46, 46, 52);
        private readonly Color _separator = Color.FromArgb(44, 44, 48);

        public override Color ToolStripDropDownBackground { get { return _bg; } }
        public override Color ImageMarginGradientBegin { get { return _bg; } }
        public override Color ImageMarginGradientMiddle { get { return _bg; } }
        public override Color ImageMarginGradientEnd { get { return _bg; } }
        public override Color ImageMarginRevealedGradientBegin { get { return _bg; } }
        public override Color ImageMarginRevealedGradientMiddle { get { return _bg; } }
        public override Color ImageMarginRevealedGradientEnd { get { return _bg; } }
        public override Color MenuBorder { get { return _border; } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return _selected; } }
        public override Color MenuItemSelectedGradientBegin { get { return _selected; } }
        public override Color MenuItemSelectedGradientEnd { get { return _selected; } }
        public override Color MenuItemPressedGradientBegin { get { return _selected; } }
        public override Color MenuItemPressedGradientMiddle { get { return _selected; } }
        public override Color MenuItemPressedGradientEnd { get { return _selected; } }
        public override Color CheckBackground { get { return _selected; } }
        public override Color CheckSelectedBackground { get { return _selected; } }
        public override Color CheckPressedBackground { get { return _selected; } }
        public override Color SeparatorDark { get { return _separator; } }
        public override Color SeparatorLight { get { return _bg; } }
    }

    /// <summary>
    /// 微信暗黑风格菜单自绘渲染器
    /// </summary>
    public class WeChatDarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public WeChatDarkMenuRenderer() : base(new WeChatDarkMenuColorTable())
        {
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.FromArgb(235, 235, 235) : Color.FromArgb(125, 125, 128);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.FromArgb(200, 200, 200);
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = e.ImageRectangle;
            using (Pen p = new Pen(Color.FromArgb(144, 90, 251), 2.2f))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawLines(p, new Point[] {
                    new Point(r.Left + 3, r.Top + r.Height / 2),
                    new Point(r.Left + r.Width / 2 - 1, r.Bottom - 4),
                    new Point(r.Right - 3, r.Top + 4)
                });
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(28, 28, 31)))
            {
                e.Graphics.FillRectangle(b, e.AffectedBounds);
            }
        }
    }

    #endregion

    #region Custom WeChat Dark Tooltip

    /// <summary>
    /// 微信暗黑全局浮动 Tooltip 气泡组件（#3b3b3d 暗灰底、1px 微光边框）
    /// </summary>
    public static class WeChatDarkTooltip
    {
        private class DarkTooltipForm : Form
        {
            public DarkTooltipForm()
            {
                this.FormBorderStyle = FormBorderStyle.None;
                this.ShowInTaskbar = false;
                this.StartPosition = FormStartPosition.Manual;
                this.BackColor = Color.FromArgb(42, 42, 45); // #2A2A2D
                this.DoubleBuffered = true;
                this.Padding = new Padding(8, 4, 8, 4);
            }

            protected override bool ShowWithoutActivation
            {
                get { return true; }
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                    cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                    cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                    return cp;
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (Pen p = new Pen(Color.FromArgb(70, 70, 76), 1f)) // #46464C
                {
                    e.Graphics.DrawRectangle(p, 0, 0, this.Width - 1, this.Height - 1);
                }
            }
        }

        private static DarkTooltipForm _tipForm;
        private static Label _lblText;

        public static void ShowTooltip(Control parent, string text)
        {
            if (parent == null || string.IsNullOrEmpty(text) || parent.IsDisposed) return;

            if (_tipForm == null || _tipForm.IsDisposed)
            {
                _tipForm = new DarkTooltipForm();

                _lblText = new Label
                {
                    ForeColor = Color.FromArgb(235, 235, 240), // #EBEBF0
                    Font = new Font("Microsoft YaHei UI", 9.0f),
                    AutoSize = false,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill
                };
                _tipForm.Controls.Add(_lblText);
            }

            _lblText.Text = text;
            Size textSize = TextRenderer.MeasureText(text, _lblText.Font);
            int tipW = textSize.Width + 18;
            int tipH = Math.Max(26, textSize.Height + 10);
            _tipForm.Size = new Size(tipW, tipH);

            Rectangle parentRect = parent.RectangleToScreen(parent.ClientRectangle);
            Screen screen = Screen.FromPoint(new Point(parentRect.Left + parentRect.Width / 2, parentRect.Top + parentRect.Height / 2));
            if (screen == null) screen = Screen.FromControl(parent) ?? Screen.PrimaryScreen;
            Rectangle work = screen.WorkingArea;

            int targetX;
            int targetY;

            if (parent is WeChatNavButton)
            {
                // 左侧导航栏按钮：靠右侧垂直居中浮现
                targetX = parentRect.Right + 8;
                targetY = parentRect.Top + (parentRect.Height - tipH) / 2;
            }
            else if (parentRect.Top - tipH - 6 < work.Top)
            {
                // 窗口顶部控制按钮：在按钮下方显示
                targetX = parentRect.Left + (parentRect.Width - tipW) / 2;
                targetY = parentRect.Bottom + 6;
            }
            else
            {
                // 底部发送按钮 / 工具按钮：在按钮上方居中显示
                targetX = parentRect.Left + (parentRect.Width - tipW) / 2;
                targetY = parentRect.Top - tipH - 6;
            }

            // Windows 规范屏幕边界检测与避让 (Multi-monitor Aware)
            const int margin = 8;
            if (targetX + tipW > work.Right - margin)
            {
                targetX = work.Right - tipW - margin;
            }
            if (targetX < work.Left + margin)
            {
                targetX = work.Left + margin;
            }
            if (targetY + tipH > work.Bottom - margin)
            {
                if (parentRect.Top - tipH - 6 >= work.Top + margin)
                    targetY = parentRect.Top - tipH - 6;
                else
                    targetY = work.Bottom - tipH - margin;
            }
            if (targetY < work.Top + margin)
            {
                if (parentRect.Bottom + tipH + 6 <= work.Bottom - margin)
                    targetY = parentRect.Bottom + 6;
                else
                    targetY = work.Top + margin;
            }

            _tipForm.Location = new Point(targetX, targetY);
            if (!_tipForm.Visible)
            {
                _tipForm.Show();
            }
            else
            {
                _tipForm.Invalidate();
            }
        }

        public static void HideTooltip()
        {
            if (_tipForm != null && _tipForm.Visible)
            {
                _tipForm.Hide();
            }
        }
    }

    #endregion
}
