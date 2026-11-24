using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 聊天输入框，依赖 Windows 原生编辑控件及系统剪贴板。
    /// 将文件和图片交给宿主暂存，文字沿用原生编辑流程，随输入框释放。
    /// </summary>
    internal sealed class ChatInputBox : TextBox
    {
        private const int PasteMessage = 0x0302;
        private const int ContextMenuMessage = 0x007B;

        /// <summary>用户明确粘贴资源管理器文件列表时通知宿主。</summary>
        internal event Action<string[]> FilesPasted;
        /// <summary>用户粘贴位图时同步通知宿主，回调返回后输入框释放图片。</summary>
        internal event Action<Image> ImagePasted;

        /// <summary>系统剪贴板提供图片或文件时接收 Ctrl+V 和 Shift+Insert。</summary>
        /// <param name="msg">当前键盘消息。</param>
        /// <param name="keyData">按键和修饰键。</param>
        /// <returns>附件粘贴已处理时返回 true，其他按键沿用原生处理。</returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData == (Keys.Control | Keys.V) || keyData == (Keys.Shift | Keys.Insert)) && TryPasteAttachment()) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>接收原生粘贴消息，并为附件提供暗色右键粘贴入口。</summary>
        /// <param name="m">Windows 编辑控件消息。</param>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == PasteMessage && TryPasteAttachment()) return;
            // 原生右键菜单会对纯附件禁用粘贴，通过暗色菜单补足入口。
            if (m.Msg == ContextMenuMessage && HasClipboardAttachment())
            {
                Point location = m.LParam == new IntPtr(-1) ? new Point(0, Height) : PointToClient(Cursor.Position);
                var menu = new GrokPopupMenu();
                menu.AddItem("粘贴附件", WeChatIconType.Folder, () => TryPasteAttachment(), "Ctrl+V");
                menu.Show(this, location);
                return;
            }
            base.WndProc(ref m);
        }

        private bool TryPasteAttachment()
        {
            IDataObject data = ReadClipboard();
            if (data == null) return false;
            string[] paths = FilesPasted == null ? null : ReadFileDrop(data);
            if (paths != null)
            {
                FilesPasted(paths);
                return true;
            }
            if (ImagePasted == null) return false;
            using (Image image = ReadClipboardImage(data))
            {
                if (image == null) return false;
                ImagePasted(image);
                return true;
            }
        }

        private bool HasClipboardAttachment()
        {
            IDataObject data = ReadClipboard();
            if (data == null) return false;
            try
            {
                return (FilesPasted != null && data.GetDataPresent(DataFormats.FileDrop, false)) ||
                    (ImagePasted != null && (data.GetDataPresent("PNG", false) || data.GetDataPresent(DataFormats.Bitmap)));
            }
            catch (ExternalException) { return false; }
        }

        private IDataObject ReadClipboard()
        {
            if (ReadOnly) return null;
            try
            {
                return Clipboard.GetDataObject();
            }
            catch (ExternalException) { return null; }
        }

        /// <summary>优先读取保留透明度的 PNG，再尝试系统位图。调用方负责释放返回值。</summary>
        internal static Image ReadClipboardImage(IDataObject data)
        {
            if (data == null) return null;
            try
            {
                object png = data.GetData("PNG", false);
                using (var stream = png as Stream ?? (png is byte[] ? new MemoryStream((byte[])png, false) : null))
                {
                    if (stream != null)
                    {
                        if (stream.CanSeek) stream.Position = 0;
                        using (var image = Image.FromStream(stream)) return new Bitmap(image);
                    }
                }
            }
            catch (ArgumentException) { }
            catch (IOException) { }
            catch (ExternalException) { }
            try { return data.GetData(DataFormats.Bitmap) as Image; }
            catch (ExternalException) { return null; }
        }

        /// <summary>仅识别剪贴板原生文件列表格式，文字路径保留其文本含义。</summary>
        internal static string[] ReadFileDrop(IDataObject data)
        {
            try
            {
                var paths = data != null ? data.GetData(DataFormats.FileDrop, false) as string[] : null;
                return paths != null && paths.Length > 0 ? paths : null;
            }
            catch (ExternalException) { return null; }
        }
    }
}
