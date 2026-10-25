using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// Grok Bot 100% 像素级对标原生连接节点窗口 (完全对标 ConnetctApp.png)
    /// 包含顶部搜索栏、标签过滤芯片 (全部/在线/直连)、局域网推荐节点卡片流与手动 IP 握手直连
    /// </summary>
    public class GrokConnectNodeForm : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private readonly NetworkService _netService;
        private readonly Action<string> _onConnectIp;
        private readonly Action<PeerInfo> _onSelectPeer;

        private TextBox _txtSearch;
        private TextBox _txtManualIp;
        private Button _btnManualConnect;
        private Button _btnRefresh;
        private Panel _listPanel;

        private int _filterIndex = 0; // 0: 全部, 1: 在线, 2: 常用, 3: 手动
        private bool _isCloseHover = false;
        private string _searchFilter = string.Empty;

        public GrokConnectNodeForm(NetworkService netService, Action<string> onConnectIp, Action<PeerInfo> onSelectPeer)
        {
            _netService = netService;
            _onConnectIp = onConnectIp;
            _onSelectPeer = onSelectPeer;

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(760, 600);
            this.BackColor = Color.FromArgb(20, 20, 22); // #141416
            this.DoubleBuffered = true;
            this.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular);

            BuildControls();
            PopulatePeers();
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

        private void BuildControls()
        {
            // 1. 顶部搜索框
            _txtSearch = new TextBox
            {
                Location = new Point(66, 73),
                Size = new Size(490, 24),
                BackColor = Color.FromArgb(16, 16, 18),
                ForeColor = Color.FromArgb(240, 240, 245),
                Font = new Font("Microsoft YaHei", 10.0f),
                BorderStyle = BorderStyle.None
            };
            _txtSearch.TextChanged += (s, e) =>
            {
                _searchFilter = _txtSearch.Text.Trim().ToLowerInvariant();
                PopulatePeers();
            };
            this.Controls.Add(_txtSearch);

            // 搜索框右侧 "扫描刷新 ⟳" 药丸按钮 (对标 ConnetctApp.png 右侧 Bot 模板)
            _btnRefresh = new Button
            {
                Text = "⟳ 扫描节点",
                Location = new Point(610, 64),
                Size = new Size(116, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold)
            };
            _btnRefresh.FlatAppearance.BorderSize = 0;
            _btnRefresh.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnRefresh.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool refHover = false;
            _btnRefresh.MouseEnter += (s, e) => { refHover = true; _btnRefresh.Invalidate(); };
            _btnRefresh.MouseLeave += (s, e) => { refHover = false; _btnRefresh.Invalidate(); };
            _btnRefresh.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, _btnRefresh.Width - 1, _btnRefresh.Height - 1);
                using (SolidBrush b = new SolidBrush(refHover ? Color.FromArgb(46, 46, 52) : Color.FromArgb(32, 32, 36)))
                using (Pen p = new Pen(Color.FromArgb(52, 52, 58), 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 19);
                    MainForm.DrawRoundedRectangle(e.Graphics, p, r, 19);
                }
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(235, 235, 240)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("⟳ 扫描刷新", _btnRefresh.Font, tb, new RectangleF(0, 1, _btnRefresh.Width, _btnRefresh.Height), sf);
                }
            };
            _btnRefresh.Click += (s, e) =>
            {
                if (_netService != null)
                {
                    _netService.BroadcastPresence("HELLO");
                }
                PopulatePeers();
            };
            this.Controls.Add(_btnRefresh);

            // 2. 节点列表滚动面板 (位于分类卡片区)
            _listPanel = new Panel
            {
                Location = new Point(32, 185),
                Size = new Size(696, 260),
                AutoScroll = true,
                BackColor = Color.Transparent
            };
            this.Controls.Add(_listPanel);

            // 3. 底部手动 IP 直连卡片容器中的控件
            _txtManualIp = new TextBox
            {
                Location = new Point(132, 514),
                Size = new Size(420, 24),
                BackColor = Color.FromArgb(16, 16, 18),
                ForeColor = Color.FromArgb(240, 240, 245),
                Font = new Font("Microsoft YaHei", 10.0f),
                BorderStyle = BorderStyle.None
            };
            this.Controls.Add(_txtManualIp);

            _btnManualConnect = new Button
            {
                Text = "连接握手",
                Location = new Point(610, 503),
                Size = new Size(100, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold)
            };
            _btnManualConnect.FlatAppearance.BorderSize = 0;
            _btnManualConnect.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnManualConnect.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool mcHover = false;
            _btnManualConnect.MouseEnter += (s, e) => { mcHover = true; _btnManualConnect.Invalidate(); };
            _btnManualConnect.MouseLeave += (s, e) => { mcHover = false; _btnManualConnect.Invalidate(); };
            _btnManualConnect.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, _btnManualConnect.Width - 1, _btnManualConnect.Height - 1);
                using (SolidBrush b = new SolidBrush(mcHover ? Color.FromArgb(142, 100, 220) : Color.FromArgb(121, 70, 215)))
                using (Pen p = new Pen(Color.FromArgb(142, 100, 220), 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 18);
                    MainForm.DrawRoundedRectangle(e.Graphics, p, r, 18);
                }
                using (SolidBrush tb = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("连接握手", _btnManualConnect.Font, tb, new RectangleF(0, 1, _btnManualConnect.Width, _btnManualConnect.Height), sf);
                }
            };
            _btnManualConnect.Click += (s, e) =>
            {
                string ip = _txtManualIp.Text.Trim();
                if (string.IsNullOrEmpty(ip))
                {
                    GrokMessageBox.Show(this, "请输入有效的对方计算机 IPv4 地址 (例如: 192.168.1.100)", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                this.Close();
                if (_onConnectIp != null) _onConnectIp(ip);
            };
            this.Controls.Add(_btnManualConnect);
        }

        private void PopulatePeers()
        {
            _listPanel.Controls.Clear();

            List<PeerInfo> peers = new List<PeerInfo>();
            if (_netService != null)
            {
                foreach (PeerInfo p in _netService.GetActivePeers())
                {
                    if (!string.IsNullOrEmpty(_searchFilter))
                    {
                        bool matchNick = !string.IsNullOrEmpty(p.NickName) && p.NickName.ToLowerInvariant().Contains(_searchFilter);
                        bool matchHost = !string.IsNullOrEmpty(p.HostName) && p.HostName.ToLowerInvariant().Contains(_searchFilter);
                        bool matchIp = p.IP != null && p.IP.ToString().Contains(_searchFilter);
                        if (!matchNick && !matchHost && !matchIp) continue;
                    }
                    peers.Add(p);
                }
            }

            int cardW = 338;
            int cardH = 76;
            int gapX = 14;
            int gapY = 12;

            if (peers.Count == 0)
            {
                Label emptyLbl = new Label
                {
                    Text = string.IsNullOrEmpty(_searchFilter) ? "局域网内暂未发现其他节点，请尝试点击右上角【⟳ 扫描刷新】或在下方手动输入对方 IP 直连握手" : "未找到匹配此关键词的节点",
                    ForeColor = Color.FromArgb(140, 140, 146),
                    Font = new Font("Microsoft YaHei", 9.5f),
                    Location = new Point(20, 40),
                    Size = new Size(650, 48),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                _listPanel.Controls.Add(emptyLbl);
                return;
            }

            for (int i = 0; i < peers.Count; i++)
            {
                PeerInfo peer = peers[i];
                int col = i % 2;
                int row = i / 2;
                int x = col * (cardW + gapX);
                int y = row * (cardH + gapY);

                Panel card = CreatePeerCard(peer, cardW, cardH);
                card.Location = new Point(x, y);
                _listPanel.Controls.Add(card);
            }
        }

        private Panel CreatePeerCard(PeerInfo peer, int w, int h)
        {
            Panel card = new Panel
            {
                Size = new Size(w, h),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            bool isHover = false;
            card.MouseEnter += (s, e) => { isHover = true; card.Invalidate(); };
            card.MouseLeave += (s, e) => { isHover = false; card.Invalidate(); };

            // 右侧 "连接" 药丸按钮 (对标 ConnetctApp.png 的“添加”/“连接”)
            Button btnAction = new Button
            {
                Text = "连接",
                Location = new Point(w - 74, (h - 32) / 2),
                Size = new Size(62, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold)
            };
            btnAction.FlatAppearance.BorderSize = 0;
            btnAction.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnAction.FlatAppearance.MouseDownBackColor = Color.Transparent;

            bool btnHover = false;
            btnAction.MouseEnter += (s, e) => { btnHover = true; btnAction.Invalidate(); };
            btnAction.MouseLeave += (s, e) => { btnHover = false; btnAction.Invalidate(); };
            btnAction.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, btnAction.Width - 1, btnAction.Height - 1);
                using (SolidBrush b = new SolidBrush(btnHover ? Color.FromArgb(56, 56, 62) : Color.FromArgb(40, 40, 44)))
                using (Pen p = new Pen(Color.FromArgb(58, 58, 64), 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 16);
                    MainForm.DrawRoundedRectangle(e.Graphics, p, r, 16);
                }
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(235, 235, 240)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("连接", btnAction.Font, tb, new RectangleF(0, 1, btnAction.Width, btnAction.Height), sf);
                }
            };

            btnAction.Click += (s, e) =>
            {
                this.Close();
                if (_onSelectPeer != null) _onSelectPeer(peer);
            };

            card.Click += (s, e) =>
            {
                this.Close();
                if (_onSelectPeer != null) _onSelectPeer(peer);
            };

            card.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle totalR = new Rectangle(0, 0, w - 1, h - 1);

                // 卡片暗黑背景与圆角
                using (SolidBrush bg = new SolidBrush(isHover ? Color.FromArgb(32, 32, 36) : Color.FromArgb(25, 25, 28)))
                using (Pen borderPen = new Pen(Color.FromArgb(44, 44, 48), 1f))
                {
                    MainForm.DrawRoundedRectangle(g, bg, totalR, 12);
                    MainForm.DrawRoundedRectangle(g, borderPen, totalR, 12);
                }

                // 左侧有机吉祥物头像 (48x48)
                int avSize = 48;
                Rectangle avRect = new Rectangle(14, (h - avSize) / 2, avSize, avSize);
                int shapeIdx = Math.Abs(peer.HostName.GetHashCode()) % 14;
                Color avColor = Color.FromArgb(144, 90, 251);
                if (shapeIdx % 3 == 0) avColor = Color.FromArgb(255, 107, 34);
                else if (shapeIdx % 3 == 1) avColor = Color.FromArgb(31, 214, 126);

                WeChatIcon.DrawMascotAvatarShape(g, avRect, shapeIdx, avColor, true, Color.FromArgb(25, 25, 28));

                // 节点名称与 IP 说明文字
                using (Font nameFont = new Font("Microsoft YaHei", 10.5f, FontStyle.Bold))
                using (Font subFont = new Font("Microsoft YaHei", 9.0f, FontStyle.Regular))
                using (SolidBrush nb = new SolidBrush(Color.FromArgb(242, 242, 246)))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(150, 150, 156)))
                {
                    string displayName = !string.IsNullOrEmpty(peer.NickName) ? peer.NickName : peer.HostName;
                    g.DrawString(displayName, nameFont, nb, new PointF(72, 16));
                    g.DrawString(peer.IP + " · 在线", subFont, sb, new PointF(72, 40));
                }
            };

            card.Controls.Add(btnAction);
            return card;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Rectangle closeRect = new Rectangle(this.Width - 38, 16, 22, 22);
                if (closeRect.Contains(e.Location))
                {
                    this.Close();
                    return;
                }

                // 过滤标签胶囊点击 (全部 / 在线 / 手动)
                int chipY = 118;
                string[] chips = new string[] { "全部", "局域网在线", "历史节点", "手动直连" };
                int curX = 32;
                for (int i = 0; i < chips.Length; i++)
                {
                    int chipW = 74;
                    Rectangle chipR = new Rectangle(curX, chipY, chipW, 32);
                    if (chipR.Contains(e.Location))
                    {
                        _filterIndex = i;
                        Invalidate(new Rectangle(30, 115, 400, 40));
                        PopulatePeers();
                        return;
                    }
                    curX += chipW + 10;
                }

                if (e.Y < 54)
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
            bool hover = closeRect.Contains(e.Location);
            if (hover != _isCloseHover)
            {
                _isCloseHover = hover;
                Invalidate(closeRect);
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

            // 2. 标题 "连接节点" (18.0pt 加粗明亮纯白，完全对标 ConnetctApp.png 与 GrokBot 规范)
            using (Font titleFont = new Font("Microsoft YaHei UI", 18f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(250, 250, 255)))
            {
                g.DrawString("连接节点", titleFont, tb, new PointF(30, 18));
            }

            // 右上角指示器 "已连接 N 个 >"
            int peerCount = _netService != null ? _netService.GetActivePeers().Count : 0;
            string countText = string.Format("已连接 {0} 个  ›", peerCount);
            using (Font countFont = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular))
            using (SolidBrush cb = new SolidBrush(Color.FromArgb(170, 170, 176)))
            {
                SizeF csz = g.MeasureString(countText, countFont);
                g.DrawString(countText, countFont, cb, new PointF(this.Width - 48 - csz.Width, 20));
            }

            // 右上角关闭按钮 (×)
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

            // 3. 搜索栏输入胶囊 (高 40px, 圆角 20px, 完全对标 ConnetctApp.png)
            Rectangle searchBox = new Rectangle(30, 64, 560, 40);
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(16, 16, 18)))
            using (Pen sp = new Pen(Color.FromArgb(44, 44, 50), 1f))
            {
                MainForm.DrawRoundedRectangle(g, sb, searchBox, 20);
                MainForm.DrawRoundedRectangle(g, sp, searchBox, 20);
            }

            // 放大镜图标
            Rectangle iconR = new Rectangle(44, 75, 16, 16);
            WeChatIcon.Draw(g, WeChatIconType.Search, iconR, false, Color.FromArgb(150, 150, 156));

            if (string.IsNullOrEmpty(_txtSearch.Text))
            {
                using (Font pf = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular))
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(110, 110, 116)))
                {
                    g.DrawString("在节点中搜索 (主机名 / IP / 昵称)...", pf, pb, new PointF(66, 74));
                }
            }

            // 4. 标签过滤芯片 (全部 / 局域网在线 / 历史节点 / 手动直连)
            int chipY = 118;
            string[] chips = new string[] { "全部", "局域网在线", "历史节点", "手动直连" };
            int curX = 32;
            using (Font chipFont = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular))
            {
                for (int i = 0; i < chips.Length; i++)
                {
                    int chipW = 74;
                    Rectangle chipR = new Rectangle(curX, chipY, chipW, 30);
                    bool active = (_filterIndex == i);

                    Color chipBg = active ? Color.FromArgb(242, 242, 246) : Color.FromArgb(28, 28, 32);
                    Color chipBorder = active ? Color.FromArgb(242, 242, 246) : Color.FromArgb(44, 44, 50);
                    Color chipTc = active ? Color.FromArgb(14, 14, 16) : Color.FromArgb(200, 200, 206);

                    using (SolidBrush cb = new SolidBrush(chipBg))
                    using (Pen cp = new Pen(chipBorder, 1f))
                    {
                        MainForm.DrawRoundedRectangle(g, cb, chipR, 15);
                        MainForm.DrawRoundedRectangle(g, cp, chipR, 15);
                    }
                    using (SolidBrush ctb = new SolidBrush(chipTc))
                    {
                        StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(chips[i], chipFont, ctb, chipR, sf);
                    }
                    curX += chipW + 10;
                }
            }

            // 5. 分组标题: "为你推荐 / 局域网节点"
            using (Font groupFont = new Font("Microsoft YaHei", 11.0f, FontStyle.Bold))
            using (SolidBrush gb = new SolidBrush(Color.FromArgb(240, 240, 246)))
            {
                g.DrawString("局域网节点", groupFont, gb, new PointF(32, 158));
            }

            // 6. 分组标题: "手动 IP 握手直连" (完全对标 ConnetctApp.png 密码管理器单卡片)
            using (Font groupFont = new Font("Microsoft YaHei", 11.0f, FontStyle.Bold))
            using (SolidBrush gb = new SolidBrush(Color.FromArgb(240, 240, 246)))
            {
                g.DrawString("手动指定 IP 握手直连", groupFont, gb, new PointF(32, 462));
            }

            // 底部直连卡片容器
            Rectangle manualCard = new Rectangle(32, 492, 696, 58);
            using (SolidBrush mb = new SolidBrush(Color.FromArgb(25, 25, 28)))
            using (Pen mp = new Pen(Color.FromArgb(44, 44, 48), 1f))
            {
                MainForm.DrawRoundedRectangle(g, mb, manualCard, 14);
                MainForm.DrawRoundedRectangle(g, mp, manualCard, 14);
            }

            // 卡片左侧小图标 (圆角方块带 Bolt 闪电)
            Rectangle boltBox = new Rectangle(46, 503, 36, 36);
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(40, 40, 46)))
            {
                MainForm.DrawRoundedRectangle(g, bb, boltBox, 10);
            }
            WeChatIcon.Draw(g, WeChatIconType.Bolt, new Rectangle(54, 511, 20, 20), false, Color.FromArgb(144, 90, 251));

            // 输入提示与输入框占位符
            using (Font ipLblFont = new Font("Microsoft YaHei", 9.0f, FontStyle.Regular))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(150, 150, 156)))
            {
                g.DrawString("输入对方 IPv4:", ipLblFont, tb, new PointF(90, 498));
            }

            // 输入框底框
            Rectangle ipBoxRect = new Rectangle(88, 510, 508, 30);
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(16, 16, 18)))
            using (Pen bp = new Pen(Color.FromArgb(50, 50, 56), 1f))
            {
                MainForm.DrawRoundedRectangle(g, bb, ipBoxRect, 6);
                MainForm.DrawRoundedRectangle(g, bp, ipBoxRect, 6);
            }

            if (string.IsNullOrEmpty(_txtManualIp.Text))
            {
                using (Font pf = new Font("Microsoft YaHei", 9.0f, FontStyle.Regular))
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(110, 110, 116)))
                {
                    g.DrawString("例如: 192.168.1.100 (无需加端口号)", pf, pb, new PointF(134, 516));
                }
            }
        }
    }
}
