using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 设置中心 - 通用设置面板 (常规、权限偏好、支持与关于)
    /// </summary>
    public class GeneralSettingsPanel : SettingsPanelBase
    {
        public GeneralSettingsPanel(SettingsContext context) : base(context)
        {
            BuildComponents();
            AutoFitHeight();
        }

        private void BuildComponents()
        {
            int curY = 4;

            // 1. 分类小标题: "常规"
            Label lblRegular = CreateSectionHeader("常规", curY);
            this.Controls.Add(lblRegular);
            curY += 28;

            // Card 1: 常规卡片 (高 216px: 语言、文件打开、字体大小、默认打开)
            Panel card1 = CreateCardPanel(CARD_W, 216);
            card1.Location = new Point(0, curY);

            card1.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    // Row 1: 语言 (y=0..54)
                    g.DrawString("语言", f, tb, new PointF(20, (54 - f.GetHeight(g)) / 2f));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2: 链接打开方式 (y=54..108)
                    g.DrawString("链接打开方式", f, tb, new PointF(20, 54 + 9));
                    g.DrawString("设置 LanChat 对话中链接的默认打开策略。", sf, sb, new PointF(20, 54 + 29));
                    g.DrawLine(sep, 16, 108, CARD_W - 16, 108);

                    // Row 3: 字体大小 (y=108..162)
                    g.DrawString("字体大小", f, tb, new PointF(20, 108 + (54 - f.GetHeight(g)) / 2f));
                    g.DrawLine(sep, 16, 162, CARD_W - 16, 162);

                    // Row 4: 默认打开方式 (y=162..216)
                    g.DrawString("默认文件接收目录", f, tb, new PointF(20, 162 + 9));
                    string dir = !string.IsNullOrEmpty(AppSettings.SaveDirectory) ? AppSettings.SaveDirectory : (Context.NetService != null ? Context.NetService.SaveDirectory : "ReceivedFiles");
                    string shortDir = dir.Length > 44 ? "..." + dir.Substring(dir.Length - 41) : dir;
                    g.DrawString(shortDir, sf, sb, new PointF(20, 162 + 29));
                }
            };

            // Row 1 语言下拉胶囊
            Button btnLang = CreateDropdownPill("跟随系统 (简体中文)  ∨", 156, 28, null);
            btnLang.Click += (s, e) => ShowLanguagePicker(btnLang);
            btnLang.Location = new Point(CARD_W - btnLang.Width - 20, (54 - 32) / 2);
            card1.Controls.Add(btnLang);

            // Row 2 链接打开方式下拉胶囊
            Button btnLink = CreateDropdownPill("智能打开  ∨", 112, 28, null);
            btnLink.Click += (s, e) => ShowLinkOpenPicker(btnLink);
            btnLink.Location = new Point(CARD_W - btnLink.Width - 20, 54 + (54 - 32) / 2);
            card1.Controls.Add(btnLink);

            // Row 3 字体大小滑块
            string[] fontLabels = new string[] { "小", "默认", "中", "大", "特大" };
            float[] fontSizes = new float[] { 9.0f, 10.0f, 11.0f, 12.0f, 14.0f };
            SteppedSlider sliderUiFont = new SteppedSlider(fontLabels, fontSizes, AppSettings.UiFontSize, (val) =>
            {
                AppSettings.UiFontSize = val;
                AppSettings.Save();
                if (Context.OnFontChanged != null) Context.OnFontChanged();
            });
            sliderUiFont.Location = new Point(CARD_W - sliderUiFont.Width - 20, 108 + (54 - 34) / 2);
            card1.Controls.Add(sliderUiFont);

            // Row 4 默认接收目录更改与打开按钮
            Button btnChangeDir = CreateActionLink("更改 >", 72, 28, () =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "请选择文件接收保存目录";
                    string cur = !string.IsNullOrEmpty(AppSettings.SaveDirectory) ? AppSettings.SaveDirectory : (Context.NetService != null ? Context.NetService.SaveDirectory : "ReceivedFiles");
                    fbd.SelectedPath = cur;
                    IWin32Window owner = Context.OwnerWindow ?? this;
                    if (fbd.ShowDialog(owner) == DialogResult.OK && !string.IsNullOrEmpty(fbd.SelectedPath))
                    {
                        AppSettings.SaveDirectory = fbd.SelectedPath;
                        if (Context.NetService != null) Context.NetService.SaveDirectory = fbd.SelectedPath;
                        AppSettings.Save();
                        card1.Invalidate();
                    }
                }
            });
            btnChangeDir.Location = new Point(CARD_W - btnChangeDir.Width - 140, 162 + (54 - 32) / 2);
            card1.Controls.Add(btnChangeDir);

            Button btnOpenFolder = CreateActionLink("打开文件夹 >", 110, 28, () =>
            {
                if (Context.OnOpenFolder != null) Context.OnOpenFolder();
            });
            btnOpenFolder.Location = new Point(CARD_W - btnOpenFolder.Width - 20, 162 + (54 - 32) / 2);
            card1.Controls.Add(btnOpenFolder);

            this.Controls.Add(card1);
            curY += 216 + 22;

            // 2. 分类小标题: "权限与偏好"
            Label lblPerm = CreateSectionHeader("权限与偏好", curY);
            this.Controls.Add(lblPerm);
            curY += 28;

            // Card 2: 权限与开关卡片 (高 216px: 锁屏运行、自动接收、开机自启、网络连接)
            Panel card2 = CreateCardPanel(CARD_W, 216);
            card2.Location = new Point(0, curY);

            card2.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    // Row 1: 托盘未读悬浮预览 (y=0..54)
                    g.DrawString("托盘未读悬浮预览", f, tb, new PointF(20, 9));
                    g.DrawString("新消息闪烁时鼠标悬停在托盘图标上显示发送人与摘要浮窗。", sf, sb, new PointF(20, 29));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2: 自动接收好友文件 (y=54..108)
                    g.DrawString("自动接收好友文件", f, tb, new PointF(20, 54 + 9));
                    g.DrawString("局域网好友发送文件时自动保存到默认目录，无需手动逐一确认。", sf, sb, new PointF(20, 54 + 29));
                    g.DrawLine(sep, 16, 108, CARD_W - 16, 108);

                    // Row 3: 运行时防止休眠 (y=108..162)
                    g.DrawString("后台网络驻留运行", f, tb, new PointF(20, 108 + 9));
                    g.DrawString("关闭主窗口后保持在系统托盘静默运行，随时响应局域网呼叫。", sf, sb, new PointF(20, 108 + 29));
                    g.DrawLine(sep, 16, 162, CARD_W - 16, 162);

                    // Row 4: 网络协议通道 (y=162..216)
                    g.DrawString("网络协议通道", f, tb, new PointF(20, 162 + 9));
                    g.DrawString("物理网卡直连通信，多节点广播穿透加速。", sf, sb, new PointF(20, 162 + 29));
                }
            };

            // Row 1 托盘悬浮预览开关
            ModernToggleSwitch swTray = new ModernToggleSwitch(true, (val) => { });
            swTray.Location = new Point(CARD_W - swTray.Width - 24, (54 - 22) / 2);
            card2.Controls.Add(swTray);

            // Row 2 自动接收文件开关
            bool autoAccept = Context.NetService != null ? Context.NetService.AutoAcceptFiles : AppSettings.AutoAcceptFiles;
            ModernToggleSwitch swAutoAccept = new ModernToggleSwitch(autoAccept, (val) =>
            {
                if (Context.NetService != null) Context.NetService.AutoAcceptFiles = val;
                AppSettings.AutoAcceptFiles = val;
                AppSettings.Save();
            });
            swAutoAccept.Location = new Point(CARD_W - swAutoAccept.Width - 24, 54 + (54 - 22) / 2);
            card2.Controls.Add(swAutoAccept);

            // Row 3 后台驻留开关
            ModernToggleSwitch swKeepAlive = new ModernToggleSwitch(true, (val) => { });
            swKeepAlive.Location = new Point(CARD_W - swKeepAlive.Width - 24, 108 + (54 - 22) / 2);
            card2.Controls.Add(swKeepAlive);

            // Row 4 协议模式下拉胶囊
            Button btnProtocol = CreateDropdownPill("直接连接 (LAN)  ∨", 136, 28, null);
            btnProtocol.Location = new Point(CARD_W - btnProtocol.Width - 20, 162 + (54 - 32) / 2);
            card2.Controls.Add(btnProtocol);

            this.Controls.Add(card2);
            curY += 216 + 22;

            // 3. 分类小标题: "支持与关于"
            Label lblSupport = CreateSectionHeader("支持与关于", curY);
            this.Controls.Add(lblSupport);
            curY += 28;

            Panel card3 = CreateCardPanel(CARD_W, 108);
            card3.Location = new Point(0, curY);

            card3.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    // Row 1: 关于与更新
                    g.DrawString("关于 LanChat", f, tb, new PointF(20, 9));
                    g.DrawString("版本 v" + MainForm.APP_VERSION + " · 局域网极速通讯与协同传输", sf, sb, new PointF(20, 29));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2: 意见与反馈
                    g.DrawString("意见与问题反馈", f, tb, new PointF(20, 54 + 9));
                    g.DrawString("帮助我们将 LanChat 打磨得更贴合你的直觉与习惯。", sf, sb, new PointF(20, 54 + 29));
                }
            };

            Button btnCheckUpdate = CreateActionLink("检查更新 >", 96, 28, () =>
            {
                if (Context.OnCheckUpdate != null) Context.OnCheckUpdate();
            });
            btnCheckUpdate.Location = new Point(CARD_W - btnCheckUpdate.Width - 20, (54 - 32) / 2);
            card3.Controls.Add(btnCheckUpdate);

            Button btnSendFk = CreateActionLink("发送反馈 >", 96, 28, () =>
            {
                using (GrokFeedbackForm fk = new GrokFeedbackForm())
                {
                    IWin32Window owner = Context.OwnerWindow ?? this;
                    fk.ShowDialog(owner);
                }
            });
            btnSendFk.Location = new Point(CARD_W - btnSendFk.Width - 20, 54 + (54 - 32) / 2);
            card3.Controls.Add(btnSendFk);

            this.Controls.Add(card3);
        }

        private void ShowLanguagePicker(Button anchor)
        {
            Point pt = anchor.PointToScreen(new Point(0, anchor.Height + 4));
            GrokPopupMenu menu = new GrokPopupMenu();
            menu.AddItem("跟随系统 (简体中文)", WeChatIconType.None, () => { anchor.Text = "跟随系统 (简体中文)  ∨"; }, "✓")
                .AddItem("English (US)", WeChatIconType.None, () => { anchor.Text = "English (US)  ∨"; })
                .AddItem("繁体中文 (中国香港)", WeChatIconType.None, () => { anchor.Text = "繁体中文  ∨"; });
            menu.ShowAt(pt);
        }

        private void ShowLinkOpenPicker(Button anchor)
        {
            Point pt = anchor.PointToScreen(new Point(0, anchor.Height + 4));
            GrokPopupMenu menu = new GrokPopupMenu();
            menu.AddItem("智能打开 (默认浏览器)", WeChatIconType.None, () => { anchor.Text = "智能打开  ∨"; }, "✓")
                .AddItem("内置极速预览", WeChatIconType.None, () => { anchor.Text = "内置极速预览  ∨"; });
            menu.ShowAt(pt);
        }
    }
}
