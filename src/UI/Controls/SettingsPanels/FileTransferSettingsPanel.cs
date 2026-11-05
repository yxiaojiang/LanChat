using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 设置中心 - 文件传输面板
    /// </summary>
    public class FileTransferSettingsPanel : SettingsPanelBase
    {
        /// <summary>
        /// 初始化文件传输面板
        /// </summary>
        /// <param name="context">全局设置上下文宿主</param>
        public FileTransferSettingsPanel(SettingsContext context) : base(context)
        {
            BuildComponents();
            AutoFitHeight();
        }

        private void BuildComponents()
        {
            int curY = 4;

            Label lblFile = CreateSectionHeader("文件偏好", curY);
            this.Controls.Add(lblFile);
            curY += 28;

            Panel cardFile = CreateCardPanel(CARD_W, 162);
            cardFile.Location = new Point(0, curY);

            string curDir = !string.IsNullOrEmpty(AppSettings.SaveDirectory) ? AppSettings.SaveDirectory : (Context.NetService != null ? Context.NetService.SaveDirectory : "ReceivedFiles");
            bool autoAccept = Context.NetService != null ? Context.NetService.AutoAcceptFiles : AppSettings.AutoAcceptFiles;

            cardFile.Paint += (s, e) =>
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
                    g.DrawString("自动接收好友文件", f, tb, new PointF(20, 9));
                    g.DrawString("局域网内接收文件时无需手动逐一确认保存。", sf, sb, new PointF(20, 29));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2
                    g.DrawString("接收文件默认存储目录", f, tb, new PointF(20, 54 + 9));
                    string dispDir = !string.IsNullOrEmpty(AppSettings.SaveDirectory) ? AppSettings.SaveDirectory : (Context.NetService != null ? Context.NetService.SaveDirectory : "ReceivedFiles");
                    string shortDir = dispDir.Length > 44 ? "..." + dispDir.Substring(dispDir.Length - 41) : dispDir;
                    g.DrawString(shortDir, sf, sb, new PointF(20, 54 + 29));
                    g.DrawLine(sep, 16, 108, CARD_W - 16, 108);

                    // Row 3
                    g.DrawString("64KB 分块极速传输校验", f, tb, new PointF(20, 108 + 9));
                    g.DrawString("纯原生 LANC 协议流式流转，零中转零依赖。", sf, sb, new PointF(20, 108 + 29));
                }
            };

            ModernToggleSwitch swAuto = new ModernToggleSwitch(autoAccept, (val) =>
            {
                if (Context.NetService != null) Context.NetService.AutoAcceptFiles = val;
                AppSettings.AutoAcceptFiles = val;
                AppSettings.Save();
            });
            swAuto.Location = new Point(CARD_W - swAuto.Width - 24, (54 - 22) / 2);
            cardFile.Controls.Add(swAuto);

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
                        cardFile.Invalidate();
                    }
                }
            });
            btnChangeDir.Location = new Point(CARD_W - btnChangeDir.Width - 140, 54 + (54 - 32) / 2);
            cardFile.Controls.Add(btnChangeDir);

            Button btnOpenDir = CreateActionLink("打开文件夹 >", 110, 28, () =>
            {
                if (Context.OnOpenFolder != null) Context.OnOpenFolder();
            });
            btnOpenDir.Location = new Point(CARD_W - btnOpenDir.Width - 20, 54 + (54 - 32) / 2);
            cardFile.Controls.Add(btnOpenDir);

            ModernToggleSwitch swChunk = new ModernToggleSwitch(true, null);
            swChunk.Location = new Point(CARD_W - swChunk.Width - 24, 108 + (54 - 22) / 2);
            cardFile.Controls.Add(swChunk);

            this.Controls.Add(cardFile);
        }
    }
}
