using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// GrokBot 100% 像素级对标原生连接节点窗口 (完全对标 ConnetctApp.png 与 Docs §9)
    /// 继承自 GrokModalFormBase，标准尺寸 760×600 (基准度量)，采用自绘 GrokButton
    /// </summary>
    public class GrokConnectNodeForm : GrokModalFormBase
    {
        private readonly NetworkService _netService;
        private readonly Action<string> _onConnectIp;
        private readonly Action<PeerInfo> _onSelectPeer;

        private TextBox _txtSearch;
        private TextBox _txtManualIp;
        private GrokButton _btnManualConnect;
        private GrokButton _btnRefresh;
        private Panel _listPanel;

        private int _filterIndex = 0; // 0: 全部, 1: 在线, 2: 常用, 3: 手动
        private string _searchFilter = string.Empty;

        // =========================================================================
        // 构造函数与初始化
        // =========================================================================

        /// <summary>
        /// 初始化连接节点弹窗
        /// </summary>
        /// <param name="netService">网络通信服务实例，用于获取对端节点和广播扫描</param>
        /// <param name="onConnectIp">手动输入 IP 连接时的回调</param>
        /// <param name="onSelectPeer">点击选择在线节点卡片时的回调</param>
        public GrokConnectNodeForm(NetworkService netService, Action<string> onConnectIp, Action<PeerInfo> onSelectPeer)
        {
            _netService = netService;
            _onConnectIp = onConnectIp;
            _onSelectPeer = onSelectPeer;

            this.Size = GrokTheme.SizeConnectNode; // 760 x 600
            this.ShowCloseButton = true;
            this.Font = GrokTheme.FontSecondary;

            BuildControls();
            PopulatePeers();
        }

        // =========================================================================
        // 界面控件构建
        // =========================================================================

        /// <summary>
        /// 构建搜索框、扫描刷新按钮、滚动面板及底部手动直连输入控件
        /// </summary>
        private void BuildControls()
        {
            // 1. 顶部搜索框
            _txtSearch = new TextBox
            {
                Location = new Point(66, 73),
                Size = new Size(490, 24),
                BackColor = GrokTheme.Level0,
                ForeColor = GrokTheme.TextPrimary,
                Font = GrokTheme.FontCardItem,
                BorderStyle = BorderStyle.None
            };
            _txtSearch.TextChanged += (s, e) =>
            {
                _searchFilter = _txtSearch.Text.Trim().ToLowerInvariant();
                PopulatePeers();
            };
            this.Controls.Add(_txtSearch);

            // 搜索框右侧 "扫描刷新" 药丸按钮
            _btnRefresh = new GrokButton("⟳ 扫描刷新", GrokButtonRole.Secondary)
            {
                IsPill = true,
                Location = new Point(610, 64),
                Size = new Size(116, 38)
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
                Size = new Size(450, 24),
                BackColor = GrokTheme.Level0,
                ForeColor = GrokTheme.TextPrimary,
                Font = GrokTheme.FontCardItem,
                BorderStyle = BorderStyle.None
            };
            this.Controls.Add(_txtManualIp);

            _btnManualConnect = new GrokButton("连接握手", GrokButtonRole.Primary)
            {
                Location = new Point(610, 503),
                Size = new Size(100, 36)
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

        // =========================================================================
        // 节点列表渲染与卡片创建
        // =========================================================================

        /// <summary>
        /// 重新根据过滤词与分类标签筛选局域网节点，并在面板中动态排布双列卡片
        /// </summary>
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
                    ForeColor = GrokTheme.TextSecondary,
                    Font = GrokTheme.FontSecondary,
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

        /// <summary>
        /// 创建单个节点卡片容器并绑定自绘与点击交互
        /// </summary>
        /// <param name="peer">对端节点实体</param>
        /// <param name="w">卡片宽度</param>
        /// <param name="h">卡片高度</param>
        /// <returns>卡片面板控件</returns>
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

            // 右侧 "连接" 药丸按钮
            GrokButton btnAction = new GrokButton("连接", GrokButtonRole.Secondary)
            {
                IsPill = true,
                Location = new Point(w - 74, (h - 32) / 2),
                Size = new Size(62, 32)
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
                using (SolidBrush bg = new SolidBrush(isHover ? GrokTheme.Level2 : Color.FromArgb(25, 25, 28)))
                using (Pen borderPen = new Pen(GrokTheme.BorderInner, 1f))
                {
                    GrokTheme.DrawRoundedRectangle(g, bg, totalR, 12);
                    GrokTheme.DrawRoundedRectangle(g, borderPen, totalR, 12);
                }

                // 左侧有机吉祥物头像 (48x48)
                int avSize = 48;
                Rectangle avRect = new Rectangle(14, (h - avSize) / 2, avSize, avSize);
                int shapeIdx = Math.Abs(peer.HostName.GetHashCode()) % 14;
                Color avColor = GrokTheme.AccentPurple;
                if (shapeIdx % 3 == 0) avColor = Color.FromArgb(255, 107, 34);
                else if (shapeIdx % 3 == 1) avColor = GrokTheme.AccentGreen;

                WeChatIcon.DrawMascotAvatarShape(g, avRect, shapeIdx, avColor, true, Color.FromArgb(25, 25, 28));

                // 节点名称与 IP 说明文字
                using (Font nameFont = GrokTheme.FontSectionTitle)
                using (Font subFont = GrokTheme.FontFootnote)
                using (SolidBrush nb = new SolidBrush(GrokTheme.TextPrimary))
                using (SolidBrush sb = new SolidBrush(GrokTheme.TextSecondary))
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
            if (e.Button == MouseButtons.Left)
            {
                // 检查 4 个分类芯片点击 (y: 118, 左右 32 开始)
                int chipY = 118;
                int curX = 32;
                for (int i = 0; i < 4; i++)
                {
                    int chipW = 74;
                    Rectangle chipR = new Rectangle(curX, chipY, chipW, 32);
                    if (chipR.Contains(e.Location))
                    {
                        _filterIndex = i;
                        this.Invalidate(new Rectangle(30, 115, 400, 40));
                        PopulatePeers();
                        return;
                    }
                    curX += chipW + 10;
                }
            }

            base.OnMouseDown(e);
        }

        protected override void OnPaintContent(Graphics g)
        {
            // 1. 标题 "连接节点" (18.0pt Bold #FFFFFF)
            using (Font titleFont = GrokTheme.FontTitle)
            using (SolidBrush tb = new SolidBrush(GrokTheme.TextTitle))
            {
                g.DrawString("连接节点", titleFont, tb, new PointF(30, 18));
            }

            // 右上角指示器 "已连接 N 个 ›"
            int peerCount = _netService != null ? _netService.GetActivePeers().Count : 0;
            string countText = string.Format("已连接 {0} 个  ›", peerCount);
            using (Font countFont = GrokTheme.FontSecondary)
            using (SolidBrush cb = new SolidBrush(GrokTheme.TextSecondary))
            {
                SizeF csz = g.MeasureString(countText, countFont);
                g.DrawString(countText, countFont, cb, new PointF(this.Width - 48 - csz.Width, 20));
            }

            // 2. 搜索栏输入胶囊 (高 40px, 圆角 20px)
            Rectangle searchBox = new Rectangle(30, 64, 560, 40);
            using (SolidBrush sb = new SolidBrush(GrokTheme.Level0))
            using (Pen sp = new Pen(GrokTheme.Border, 1f))
            {
                GrokTheme.DrawRoundedRectangle(g, sb, searchBox, 20);
                GrokTheme.DrawRoundedRectangle(g, sp, searchBox, 20);
            }

            // 放大镜图标
            Rectangle iconR = new Rectangle(44, 75, 16, 16);
            WeChatIcon.Draw(g, WeChatIconType.Search, iconR, false, GrokTheme.IconMuted);

            if (_txtSearch != null && string.IsNullOrEmpty(_txtSearch.Text))
            {
                using (Font pf = GrokTheme.FontSecondary)
                using (SolidBrush pb = new SolidBrush(GrokTheme.TextPlaceholder))
                {
                    g.DrawString("在节点中搜索 (主机名 / IP / 昵称)...", pf, pb, new PointF(66, 74));
                }
            }

            // 3. 标签过滤芯片 (全部 / 局域网在线 / 常用节点 / 手动直连)
            int chipY = 118;
            string[] chips = new string[] { "全部", "局域网在线", "常用节点", "手动直连" };
            int curX = 32;
            using (Font chipFont = GrokTheme.FontSecondary)
            {
                for (int i = 0; i < chips.Length; i++)
                {
                    int chipW = 74;
                    Rectangle chipR = new Rectangle(curX, chipY, chipW, 30);
                    bool active = (_filterIndex == i);

                    Color chipBg = active ? GrokTheme.TextPrimary : GrokTheme.Level2;
                    Color chipBorder = active ? GrokTheme.TextPrimary : GrokTheme.Border;
                    Color chipTc = active ? GrokTheme.Level0 : GrokTheme.TextSecondary;

                    using (SolidBrush cb = new SolidBrush(chipBg))
                    using (Pen cp = new Pen(chipBorder, 1f))
                    {
                        GrokTheme.DrawRoundedRectangle(g, cb, chipR, 15);
                        GrokTheme.DrawRoundedRectangle(g, cp, chipR, 15);
                    }
                    using (SolidBrush ctb = new SolidBrush(chipTc))
                    {
                        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        {
                            g.DrawString(chips[i], chipFont, ctb, chipR, sf);
                        }
                    }
                    curX += chipW + 10;
                }
            }

            // 4. 分组标题: "为你推荐 / 局域网节点"
            using (Font groupFont = GrokTheme.FontPrimaryBold)
            using (SolidBrush gb = new SolidBrush(GrokTheme.TextPrimary))
            {
                g.DrawString("局域网节点", groupFont, gb, new PointF(32, 158));
            }

            // 5. 分组标题: "手动指定 IP 握手直连"
            using (Font groupFont = GrokTheme.FontPrimaryBold)
            using (SolidBrush gb = new SolidBrush(GrokTheme.TextPrimary))
            {
                g.DrawString("手动指定 IP 握手直连", groupFont, gb, new PointF(32, 462));
            }

            // 底部直连卡片容器
            Rectangle manualCard = new Rectangle(32, 492, 696, 58);
            using (SolidBrush mb = new SolidBrush(Color.FromArgb(25, 25, 28)))
            using (Pen mp = new Pen(GrokTheme.BorderInner, 1f))
            {
                GrokTheme.DrawRoundedRectangle(g, mb, manualCard, 14);
                GrokTheme.DrawRoundedRectangle(g, mp, manualCard, 14);
            }

            // 卡片左侧小图标 (圆角方块带 Bolt 闪电)
            Rectangle boltBox = new Rectangle(46, 503, 36, 36);
            using (SolidBrush bb = new SolidBrush(GrokTheme.Level3))
            {
                GrokTheme.DrawRoundedRectangle(g, bb, boltBox, 10);
            }
            WeChatIcon.Draw(g, WeChatIconType.Bolt, new Rectangle(54, 511, 20, 20), false, GrokTheme.AccentPurple);

            // 输入提示与输入框底框
            using (Font ipLblFont = GrokTheme.FontFootnote)
            using (SolidBrush tb = new SolidBrush(GrokTheme.TextSecondary))
            {
                g.DrawString("输入对方 IPv4:", ipLblFont, tb, new PointF(90, 498));
            }

            Rectangle ipBoxRect = new Rectangle(88, 510, 508, 30);
            using (SolidBrush bb = new SolidBrush(GrokTheme.Level0))
            using (Pen bp = new Pen(GrokTheme.Border, 1f))
            {
                GrokTheme.DrawRoundedRectangle(g, bb, ipBoxRect, 6);
                GrokTheme.DrawRoundedRectangle(g, bp, ipBoxRect, 6);
            }

            if (_txtManualIp != null && string.IsNullOrEmpty(_txtManualIp.Text))
            {
                using (Font pf = GrokTheme.FontFootnote)
                using (SolidBrush pb = new SolidBrush(GrokTheme.TextPlaceholder))
                {
                    g.DrawString("例如: 192.168.1.100 (无需加端口号)", pf, pb, new PointF(134, 516));
                }
            }
        }
    }
}
