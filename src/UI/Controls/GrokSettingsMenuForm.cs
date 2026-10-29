using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// Grok Bot 100% 像素级对标原生设置弹出菜单窗口 (对标 Alis3.png)
    /// 包含深蓝顶栏 (发现新版本 / 安装)、48px 舒适行高、细线矢量图标与 16px 圆角阴影
    /// </summary>
    public class GrokSettingsMenuForm : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private readonly NetworkService _netService;
        private readonly Action _onCheckUpdate;
        private readonly Action _onShowChangeNick;
        private readonly Action _onOpenFolder;
        private readonly Action _onClearChat;
        private readonly Action _onShowAbout;
        private readonly Action _onExit;

        private int _hoverIndex = -1; // -2: banner button, 0..5: menu items
        private const int ITEM_COUNT = 6;
        private const int ROW_HEIGHT = 44; // Docs menu row 44px
        private const int BANNER_HEIGHT = 58;

        public GrokSettingsMenuForm(
            NetworkService netService,
            Action onCheckUpdate,
            Action onShowChangeNick,
            Action onOpenFolder,
            Action onClearChat,
            Action onShowAbout,
            Action onExit)
        {
            _netService = netService;
            _onCheckUpdate = onCheckUpdate;
            _onShowChangeNick = onShowChangeNick;
            _onOpenFolder = onOpenFolder;
            _onClearChat = onClearChat;
            _onShowAbout = onShowAbout;
            _onExit = onExit;

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(342, 356);
            this.BackColor = Color.FromArgb(28, 28, 31); // #1C1C1F Level1
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Default;

            this.Deactivate += (s, e) => {
                try { this.Close(); } catch { }
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                IntPtr rgn = CreateRoundRectRgn(0, 0, this.Width + 1, this.Height + 1, 18, 18);
                if (rgn != IntPtr.Zero)
                {
                    this.Region = Region.FromHrgn(rgn);
                }
            }
            catch { }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                cp.ExStyle |= 0x00000080;    // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newHover = -1;

            Rectangle bannerBtn = new Rectangle(this.Width - 76, 15, 56, 28);
            if (bannerBtn.Contains(e.Location))
            {
                newHover = -2;
            }
            else if (e.Y < BANNER_HEIGHT)
            {
                newHover = -3; // banner hover
            }
            else
            {
                int itemAreaY = e.Y - (BANNER_HEIGHT + 6);
                if (itemAreaY >= 0)
                {
                    if (itemAreaY < 4 * ROW_HEIGHT)
                    {
                        newHover = itemAreaY / ROW_HEIGHT;
                    }
                    else
                    {
                        int belowSep = itemAreaY - (4 * ROW_HEIGHT + 10);
                        if (belowSep >= 0 && belowSep < 2 * ROW_HEIGHT)
                        {
                            newHover = 4 + (belowSep / ROW_HEIGHT);
                        }
                    }
                }
            }

            if (newHover != _hoverIndex)
            {
                _hoverIndex = newHover;
                this.Cursor = (_hoverIndex != -1) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverIndex = -1;
            this.Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;

            Rectangle bannerBtn = new Rectangle(this.Width - 76, 15, 56, 28);
            if (bannerBtn.Contains(e.Location) || (e.Y < BANNER_HEIGHT && e.X >= this.Width - 85))
            {
                this.Close();
                if (_onCheckUpdate != null) _onCheckUpdate();
                return;
            }

            switch (_hoverIndex)
            {
                case 0: // 修改昵称
                    this.Close();
                    if (_onShowChangeNick != null) _onShowChangeNick();
                    break;
                case 1: // 自动接收文件
                    if (_netService != null)
                    {
                        _netService.AutoAcceptFiles = !_netService.AutoAcceptFiles;
                        Invalidate();
                    }
                    break;
                case 2: // 接收文件夹
                    this.Close();
                    if (_onOpenFolder != null) _onOpenFolder();
                    break;
                case 3: // 支持与关于
                    this.Close();
                    if (_onShowAbout != null) _onShowAbout();
                    break;
                case 4: // 清空聊天
                    this.Close();
                    if (_onClearChat != null) _onClearChat();
                    break;
                case 5: // 退出
                    this.Close();
                    if (_onExit != null) _onExit();
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // 1. 全局深灰底色 (#1F1F1F) 与微光外边框 (#2C2C30)
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(28, 28, 31))) // #1C1C1F
            using (Pen borderPen = new Pen(Color.FromArgb(44, 44, 50), 1f)) // #2C2C32
            {
                Rectangle totalRect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
                MainForm.DrawRoundedRectangle(g, bgBrush, totalRect, 16);
                MainForm.DrawRoundedRectangle(g, borderPen, totalRect, 16);
            }

            // 2. 顶部深蓝版本横幅 (#1E2E40，复刻 Alis3.png)
            Rectangle bannerRect = new Rectangle(0, 0, this.Width, BANNER_HEIGHT);
            using (GraphicsPath bp = new GraphicsPath())
            {
                int r = 16;
                bp.AddArc(0, 0, r * 2, r * 2, 180, 90);
                bp.AddArc(this.Width - r * 2, 0, r * 2, r * 2, 270, 90);
                bp.AddLine(this.Width, BANNER_HEIGHT, 0, BANNER_HEIGHT);
                bp.CloseFigure();
                using (SolidBrush bannerBrush = new SolidBrush(Color.FromArgb(30, 46, 64)))
                {
                    g.FillPath(bannerBrush, bp);
                }
            }

            // 横幅文字：发现新版本
            using (Font bf = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold))
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(77, 155, 255))) // #4D9BFF
            {
                g.DrawString("发现新版本", bf, bb, new PointF(22, 19));
            }

            // 横幅右侧按钮：安装 (圆角 14px 蓝胶囊 #116DD2)
            Rectangle btnRect = new Rectangle(this.Width - 76, 15, 56, 28);
            Color btnColor = (_hoverIndex == -2) ? Color.FromArgb(22, 119, 255) : Color.FromArgb(17, 109, 210);
            using (SolidBrush btnBrush = new SolidBrush(btnColor))
            {
                MainForm.DrawRoundedRectangle(g, btnBrush, btnRect, 14);
            }
            using (Font btnFont = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold))
            using (SolidBrush btnTextBrush = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("安装", btnFont, btnTextBrush, btnRect, sf);
            }

            // 3. 菜单列表项数据定义 (与 Alis3.png 100% 对标)
            string currentNick = _netService != null ? _netService.NickName : Environment.UserName;
            bool autoAccept = _netService != null && _netService.AutoAcceptFiles;

            MenuItemDef[] items = new MenuItemDef[]
            {
                new MenuItemDef(WeChatIconType.User, "修改我的昵称", currentNick + "  ›"),
                new MenuItemDef(WeChatIconType.Bolt, "自动接收文件", (autoAccept ? "已开启" : "已关闭") + "  ›"),
                new MenuItemDef(WeChatIconType.Folder, "接收文件目录", "打开  ›"),
                new MenuItemDef(WeChatIconType.Settings, "支持与关于", "›"),
                new MenuItemDef(WeChatIconType.Clear, "清空聊天记录", ""),
                new MenuItemDef(WeChatIconType.Logout, "退出登录", "")
            };

            int startY = BANNER_HEIGHT + 6;
            using (Font itemFont = new Font("Microsoft YaHei UI", 10.0f, FontStyle.Regular))
            using (Font subFont = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Regular))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(228, 228, 232))) // #E4E4E8
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(142, 142, 147))) // #8E8E93
            using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(46, 46, 52))) // #2E2E34
            {
                for (int i = 0; i < items.Length; i++)
                {
                    int itemY = startY + (i < 4 ? i * ROW_HEIGHT : (i * ROW_HEIGHT + 10));

                    // 分割线 (在第4项之后)
                    if (i == 4)
                    {
                        int sepY = startY + 4 * ROW_HEIGHT + 4;
                        using (Pen sepPen = new Pen(Color.FromArgb(44, 44, 52), 1f)) // #2C2C34
                        {
                            g.DrawLine(sepPen, 18, sepY, this.Width - 18, sepY);
                        }
                    }

                    // 悬停高亮底色 (圆角 8px)
                    if (_hoverIndex == i)
                    {
                        Rectangle hr = new Rectangle(8, itemY, this.Width - 16, ROW_HEIGHT - 2);
                        MainForm.DrawRoundedRectangle(g, hoverBg, hr, 8);
                    }

                    // 左侧矢量图标 (18x18, 居中对齐)
                    Rectangle iconRect = new Rectangle(20, itemY + (ROW_HEIGHT - 18) / 2, 18, 18);
                    Color iconColor = (_hoverIndex == i) ? Color.FromArgb(250, 250, 252) : Color.FromArgb(160, 160, 165);
                    WeChatIcon.Draw(g, items[i].Icon, iconRect, false, iconColor);

                    // 主标题文字 (x=48 per Docs §7)
                    float ty = itemY + (ROW_HEIGHT - itemFont.GetHeight(g)) / 2f;
                    g.DrawString(items[i].Title, itemFont, textBrush, new PointF(48, ty));

                    // 右侧副标题与箭头指示符 (如 "已开启 ›" 或 "mimovr ›")
                    if (!string.IsNullOrEmpty(items[i].SubText))
                    {
                        SizeF ssz = g.MeasureString(items[i].SubText, subFont);
                        float sx = this.Width - 22 - ssz.Width;
                        g.DrawString(items[i].SubText, subFont, subBrush, new PointF(sx, ty));
                    }
                }
            }
        }

        private struct MenuItemDef
        {
            public WeChatIconType Icon;
            public string Title;
            public string SubText;

            public MenuItemDef(WeChatIconType icon, string title, string sub)
            {
                Icon = icon;
                Title = title;
                SubText = sub;
            }
        }
    }
}
