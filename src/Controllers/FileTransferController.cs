using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 文件传输业务控制器
    /// <para>【职责】接管文件流式发送、分块进度、落盘校验、打开文件及定位文件目录等与系统的交互操作。</para>
    /// <para>【线程安全】底层 TCP Socket 传输事件均通过注入的 ISynchronizeInvoke 统一安全切回 UI 主线程。</para>
    /// </summary>
    public class FileTransferController
    {
        private readonly NetworkService _netService;
        private readonly SessionManager _sessionManager;
        private readonly ISynchronizeInvoke _sync;

        /// <summary>文件传输百分比与速率进度刷新事件</summary>
        public event Action<FileTransferProgress> ProgressChanged;
        /// <summary>传输完成或失败通知事件 (参数：fileName, success, reason)</summary>
        public event Action<string, bool, string> TransferCompleted;
        /// <summary>远端文件接收完毕并落盘通知事件 (参数：fileMsg, convKey, wasActivelyViewing)</summary>
        public event Action<ChatMessage, string, bool /* wasActivelyViewing */> FileReceived;
        /// <summary>本地文件发送成功创建气泡通知事件 (参数：fileMsg, targetPeerId)</summary>
        public event Action<ChatMessage, string> FileSent;

        /// <summary>获取或设置判定当前接收的文件是否处于当前激活查看视口的委托</summary>
        public Func<string, string, bool> IsActivelyViewingPredicate { get; set; }

        /// <summary>
        /// 初始化文件传输控制器并挂接网络传输事件
        /// </summary>
        /// <param name="netService">网络通信服务</param>
        /// <param name="sessionManager">会话持久化管理器</param>
        /// <param name="sync">跨线程 UI 同步器</param>
        public FileTransferController(NetworkService netService, SessionManager sessionManager, ISynchronizeInvoke sync)
        {
            _netService = netService;
            _sessionManager = sessionManager;
            _sync = sync;

            if (_netService != null)
            {
                _netService.OnFileProgress += (prog) => SafeInvoke(() => HandleFileProgress(prog));
                _netService.OnFileReceived += (nick, fn, path, sz) => SafeInvoke(() => HandleFileReceived(nick, fn, path, sz));
            }
        }

        /// <summary>跨线程安全调用：防 Disposed 异常与死锁</summary>
        private void SafeInvoke(Action action)
        {
            if (action == null) return;
            Control c = _sync as Control;
            if (c != null && (c.IsDisposed || !c.IsHandleCreated)) return;
            if (_sync != null && _sync.InvokeRequired)
            {
                try { _sync.BeginInvoke(action, null); } catch { }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// 批量向指定节点发送本地文件列表：记录聊天气泡并启动后台 TCP 流式传输
        /// </summary>
        /// <param name="peer">目标接收节点</param>
        /// <param name="filePaths">本地物理文件路径数组</param>
        public void SendFiles(PeerInfo peer, string[] filePaths)
        {
            if (peer == null || filePaths == null || filePaths.Length == 0) return;

            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath)) continue;

                string fileName = Path.GetFileName(filePath);
                long fileSize = new FileInfo(filePath).Length;

                ChatMessage fileMsg = new ChatMessage
                {
                    SenderNick = _netService != null ? _netService.NickName : Environment.UserName,
                    Content = "[发送文件] " + fileName,
                    Timestamp = DateTime.Now,
                    IsSelf = true,
                    IsFile = true,
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = fileSize
                };

                peer.LastMessage = "[文件] " + fileName;
                peer.LastMessageTime = DateTime.Now;
                if (_sessionManager != null)
                {
                    _sessionManager.AddMessage(peer.Id, fileMsg);
                }

                if (FileSent != null)
                {
                    FileSent(fileMsg, peer.Id);
                }

                if (_netService != null)
                {
                    _netService.SendFileAsync(peer, filePath,
                        prog => SafeInvoke(() => HandleFileProgress(prog)),
                        (success, reason) => SafeInvoke(() => {
                            if (TransferCompleted != null)
                            {
                                TransferCompleted(fileName, success, reason);
                            }
                        })
                    );
                }
            }
        }

        private void HandleFileProgress(FileTransferProgress prog)
        {
            if (ProgressChanged != null)
            {
                ProgressChanged(prog);
            }
        }

        private void HandleFileReceived(string senderNick, string fileName, string localPath, long size)
        {
            ChatMessage fileMsg = new ChatMessage
            {
                SenderNick = senderNick,
                Content = "[接收文件] " + fileName,
                Timestamp = DateTime.Now,
                IsSelf = false,
                IsFile = true,
                FileName = fileName,
                FilePath = localPath,
                FileSize = size
            };

            string convKey = "BROADCAST";
            PeerInfo matchedPeer = null;
            if (_netService != null)
            {
                var peers = _netService.GetActivePeers();
                foreach (var p in peers)
                {
                    if (p.NickName == senderNick)
                    {
                        matchedPeer = p;
                        convKey = p.Id;
                        p.LastMessage = "[文件] " + fileName;
                        p.LastMessageTime = DateTime.Now;
                        break;
                    }
                }
            }

            if (_sessionManager != null)
            {
                _sessionManager.AddMessage(convKey, fileMsg);
            }

            bool wasActivelyViewing = false;
            if (IsActivelyViewingPredicate != null)
            {
                wasActivelyViewing = IsActivelyViewingPredicate(convKey, senderNick);
            }

            if (!wasActivelyViewing && matchedPeer != null)
            {
                matchedPeer.UnreadCount++;
            }

            if (FileReceived != null)
            {
                FileReceived(fileMsg, convKey, wasActivelyViewing);
            }
        }

        /// <summary>
        /// 调用系统默认关联程序直接打开目标文件
        /// </summary>
        /// <param name="filePath">文件完整路径</param>
        /// <param name="owner">父级窗体（用于弹出错误提示）</param>
        /// <returns>打开成功返回 true，否则返回 false</returns>
        public bool OpenFile(string filePath, IWin32Window owner)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Process.Start(filePath);
                    return true;
                }
                else
                {
                    GrokMessageBox.Show(owner, "文件不存在或已被移动: " + filePath, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                GrokMessageBox.Show(owner, "打开文件失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// 调用 Windows 资源管理器打开所在文件夹并直接选中该文件 (explorer.exe /select,"...")
        /// </summary>
        /// <param name="filePath">文件完整路径</param>
        /// <param name="owner">父级窗体</param>
        /// <returns>定位成功返回 true，否则返回 false</returns>
        public bool OpenContainingFolder(string filePath, IWin32Window owner)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (Directory.Exists(dir))
                {
                    Process.Start("explorer.exe", "/select,\"" + filePath + "\"");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                GrokMessageBox.Show(owner, "定位文件失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// 格式化文件字节数为易读字符串 (B / KB / MB / GB)
        /// </summary>
        /// <param name="bytes">字节总数</param>
        /// <returns>格式化后的带单位文本</returns>
        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
        }
    }
}
