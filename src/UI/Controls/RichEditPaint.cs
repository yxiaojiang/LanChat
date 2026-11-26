using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 为聊天输入框提供 RichEdit 的离屏绘制和行坐标，依赖 Win32 与原生文本对象模型。
    /// 每次绘制持有一个临时缓冲，文本坐标查询持有的 COM 引用在查询结束时释放。
    /// </summary>
    internal sealed class RichEditPaint : IDisposable
    {
        private const int GetOleInterface = 0x043C;
        private const int TextDocumentRangeSlot = 24;
        private const int TextRangeGetPointSlot = 54;
        // TOM 客户区坐标、允许读取客户区外位置和范围起点的组合标志。
        private const int ClientStartPoint = 0x0320;
        private const int Baseline = 24; // 读取文字基线
        private const int LineBottom = 8; // 读取文字行底
        private IntPtr _document;
        private readonly ReadRange _readRange;

        /// <summary>对应 Win32 PAINTSTRUCT，只用于配对 BeginPaint 和 EndPaint。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct PaintStruct
        {
            private IntPtr _dc;
            private int _erase, _left, _top, _right, _bottom, _restore, _update;
            private int _reserved0, _reserved1, _reserved2, _reserved3;
            private int _reserved4, _reserved5, _reserved6, _reserved7;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr window, out PaintStruct paint);

        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr window, ref PaintStruct paint);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, out IntPtr lParam);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ReadRange(IntPtr document, int start, int end, out IntPtr range);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ReadPoint(IntPtr range, int flags, out int x, out int y);

        /// <summary>从当前 RichEdit 句柄获取文本对象模型，供本帧的表情和光标查询。</summary>
        /// <param name="window">输入框的原生句柄。</param>
        internal RichEditPaint(IntPtr window)
        {
            IntPtr ole;
            SendMessage(window, GetOleInterface, IntPtr.Zero, out ole);
            if (ole == IntPtr.Zero) return;
            try
            {
                var documentId = new Guid("8CC497C0-A1DF-11CE-8098-00AA0047BE5D");
                if (Marshal.QueryInterface(ole, ref documentId, out _document) == 0)
                    _readRange = (ReadRange)GetMethod(_document, TextDocumentRangeSlot, typeof(ReadRange));
            }
            finally { Marshal.Release(ole); }
        }

        /// <summary>释放本帧持有的原生文本对象模型。</summary>
        public void Dispose()
        {
            if (_document == IntPtr.Zero) return;
            Marshal.Release(_document);
            _document = IntPtr.Zero;
        }

        /// <summary>返回字符所在视觉行的文字基线和底边，单位为客户区像素。</summary>
        /// <param name="index">RichEdit 字符索引。</param>
        /// <param name="baseline">当前行基线。</param>
        /// <param name="bottom">当前行底边。</param>
        /// <returns>原生控件能读取该行坐标时返回 true。</returns>
        internal bool TryGetLineBounds(int index, out int baseline, out int bottom)
        {
            baseline = bottom = 0;
            IntPtr range;
            if (_readRange == null || _readRange(_document, index, index, out range) != 0 || range == IntPtr.Zero)
                return false;
            try
            {
                // 方法槽来自 Windows SDK 的 TOM.h，跳过 IDispatch 和未使用的编辑接口。
                var point = (ReadPoint)GetMethod(range, TextRangeGetPointSlot, typeof(ReadPoint));
                int x;
                return point(range, ClientStartPoint | Baseline, out x, out baseline) == 0 &&
                    point(range, ClientStartPoint | LineBottom, out x, out bottom) == 0;
            }
            finally { Marshal.Release(range); }
        }

        private static Delegate GetMethod(IntPtr instance, int slot, Type signature)
        {
            IntPtr table = Marshal.ReadIntPtr(instance);
            return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(table, slot * IntPtr.Size), signature);
        }

        /// <summary>把客户区先绘制到兼容的离屏 DC，再整帧复制给窗口或截图目标。</summary>
        /// <param name="window">正在绘制的 RichEdit 句柄。</param>
        /// <param name="target">截图 DC，或 IntPtr.Zero 以处理窗口的 WM_PAINT。</param>
        /// <param name="bounds">输入框的客户区。</param>
        /// <param name="paintFrame">向缓冲内绘制原生文字与自绘元素的回调。</param>
        internal static void Render(IntPtr window, IntPtr target, Rectangle bounds, Action<Graphics> paintFrame)
        {
            PaintStruct paint;
            bool windowPaint = target == IntPtr.Zero;
            if (windowPaint) target = BeginPaint(window, out paint);
            else paint = default(PaintStruct);
            try
            {
                if (target == IntPtr.Zero || bounds.Width <= 0 || bounds.Height <= 0) return;
                using (BufferedGraphics buffer = BufferedGraphicsManager.Current.Allocate(target, bounds))
                {
                    paintFrame(buffer.Graphics);
                    buffer.Render(target);
                }
            }
            finally
            {
                if (windowPaint) EndPaint(window, ref paint);
            }
        }
    }
}
