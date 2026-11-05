using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// Grok Bot 原生设置弹出菜单窗口 (完全对标 GrokBot UI 设计规范)
    /// 包含 44px 舒适行高、细线矢量图标与 16px 圆角阴影
    /// 精简聚焦核心快捷操作：设置、修改我的昵称、清空聊天记录、退出登录
    /// （自动接收文件、接收目录、支持与关于已统一收口至通用设置面板）
    /// </summary>
    public class GrokSettingsMenuForm : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private readonly NetworkService _netService;
        private readonly Action _onShowSettings;
        private readonly Action _onShowChangeNick;
        private readonly Action _onClearChat;
        private readonly Action _onExit;

        private int _hoverIndex = -1; // 0..3: menu items
        private const int ITEM_COUNT = 4;
        private const int ROW_HEIGHT = 44; // Docs menu row 44px
        private const int TOP_PADDING = 8;
        private const int MENU_HEIGHT = 200; // 8 + 44*2 + 8 + 44*2 + 8 = 200px

        // =========================================================================
        // 构造函数与重载
        // =========================================================================

        /// <summary>
        /// 初始化底部齿轮设置弹出快捷菜单
        /// </summary>
        /// <param name="netService">网络服务实例</param>
        /// <param name="onShowSettings">打开完整设置面板回调</param>
        /// <param name="onShowChangeNick">打开修改昵称窗口回调</param>
        /// <param name="onClearChat">清空当前会话聊天记录回调</param>
        /// <param name="onExit">退出应用程序回调</param>
        public GrokSettingsMenuForm(
            NetworkService netService,
            Action onShowSettings,
            Action onShowChangeNick,
            Action onClearChat,
            Action onExit)
        {
            _netService = netService;
            _onShowSettings = onShowSettings;
            _onShowChangeNick = onShowChangeNick;
            _onClearChat = onClearChat;
            _onExit = onExit;

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(342, MENU_HEIGHT);
            this.BackColor = Color.FromArgb(28, 28, 31); // #1C1C1F Level1
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Default;

            this.Deactivate += (s, e) => {
                try { this.Close(); } catch { }
            };
        }

        /// <summary>
        /// 兼容旧版参数的重载构造函数
        /// </summary>
        public GrokSettingsMenuForm(
            NetworkService netService,
            Action onShowSettings,
            Action onCheckUpdate,
            Action onShowChangeNick,
            Action onOpenFolder,
            Action onClearChat,
            Action onShowAbout,
            Action onExit)
            : this(netService, onShowSettings, onShowChangeNick, onClearChat, onExit)
        {
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

            int itemAreaY = e.Y - TOP_PADDING;
            if (itemAreaY >= 0)
            {
                if (itemAreaY < 2 * ROW_HEIGHT)
                {
                    newHover = itemAreaY / ROW_HEIGHT;
                }
                else
                {
                    int belowSep = itemAreaY - (2 * ROW_HEIGHT + 8);
                    if (belowSep >= 0 && belowSep < 2 * ROW_HEIGHT)
                    {
                        newHover = 2 + (belowSep / ROW_HEIGHT);
                    }
                }
            }

            if (newHover != _hoverIndex)
            {
                _hoverIndex = newHover;
                this.Cursor = (_hoverIndex >= 0 && _hoverIndex < ITEM_COUNT) ? Cursors.Hand : Cursors.Default;
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

            switch (_hoverIndex)
            {
                case 0: // 设置
                    this.Close();
                    if (_onShowSettings != null) _onShowSettings();
                    break;
                case 1: // 修改我的昵称
                    this.Close();
                    if (_onShowChangeNick != null) _onShowChangeNick();
                    break;
                case 2: // 清空聊天记录
                    this.Close();
                    if (_onClearChat != null) _onClearChat();
                    break;
                case 3: // 退出登录
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

            // 2. 菜单列表项数据定义
            string currentNick = _netService != null ? _netService.NickName : Environment.UserName;

            MenuItemDef[] items = new MenuItemDef[]
            {
                new MenuItemDef(WeChatIconType.Settings, "设置", ""),
                new MenuItemDef(WeChatIconType.User, "修改我的昵称", currentNick + "  ›"),
                new MenuItemDef(WeChatIconType.Clear, "清空聊天记录", ""),
                new MenuItemDef(WeChatIconType.Logout, "退出登录", "")
            };

            int startY = TOP_PADDING;
            using (Font itemFont = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
            using (Font subFont = new Font(GrokTheme.FontFamily, 9.0f, FontStyle.Regular))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(228, 228, 232))) // #E4E4E8
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(142, 142, 147))) // #8E8E93
            using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(46, 46, 52))) // #2E2E34
            {
                for (int i = 0; i < items.Length; i++)
                {
                    int itemY = startY + (i < 2 ? i * ROW_HEIGHT : (i * ROW_HEIGHT + 8));

                    // 分割线 (在第2项之后)
                    if (i == 2)
                    {
                        int sepY = startY + 2 * ROW_HEIGHT + 4;
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

                    // 右侧副标题与指示符 (如 "mimovr  ›")
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
