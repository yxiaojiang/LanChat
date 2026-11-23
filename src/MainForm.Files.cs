using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LanChat
{
    public partial class MainForm : Form
    {
        // 附件草稿依附会话，切换聊天时不发送或丢弃草稿。
        private readonly Dictionary<string, List<string>> _attachmentDrafts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private SessionListBox _attachmentList;

        private void InitializeFileAttachments()
        {
            _attachmentList = new SessionListBox
            {
                Dock = DockStyle.Top, Visible = false, BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed, IntegralHeight = false, ItemHeight = 34,
                AccessibleName = "待发送附件", BackColor = GrokTheme.Level05
            };
            _attachmentList.DrawItem += DrawAttachment;
            _attachmentList.MouseClick += (sender, e) =>
            {
                int index = _attachmentList.IndexFromPoint(e.Location);
                if (e.Button != MouseButtons.Left || index < 0) return;
                if (AttachmentRemoveBounds(_attachmentList.GetItemRectangle(index)).Contains(e.Location)) RemoveAttachment(index);
            };
            _attachmentList.KeyDown += (sender, e) =>
            {
                if (e.KeyCode != Keys.Delete) return;
                e.SuppressKeyPress = true;
                RemoveAttachment(_attachmentList.SelectedIndex);
            };
            _bottomSendPanel.Controls.Add(_attachmentList);
        }

        private static Rectangle AttachmentRemoveBounds(Rectangle row)
        {
            return new Rectangle(row.Right - 46, row.Top + 1, 32, 32);
        }

        private void DrawAttachment(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var row = new Rectangle(e.Bounds.X + 1, e.Bounds.Y + 1, e.Bounds.Width - 14, 32);
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var bg = new SolidBrush(selected ? GrokTheme.Level3 : GrokTheme.Level2))
            using (var border = new Pen(GrokTheme.Border, 1f))
            using (var font = GrokTheme.UiFont(9.5f))
            {
                DrawRoundedRectangle(e.Graphics, bg, row, 16);
                DrawRoundedRectangle(e.Graphics, border, row, 16);
                WeChatIcon.Draw(e.Graphics, WeChatIconType.Box, new Rectangle(row.X + 11, row.Y + 7, 18, 18), false, GrokTheme.AccentHot);
                GrokTheme.DrawUiTextLeft(e.Graphics, Path.GetFileName((string)_attachmentList.Items[e.Index]), font, GrokTheme.TextPrimary,
                    row.X + 38, row.Y, Math.Max(1, row.Width - 74), row.Height);
                var remove = AttachmentRemoveBounds(e.Bounds);
                WeChatIcon.Draw(e.Graphics, WeChatIconType.Dismiss, remove, false, GrokTheme.TextSecondary);
            }
        }

        private List<string> GetAttachmentDraft(string key)
        {
            List<string> paths;
            return _attachmentDrafts.TryGetValue(key, out paths) ? paths : null;
        }

        private void StageClipboardFiles(string[] files)
        {
            string key = _activeChatKey;
            var paths = GetAttachmentDraft(key) ?? new List<string>();
            foreach (string file in files)
            {
                if (paths.Count == 32) break;
                if (File.Exists(file) && !paths.Contains(file)) paths.Add(file);
            }
            if (paths.Count == 0)
            {
                GrokMessageBox.Show(this, "剪贴板里没有可发送的文件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _attachmentDrafts[key] = paths;
            RefreshAttachmentDraft();
        }

        private void RemoveAttachment(int index)
        {
            string key = _activeChatKey;
            var paths = GetAttachmentDraft(key);
            if (paths == null || index < 0 || index >= paths.Count) return;
            paths.RemoveAt(index);
            if (paths.Count == 0) _attachmentDrafts.Remove(key);
            RefreshAttachmentDraft();
            _txtInput.Visible = true;
            _txtInput.Focus();
        }

        private void RefreshAttachmentDraft()
        {
            if (_attachmentList == null) return;
            var paths = GetAttachmentDraft(_activeChatKey);
            _attachmentList.BeginUpdate();
            _attachmentList.Items.Clear();
            if (paths != null) _attachmentList.Items.AddRange(paths.ToArray());
            _attachmentList.EndUpdate();
            // 最多展开三行，更多附件使用会话列表已有的暗色自绘滑块。
            int height = Math.Min(3, _attachmentList.Items.Count) * _attachmentList.ItemHeight;
            _attachmentList.Visible = height > 0;
            _attachmentList.Height = height;
            _bottomSendPanel.Height = 90 + height;
            TxtInput_TextChanged(_txtInput, EventArgs.Empty);
        }

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
            PeerInfo peer = PickFileRecipient();
            if (peer != null) SendFilesToPeer(peer, filePaths);
        }

        private PeerInfo PickFileRecipient()
        {
            var peers = _netService != null ? _netService.GetActivePeers() : new List<PeerInfo>();
            if (peers.Count == 0)
            {
                GrokMessageBox.Show(this, "当前无在线好友，无法发送文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            if (peers.Count == 1)
            {
                return peers[0];
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

                var lb = new SessionListBox
                {
                    Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 32,
                    IntegralHeight = false, BorderStyle = BorderStyle.None, BackColor = GrokTheme.Level05
                };
                lb.DrawItem += (sender, e) =>
                {
                    if (e.Index < 0) return;
                    var row = Rectangle.Inflate(e.Bounds, -6, -2);
                    bool selected = (e.State & DrawItemState.Selected) != 0;
                    using (var bg = new SolidBrush(selected ? GrokTheme.Level3 : GrokTheme.Level2))
                    using (var font = GrokTheme.UiFont(9.5f))
                    {
                        DrawRoundedRectangle(e.Graphics, bg, row, 8);
                        GrokTheme.DrawUiTextLeft(e.Graphics, lb.Items[e.Index].ToString(), font, GrokTheme.TextPrimary,
                            row.X + 10, row.Y, row.Width - 28, row.Height);
                    }
                };
                foreach (var p in peers) lb.Items.Add(p);
                lb.SelectedIndex = 0;

                var btnOk = new GrokButton("确定发送", GrokButtonRole.Primary, DialogResult.OK)
                {
                    Dock = DockStyle.Bottom,
                    Height = 36
                };
                picker.AcceptButton = btnOk;
                picker.Controls.Add(lb);
                picker.Controls.Add(btnOk);

                if (picker.ShowDialog(this) == DialogResult.OK && lb.SelectedItem is PeerInfo)
                {
                    return (PeerInfo)lb.SelectedItem;
                }
            }
            return null;
        }

        private bool SendFilesToPeer(PeerInfo peer, string[] filePaths)
        {
            if (peer.TcpPort <= 0)
            {
                GrokMessageBox.Show(this, "好友当前离线，暂时无法发送文件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            if (_fileController == null || _fileController.SendFiles(peer, filePaths) == 0)
            {
                GrokMessageBox.Show(this, "选中的文件已不可用。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (_listSessions != null) _listSessions.Invalidate();
            return true;
        }

        private void OnFileTransferMessageChanged(ChatMessage message, string convKey, bool added)
        {
            if (_activeChatKey == convKey && _chatView != null)
            {
                if (added) _chatView.AppendMessage(message);
                else _chatView.RefreshFileMessage(message);
            }
            if (!added) return;
            if (_sessionPresenter != null && _sessionPresenter.SearchFilter.Length == 0 && _sessionPresenter.FindSessionIndex(convKey) < 0)
                RefreshPeerListUI();
            else if (_listSessions != null) _listSessions.Invalidate();
        }

        private void OnFileReceived(ChatMessage fileMsg, string convKey, bool wasActivelyViewing)
        {
            if (!wasActivelyViewing)
            {
                FlashTaskbar(true);
                StartTrayFlash();
                UpdateTrayTooltipText();
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
