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

            object currentSelected = _listSessions.SelectedItem;
            _listSessions.BeginUpdate();
            _listSessions.Items.Clear();

            var items = _sessionPresenter.FilteredSessionItems;
            for (int i = 0; i < items.Count; i++)
            {
                _listSessions.Items.Add(items[i]);
            }

            int index = _sessionPresenter.FindSessionIndex(currentSelected);
            if (index >= 0) _listSessions.SelectedIndex = index;
            else if (_listSessions.Items.Count > 0) _listSessions.SelectedIndex = 0;

            _listSessions.EndUpdate();
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
            _inputPlaceholder = placeholder;
            if (_chatView != null)
            {
                _chatView.ShowGroupSenderName = showGroupSender;
            }

            SwitchActiveChatView();

            if (_chatHeader != null) _chatHeader.Invalidate();
            if (_inputPanel != null) _inputPanel.Invalidate();
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
            if (string.IsNullOrEmpty(key) || _listSessions == null) return -1;
            for (int i = 0; i < _listSessions.Items.Count; i++)
            {
                object item = _listSessions.Items[i];
                if (item is string && key == "BROADCAST") return i;
                if (item is PeerInfo && ((PeerInfo)item).Id == key) return i;
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
                using (Pen cardPen = new Pen(Color.FromArgb(145, 95, 240), 1f))
                {
                    DrawRoundedRectangle(g, cardBrush, cardRect, cardRadius);
                    DrawRoundedRectangle(g, cardPen, cardRect, cardRadius);
                }
            }
            else if (isHover)
            {
                currentCardBg = isPinned ? Color.FromArgb(32, 28, 42) : GrokTheme.SessionHover;
                using (SolidBrush cardBrush = new SolidBrush(currentCardBg))
                {
                    DrawRoundedRectangle(g, cardBrush, cardRect, cardRadius);
                }
            }
            else if (isPinned)
            {
                currentCardBg = Color.FromArgb(24, 21, 32);
                Color pinnedBorder = Color.FromArgb(42, 36, 56);
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

            Color subTextColor = isSelected ? Color.FromArgb(228, 218, 250) : GrokTheme.TextSubtitle;

            if (item is string)
            {
                string sItem = (string)item;
                WeChatIcon.DrawMascotAvatar(g, avatarRect, Color.FromArgb(144, 90, 251), false, false, currentCardBg);

                using (Font titleFont = GrokTheme.Font(11.0f, FontStyle.Bold))
                {
                    string title = sItem;
                    if (sItem == SessionPresenter.BROADCAST_TARGET)
                    {
                        int onlineCount = _sessionPresenter != null ? _sessionPresenter.GetOnlineCount() : 0;
                        title = onlineCount > 0 ? string.Format("局域网全员群聊 ({0})", onlineCount) : "局域网全员群聊";
                    }
                    GrokTheme.DrawUiTextLeft(g, title, titleFont, GrokTheme.TextTitle,
                        textX, titleY, textW - (isPinned ? 38 : 0), 22);
                }

                if (isPinned)
                {
                    Color pinColor = isSelected ? Color.White : Color.FromArgb(144, 100, 220);
                    using (Font pinFont = GrokTheme.Font(8.0f, FontStyle.Regular))
                    {
                        GrokTheme.DrawUiTextRight(g, "置顶", pinFont, pinColor,
                            r.Right - 55, titleY + 3, 35, 18);
                    }
                }

                string snippet = _sessionManager != null ? _sessionManager.GetLastMessageSnippet(SessionManager.BROADCAST_KEY, 24) : null;
                if (string.IsNullOrEmpty(snippet)) snippet = "已按 Rules 刷新本机记忆 (刷新...";

                using (Font subFont = GrokTheme.Font(9.5f, FontStyle.Regular))
                {
                    if (snippet.Length > 20) snippet = snippet.Substring(0, 19) + "...";
                    GrokTheme.DrawUiTextLeft(g, snippet, subFont, subTextColor,
                        textX, subY, textW, 20);
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
                    GrokTheme.DrawUiTextLeft(g, snippet, subFont, subTextColor,
                        textX, subY, textW, 20);
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
            bool hasText = !string.IsNullOrWhiteSpace(_txtInput.Text);
            if (_btnSendWave != null && _btnSendWave.IsActive != hasText)
            {
                _btnSendWave.IsActive = hasText;
            }
            if (_inputPanel != null) _inputPanel.Invalidate();
            if (_bottomSendPanel != null) _bottomSendPanel.Invalidate();
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
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
                if (index >= 0)
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
                _sessionPresenter.DeletePeerSession(peer);
            }
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
