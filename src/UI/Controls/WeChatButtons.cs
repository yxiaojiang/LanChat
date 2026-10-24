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

    #region Custom Icon & Circle Button (For Grok Header, Sidebar & Input Pill)

    /// <summary>
    /// 暗黑圆形/圆角图标按钮（支持 Grok 风格正圆底色、紫色声波圆钮、蓝色上箭头圆钮及无边框窗口控制按钮）
    /// </summary>
    public class WeChatIconButton : Control
    {
        private WeChatIconType _iconType;
        private bool _isHovered;
        private bool _isPressed;
        private string _tooltipText;
        private bool _isCircle;
        private Color _fillColor = Color.Transparent;
        private Color _hoverFillColor = Color.FromArgb(46, 46, 48);
        private Color _iconColor = Color.FromArgb(200, 200, 204);
        private Color _borderColor = Color.Transparent;
        private bool _isWindowClose = false;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsCircle
        {
            get { return _isCircle; }
            set { _isCircle = value; Invalidate(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color FillColor
        {
            get { return _fillColor; }
            set { _fillColor = value; Invalidate(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color HoverFillColor
        {
            get { return _hoverFillColor; }
            set { _hoverFillColor = value; Invalidate(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color IconColor
        {
            get { return _iconColor; }
            set { _iconColor = value; Invalidate(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color BorderColor
        {
            get { return _borderColor; }
            set { _borderColor = value; Invalidate(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsWindowClose
        {
            get { return _isWindowClose; }
            set { _isWindowClose = value; Invalidate(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string TooltipText
        {
            get { return _tooltipText; }
            set { _tooltipText = value; }
        }

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
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color bg = _fillColor;
            if (_isPressed || _isHovered)
            {
                bg = _isWindowClose ? Color.FromArgb(232, 17, 35) : _hoverFillColor;
            }

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            if (bg != Color.Transparent)
            {
                using (SolidBrush b = new SolidBrush(bg))
                {
                    if (_isCircle)
                    {
                        g.FillEllipse(b, rect);
                    }
                    else
                    {
                        MainForm.DrawRoundedRectangle(g, b, rect, _isWindowClose ? 0 : 4);
                    }
                }
            }

            if (_borderColor != Color.Transparent)
            {
                using (Pen p = new Pen(_borderColor, 1f))
                {
                    if (_isCircle)
                    {
                        g.DrawEllipse(p, rect);
                    }
                    else
                    {
                        MainForm.DrawRoundedRectangle(g, p, rect, 4);
                    }
                }
            }

            Color ic = (_isHovered || _isPressed) ? Color.White : _iconColor;
            WeChatIcon.Draw(g, _iconType, new Rectangle(0, 0, this.Width, this.Height), false, ic);
        }
    }

    #endregion
}
