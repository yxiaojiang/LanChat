using System;
using System.Drawing;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 全局双缓冲无闪烁容器面板
    /// <para>【职责】作为复杂子控件树与自绘界面的基础容器，消除 WinForms 原生重绘闪烁。</para>
    /// <para>【特性】启用优化双缓冲、所有绘制在 WmPaint 中完成、拦截 WM_ERASEBKGND 并启用 WS_CLIPCHILDREN。</para>
    /// </summary>
    public class DoubleBufferedPanel : Panel
    {
        /// <summary>
        /// 初始化双缓冲面板并开启硬件级抗闪烁绘制样式
        /// </summary>
        public DoubleBufferedPanel()
        {
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            this.DoubleBuffered = true;
        }

        /// <summary>
        /// 扩展窗口样式参数：追加 WS_CLIPCHILDREN (0x02000000)，防止容器背景重绘时覆盖子控件区域导致闪烁
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x02000000; // WS_CLIPCHILDREN
                return cp;
            }
        }

        /// <summary>
        /// 消息泵分发：彻底压制 WM_ERASEBKGND (0x0014) 背景擦除，防止呈现灰白色闪烁底色
        /// </summary>
        /// <param name="m">Windows 消息结构引用</param>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0014) // WM_ERASEBKGND
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }
    }
}
