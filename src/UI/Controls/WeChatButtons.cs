using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    #region Custom WeChat Navigation Button

    /// <summary>
    /// 微信桌面端侧边栏导航按钮（支持图标矢量渲染、悬停圆角背景与微信绿激活态）
    /// </summary>
    public class WeChatNavButton : Control
    {
        private WeChatIconType _iconType;
        private bool _isActive;
        private bool _isHovered;
        private bool _isPressed;
        private string _tooltipText;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public WeChatIconType IconType
        {
            get { return _iconType; }
            set { _iconType = value; Invalidate(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsActive
        {
            get { return _isActive; }
            set { _isActive = value; Invalidate(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string TooltipText
        {
            get { return _tooltipText; }
            set { _tooltipText = value; }
        }

        public WeChatNavButton(WeChatIconType iconType, string tooltip, bool active = false)
        {
            _iconType = iconType;
            _tooltipText = tooltip;
            _isActive = active;

            this.Size = new Size(36, 36);
            this.Cursor = Cursors.Hand;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
            WeChatDarkTooltip.ShowTooltip(this, _tooltipText);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
            WeChatDarkTooltip.HideTooltip();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _isPressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background hover rounded rectangle (#303031, exactly per screenshot)
            if (_isHovered && !_isActive)
            {
                using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(48, 48, 49)))
                {
                    MainForm.DrawRoundedRectangle(g, hoverBg, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 6);
                }
            }
            else if (_isPressed)
            {
                using (SolidBrush pressBg = new SolidBrush(Color.FromArgb(40, 40, 41)))
                {
                    MainForm.DrawRoundedRectangle(g, pressBg, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 6);
                }
            }

            // Determine Icon color
            Color iconColor;
            if (_isActive)
            {
                iconColor = Color.FromArgb(0, 163, 97); // WeChat Green
            }
            else if (_isHovered)
            {
                iconColor = Color.FromArgb(240, 240, 240); // Bright white
            }
            else
            {
                iconColor = Color.FromArgb(152, 152, 152); // Muted grey #989898
            }

            Rectangle iconRect = new Rectangle(0, 0, this.Width, this.Height);
            WeChatIcon.Draw(g, _iconType, iconRect, _isActive, iconColor);
        }
    }

    #endregion

    #region Custom WeChat Icon Button (For Header & Toolbar)

    /// <summary>
    /// 微信暗黑工具栏与标题栏轻量图标按钮（无边框透明背景、圆角暗黑悬停）
    /// </summary>
    public class WeChatIconButton : Control
    {
        private WeChatIconType _iconType;
        private bool _isHovered;
        private bool _isPressed;
        private string _tooltipText;

        public WeChatIconButton(WeChatIconType iconType, string tooltip, int size = 28)
        {
            _iconType = iconType;
            _tooltipText = tooltip;

            this.Size = new Size(size, size);
            this.Cursor = Cursors.Hand;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
            if (!string.IsNullOrEmpty(_tooltipText))
            {
                WeChatDarkTooltip.ShowTooltip(this, _tooltipText);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
            WeChatDarkTooltip.HideTooltip();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_isPressed)
            {
                using (SolidBrush pressBg = new SolidBrush(Color.FromArgb(60, 60, 64)))
                {
                    MainForm.DrawRoundedRectangle(g, pressBg, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 4);
                }
            }
            else if (_isHovered)
            {
                using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(50, 50, 50)))
                {
                    MainForm.DrawRoundedRectangle(g, hoverBg, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 4);
                }
            }

            Color iconColor = (_isHovered || _isPressed) ? Color.FromArgb(240, 240, 240) : Color.FromArgb(145, 145, 148);
            WeChatIcon.Draw(g, _iconType, new Rectangle(0, 0, this.Width, this.Height), false, iconColor);
        }
    }

    #endregion
}
