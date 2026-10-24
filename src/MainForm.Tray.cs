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
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Rounded rectangle avatar (radius 4px)
            Rectangle rect = new Rectangle(0, 0, 35, 35);
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(7, 193, 96)))
            {
                DrawRoundedRectangle(e.Graphics, brush, rect, 4);
            }

            // Initials
            string nick = _netService != null ? _netService.NickName : Environment.UserName;
            string initial = string.IsNullOrEmpty(nick) ? "我" : nick.Substring(0, 1).ToUpperInvariant();
            using (Font f = new Font("Microsoft YaHei", 11f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(initial, f, textBrush, rect, sf);
            }

            // Green online indicator dot
            Rectangle dotRect = new Rectangle(24, 24, 9, 9);
            using (SolidBrush dotBg = new SolidBrush(Color.FromArgb(37, 37, 38)))
            using (SolidBrush dotFg = new SolidBrush(Color.FromArgb(7, 193, 96)))
            {
                e.Graphics.FillEllipse(dotBg, new Rectangle(23, 23, 11, 11));
                e.Graphics.FillEllipse(dotFg, dotRect);
            }
        }

        private void BtnAvatar_Click(object sender, EventArgs e)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new WeChatDarkMenuRenderer();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.BackColor = Color.FromArgb(44, 44, 46);
            menu.ForeColor = Color.FromArgb(235, 235, 235);

            string myNick = _netService != null ? _netService.NickName : Environment.UserName;
            IPAddress ip = _netService != null ? _netService.GetPrimaryPhysicalIp() : IPAddress.Loopback;
            int port = _netService != null ? _netService.LocalTcpPort : 38889;

            menu.Items.Add(new ToolStripMenuItem("👤 我的昵称: " + myNick) { Enabled = false });
            menu.Items.Add(new ToolStripMenuItem(string.Format("🌐 本机地址: {0}:{1}", ip, port)) { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("✏️ 修改昵称...", null, (s, ev) => ShowChangeNickDialog());
            menu.Items.Add("📂 打开接收文件夹", null, (s, ev) => OpenReceivedFolder());

            menu.Show(_btnAvatar, new Point(_btnAvatar.Width + 4, 0));
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new WeChatDarkMenuRenderer();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.BackColor = Color.FromArgb(44, 44, 46);
            menu.ForeColor = Color.FromArgb(235, 235, 235);

            menu.Items.Add("✏️ 修改我的昵称...", null, (s, ev) => ShowChangeNickDialog());
            var autoItem = new ToolStripMenuItem("⚡ 自动接收文件") { Checked = _netService != null && _netService.AutoAcceptFiles };
            autoItem.Click += (s, ev) => {
                _netService.AutoAcceptFiles = !_netService.AutoAcceptFiles;
                _lblAutoAccept.Text = _netService.AutoAcceptFiles ? "⚡ 自动接收: 开" : "⏸ 自动接收: 关";
                _lblAutoAccept.ForeColor = _netService.AutoAcceptFiles ? Color.FromArgb(7, 193, 96) : Color.FromArgb(140, 140, 140);
            };
            menu.Items.Add(autoItem);
            menu.Items.Add("📁 打开接收文件夹", null, (s, ev) => OpenReceivedFolder());
            menu.Items.Add("🧹 清空当前聊天记录", null, (s, ev) => ClearCurrentChat());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("🔄 检查新版本...", null, (s, ev) => CheckForUpdates(false));
            menu.Items.Add("ℹ️ 关于 LanChat (WeChat 风格版)", null, (s, ev) => {
                MessageBox.Show(this, "LanChat v" + APP_VERSION + "\n局域网秒级极速通讯与文件互传\nUI: WeChat Desktop 风格 (高精度微距对标)\n零外部运行环境依赖\n开源主页: https://github.com/" + GITHUB_REPO, "关于 LanChat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("🚪 退出 LanChat", null, (s, ev) => {
                _isExplicitExit = true;
                this.Close();
            });

            menu.Show(_btnNavMenu, new Point(_btnNavMenu.Width + 4, -80));
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
                prompt.BackColor = Color.FromArgb(44, 44, 46);
                prompt.ForeColor = Color.White;

                Label lbl = new Label { Text = "请输入新的局域网昵称:", Location = new Point(16, 16), AutoSize = true };
                TextBox txt = new TextBox { Text = currentNick, Location = new Point(18, 42), Width = 308, BackColor = Color.FromArgb(32, 32, 34), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
                Button btnOk = new Button { Text = "保存", Location = new Point(156, 78), Size = new Size(80, 28), DialogResult = DialogResult.OK, BackColor = Color.FromArgb(7, 193, 96), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                Button btnCancel = new Button { Text = "取消", Location = new Point(246, 78), Size = new Size(80, 28), DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(60, 60, 62), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };

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
