using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 聊天输入框，依赖 Windows 原生编辑控件和资源管理器剪贴板。
    /// 文件粘贴交给宿主暂存，文字仍走原生编辑流程，随输入框一起释放。
    /// </summary>
    internal sealed class ChatInputBox : TextBox
    {
        private const int PasteMessage = 0x0302;
        private const int ContextMenuMessage = 0x007B;

        /// <summary>用户明确粘贴资源管理器文件列表时通知宿主。</summary>
        internal event Action<string[]> FilesPasted;

        /// <summary>在系统只提供文件列表时也接收 Ctrl+V 和 Shift+Insert。</summary>
        /// <param name="msg">当前键盘消息。</param>
        /// <param name="keyData">按键和修饰键。</param>
        /// <returns>文件粘贴已处理时返回 true，其他按键沿用原生处理。</returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData == (Keys.Control | Keys.V) || keyData == (Keys.Shift | Keys.Insert)) && TryPasteFiles()) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>接收原生粘贴消息，并在文件剪贴板上提供右键粘贴附件入口。</summary>
        /// <param name="m">Windows 编辑控件消息。</param>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == PasteMessage && TryPasteFiles()) return;
            // 原生右键菜单可能对纯 CF_HDROP 禁用粘贴，文件剪贴板使用已有的暗色菜单。
            if (m.Msg == ContextMenuMessage && ReadClipboardFiles() != null)
            {
                Point location = m.LParam == new IntPtr(-1) ? new Point(0, Height) : PointToClient(Cursor.Position);
                var menu = new GrokPopupMenu();
                menu.AddItem("粘贴附件", WeChatIconType.Folder, () => TryPasteFiles(), "Ctrl+V");
                menu.Show(this, location);
                return;
            }
            base.WndProc(ref m);
        }

        private bool TryPasteFiles()
        {
            string[] paths = ReadClipboardFiles();
            if (paths == null) return false;
            FilesPasted(paths);
            return true;
        }

        private string[] ReadClipboardFiles()
        {
            if (FilesPasted == null || ReadOnly) return null;
            try
            {
                return ReadFileDrop(Clipboard.GetDataObject());
            }
            catch (ExternalException) { return null; }
        }

        /// <summary>仅识别剪贴板原生文件列表格式，文字路径保留其文本含义。</summary>
        internal static string[] ReadFileDrop(IDataObject data)
        {
            var paths = data != null ? data.GetData(DataFormats.FileDrop, false) as string[] : null;
            return paths != null && paths.Length > 0 ? paths : null;
        }
    }
}
