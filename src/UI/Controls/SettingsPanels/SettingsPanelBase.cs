using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 设置中心子面板抽象基类
    /// <para>【职责】作为设置中心 8 大业务子面板的通用容器基类，规范卡片排版流与自绘控件工厂。</para>
    /// <para>【特性】提供统一宽度 (900px)、统一背景色阶、分组标题工厂、圆角卡片容器工厂、下拉药丸及现代滑动开关。</para>
    /// </summary>
    public abstract class SettingsPanelBase : DoubleBufferedPanel
    {
        /// <summary>标准卡片统一基准宽度（像素）</summary>
        public const int CARD_W = 900;

        /// <summary>当前面板关联的设置上下文 DTO</summary>
        protected SettingsContext Context { get; private set; }

        // ==========================================
        // 统一颜色设计令牌 (对齐 GrokTheme)
        // ==========================================
        /// <summary>当前主题的设置视口底色。</summary>
        protected static Color ColWindowBg { get { return GrokTheme.Level05; } }
        /// <summary>当前主题的卡片底色。</summary>
        protected static Color ColCardBg { get { return GrokTheme.Level1; } }
        /// <summary>当前主题的卡片描边。</summary>
        protected static Color ColCardBorder { get { return GrokTheme.Border; } }
        /// <summary>当前主题的卡片分割线。</summary>
        protected static Color ColCardDivider { get { return GrokTheme.BorderInner; } }
        protected static readonly Color ColTextPrimary = GrokTheme.TextPrimary;
        protected static readonly Color ColTextSecondary = GrokTheme.TextSecondary;
        protected static readonly Color ColSectionHeader = GrokTheme.TextSubtitle;
        protected static readonly Color ColMintGreen = GrokTheme.AccentGreen;
        protected static readonly Color ColSwitchOff = GrokTheme.TextDisabled;
        /// <summary>当前主题的胶囊底色。</summary>
        protected static Color ColPillBg { get { return GrokTheme.Level2; } }
        /// <summary>当前主题的胶囊描边。</summary>
        protected static Color ColPillBorder { get { return GrokTheme.Border; } }

        /// <summary>
        /// 初始化子面板基础尺寸与背景
        /// </summary>
        /// <param name="context">注入的上下文环境</param>
        public SettingsPanelBase(SettingsContext context)
        {
            Context = context;
            this.Location = new Point(0, 0);
            this.Size = new Size(CARD_W, 600);
            this.BackColor = ColWindowBg;
        }

        // ==========================================
        // Grok 卡片与控件组件工厂方法
        // ==========================================

        /// <summary>
        /// 创建 11.0pt 粗体分组小标题（如“常规”、“外观偏好”、“存储位置”）
        /// </summary>
        /// <param name="text">标题显示文本</param>
        /// <param name="y">在面板中的 Y 轴绝对坐标</param>
        /// <returns>构建完成的标题 Label 控件</returns>
        protected Label CreateSectionHeader(string text, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(4, y),
                Size = new Size(300, 24),
                Font = new Font(GrokTheme.FontFamily, 11.0f, FontStyle.Regular),
                ForeColor = ColSectionHeader,
                BackColor = ColWindowBg
            };
        }

        /// <summary>
        /// 创建 12px 自绘圆角实体卡片容器（底色 #1C1C1F，1px 微光描边 #2C2C32）
        /// </summary>
        /// <param name="w">卡片宽度（通常等于 CARD_W = 900）</param>
        /// <param name="h">卡片高度（由内部行数决定，单行 54px）</param>
        /// <returns>构建完成的卡片 Panel 容器</returns>
        protected Panel CreateCardPanel(int w, int h)
        {
            Panel p = new Panel
            {
                Size = new Size(w, h),
                BackColor = ColWindowBg
            };
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, p.ClientSize.Width - 1, h - 1);
                using (SolidBrush b = new SolidBrush(ColCardBg))
                using (Pen pen = new Pen(ColCardBorder, 1f))
                {
                    GrokTheme.DrawRoundedRectangle(e.Graphics, b, r, GrokTheme.RadiusModal);
                    GrokTheme.DrawRoundedRectangle(e.Graphics, pen, r, GrokTheme.RadiusModal);
                }
            };
            return p;
        }

        /// <summary>
        /// 创建右侧下拉药丸胶囊按钮，圆角随高度调整，并提供悬停和焦点反馈。
        /// </summary>
        /// <param name="text">按钮当前标签文字</param>
        /// <param name="minW">最小保证宽度</param>
        /// <param name="h">高度（默认 32px）</param>
        /// <param name="onClick">点击事件回调</param>
        /// <returns>构建完成的药丸下拉按钮</returns>
        protected Button CreateDropdownPill(string text, int minW, int h, Action onClick)
        {
            int btnH = (h > 0) ? h : 32;
            Button btn = new Button
            {
                Text = text,
                Height = btnH,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColCardBg,
                Cursor = Cursors.Hand,
                Font = GrokTheme.FontButtonPill
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ColCardBg;
            btn.FlatAppearance.MouseDownBackColor = ColCardBg;

            string cleanLabel = text.Replace("  ∨", "").Trim();
            using (Graphics gTemp = this.CreateGraphics())
            {
                SizeF sz = gTemp.MeasureString(cleanLabel, btn.Font);
                btn.Width = Math.Max(minW, (int)sz.Width + 56);
            }

            bool hover = false;
            btn.MouseEnter += (s, e) => { hover = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { hover = false; btn.Invalidate(); };
            if (onClick != null) btn.Click += (s, e) => onClick();

            btn.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                GrokTheme.PrepareSoftText(g);

                using (SolidBrush bgB = new SolidBrush(ColCardBg))
                {
                    g.FillRectangle(bgB, 0, 0, btn.Width, btn.Height);
                }

                Rectangle r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (SolidBrush b = new SolidBrush(hover || btn.Focused ? GrokTheme.Level3Hot : ColPillBg))
                using (Pen p = new Pen(ColPillBorder, 1f))
                {
                    GrokTheme.DrawRoundedRectangle(g, b, r, btn.Height / 2);
                    GrokTheme.DrawRoundedRectangle(g, p, r, btn.Height / 2);
                }

                string curLabel = btn.Text.Replace("  ∨", "").Trim();
                GrokTheme.DrawUiTextLeft(g, curLabel, btn.Font, GrokTheme.TextTitle, 16, 0, btn.Width - 44, btn.Height);
                using (Pen arrow = new Pen(hover ? GrokTheme.TextTitle : GrokTheme.TextSecondary, 1.6f))
                {
                    arrow.StartCap = arrow.EndCap = LineCap.Round;
                    arrow.LineJoin = LineJoin.Round;
                    float x = btn.Width - 17, y = btn.Height / 2f;
                    g.DrawLines(arrow, new[] { new PointF(x - 3, y - 1), new PointF(x, y + 2), new PointF(x + 3, y - 1) });
                }
            };
            return btn;
        }

        /// <summary>
        /// 创建右侧纯文字链接按钮（如“修改昵称”、“选择目录”、“清空缓存”）
        /// </summary>
        /// <param name="text">链接显示文本</param>
        /// <param name="minW">最小保证宽度</param>
        /// <param name="h">高度（默认 32px）</param>
        /// <param name="onClick">点击事件回调</param>
        /// <param name="isDanger">是否为危险操作（true 时呈现醒目警示红）</param>
        /// <returns>构建完成的链接按钮</returns>
        protected Button CreateActionLink(string text, int minW, int h, Action onClick, bool isDanger = false)
        {
            int btnH = (h > 0) ? h : 32;
            Button btn = new Button
            {
                Text = text,
                Height = btnH,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColCardBg,
                Cursor = Cursors.Hand,
                Font = GrokTheme.FontSecondary
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ColCardBg;
            btn.FlatAppearance.MouseDownBackColor = ColCardBg;

            using (Graphics gTemp = this.CreateGraphics())
            {
                SizeF sz = gTemp.MeasureString(text, btn.Font);
                btn.Width = Math.Max(minW, (int)sz.Width + 24);
            }

            bool hover = false;
            btn.MouseEnter += (s, e) => { hover = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { hover = false; btn.Invalidate(); };
            if (onClick != null) btn.Click += (s, e) => onClick();

            btn.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (SolidBrush bgB = new SolidBrush(ColCardBg))
                {
                    g.FillRectangle(bgB, 0, 0, btn.Width, btn.Height);
                }

                Color tc = isDanger
                    ? (hover ? GrokTheme.TextDestructiveHot : GrokTheme.TextDestructive)
                    : (hover ? GrokTheme.TextTitle : GrokTheme.TextSubtitle);

                using (SolidBrush tb = new SolidBrush(tc))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    g.DrawString(btn.Text, btn.Font, tb, new RectangleF(0, 0, btn.Width - 6, btn.Height), sf);
                }
            };
            return btn;
        }

        /// <summary>
        /// 自动根据所有子卡片的底部坐标自适应扩展面板自身总高度
        /// </summary>
        /// <param name="bottomPadding">底部呼吸内边距（默认 24px）</param>
        protected void AutoFitHeight(int bottomPadding = 24)
        {
            int maxY = 0;
            foreach (Control c in this.Controls)
            {
                if (c.Bottom > maxY) maxY = c.Bottom;
            }
            this.Height = maxY + bottomPadding;
        }
    }

    /// <summary>
    /// iOS / WorkBuddy 现代平滑自绘滑动开关 (Toggle Switch)
    /// <para>【职责】提供开/关状态的双态胶囊滑动指示控件，自绘翠绿/深灰底衬与白色圆钮滑块。</para>
    /// </summary>
    public class ModernToggleSwitch : Control
    {
        private bool _checked = false;
        private readonly Action<bool> _onChanged;

        /// <summary>获取或设置当前开关是否处于开启状态</summary>
        public bool Checked
        {
            get { return _checked; }
            set { if (_checked != value) { _checked = value; Invalidate(); } }
        }

        /// <summary>
        /// 初始化滑动开关
        /// </summary>
        /// <param name="isChecked">初始开/关状态</param>
        /// <param name="onChanged">状态切换后的回调通知</param>
        public ModernToggleSwitch(bool isChecked, Action<bool> onChanged)
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            this.Size = new Size(40, 22);
            this.Cursor = Cursors.Hand;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = GrokTheme.Level1;
            _checked = isChecked;
            _onChanged = onChanged;
            this.Click += (s, e) =>
            {
                _checked = !_checked;
                Invalidate();
                if (_onChanged != null) _onChanged(_checked);
            };
        }

        /// <summary>执行开关自绘：开启呈现翠绿 #1FD67E，关闭呈现深灰 #4A4A50，白色圆钮滑块平滑贴合两端</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush bgB = new SolidBrush(GrokTheme.Level1))
            {
                g.FillRectangle(bgB, 0, 0, this.Width, this.Height);
            }

            Color bg = _checked ? GrokTheme.AccentGreen : GrokTheme.TextDisabled;
            Rectangle pill = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (SolidBrush bb = new SolidBrush(bg))
            {
                MainForm.DrawRoundedRectangle(g, bb, pill, 11);
            }

            int knobSize = 16;
            int knobX = _checked ? (this.Width - knobSize - 3) : 3;
            int knobY = (this.Height - knobSize) / 2;
            Rectangle knobRect = new Rectangle(knobX, knobY, knobSize, knobSize);

            using (SolidBrush kb = new SolidBrush(Color.White))
            {
                g.FillEllipse(kb, knobRect);
            }
        }
    }

    /// <summary>
    /// WorkBuddy 现代平滑自绘刻度轨道滑块 (Stepped Slider)
    /// <para>【职责】提供离散刻度值选择器（如字号档位调整），支持鼠标拖拽与点击刻度吸附。</para>
    /// </summary>
    public class SteppedSlider : Control
    {
        private readonly string[] _labels;
        private readonly float[] _values;
        private int _selectedIndex;
        private readonly Action<float> _onValueChanged;
        private bool _isDragging = false;
        private const int TrackPadding = 18;

        /// <summary>
        /// 初始化刻度滑块
        /// </summary>
        /// <param name="labels">每个刻度点的文本标签数组</param>
        /// <param name="values">每个刻度点对应的实际数值数组</param>
        /// <param name="currentValue">初始数值</param>
        /// <param name="onValueChanged">数值变更后的回调通知</param>
        public SteppedSlider(string[] labels, float[] values, float currentValue, Action<float> onValueChanged)
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            this.Size = new Size(280, 34);
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = GrokTheme.Level1;
            this.Cursor = Cursors.Hand;
            this.TabStop = true;
            _labels = labels;
            _values = values;
            _onValueChanged = onValueChanged;

            SetCurrentValue(currentValue);
            this.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                Focus();
                Capture = true;
                _isDragging = true;
                HandleClick(e.X);
            };
            this.MouseMove += (s, e) => { if (_isDragging) HandleClick(e.X); };
            this.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) Capture = false; };
            this.MouseCaptureChanged += (s, e) => { if (!Capture) _isDragging = false; };
            this.GotFocus += (s, e) => Invalidate();
            this.LostFocus += (s, e) => Invalidate();
        }

        // 两个设置页面共享主界面字号。切页时更新滑块指示，不再次保存设置。
        internal void SetCurrentValue(float currentValue)
        {
            _selectedIndex = 0;
            float minDiff = float.MaxValue;
            for (int i = 0; i < _values.Length; i++)
            {
                float diff = Math.Abs(_values[i] - currentValue);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    _selectedIndex = i;
                }
            }

            Invalidate();
        }

        private void HandleClick(int mouseX)
        {
            int count = _labels.Length;
            int trackPad = TrackPadding;
            float trackW = this.Width - trackPad * 2;
            int bestIdx = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float x = trackPad + (trackW / (count - 1)) * i;
                float dist = Math.Abs(mouseX - x);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = i;
                }
            }

            SelectIndex(bestIdx);
        }

        private void SelectIndex(int index)
        {
            if (index != _selectedIndex && index >= 0 && index < _values.Length)
            {
                _selectedIndex = index;
                Invalidate();
                if (_onValueChanged != null && _selectedIndex < _values.Length)
                {
                    _onValueChanged(_values[_selectedIndex]);
                }
            }
        }

        /// <summary>将左右方向键留给当前滑块处理。</summary>
        /// <param name="keyData">按键及修饰键。</param>
        /// <returns>是否由当前控件处理。</returns>
        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
        }

        /// <summary>使用左右方向键调整一个档位。</summary>
        /// <param name="e">按键事件。</param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyData == Keys.Left) SelectIndex(_selectedIndex - 1);
            if (e.KeyData == Keys.Right) SelectIndex(_selectedIndex + 1);
        }

        /// <summary>绘制水平灰色轨道基准线、竖向离散刻度线、刻度文字与高光圆形滑块钮</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush bgB = new SolidBrush(GrokTheme.Level1))
            {
                g.FillRectangle(bgB, 0, 0, this.Width, this.Height);
            }

            int count = _labels.Length;
            int trackPad = TrackPadding;
            int railY = 8;
            float trackW = this.Width - trackPad * 2;

            // 1. 绘制水平灰色基准轨道线
            using (Pen rp = new Pen(Color.FromArgb(50, 50, 58), 2f))
            {
                g.DrawLine(rp, trackPad, railY, this.Width - trackPad, railY);
            }

            // 2. 绘制各离散刻度短线与底部档位标签
            using (Pen tp = new Pen(Color.FromArgb(70, 70, 80), 2f))
            using (Font lf = GrokTheme.FontSecondary)
            using (SolidBrush textBrush = new SolidBrush(GrokTheme.TextSecondary))
            using (SolidBrush activeText = new SolidBrush(GrokTheme.TextPrimary))
            {
                for (int i = 0; i < count; i++)
                {
                    float x = trackPad + (trackW / (count - 1)) * i;
                    g.DrawLine(tp, x, railY - 3, x, railY + 3);

                    string lbl = _labels[i];
                    SizeF sz = g.MeasureString(lbl, lf);
                    float tx = x - sz.Width / 2f;
                    float ty = railY + 6;
                    g.DrawString(lbl, lf, (i == _selectedIndex) ? activeText : textBrush, tx, ty);
                }
            }

            // 3. 绘制当前档位圆形滑块钮
            float thumbX = trackPad + (trackW / (count - 1)) * _selectedIndex;
            int knobSize = 12;
            Rectangle knobRect = new Rectangle((int)thumbX - knobSize / 2, railY - knobSize / 2, knobSize, knobSize);

            using (SolidBrush kb = new SolidBrush(Focused ? GrokTheme.AccentHot : GrokTheme.Accent))
            using (Pen kp = new Pen(GrokTheme.Level05, 1.5f))
            {
                g.FillEllipse(kb, knobRect);
                g.DrawEllipse(kp, knobRect);
            }
        }
    }
}
