using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// GrokBot 统一样式无边框圆角模态窗基类
    /// <para>【职责】全客户端所有独立模态对话框的统一父类，提供标准化无边框外壳与系统窗口级特性。</para>
    /// <para>【特性】
    /// 1. 16px 圆角 Region 剪裁与 GDI 原生句柄安全回收（零泄漏）；
    /// 2. Windows DWM 真实阴影 (CS_DROPSHADOW)；
    /// 3. 空白区域鼠标按下原生 WM_NCLBUTTONDOWN 拖拽移动窗体；
    /// 4. 1px 微光边框 (#2C2C32) 与 5 级纵深背景 (#1C1C1F Level1)；
    /// 5. 标准右上角自绘关闭叉（28×28px，距边 16px，悬停高亮与平滑抗锯齿）；
    /// 6. ESC 键与 CancelButton 标准键盘回退。
    /// </para>
    /// </summary>
    public class GrokModalFormBase : Form
    {
        // ==========================================
        // Win32 原生 GDI 与窗口消息 API 互操作
        // ==========================================

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

        // 交互状态字段
        private bool _isCloseHover = false;
        private bool _showCloseButton = true;
        private bool _closeOnEscape = true;
        private string _titleText = string.Empty;

        /// <summary>
        /// 获取或设置是否在右上角显示统一自绘的 28×28px 关闭按钮
        /// </summary>
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

        /// <summary>
        /// 获取或设置按下键盘 ESC 键时是否自动关闭本窗体并返回 DialogResult.Cancel
        /// </summary>
        public bool CloseOnEscape
        {
            get { return _closeOnEscape; }
            set { _closeOnEscape = value; }
        }

        /// <summary>
        /// 获取或设置弹窗左上角主标题文本（采用 Docs 规范：18.0pt 粗体，X:32, Y:28）
        /// </summary>
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

        /// <summary>
        /// 获取右上角关闭按钮的标准几何矩形范围（距边 16px，尺寸 28×28px）
        /// </summary>
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

        /// <summary>
        /// 初始化模态窗体基类，配置双缓冲、无边框、屏幕居中与默认暗黑底色
        /// </summary>
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

        /// <summary>
        /// 扩展窗体创建参数：附加 CS_DROPSHADOW (0x00020000)，激活 Windows 桌面窗口管理器 (DWM) 的平滑外阴影
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        /// <summary>
        /// 动态刷新并应用 16px 圆角 Region，确保旧 Region 句柄安全释放
        /// </summary>
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

        /// <summary>句柄创建后初次建立圆角 Region</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateFormRegion();
        }

        /// <summary>窗体尺寸变化时同步重建圆角 Region</summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateFormRegion();
        }

        // ==========================================
        // 拖拽移动与关闭按钮交互
        // ==========================================

        /// <summary>鼠标按下事件：若位于空白区则启动系统级窗体拖动</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_showCloseButton && CloseButtonRect.Contains(e.Location))
                {
                    return; // 点击在关闭叉上，不触发拖拽
                }

                // 释放鼠标捕获并通过 WM_NCLBUTTONDOWN 通知 Windows 调度窗口移动
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
            }
        }

        /// <summary>鼠标移动事件：检测右上角关闭按钮悬停状态</summary>
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

        /// <summary>鼠标移出窗体：恢复关闭按钮普通状态</summary>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isCloseHover)
            {
                _isCloseHover = false;
                this.Invalidate(CloseButtonRect);
            }
        }

        /// <summary>鼠标松开事件：若在关闭按钮区域内松开则触发关闭</summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_showCloseButton && e.Button == MouseButtons.Left && CloseButtonRect.Contains(e.Location))
            {
                OnCloseButtonClick();
            }
        }

        /// <summary>
        /// 关闭按钮点击虚方法：默认设置 DialogResult 为 Cancel 并关闭当前窗体
        /// </summary>
        protected virtual void OnCloseButtonClick()
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// 键盘快捷键预处理：支持 ESC 键快捷取消并关闭
        /// </summary>
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
        /// 便捷辅助：允许任何内部子控件（如标题栏 Label、面板容器）联动拖拽窗体
        /// </summary>
        /// <param name="control">目标子控件引用</param>
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

        // ==========================================
        // GDI+ 统一模态窗体渲染管线
        // ==========================================

        /// <summary>
        /// 执行模态窗外壳自绘：包含 16px 圆角底色、1px 微光描边、大标题与右上角关闭叉
        /// </summary>
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

            // 3. 绘制右上角标准自绘关闭按钮 (28×28px，悬停高亮)
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

            // 4. 调用派生类专属内容绘制挂钩
            OnPaintContent(g);
        }

        /// <summary>
        /// 派生类可重写此方法进行自身特定的 GDI+ 自绘，无需重复编写底色、外边框与关闭叉逻辑
        /// </summary>
        /// <param name="g">GDI+ 画布上下文</param>
        protected virtual void OnPaintContent(Graphics g)
        {
        }
    }
}
