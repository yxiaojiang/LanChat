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
            if (_listSessions.SelectedItem == null) return;

            // 点击列表末尾“新建 Bot”项：弹出对标 CreateNewBot.png 的新建节点窗口
            if (_listSessions.SelectedItem is string && (string)_listSessions.SelectedItem == NEW_CONNECTION_ITEM)
            {
                _listSessions.SelectedIndex = 0;
                BtnCreateNode_Click(this, EventArgs.Empty);
                return;
            }

            if (_listSessions.SelectedItem is string)
            {
                string sItem = (string)_listSessions.SelectedItem;
                if (sItem == NEW_CONNECTION_ITEM)
                {
                    _listSessions.SelectedIndex = 0;
                    BtnCreateNode_Click(this, EventArgs.Empty);
                    return;
                }

                _selectedPeer = null;
                _activeChatKey = "BROADCAST";
                _lblChatTitle.Text = sItem;
                _inputPlaceholder = string.Format("给 {0} 发消息", sItem);
                _chatView.ShowGroupSenderName = (sItem == BROADCAST_TARGET);
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

            int cardMarginX = 19;
            int cardMarginY = 3;
            int cardRadius = 16;

            // 2. 选中态绘制 #222222 圆角卡片背景（与截图选中项 Alist迅雷 100% 一致）
            if (isSelected)
            {
                Rectangle cardRect = new Rectangle(r.Left + cardMarginX, r.Top + cardMarginY, r.Width - cardMarginX * 2, 80);
                using (SolidBrush cardBrush = new SolidBrush(Color.FromArgb(34, 34, 34))) // #222222
                {
                    DrawRoundedRectangle(g, cardBrush, cardRect, cardRadius);
                }
            }

            // 48x48 饱满吉祥物头像
            int avSize = 48;
            int avX = r.Left + 32;
            int avY = r.Top + 16;
            Rectangle avatarRect = new Rectangle(avX, avY, avSize, avSize);
            int textX = r.Left + 96;

            if (isNewConnItem)
            {
                // 底部“新建连接”项：有机橙黄云朵吉祥物 + 单行粗体白字（复刻截图底部“新建 Bot”）
                WeChatIcon.DrawMascotAvatar(g, avatarRect, Color.FromArgb(246, 148, 18), true, false, Color.FromArgb(14, 14, 14));

                using (Font titleFont = new Font("Microsoft YaHei", 11.0f, FontStyle.Bold))
                using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(240, 240, 245)))
                {
                    float ty = r.Top + (r.Height - titleFont.GetHeight(g)) / 2f;
                    g.DrawString("新建连接", titleFont, titleBrush, new PointF(textX, ty));
                }
                return;
            }

            if (item is string)
            {
                string sItem = (string)item;
                // 顶部第一项（局域网全员群聊 / 专属会话）：亮紫色正圆吉祥物 (#905AFB，复刻截图 Alist迅雷)
                WeChatIcon.DrawMascotAvatar(g, avatarRect, Color.FromArgb(144, 90, 251), false, false, Color.FromArgb(14, 14, 14));

                using (Font titleFont = new Font("Microsoft YaHei", 11.0f, FontStyle.Bold))
                using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(245, 245, 248)))
                {
                    string title = sItem;
                    if (sItem == BROADCAST_TARGET)
                    {
                        int onlineCount = _netService != null ? _netService.GetActivePeers().Count : 0;
                        title = onlineCount > 0 ? string.Format("局域网全员群聊 ({0})", onlineCount) : "局域网全员群聊";
                    }
                    g.DrawString(title, titleFont, titleBrush, new PointF(textX, r.Top + 18));
                }

                string snippet = _sessionManager != null ? _sessionManager.GetLastMessageSnippet(SessionManager.BROADCAST_KEY, 24) : null;
                if (string.IsNullOrEmpty(snippet)) snippet = "已按 Rules 刷新本机记忆 (刷新...";

                using (Font subFont = new Font("Microsoft YaHei", 9.5f))
                using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(136, 136, 142)))
                {
                    if (snippet.Length > 20) snippet = snippet.Substring(0, 19) + "...";
                    g.DrawString(snippet, subFont, subBrush, new PointF(textX, r.Top + 46));
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

                using (Font titleFont = new Font("Microsoft YaHei", 11.0f, FontStyle.Bold))
                using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(245, 245, 248)))
                {
                    string name = peer.NickName ?? "未知节点";
                    if (name.Length > 13) name = name.Substring(0, 12) + "...";
                    g.DrawString(name, titleFont, titleBrush, new PointF(textX, r.Top + 18));
                }

                string snippet = peer.LastMessage;
                if (string.IsNullOrEmpty(snippet))
                {
                    snippet = string.Format("{0} ({1})", peer.HostName, peer.IP);
                }
                using (Font subFont = new Font("Microsoft YaHei", 9.5f))
                using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(136, 136, 142)))
                {
                    if (snippet.Length > 16) snippet = snippet.Substring(0, 15) + "...";
                    g.DrawString(snippet, subFont, subBrush, new PointF(textX, r.Top + 46));
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
            if (_bottomSendPanel != null) _bottomSendPanel.Invalidate();
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (e.Control || e.Shift)
                {
                    e.SuppressKeyPress = true;
                    int sel = _txtInput.SelectionStart;
                    _txtInput.SelectedText = Environment.NewLine;
                    _txtInput.SelectionStart = sel + Environment.NewLine.Length;
                }
                else
                {
                    e.SuppressKeyPress = true;
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
            _lblChatTitle.Text = peer.NickName ?? "在线节点";
            _inputPlaceholder = string.Format("给 {0} 发消息", peer.NickName ?? "好友");
            _chatView.ShowGroupSenderName = false;
            SwitchActiveChatView();
            _chatHeader.Invalidate();
            _inputPanel.Invalidate();
            _listSessions.Invalidate();
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
