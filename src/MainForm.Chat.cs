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

        private void RenderSessionList()
        {
            if (_listSessions == null || _sessionPresenter == null) return;

            int previousTop = _listSessions.TopIndex;
            object topItem = previousTop >= 0 && previousTop < _listSessions.Items.Count
                ? _listSessions.Items[previousTop] : null;
            _listSessions.BeginUpdate();
            try
            {
                _listSessions.ClearSessionItems();
                _hoverSessionIndex = -1;

                var items = _sessionPresenter.FilteredSessionItems;
                for (int i = 0; i < items.Count; i++)
                    _listSessions.Items.Add(items[i]);

                int index = _sessionPresenter.FindSessionIndex(_activeChatKey);
                if (index >= 0 && index < _listSessions.Items.Count) _listSessions.SelectedIndex = index;

                // 先恢复选中会话，再恢复视口。当前聊天可以位于视口之外。
                var topPeer = topItem as PeerInfo;
                int top = topPeer == null
                    ? (topItem == null ? -1 : _listSessions.Items.IndexOf(topItem))
                    : _sessionPresenter.FindSessionIndex(_sessionPresenter.GetSessionKey(topPeer));
                _listSessions.RestoreScrollAnchor(top);
            }
            finally
            {
                _listSessions.EndUpdate();
            }
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            if (_btnSearchClear != null)
            {
                bool hasText = !string.IsNullOrEmpty(_txtSearch.Text);
                if (_btnSearchClear.Visible != hasText)
                {
                    _btnSearchClear.Visible = hasText;
                    if (_txtSearch != null && _searchBoxContainer != null)
                    {
                        int clearSize = 24;
                        int rightPad = hasText ? (clearSize + 16) : 28;
                        _txtSearch.Width = Math.Max(40, _searchBoxContainer.Width - 14 - rightPad);
                    }
                }
            }

            if (_sessionPresenter != null)
            {
                _sessionPresenter.SearchFilter = _txtSearch.Text;
            }
        }

        private void ListSessions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_listSessions.SelectedItem == null || _sessionPresenter == null) return;
            _sessionPresenter.SelectSession(_listSessions.SelectedItem);
        }

        private void OnActiveSessionChanged(string newKey, PeerInfo newPeer, string titleText, string placeholder, bool showGroupSender)
        {
            CheckAndClearGlobalNotifications();

            _lblChatTitle.Text = titleText;
            if (_chatView != null)
            {
                _chatView.ShowGroupSenderName = showGroupSender;
            }

            SwitchActiveChatView();
            RefreshAttachmentDraft();

            if (_chatHeader != null) _chatHeader.Invalidate();
            if (_listSessions != null) _listSessions.Invalidate();
        }

        private void SwitchActiveChatView()
        {
            if (_chatView != null && _chatPresenter != null)
            {
                _chatView.SetMessages(_chatPresenter.GetActiveMessages());
            }
        }

        private void OnMessageAppended(ChatMessage msg, string convKey)
        {
            if (_activeChatKey == convKey && _chatView != null)
            {
                _chatView.AppendMessage(msg);
            }
            if (_listSessions != null) _listSessions.Invalidate();
        }

        private void OnMessageNotification(ChatMessage msg)
        {
            FlashTaskbar(true);
            StartTrayFlash();
            UpdateTrayTooltipText();
            if (_listSessions != null) _listSessions.Invalidate();

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

        private bool IsActivelyViewing(string convKey)
        {
            bool isForeground = (Form.ActiveForm == this) && (this.WindowState != FormWindowState.Minimized) && this.Visible;
            bool isCurrent = string.Equals(_activeChatKey, convKey, StringComparison.OrdinalIgnoreCase);
            return isForeground && isCurrent;
        }

        private int FindSessionIndexForKey(string key)
        {
            return _sessionPresenter != null ? _sessionPresenter.FindSessionIndex(key) : -1;
        }

        private void ListSessions_MouseMove(object sender, MouseEventArgs e)
        {
            if (_listSessions == null) return;
            int index = _listSessions.IndexFromPoint(e.Location);
            if (index < 0 || index >= _listSessions.Items.Count || _listSessions.IsHeading(index))
                index = -1;
            if (index == _hoverSessionIndex) return;

            int old = _hoverSessionIndex;
            _hoverSessionIndex = index;
            if (old >= 0 && old < _listSessions.Items.Count)
            {
                try { _listSessions.Invalidate(_listSessions.GetItemRectangle(old)); } catch { }
            }
            if (_hoverSessionIndex >= 0)
            {
                try { _listSessions.Invalidate(_listSessions.GetItemRectangle(_hoverSessionIndex)); } catch { }
            }
        }

        private void ListSessions_MouseLeave(object sender, EventArgs e)
        {
            if (_hoverSessionIndex < 0) return;
            int old = _hoverSessionIndex;
            _hoverSessionIndex = -1;
            if (_listSessions != null && old < _listSessions.Items.Count)
            {
                try { _listSessions.Invalidate(_listSessions.GetItemRectangle(old)); } catch { }
            }
        }

        // 两组同时显示时，仅在离线标题前留出 8 像素。
        private int SessionHeadingGap(int index)
        {
            return index > 0 && (SessionPresenter.GroupHeading)_listSessions.Items[index] == SessionPresenter.GroupHeading.Offline
                && _listSessions.Items.Contains(SessionPresenter.GroupHeading.Online) ? 8 : 0;
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
            bool isPinned = (item is string && (string)item == SessionPresenter.BROADCAST_TARGET);
            bool isSelected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected);
            bool isHover = (!isSelected && e.Index == _hoverSessionIndex);

            // 1. 填充底色
            using (SolidBrush bgBrush = new SolidBrush(GrokTheme.Level0))
            {
                g.FillRectangle(bgBrush, r);
            }

            if (item is SessionPresenter.GroupHeading)
            {
                bool online = (SessionPresenter.GroupHeading)item == SessionPresenter.GroupHeading.Online;
                string label = online ? "在线" : "离线";
                int left = r.Left + GrokTheme.SessionCardMarginX;
                int gap = SessionHeadingGap(e.Index);
                int height = r.Height - gap;
                using (Font font = GrokTheme.UiFont(10.5f, FontStyle.Bold))
                using (var status = new SolidBrush(online ? GrokTheme.AccentGreen : GrokTheme.TextDestructive))
                {
                    GrokTheme.DrawUiTextLeft(g, label, font, GrokTheme.TextSecondary,
                        left, r.Top + gap, r.Width - GrokTheme.SessionCardMarginX * 2, height);
                    int dotX = left + GrokTheme.MeasureUiText(g, label, font).Width + 6;
                    g.FillEllipse(status, dotX, r.Top + gap + (height - 6) / 2, 6, 6);
                }
                return;
            }

            int cardMarginX = GrokTheme.SessionCardMarginX;
            int cardMarginY = GrokTheme.SessionCardMarginY;
            int cardRadius = GrokTheme.SessionCardRadius;

            Rectangle cardRect = new Rectangle(
                r.Left + cardMarginX,
                r.Top + cardMarginY,
                r.Width - cardMarginX * 2,
                r.Height - cardMarginY * 2);

            Color currentCardBg = GrokTheme.Level0;

            if (isSelected)
            {
                currentCardBg = GrokTheme.SessionSelected;
                using (SolidBrush cardBrush = new SolidBrush(currentCardBg))
                using (Pen cardPen = new Pen(GrokTheme.Accent, 1f))
                {
                    DrawRoundedRectangle(g, cardBrush, cardRect, cardRadius);
                    DrawRoundedRectangle(g, cardPen, cardRect, cardRadius);
                }
            }
            else if (isHover)
            {
                currentCardBg = isPinned ? GrokTheme.Surface(32, 28, 42) : GrokTheme.SessionHover;
                using (SolidBrush cardBrush = new SolidBrush(currentCardBg))
                {
                    DrawRoundedRectangle(g, cardBrush, cardRect, cardRadius);
                }
            }
            else if (isPinned)
            {
                currentCardBg = GrokTheme.Surface(24, 21, 32);
                Color pinnedBorder = GrokTheme.Surface(42, 36, 56);
                using (SolidBrush cardBrush = new SolidBrush(currentCardBg))
                using (Pen cardPen = new Pen(pinnedBorder, 1f))
                {
                    DrawRoundedRectangle(g, cardBrush, cardRect, cardRadius);
                    DrawRoundedRectangle(g, cardPen, cardRect, cardRadius);
                }
            }

            int avSize = GrokTheme.SessionAvatarSize;
            int avX = r.Left + GrokTheme.SessionAvatarPaddingLeft;
            int avY = r.Top + (r.Height - avSize) / 2;
            Rectangle avatarRect = new Rectangle(avX, avY, avSize, avSize);
            int textX = r.Left + 100;
            int textW = Math.Max(40, r.Right - textX - 18);
            int titleY = r.Top + 14;
            int subY = r.Top + 38;

            Color subTextColor = isSelected ? GrokTheme.TextPrimary : GrokTheme.TextSubtitle;

            if (item is string)
            {
                string sItem = (string)item;
                WeChatIcon.DrawMascotAvatar(g, avatarRect, GrokTheme.Accent, false, false, currentCardBg);

                using (Font titleFont = GrokTheme.UiFont(11.0f, FontStyle.Bold))
                {
                    string title = sItem;
                    if (sItem == SessionPresenter.BROADCAST_TARGET)
                    {
                        int onlineCount = _sessionPresenter != null ? _sessionPresenter.GetOnlineCount() : 0;
                        title = onlineCount > 0 ? string.Format("局域网全员群聊 ({0})", onlineCount) : "局域网全员群聊";
                    }
                    GrokTheme.DrawUiTextLeft(g, title, titleFont, GrokTheme.TextTitle,
                        textX, titleY - 3, textW - (isPinned ? 38 : 0), 27);
                }

                if (isPinned)
                {
                    Color pinColor = isSelected ? Color.White : GrokTheme.Accent;
                    using (Font pinFont = GrokTheme.UiFont(9.5f))
                    {
                        GrokTheme.DrawUiTextRight(g, "置顶", pinFont, pinColor,
                            r.Right - 59, titleY - 2, 40, 26);
                    }
                }

                string snippet = _sessionManager != null ? _sessionManager.GetLastMessageSnippet(SessionManager.BROADCAST_KEY, int.MaxValue) : null;
                if (string.IsNullOrEmpty(snippet)) snippet = "已按 Rules 刷新本机记忆 (刷新...";

                using (Font subFont = GrokTheme.UiFont(9.5f))
                {
                    ChatTextLayout.DrawSnippet(g, snippet, subFont, subTextColor, new Rectangle(textX, subY, textW, 20));
                }

                DrawUnreadBadge(g, avatarRect, _broadcastUnreadCount);
            }
            else if (item is PeerInfo)
            {
                PeerInfo peer = (PeerInfo)item;

                bool useCoralStyle = (e.Index % 3 == 0);
                Color bodyColor = useCoralStyle ? Color.FromArgb(252, 44, 62) : Color.FromArgb(246, 148, 18);
                string badge = useCoralStyle ? "节点" : null;
                WeChatIcon.DrawMascotAvatar(g, avatarRect, bodyColor, !useCoralStyle, false, currentCardBg, badge);

                using (Font titleFont = GrokTheme.UiFont(11.0f, FontStyle.Bold))
                {
                    string name = _sessionPresenter.GetPeerDisplayName(peer);
                    if (name.Length > 13) name = name.Substring(0, 12) + "...";
                    GrokTheme.DrawUiTextLeft(g, name, titleFont, GrokTheme.TextTitle,
                        textX, titleY - 3, textW, 27);
                }

                string snippet = _sessionManager.GetLastMessageSnippet(_sessionPresenter.GetSessionKey(peer), int.MaxValue);
                if (string.IsNullOrEmpty(snippet))
                {
                    snippet = peer.IP == null ? peer.HostName : string.Format("{0} ({1})", peer.HostName, peer.IP);
                }
                using (Font subFont = GrokTheme.UiFont(9.5f))
                {
                    ChatTextLayout.DrawSnippet(g, snippet, subFont, subTextColor, new Rectangle(textX, subY, textW, 20));
                }

                DrawUnreadBadge(g, avatarRect, peer.UnreadCount);
            }
        }

        private void DrawUnreadBadge(Graphics g, Rectangle avatarRect, int unreadCount)
        {
            if (unreadCount <= 0) return;

            string unreadText = unreadCount > 99 ? "99+" : unreadCount.ToString();
            using (Font badgeFont = new Font("Microsoft YaHei UI", 8.0f, FontStyle.Bold))
            using (SolidBrush badgeBg = new SolidBrush(Color.FromArgb(250, 81, 81)))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (Pen borderPen = new Pen(GrokTheme.Level0, 1.5f))
            {
                SizeF sz = g.MeasureString(unreadText, badgeFont);
                int badgeH = 16;
                int badgeW = Math.Max(badgeH, (int)Math.Ceiling(sz.Width) + 6);
                int bx = avatarRect.Right - badgeW / 2 - 2;
                int by = avatarRect.Top - 3;
                Rectangle badgeRect = new Rectangle(bx, by, badgeW, badgeH);

                DrawRoundedRectangle(g, badgeBg, badgeRect, badgeH / 2);
                DrawRoundedRectangle(g, borderPen, badgeRect, badgeH / 2);

                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(unreadText, badgeFont, textBrush,
                    new RectangleF(badgeRect.X, badgeRect.Y + 0.5f, badgeRect.Width, badgeRect.Height), sf);
            }
        }

        #endregion

        #region Messaging & Input

        private void TxtInput_TextChanged(object sender, EventArgs e)
        {
            bool hasText = !string.IsNullOrWhiteSpace(_txtInput.GetMessageText()) || GetAttachmentDraft(_activeChatKey) != null;
            if (_btnSendWave != null && _btnSendWave.IsActive != hasText)
            {
                _btnSendWave.IsActive = hasText;
            }
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                bool send = AppSettings.SendShortcutMode == 1
                    ? e.Control && !e.Shift
                    : !e.Control && !e.Shift;
                if (send)
                {
                    e.SuppressKeyPress = true;
                    SendMessage();
                }
                else if (!e.Control || e.Shift)
                {
                    // 非发送模式保留输入框的换行行为。
                    e.SuppressKeyPress = false;
                }
                else
                {
                    e.SuppressKeyPress = true;
                }
            }
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            SendMessage();
        }

        private void SendMessage()
        {
            string text = _txtInput.GetMessageText().Trim();
            string chat = _activeChatKey;
            var attachments = GetAttachmentDraft(chat);
            if (string.IsNullOrEmpty(text) && attachments == null) return;
            if (_selectedPeer != null && _selectedPeer.TcpPort <= 0)
            {
                GrokMessageBox.Show(this, "好友当前离线，暂时无法发送消息。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (attachments != null)
            {
                bool sent = _selectedPeer != null
                    ? SendFilesToPeer(_selectedPeer, attachments.ToArray(), TakeDraftImage)
                    : SendFilesToPeers(PickFileRecipients(IsBroadcastChat(chat)), attachments.ToArray());
                if (!sent) return;
                ClearAttachmentDraft(chat);
                RefreshAttachmentDraft();
            }

            if (string.IsNullOrEmpty(text)) return;
            _txtInput.Clear();

            if (_chatPresenter != null)
            {
                _chatPresenter.SendTextMessage(text);
            }

            if (_listSessions != null) _listSessions.Invalidate();
        }

        #endregion

        #region Emoji Popup

        private void ShowEmojiPopup()
        {
            Control anchor = _btnToolEmoji ?? (Control)this;
            ChatEmojiPicker.Open(this, anchor, token =>
            {
                _txtInput.Visible = true;
                if (_txtInput.TextLength - _txtInput.SelectionLength + token.Length <= _txtInput.MaxLength)
                    _txtInput.InsertEmoji(token);
                _txtInput.Focus();
            });
        }

        #endregion

        #region Actions & Navigation

        /// <summary>
        /// 切换选中指定好友节点的私聊会话
        /// </summary>
        /// <param name="peer">目标对端节点</param>
        public void SelectPeerChat(PeerInfo peer)
        {
            if (peer == null || _sessionPresenter == null) return;
            _sessionPresenter.SelectPeerChat(peer);
        }

        private void BtnConnectNode_Click(object sender, EventArgs e)
        {
            using (GrokConnectNodeForm form = new GrokConnectNodeForm(
                _netService,
                (ip) => {
                    if (_chatPresenter != null)
                        _chatPresenter.AppendChatSystem("正在主动向 " + ip + " 发起 TCP 直连握手...");
                    _netService.ProbeIp(ip, (success, msg) => {
                        SafeInvoke(() => {
                            if (_chatPresenter != null) _chatPresenter.AppendChatSystem(msg);
                        });
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
                        if (_chatPresenter != null)
                            _chatPresenter.AppendChatSystem("正在主动向 " + nameOrIp + " 发起直连握手...");
                        _netService.ProbeIp(nameOrIp, (success, msg) => {
                            SafeInvoke(() => {
                                if (_chatPresenter != null) _chatPresenter.AppendChatSystem(msg);
                            });
                        });
                    }
                    else
                    {
                        if (_chatPresenter != null)
                            _chatPresenter.AppendChatSystem("已创建新节点: " + nameOrIp);
                    }
                }
            }
        }

        private void BtnAddIp_Click(object sender, EventArgs e)
        {
            BtnConnectNode_Click(sender, e);
        }

        /// <summary>
        /// 会话列表鼠标右键弹窗菜单响应（置顶、删除会话、修改备注等）
        /// </summary>
        public void ListSessions_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int index = _listSessions.IndexFromPoint(e.Location);
                if (index >= 0 && !_listSessions.IsHeading(index))
                {
                    _listSessions.SelectedIndex = index;
                    object item = _listSessions.Items[index];

                    if (item is PeerInfo)
                    {
                        PeerInfo peer = (PeerInfo)item;
                        GrokPopupMenu menu = new GrokPopupMenu();
                        menu.AddItem("发送文件...", WeChatIconType.Folder, () => BtnSendFile_Click(this, EventArgs.Empty))
                            .AddSeparator()
                            .AddItem("删除", WeChatIconType.Clear, () => ConfirmDeletePeerSession(peer), null, true);

                        menu.Show(_listSessions, e.Location);
                    }
                    else if (item is string)
                    {
                        GrokPopupMenu menu = new GrokPopupMenu();
                        menu.AddItem("发送文件给所有人...", WeChatIconType.Folder, () => BtnSendFile_Click(this, EventArgs.Empty))
                            .AddSeparator()
                            .AddItem("清空群聊记录", WeChatIconType.Clear, () => ConfirmDeleteBroadcastSession(), null, true);
                        menu.Show(_listSessions, e.Location);
                    }
                }
            }
        }

        private void ConfirmDeletePeerSession(PeerInfo peer)
        {
            if (peer == null) return;
            string peerNick = !string.IsNullOrEmpty(peer.NickName) ? peer.NickName : "未知节点";

            DialogResult dr = GrokMessageBox.Show(
                this,
                string.Format("确定要删除好友“{0}”及全部历史消息吗？\n删除后历史记录将无法恢复。", peerNick),
                "删除确认",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (dr == DialogResult.OK && _sessionPresenter != null)
            {
                ClearAttachmentDraft(SessionManager.GetLanKey(peer.Id, peer.IP));
                _sessionPresenter.DeletePeerSession(peer);
            }
        }

        private void ConfirmClearPeerChat()
        {
            if (_selectedPeer == null) return;
            string peerNick = !string.IsNullOrEmpty(_selectedPeer.NickName) ? _selectedPeer.NickName : "未知节点";

            DialogResult dr = GrokMessageBox.Show(
                this,
                string.Format("确定要仅清空与“{0}”的当前聊天历史吗？\n仅清空当前聊天历史（不删除好友），此操作无法恢复。", peerNick),
                "清空确认",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (dr == DialogResult.OK) ClearCurrentChat();
        }

        private void ConfirmDeleteBroadcastSession()
        {
            DialogResult dr = GrokMessageBox.Show(
                this,
                "确定要清空局域网全员群聊的全部历史消息吗？\n删除后历史记录将无法恢复。",
                "清空确认",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (dr == DialogResult.OK && _sessionPresenter != null)
            {
                _sessionPresenter.ClearBroadcastSession();
            }
        }

        #endregion
    }
}
