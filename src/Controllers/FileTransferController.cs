using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 文件传输业务控制器：接管文件流式发送、分块进度、落盘校验与外部交互
    /// </summary>
    public class FileTransferController
    {
        private readonly NetworkService _netService;
        private readonly SessionManager _sessionManager;
        private readonly ISynchronizeInvoke _sync;

        public event Action<FileTransferProgress> ProgressChanged;
        public event Action<string, bool, string> TransferCompleted; // fileName, success, reason
        public event Action<ChatMessage, string, bool /* wasActivelyViewing */> FileReceived;

        public Func<string, string, bool> IsActivelyViewingPredicate { get; set; }

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

        private void SafeInvoke(Action action)
        {
            if (action == null) return;
            if (_sync != null && _sync.InvokeRequired)
            {
                try { _sync.BeginInvoke(action, null); } catch { }
            }
            else
            {
                action();
            }
        }

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

        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
        }
    }
}
