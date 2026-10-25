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
    /// Grok Bot 100% 像素级对齐原生全局设置窗口 (完全对标 Alis4.png ~ Alis7.png 与 Image 3 实测比对)
    /// 包含：
    /// 1. 宽屏比例 920x640，左侧 220px 纯黑侧边栏与右侧无界过渡 (无竖直粗线)
    /// 2. 18.0pt 加粗明亮大标题，10.5pt 加粗中性灰分组标签
    /// 3. 54px 舒适行高卡片流、22px 边缘内边距与 32px 药丸下拉控制组件
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

        private const int SIDEBAR_WIDTH = 220;
        private const int CARD_WIDTH = 636;

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
            this.Size = new Size(920, 640);
            this.BackColor = Color.FromArgb(18, 18, 18); // #121212 主视口纯正暗黑
            this.DoubleBuffered = true;
            this.Font = new Font("Microsoft YaHei UI", 10.0f, FontStyle.Regular);

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
                Location = new Point(252, 76),
                Size = new Size(642, 540),
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

        #region Tab 0: 通用 (完全对标 Alis4.png 与 Image 3)

        private void BuildGeneralTab()
        {
            int curY = 6;

            // 1. 分组标签: "账户" (10.5pt 加粗 #8E8E93)
            Label lblUser = CreateSectionHeader("账户", curY);
            _contentPanel.Controls.Add(lblUser);
            curY += 28;

            // 账户信息大卡片 (高 82px)
            Panel cardUser = CreateCardPanel(CARD_WIDTH, 82);
            cardUser.Location = new Point(0, curY);

            int avSize = 48;
            Rectangle avRect = new Rectangle(20, (82 - avSize) / 2, avSize, avSize);
            cardUser.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // 头像绘制
                WeChatIcon.DrawMascotAvatarShape(g, avRect, 0, Color.FromArgb(252, 44, 62), true, Color.FromArgb(28, 28, 31));

                using (Font nf = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold))
                using (Font sf = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Regular))
                using (SolidBrush nb = new SolidBrush(Color.FromArgb(245, 245, 247)))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(142, 142, 147)))
                {
                    string nick = _netService != null ? _netService.NickName : Environment.UserName;
                    g.DrawString(nick, nf, nb, new PointF(80, 20));
                    g.DrawString("主机: " + Environment.MachineName + " · 局域网在线", sf, sb, new PointF(80, 46));
                }
            };

            // "修改昵称" 药丸按钮 (高 32px)
            Button btnEditNick = CreateCardActionButton("修改昵称", 96, 32);
            btnEditNick.Location = new Point(CARD_WIDTH - 118, (82 - 32) / 2);
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

            curY += 100;

            // 2. 分组标签: "外观与偏好"
            Label lblTheme = CreateSectionHeader("外观与偏好", curY);
            _contentPanel.Controls.Add(lblTheme);
            curY += 28;

            // 外观大卡片 (3 行，每行 54px，总高 164px)
            Panel cardTheme = CreateCardPanel(CARD_WIDTH, 164);
            cardTheme.Location = new Point(0, curY);
            cardTheme.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Font f = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(240, 240, 244)))
                using (Pen sepPen = new Pen(Color.FromArgb(38, 38, 43), 1f))
                {
                    // Row 1 (y = 0..54)
                    g.DrawString("主题模式", f, tb, new PointF(22, (54 - f.GetHeight(g)) / 2f));
                    g.DrawLine(sepPen, 22, 54, CARD_WIDTH - 22, 54);

                    // Row 2 (y = 54..108)
                    g.DrawString("强调色", f, tb, new PointF(22, 54 + (54 - f.GetHeight(g)) / 2f));
                    g.DrawLine(sepPen, 22, 108, CARD_WIDTH - 22, 108);

                    // Row 3 (y = 108..162)
                    g.DrawString("界面语言", f, tb, new PointF(22, 108 + (54 - f.GetHeight(g)) / 2f));
                }
            };

            // Row 1 右侧药丸胶囊: 深色 (Grok Dark)
            Button btnThemePill = CreateDropdownPill("深色 (Grok Dark)  ∨", 160, 32);
            btnThemePill.Location = new Point(CARD_WIDTH - 182, (54 - 32) / 2);
            cardTheme.Controls.Add(btnThemePill);

            // Row 2 右侧药丸胶囊: 魅紫 (#8E64DC)
            Button btnAccentPill = CreateDropdownPill("魅紫 (#8E64DC)  ∨", 160, 32);
            btnAccentPill.Location = new Point(CARD_WIDTH - 182, 54 + (54 - 32) / 2);
            cardTheme.Controls.Add(btnAccentPill);

            // Row 3 右侧药丸胶囊: 跟随系统 (简体中文)
            Button btnLangPill = CreateDropdownPill("跟随系统 (简体中文)  ∨", 190, 32);
            btnLangPill.Location = new Point(CARD_WIDTH - 212, 108 + (54 - 32) / 2);
            cardTheme.Controls.Add(btnLangPill);

            _contentPanel.Controls.Add(cardTheme);

            curY += 182;

            // 3. 分组标签: "支持与反馈"
            Label lblFk = CreateSectionHeader("支持与反馈", curY);
            _contentPanel.Controls.Add(lblFk);
            curY += 28;

            // 反馈快捷卡片 (高 72px)
            Panel cardFk = CreateCardPanel(CARD_WIDTH, 72);
            cardFk.Location = new Point(0, curY);
            cardFk.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Font f = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Regular))
                using (Font sf = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(240, 240, 244)))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(142, 142, 147)))
                {
                    g.DrawString("意见与问题反馈", f, tb, new PointF(22, 17));
                    g.DrawString("帮助我们将 LanChat 打磨得更贴合你的直觉与习惯", sf, sb, new PointF(22, 41));
                }
            };
            Button btnOpenFk = CreateCardActionButton("发送反馈", 96, 32);
            btnOpenFk.Location = new Point(CARD_WIDTH - 118, (72 - 32) / 2);
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
            int curY = 6;

            Label lblDev = CreateSectionHeader("网络设备", curY);
            _contentPanel.Controls.Add(lblDev);
            curY += 28;

            // 本机设备卡片 (高 88px)
            Panel cardNet = CreateCardPanel(CARD_WIDTH, 88);
            cardNet.Location = new Point(0, curY);
            cardNet.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Rectangle iconBox = new Rectangle(20, (88 - 46) / 2, 46, 46);
                using (SolidBrush bb = new SolidBrush(Color.FromArgb(36, 36, 42)))
                {
                    MainForm.DrawRoundedRectangle(g, bb, iconBox, 12);
                }
                WeChatIcon.Draw(g, WeChatIconType.Contacts, new Rectangle(iconBox.X + 11, iconBox.Y + 11, 24, 24), false, Color.FromArgb(142, 100, 220));

                string ipStr = _netService != null ? _netService.GetPrimaryPhysicalIp().ToString() : "127.0.0.1";
                int port = _netService != null ? _netService.LocalTcpPort : 38889;

                using (Font nf = new Font("Microsoft YaHei UI", 11.0f, FontStyle.Bold))
                using (Font sf = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Regular))
                using (SolidBrush nb = new SolidBrush(Color.FromArgb(245, 245, 247)))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(142, 142, 147)))
                {
                    g.DrawString(Environment.MachineName, nf, nb, new PointF(80, 22));
                    g.DrawString("IP 绑定: " + ipStr + ":" + port + "  ·  物理以太网/Wi-Fi", sf, sb, new PointF(80, 48));
                }
            };

            // 在线状态药丸
            Button btnStatusPill = CreateDropdownPill("● 局域网在线", 120, 32);
            btnStatusPill.Location = new Point(CARD_WIDTH - 142, (88 - 32) / 2);
            cardNet.Controls.Add(btnStatusPill);
            _contentPanel.Controls.Add(cardNet);

            curY += 106;

            Label lblPref = CreateSectionHeader("文件传输偏好", curY);
            _contentPanel.Controls.Add(lblPref);
            curY += 28;

            // 文件传输大卡片 (2 行，每行 54px，总高 112px)
            Panel cardFile = CreateCardPanel(CARD_WIDTH, 112);
            cardFile.Location = new Point(0, curY);
            bool autoAccept = _netService != null && _netService.AutoAcceptFiles;
            string saveDir = _netService != null ? _netService.SaveDirectory : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReceivedFiles");

            cardFile.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Font f = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Regular))
                using (Font sf = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(240, 240, 244)))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(142, 142, 147)))
                using (Pen sep = new Pen(Color.FromArgb(38, 38, 43), 1f))
                {
                    // Row 1: 自动接收好友文件
                    g.DrawString("自动接收好友文件", f, tb, new PointF(22, 10));
                    g.DrawString("局域网内接收文件时无需手动逐一确认保存", sf, sb, new PointF(22, 32));
                    g.DrawLine(sep, 22, 56, CARD_WIDTH - 22, 56);

                    // Row 2: 存储目录
                    g.DrawString("文件默认存储目录", f, tb, new PointF(22, 66));
                    string shortDir = saveDir.Length > 44 ? "..." + saveDir.Substring(saveDir.Length - 41) : saveDir;
                    g.DrawString(shortDir, sf, sb, new PointF(22, 88));
                }
            };

            // 自动接收开关按钮 (药丸切换)
            Button btnAutoAccept = CreateCardActionButton(autoAccept ? "已开启" : "已关闭", 84, 32);
            btnAutoAccept.Location = new Point(CARD_WIDTH - 106, (56 - 32) / 2);
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
            Button btnOpenDir = CreateCardActionButton("打开文件夹", 96, 32);
            btnOpenDir.Location = new Point(CARD_WIDTH - 118, 56 + (56 - 32) / 2);
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
            int curY = 6;

            Label lblBanner = CreateSectionHeader("更新状态", curY);
            _contentPanel.Controls.Add(lblBanner);
            curY += 28;

            // 顶部版本横幅卡片 (对标 Alis7.png 经典更新卡片，深蓝微光底色)
            Panel cardBanner = CreateCardPanel(CARD_WIDTH, 84);
            cardBanner.Location = new Point(0, curY);
            cardBanner.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Font tf = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold))
                using (Font sf = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(245, 245, 250)))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(142, 142, 147)))
                {
                    g.DrawString("LanChat v" + MainForm.APP_VERSION, tf, tb, new PointF(22, 18));
                    g.DrawString("当前版本运行良好，已完整对齐 Grok Bot 全套暗黑规范", sf, sb, new PointF(22, 46));
                }
            };

            Button btnCheckNow = CreateCardActionButton("检查新版本", 106, 32);
            btnCheckNow.Location = new Point(CARD_WIDTH - 128, (84 - 32) / 2);
            btnCheckNow.Click += (s, e) =>
            {
                if (_onCheckUpdate != null) _onCheckUpdate();
            };
            cardBanner.Controls.Add(btnCheckNow);
            _contentPanel.Controls.Add(cardBanner);

            curY += 102;

            Label lblChannel = CreateSectionHeader("通道与偏好", curY);
            _contentPanel.Controls.Add(lblChannel);
            curY += 28;

            // 更新通道大卡片 (2 行，每行 54px，总高 112px)
            Panel cardChannel = CreateCardPanel(CARD_WIDTH, 112);
            cardChannel.Location = new Point(0, curY);
            cardChannel.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Font f = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Regular))
                using (Font sf = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(240, 240, 244)))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(142, 142, 147)))
                using (Pen sep = new Pen(Color.FromArgb(38, 38, 43), 1f))
                {
                    g.DrawString("更新通道", f, tb, new PointF(22, 10));
                    g.DrawString("接收经过全面测试与实机验证的正式发布版本", sf, sb, new PointF(22, 32));
                    g.DrawLine(sep, 22, 56, CARD_WIDTH - 22, 56);

                    g.DrawString("自动检查更新", f, tb, new PointF(22, 66));
                    g.DrawString("程序启动与网络空闲时检测 GitHub Release", sf, sb, new PointF(22, 88));
                }
            };

            Button btnChannelPill = CreateDropdownPill("正式稳定版 (Stable)  ∨", 175, 32);
            btnChannelPill.Location = new Point(CARD_WIDTH - 197, (56 - 32) / 2);
            cardChannel.Controls.Add(btnChannelPill);

            Button btnAutoSw = CreateCardActionButton("已开启", 84, 32);
            btnAutoSw.Location = new Point(CARD_WIDTH - 106, 56 + (56 - 32) / 2);
            cardChannel.Controls.Add(btnAutoSw);

            _contentPanel.Controls.Add(cardChannel);
        }

        #endregion

        #region Tab 3: 关于 (对标 About.png)

        private void BuildAboutTab()
        {
            int curY = 6;

            Label lblAbout = CreateSectionHeader("关于产品", curY);
            _contentPanel.Controls.Add(lblAbout);
            curY += 28;

            Panel cardAbout = CreateCardPanel(CARD_WIDTH, 340);
            cardAbout.Location = new Point(0, curY);

            cardAbout.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // 居中 Squircle 吉祥物徽章 (68x68)
                int bSize = 68;
                int bx = (CARD_WIDTH - bSize) / 2;
                int by = 24;
                Rectangle bRect = new Rectangle(bx, by, bSize, bSize);

                using (SolidBrush bb = new SolidBrush(Color.FromArgb(36, 36, 42)))
                using (Pen bp = new Pen(Color.FromArgb(52, 52, 60), 1f))
                {
                    MainForm.DrawRoundedRectangle(g, bb, bRect, 18);
                    MainForm.DrawRoundedRectangle(g, bp, bRect, 18);
                }

                Rectangle mRect = new Rectangle(bx + 9, by + 9, 50, 50);
                WeChatIcon.DrawMascotAvatarShape(g, mRect, 0, Color.FromArgb(245, 245, 250), false, Color.Transparent);

                // 文字说明
                using (Font tf = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold))
                using (Font mf = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular))
                using (Font sf = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(250, 250, 255)))
                using (SolidBrush mb = new SolidBrush(Color.FromArgb(170, 170, 178)))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(130, 130, 138)))
                {
                    StringFormat csf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("LanChat", tf, tb, new RectangleF(0, by + bSize + 12, CARD_WIDTH, 26), csf);
                    g.DrawString("版本 " + MainForm.APP_VERSION + " · Grok Bot 100% 视觉对标标准", mf, mb, new RectangleF(0, by + bSize + 40, CARD_WIDTH, 22), csf);
                    g.DrawString("局域网秒级多点通讯 · 原生高性能零依赖架构", sf, sb, new RectangleF(0, by + bSize + 64, CARD_WIDTH, 20), csf);
                    g.DrawString("版权所有 © 2026 Aritex LanChat Team. All rights reserved.", sf, sb, new RectangleF(0, by + bSize + 84, CARD_WIDTH, 20), csf);
                }
            };

            // 操作药丸按钮
            Button btnCheck = CreateCardActionButton("检查新版本", 116, 34);
            btnCheck.Location = new Point(CARD_WIDTH / 2 - 126, 260);
            btnCheck.Click += (s, e) =>
            {
                if (_onCheckUpdate != null) _onCheckUpdate();
            };
            cardAbout.Controls.Add(btnCheck);

            Button btnCopy = CreateCardActionButton("复制诊断信息", 116, 34);
            btnCopy.Location = new Point(CARD_WIDTH / 2 + 10, 260);
            btnCopy.Click += (s, e) =>
            {
                try
                {
                    string info = string.Format("LanChat v{0}\nOS: {1}\nMachine: {2}\nCLR: {3}\nDesign: GrokBot UI System v2.1",
                        MainForm.APP_VERSION, Environment.OSVersion, Environment.MachineName, Environment.Version);
                    Clipboard.SetText(info);
                    btnCopy.Text = "已复制 ✓";
                }
                catch { }
            };
            cardAbout.Controls.Add(btnCopy);

            _contentPanel.Controls.Add(cardAbout);
        }

        #endregion

        #region UI Component Helpers

        private Label CreateSectionHeader(string text, int curY)
        {
            Label lbl = new Label
            {
                Text = text,
                Location = new Point(2, curY),
                Size = new Size(300, 22),
                Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(142, 142, 147), // #8E8E93
                BackColor = Color.Transparent
            };
            return lbl;
        }

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
                using (SolidBrush b = new SolidBrush(Color.FromArgb(28, 28, 31))) // #1C1C1F
                using (Pen pen = new Pen(Color.FromArgb(44, 44, 50), 1f))        // #2C2C32
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 16);
                    MainForm.DrawRoundedRectangle(e.Graphics, pen, r, 16);
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
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;

            bool hover = false;
            btn.MouseEnter += (s, e) => { hover = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { hover = false; btn.Invalidate(); };

            btn.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                Rectangle r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (SolidBrush b = new SolidBrush(hover ? Color.FromArgb(56, 56, 64) : Color.FromArgb(42, 42, 48)))
                using (Pen p = new Pen(Color.FromArgb(60, 60, 68), 1f))
                {
                    MainForm.DrawRoundedRectangle(g, b, r, 16);
                    MainForm.DrawRoundedRectangle(g, p, r, 16);
                }
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(242, 242, 246)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(btn.Text, btn.Font, tb, new RectangleF(0, 0, btn.Width, btn.Height), sf);
                }
            };
            return btn;
        }

        private Button CreateDropdownPill(string text, int w, int h)
        {
            Button btn = new Button
            {
                Text = text,
                Size = new Size(w, h),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;

            bool hover = false;
            btn.MouseEnter += (s, e) => { hover = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { hover = false; btn.Invalidate(); };

            btn.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                Rectangle r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (SolidBrush b = new SolidBrush(hover ? Color.FromArgb(46, 46, 52) : Color.FromArgb(36, 36, 40)))
                using (Pen p = new Pen(Color.FromArgb(50, 50, 56), 1f))
                {
                    MainForm.DrawRoundedRectangle(g, b, r, 16);
                    MainForm.DrawRoundedRectangle(g, p, r, 16);
                }
                using (SolidBrush tb = new SolidBrush(hover ? Color.White : Color.FromArgb(224, 224, 229)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(btn.Text, btn.Font, tb, new RectangleF(0, 0, btn.Width, btn.Height), sf);
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
                Rectangle closeRect = new Rectangle(this.Width - 44, 20, 28, 28);
                if (closeRect.Contains(e.Location))
                {
                    this.Close();
                    return;
                }

                // 检查左侧导航选项卡点击 (width 220)
                if (e.X < SIDEBAR_WIDTH && e.Y > 60)
                {
                    int itemH = 46;
                    int startY = 72;
                    for (int i = 0; i < 4; i++)
                    {
                        Rectangle itemR = new Rectangle(16, startY + i * itemH, 188, 42);
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
            Rectangle closeRect = new Rectangle(this.Width - 44, 20, 28, 28);
            bool cHover = closeRect.Contains(e.Location);
            if (cHover != _isCloseHover)
            {
                _isCloseHover = cHover;
                Invalidate(closeRect);
            }

            int oldTab = _hoverTab;
            _hoverTab = -1;
            if (e.X < SIDEBAR_WIDTH && e.Y > 60)
            {
                int itemH = 46;
                int startY = 72;
                for (int i = 0; i < 4; i++)
                {
                    Rectangle itemR = new Rectangle(16, startY + i * itemH, 188, 42);
                    if (itemR.Contains(e.Location))
                    {
                        _hoverTab = i;
                        break;
                    }
                }
            }
            if (_hoverTab != oldTab)
            {
                Invalidate(new Rectangle(12, 60, 200, 220));
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
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 1. 左侧侧边栏纯黑底色基板 (Level 0: #0E0E0E，绝无竖向单线，纯靠底色差过渡)
            using (SolidBrush sideBrush = new SolidBrush(Color.FromArgb(14, 14, 14)))
            {
                g.FillRectangle(sideBrush, 0, 0, SIDEBAR_WIDTH, this.Height);
            }

            // 2. 外部 1px 微妙圆角描边
            using (Pen borderPen = new Pen(Color.FromArgb(44, 44, 50), 1f))
            {
                MainForm.DrawRoundedRectangle(g, borderPen, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 16);
            }

            // 3. 左侧导航选项卡
            string[] tabNames = new string[] { "通用", "电脑", "更新", "关于" };
            WeChatIconType[] tabIcons = new WeChatIconType[] { WeChatIconType.Settings, WeChatIconType.Contacts, WeChatIconType.Refresh, WeChatIconType.User };
            int startY = 72;
            int itemH = 46;

            using (Font tabFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Regular))
            using (Font tabActiveFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold))
            {
                for (int i = 0; i < tabNames.Length; i++)
                {
                    int iy = startY + i * itemH;
                    Rectangle itemR = new Rectangle(16, iy, 188, 42);

                    if (_activeTab == i)
                    {
                        // 激活态圆角背景卡片 (#242428)
                        using (SolidBrush ab = new SolidBrush(Color.FromArgb(36, 36, 40)))
                        {
                            MainForm.DrawRoundedRectangle(g, ab, itemR, 10);
                        }
                    }
                    else if (_hoverTab == i)
                    {
                        using (SolidBrush hb = new SolidBrush(Color.FromArgb(26, 26, 30)))
                        {
                            MainForm.DrawRoundedRectangle(g, hb, itemR, 10);
                        }
                    }

                    // 图标与文字
                    Rectangle iconR = new Rectangle(itemR.X + 16, iy + (42 - 18) / 2, 18, 18);
                    Color ic = (_activeTab == i) ? Color.White : (_hoverTab == i ? Color.FromArgb(200, 200, 208) : Color.FromArgb(142, 142, 147));
                    WeChatIcon.Draw(g, tabIcons[i], iconR, false, ic);

                    Color tc = (_activeTab == i) ? Color.FromArgb(245, 245, 247) : (_hoverTab == i ? Color.FromArgb(212, 212, 218) : Color.FromArgb(142, 142, 147));
                    Font tf = (_activeTab == i) ? tabActiveFont : tabFont;
                    using (SolidBrush tb = new SolidBrush(tc))
                    {
                        g.DrawString(tabNames[i], tf, tb, new PointF(itemR.X + 46, iy + (42 - tf.GetHeight(g)) / 2f));
                    }
                }
            }

            // 4. 右侧顶部大标题 (18.0pt 加粗明亮纯白 #FFFFFF，对齐 Alis4.png)
            string[] titles = new string[] { "通用", "电脑与网络", "软件更新", "关于 LanChat" };
            using (Font titleFont = new Font("Microsoft YaHei UI", 18f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.White))
            {
                g.DrawString(titles[_activeTab], titleFont, tb, new PointF(252, 28));
            }

            // 5. 右上角极简关闭叉叉 (×)
            Rectangle closeRect = new Rectangle(this.Width - 44, 20, 28, 28);
            if (_isCloseHover)
            {
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(46, 46, 52)))
                {
                    g.FillEllipse(hb, closeRect);
                }
            }
            using (Pen cp = new Pen(_isCloseHover ? Color.White : Color.FromArgb(142, 142, 147), 1.6f))
            {
                cp.StartCap = LineCap.Round;
                cp.EndCap = LineCap.Round;
                int pad = 8;
                g.DrawLine(cp, closeRect.X + pad, closeRect.Y + pad, closeRect.Right - pad, closeRect.Bottom - pad);
                g.DrawLine(cp, closeRect.Right - pad, closeRect.Y + pad, closeRect.X + pad, closeRect.Bottom - pad);
            }
        }

        #endregion
    }
}
