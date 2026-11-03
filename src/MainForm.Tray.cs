using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace LanChat
{
    public partial class MainForm : Form
    {
        #region Network & Init

        private void InitializeNetwork()
        {
            try
            {
                string initialNick = !string.IsNullOrEmpty(AppSettings.NickName) ? AppSettings.NickName : Environment.UserName;
                _netService = new NetworkService(initialNick);
                _netService.AutoAcceptFiles = AppSettings.AutoAcceptFiles;
                _netService.OnPeerJoined += (p) => SafeInvoke(() => PeerJoined(p));
                _netService.OnPeerUpdated += (p) => SafeInvoke(() => PeerUpdated(p));
                _netService.OnPeerLeft += (pid) => SafeInvoke(() => PeerLeft(pid));
                _netService.OnMessageReceived += (msg) => SafeInvoke(() => HandleReceivedMessage(msg));
                _netService.OnFileProgress += (prog) => SafeInvoke(() => HandleFileProgress(prog));
                _netService.OnFileReceived += (nick, fn, path, sz) => SafeInvoke(() => HandleFileReceived(nick, fn, path, sz));
                _netService.OnLogMessage += (log) => { };

                _netService.Start();

                IPAddress primaryIp = _netService.GetPrimaryPhysicalIp();
                _btnAvatar.Invalidate();

                AppendChatSystem("LanChat 启动成功！已开始在局域网内搜索好友...");
                AppendChatSystem(string.Format("物理网卡绑定 IP: {0}:{1}", primaryIp, _netService.LocalTcpPort));
                AppendChatSystem(string.Format("文件接收目录: {0}", _netService.SaveDirectory));

                RefreshPeerListUI();
            }
            catch (Exception ex)
            {
                GrokMessageBox.Show(this, "启动网络服务失败: " + ex.Message, "网络错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SwitchNavTab(int tabIndex)
        {
            _currentNavTab = tabIndex;
            _btnNavChat.IsActive = (tabIndex == 0);
            _btnNavContacts.IsActive = (tabIndex == 1);
            RefreshPeerListUI();
        }

        private void BtnAvatar_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            GrokTheme.PrepareSoftText(g);

            int avSize = 82;
            int avX = (_heroProfilePanel.Width - avSize) / 2;
            int avY = 20;
            Rectangle avRect = new Rectangle(avX, avY, avSize, avSize);
            WeChatIcon.DrawMascotAvatar(g, avRect, Color.FromArgb(252, 44, 62), false, true, Color.FromArgb(14, 14, 14));

            // Hero 昵称：12.0pt YaHei UI Regular + soft AA（对标 Alis Karina）
            string nick = _netService != null && !string.IsNullOrEmpty(_netService.NickName) ? _netService.NickName : Environment.UserName;
            using (Font f = new Font("Microsoft YaHei UI", 12.0f, FontStyle.Regular))
            using (StringFormat sf = new StringFormat(StringFormatFlags.NoWrap))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Near;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None;
                GrokTheme.DrawUiText(g, nick, f, GrokTheme.TextPrimary,
                    new Rectangle(0, avY + avSize + 14, _heroProfilePanel.Width, 28), sf);
            }
        }

        private void BtnAvatar_Click(object sender, EventArgs e)
        {
            BtnSettings_Click(sender, e);
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            Control anchor = (sender as Control) ?? _btnBottomUser ?? _btnAvatar;
            Point screenPt = anchor.PointToScreen(Point.Empty);
            int menuH = 332;
            int x = screenPt.X;
            int y = screenPt.Y - menuH - 8;

            // 若上方空间不足（如靠顶部的 Hero 头像点击），向下弹出
            if (y < 40)
            {
                y = screenPt.Y + anchor.Height + 8;
            }

            GrokSettingsMenuForm menu = new GrokSettingsMenuForm(
                _netService,
                () => ShowSettingsDialog(0),
                () => CheckForUpdates(false),
                () => ShowChangeNickDialog(),
                () => OpenReceivedFolder(),
                () => ClearCurrentChat(),
                () => ShowAboutDialog(),
                () => {
                    _isExplicitExit = true;
                    this.Close();
                }
            );
            menu.Location = new Point(x, y);
            menu.Show(this);
        }

        private void ShowAboutDialog()
        {
            using (GrokAboutForm about = new GrokAboutForm(APP_VERSION))
            {
                about.ShowDialog(this);
            }
        }

        private void ShowChangeNickDialog()
        {
            string currentNick = _netService != null ? _netService.NickName : Environment.UserName;
            using (GrokChangeNickForm prompt = new GrokChangeNickForm(currentNick))
            {
                if (prompt.ShowDialog(this) == DialogResult.OK)
                {
                    string newNick = prompt.NewNickName;
                    if (!string.IsNullOrEmpty(newNick) && _netService != null)
                    {
                        _netService.NickName = newNick;
                        AppSettings.NickName = newNick;
                        AppSettings.Save();
                        _netService.BroadcastPresence("HELLO");
                        _btnAvatar.Invalidate();
                        if (_btnBottomUser != null) _btnBottomUser.Invalidate();
                        AppendChatSystem("您的昵称已更改为: " + newNick);
                    }
                }
            }
        }

        private void ShowSettingsDialog(int tab = 0)
        {
            using (GrokSettingsDialog dlg = new GrokSettingsDialog(
                _netService,
                () => CheckForUpdates(false),
                () => ShowChangeNickDialog(),
                () => OpenReceivedFolder(),
                tab))
            {
                dlg.ShowDialog(this);
            }
        }

        private void RefreshPeersWithNotice()
        {
            if (_netService != null)
            {
                _netService.BroadcastPresence("HELLO");
                AppendChatSystem("已向局域网广播并扫描同网段好友...");
            }
        }

        private void OpenReceivedFolder()
        {
            try
            {
                if (_netService != null)
                {
                    if (!Directory.Exists(_netService.SaveDirectory))
                    {
                        Directory.CreateDirectory(_netService.SaveDirectory);
                    }
                    Process.Start("explorer.exe", _netService.SaveDirectory);
                }
            }
            catch (Exception ex)
            {
                GrokMessageBox.Show(this, "打开文件夹失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region System Tray & Lifecycle

        private void InitializeTrayIcon()
        {
            try
            {
                _normalTrayIcon = this.Icon ?? WeChatIcon.CreateAppIcon(16);
                try
                {
                    using (Bitmap bmp = new Bitmap(16, 16))
                    {
                        IntPtr h = bmp.GetHicon();
                        _blankTrayIcon = (Icon)Icon.FromHandle(h).Clone();
                        DestroyIcon(h);
                    }
                }
                catch { }

                _trayFlashTimer = new System.Windows.Forms.Timer();
                _trayFlashTimer.Interval = 450;
                _trayFlashTimer.Tick += (s, e) => {
                    if (!_isTrayFlashing || _trayIcon == null) return;
                    _trayFlashState = !_trayFlashState;
                    _trayIcon.Icon = _trayFlashState ? _normalTrayIcon : (_blankTrayIcon ?? _normalTrayIcon);
                };

                _trayIcon = new NotifyIcon();
                _trayIcon.Icon = _normalTrayIcon ?? this.Icon;
                _trayIcon.Text = "LanChat 局域网通讯";
                _trayIcon.Visible = true;
                _trayIcon.MouseMove += TrayIcon_MouseMove;

                // 左键单击或双击还原主界面
                _trayIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        RestoreFromTray();
                    }
                };
                _trayIcon.DoubleClick += (s, e) =>
                {
                    RestoreFromTray();
                };
                _trayIcon.BalloonTipClicked += (s, e) =>
                {
                    RestoreFromTray();
                };

                // Grok 风格 16px 圆角高雅托盘右键悬浮菜单 (彻底废除原生直角 ContextMenuStrip)
                _trayIcon.ContextMenuStrip = null;
                _trayIcon.MouseUp += (s, e) => {
                    if (e.Button == MouseButtons.Right)
                    {
                        CloseTrayPreviewPopup();
                        GrokPopupMenu trayMenu = new GrokPopupMenu();
                        trayMenu.AddItem("显示主界面", WeChatIconType.Chat, () => RestoreFromTray())
                                .AddItem("系统设置...", WeChatIconType.Settings, () => {
                                    RestoreFromTray();
                                    ShowSettingsDialog(0);
                                })
                                .AddItem("检查新版本...", WeChatIconType.Refresh, () => CheckForUpdates(false))
                                .AddSeparator()
                                .AddItem("退出 LanChat", WeChatIconType.Logout, () => {
                                    _isExplicitExit = true;
                                    this.Close();
                                }, null, true);

                        trayMenu.ShowAt(Cursor.Position);
                    }
                };
            }
            catch { }
        }

        private void RestoreFromTray()
        {
            CloseTrayPreviewPopup();
            if (!this.Visible)
            {
                this.Show();
            }
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            this.Activate();
            this.BringToFront();

            StopTrayFlash();
            FlashTaskbar(false);
            ClearActiveChatUnread();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 用户点击右上角关闭按钮（X）且非显式退出：最小化到托盘
            if (!_isExplicitExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();

                if (!_hasShownTrayBalloon && _trayIcon != null)
                {
                    _hasShownTrayBalloon = true;
                    try
                    {
                        _trayIcon.ShowBalloonTip(2000, "LanChat 已最小化到托盘", "程序已转入后台运行。双击托盘图标重新打开，右键可真正退出。", ToolTipIcon.Info);
                    }
                    catch { }
                }
                return;
            }

            CloseTrayPreviewPopup();
            if (_trayPreviewPopup != null)
            {
                try { _trayPreviewPopup.Dispose(); } catch { }
                _trayPreviewPopup = null;
            }

            if (_trayFlashTimer != null)
            {
                _trayFlashTimer.Stop();
                _trayFlashTimer.Dispose();
                _trayFlashTimer = null;
            }
            if (_blankTrayIcon != null)
            {
                try { _blankTrayIcon.Dispose(); } catch { }
                _blankTrayIcon = null;
            }

            // 显式退出或系统关机：安全释放托盘与网络资源
            if (_trayIcon != null)
            {
                try
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                catch { }
                _trayIcon = null;
            }

            if (_netService != null)
            {
                _netService.Dispose();
            }
        }

        #endregion

        #region Online Update (GitHub Releases)

        private void CheckForUpdates(bool silentIfNotNew)
        {
            UpdateService.CheckForUpdates(this, silentIfNotNew, () =>
            {
                _isExplicitExit = true;
                this.Close();
            });
        }

        #endregion

        #region Message Notifications (Taskbar Flash & Tray Flash)

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint FLASHW_STOP = 0;
        private const uint FLASHW_CAPTION = 1;
        private const uint FLASHW_TRAY = 2;
        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMER = 4;
        private const uint FLASHW_TIMERNOFG = 12;

        private Icon _normalTrayIcon = null;
        private Icon _blankTrayIcon = null;
        private System.Windows.Forms.Timer _trayFlashTimer = null;
        private bool _trayFlashState = false;
        private bool _isTrayFlashing = false;

        public void FlashTaskbar(bool flash)
        {
            try
            {
                if (this.IsDisposed || !this.IsHandleCreated) return;
                FLASHWINFO fi = new FLASHWINFO();
                fi.cbSize = (uint)Marshal.SizeOf(fi);
                fi.hwnd = this.Handle;
                if (flash)
                {
                    fi.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
                    fi.uCount = uint.MaxValue;
                    fi.dwTimeout = 0;
                }
                else
                {
                    fi.dwFlags = FLASHW_STOP;
                }
                FlashWindowEx(ref fi);
            }
            catch { }
        }

        public void StartTrayFlash()
        {
            if (_isTrayFlashing) return;
            _isTrayFlashing = true;
            _trayFlashState = false;
            if (_trayFlashTimer != null) _trayFlashTimer.Start();
        }

        public void StopTrayFlash()
        {
            if (!_isTrayFlashing) return;
            _isTrayFlashing = false;
            if (_trayFlashTimer != null) _trayFlashTimer.Stop();
            if (_trayIcon != null && _normalTrayIcon != null)
            {
                _trayIcon.Icon = _normalTrayIcon;
            }
            CloseTrayPreviewPopup();
            UpdateTrayTooltipText();
        }

        public void ClearActiveChatUnread()
        {
            bool changed = false;
            if (_activeChatKey == "BROADCAST")
            {
                if (_broadcastUnreadCount > 0)
                {
                    _broadcastUnreadCount = 0;
                    changed = true;
                }
            }
            else if (_selectedPeer != null)
            {
                if (_selectedPeer.UnreadCount > 0)
                {
                    _selectedPeer.UnreadCount = 0;
                    changed = true;
                }
            }

            if (changed)
            {
                if (_listSessions != null) _listSessions.Invalidate();
            }
            CheckAndClearGlobalNotifications();
            UpdateTrayTooltipText();
        }

        public int GetTotalUnreadCount()
        {
            int total = _broadcastUnreadCount;
            if (_netService != null)
            {
                var peers = _netService.GetActivePeers();
                foreach (var p in peers)
                {
                    total += p.UnreadCount;
                }
            }
            return total;
        }

        public void CheckAndClearGlobalNotifications()
        {
            if (GetTotalUnreadCount() == 0)
            {
                StopTrayFlash();
                FlashTaskbar(false);
            }
        }

        private void MainForm_Activated(object sender, EventArgs e)
        {
            FlashTaskbar(false);
            StopTrayFlash();
            ClearActiveChatUnread();
            CloseTrayPreviewPopup();
            UpdateTrayTooltipText();
        }

        
        private void TrayIcon_MouseMove(object sender, MouseEventArgs e)
        {
            if (GetTotalUnreadCount() > 0)
            {
                ShowTrayPreviewPopup();
            }
        }

        public void ShowTrayPreviewPopup()
        {
            try
            {
                if (_trayPreviewPopup != null && _trayPreviewPopup.Visible) return;

                var items = GetTrayUnreadItems();
                if (items == null || items.Count == 0) return;

                if (_trayPreviewPopup == null || _trayPreviewPopup.IsDisposed)
                {
                    _trayPreviewPopup = new GrokTrayPreviewPopup();
                }

                Point cursorPt = Cursor.Position;
                _trayPreviewPopup.ShowPreview(cursorPt, items, (item) =>
                {
                    RestoreFromTray();
                    if (item != null && !string.IsNullOrEmpty(item.ConvKey))
                    {
                        SwitchToSession(item.ConvKey);
                    }
                });
            }
            catch { }
        }

        public void CloseTrayPreviewPopup()
        {
            try
            {
                if (_trayPreviewPopup != null && _trayPreviewPopup.Visible)
                {
                    _trayPreviewPopup.Hide();
                }
            }
            catch { }
        }

        public List<TrayUnreadItem> GetTrayUnreadItems()
        {
            List<TrayUnreadItem> items = new List<TrayUnreadItem>();

            // 1. 局域网全员群聊 (广播未读)
            if (_broadcastUnreadCount > 0)
            {
                string snippet = _sessionManager != null ? _sessionManager.GetLastMessageSnippet(SessionManager.BROADCAST_KEY, 30) : null;
                if (string.IsNullOrEmpty(snippet)) snippet = _lastBroadcastSnippet;
                if (string.IsNullOrEmpty(snippet)) snippet = "[新广播消息]";

                items.Add(new TrayUnreadItem
                {
                    ConvKey = "BROADCAST",
                    Title = "局域网全员群聊",
                    UnreadCount = _broadcastUnreadCount,
                    LatestSnippet = snippet,
                    TimeText = _lastBroadcastTimeText ?? DateTime.Now.ToString("HH:mm"),
                    IsBroadcast = true
                });
            }

            // 2. 好友私聊未读列表
            if (_netService != null)
            {
                var peers = _netService.GetActivePeers();
                for (int i = 0; i < peers.Count; i++)
                {
                    var p = peers[i];
                    if (p.UnreadCount > 0)
                    {
                        string snippet = _sessionManager != null ? _sessionManager.GetLastMessageSnippet(p.Id, 30) : null;
                        if (string.IsNullOrEmpty(snippet)) snippet = p.LastMessage;
                        if (string.IsNullOrEmpty(snippet)) snippet = "[新消息]";

                        string timeStr = (p.LastMessageTime != DateTime.MinValue) ? p.LastMessageTime.ToString("HH:mm") : DateTime.Now.ToString("HH:mm");
                        Color peerColor = (i % 2 == 0) ? Color.FromArgb(246, 148, 18) : Color.FromArgb(252, 44, 62);

                        items.Add(new TrayUnreadItem
                        {
                            ConvKey = p.Id,
                            Title = !string.IsNullOrEmpty(p.NickName) ? p.NickName : "局域网好友",
                            UnreadCount = p.UnreadCount,
                            LatestSnippet = snippet,
                            TimeText = timeStr,
                            IsBroadcast = false,
                            Peer = p,
                            AvatarColor = peerColor,
                            AvatarShape = 0
                        });
                    }
                }
            }

            return items;
        }

        public void UpdateTrayTooltipText()
        {
            if (_trayIcon == null) return;
            try
            {
                int totalUnread = GetTotalUnreadCount();
                if (totalUnread <= 0)
                {
                    _trayIcon.Text = "LanChat 局域网通讯";
                    return;
                }

                var items = GetTrayUnreadItems();
                string tip;
                if (items.Count == 1)
                {
                    tip = string.Format("LanChat: {0} ({1}条未读)", items[0].Title, items[0].UnreadCount);
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(string.Format("LanChat: {0}条未读 (", totalUnread));
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (i > 0) sb.Append("、");
                        sb.Append(items[i].Title);
                        if (sb.Length > 48) break;
                    }
                    sb.Append(")");
                    tip = sb.ToString();
                }

                if (tip.Length > 63)
                {
                    tip = tip.Substring(0, 60) + "...";
                }
                _trayIcon.Text = tip;
            }
            catch { }
        }

        public void SwitchToSession(string convKey)
        {
            try
            {
                SwitchNavTab(0);
                int idx = FindSessionIndexForKey(convKey);
                if (idx >= 0 && _listSessions != null)
                {
                    if (_listSessions.SelectedIndex == idx)
                    {
                        ListSessions_SelectedIndexChanged(_listSessions, EventArgs.Empty);
                    }
                    else
                    {
                        _listSessions.SelectedIndex = idx;
                    }
                }
            }
            catch { }
        }

        #endregion
    }
}
