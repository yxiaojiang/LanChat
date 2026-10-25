using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// Grok Bot 100% 像素级对标原生全局设置窗口 (完全对标 Alis4.png ~ Alis7.png)
    /// 包含左侧侧边栏导航 (通用/电脑/更新/关于)、右侧现代圆角卡片流与原生暗黑开关控制
    /// </summary>
    public class GrokSettingsDialog : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private readonly NetworkService _netService;
        private readonly Action _onCheckUpdate;
        private readonly Action _onShowChangeNick;
        private readonly Action _onOpenFolder;

        private int _activeTab = 0; // 0: 通用, 1: 电脑, 2: 更新, 3: 关于
        private int _hoverTab = -1;
        private bool _isCloseHover = false;
        private Panel _contentPanel;

        public GrokSettingsDialog(
            NetworkService netService,
            Action onCheckUpdate,
            Action onShowChangeNick,
            Action onOpenFolder,
            int initialTab = 0)
        {
            _netService = netService;
            _onCheckUpdate = onCheckUpdate;
            _onShowChangeNick = onShowChangeNick;
            _onOpenFolder = onOpenFolder;
            _activeTab = initialTab;

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(820, 580);
            this.BackColor = Color.FromArgb(20, 20, 22); // #141416
            this.DoubleBuffered = true;
            this.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular);

            BuildLayout();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                IntPtr rgn = CreateRoundRectRgn(0, 0, this.Width + 1, this.Height + 1, 16, 16);
                if (rgn != IntPtr.Zero)
                {
                    this.Region = Region.FromHrgn(rgn);
                }
            }
            catch { }
        }

        private void BuildLayout()
        {
            _contentPanel = new Panel
            {
                Location = new Point(220, 56),
                Size = new Size(576, 500),
                AutoScroll = true,
                BackColor = Color.Transparent
            };
            this.Controls.Add(_contentPanel);

            RenderCurrentTab();
        }

        private void RenderCurrentTab()
        {
            _contentPanel.Controls.Clear();

            switch (_activeTab)
            {
                case 0:
                    BuildGeneralTab();
                    break;
                case 1:
                    BuildComputerTab();
                    break;
                case 2:
                    BuildUpdateTab();
                    break;
                case 3:
                    BuildAboutTab();
                    break;
            }
            Invalidate();
        }

        #region Tab 0: 通用 (对标 Alis4.png)

        private void BuildGeneralTab()
        {
            int cardW = 550;
            int curY = 10;

            // 1. 账户卡片 (我的昵称与身份)
            Panel cardUser = CreateCardPanel(cardW, 78);
            cardUser.Location = new Point(0, curY);

            // 头像徽章
            int avSize = 46;
            Rectangle avRect = new Rectangle(16, (78 - avSize) / 2, avSize, avSize);
            cardUser.Paint += (s, e) =>
            {
                WeChatIcon.DrawMascotAvatarShape(e.Graphics, avRect, 0, Color.FromArgb(252, 44, 62), true, Color.FromArgb(26, 26, 30));
                using (Font nf = new Font("Microsoft YaHei", 11f, FontStyle.Bold))
                using (Font sf = new Font("Microsoft YaHei", 9f, FontStyle.Regular))
                using (SolidBrush nb = new SolidBrush(Color.FromArgb(242, 242, 246)))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(150, 150, 156)))
                {
                    string nick = _netService != null ? _netService.NickName : Environment.UserName;
                    e.Graphics.DrawString(nick, nf, nb, new PointF(72, 18));
                    e.Graphics.DrawString("主机: " + Environment.MachineName + " · 局域网在线", sf, sb, new PointF(72, 42));
                }
            };

            // "修改" 药丸按钮
            Button btnEditNick = CreateCardActionButton("修改昵称", 86, 32);
            btnEditNick.Location = new Point(cardW - 98, (78 - 32) / 2);
            btnEditNick.Click += (s, e) =>
            {
                if (_onShowChangeNick != null)
                {
                    _onShowChangeNick();
                    cardUser.Invalidate();
                }
            };
            cardUser.Controls.Add(btnEditNick);
            _contentPanel.Controls.Add(cardUser);

            curY += 92;

            // 2. 外观卡片 (对标 Alis4.png 下方外观大卡片)
            Panel cardTheme = CreateCardPanel(cardW, 140);
            cardTheme.Location = new Point(0, curY);
            cardTheme.Paint += (s, e) =>
            {
                using (Font f = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular))
                using (Font vf = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(220, 220, 225)))
                using (SolidBrush vb = new SolidBrush(Color.FromArgb(170, 170, 178)))
                using (Pen sepPen = new Pen(Color.FromArgb(38, 38, 44), 1f))
                {
                    // Row 1: 主题
                    e.Graphics.DrawString("主题模式", f, tb, new PointF(20, 14));
                    e.Graphics.DrawString("深色 (Grok Dark)  ›", vf, vb, new PointF(cardW - 160, 14));
                    e.Graphics.DrawLine(sepPen, 20, 46, cardW - 20, 46);

                    // Row 2: 强调色
                    e.Graphics.DrawString("强调色", f, tb, new PointF(20, 58));
                    e.Graphics.DrawString("魅紫 (#7946D7)  ›", vf, vb, new PointF(cardW - 160, 58));
                    e.Graphics.DrawLine(sepPen, 20, 92, cardW - 20, 92);

                    // Row 3: 界面语言
                    e.Graphics.DrawString("界面语言", f, tb, new PointF(20, 104));
                    e.Graphics.DrawString("跟随系统 (简体中文)  ›", vf, vb, new PointF(cardW - 190, 104));
                }
            };
            _contentPanel.Controls.Add(cardTheme);

            curY += 154;

            // 3. 反馈快捷卡片
            Panel cardFk = CreateCardPanel(cardW, 64);
            cardFk.Location = new Point(0, curY);
            cardFk.Paint += (s, e) =>
            {
                using (Font f = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(220, 220, 225)))
                {
                    e.Graphics.DrawString("意见与问题反馈", f, tb, new PointF(20, 22));
                }
            };
            Button btnOpenFk = CreateCardActionButton("发送反馈", 86, 32);
            btnOpenFk.Location = new Point(cardW - 98, (64 - 32) / 2);
            btnOpenFk.Click += (s, e) =>
            {
                using (GrokFeedbackForm fk = new GrokFeedbackForm())
                {
                    fk.ShowDialog(this);
                }
            };
            cardFk.Controls.Add(btnOpenFk);
            _contentPanel.Controls.Add(cardFk);
        }

        #endregion

        #region Tab 1: 电脑 / 网络 (对标 Alis5.png)

        private void BuildComputerTab()
        {
            int cardW = 550;
            int curY = 10;

            // 1. 本机网络设备信息卡片 (对标 Alis5.png 管理设备)
            Panel cardNet = CreateCardPanel(cardW, 90);
            cardNet.Location = new Point(0, curY);
            cardNet.Paint += (s, e) =>
            {
                Rectangle iconBox = new Rectangle(18, 22, 44, 44);
                using (SolidBrush bb = new SolidBrush(Color.FromArgb(36, 36, 42)))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, bb, iconBox, 10);
                }
                WeChatIcon.Draw(e.Graphics, WeChatIconType.Contacts, new Rectangle(28, 32, 24, 24), false, Color.FromArgb(144, 90, 251));

                string ipStr = _netService != null ? _netService.GetPrimaryPhysicalIp().ToString() : "127.0.0.1";
                int port = _netService != null ? _netService.LocalTcpPort : 38889;

                using (Font nf = new Font("Microsoft YaHei", 10.5f, FontStyle.Bold))
                using (Font sf = new Font("Microsoft YaHei", 9f, FontStyle.Regular))
                using (SolidBrush nb = new SolidBrush(Color.FromArgb(242, 242, 246)))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(150, 150, 156)))
                {
                    e.Graphics.DrawString(Environment.MachineName, nf, nb, new PointF(74, 22));
                    e.Graphics.DrawString("IP 绑定: " + ipStr + ":" + port + "  ·  物理网卡 · 在线", sf, sb, new PointF(74, 46));
                }
            };
            _contentPanel.Controls.Add(cardNet);

            curY += 104;

            // 2. 文件传输与自动接收开关卡片
            Panel cardFile = CreateCardPanel(cardW, 116);
            cardFile.Location = new Point(0, curY);
            bool autoAccept = _netService != null && _netService.AutoAcceptFiles;
            string saveDir = _netService != null ? _netService.SaveDirectory : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReceivedFiles");

            cardFile.Paint += (s, e) =>
            {
                using (Font f = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular))
                using (Font sf = new Font("Microsoft YaHei", 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(220, 220, 225)))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(140, 140, 146)))
                using (Pen sep = new Pen(Color.FromArgb(38, 38, 44), 1f))
                {
                    // Row 1: 自动接收
                    e.Graphics.DrawString("自动接收好友文件", f, tb, new PointF(20, 14));
                    e.Graphics.DrawString("局域网接收文件时无需手动每次确认", sf, sb, new PointF(20, 34));
                    e.Graphics.DrawLine(sep, 20, 56, cardW - 20, 56);

                    // Row 2: 存储目录
                    e.Graphics.DrawString("文件保存目录", f, tb, new PointF(20, 68));
                    string shortDir = saveDir.Length > 36 ? "..." + saveDir.Substring(saveDir.Length - 33) : saveDir;
                    e.Graphics.DrawString(shortDir, sf, sb, new PointF(20, 88));
                }
            };

            // 自动接收开关按钮 (药丸切换)
            Button btnAutoAccept = CreateCardActionButton(autoAccept ? "已开启" : "已关闭", 78, 30);
            btnAutoAccept.Location = new Point(cardW - 90, 16);
            btnAutoAccept.Click += (s, e) =>
            {
                if (_netService != null)
                {
                    _netService.AutoAcceptFiles = !_netService.AutoAcceptFiles;
                    btnAutoAccept.Text = _netService.AutoAcceptFiles ? "已开启" : "已关闭";
                    btnAutoAccept.Invalidate();
                    cardFile.Invalidate();
                }
            };
            cardFile.Controls.Add(btnAutoAccept);

            // 打开接收目录按钮
            Button btnOpenDir = CreateCardActionButton("打开目录", 78, 30);
            btnOpenDir.Location = new Point(cardW - 90, 70);
            btnOpenDir.Click += (s, e) =>
            {
                if (_onOpenFolder != null) _onOpenFolder();
            };
            cardFile.Controls.Add(btnOpenDir);

            _contentPanel.Controls.Add(cardFile);
        }

        #endregion

        #region Tab 2: 更新 (对标 Alis7.png)

        private void BuildUpdateTab()
        {
            int cardW = 550;
            int curY = 10;

            // 对标 Alis7.png 经典更新卡片
            Panel cardUpdate = CreateCardPanel(cardW, 110);
            cardUpdate.Location = new Point(0, curY);

            cardUpdate.Paint += (s, e) =>
            {
                using (Font f = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular))
                using (Font sf = new Font("Microsoft YaHei", 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(220, 220, 225)))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(140, 140, 146)))
                using (Pen sep = new Pen(Color.FromArgb(38, 38, 44), 1f))
                {
                    // Row 1: 自动检查更新
                    e.Graphics.DrawString("自动检查更新", f, tb, new PointF(20, 14));
                    e.Graphics.DrawString("在程序启动与网络空闲时检测 GitHub 最新 Release", sf, sb, new PointF(20, 34));
                    e.Graphics.DrawLine(sep, 20, 56, cardW - 20, 56);

                    // Row 2: 版本状态
                    e.Graphics.DrawString("版本 " + MainForm.APP_VERSION, f, tb, new PointF(20, 68));
                    e.Graphics.DrawString("当前版本运行良好，已包含全套 Grok Bot 视觉对齐", sf, sb, new PointF(20, 88));
                }
            };

            // 开关
            Button btnAutoSw = CreateCardActionButton("已开启", 78, 30);
            btnAutoSw.Location = new Point(cardW - 90, 16);
            cardUpdate.Controls.Add(btnAutoSw);

            // 检查更新按钮
            Button btnCheck = CreateCardActionButton("检查更新", 84, 30);
            btnCheck.Location = new Point(cardW - 96, 68);
            btnCheck.Click += (s, e) =>
            {
                if (_onCheckUpdate != null) _onCheckUpdate();
            };
            cardUpdate.Controls.Add(btnCheck);

            _contentPanel.Controls.Add(cardUpdate);
        }

        #endregion

        #region Tab 3: 关于 (对标 About.png)

        private void BuildAboutTab()
        {
            int cardW = 550;

            Panel cardAbout = CreateCardPanel(cardW, 260);
            cardAbout.Location = new Point(0, 10);

            cardAbout.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 居中 Squircle 吉祥物徽章
                int bSize = 72;
                int bx = (cardW - bSize) / 2;
                int by = 26;
                Rectangle bRect = new Rectangle(bx, by, bSize, bSize);

                using (SolidBrush bb = new SolidBrush(Color.FromArgb(38, 38, 42)))
                using (Pen bp = new Pen(Color.FromArgb(54, 54, 60), 1f))
                {
                    MainForm.DrawRoundedRectangle(g, bb, bRect, 20);
                    MainForm.DrawRoundedRectangle(g, bp, bRect, 20);
                }

                Rectangle mRect = new Rectangle(bx + 10, by + 10, 52, 52);
                WeChatIcon.DrawMascotAvatarShape(g, mRect, 0, Color.FromArgb(240, 240, 245), false, Color.Transparent);

                // 文字说明
                using (Font tf = new Font("Microsoft YaHei", 14f, FontStyle.Bold))
                using (Font sf = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(245, 245, 250)))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(160, 160, 168)))
                {
                    StringFormat csf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("LanChat", tf, tb, new RectangleF(0, by + bSize + 12, cardW, 26), csf);
                    g.DrawString("版本 " + MainForm.APP_VERSION + " · 构建于 2026年10月25日", sf, sb, new RectangleF(0, by + bSize + 40, cardW, 22), csf);
                    g.DrawString("版权所有 © 2026 Aritex LanChat · 100% Grok Bot 视觉对标", sf, sb, new RectangleF(0, by + bSize + 62, cardW, 22), csf);
                }
            };

            // 复制信息按钮
            Button btnCopy = CreateCardActionButton("复制版本信息", 120, 34);
            btnCopy.Location = new Point((cardW - 120) / 2, 196);
            btnCopy.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText("LanChat v" + MainForm.APP_VERSION + "\n局域网秒级通讯与文件传输\nUI: 100% 对标 Grok Bot");
                    btnCopy.Text = "已复制 ✓";
                }
                catch { }
            };
            cardAbout.Controls.Add(btnCopy);

            _contentPanel.Controls.Add(cardAbout);
        }

        #endregion

        #region UI Helpers

        private Panel CreateCardPanel(int w, int h)
        {
            Panel p = new Panel
            {
                Size = new Size(w, h),
                BackColor = Color.Transparent
            };
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, w - 1, h - 1);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(26, 26, 30)))
                using (Pen pen = new Pen(Color.FromArgb(42, 42, 48), 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 14);
                    MainForm.DrawRoundedRectangle(e.Graphics, pen, r, 14);
                }
            };
            return p;
        }

        private Button CreateCardActionButton(string text, int w, int h)
        {
            Button btn = new Button
            {
                Text = text,
                Size = new Size(w, h),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei", 9.0f, FontStyle.Bold)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;

            bool hover = false;
            btn.MouseEnter += (s, e) => { hover = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { hover = false; btn.Invalidate(); };

            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (SolidBrush b = new SolidBrush(hover ? Color.FromArgb(50, 50, 56) : Color.FromArgb(36, 36, 40)))
                using (Pen p = new Pen(Color.FromArgb(54, 54, 60), 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 15);
                    MainForm.DrawRoundedRectangle(e.Graphics, p, r, 15);
                }
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(225, 225, 230)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(btn.Text, btn.Font, tb, new RectangleF(0, 1, btn.Width, btn.Height), sf);
                }
            };
            return btn;
        }

        #endregion

        #region Navigation & Window Events

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                // 关闭按钮
                Rectangle closeRect = new Rectangle(this.Width - 38, 16, 22, 22);
                if (closeRect.Contains(e.Location))
                {
                    this.Close();
                    return;
                }

                // 检查左侧导航选项卡点击 (width 200)
                if (e.X < 200 && e.Y > 50)
                {
                    int itemH = 44;
                    int startY = 60;
                    for (int i = 0; i < 4; i++)
                    {
                        Rectangle itemR = new Rectangle(14, startY + i * itemH, 186, itemH - 4);
                        if (itemR.Contains(e.Location))
                        {
                            _activeTab = i;
                            RenderCurrentTab();
                            return;
                        }
                    }
                }

                // 拖拽窗口
                if (e.Y < 48)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, 0x00A1, (IntPtr)0x0002, IntPtr.Zero);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Rectangle closeRect = new Rectangle(this.Width - 38, 16, 22, 22);
            bool cHover = closeRect.Contains(e.Location);
            if (cHover != _isCloseHover)
            {
                _isCloseHover = cHover;
                Invalidate(closeRect);
            }

            int oldTab = _hoverTab;
            _hoverTab = -1;
            if (e.X < 200 && e.Y > 50)
            {
                int itemH = 44;
                int startY = 60;
                for (int i = 0; i < 4; i++)
                {
                    Rectangle itemR = new Rectangle(14, startY + i * itemH, 186, itemH - 4);
                    if (itemR.Contains(e.Location))
                    {
                        _hoverTab = i;
                        break;
                    }
                }
            }
            if (_hoverTab != oldTab)
            {
                Invalidate(new Rectangle(10, 50, 195, 200));
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. 背景与外边框
            using (Pen borderPen = new Pen(Color.FromArgb(46, 46, 52), 1f))
            {
                MainForm.DrawRoundedRectangle(g, borderPen, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 16);
            }

            // 2. 左侧侧边栏竖向分割线 (x = 206)
            using (Pen divPen = new Pen(Color.FromArgb(34, 34, 38), 1f))
            {
                g.DrawLine(divPen, 206, 0, 206, this.Height);
            }

            // 3. 左侧导航选项卡
            string[] tabNames = new string[] { "通用", "电脑", "更新", "关于" };
            WeChatIconType[] tabIcons = new WeChatIconType[] { WeChatIconType.Settings, WeChatIconType.Contacts, WeChatIconType.Refresh, WeChatIconType.User };
            int startY = 60;
            int itemH = 44;

            using (Font tabFont = new Font("Microsoft YaHei", 10.0f, FontStyle.Regular))
            {
                for (int i = 0; i < tabNames.Length; i++)
                {
                    int iy = startY + i * itemH;
                    Rectangle itemR = new Rectangle(14, iy, 182, itemH - 4);

                    if (_activeTab == i)
                    {
                        // 激活态圆角背景
                        using (SolidBrush ab = new SolidBrush(Color.FromArgb(38, 38, 44)))
                        {
                            MainForm.DrawRoundedRectangle(g, ab, itemR, 8);
                        }
                    }
                    else if (_hoverTab == i)
                    {
                        using (SolidBrush hb = new SolidBrush(Color.FromArgb(28, 28, 32)))
                        {
                            MainForm.DrawRoundedRectangle(g, hb, itemR, 8);
                        }
                    }

                    // 图标与文字
                    Rectangle iconR = new Rectangle(28, iy + (itemH - 4 - 18) / 2, 18, 18);
                    Color ic = (_activeTab == i) ? Color.White : Color.FromArgb(170, 170, 178);
                    WeChatIcon.Draw(g, tabIcons[i], iconR, false, ic);

                    Color tc = (_activeTab == i) ? Color.White : Color.FromArgb(190, 190, 198);
                    using (SolidBrush tb = new SolidBrush(tc))
                    {
                        g.DrawString(tabNames[i], tabFont, tb, new PointF(58, iy + (itemH - 4 - tabFont.GetHeight(g)) / 2f));
                    }
                }
            }

            // 4. 右侧顶部标题
            string[] titles = new string[] { "通用", "电脑与网络", "软件更新", "关于 LanChat" };
            using (Font titleFont = new Font("Microsoft YaHei", 14f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(245, 245, 250)))
            {
                g.DrawString(titles[_activeTab], titleFont, tb, new PointF(220, 18));
            }

            // 5. 右上角极简关闭叉叉 (×)
            Rectangle closeRect = new Rectangle(this.Width - 38, 16, 22, 22);
            if (_isCloseHover)
            {
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(48, 48, 54)))
                {
                    g.FillEllipse(hb, closeRect);
                }
            }
            using (Pen cp = new Pen(_isCloseHover ? Color.White : Color.FromArgb(160, 160, 166), 1.5f))
            {
                cp.StartCap = LineCap.Round;
                cp.EndCap = LineCap.Round;
                int pad = 5;
                g.DrawLine(cp, closeRect.X + pad, closeRect.Y + pad, closeRect.Right - pad, closeRect.Bottom - pad);
                g.DrawLine(cp, closeRect.Right - pad, closeRect.Y + pad, closeRect.X + pad, closeRect.Bottom - pad);
            }
        }

        #endregion
    }
}
