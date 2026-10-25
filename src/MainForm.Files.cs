using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace LanChat
{
    public partial class MainForm : Form
    {
        #region File Transfer & Drag Drop

        private void BtnSendFile_Click(object sender, EventArgs e)
        {
            if (_selectedPeer == null)
            {
                var peers = _netService != null ? _netService.GetActivePeers() : new List<PeerInfo>();
                if (peers.Count == 0)
                {
                    GrokMessageBox.Show(this, "局域网内暂无其他在线好友，请等待好友加入或点击【连接节点】！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (OpenFileDialog ofd = new OpenFileDialog { Title = "选择要发送的文件", Multiselect = true })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        PromptPeerAndSendFiles(ofd.FileNames);
                    }
                }
            }
            else
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Title = "选择要发送给 " + _selectedPeer.NickName + " 的文件", Multiselect = true })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        SendFilesToPeer(_selectedPeer, ofd.FileNames);
                    }
                }
            }
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            if (_selectedPeer != null)
            {
                SendFilesToPeer(_selectedPeer, files);
            }
            else
            {
                PromptPeerAndSendFiles(files);
            }
        }

        private void PromptPeerAndSendFiles(string[] filePaths)
        {
            var peers = _netService != null ? _netService.GetActivePeers() : new List<PeerInfo>();
            if (peers.Count == 0)
            {
                GrokMessageBox.Show(this, "当前无在线好友，无法发送文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (peers.Count == 1)
            {
                SendFilesToPeer(peers[0], filePaths);
                return;
            }

            using (Form picker = new Form())
            {
                picker.Text = "选择接收文件的成员";
                picker.Size = new System.Drawing.Size(360, 260);
                picker.StartPosition = FormStartPosition.CenterParent;
                picker.FormBorderStyle = FormBorderStyle.FixedDialog;
                picker.MaximizeBox = false;
                picker.MinimizeBox = false;
                picker.BackColor = System.Drawing.Color.FromArgb(24, 24, 26);
                picker.ForeColor = System.Drawing.Color.White;

                ListBox lb = new ListBox
                {
                    Dock = DockStyle.Fill,
                    Font = new System.Drawing.Font("Microsoft YaHei", 9.5f),
                    BackColor = System.Drawing.Color.FromArgb(18, 18, 20),
                    ForeColor = System.Drawing.Color.White
                };
                foreach (var p in peers) lb.Items.Add(p);
                lb.SelectedIndex = 0;

                Button btnOk = new Button { Text = "确定发送", Dock = DockStyle.Bottom, Height = 36, DialogResult = DialogResult.OK, BackColor = System.Drawing.Color.FromArgb(121, 70, 215), ForeColor = System.Drawing.Color.White, FlatStyle = FlatStyle.Flat };
                btnOk.FlatAppearance.BorderSize = 0;
                picker.Controls.Add(lb);
                picker.Controls.Add(btnOk);

                if (picker.ShowDialog(this) == DialogResult.OK && lb.SelectedItem is PeerInfo)
                {
                    SendFilesToPeer((PeerInfo)lb.SelectedItem, filePaths);
                }
            }
        }

        private void SendFilesToPeer(PeerInfo peer, string[] filePaths)
        {
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
                AddMessageToHistory(peer.Id, fileMsg);

                _progressPanel.Visible = true;
                _progressBar.Value = 0;
                _lblProgressInfo.Text = string.Format("正在发送 {0} 至 {1}...", fileName, peer.NickName);

                _netService.SendFileAsync(peer, filePath,
                    prog => SafeInvoke(() => HandleFileProgress(prog)),
                    (success, reason) => SafeInvoke(() => {
                        _progressPanel.Visible = false;
                        if (success)
                        {
                            AppendChatSystem(string.Format("文件 {0} 发送成功！", fileName));
                        }
                        else
                        {
                            AppendChatSystem(string.Format("文件 {0} 发送失败: {1}", fileName, reason));
                        }
                    })
                );
            }
            _listSessions.Invalidate();
        }

        private void HandleFileProgress(FileTransferProgress prog)
        {
            _progressPanel.Visible = true;
            _progressBar.Value = prog.ProgressPercentage;

            string action = prog.IsSending ? "发送给 " + prog.PeerNick : "来自 " + prog.PeerNick + " 接收";
            _lblProgressInfo.Text = string.Format("[{0}] {1} ({2}/{3}) - {4:F1} MB/s [{5}%]",
                action, prog.FileName,
                FormatFileSize(prog.TransferredBytes),
                FormatFileSize(prog.TotalBytes),
                prog.SpeedMBps,
                prog.ProgressPercentage);

            if (prog.IsCompleted || prog.IsFailed)
            {
                _progressPanel.Visible = false;
            }
        }

        private void HandleFileReceived(string senderNick, string fileName, string localPath, long size)
        {
            _progressPanel.Visible = false;

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

            // Find peer
            string convKey = _activeChatKey;
            if (_netService != null)
            {
                var peers = _netService.GetActivePeers();
                foreach (var p in peers)
                {
                    if (p.NickName == senderNick)
                    {
                        convKey = p.Id;
                        p.LastMessage = "[文件] " + fileName;
                        p.LastMessageTime = DateTime.Now;
                        break;
                    }
                }
            }

            AddMessageToHistory(convKey, fileMsg);
            _listSessions.Invalidate();

            // 若窗体隐藏在系统托盘中，显示文件接收气泡提醒
            if (!this.Visible && _trayIcon != null)
            {
                try
                {
                    _trayIcon.ShowBalloonTip(2500, "收到新文件", string.Format("来自 {0}: {1}", senderNick, fileName), ToolTipIcon.Info);
                }
                catch { }
            }
        }

        private void HandleFileActionClick(ChatMessage msg, string action)
        {
            try
            {
                if (action == "OPEN_FILE")
                {
                    if (File.Exists(msg.FilePath))
                    {
                        Process.Start(msg.FilePath);
                    }
                    else
                    {
                        GrokMessageBox.Show(this, "文件不存在或已被移动: " + msg.FilePath, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else if (action == "OPEN_FOLDER")
                {
                    string dir = Path.GetDirectoryName(msg.FilePath);
                    if (Directory.Exists(dir))
                    {
                        Process.Start("explorer.exe", "/select,\"" + msg.FilePath + "\"");
                    }
                }
            }
            catch (Exception ex)
            {
                GrokMessageBox.Show(this, "打开失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
        }

        #endregion
    }
}
