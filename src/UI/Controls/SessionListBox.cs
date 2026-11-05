using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 自绘暗黑会话列表控件 (SessionListBox)
    /// <para>【职责】提供左侧局域网全员群聊与节点联系人列表的平滑双缓冲自绘展示。</para>
    /// <para>【特性】
    /// 1. 彻底禁用 Windows 原生灰白色滚动条 (Zero White Scrollbar 契约)；
    /// 2. 纯自绘超细 4px/5px 极简暗灰浮动滑块，支持悬停提亮、鼠标拖拽定位与平滑滚轮；
    /// 3. 双缓冲无闪烁抗锯齿渲染，拦截原生 OwnerDraw 反射与背景擦除。
    /// </para>
    /// </summary>
    public class SessionListBox : ListBox
    {
        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        // 自绘滑块交互状态
        private bool _isThumbHover = false;
        private bool _isThumbDragging = false;
        private int _dragStartY = 0;
        private int _dragStartTop = 0;
        private int _previousSelectedIndex = -1;

        /// <summary>
        /// 初始化暗黑会话列表控件，配置双缓冲与自绘样式
        /// </summary>
        public SessionListBox()
        {
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
        }

        /// <summary>
        /// 重写窗口创建参数：剥离原生 WS_VSCROLL 垂直滚动条，追加 WS_CLIPCHILDREN
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style &= ~0x00200000; // 移除 WS_VSCROLL
                cp.Style |= 0x02000000;  // 追加 WS_CLIPCHILDREN
                return cp;
            }
        }

        /// <summary>
        /// 消息泵分发：拦截背景擦除与原生非缓冲单项反射绘制，压制非客户区系统滚动条
        /// </summary>
        /// <param name="m">Windows 消息结构</param>
        protected override void WndProc(ref Message m)
        {
            // 彻底拦截系统背景擦除，避免白色/灰色重绘闪烁
            if (m.Msg == 0x0014) // WM_ERASEBKGND
            {
                m.Result = (IntPtr)1;
                return;
            }

            // 拦截 Win32 原生 OwnerDraw 反射消息 (WM_REFLECT + WM_DRAWITEM)
            // 原生 ListBox 会在此消息中使用无缓冲的窗口 DC 直绘单个项，随后又被双缓冲 OnPaint 覆盖，造成文字和图形瞬间重绘闪烁
            const int WM_REFLECT = 0x2000;
            const int WM_DRAWITEM = 0x002B;
            if (m.Msg == (WM_REFLECT | WM_DRAWITEM))
            {
                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);

            // 确保系统在任何非客户区重绘周期都不会插入原生非客户区滚动条
            if (m.Msg == 0x0085) // WM_NCPAINT
            {
                try { ShowScrollBar(this.Handle, 1, false); } catch { }
            }
        }

        /// <summary>
        /// 压制系统默认底色擦除，在 OnPaint 中统一全画幅双缓冲填充，避免白色闪烁
        /// </summary>
        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
        }

        /// <summary>
        /// 执行会话列表全画幅自绘：包含背景底衬、可见会话行项分发及自绘极简暗灰浮动滑块
        /// </summary>
        /// <param name="e">绘制事件参数</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 1. 双缓冲填充整栏底色
            using (SolidBrush bg = new SolidBrush(this.BackColor))
            {
                g.FillRectangle(bg, e.ClipRectangle);
            }

            int count = this.Items.Count;
            if (count == 0 || this.ItemHeight <= 0) return;

            // 2. 遍历并绘制可视区域内的会话行项
            for (int i = 0; i < count; i++)
            {
                Rectangle itemRect;
                try
                {
                    itemRect = this.GetItemRectangle(i);
                }
                catch
                {
                    continue;
                }

                if (itemRect.Bottom < 0 || itemRect.Top > this.ClientSize.Height)
                    continue;
                if (!e.ClipRectangle.IntersectsWith(itemRect))
                    continue;

                DrawItemState state = DrawItemState.Default;
                if (i == this.SelectedIndex)
                    state |= DrawItemState.Selected;
                if (this.Focused && i == this.SelectedIndex)
                    state |= DrawItemState.Focus;

                DrawItemEventArgs dea = new DrawItemEventArgs(
                    g, this.Font, itemRect, i, state, this.ForeColor, this.BackColor);
                this.OnDrawItem(dea);
            }

            // 3. 自绘极简暗灰浮动滑块 (Zero White Scrollbar 契约)
            int visibleCount = Math.Max(1, this.ClientSize.Height / Math.Max(1, this.ItemHeight));
            if (count > visibleCount && this.ClientSize.Height > 0)
            {
                int viewH = this.ClientSize.Height;
                int maxTop = count - visibleCount;
                int thumbH = Math.Max(28, (int)((float)visibleCount / count * viewH));
                int thumbY = (int)((float)this.TopIndex / maxTop * (viewH - thumbH));

                // 静止 4px，悬停/拖拽提亮并加宽至 5px
                int barW = (_isThumbHover || _isThumbDragging) ? 5 : 4;
                int barX = this.ClientSize.Width - barW - 2;

                Rectangle thumbRect = new Rectangle(barX, thumbY, barW, thumbH);
                Color thumbCol = (_isThumbHover || _isThumbDragging) ? Color.FromArgb(100, 100, 112) : Color.FromArgb(52, 52, 60);

                using (SolidBrush sb = new SolidBrush(thumbCol))
                {
                    MainForm.DrawRoundedRectangle(g, sb, thumbRect, barW / 2);
                }
            }
        }

        /// <summary>鼠标滚轮事件：平滑滚动会话列表视口</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int visibleCount = Math.Max(1, this.ClientSize.Height / Math.Max(1, this.ItemHeight));
            int maxTop = Math.Max(0, this.Items.Count - visibleCount);
            if (maxTop <= 0) return;

            int change = (e.Delta > 0) ? -1 : 1;
            int newTop = this.TopIndex + change;
            if (newTop < 0) newTop = 0;
            if (newTop > maxTop) newTop = maxTop;
            if (newTop != this.TopIndex)
            {
                this.TopIndex = newTop;
                this.Invalidate();
            }
        }

        /// <summary>鼠标按下事件：判定是否点击滑块启动拖拽</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            int visibleCount = Math.Max(1, this.ClientSize.Height / Math.Max(1, this.ItemHeight));
            int maxTop = Math.Max(0, this.Items.Count - visibleCount);
            if (maxTop > 0 && e.X >= this.ClientSize.Width - 12)
            {
                _isThumbDragging = true;
                _dragStartY = e.Y;
                _dragStartTop = this.TopIndex;
                this.Invalidate();
                return;
            }
            base.OnMouseDown(e);
        }

        /// <summary>鼠标移动事件：处理滑块悬停提亮与鼠标按住拖拽滚动</summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int visibleCount = Math.Max(1, this.ClientSize.Height / Math.Max(1, this.ItemHeight));
            int maxTop = Math.Max(0, this.Items.Count - visibleCount);
            bool hover = (maxTop > 0 && e.X >= this.ClientSize.Width - 12);
            if (hover != _isThumbHover)
            {
                _isThumbHover = hover;
                this.Invalidate();
            }

            if (_isThumbDragging && maxTop > 0)
            {
                int viewH = this.ClientSize.Height;
                int thumbH = Math.Max(28, (int)((float)visibleCount / this.Items.Count * viewH));
                int availTrack = Math.Max(1, viewH - thumbH);
                int dy = e.Y - _dragStartY;
                int deltaTop = (int)((float)dy / availTrack * maxTop);
                int targetTop = _dragStartTop + deltaTop;
                if (targetTop < 0) targetTop = 0;
                if (targetTop > maxTop) targetTop = maxTop;
                if (targetTop != this.TopIndex)
                {
                    this.TopIndex = targetTop;
                    this.Invalidate();
                }
                return;
            }
            base.OnMouseMove(e);
        }

        /// <summary>鼠标松开事件：释放滑块拖拽状态</summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_isThumbDragging)
            {
                _isThumbDragging = false;
                this.Invalidate();
            }
            base.OnMouseUp(e);
        }

        /// <summary>鼠标离开控件：取消滑块悬停态</summary>
        protected override void OnMouseLeave(EventArgs e)
        {
            if (_isThumbHover)
            {
                _isThumbHover = false;
                this.Invalidate();
            }
            base.OnMouseLeave(e);
        }

        /// <summary>
        /// 选中索引变更事件：仅精准刷新旧选中行与新选中行的脏矩形，避免全量重绘导致的整栏文字闪烁
        /// </summary>
        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            int prev = _previousSelectedIndex;
            int curr = this.SelectedIndex;
            _previousSelectedIndex = curr;

            if (prev >= 0 && prev < this.Items.Count)
            {
                try { this.Invalidate(this.GetItemRectangle(prev)); } catch { }
            }
            if (curr >= 0 && curr < this.Items.Count)
            {
                try { this.Invalidate(this.GetItemRectangle(curr)); } catch { }
            }
        }
    }
}
