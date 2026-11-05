using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace LanChat
{
    public partial class MainForm : Form
    {
        #region File Transfer & Drag Drop

        private void BtnSendFile_Click(object sender, EventArgs e)
        {
            PeerInfo currentPeer = _sessionPresenter != null ? _sessionPresenter.SelectedPeer : null;
            if (currentPeer == null)
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
                using (OpenFileDialog ofd = new OpenFileDialog { Title = "选择要发送给 " + currentPeer.NickName + " 的文件", Multiselect = true })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        SendFilesToPeer(currentPeer, ofd.FileNames);
                    }
                }
            }
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            PeerInfo currentPeer = _sessionPresenter != null ? _sessionPresenter.SelectedPeer : null;
            if (currentPeer != null)
                SendFilesToPeer(currentPeer, files);
            else
                PromptPeerAndSendFiles(files);
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
                picker.MaximizeBox = false; picker.MinimizeBox = false;
                picker.BackColor = System.Drawing.Color.FromArgb(24, 24, 26);
                picker.ForeColor = System.Drawing.Color.White;

                ListBox lb = new ListBox
                {
                    Dock = DockStyle.Fill,
                    Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5f),
                    BackColor = System.Drawing.Color.FromArgb(18, 18, 20),
                    ForeColor = System.Drawing.Color.White
                };
                foreach (var p in peers) lb.Items.Add(p);
                lb.SelectedIndex = 0;

                Button btnOk = new Button
                {
                    Text = "确定发送",
                    Dock = DockStyle.Bottom,
                    Height = 36,
                    DialogResult = DialogResult.OK,
                    BackColor = System.Drawing.Color.FromArgb(121, 70, 215),
                    ForeColor = System.Drawing.Color.White,
                    FlatStyle = FlatStyle.Flat
                };
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
            if (_fileController != null)
            {
                _fileController.SendFiles(peer, filePaths);
            }
            if (_listSessions != null) _listSessions.Invalidate();
        }

        private void HandleFileProgress(FileTransferProgress prog)
        {
            _progressPanel.Visible = true;
            _progressBar.Value = prog.ProgressPercentage;

            string action = prog.IsSending ? "发送给 " + prog.PeerNick : "来自 " + prog.PeerNick + " 接收";
            _lblProgressInfo.Text = string.Format("[{0}] {1} ({2}/{3}) - {4:F1} MB/s [{5}%]",
                action, prog.FileName,
                FileTransferController.FormatFileSize(prog.TransferredBytes),
                FileTransferController.FormatFileSize(prog.TotalBytes),
                prog.SpeedMBps,
                prog.ProgressPercentage);

            if (prog.IsCompleted || prog.IsFailed)
            {
                _progressPanel.Visible = false;
            }
        }

        private void OnFileTransferCompleted(string fileName, bool success, string reason)
        {
            _progressPanel.Visible = false;
            if (_chatPresenter != null)
            {
                if (success)
                    _chatPresenter.AppendChatSystem(string.Format("文件 {0} 发送成功！", fileName));
                else
                    _chatPresenter.AppendChatSystem(string.Format("文件 {0} 发送失败: {1}", fileName, reason));
            }
        }

        private void OnFileReceived(ChatMessage fileMsg, string convKey, bool wasActivelyViewing)
        {
            _progressPanel.Visible = false;

            if (!wasActivelyViewing)
            {
                FlashTaskbar(true);
                StartTrayFlash();
                UpdateTrayTooltipText();
            }

            if (_activeChatKey == convKey && _chatView != null)
            {
                _chatView.AppendMessage(fileMsg);
            }
            if (_listSessions != null) _listSessions.Invalidate();

            if (!this.Visible && _trayIcon != null)
            {
                try
                {
                    _trayIcon.ShowBalloonTip(2500, "收到新文件", string.Format("来自 {0}: {1}", fileMsg.SenderNick, fileMsg.FileName), ToolTipIcon.Info);
                }
                catch { }
            }
        }

        private void HandleFileActionClick(ChatMessage msg, string action)
        {
            if (_fileController == null) return;
            if (action == "OPEN_FILE")
            {
                _fileController.OpenFile(msg.FilePath, this);
            }
            else if (action == "OPEN_FOLDER")
            {
                _fileController.OpenContainingFolder(msg.FilePath, this);
            }
        }

        #endregion
    }
}
