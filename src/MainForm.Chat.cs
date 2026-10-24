using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    public partial class MainForm : Form
    {
        #region Peer & Sessions Management

        private void PeerJoined(PeerInfo peer)
        {
            RefreshPeerListUI();
            AppendChatSystem(string.Format("发现好友上线: {0} ({1} - {2})", peer.NickName, peer.HostName, peer.IP));
        }

        private void PeerUpdated(PeerInfo peer)
        {
            RefreshPeerListUI();
        }

        private void PeerLeft(string peerId)
        {
            RefreshPeerListUI();
        }

        private void RefreshPeerListUI()
        {
            var peers = _netService != null ? _netService.GetActivePeers() : new List<PeerInfo>();

            _allSessionItems.Clear();
            _allSessionItems.Add(BROADCAST_TARGET);

            foreach (var p in peers)
            {
                _allSessionItems.Add(p);
            }

            ApplyFilterAndRenderList();
        }

        private void ApplyFilterAndRenderList()
        {
            string filter = _txtSearch.Text.Trim().ToLowerInvariant();

            object currentSelected = _listSessions.SelectedItem;
            _listSessions.BeginUpdate();
            _listSessions.Items.Clear();

            foreach (var item in _allSessionItems)
            {
                if (string.IsNullOrEmpty(filter))
                {
                    _listSessions.Items.Add(item);
                }
                else
                {
                    if (item is string)
                    {
                        if (((string)item).ToLowerInvariant().Contains(filter))
                            _listSessions.Items.Add(item);
                    }
                    else if (item is PeerInfo)
                    {
                        PeerInfo p = (PeerInfo)item;
                        string nick = (p.NickName ?? "").ToLowerInvariant();
                        string ip = (p.IP != null ? p.IP.ToString() : "").ToLowerInvariant();
                        string host = (p.HostName ?? "").ToLowerInvariant();
                        if (nick.Contains(filter) || ip.Contains(filter) || host.Contains(filter))
                        {
                            _listSessions.Items.Add(p);
                        }
                    }
                }
            }

            // Restore selection or select 0
            if (currentSelected != null)
            {
                int index = -1;
                for (int i = 0; i < _listSessions.Items.Count; i++)
                {
                    if (_listSessions.Items[i].ToString() == currentSelected.ToString())
                    {
                        index = i;
                        break;
                    }
                }
                if (index >= 0) _listSessions.SelectedIndex = index;
                else if (_listSessions.Items.Count > 0) _listSessions.SelectedIndex = 0;
            }
            else if (_listSessions.Items.Count > 0)
            {
                _listSessions.SelectedIndex = 0;
            }

            _listSessions.EndUpdate();
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            ApplyFilterAndRenderList();
        }

        private void BtnAddSession_Click(object sender, EventArgs e)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new WeChatDarkMenuRenderer();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(44, 44, 46);
            menu.ForeColor = Color.FromArgb(235, 235, 235);

            menu.Items.Add("➕ 手动添加好友 IP...", null, (s, ev) => BtnAddIp_Click(s, ev));
            menu.Items.Add("🔄 扫描局域网好友", null, (s, ev) => RefreshPeersWithNotice());
            menu.Items.Add("📂 打开接收文件夹", null, (s, ev) => OpenReceivedFolder());

            menu.Show(_btnAddSession, new Point(0, _btnAddSession.Height + 2));
        }

        private void ListSessions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_listSessions.SelectedItem == null) return;

            if (_listSessions.SelectedItem is string && (string)_listSessions.SelectedItem == BROADCAST_TARGET)
            {
                _selectedPeer = null;
                _activeChatKey = "BROADCAST";
                int count = _netService != null ? _netService.GetActivePeers().Count : 0;
                _lblChatTitle.Text = string.Format("📢 局域网全员群聊 ({0}人在线)", count);
                _lblChatSubtitle.Text = "局域网全员广播消息通道";
            }
            else if (_listSessions.SelectedItem is PeerInfo)
            {
                _selectedPeer = (PeerInfo)_listSessions.SelectedItem;
                _activeChatKey = _selectedPeer.Id;
                _selectedPeer.UnreadCount = 0;
                _lblChatTitle.Text = string.Format("👤 {0}", _selectedPeer.NickName);
                _lblChatSubtitle.Text = string.Format("{0} - {1}:{2}", _selectedPeer.HostName, _selectedPeer.IP, _selectedPeer.TcpPort);
            }

            SwitchActiveChatView();
            _listSessions.Invalidate();
        }

        private void SwitchActiveChatView()
        {
            _chatView.SetMessages(_sessionManager.GetMessages(_activeChatKey));
        }

        private void ListSessions_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _listSessions.Items.Count) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            // WeChat Green selection highlight as in screenshot: #07c160
            Color bgColor = isSelected ? Color.FromArgb(7, 193, 96) : Color.FromArgb(47, 47, 48);

            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
            }

            object item = _listSessions.Items[e.Index];
            Rectangle r = e.Bounds;

            if (item is string)
            {
                // Group Chat item avatar
                Rectangle avatarRect = new Rectangle(r.Left + 12, r.Top + 12, 40, 40);
                using (SolidBrush avBrush = new SolidBrush(isSelected ? Color.FromArgb(4, 150, 74) : Color.FromArgb(32, 128, 70)))
                {
                    DrawRoundedRectangle(e.Graphics, avBrush, avatarRect, 4);
                }

                using (Font iconFont = new Font("Microsoft YaHei", 12f))
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("📢", iconFont, textBrush, avatarRect, sf);
                }

                // Name
                using (Font titleFont = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold))
                using (SolidBrush titleBrush = new SolidBrush(Color.White))
                {
                    int onlineCount = _netService != null ? _netService.GetActivePeers().Count : 0;
                    string title = string.Format("局域网全员群聊 ({0})", onlineCount);
                    e.Graphics.DrawString(title, titleFont, titleBrush, new PointF(r.Left + 60, r.Top + 14));
                }

                // Last message snippet
                string snippet = _sessionManager.GetLastMessageSnippet(SessionManager.BROADCAST_KEY, 20);
                if (string.IsNullOrEmpty(snippet)) snippet = "点击发送全网广播消息";

                using (Font subFont = new Font("Microsoft YaHei", 8.5f))
                using (SolidBrush subBrush = new SolidBrush(isSelected ? Color.FromArgb(220, 248, 230) : Color.FromArgb(145, 145, 145)))
                {
                    if (snippet.Length > 20) snippet = snippet.Substring(0, 19) + "...";
                    e.Graphics.DrawString(snippet, subFont, subBrush, new PointF(r.Left + 60, r.Top + 36));
                }
            }
            else if (item is PeerInfo)
            {
                PeerInfo peer = (PeerInfo)item;

                // Round Avatar (radius 4px)
                Rectangle avatarRect = new Rectangle(r.Left + 12, r.Top + 12, 40, 40);
                Color avColor = GetAvatarColor(peer.NickName);
                using (SolidBrush avBrush = new SolidBrush(avColor))
                {
                    DrawRoundedRectangle(e.Graphics, avBrush, avatarRect, 4);
                }

                string initial = string.IsNullOrEmpty(peer.NickName) ? "友" : peer.NickName.Substring(0, 1).ToUpperInvariant();
                using (Font f = new Font("Microsoft YaHei", 11f, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(initial, f, textBrush, avatarRect, sf);
                }

                // Online green dot
                Rectangle dotRect = new Rectangle(r.Left + 42, r.Top + 42, 10, 10);
                using (SolidBrush dotBg = new SolidBrush(bgColor))
                using (SolidBrush dotFg = new SolidBrush(Color.FromArgb(7, 193, 96)))
                {
                    e.Graphics.FillEllipse(dotBg, new Rectangle(r.Left + 41, r.Top + 41, 12, 12));
                    e.Graphics.FillEllipse(dotFg, dotRect);
                }

                // Peer Nickname
                using (Font titleFont = new Font("Microsoft YaHei", 9.5f, isSelected ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush titleBrush = new SolidBrush(Color.White))
                {
                    e.Graphics.DrawString(peer.NickName ?? "未知用户", titleFont, titleBrush, new PointF(r.Left + 60, r.Top + 13));
                }

                // Time or IP on right
                using (Font timeFont = new Font("Microsoft YaHei", 8f))
                using (SolidBrush timeBrush = new SolidBrush(isSelected ? Color.FromArgb(220, 245, 220) : Color.FromArgb(130, 130, 130)))
                {
                    string timeStr = peer.LastMessageTime != DateTime.MinValue ? peer.LastMessageTime.ToString("MM/dd") : "";
                    if (!string.IsNullOrEmpty(timeStr))
                    {
                        e.Graphics.DrawString(timeStr, timeFont, timeBrush, new PointF(r.Right - 46, r.Top + 14));
                    }
                }

                // Peer Snippet
                string snippet = peer.LastMessage;
                if (string.IsNullOrEmpty(snippet))
                {
                    snippet = string.Format("{0} ({1})", peer.HostName, peer.IP);
                }
                using (Font subFont = new Font("Microsoft YaHei", 8.5f))
                using (SolidBrush subBrush = new SolidBrush(isSelected ? Color.FromArgb(220, 248, 230) : Color.FromArgb(145, 145, 145)))
                {
                    if (snippet.Length > 22) snippet = snippet.Substring(0, 21) + "...";
                    e.Graphics.DrawString(snippet, subFont, subBrush, new PointF(r.Left + 60, r.Top + 36));
                }
            }

            // Divider line
            using (Pen divPen = new Pen(Color.FromArgb(40, 40, 42)))
            {
                e.Graphics.DrawLine(divPen, r.Left + 60, r.Bottom - 1, r.Right, r.Bottom - 1);
            }
        }

        private Color GetAvatarColor(string name)
        {
            if (string.IsNullOrEmpty(name)) return Color.FromArgb(59, 130, 246);
            int hash = Math.Abs(name.GetHashCode());
            Color[] colors = new Color[]
            {
                Color.FromArgb(59, 130, 246),  // Blue
                Color.FromArgb(139, 92, 246),  // Purple
                Color.FromArgb(236, 72, 153),  // Pink
                Color.FromArgb(16, 185, 129),  // Emerald
                Color.FromArgb(245, 158, 11),  // Amber
                Color.FromArgb(14, 165, 233),  // Sky
                Color.FromArgb(168, 85, 247)   // Violet
            };
            return colors[hash % colors.Length];
        }

        #endregion

        #region Messaging & Input

        private void TxtInput_TextChanged(object sender, EventArgs e)
        {
            bool hasText = !string.IsNullOrEmpty(_txtInput.Text.Trim());
            _btnSend.BackColor = hasText ? Color.FromArgb(7, 193, 96) : Color.FromArgb(42, 42, 44);
            _btnSend.ForeColor = hasText ? Color.White : Color.FromArgb(120, 120, 120);
            _btnSend.Invalidate();
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift && !e.Control)
            {
                e.SuppressKeyPress = true;
                SendMessage();
            }
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            SendMessage();
        }

        private void SendMessage()
        {
            string text = _txtInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _txtInput.Clear();

            string myNick = _netService != null ? _netService.NickName : Environment.UserName;

            if (_selectedPeer == null)
            {
                // Broadcast
                var peers = _netService != null ? _netService.GetActivePeers() : new List<PeerInfo>();
                ChatMessage msg = new ChatMessage
                {
                    SenderId = _netService != null ? _netService.LocalId : "",
                    SenderNick = myNick,
                    TargetId = null,
                    Content = text,
                    Timestamp = DateTime.Now,
                    IsSelf = true,
                    IsBroadcast = true
                };

                AddMessageToHistory("BROADCAST", msg);

                new System.Threading.Thread(() => {
                    foreach (var peer in peers)
                    {
                        _netService.SendTextMessage(peer, text, true);
                    }
                }) { IsBackground = true }.Start();
            }
            else
            {
                // Send to selected peer
                PeerInfo target = _selectedPeer;
                ChatMessage msg = new ChatMessage
                {
                    SenderId = _netService != null ? _netService.LocalId : "",
                    SenderNick = myNick,
                    TargetId = target.Id,
                    Content = text,
                    Timestamp = DateTime.Now,
                    IsSelf = true,
                    IsBroadcast = false
                };

                target.LastMessage = text;
                target.LastMessageTime = DateTime.Now;
                AddMessageToHistory(target.Id, msg);

                new System.Threading.Thread(() => {
                    bool ok = _netService.SendTextMessage(target, text, false);
                    if (!ok)
                    {
                        SafeInvoke(() => AppendChatSystem(string.Format("向 {0} 发送私聊消息失败，对方可能已离线", target.NickName)));
                    }
                }) { IsBackground = true }.Start();
            }

            _listSessions.Invalidate();
        }

        private void HandleReceivedMessage(ChatMessage msg)
        {
            string convKey = msg.IsBroadcast ? "BROADCAST" : msg.SenderId;

            // Update peer last message info
            if (!msg.IsBroadcast && _netService != null)
            {
                var peers = _netService.GetActivePeers();
                foreach (var p in peers)
                {
                    if (p.Id == msg.SenderId)
                    {
                        p.LastMessage = msg.Content;
                        p.LastMessageTime = msg.Timestamp;
                        break;
                    }
                }
            }

            AddMessageToHistory(convKey, msg);
            _listSessions.Invalidate();

            // 若窗体隐藏在系统托盘中，显示微信风格气泡提醒
            if (!this.Visible && _trayIcon != null)
            {
                try
                {
                    string preview = msg.Content ?? "";
                    if (preview.Length > 36) preview = preview.Substring(0, 36) + "...";
                    string title = msg.IsBroadcast ? string.Format("[📢 广播] {0}", msg.SenderNick) : string.Format("来自 {0} 的新消息", msg.SenderNick);
                    _trayIcon.ShowBalloonTip(2000, title, preview, ToolTipIcon.Info);
                }
                catch { }
            }
        }

        private void AddMessageToHistory(string convKey, ChatMessage msg)
        {
            _sessionManager.AddMessage(convKey, msg);

            if (_activeChatKey == convKey)
            {
                _chatView.AppendMessage(msg);
            }
        }

        private void AppendChatSystem(string text)
        {
            ChatMessage msg = new ChatMessage
            {
                SenderNick = "系统",
                Content = text,
                Timestamp = DateTime.Now,
                IsSystem = true
            };

            // Add to current active chat
            AddMessageToHistory(_activeChatKey, msg);
        }

        private void ClearCurrentChat()
        {
            _sessionManager.ClearHistory(_activeChatKey);
            SwitchActiveChatView();
        }

        #endregion

        #region Emoji Popup

        private void ShowEmojiPopup()
        {
            Form popup = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                Size = new Size(290, 200),
                BackColor = Color.FromArgb(44, 44, 46)
            };

            Point screenPoint = _btnToolEmoji.PointToScreen(new Point(0, -popup.Height - 4));
            popup.Location = screenPoint;

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(6),
                BackColor = Color.FromArgb(44, 44, 46)
            };

            string[] emojis = new string[]
            {
                "😊", "😃", "😄", "😁", "😆", "😅", "😂", "🤣", "😇", "😉",
                "😋", "😍", "🥰", "😘", "😜", "🤔", "🤫", "🤐", "😴", "😷",
                "🥳", "🤩", "🥺", "😭", "😱", "😨", "😰", "😡", "🤬", "🤡",
                "👍", "👎", "👏", "🙏", "🤝", "🎉", "🔥", "❤️", "💯", "✨",
                "💪", "✌️", "🖐️", "✋", "👊", "☕", "💻", "📱", "🍺", "🎁"
            };

            foreach (string em in emojis)
            {
                Button btn = new Button
                {
                    Text = em,
                    Size = new Size(32, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI Emoji", 11f),
                    Margin = new Padding(2),
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(60, 60, 64);
                btn.MouseLeave += (s, e) => btn.BackColor = Color.Transparent;
                btn.Click += (s, e) => {
                    _txtInput.Paste(em);
                    popup.Close();
                    _txtInput.Focus();
                };
                flow.Controls.Add(btn);
            }

            popup.Controls.Add(flow);
            popup.Deactivate += (s, e) => popup.Close();
            popup.Show(this);
        }

        #endregion

        #region Actions

        private void BtnAddIp_Click(object sender, EventArgs e)
        {
            using (Form inputForm = new Form())
            {
                if (this.Icon != null) inputForm.Icon = this.Icon;
                inputForm.Text = "手动添加好友 IP (直连握手)";
                inputForm.Size = new Size(390, 165);
                inputForm.StartPosition = FormStartPosition.CenterParent;
                inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                inputForm.MaximizeBox = false;
                inputForm.MinimizeBox = false;
                inputForm.BackColor = Color.FromArgb(44, 44, 46);
                inputForm.ForeColor = Color.White;

                Label lblPrompt = new Label
                {
                    Text = "请输入对方计算机的 IPv4 地址 (例如 192.168.255.147):",
                    Location = new Point(16, 16),
                    AutoSize = true,
                    Font = new Font("Microsoft YaHei", 9f)
                };

                TextBox txtIp = new TextBox
                {
                    Location = new Point(18, 42),
                    Width = 338,
                    Font = new Font("Microsoft YaHei", 10f),
                    BackColor = Color.FromArgb(32, 32, 34),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                Button btnOk = new Button
                {
                    Text = "连接握手",
                    Location = new Point(180, 78),
                    Size = new Size(85, 30),
                    DialogResult = DialogResult.OK,
                    BackColor = Color.FromArgb(7, 193, 96),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };

                Button btnCancel = new Button
                {
                    Text = "取消",
                    Location = new Point(275, 78),
                    Size = new Size(80, 30),
                    DialogResult = DialogResult.Cancel,
                    BackColor = Color.FromArgb(60, 60, 62),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };

                inputForm.Controls.AddRange(new Control[] { lblPrompt, txtIp, btnOk, btnCancel });
                inputForm.AcceptButton = btnOk;
                inputForm.CancelButton = btnCancel;

                if (inputForm.ShowDialog(this) == DialogResult.OK)
                {
                    string ipStr = txtIp.Text.Trim();
                    if (!string.IsNullOrEmpty(ipStr) && _netService != null)
                    {
                        AppendChatSystem("正在主动向 " + ipStr + " 发起 TCP 直连握手...");
                        _netService.ProbeIp(ipStr, (success, msg) => {
                            SafeInvoke(() => AppendChatSystem(msg));
                        });
                    }
                }
            }
        }

        #endregion
    }
}
