using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 自绘暗黑会话列表 (SessionListBox)
    /// 特性：
    /// 1. 彻底禁用 Windows 原生灰白色滚动条 (Zero White Scrollbar 规范)
    /// 2. 自绘超细 4px/5px 极简暗灰浮动滑动条，支持悬停提亮、鼠标拖拽与平滑滚轮
    /// 3. 双缓冲纯 GDI+ 无闪烁抗锯齿渲染
    /// </summary>
    public class SessionListBox : ListBox
    {
        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        private bool _isThumbHover = false;
        private bool _isThumbDragging = false;
        private int _dragStartY = 0;
        private int _dragStartTop = 0;

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

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style &= ~0x00200000; // WS_VSCROLL
                cp.Style |= 0x02000000;  // WS_CLIPCHILDREN
                return cp;
            }
        }

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

            // 确保系统在任何重绘周期都不会插入原生非客户区滚动条
            if (m.Msg == 0x0085) // WM_NCPAINT
            {
                try { ShowScrollBar(this.Handle, 1, false); } catch { }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // 压制系统默认底色擦除，在 OnPaint 中统一全画幅双缓冲填充，避免白色闪烁
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (SolidBrush bg = new SolidBrush(this.BackColor))
            {
                g.FillRectangle(bg, e.ClipRectangle);
            }

            int count = this.Items.Count;
            if (count == 0 || this.ItemHeight <= 0) return;

            // 绘制会话行项
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

            // 自绘极简暗灰浮动滑块 (Zero White Scrollbar)
            int visibleCount = Math.Max(1, this.ClientSize.Height / Math.Max(1, this.ItemHeight));
            if (count > visibleCount && this.ClientSize.Height > 0)
            {
                int viewH = this.ClientSize.Height;
                int maxTop = count - visibleCount;
                int thumbH = Math.Max(28, (int)((float)visibleCount / count * viewH));
                int thumbY = (int)((float)this.TopIndex / maxTop * (viewH - thumbH));

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

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_isThumbDragging)
            {
                _isThumbDragging = false;
                this.Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_isThumbHover)
            {
                _isThumbHover = false;
                this.Invalidate();
            }
            base.OnMouseLeave(e);
        }

        private int _previousSelectedIndex = -1;

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            int prev = _previousSelectedIndex;
            int curr = this.SelectedIndex;
            _previousSelectedIndex = curr;

            // 仅精准刷新旧选中行与新选中行的脏矩形，避免全量重绘导致的整栏文字闪烁
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
