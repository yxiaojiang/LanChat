using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 设置中心子面板抽象基类
    /// </summary>
    public abstract class SettingsPanelBase : DoubleBufferedPanel
    {
        public const int CARD_W = 900;

        protected SettingsContext Context { get; private set; }

        // 统一颜色设计令牌 (对齐 GrokTheme)
        protected static readonly Color ColWindowBg = GrokTheme.Level05;
        protected static readonly Color ColCardBg = GrokTheme.Level1;
        protected static readonly Color ColCardBorder = GrokTheme.Border;
        protected static readonly Color ColCardDivider = GrokTheme.BorderInner;
        protected static readonly Color ColTextPrimary = GrokTheme.TextPrimary;
        protected static readonly Color ColTextSecondary = GrokTheme.TextSecondary;
        protected static readonly Color ColSectionHeader = GrokTheme.TextSubtitle;
        protected static readonly Color ColMintGreen = GrokTheme.AccentGreen;
        protected static readonly Color ColSwitchOff = GrokTheme.TextDisabled;
        protected static readonly Color ColPillBg = GrokTheme.Level2;
        protected static readonly Color ColPillBorder = GrokTheme.Border;

        public SettingsPanelBase(SettingsContext context)
        {
            Context = context;
            this.Location = new Point(0, 0);
            this.Size = new Size(CARD_W, 600);
            this.BackColor = ColWindowBg;
        }

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
                Rectangle r = new Rectangle(0, 0, w - 1, h - 1);
                using (SolidBrush b = new SolidBrush(ColCardBg))
                using (Pen pen = new Pen(ColCardBorder, 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 12);
                    MainForm.DrawRoundedRectangle(e.Graphics, pen, r, 12);
                }
            };
            return p;
        }

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
                Font = GrokTheme.FontSecondary
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
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (SolidBrush bgB = new SolidBrush(ColCardBg))
                {
                    g.FillRectangle(bgB, 0, 0, btn.Width, btn.Height);
                }

                Rectangle r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (SolidBrush b = new SolidBrush(hover ? GrokTheme.Level3Hot : ColPillBg))
                using (Pen p = new Pen(hover ? Color.FromArgb(70, 70, 82) : ColPillBorder, 1f))
                {
                    GrokTheme.DrawRoundedRectangle(g, b, r, 7);
                    GrokTheme.DrawRoundedRectangle(g, p, r, 7);
                }

                string curLabel = btn.Text.Replace("  ∨", "").Trim();
                using (SolidBrush tb = new SolidBrush(GrokTheme.TextTitle))
                {
                    float ty = (btn.Height - btn.Font.GetHeight(g)) / 2f;
                    g.DrawString(curLabel, btn.Font, tb, new PointF(16, ty));
                }

                using (Font arrowFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
                using (SolidBrush arrowBrush = new SolidBrush(hover ? GrokTheme.TextTitle : GrokTheme.TextSecondary))
                {
                    float ay = (btn.Height - arrowFont.GetHeight(g)) / 2f;
                    g.DrawString("∨", arrowFont, arrowBrush, new PointF(btn.Width - 22, ay));
                }
            };
            return btn;
        }

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
    /// iOS / WorkBuddy 现代滑动开关 (Toggle Switch)
    /// </summary>
    public class ModernToggleSwitch : Control
    {
        private bool _checked = false;
        private readonly Action<bool> _onChanged;

        public bool Checked
        {
            get { return _checked; }
            set { if (_checked != value) { _checked = value; Invalidate(); } }
        }

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
    /// WorkBuddy 刻度轨道滑块 (Stepped Slider)
    /// </summary>
    public class SteppedSlider : Control
    {
        private readonly string[] _labels;
        private readonly float[] _values;
        private int _selectedIndex;
        private readonly Action<float> _onValueChanged;
        private bool _isDragging = false;

        public SteppedSlider(string[] labels, float[] values, float currentValue, Action<float> onValueChanged)
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            this.Size = new Size(280, 34);
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = GrokTheme.Level1;
            this.Cursor = Cursors.Hand;
            _labels = labels;
            _values = values;
            _onValueChanged = onValueChanged;

            _selectedIndex = 0;
            float minDiff = float.MaxValue;
            for (int i = 0; i < values.Length; i++)
            {
                float diff = Math.Abs(values[i] - currentValue);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    _selectedIndex = i;
                }
            }

            this.MouseDown += (s, e) => { _isDragging = true; HandleClick(e.X); };
            this.MouseMove += (s, e) => { if (_isDragging) HandleClick(e.X); };
            this.MouseUp += (s, e) => { _isDragging = false; };
        }

        private void HandleClick(int mouseX)
        {
            int count = _labels.Length;
            int trackPad = 12;
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

            if (bestIdx != _selectedIndex)
            {
                _selectedIndex = bestIdx;
                Invalidate();
                if (_onValueChanged != null && _selectedIndex < _values.Length)
                {
                    _onValueChanged(_values[_selectedIndex]);
                }
            }
        }

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
            int trackPad = 12;
            int railY = 8;
            float trackW = this.Width - trackPad * 2;

            // 轨道线
            using (Pen rp = new Pen(Color.FromArgb(50, 50, 58), 2f))
            {
                g.DrawLine(rp, trackPad, railY, this.Width - trackPad, railY);
            }

            // 刻度与文字
            using (Pen tp = new Pen(Color.FromArgb(70, 70, 80), 2f))
            using (Font lf = new Font(GrokTheme.FontFamily, 8.0f, FontStyle.Regular))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(120, 120, 132)))
            using (SolidBrush activeText = new SolidBrush(Color.FromArgb(235, 235, 242)))
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

            // 滑块钮 (圆点)
            float thumbX = trackPad + (trackW / (count - 1)) * _selectedIndex;
            int knobSize = 12;
            Rectangle knobRect = new Rectangle((int)thumbX - knobSize / 2, railY - knobSize / 2, knobSize, knobSize);

            using (SolidBrush kb = new SolidBrush(Color.FromArgb(210, 210, 220)))
            using (Pen kp = new Pen(Color.FromArgb(24, 24, 28), 1.5f))
            {
                g.FillEllipse(kb, knobRect);
                g.DrawEllipse(kp, knobRect);
            }
        }
    }
}
