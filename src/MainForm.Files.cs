using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    public partial class MainForm : Form
    {
        // 附件草稿依附会话，切换聊天时不发送或丢弃草稿。
        private readonly Dictionary<string, List<string>> _attachmentDrafts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        // 只记录从剪贴板生成的图片，删除草稿时不能误删用户复制的原始文件。
        private readonly Dictionary<string, Image> _draftImagePreviews = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
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
            Disposed += (sender, e) =>
            {
                foreach (string path in new List<string>(_draftImagePreviews.Keys)) ReleaseDraftImage(path);
                if (_fileController != null) _fileController.DiscardOutgoingPastes();
            };
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
                string path = (string)_attachmentList.Items[e.Index];
                Image preview;
                if (_draftImagePreviews.TryGetValue(path, out preview))
                    e.Graphics.DrawImage(preview, row.X + 20 - preview.Width / 2, row.Y + 16 - preview.Height / 2, preview.Width, preview.Height);
                else
                    WeChatIcon.Draw(e.Graphics, WeChatIconType.Box, new Rectangle(row.X + 11, row.Y + 7, 18, 18), false, GrokTheme.AccentHot);
                GrokTheme.DrawUiTextLeft(e.Graphics, Path.GetFileName(path), font, GrokTheme.TextPrimary,
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
            ReleaseDraftImage(paths[index]);
            paths.RemoveAt(index);
            if (paths.Count == 0) _attachmentDrafts.Remove(key);
            RefreshAttachmentDraft();
            _txtInput.Visible = true;
            _txtInput.Focus();
        }

        private void StageClipboardImage(Image image)
        {
            string key = _activeChatKey;
            var paths = GetAttachmentDraft(key) ?? new List<string>();
            if (paths.Count >= 32)
            {
                GrokMessageBox.Show(this, "一次最多发送 32 个附件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 截图以独占方式创建，发送时将清理责任交给文件传输控制器。
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LanChat", "PastedImages");
            string path = Path.Combine(directory, "截图-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 12) + ".png");
            Bitmap preview = null;
            bool created = false;
            try
            {
                float scale = 24f / Math.Max(image.Width, image.Height);
                preview = new Bitmap(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)));
                using (Graphics graphics = Graphics.FromImage(preview))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(image, 0, 0, preview.Width, preview.Height);
                }
                Directory.CreateDirectory(directory);
                using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                {
                    created = true;
                    image.Save(file, ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                if (preview != null) preview.Dispose();
                if (created) FileTransferController.DeleteManagedImage(path);
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is ExternalException || ex is ArgumentException)) throw;
                GrokMessageBox.Show(this, "无法保存剪贴板图片。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _draftImagePreviews.Add(path, preview);
            paths.Add(path);
            _attachmentDrafts[key] = paths;
            RefreshAttachmentDraft();
        }

        private void ClearAttachmentDraft(string key)
        {
            var paths = GetAttachmentDraft(key);
            if (paths == null) return;
            foreach (string path in paths) ReleaseDraftImage(path);
            _attachmentDrafts.Remove(key);
        }

        // 发送批次只接管实际入队的截图，释放预览并保留原 PNG 给传输线程。
        private bool TakeDraftImage(string path)
        {
            Image preview;
            if (!_draftImagePreviews.TryGetValue(path, out preview)) return false;
            _draftImagePreviews.Remove(path);
            preview.Dispose();
            return true;
        }

        private void ReleaseDraftImage(string path)
        {
            if (TakeDraftImage(path)) FileTransferController.DeleteManagedImage(path);
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
            List<PeerInfo> peers = PickFileRecipients(IsBroadcastChat(_activeChatKey));
            if (peers.Count > 0) SendFilesToPeers(peers, filePaths);
        }

        private static bool IsBroadcastChat(string key)
        {
            return string.Equals(key, SessionManager.BROADCAST_KEY, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>将在线收件人快照归一化为非空列表，保证无在线好友时调用方可安全读取 Count。</summary>
        internal static List<PeerInfo> NormalizeRecipientList(IEnumerable<PeerInfo> peers)
        {
            return peers == null ? new List<PeerInfo>() : new List<PeerInfo>(peers);
        }

        internal static List<PeerInfo> ReadSelectedRecipients(ListBox listBox)
        {
            var selected = new List<PeerInfo>();
            if (listBox == null) return selected;
            foreach (object item in listBox.SelectedItems)
            {
                PeerInfo peer = item as PeerInfo;
                if (peer != null) selected.Add(peer);
            }
            return selected;
        }

        private List<PeerInfo> PickFileRecipients(bool allowMultiple)
        {
            List<PeerInfo> peers = NormalizeRecipientList(_netService != null ? _netService.GetActivePeers() : null);
            if (peers.Count == 0)
            {
                GrokMessageBox.Show(this, "当前无在线好友，无法发送文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return new List<PeerInfo>();
            }

            if (peers.Count == 1)
            {
                return new List<PeerInfo> { peers[0] };
            }

            using (Form picker = new Form())
            {
                picker.Text = allowMultiple ? "选择接收文件的成员（可多选）" : "选择接收文件的成员";
                picker.Size = new System.Drawing.Size(360, 260);
                picker.StartPosition = FormStartPosition.CenterParent;
                picker.FormBorderStyle = FormBorderStyle.FixedDialog;
                picker.MaximizeBox = false; picker.MinimizeBox = false;
                picker.BackColor = System.Drawing.Color.FromArgb(24, 24, 26);
                picker.ForeColor = System.Drawing.Color.White;

                var lb = new SessionListBox
                {
                    Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 32,
                    IntegralHeight = false, BorderStyle = BorderStyle.None, BackColor = GrokTheme.Level05,
                    SelectionMode = allowMultiple ? SelectionMode.MultiSimple : SelectionMode.One
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

                var btnOk = new GrokButton("确定发送", GrokButtonRole.Primary)
                {
                    Dock = DockStyle.Bottom,
                    Height = 36
                };
                btnOk.Click += (s, e) =>
                {
                    if (ReadSelectedRecipients(lb).Count == 0)
                    {
                        GrokMessageBox.Show(picker, "请至少选择一位在线成员。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    picker.DialogResult = DialogResult.OK;
                };
                picker.AcceptButton = btnOk;
                picker.Controls.Add(lb);
                picker.Controls.Add(btnOk);

                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    return ReadSelectedRecipients(lb);
                }
            }
            return new List<PeerInfo>();
        }

        private PeerInfo PickFileRecipient()
        {
            List<PeerInfo> peers = PickFileRecipients(false);
            return peers.Count > 0 ? peers[0] : null;
        }

        private bool SendFilesToPeers(IList<PeerInfo> peers, string[] filePaths)
        {
            if (peers == null || peers.Count == 0 || filePaths == null || filePaths.Length == 0) return false;
            var managedDraftPaths = new HashSet<string>(_draftImagePreviews.Keys, StringComparer.OrdinalIgnoreCase);
            int sent = 0;
            for (int i = 0; i < peers.Count; i++)
            {
                PeerInfo peer = peers[i];
                if (peer == null || peer.TcpPort <= 0) continue;
                string[] recipientPaths = filePaths;
                var copiedManaged = new List<string>();
                if (i > 0) recipientPaths = CopyManagedDraftFiles(filePaths, managedDraftPaths, copiedManaged);
                bool queued = SendFilesToPeer(peer, recipientPaths, path =>
                {
                    if (copiedManaged.Contains(path)) return true;
                    return TakeDraftImage(path);
                });
                if (!queued)
                {
                    foreach (string copy in copiedManaged) FileTransferController.DeleteManagedImage(copy);
                }
                else sent++;
            }
            if (_listSessions != null) _listSessions.Invalidate();
            return sent > 0;
        }

        private string[] CopyManagedDraftFiles(string[] filePaths, HashSet<string> managedDraftPaths, List<string> copies)
        {
            var result = new List<string>();
            foreach (string path in filePaths)
            {
                if (managedDraftPaths == null || !managedDraftPaths.Contains(path))
                {
                    result.Add(path);
                    continue;
                }
                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LanChat", "PastedImages");
                    Directory.CreateDirectory(dir);
                    string copy = Path.Combine(dir, "群发-" + Guid.NewGuid().ToString("N") + ".png");
                    File.Copy(path, copy, false);
                    copies.Add(copy);
                    result.Add(copy);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return result.ToArray();
        }

        private bool SendFilesToPeer(PeerInfo peer, string[] filePaths, Func<string, bool> takeDraftImage = null)
        {
            if (peer.TcpPort <= 0)
            {
                GrokMessageBox.Show(this, "好友当前离线，暂时无法发送文件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            if (_fileController == null || _fileController.SendFiles(peer, filePaths, takeDraftImage) == 0)
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
