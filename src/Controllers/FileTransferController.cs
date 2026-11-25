using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 文件传输业务控制器
    /// <para>【职责】接管文件流式发送、分块进度、落盘校验、打开文件及定位文件目录等与系统的交互操作。</para>
    /// <para>【线程安全】界面通知经 ISynchronizeInvoke 调度，应用截图的落盘与退出清理由控制器同步。</para>
    /// </summary>
    public class FileTransferController
    {
        private readonly NetworkService _netService;
        private readonly SessionManager _sessionManager;
        private readonly ISynchronizeInvoke _sync;
        // 收件先有进度、后有最终路径，按会话和传输号关联同一气泡。
        private readonly Dictionary<Tuple<string, string>, ChatMessage> _incoming = new Dictionary<Tuple<string, string>, ChatMessage>();
        /// <summary>
        /// 同一托管截图被群发到多个目标时的共享清理状态。
        /// <para>职责：记录尚未结算的发送数和是否已有历史引用。</para>
        /// <para>依赖：由文件发送完成回调和窗体退出清理共同更新。</para>
        /// <para>边界：只管理应用生成的截图，不触碰用户原始文件。</para>
        /// <para>生命周期：第一次入队创建，所有目标结算或退出清理时结束。</para>
        /// </summary>
        private sealed class ManagedPasteState
        {
            internal int Remaining;
            internal bool HasSavedReference;
        }

        // 仅跟踪已从草稿接管且尚未写入历史的应用截图，并按群发目标引用计数。
        private readonly Dictionary<string, ManagedPasteState> _outgoingPastes =
            new Dictionary<string, ManagedPasteState>(StringComparer.OrdinalIgnoreCase);
        // 高频网络进度先合并到内存，避免每个 64KB 分块都向 UI 投递 BeginInvoke。
        private readonly object _progressGate = new object();
        private readonly Dictionary<string, FileTransferProgress> _pendingProgress =
            new Dictionary<string, FileTransferProgress>(StringComparer.Ordinal);
        private readonly System.Threading.Timer _progressFlushTimer;
        private bool _progressFlushScheduled;

        /// <summary>向界面通知文件气泡、会话键以及是否是首次追加。</summary>
        internal event Action<ChatMessage, string, bool> TransferMessageChanged;
        /// <summary>远端文件接收完毕并落盘通知事件 (参数：fileMsg, convKey, wasActivelyViewing)</summary>
        public event Action<ChatMessage, string, bool /* wasActivelyViewing */> FileReceived;

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
            _progressFlushTimer = new System.Threading.Timer(FlushProgressTimer, null, Timeout.Infinite, Timeout.Infinite);
            Control owner = sync as Control;
            if (owner != null)
            {
                owner.Disposed += (sender, args) =>
                {
                    lock (_progressGate) _pendingProgress.Clear();
                    _progressFlushTimer.Dispose();
                };
            }

            if (_netService != null)
            {
                _netService.OnFileProgress += progress =>
                {
                    var snapshot = progress.Snapshot();
                    QueueProgress(snapshot);
                };
                _netService.OnFileReceived += msg => SafeInvoke(() => HandleFileReceived(msg));
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

        private void QueueProgress(FileTransferProgress progress)
        {
            if (progress == null) return;
            lock (_progressGate)
            {
                _pendingProgress[progress.TransferId ?? string.Empty] = progress;
                if (_progressFlushScheduled) return;
                _progressFlushScheduled = true;
                _progressFlushTimer.Change(50, Timeout.Infinite);
            }
        }

        private void FlushProgressTimer(object state)
        {
            Control control = _sync as Control;
            if (control != null && !control.IsHandleCreated)
            {
                lock (_progressGate) _progressFlushScheduled = false;
                return;
            }
            SafeInvoke(FlushPendingProgress);
        }

        private void FlushPendingProgress()
        {
            List<FileTransferProgress> pending;
            lock (_progressGate)
            {
                pending = new List<FileTransferProgress>(_pendingProgress.Values);
                _pendingProgress.Clear();
                _progressFlushScheduled = false;
            }
            foreach (FileTransferProgress progress in pending) HandleIncomingProgress(progress);
        }

        /// <summary>
        /// 批量向指定节点发送本地文件列表：记录聊天气泡并启动后台 TCP 流式传输
        /// </summary>
        /// <param name="peer">目标接收节点</param>
        /// <param name="filePaths">本地物理文件路径数组</param>
        /// <returns>进入传输的文件数。</returns>
        public int SendFiles(PeerInfo peer, string[] filePaths)
        {
            return SendFiles(peer, filePaths, null);
        }

        // 草稿回调只交出实际入队的应用截图，普通文件不移交清理责任。
        internal int SendFiles(PeerInfo peer, string[] filePaths, Func<string, bool> takeDraftImage)
        {
            if (_netService == null || peer == null || peer.TcpPort <= 0 || filePaths == null) return 0;

            string convKey = _sessionManager.ResolveKey(SessionManager.GetLanKey(peer.Id, peer.IP));
            var batch = new List<ChatMessage>();
            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath)) continue;
                string fileName = Path.GetFileName(filePath);
                long fileSize;
                try { fileSize = new FileInfo(filePath).Length; }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }

                ChatMessage fileMsg = new ChatMessage
                {
                    SenderNick = _netService.NickName,
                    SenderId = _netService.LocalId,
                    TargetId = peer.Id,
                    RemoteAddress = peer.IP,
                    PeerNick = peer.NickName,
                    Content = "[发送文件] " + fileName,
                    Timestamp = DateTime.Now,
                    IsSelf = true,
                    IsFile = true,
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = fileSize,
                    TransferProgress = new FileTransferProgress { IsSending = true, TotalBytes = fileSize }
                };

                peer.LastMessage = "[文件] " + fileName;
                peer.LastMessageTime = DateTime.Now;
                _sessionManager.AddMessage(convKey, fileMsg, persist: false);
                batch.Add(fileMsg);
            }
            // 先追加整批气泡，再交给底层有界传输队列调度。
            foreach (ChatMessage message in batch) PublishTransferMessage(message, convKey, true);
            foreach (ChatMessage message in batch)
            {
                ChatMessage fileMsg = message;
                bool managed = takeDraftImage != null && takeDraftImage(fileMsg.FilePath);
                if (managed)
                {
                    lock (_outgoingPastes)
                    {
                        ManagedPasteState state;
                        if (!_outgoingPastes.TryGetValue(fileMsg.FilePath, out state))
                        {
                            state = new ManagedPasteState();
                            _outgoingPastes.Add(fileMsg.FilePath, state);
                        }
                        state.Remaining++;
                    }
                }
                _netService.SendFileAsync(peer, fileMsg.FilePath, progress =>
                {
                    var snapshot = progress.Snapshot();
                    SafeInvoke(() =>
                    {
                        fileMsg.TransferProgress = snapshot;
                        fileMsg.FileSize = snapshot.TotalBytes;
                        PublishTransferMessage(fileMsg, convKey, false);
                    });
                }, (success, reason) =>
                {
                    bool saved = false;
                    if (managed)
                    {
                        // 即使窗体已销毁，传输结果也必须与退出清理按同一顺序结算。
                        lock (_outgoingPastes)
                        {
                            ManagedPasteState state;
                            if (!_outgoingPastes.TryGetValue(fileMsg.FilePath, out state)) return;
                            saved = success && _sessionManager.PersistExistingMessage(convKey, fileMsg, confirmStorage: true);
                            if (saved) state.HasSavedReference = true;
                            state.Remaining--;
                            if (state.Remaining <= 0)
                            {
                                _outgoingPastes.Remove(fileMsg.FilePath);
                                if (!state.HasSavedReference) DeleteManagedImage(fileMsg.FilePath);
                            }
                        }
                    }
                    if (!success) return;
                    SafeInvoke(() =>
                    {
                        fileMsg.TransferProgress = null;
                        if (managed)
                        {
                            if (!saved) fileMsg.FilePath = null;
                        }
                        else _sessionManager.PersistExistingMessage(convKey, fileMsg);
                        PublishTransferMessage(fileMsg, convKey, false);
                    });
                }, managed);
            }
            return batch.Count;
        }

        internal void DiscardOutgoingPastes()
        {
            lock (_outgoingPastes)
            {
                foreach (KeyValuePair<string, ManagedPasteState> item in _outgoingPastes)
                    if (!item.Value.HasSavedReference) DeleteManagedImage(item.Key);
                _outgoingPastes.Clear();
            }
        }

        internal static void DeleteManagedImage(string path)
        {
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void PublishTransferMessage(ChatMessage message, string key, bool added)
        {
            if (TransferMessageChanged != null) TransferMessageChanged(message, key, added);
        }

        private void HandleIncomingProgress(FileTransferProgress progress)
        {
            string key = _sessionManager.ResolveKey(SessionManager.GetLanKey(progress.TransferPeerId, progress.TransferPeerAddress));
            var id = Tuple.Create(key, progress.TransferId);
            ChatMessage message;
            bool added = !_incoming.TryGetValue(id, out message);
            if (added)
            {
                message = new ChatMessage
                {
                    SenderId = progress.TransferPeerId, RemoteAddress = progress.TransferPeerAddress,
                    SenderNick = progress.PeerNick, IsFile = true, Timestamp = DateTime.Now,
                    FileName = progress.FileName, FileSize = progress.TotalBytes, TransferProgress = progress
                };
                _sessionManager.AddMessage(key, message, persist: false);
                _incoming.Add(id, message);
            }
            message.TransferProgress = progress;
            if (progress.IsFailed) _incoming.Remove(id);
            PublishTransferMessage(message, key, added);
        }

        private void HandleFileReceived(ChatMessage fileMsg)
        {
            string convKey = _sessionManager.ResolveKey(SessionManager.GetLanKey(fileMsg.SenderId, fileMsg.RemoteAddress));
            PeerInfo matchedPeer = null;
            if (_netService != null)
            {
                var peers = _netService.GetActivePeers();
                foreach (var p in peers)
                {
                    if (p.Id == fileMsg.SenderId)
                    {
                        matchedPeer = p;
                        p.LastMessage = "[文件] " + fileMsg.FileName;
                        p.LastMessageTime = fileMsg.Timestamp;
                        break;
                    }
                }
            }

            var id = Tuple.Create(convKey, fileMsg.FileTransferId);
            ChatMessage message;
            bool added = true;
            if (_incoming.TryGetValue(id, out message))
            {
                _incoming.Remove(id);
                message.FilePath = fileMsg.FilePath;
                message.FileName = fileMsg.FileName;
                message.FileSize = fileMsg.FileSize;
                message.Content = fileMsg.Content;
                message.TransferProgress = null;
                if (_sessionManager.PersistExistingMessage(convKey, message)) added = false;
            }
            if (added) _sessionManager.AddMessage(convKey, fileMsg);
            PublishTransferMessage(added ? fileMsg : message, convKey, added);

            bool wasActivelyViewing = false;
            if (IsActivelyViewingPredicate != null)
            {
                wasActivelyViewing = IsActivelyViewingPredicate(convKey, fileMsg.SenderNick);
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

        /// <summary>将瞬时传输速率格式化为气泡可读的 KB/s 或 MB/s。</summary>
        internal static string FormatTransferRate(double megabytesPerSecond)
        {
            if (double.IsNaN(megabytesPerSecond) || double.IsInfinity(megabytesPerSecond) || megabytesPerSecond <= 0)
                return string.Empty;
            if (megabytesPerSecond < 1.0)
                return (megabytesPerSecond * 1024.0).ToString("F0", CultureInfo.InvariantCulture) + " KB/s";
            return megabytesPerSecond.ToString(megabytesPerSecond < 10.0 ? "F2" : "F1", CultureInfo.InvariantCulture) + " MB/s";
        }
    }
}
