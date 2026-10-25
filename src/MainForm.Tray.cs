using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
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
                string initialNick = Environment.UserName;
                _netService = new NetworkService(initialNick);
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
                MessageBox.Show(this, "启动网络服务失败: " + ex.Message, "网络错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            int avSize = 82;
            int avX = (_heroProfilePanel.Width - avSize) / 2;
            int avY = 20;
            Rectangle avRect = new Rectangle(avX, avY, avSize, avSize);
            WeChatIcon.DrawMascotAvatar(g, avRect, Color.FromArgb(252, 44, 62), false, true, Color.FromArgb(14, 14, 14));

            // 头像正下方居中绘制昵称文字 (10.5pt Microsoft YaHei, 完美对标 Grok Bot Karina)
            string nick = _netService != null && !string.IsNullOrEmpty(_netService.NickName) ? _netService.NickName : Environment.UserName;
            using (Font f = new Font("Microsoft YaHei", 10.5f, FontStyle.Regular))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(208, 208, 214)))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Near
                };
                g.DrawString(nick, f, textBrush, new RectangleF(0, avY + avSize + 16, _heroProfilePanel.Width, 24), sf);
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
            int menuH = 368;
            int x = screenPt.X;
            int y = screenPt.Y - menuH - 8;

            // 若上方空间不足（如靠顶部的 Hero 头像点击），向下弹出
            if (y < 40)
            {
                y = screenPt.Y + anchor.Height + 8;
            }

            GrokSettingsMenuForm menu = new GrokSettingsMenuForm(
                _netService,
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
            MessageBox.Show(this, "LanChat v" + APP_VERSION + "\n局域网秒级极速通讯与文件互传\nUI: Grok Bot 极简现代纯黑视觉 (100% 像素级复刻)\n零外部运行环境依赖\n开源主页: https://github.com/" + GITHUB_REPO, "关于 LanChat", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowChangeNickDialog()
        {
            string currentNick = _netService != null ? _netService.NickName : Environment.UserName;
            using (Form prompt = new Form())
            {
                if (this.Icon != null) prompt.Icon = this.Icon;
                prompt.Text = "修改我的昵称";
                prompt.Size = new Size(360, 160);
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
                prompt.BackColor = Color.FromArgb(24, 24, 26);
                prompt.ForeColor = Color.White;

                Label lbl = new Label { Text = "请输入新的局域网昵称:", Location = new Point(16, 16), AutoSize = true };
                TextBox txt = new TextBox { Text = currentNick, Location = new Point(18, 42), Width = 308, BackColor = Color.FromArgb(16, 16, 18), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
                Button btnOk = new Button { Text = "保存", Location = new Point(156, 78), Size = new Size(80, 28), DialogResult = DialogResult.OK, BackColor = Color.FromArgb(121, 70, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                btnOk.FlatAppearance.BorderSize = 0;
                Button btnCancel = new Button { Text = "取消", Location = new Point(246, 78), Size = new Size(80, 28), DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(46, 46, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                btnCancel.FlatAppearance.BorderSize = 0;

                prompt.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                prompt.AcceptButton = btnOk;
                prompt.CancelButton = btnCancel;

                if (prompt.ShowDialog(this) == DialogResult.OK)
                {
                    string newNick = txt.Text.Trim();
                    if (!string.IsNullOrEmpty(newNick) && _netService != null)
                    {
                        _netService.NickName = newNick;
                        _netService.BroadcastPresence("HELLO");
                        _btnAvatar.Invalidate();
                        if (_btnBottomUser != null) _btnBottomUser.Invalidate();
                        AppendChatSystem("您的昵称已更改为: " + newNick);
                    }
                }
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
                MessageBox.Show(this, "打开文件夹失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region System Tray & Lifecycle

        private void InitializeTrayIcon()
        {
            try
            {
                _trayIcon = new NotifyIcon();
                _trayIcon.Icon = this.Icon ?? SystemIcons.Application;
                _trayIcon.Text = "LanChat 局域网通讯";
                _trayIcon.Visible = true;

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

                // WeChat Dark 风格暗黑托盘右键菜单
                ContextMenuStrip trayMenu = new ContextMenuStrip();
                trayMenu.Renderer = new WeChatDarkMenuRenderer();
                trayMenu.ShowImageMargin = false;
                trayMenu.ShowCheckMargin = false;
                trayMenu.BackColor = Color.FromArgb(44, 44, 46);
                trayMenu.ForeColor = Color.FromArgb(235, 235, 235);

                ToolStripMenuItem itemShow = new ToolStripMenuItem("显示主界面");
                itemShow.Font = new Font(this.Font, FontStyle.Bold);
                itemShow.Click += (s, e) => RestoreFromTray();
                trayMenu.Items.Add(itemShow);

                ToolStripMenuItem itemCheckUpdate = new ToolStripMenuItem("🔄 检查新版本...");
                itemCheckUpdate.Click += (s, e) => CheckForUpdates(false);
                trayMenu.Items.Add(itemCheckUpdate);

                trayMenu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem itemExit = new ToolStripMenuItem("退出 LanChat");
                itemExit.Click += (s, e) =>
                {
                    _isExplicitExit = true;
                    this.Close();
                };
                trayMenu.Items.Add(itemExit);

                _trayIcon.ContextMenuStrip = trayMenu;
            }
            catch { }
        }

        private void RestoreFromTray()
        {
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
    }
}
