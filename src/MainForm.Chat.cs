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
            AppendChatSystem(string.Format("发现节点上线: {0} ({1} - {2})", peer.NickName, peer.HostName, peer.IP));
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

            // 列表末尾固定附带“新建连接”（对应截图中底部的“新建 Bot”）
            _allSessionItems.Add(NEW_CONNECTION_ITEM);

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
                if (item is string && (string)item == NEW_CONNECTION_ITEM)
                {
                    if (string.IsNullOrEmpty(filter) || "新建连接".Contains(filter) || "bot".Contains(filter))
                    {
                        _listSessions.Items.Add(item);
                    }
                    continue;
                }

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
            if (currentSelected != null && !(currentSelected is string && (string)currentSelected == NEW_CONNECTION_ITEM))
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
            menu.BackColor = Color.FromArgb(30, 30, 32);
            menu.ForeColor = Color.FromArgb(235, 235, 240);

            menu.Items.Add("➕ 手动添加好友 IP (直连握手)...", null, (s, ev) => BtnAddIp_Click(s, ev));
            menu.Items.Add("🔄 扫描并广播局域网节点", null, (s, ev) => RefreshPeersWithNotice());
            menu.Items.Add("📂 打开接收文件夹", null, (s, ev) => OpenReceivedFolder());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("⚙️ 系统设置与更多...", null, (s, ev) => BtnSettings_Click(s, ev));

            menu.Show(_btnAddSession, new Point(0, _btnAddSession.Height + 4));
        }

        private void ListSessions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_listSessions.SelectedItem == null) return;

            // 点击列表末尾“新建连接”项：触发手动连接 IP 弹窗并恢复原会话选中态
            if (_listSessions.SelectedItem is string && (string)_listSessions.SelectedItem == NEW_CONNECTION_ITEM)
            {
                _listSessions.SelectedIndex = 0;
                BtnAddIp_Click(this, EventArgs.Empty);
                return;
            }

            if (_listSessions.SelectedItem is string && (string)_listSessions.SelectedItem == BROADCAST_TARGET)
            {
                _selectedPeer = null;
                _activeChatKey = "BROADCAST";
                _lblChatTitle.Text = "局域网全员群聊";
                _inputPlaceholder = "给 局域网全员群聊 发消息";
                _chatView.ShowGroupSenderName = true;
            }
            else if (_listSessions.SelectedItem is PeerInfo)
            {
                _selectedPeer = (PeerInfo)_listSessions.SelectedItem;
                _activeChatKey = _selectedPeer.Id;
                _selectedPeer.UnreadCount = 0;
                _lblChatTitle.Text = _selectedPeer.NickName ?? "在线节点";
                _inputPlaceholder = string.Format("给 {0} 发消息", _selectedPeer.NickName ?? "好友");
                _chatView.ShowGroupSenderName = false; // 1对1私聊与截图完全一致，隐藏双方头像与昵称
            }

            if (_lblSendTip != null)
            {
                _lblSendTip.Text = _inputPlaceholder;
                _lblSendTip.Visible = string.IsNullOrEmpty(_txtInput.Text);
            }

            SwitchActiveChatView();
            _chatHeader.Invalidate();
            _inputPanel.Invalidate();
            _listSessions.Invalidate();
        }

        private void SwitchActiveChatView()
        {
            _chatView.SetMessages(_sessionManager.GetMessages(_activeChatKey));
        }

        private void ListSessions_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _listSessions.Items.Count) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle r = e.Bounds;
            object item = _listSessions.Items[e.Index];
            bool isNewConnItem = (item is string && (string)item == NEW_CONNECTION_ITEM);
            bool isSelected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected) && !isNewConnItem;

            // 1. 填充纯黑底色 (#0E0E0E)
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(14, 14, 14)))
            {
                g.FillRectangle(bgBrush, r);
            }

            bool isLarge = (r.Height > 50);
            int cardMarginX = isLarge ? 10 : 8;
            int cardMarginY = isLarge ? 3 : 2;
            int cardRadius = isLarge ? 11 : 9;

            // 2. 选中态绘制 #222226 圆角卡片背景（与截图选中项 Alist迅雷 100% 一致）
            if (isSelected)
            {
                Rectangle cardRect = new Rectangle(r.Left + cardMarginX, r.Top + cardMarginY, r.Width - cardMarginX * 2, r.Height - cardMarginY * 2);
                using (SolidBrush cardBrush = new SolidBrush(Color.FromArgb(34, 34, 38)))
                {
                    DrawRoundedRectangle(g, cardBrush, cardRect, cardRadius);
                }
            }

            int avSize = isLarge ? 32 : 26;
            int avX = r.Left + (isLarge ? 18 : 14);
            int avY = r.Top + (r.Height - avSize) / 2;
            Rectangle avatarRect = new Rectangle(avX, avY, avSize, avSize);
            int textX = avX + avSize + (isLarge ? 10 : 8);

            if (isNewConnItem)
            {
                // 底部“新建连接”项：有机橙黄云朵吉祥物 + 单行粗体白字（复刻截图底部“新建 Bot”）
                WeChatIcon.DrawMascotAvatar(g, avatarRect, Color.FromArgb(246, 148, 18), true, false, Color.FromArgb(14, 14, 14));

                using (Font titleFont = new Font("Microsoft YaHei", isLarge ? 9.5f : 9.0f, FontStyle.Bold))
                using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(240, 240, 245)))
                {
                    float ty = r.Top + (r.Height - titleFont.GetHeight(g)) / 2f;
                    g.DrawString("新建连接", titleFont, titleBrush, new PointF(textX, ty));
                }
                return;
            }

            if (item is string)
            {
                // 顶部第一项（局域网全员群聊）：亮紫色正圆吉祥物 (#905AFB，复刻截图 Alist迅雷)
                WeChatIcon.DrawMascotAvatar(g, avatarRect, Color.FromArgb(144, 90, 251), false, false, Color.FromArgb(14, 14, 14));

                using (Font titleFont = new Font("Microsoft YaHei", isLarge ? 9.5f : 9.0f, FontStyle.Bold))
                using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(242, 242, 245)))
                {
                    int onlineCount = _netService != null ? _netService.GetActivePeers().Count : 0;
                    string title = onlineCount > 0 ? string.Format("局域网全员群聊 ({0})", onlineCount) : "局域网全员群聊";
                    g.DrawString(title, titleFont, titleBrush, new PointF(textX, r.Top + (isLarge ? 8 : 5)));
                }

                string snippet = _sessionManager.GetLastMessageSnippet(SessionManager.BROADCAST_KEY, 18);
                if (string.IsNullOrEmpty(snippet)) snippet = "已按 Rules 刷新本机局域网广播...";

                using (Font subFont = new Font("Microsoft YaHei", isLarge ? 8.5f : 8.0f))
                using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(135, 135, 140)))
                {
                    if (snippet.Length > 16) snippet = snippet.Substring(0, 15) + "...";
                    g.DrawString(snippet, subFont, subBrush, new PointF(textX, r.Top + (isLarge ? 28 : 24)));
                }
            }
            else if (item is PeerInfo)
            {
                PeerInfo peer = (PeerInfo)item;

                // 在线节点：复刻截图中的橙色云朵吉祥物 (#F69412) 与珊瑚红带角标吉祥物 (#FC2C3E)
                bool useCoralStyle = (e.Index % 3 == 0);
                Color bodyColor = useCoralStyle ? Color.FromArgb(252, 44, 62) : Color.FromArgb(246, 148, 18);
                string badge = useCoralStyle ? "节点" : null;
                WeChatIcon.DrawMascotAvatar(g, avatarRect, bodyColor, !useCoralStyle, false, Color.FromArgb(14, 14, 14), badge);

                using (Font titleFont = new Font("Microsoft YaHei", isLarge ? 9.5f : 9.0f, FontStyle.Bold))
                using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(242, 242, 245)))
                {
                    string name = peer.NickName ?? "未知节点";
                    if (name.Length > 13) name = name.Substring(0, 12) + "...";
                    g.DrawString(name, titleFont, titleBrush, new PointF(textX, r.Top + (isLarge ? 8 : 5)));
                }

                string snippet = peer.LastMessage;
                if (string.IsNullOrEmpty(snippet))
                {
                    snippet = string.Format("{0} ({1})", peer.HostName, peer.IP);
                }
                using (Font subFont = new Font("Microsoft YaHei", isLarge ? 8.5f : 8.0f))
                using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(135, 135, 140)))
                {
                    if (snippet.Length > 16) snippet = snippet.Substring(0, 15) + "...";
                    g.DrawString(snippet, subFont, subBrush, new PointF(textX, r.Top + (isLarge ? 28 : 24)));
                }
            }
        }

        private Color GetAvatarColor(string name)
        {
            if (string.IsNullOrEmpty(name)) return Color.FromArgb(144, 90, 251);
            int hash = Math.Abs(name.GetHashCode());
            Color[] colors = new Color[]
            {
                Color.FromArgb(144, 90, 251),  // Grok Purple
                Color.FromArgb(246, 148, 18),  // Grok Orange
                Color.FromArgb(252, 44, 62),   // Grok Coral Red
                Color.FromArgb(77, 155, 255)   // Grok Blue
            };
            return colors[hash % colors.Length];
        }

        #endregion

        #region Messaging & Input

        private void TxtInput_TextChanged(object sender, EventArgs e)
        {
            if (_lblSendTip != null)
            {
                _lblSendTip.Visible = string.IsNullOrEmpty(_txtInput.Text) && !_txtInput.Focused;
            }
            _inputPanel.Invalidate();
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

            // 若窗体隐藏在系统托盘中，显示气泡提醒
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
                BackColor = Color.FromArgb(28, 28, 30)
            };

            Point screenPoint = _btnToolSendFile.PointToScreen(new Point(0, -popup.Height - 6));
            popup.Location = screenPoint;

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(6),
                BackColor = Color.FromArgb(28, 28, 30)
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
                btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(52, 52, 56);
                btn.MouseLeave += (s, e) => btn.BackColor = Color.Transparent;
                btn.Click += (s, e) => {
                    _txtInput.Visible = true;
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
                inputForm.Text = "连接节点 (手动输入 IP 直连握手)";
                inputForm.Size = new Size(390, 165);
                inputForm.StartPosition = FormStartPosition.CenterParent;
                inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                inputForm.MaximizeBox = false;
                inputForm.MinimizeBox = false;
                inputForm.BackColor = Color.FromArgb(24, 24, 26);
                inputForm.ForeColor = Color.White;

                Label lblPrompt = new Label
                {
                    Text = "请输入对方计算机的 IPv4 地址 (例如 192.168.1.100):",
                    Location = new Point(16, 16),
                    AutoSize = true,
                    Font = new Font("Microsoft YaHei", 9f)
                };

                TextBox txtIp = new TextBox
                {
                    Location = new Point(18, 42),
                    Width = 338,
                    Font = new Font("Microsoft YaHei", 10f),
                    BackColor = Color.FromArgb(16, 16, 18),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                Button btnOk = new Button
                {
                    Text = "连接握手",
                    Location = new Point(180, 78),
                    Size = new Size(85, 30),
                    DialogResult = DialogResult.OK,
                    BackColor = Color.FromArgb(121, 70, 215),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnOk.FlatAppearance.BorderSize = 0;

                Button btnCancel = new Button
                {
                    Text = "取消",
                    Location = new Point(275, 78),
                    Size = new Size(80, 30),
                    DialogResult = DialogResult.Cancel,
                    BackColor = Color.FromArgb(46, 46, 50),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnCancel.FlatAppearance.BorderSize = 0;

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
