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

        // 最近有效选中行，用于判断跨标题方向和局部重绘。
        private int _previousSelectedIndex = -1;

        // 主侧栏可提供不可选的分组标题；复用此控件的附件列表仍使用等高行。
        internal Func<object, bool> IsSectionHeading { get; set; }

        internal bool IsHeading(int index)
        {
            return IsSectionHeading != null && index >= 0 && index < Items.Count && IsSectionHeading(Items[index]);
        }

        internal void ClearSessionItems()
        {
            // 原生列表清空时可能不派发选中事件，同步丢弃指向旧列表的索引。
            _previousSelectedIndex = -1;
            Items.Clear();
        }

        internal void RestoreScrollAnchor(int index)
        {
            if (index < 0 || index >= Items.Count) return;
            int maxTop;
            int thumbHeight;
            GetScrollMetrics(out maxTop, out thumbHeight);
            TopIndex = Math.Min(index, maxTop);
        }

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
        /// 消息泵分发：处理标题和滑块点击，拦截原生直绘与系统滚动条。
        /// </summary>
        /// <param name="m">Windows 消息结构</param>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0201 || (m.Msg == 0x0202 && _isThumbDragging)) // 左键按下或结束滑块拖拽
            {
                // 消息只用低 32 位，两段坐标各为有符号 16 位。
                int position = unchecked((int)m.LParam.ToInt64());
                int x = unchecked((short)position);
                int y = position >> 16;

                // 原生 ListBox 会先选中鼠标下方的行。滑块直接走托管鼠标事件，保留当前会话。
                if (m.Msg == 0x0202)
                {
                    OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
                    return;
                }
                if (y >= 0 && y < ClientSize.Height && IsScrollHotZone(x))
                {
                    OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
                    return;
                }
                if (IsSectionHeading != null && IsHeading(IndexFromPoint(x, y))) return;
            }

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
            int maxTop;
            int thumbH;
            GetScrollMetrics(out maxTop, out thumbH);
            if (maxTop > 0 && this.ClientSize.Height > 0)
            {
                int thumbY = Math.Min(TopIndex, maxTop) * (ClientSize.Height - thumbH) / maxTop;

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

        private void GetScrollMetrics(out int maxTop, out int thumbHeight)
        {
            int viewHeight = ClientSize.Height;
            int contentHeight = 0;
            maxTop = Math.Max(0, Items.Count - 1);
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                contentHeight += DrawMode == DrawMode.OwnerDrawVariable ? GetItemHeight(i) : ItemHeight;
                if (contentHeight <= viewHeight) maxTop = i;
            }
            thumbHeight = contentHeight == 0 ? 0 :
                Math.Min(viewHeight, Math.Max(28, (int)((double)viewHeight * viewHeight / contentHeight)));
        }

        private bool IsScrollHotZone(int x)
        {
            if (x < ClientSize.Width - 12 || x >= ClientSize.Width) return false;
            int maxTop;
            int thumbHeight;
            GetScrollMetrics(out maxTop, out thumbHeight);
            return maxTop > 0;
        }

        /// <summary>鼠标滚轮事件：平滑滚动会话列表视口</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int maxTop;
            int thumbHeight;
            GetScrollMetrics(out maxTop, out thumbHeight);
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
            if (e.Button == MouseButtons.Left && IsScrollHotZone(e.X))
            {
                _isThumbDragging = true;
                _dragStartY = e.Y;
                _dragStartTop = this.TopIndex;
                Capture = true;
                this.Invalidate();
                return;
            }
            base.OnMouseDown(e);
        }

        /// <summary>鼠标移动事件：处理滑块悬停提亮与鼠标按住拖拽滚动</summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int maxTop;
            int thumbH;
            GetScrollMetrics(out maxTop, out thumbH);
            bool hover = (maxTop > 0 && e.X >= this.ClientSize.Width - 12);
            if (hover != _isThumbHover)
            {
                _isThumbHover = hover;
                this.Invalidate();
            }

            if (_isThumbDragging && maxTop > 0)
            {
                int viewH = this.ClientSize.Height;
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
                Capture = false;
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
            int current = SelectedIndex;
            if (IsHeading(current))
            {
                // 键盘越过分组标题，只有选中实际会话时才向主窗口发送事件。
                int previous = _previousSelectedIndex;
                int direction = previous >= 0 && previous < Items.Count && !IsHeading(previous) && current < previous ? -1 : 1;
                int next = FindSessionIndex(current, direction);
                SelectedIndex = next >= 0 ? next : FindSessionIndex(current, -direction);
                return;
            }

            int prev = _previousSelectedIndex;
            _previousSelectedIndex = current;

            if (prev >= 0 && prev < this.Items.Count)
            {
                try { this.Invalidate(this.GetItemRectangle(prev)); } catch { }
            }
            if (current >= 0 && current < this.Items.Count)
            {
                try { this.Invalidate(this.GetItemRectangle(current)); } catch { }
            }
            base.OnSelectedIndexChanged(e);
        }

        private int FindSessionIndex(int heading, int direction)
        {
            for (int index = heading + direction; index >= 0 && index < Items.Count; index += direction)
            {
                if (!IsHeading(index)) return index;
            }
            return -1;
        }
    }
}
