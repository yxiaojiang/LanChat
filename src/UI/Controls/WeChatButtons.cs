using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    #region Custom WeChat Navigation Button

    /// <summary>
    /// 微信/Grok 风格侧边栏导航图标按钮
    /// <para>【职责】提供侧边栏垂直排版的导航项切换按钮。</para>
    /// <para>【特性】支持矢量图标自绘、鼠标悬停半透明圆角底衬、点击按压反馈与微信绿/高光激活态指示，集成暗黑 Tooltip 悬浮提示。</para>
    /// </summary>
    public class WeChatNavButton : Control
    {
        private WeChatIconType _iconType;
        private bool _isActive;
        private bool _isHovered;
        private bool _isPressed;
        private string _tooltipText;

        /// <summary>
        /// 获取或设置按钮呈现的纯矢量图标类型
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public WeChatIconType IconType
        {
            get { return _iconType; }
            set { _iconType = value; Invalidate(); }
        }

        /// <summary>
        /// 获取或设置按钮是否处于激活/当前选中状态（激活时图标呈现品牌绿色或高亮纯白）
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsActive
        {
            get { return _isActive; }
            set { _isActive = value; Invalidate(); }
        }

        /// <summary>
        /// 获取或设置鼠标悬停在按钮上时显示的暗黑浮动提示文本
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string TooltipText
        {
            get { return _tooltipText; }
            set { _tooltipText = value; }
        }

        /// <summary>
        /// 初始化导航按钮
        /// </summary>
        /// <param name="iconType">绘制的矢量图标类型</param>
        /// <param name="tooltip">悬停提示文本</param>
        /// <param name="active">初始激活状态（默认为 false）</param>
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

        /// <summary>鼠标进入：激活悬停并浮现暗黑 Tooltip</summary>
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
            WeChatDarkTooltip.ShowTooltip(this, _tooltipText);
        }

        /// <summary>鼠标移出：重置状态并隐藏 Tooltip</summary>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
            WeChatDarkTooltip.HideTooltip();
        }

        /// <summary>鼠标左键按下：触发微下沉反馈</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _isPressed = true;
            Invalidate();
        }

        /// <summary>鼠标松开：恢复状态</summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        /// <summary>
        /// 执行自绘：包含 6px 圆角悬停背景、按压底色与矢量图标抗锯齿渲染
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. 悬停/按压圆角背景
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

            // 2. 状态图标前景色解析
            Color iconColor;
            if (_isActive)
            {
                iconColor = Color.FromArgb(0, 163, 97); // WeChat Green
            }
            else if (_isHovered)
            {
                iconColor = Color.FromArgb(240, 240, 240); // 纯白高亮
            }
            else
            {
                iconColor = Color.FromArgb(152, 152, 152); // 暗灰中性色 #989898
            }

            Rectangle iconRect = new Rectangle(0, 0, this.Width, this.Height);
            WeChatIcon.Draw(g, _iconType, iconRect, _isActive, iconColor);
        }
    }

    #endregion

    #region Custom Icon & Circle Button (For Grok Header, Sidebar & Input Pill)

    /// <summary>
    /// 暗黑圆形/圆角多功能图标按钮
    /// <para>【职责】广泛用于主界面顶部标题栏无边框控制钮 (— □ ×)、搜索圆钮、胶囊内 (+) 附件加号圆钮、(↑) 魅紫发送圆钮及输入清理叉。</para>
    /// <para>【特性】支持正圆形/圆角矩形切换、自定义填充与悬停高亮色、关闭按钮专属危险红反馈及禁用态灰阶呈现。</para>
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
        private bool _isActive = true;
        private Color _disabledFillColor = Color.FromArgb(44, 44, 50);
        private Color _disabledIconColor = Color.FromArgb(115, 115, 122);

        /// <summary>获取或设置矢量图标类型</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public WeChatIconType IconType
        {
            get { return _iconType; }
            set { _iconType = value; Invalidate(); }
        }

        /// <summary>获取或设置是否以正圆形自绘（true 为圆形，false 为圆角或矩形）</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsCircle
        {
            get { return _isCircle; }
            set { _isCircle = value; Invalidate(); }
        }

        /// <summary>获取或设置普通静止态底色</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color FillColor
        {
            get { return _fillColor; }
            set { _fillColor = value; Invalidate(); }
        }

        /// <summary>获取或设置鼠标悬停态底色</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color HoverFillColor
        {
            get { return _hoverFillColor; }
            set { _hoverFillColor = value; Invalidate(); }
        }

        /// <summary>获取或设置常规图标线条颜色</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color IconColor
        {
            get { return _iconColor; }
            set { _iconColor = value; Invalidate(); }
        }

        /// <summary>获取或设置外描边颜色（Transparent 为无描边）</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color BorderColor
        {
            get { return _borderColor; }
            set { _borderColor = value; Invalidate(); }
        }

        /// <summary>获取或设置是否为窗体关闭叉按钮（为 true 时悬停自动呈现系统经典危险红 #E81123）</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsWindowClose
        {
            get { return _isWindowClose; }
            set { _isWindowClose = value; Invalidate(); }
        }

        /// <summary>获取或设置交互启用状态（为 false 时呈现禁用灰阶且不响应点击）</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsActive
        {
            get { return _isActive; }
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    this.Cursor = _isActive ? Cursors.Hand : Cursors.Default;
                    Invalidate();
                }
            }
        }

        /// <summary>获取或设置禁用态底色</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color DisabledFillColor
        {
            get { return _disabledFillColor; }
            set { _disabledFillColor = value; Invalidate(); }
        }

        /// <summary>获取或设置禁用态图标颜色</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color DisabledIconColor
        {
            get { return _disabledIconColor; }
            set { _disabledIconColor = value; Invalidate(); }
        }

        /// <summary>获取或设置悬停 Tooltip 文本</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string TooltipText
        {
            get { return _tooltipText; }
            set { _tooltipText = value; }
        }

        /// <summary>
        /// 初始化图标按钮
        /// </summary>
        /// <param name="iconType">矢量图标类型</param>
        /// <param name="tooltip">悬停提示文本</param>
        /// <param name="size">按钮正方形边长像素（默认 28px）</param>
        public WeChatIconButton(WeChatIconType iconType, string tooltip, int size = 28)
        {
            _iconType = iconType;
            _tooltipText = tooltip;

            this.Size = new Size(size, size);
            this.Cursor = Cursors.Hand;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = Color.Transparent;
        }

        /// <summary>鼠标进入悬停态并浮现提示</summary>
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (_isActive) _isHovered = true;
            Invalidate();
            if (!string.IsNullOrEmpty(_tooltipText))
            {
                WeChatDarkTooltip.ShowTooltip(this, _tooltipText);
            }
        }

        /// <summary>鼠标移出恢复状态并隐藏提示</summary>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
            WeChatDarkTooltip.HideTooltip();
        }

        /// <summary>鼠标按下状态切换</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!_isActive) return;
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        /// <summary>鼠标松开恢复状态</summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!_isActive) return;
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        /// <summary>点击拦截：禁用态下压制 Click 事件派发</summary>
        protected override void OnClick(EventArgs e)
        {
            if (!_isActive) return;
            base.OnClick(e);
        }

        /// <summary>
        /// 执行自绘：包含背景底衬绘制（正圆/矩形/4px 圆角）、微光外描边与自适应内缩距矢量图标抗锯齿绘制
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // 1. 底色计算
            Color bg = _fillColor;
            if (!_isActive)
            {
                bg = (_fillColor == Color.Transparent) ? Color.Transparent : _disabledFillColor;
            }
            else if (_isPressed || _isHovered)
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
                    else if (_isWindowClose || _hoverFillColor == Color.FromArgb(55, 55, 58))
                    {
                        g.FillRectangle(b, new Rectangle(0, 0, this.Width, this.Height));
                    }
                    else
                    {
                        MainForm.DrawRoundedRectangle(g, b, rect, 4);
                    }
                }
            }

            // 2. 边框绘制
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

            // 3. 图标前景色与居中几何缩放绘制
            Color ic = !_isActive ? _disabledIconColor : ((_isHovered || _isPressed) ? Color.White : _iconColor);
            // 圆纽内边距缩进 ~22%，确保搜索圆纽与加号圆纽视觉张力完全契合 Alis 设计规范
            int pad = _isCircle ? Math.Max(6, this.Width * 22 / 100) : 0;
            Rectangle iconRect = new Rectangle(pad, pad, this.Width - pad * 2, this.Height - pad * 2);
            WeChatIcon.Draw(g, _iconType, iconRect, false, ic);
        }
    }

    #endregion
}
