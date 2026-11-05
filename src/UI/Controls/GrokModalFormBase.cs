using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// GrokBot 统一样式无边框圆角模态窗基类
    /// 统一接管：
    /// 1. 16px 圆角 Region 与安全句柄回收 (无泄漏)
    /// 2. Windows DWM 阴影 (CS_DROPSHADOW)
    /// 3. 鼠标在空白区域按下并拖拽移动窗体
    /// 4. 1px 微光边框 (#2C2C32) 与 5 级纵深背景 (#1C1C1F Level1)
    /// 5. 标准右上角自绘关闭叉 (28×28px，距边 16px，支持悬停动效与抗锯齿)
    /// 6. ESC 键与 CancelButton 标准键盘回退
    /// </summary>
    public class GrokModalFormBase : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        protected static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        protected static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        protected static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        protected static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private bool _isCloseHover = false;
        private bool _showCloseButton = true;
        private bool _closeOnEscape = true;
        private string _titleText = string.Empty;

        public bool ShowCloseButton
        {
            get { return _showCloseButton; }
            set
            {
                if (_showCloseButton != value)
                {
                    _showCloseButton = value;
                    this.Invalidate(CloseButtonRect);
                }
            }
        }

        public bool CloseOnEscape
        {
            get { return _closeOnEscape; }
            set { _closeOnEscape = value; }
        }

        public string TitleText
        {
            get { return _titleText; }
            set
            {
                if (_titleText != value)
                {
                    _titleText = value ?? string.Empty;
                    this.Invalidate();
                }
            }
        }

        public Rectangle CloseButtonRect
        {
            get
            {
                return new Rectangle(
                    this.ClientSize.Width - GrokTheme.CloseButtonMargin - GrokTheme.CloseButtonSize,
                    GrokTheme.CloseButtonMargin,
                    GrokTheme.CloseButtonSize,
                    GrokTheme.CloseButtonSize);
            }
        }

        public GrokModalFormBase()
        {
            this.SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.BackColor = GrokTheme.Level1;
            this.DoubleBuffered = true;
            this.Font = GrokTheme.FontSecondary;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected void UpdateFormRegion()
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            try
            {
                IntPtr rgn = CreateRoundRectRgn(0, 0, this.Width + 1, this.Height + 1, GrokTheme.RadiusModal, GrokTheme.RadiusModal);
                if (rgn != IntPtr.Zero)
                {
                    Region oldRegion = this.Region;
                    this.Region = Region.FromHrgn(rgn);
                    DeleteObject(rgn);
                    if (oldRegion != null)
                    {
                        oldRegion.Dispose();
                    }
                }
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateFormRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateFormRegion();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_showCloseButton && CloseButtonRect.Contains(e.Location))
                {
                    return; // 点击在关闭叉上，不触发拖拽
                }

                // 允许拖拽移动窗体
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_showCloseButton)
            {
                bool hover = CloseButtonRect.Contains(e.Location);
                if (hover != _isCloseHover)
                {
                    _isCloseHover = hover;
                    this.Invalidate(CloseButtonRect);
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isCloseHover)
            {
                _isCloseHover = false;
                this.Invalidate(CloseButtonRect);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_showCloseButton && e.Button == MouseButtons.Left && CloseButtonRect.Contains(e.Location))
            {
                OnCloseButtonClick();
            }
        }

        protected virtual void OnCloseButtonClick()
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && _closeOnEscape)
            {
                OnCloseButtonClick();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// 便捷辅助：允许任何内部子控件（如标题栏 Label 或 Panel）联动拖拽窗体
        /// </summary>
        public void EnableDrag(Control control)
        {
            if (control == null) return;
            control.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
                }
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. 绘制暗黑背景与 1px 微光边框
            Rectangle bounds = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (SolidBrush bgBrush = new SolidBrush(this.BackColor))
            using (Pen borderPen = new Pen(GrokTheme.Border, 1f))
            {
                GrokTheme.DrawRoundedRectangle(g, bgBrush, bounds, GrokTheme.RadiusModal);
                GrokTheme.DrawRoundedRectangle(g, borderPen, bounds, GrokTheme.RadiusModal);
            }

            // 2. 绘制大标题 (Docs §3 & §9: 距顶部 28px，距左侧 32px，18.0pt Bold #FFFFFF)
            if (!string.IsNullOrEmpty(_titleText))
            {
                int maxTitleW = this.Width - 32 - (_showCloseButton ? (GrokTheme.CloseButtonSize + GrokTheme.CloseButtonMargin * 2) : 32);
                Rectangle titleRect = new Rectangle(32, 28, maxTitleW, 36);
                using (Font titleFont = GrokTheme.FontTitle)
                {
                    GrokTheme.DrawUiTextLeft(g, _titleText, titleFont, GrokTheme.TextTitle, titleRect.X, titleRect.Y, titleRect.Width, titleRect.Height);
                }
            }

            // 3. 绘制右上角标准自绘关闭按钮 (28×28px)
            if (_showCloseButton)
            {
                Rectangle r = CloseButtonRect;
                if (_isCloseHover)
                {
                    using (SolidBrush hoverBg = new SolidBrush(GrokTheme.Level3Hot))
                    {
                        GrokTheme.DrawRoundedRectangle(g, hoverBg, r, 6);
                    }
                }

                Color iconColor = _isCloseHover ? GrokTheme.TextTitle : GrokTheme.TextSecondary;
                WeChatIcon.DrawDismissIcon(g, r, iconColor);
            }

            // 4. 调用派生类内容绘制挂钩
            OnPaintContent(g);
        }

        /// <summary>
        /// 派生类可重写此方法进行自身特定的 GDI+ 自绘，无需重复编写底色、外边框与关闭叉绘制逻辑
        /// </summary>
        protected virtual void OnPaintContent(Graphics g)
        {
        }
    }
}
