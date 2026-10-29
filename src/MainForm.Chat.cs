using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
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
            GrokPopupMenu menu = new GrokPopupMenu();
            menu.AddItem("新建节点 (自定义外观与Bot)", WeChatIconType.Add, () => BtnCreateNode_Click(this, EventArgs.Empty))
                .AddItem("连接节点 (局域网与 IP 直连)", WeChatIconType.Bolt, () => BtnConnectNode_Click(this, EventArgs.Empty))
                .AddItem("扫描并广播局域网节点", WeChatIconType.Refresh, () => RefreshPeersWithNotice())
                .AddItem("打开接收文件夹", WeChatIconType.Folder, () => OpenReceivedFolder())
                .AddSeparator()
                .AddItem("系统设置与偏好", WeChatIconType.Settings, () => ShowSettingsDialog(0));

            menu.Show(_btnAddSession, new Point(0, _btnAddSession.Height + 6));
        }

        private void ListSessions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressSessionSelect) return;
            if (_listSessions.SelectedItem == null) return;

            // 点击「新建连接」：抑制嵌套 SelectedIndexChanged，恢复原选中后再开对话框
            if (_listSessions.SelectedItem is string && (string)_listSessions.SelectedItem == NEW_CONNECTION_ITEM)
            {
                int restore = FindSessionIndexForKey(_activeChatKey);
                _suppressSessionSelect = true;
                try
                {
                    if (restore >= 0)
                        _listSessions.SelectedIndex = restore;
                    else if (_listSessions.Items.Count > 0)
                        _listSessions.SelectedIndex = 0;
                }
                finally
                {
                    _suppressSessionSelect = false;
                }
                BtnCreateNode_Click(this, EventArgs.Empty);
                return;
            }

            string previousKey = _activeChatKey;
            string newKey = previousKey;
            PeerInfo newPeer = null;
            string titleText = null;
            string placeholder = null;
            bool showGroupSender = false;

            if (_listSessions.SelectedItem is string)
            {
                string sItem = (string)_listSessions.SelectedItem;
                newPeer = null;
                newKey = "BROADCAST";
                titleText = GrokTheme.SanitizeUiText(sItem);
                placeholder = GrokTheme.SanitizeUiText(string.Format("给 {0} 发消息", sItem));
                showGroupSender = (sItem == BROADCAST_TARGET);
            }
            else if (_listSessions.SelectedItem is PeerInfo)
            {
                newPeer = (PeerInfo)_listSessions.SelectedItem;
                newKey = newPeer.Id;
                newPeer.UnreadCount = 0;
                titleText = GrokTheme.SanitizeUiText(newPeer.NickName ?? "在线节点");
                placeholder = GrokTheme.SanitizeUiText(string.Format("给 {0} 发消息", newPeer.NickName ?? "好友"));
                showGroupSender = false;
            }
            else
            {
                return;
            }

            // Same session: selection paint only — skip SetMessages / layout flash
            if (string.Equals(newKey, previousKey, StringComparison.Ordinal))
            {
                _selectedPeer = newPeer;
                return;
            }

            _selectedPeer = newPeer;
            _activeChatKey = newKey;
            _lblChatTitle.Text = titleText;
            _inputPlaceholder = placeholder;
            _chatView.ShowGroupSenderName = showGroupSender; // field-only when changed; SetMessages does layout

            if (_chatPanel != null) _chatPanel.SuspendLayout();
            try
            {
                SwitchActiveChatView();
            }
            finally
            {
                if (_chatPanel != null) _chatPanel.ResumeLayout(true);
            }

            // Invalidate header / input placeholder regions only — not the whole form
            if (_chatHeader != null) _chatHeader.Invalidate();
            if (_inputPanel != null) _inputPanel.Invalidate();
        }

        private int FindSessionIndexForKey(string key)
        {
            if (string.IsNullOrEmpty(key) || _listSessions == null) return -1;
            for (int i = 0; i < _listSessions.Items.Count; i++)
            {
                object item = _listSessions.Items[i];
                if (item is string)
                {
                    if (key == "BROADCAST" && (string)item != NEW_CONNECTION_ITEM)
                        return i;
                }
                else if (item is PeerInfo)
                {
                    if (((PeerInfo)item).Id == key)
                        return i;
                }
            }
            return -1;
        }

        private void ListSessions_MouseMove(object sender, MouseEventArgs e)
        {
            if (_listSessions == null) return;
            int index = _listSessions.IndexFromPoint(e.Location);
            if (index < 0 || index >= _listSessions.Items.Count)
                index = -1;
            if (index == _hoverSessionIndex) return;

            int old = _hoverSessionIndex;
            _hoverSessionIndex = index;
            if (old >= 0 && old < _listSessions.Items.Count)
            {
                try { _listSessions.Invalidate(_listSessions.GetItemRectangle(old)); }
                catch { }
            }
            if (_hoverSessionIndex >= 0)
            {
                try { _listSessions.Invalidate(_listSessions.GetItemRectangle(_hoverSessionIndex)); }
                catch { }
            }
        }

        private void ListSessions_MouseLeave(object sender, EventArgs e)
        {
            if (_hoverSessionIndex < 0) return;
            int old = _hoverSessionIndex;
            _hoverSessionIndex = -1;
            if (_listSessions != null && old < _listSessions.Items.Count)
            {
                try { _listSessions.Invalidate(_listSessions.GetItemRectangle(old)); }
                catch { }
            }
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
            GrokTheme.PrepareSoftText(g);

            Rectangle r = e.Bounds;
            object item = _listSessions.Items[e.Index];
            bool isNewConnItem = (item is string && (string)item == NEW_CONNECTION_ITEM);
            bool isSelected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected) && !isNewConnItem;
            bool isHover = (!isSelected && !isNewConnItem && e.Index == _hoverSessionIndex);

            // 1. 填充纯黑底色 (#0E0E0E)
            using (SolidBrush bgBrush = new SolidBrush(GrokTheme.Level0))
            {
                g.FillRectangle(bgBrush, r);
            }

            // denser 72px rows: more breathing room on selection card
            int cardMarginX = 17;
            int cardMarginY = 5;
            int cardRadius = 14;

            if (isSelected || isHover)
            {
                Rectangle cardRect = new Rectangle(
                    r.Left + cardMarginX,
                    r.Top + cardMarginY,
                    r.Width - cardMarginX * 2,
                    r.Height - cardMarginY * 2);
                Color cardColor = isSelected ? GrokTheme.SessionSelected : GrokTheme.SessionHover;
                using (SolidBrush cardBrush = new SolidBrush(cardColor))
                {
                    DrawRoundedRectangle(g, cardBrush, cardRect, cardRadius);
                }
            }

            // 48x48 avatar, vertically centered in 72px row; wider icon→text gap
            int avSize = 48;
            int avX = r.Left + 30;
            int avY = r.Top + (r.Height - avSize) / 2;
            Rectangle avatarRect = new Rectangle(avX, avY, avSize, avSize);
            int textX = r.Left + 100;
            int textW = Math.Max(40, r.Right - textX - 18);
            int titleY = r.Top + 14;
            int subY = r.Top + 38;

            if (isNewConnItem)
            {
                WeChatIcon.DrawMascotAvatar(g, avatarRect, Color.FromArgb(246, 148, 18), true, false, GrokTheme.Level0);

                using (Font titleFont = GrokTheme.Font(11.0f, FontStyle.Bold))
                {
                    GrokTheme.DrawUiTextLeft(g, "新建连接", titleFont, GrokTheme.TextTitle,
                        textX, r.Top, textW, r.Height);
                }
                return;
            }

            if (item is string)
            {
                string sItem = (string)item;
                WeChatIcon.DrawMascotAvatar(g, avatarRect, Color.FromArgb(144, 90, 251), false, false, GrokTheme.Level0);

                using (Font titleFont = GrokTheme.Font(11.0f, FontStyle.Bold))
                {
                    string title = sItem;
                    if (sItem == BROADCAST_TARGET)
                    {
                        int onlineCount = _netService != null ? _netService.GetActivePeers().Count : 0;
                        title = onlineCount > 0 ? string.Format("局域网全员群聊 ({0})", onlineCount) : "局域网全员群聊";
                    }
                    GrokTheme.DrawUiTextLeft(g, title, titleFont, GrokTheme.TextTitle,
                        textX, titleY, textW, 22);
                }

                string snippet = _sessionManager != null ? _sessionManager.GetLastMessageSnippet(SessionManager.BROADCAST_KEY, 24) : null;
                if (string.IsNullOrEmpty(snippet)) snippet = "已按 Rules 刷新本机记忆 (刷新...";

                using (Font subFont = GrokTheme.Font(9.5f, FontStyle.Regular))
                {
                    if (snippet.Length > 20) snippet = snippet.Substring(0, 19) + "...";
                    GrokTheme.DrawUiTextLeft(g, snippet, subFont, GrokTheme.TextSubtitle,
                        textX, subY, textW, 20);
                }
            }
            else if (item is PeerInfo)
            {
                PeerInfo peer = (PeerInfo)item;

                bool useCoralStyle = (e.Index % 3 == 0);
                Color bodyColor = useCoralStyle ? Color.FromArgb(252, 44, 62) : Color.FromArgb(246, 148, 18);
                string badge = useCoralStyle ? "节点" : null;
                WeChatIcon.DrawMascotAvatar(g, avatarRect, bodyColor, !useCoralStyle, false, GrokTheme.Level0, badge);

                using (Font titleFont = GrokTheme.Font(11.0f, FontStyle.Bold))
                {
                    string name = peer.NickName ?? "未知节点";
                    if (name.Length > 13) name = name.Substring(0, 12) + "...";
                    GrokTheme.DrawUiTextLeft(g, name, titleFont, GrokTheme.TextTitle,
                        textX, titleY, textW, 22);
                }

                string snippet = peer.LastMessage;
                if (string.IsNullOrEmpty(snippet))
                {
                    snippet = string.Format("{0} ({1})", peer.HostName, peer.IP);
                }
                using (Font subFont = GrokTheme.Font(9.5f, FontStyle.Regular))
                {
                    if (snippet.Length > 16) snippet = snippet.Substring(0, 15) + "...";
                    GrokTheme.DrawUiTextLeft(g, snippet, subFont, GrokTheme.TextSubtitle,
                        textX, subY, textW, 20);
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
            // 单行胶囊：重绘占位符；多行换行逻辑已随 Multiline=false 移除
            if (_inputPanel != null) _inputPanel.Invalidate();
            if (_bottomSendPanel != null) _bottomSendPanel.Invalidate();
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            // 单行胶囊：Enter 发送；Ctrl/Shift+Enter 不适用换行
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                if (!e.Control && !e.Shift)
                {
                    SendMessage();
                }
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
                Size = new Size(300, 210),
                BackColor = Color.FromArgb(28, 28, 30)
            };

            popup.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Color.FromArgb(50, 50, 56), 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, p, new Rectangle(0, 0, popup.Width - 1, popup.Height - 1), 14);
                }
            };

            popup.HandleCreated += (s, e) =>
            {
                try
                {
                    IntPtr rgn = CreateRoundRectRgn(0, 0, popup.Width + 1, popup.Height + 1, 14, 14);
                    if (rgn != IntPtr.Zero) popup.Region = Region.FromHrgn(rgn);
                }
                catch { }
            };

            Control anchor = _btnToolEmoji ?? _btnToolSendFile ?? (Control)this;
            Point screenPoint = anchor.PointToScreen(new Point(0, -popup.Height - 6));
            popup.Location = screenPoint;

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(8),
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

        #region Actions & Navigation

        public void SelectPeerChat(PeerInfo peer)
        {
            if (peer == null) return;
            for (int i = 0; i < _listSessions.Items.Count; i++)
            {
                if (_listSessions.Items[i] is PeerInfo)
                {
                    PeerInfo p = (PeerInfo)_listSessions.Items[i];
                    if (p.Id == peer.Id || p.IP == peer.IP)
                    {
                        _listSessions.SelectedIndex = i;
                        return;
                    }
                }
            }

            _selectedPeer = peer;
            _activeChatKey = peer.Id;
            _lblChatTitle.Text = GrokTheme.SanitizeUiText(peer.NickName ?? "在线节点");
            _inputPlaceholder = GrokTheme.SanitizeUiText(string.Format("给 {0} 发消息", peer.NickName ?? "好友"));
            _chatView.ShowGroupSenderName = false;
            if (_chatPanel != null) _chatPanel.SuspendLayout();
            try
            {
                SwitchActiveChatView();
            }
            finally
            {
                if (_chatPanel != null) _chatPanel.ResumeLayout(true);
            }
            if (_chatHeader != null) _chatHeader.Invalidate();
            if (_inputPanel != null) _inputPanel.Invalidate();
        }

        private void BtnConnectNode_Click(object sender, EventArgs e)
        {
            using (GrokConnectNodeForm form = new GrokConnectNodeForm(
                _netService,
                (ip) => {
                    AppendChatSystem("正在主动向 " + ip + " 发起 TCP 直连握手...");
                    _netService.ProbeIp(ip, (success, msg) => {
                        SafeInvoke(() => AppendChatSystem(msg));
                    });
                },
                (peer) => {
                    SelectPeerChat(peer);
                }))
            {
                form.ShowDialog(this);
            }
        }

        private void BtnCreateNode_Click(object sender, EventArgs e)
        {
            using (GrokCreateNodeForm form = new GrokCreateNodeForm())
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    string nameOrIp = form.NodeName;
                    IPAddress parsedIp;
                    if (IPAddress.TryParse(nameOrIp, out parsedIp) && _netService != null)
                    {
                        AppendChatSystem("正在主动向 " + nameOrIp + " 发起直连握手...");
                        _netService.ProbeIp(nameOrIp, (success, msg) => {
                            SafeInvoke(() => AppendChatSystem(msg));
                        });
                    }
                    else
                    {
                        AppendChatSystem("已创建新节点: " + nameOrIp);
                    }
                }
            }
        }

        private void BtnAddIp_Click(object sender, EventArgs e)
        {
            BtnConnectNode_Click(sender, e);
        }

        public void ListSessions_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int index = _listSessions.IndexFromPoint(e.Location);
                if (index >= 0)
                {
                    _listSessions.SelectedIndex = index;
                    object item = _listSessions.Items[index];

                    if (item is PeerInfo)
                    {
                        PeerInfo peer = (PeerInfo)item;
                        GrokPopupMenu menu = new GrokPopupMenu();
                        menu.AddItem("发起会话", WeChatIconType.Chat, () => SelectPeerChat(peer))
                            .AddItem("发送文件...", WeChatIconType.Folder, () => BtnSendFile_Click(this, EventArgs.Empty))
                            .AddItem("复制节点 IP (" + peer.IP + ")", WeChatIconType.Contacts, () => {
                                try { Clipboard.SetText(peer.IP != null ? peer.IP.ToString() : ""); } catch { }
                            })
                            .AddSeparator()
                            .AddItem("清空聊天记录", WeChatIconType.Clear, () => ClearCurrentChat(), null, true);

                        menu.Show(_listSessions, e.Location);
                    }
                    else if (item is string)
                    {
                        string sItem = (string)item;
                        if (sItem == NEW_CONNECTION_ITEM)
                        {
                            GrokPopupMenu menu = new GrokPopupMenu();
                            menu.AddItem("新建节点 (自定义外观)...", WeChatIconType.Add, () => BtnCreateNode_Click(this, EventArgs.Empty))
                                .AddItem("连接节点 (局域网与IP)...", WeChatIconType.Bolt, () => BtnConnectNode_Click(this, EventArgs.Empty));
                            menu.Show(_listSessions, e.Location);
                        }
                        else
                        {
                            GrokPopupMenu menu = new GrokPopupMenu();
                            menu.AddItem("全员群聊", WeChatIconType.Chat, () => {
                                _listSessions.SelectedIndex = 0;
                            })
                            .AddItem("发送文件给所有人...", WeChatIconType.Folder, () => BtnSendFile_Click(this, EventArgs.Empty))
                            .AddSeparator()
                            .AddItem("清空群聊记录", WeChatIconType.Clear, () => ClearCurrentChat(), null, true);
                            menu.Show(_listSessions, e.Location);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
