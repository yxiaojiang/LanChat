using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// GrokBot 按钮角色枚举
    /// </summary>
    public enum GrokButtonRole
    {
        Primary,      // 魅紫主确认按钮
        Secondary,    // 深灰次要操作按钮
        Destructive,  // 警示危险红操作按钮
        Pill          // 药丸胶囊小按钮
    }

    /// <summary>
    /// GrokBot 统一样式自绘抗锯齿按钮控件
    /// 继承自 WinForms Button，原生实现 IButtonControl 契约 (Enter 回车触发 AcceptButton，ESC 触发 CancelButton，DialogResult 自动关闭父窗体)
    /// 100% 纯 GDI+ 平滑自绘，消除 Windows 原生白底直角与系统默认焦点框
    /// </summary>
    public class GrokButton : Button
    {
        private GrokButtonRole _role = GrokButtonRole.Secondary;
        private int? _cornerRadius = null;
        private bool _isPill = false;
        private bool _isHover = false;
        private bool _isPressed = false;

        public GrokButtonRole Role
        {
            get { return _role; }
            set
            {
                if (_role != value)
                {
                    _role = value;
                    ApplyRoleDefaults();
                    this.Invalidate();
                }
            }
        }

        public int? CornerRadius
        {
            get { return _cornerRadius; }
            set
            {
                if (_cornerRadius != value)
                {
                    _cornerRadius = value;
                    this.Invalidate();
                }
            }
        }

        public bool IsPill
        {
            get { return _isPill || _role == GrokButtonRole.Pill; }
            set
            {
                if (_isPill != value)
                {
                    _isPill = value;
                    this.Invalidate();
                }
            }
        }

        public GrokButton() : this(string.Empty, GrokButtonRole.Secondary)
        {
        }

        public GrokButton(string text, GrokButtonRole role = GrokButtonRole.Secondary)
        {
            this.SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);

            this.DoubleBuffered = true;
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.FlatAppearance.MouseOverBackColor = Color.Transparent;
            this.FlatAppearance.MouseDownBackColor = Color.Transparent;
            this.BackColor = Color.Transparent;
            this.Cursor = Cursors.Hand;
            this.Text = text;
            this._role = role;

            ApplyRoleDefaults();
        }

        public GrokButton(string text, GrokButtonRole role, DialogResult dialogResult)
            : this(text, role)
        {
            this.DialogResult = dialogResult;
        }

        private void ApplyRoleDefaults()
        {
            if (_role == GrokButtonRole.Pill)
            {
                this.Size = new Size(82, GrokTheme.ButtonHeightPill);
                this.Font = GrokTheme.FontButtonPill;
            }
            else
            {
                this.Size = new Size(92, GrokTheme.ButtonHeightStandard);
                this.Font = GrokTheme.FontButton;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHover = true;
            this.Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHover = false;
            _isPressed = false;
            this.Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                this.Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isPressed)
            {
                _isPressed = false;
                this.Invalidate();
            }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // 用父容器背景色填充底衬，确保圆角外部无任何原生灰白毛刺
            Color parentBg = (this.Parent != null) ? this.Parent.BackColor : GrokTheme.Level1;
            using (SolidBrush pb = new SolidBrush(parentBg))
            {
                g.FillRectangle(pb, 0, 0, this.Width, this.Height);
            }

            int radius;
            if (_cornerRadius.HasValue)
            {
                radius = _cornerRadius.Value;
            }
            else if (IsPill)
            {
                radius = Math.Max(1, this.Height / 2);
            }
            else
            {
                radius = GrokTheme.ButtonRadiusStandard;
            }

            Color bgColor;
            Color borderColor;
            Color textColor;

            if (!this.Enabled)
            {
                bgColor = Color.FromArgb(28, 28, 32);
                borderColor = Color.FromArgb(40, 40, 46);
                textColor = GrokTheme.TextDisabled;
            }
            else
            {
                switch (_role)
                {
                    case GrokButtonRole.Primary:
                        bgColor = _isPressed ? GrokTheme.ButtonPrimaryDown : (_isHover ? GrokTheme.ButtonPrimaryHover : GrokTheme.ButtonPrimaryNormal);
                        borderColor = _isHover ? GrokTheme.AccentPurpleHot : GrokTheme.ButtonPrimaryHover;
                        textColor = GrokTheme.TextTitle;
                        break;

                    case GrokButtonRole.Destructive:
                        bgColor = _isPressed ? GrokTheme.ButtonDestructiveDown : (_isHover ? GrokTheme.ButtonDestructiveHover : GrokTheme.ButtonDestructiveNormal);
                        borderColor = _isHover ? Color.FromArgb(140, 45, 55) : Color.FromArgb(100, 32, 40);
                        textColor = _isHover ? GrokTheme.TextDestructiveHot : GrokTheme.TextDestructive;
                        break;

                    case GrokButtonRole.Pill:
                        bgColor = _isPressed ? Color.FromArgb(24, 24, 28) : (_isHover ? Color.FromArgb(44, 44, 50) : Color.FromArgb(32, 32, 36));
                        borderColor = _isHover ? Color.FromArgb(64, 64, 72) : GrokTheme.Border;
                        textColor = _isHover ? GrokTheme.TextPrimary : GrokTheme.TextPill;
                        break;

                    case GrokButtonRole.Secondary:
                    default:
                        bgColor = _isPressed ? GrokTheme.ButtonSecondaryDown : (_isHover ? Color.FromArgb(46, 46, 52) : GrokTheme.ButtonSecondaryNormal);
                        borderColor = _isHover ? Color.FromArgb(64, 64, 72) : GrokTheme.Border;
                        textColor = _isHover ? GrokTheme.TextPrimary : GrokTheme.TextCard;
                        break;
                }
            }

            Rectangle bounds = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            using (Pen borderPen = new Pen(borderColor, 1f))
            {
                GrokTheme.DrawRoundedRectangle(g, bgBrush, bounds, radius);
                GrokTheme.DrawRoundedRectangle(g, borderPen, bounds, radius);
            }

            // 绘制居中文本
            GrokTheme.PrepareSoftText(g);
            int textOffsetY = _isPressed ? 1 : 0;
            Rectangle textRect = new Rectangle(0, textOffsetY, this.Width, this.Height);

            using (StringFormat sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None
            })
            using (SolidBrush tb = new SolidBrush(textColor))
            {
                g.DrawString(this.Text, this.Font, tb, textRect, sf);
            }
        }
    }
}
