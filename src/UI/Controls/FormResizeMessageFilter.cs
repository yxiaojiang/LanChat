using System;
using System.Drawing;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 全局无边框窗体拖拽缩放消息过滤器
    /// <para>【职责】全局监听鼠标移动与点击，拦截无边框主窗体四周 8px 边缘的拖拽交互。</para>
    /// <para>【原理】通过 Windows 原生 WM_SYSCOMMAND (SC_SIZE) 将尺寸调节委托给系统窗口管理器，实现平滑无卡顿的边缘与四角缩放。</para>
    /// </summary>
    internal class FormResizeMessageFilter : IMessageFilter
    {
        private readonly MainForm _form;

        // Windows 消息常量
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_SETCURSOR = 0x0020;
        private const int WM_SYSCOMMAND = 0x0112;

        // 系统缩放方向标识
        private const int SC_SIZE_LEFT = 0xF001;
        private const int SC_SIZE_RIGHT = 0xF002;
        private const int SC_SIZE_TOP = 0xF003;
        private const int SC_SIZE_TOPLEFT = 0xF004;
        private const int SC_SIZE_TOPRIGHT = 0xF005;
        private const int SC_SIZE_BOTTOM = 0xF006;
        private const int SC_SIZE_BOTTOMLEFT = 0xF007;
        private const int SC_SIZE_BOTTOMRIGHT = 0xF008;

        /// <summary>
        /// 创建并初始化无边框窗体缩放过滤器
        /// </summary>
        /// <param name="form">被代理的主窗体实例引用</param>
        public FormResizeMessageFilter(MainForm form)
        {
            _form = form;
        }

        /// <summary>
        /// 消息预过滤处理：检测鼠标是否位于窗体边缘 8px 握持区，并动态切换光标形态或派发原生缩放指令
        /// </summary>
        /// <param name="m">Windows 消息结构</param>
        /// <returns>若已消费该消息则返回 true，否则交由系统默认派发</returns>
        public bool PreFilterMessage(ref Message m)
        {
            // 窗体已被释放、不可见或最大化状态下不处理缩放
            if (_form == null || _form.IsDisposed || !_form.Visible) return false;
            if (_form.WindowState != FormWindowState.Normal) return false;

            if (m.Msg == WM_MOUSEMOVE || m.Msg == WM_LBUTTONDOWN || m.Msg == WM_SETCURSOR)
            {
                Point screenPt = Cursor.Position;
                Rectangle bounds = _form.Bounds;
                if (!bounds.Contains(screenPt)) return false;

                // 8px 边缘感应握持区 (Grip Area)
                int grip = 8;
                bool left = screenPt.X < bounds.Left + grip;
                bool right = screenPt.X >= bounds.Right - grip;
                bool top = screenPt.Y < bounds.Top + grip;
                bool bottom = screenPt.Y >= bounds.Bottom - grip;

                if (!left && !right && !top && !bottom) return false;

                Cursor cursor = Cursors.Default;
                int dir = 0;

                // 八个方向判定
                if (left && top) { cursor = Cursors.SizeNWSE; dir = SC_SIZE_TOPLEFT; }
                else if (right && top) { cursor = Cursors.SizeNESW; dir = SC_SIZE_TOPRIGHT; }
                else if (left && bottom) { cursor = Cursors.SizeNESW; dir = SC_SIZE_BOTTOMLEFT; }
                else if (right && bottom) { cursor = Cursors.SizeNWSE; dir = SC_SIZE_BOTTOMRIGHT; }
                else if (left) { cursor = Cursors.SizeWE; dir = SC_SIZE_LEFT; }
                else if (right) { cursor = Cursors.SizeWE; dir = SC_SIZE_RIGHT; }
                else if (top) { cursor = Cursors.SizeNS; dir = SC_SIZE_TOP; }
                else if (bottom) { cursor = Cursors.SizeNS; dir = SC_SIZE_BOTTOM; }

                if (dir != 0)
                {
                    Cursor.Current = cursor;
                    // 鼠标左键按下时，释放捕获并通知 Windows 核心进入原生拖拽缩放模态
                    if (m.Msg == WM_LBUTTONDOWN)
                    {
                        MainForm.ReleaseCaptureWin32();
                        MainForm.SendMessageWin32(_form.Handle, WM_SYSCOMMAND, (IntPtr)dir, IntPtr.Zero);
                        return true;
                    }
                    if (m.Msg == WM_SETCURSOR)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
