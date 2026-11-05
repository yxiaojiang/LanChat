using System;
using System.Drawing;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 全局消息过滤器：拦截无边框窗口四周 8px 边缘的鼠标拖拽消息，
    /// 通过 Windows 原生 WM_SYSCOMMAND (SC_SIZE) 实现丝滑四周与四角鼠标拖拽缩放
    /// </summary>
    internal class FormResizeMessageFilter : IMessageFilter
    {
        private readonly MainForm _form;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_SETCURSOR = 0x0020;
        private const int WM_SYSCOMMAND = 0x0112;

        private const int SC_SIZE_LEFT = 0xF001;
        private const int SC_SIZE_RIGHT = 0xF002;
        private const int SC_SIZE_TOP = 0xF003;
        private const int SC_SIZE_TOPLEFT = 0xF004;
        private const int SC_SIZE_TOPRIGHT = 0xF005;
        private const int SC_SIZE_BOTTOM = 0xF006;
        private const int SC_SIZE_BOTTOMLEFT = 0xF007;
        private const int SC_SIZE_BOTTOMRIGHT = 0xF008;

        public FormResizeMessageFilter(MainForm form)
        {
            _form = form;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (_form == null || _form.IsDisposed || !_form.Visible) return false;
            if (_form.WindowState != FormWindowState.Normal) return false;

            if (m.Msg == WM_MOUSEMOVE || m.Msg == WM_LBUTTONDOWN || m.Msg == WM_SETCURSOR)
            {
                Point screenPt = Cursor.Position;
                Rectangle bounds = _form.Bounds;
                if (!bounds.Contains(screenPt)) return false;

                int grip = 8;
                bool left = screenPt.X < bounds.Left + grip;
                bool right = screenPt.X >= bounds.Right - grip;
                bool top = screenPt.Y < bounds.Top + grip;
                bool bottom = screenPt.Y >= bounds.Bottom - grip;

                if (!left && !right && !top && !bottom) return false;

                Cursor cursor = Cursors.Default;
                int dir = 0;

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
