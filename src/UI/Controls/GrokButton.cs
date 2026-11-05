using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// GrokBot 统一自绘按钮角色枚举
    /// </summary>
    public enum GrokButtonRole
    {
        /// <summary>魅紫核心确认主按钮（用于保存、确定、发送等主要操作）</summary>
        Primary,
        /// <summary>深灰次要操作按钮（用于取消、关闭、次级功能）</summary>
        Secondary,
        /// <summary>警示危险操作按钮（用于删除、清空、断开连接）</summary>
        Destructive,
        /// <summary>小巧药丸胶囊按钮（用于弹窗操作、卡片内嵌小按钮）</summary>
        Pill
    }

    /// <summary>
    /// GrokBot 统一样式自绘抗锯齿按钮控件
    /// <para>【职责】提供 100% 纯 GDI+ 平滑自绘按钮，彻底消除 Windows 原生白底、生硬直角与默认焦点框。</para>
    /// <para>【特性】继承自 WinForms Button，原生实现 IButtonControl 契约（Enter 触发 AcceptButton，ESC 触发 CancelButton，DialogResult 自动关闭父窗体）。</para>
    /// <para>【度量】对标 Docs/GrokBot_UI_Design_System.md，联动 GrokTheme 设计令牌。</para>
    /// </summary>
    public class GrokButton : Button
    {
        private GrokButtonRole _role = GrokButtonRole.Secondary;
        private int? _cornerRadius = null;
        private bool _isPill = false;
        private bool _isHover = false;
        private bool _isPressed = false;

        /// <summary>
        /// 获取或设置按钮的视觉角色分类，设置时会自动应用对应角色的默认尺寸和字号
        /// </summary>
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

        /// <summary>
        /// 获取或设置按钮的自定义圆角半径（像素）。为 null 时使用对应角色的设计规范默认值
        /// </summary>
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

        /// <summary>
        /// 获取或设置按钮是否强制以药丸全圆角形态呈现（圆角半径等于高度的一半）
        /// </summary>
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

        /// <summary>
        /// 初始化默认深灰样式的空白按钮
        /// </summary>
        public GrokButton() : this(string.Empty, GrokButtonRole.Secondary)
        {
        }

        /// <summary>
        /// 使用指定文本与视觉角色初始化按钮
        /// </summary>
        /// <param name="text">按钮显示文本</param>
        /// <param name="role">按钮角色类型（Primary / Secondary / Destructive / Pill）</param>
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

        /// <summary>
        /// 使用指定文本、视觉角色以及对话框结果标识初始化按钮（点击时自动关闭所属模态窗并返回该结果）
        /// </summary>
        /// <param name="text">按钮显示文本</param>
        /// <param name="role">按钮角色类型</param>
        /// <param name="dialogResult">模态对话框返回结果（DialogResult.OK / DialogResult.Cancel 等）</param>
        public GrokButton(string text, GrokButtonRole role, DialogResult dialogResult)
            : this(text, role)
        {
            this.DialogResult = dialogResult;
        }

        /// <summary>
        /// 根据当前角色应用默认尺寸与字体规格（对标 GrokBot UI 设计系统矩阵）
        /// </summary>
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

        // ==========================================
        // 鼠标状态机与交互事件
        // ==========================================

        /// <summary>鼠标进入悬停状态：触发重绘激活提亮高光</summary>
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHover = true;
            this.Invalidate();
        }

        /// <summary>鼠标离开状态：重置悬停与按压态并触发重绘</summary>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHover = false;
            _isPressed = false;
            this.Invalidate();
        }

        /// <summary>鼠标左键按下：进入下沉按压态</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                this.Invalidate();
            }
        }

        /// <summary>鼠标左键松开：恢复悬停态</summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isPressed)
            {
                _isPressed = false;
                this.Invalidate();
            }
        }

        /// <summary>启用状态变更：重绘以应用禁用态弱化色彩</summary>
        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            this.Invalidate();
        }

        // ==========================================
        // GDI+ 矢量抗锯齿平滑绘制管线
        // ==========================================

        /// <summary>
        /// 执行按钮全画幅自绘：包含父容器底衬、抗锯齿圆角卡片、微光边框与数学居中排版文字
        /// </summary>
        /// <param name="e">绘制事件参数</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // 1. 用父容器背景色填充底衬，确保圆角外沿无任何原生灰白毛刺
            Color parentBg = (this.Parent != null) ? this.Parent.BackColor : GrokTheme.Level1;
            using (SolidBrush pb = new SolidBrush(parentBg))
            {
                g.FillRectangle(pb, 0, 0, this.Width, this.Height);
            }

            // 2. 计算圆角半径
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

            // 3. 依角色与当前交互状态解析色彩令牌
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

            // 4. 绘制平滑圆角实体背景与 1px 微光描边
            Rectangle bounds = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            using (Pen borderPen = new Pen(borderColor, 1f))
            {
                GrokTheme.DrawRoundedRectangle(g, bgBrush, bounds, radius);
                GrokTheme.DrawRoundedRectangle(g, borderPen, bounds, radius);
            }

            // 5. 绘制数学严格居中文本（按下时产生 1px 微下沉按压动效）
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
