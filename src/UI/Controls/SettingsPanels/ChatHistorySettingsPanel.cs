using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 设置中心 - 聊天记录管理面板
    /// </summary>
    public class ChatHistorySettingsPanel : SettingsPanelBase
    {
        /// <summary>
        /// 初始化聊天记录管理面板
        /// </summary>
        /// <param name="context">全局设置上下文宿主</param>
        public ChatHistorySettingsPanel(SettingsContext context) : base(context)
        {
            BuildComponents();
            AutoFitHeight();
        }

        private void BuildComponents()
        {
            int curY = 4;

            Label lblHist = CreateSectionHeader("聊天记录管理", curY);
            this.Controls.Add(lblHist);
            curY += 28;

            Panel cardHist = CreateCardPanel(CARD_W, 162);
            cardHist.Location = new Point(0, curY);

            cardHist.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    // Row 1
                    g.DrawString("聊天记录存储目录", f, tb, new PointF(20, 9));
                    string curHDir = ChatHistoryService.StorageDirectory;
                    string shortHDir = curHDir.Length > 44 ? "..." + curHDir.Substring(curHDir.Length - 41) : curHDir;
                    g.DrawString(shortHDir, sf, sb, new PointF(20, 29));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2
                    g.DrawString("自动加载历史消息", f, tb, new PointF(20, 54 + 9));
                    g.DrawString("重启或更新覆盖安装后，自动读取历史会话与消息流。", sf, sb, new PointF(20, 54 + 29));
                    g.DrawLine(sep, 16, 108, CARD_W - 16, 108);

                    // Row 3
                    g.DrawString("彻底清空所有本地聊天记录", f, tb, new PointF(20, 108 + 9));
                    g.DrawString("彻底清除本机保存的所有广播与好友聊天历史文件。", sf, sb, new PointF(20, 108 + 29));
                }
            };

            Button btnChangeHist = CreateActionLink("更改 >", 72, 28, () =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "请选择聊天记录存储路径 (系统将自动迁移已有历史记录)";
                    fbd.SelectedPath = ChatHistoryService.StorageDirectory;
                    IWin32Window owner = Context.OwnerWindow ?? this;
                    if (fbd.ShowDialog(owner) == DialogResult.OK && !string.IsNullOrEmpty(fbd.SelectedPath))
                    {
                        ChatHistoryService.ChangeStorageDirectory(fbd.SelectedPath, true);
                        cardHist.Invalidate();
                    }
                }
            });
            btnChangeHist.Location = new Point(CARD_W - btnChangeHist.Width - 140, (54 - 32) / 2);
            cardHist.Controls.Add(btnChangeHist);

            Button btnOpenHist = CreateActionLink("打开文件夹 >", 110, 28, () =>
            {
                try { Process.Start("explorer.exe", ChatHistoryService.StorageDirectory); } catch { }
            });
            btnOpenHist.Location = new Point(CARD_W - btnOpenHist.Width - 20, (54 - 32) / 2);
            cardHist.Controls.Add(btnOpenHist);

            ModernToggleSwitch swAutoLoad = new ModernToggleSwitch(true, null);
            swAutoLoad.Location = new Point(CARD_W - swAutoLoad.Width - 24, 54 + (54 - 22) / 2);
            cardHist.Controls.Add(swAutoLoad);

            Button btnClrHist = CreateActionLink("立即清空 >", 96, 28, null, true);
            btnClrHist.Click += (s, e) =>
            {
                IWin32Window owner = Context.OwnerWindow ?? this;
                if (GrokMessageBox.Show(owner, "确定要彻底清空本机所有本地聊天记录吗？此操作将删除所有往期历史且无法撤销。", "清空记录确认", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    ChatHistoryService.ClearAllHistories();
                    btnClrHist.Text = "已清空 ✓";
                }
            };
            btnClrHist.Location = new Point(CARD_W - btnClrHist.Width - 20, 108 + (54 - 32) / 2);
            cardHist.Controls.Add(btnClrHist);

            this.Controls.Add(cardHist);
        }
    }
}
