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
        private readonly Color _bg = Color.FromArgb(44, 44, 46);
        private readonly Color _border = Color.FromArgb(60, 60, 64);
        private readonly Color _selected = Color.FromArgb(60, 60, 64);
        private readonly Color _separator = Color.FromArgb(55, 55, 58);

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
            using (Pen p = new Pen(Color.FromArgb(7, 193, 96), 2.2f))
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
            using (SolidBrush b = new SolidBrush(Color.FromArgb(44, 44, 46)))
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
        private static Form _tipForm;
        private static Label _lblText;

        public static void ShowTooltip(Control parent, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (_tipForm == null)
            {
                _tipForm = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    BackColor = Color.FromArgb(59, 59, 61), // #3b3b3d per screenshot
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(8, 5, 8, 5)
                };

                _lblText = new Label
                {
                    ForeColor = Color.FromArgb(225, 225, 229), // #e1e1e5
                    Font = new Font("Microsoft YaHei", 8.5f),
                    AutoSize = true,
                    BackColor = Color.Transparent
                };
                _tipForm.Controls.Add(_lblText);

                _tipForm.Paint += (s, e) => {
                    using (Pen p = new Pen(Color.FromArgb(75, 75, 78)))
                    {
                        e.Graphics.DrawRectangle(p, 0, 0, _tipForm.Width - 1, _tipForm.Height - 1);
                    }
                };
            }

            _lblText.Text = text;
            Point screenPt = parent.PointToScreen(new Point(parent.Width + 8, (parent.Height - 24) / 2));
            _tipForm.Location = screenPt;
            _tipForm.Show();
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
