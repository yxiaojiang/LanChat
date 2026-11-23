using System;
using System.Drawing;

namespace LanChat
{
    /// <summary>
    /// 绘制聊天视口中的文件卡片，依赖 GrokTheme 和调用方提供的布局矩形。
    /// 仅在当前绘制帧使用画布，不保存传输状态或占用系统控件。
    /// </summary>
    internal static class ChatFileCardRenderer
    {
        /// <summary>按既有文件卡片尺寸绘制名称和大小，并显示进度或落盘后的操作。</summary>
        internal static void Draw(Graphics g, ChatMessage message, Rectangle bounds, Rectangle open, Rectangle folder)
        {
            using (var background = new SolidBrush(GrokTheme.PeerBubble))
            using (var border = new Pen(GrokTheme.Border, 1f))
            using (var badge = new SolidBrush(GrokTheme.AccentDeep))
            using (var nameFont = GrokTheme.UiFont(10.5f, FontStyle.Bold))
            using (var secondaryFont = GrokTheme.UiFont(9.5f))
            {
                MainForm.DrawRoundedRectangle(g, background, bounds, 20);
                MainForm.DrawRoundedRectangle(g, border, bounds, 20);
                var badgeBounds = new Rectangle(bounds.X + 16, bounds.Y + 16, 36, 36);
                MainForm.DrawRoundedRectangle(g, badge, badgeBounds, 10);
                WeChatIcon.Draw(g, WeChatIconType.Box, new Rectangle(badgeBounds.X + 8, badgeBounds.Y + 8, 20, 20), false, Color.White);

                int textWidth = Math.Max(1, bounds.Width - 78);
                GrokTheme.DrawUiTextLeft(g, message.FileName ?? "未知文件", nameFont, GrokTheme.TextPrimary, bounds.X + 62, bounds.Y + 12, textWidth, 23);
                GrokTheme.DrawUiTextLeft(g, FileTransferController.FormatFileSize(message.FileSize), secondaryFont, GrokTheme.TextSecondary,
                    bounds.X + 62, bounds.Y + 35, textWidth, 20);

                if (message.TransferProgress != null) DrawProgress(g, message.TransferProgress, bounds, secondaryFont);
                else
                {
                    using (var link = new SolidBrush(GrokTheme.AccentHot))
                    {
                        g.DrawString("打开文件", secondaryFont, link, open);
                        g.DrawString("打开目录", secondaryFont, link, folder);
                    }
                }
            }
        }

        private static void DrawProgress(Graphics g, FileTransferProgress progress, Rectangle bounds, Font font)
        {
            int percent = progress.IsCompleted ? 100 : progress.ProgressPercentage;
            string action = progress.IsSending ? "发送" : "接收";
            string status = progress.IsFailed ? action + "失败 · " + progress.ErrorMessage :
                action + (progress.IsCompleted ? "完成" : "中") + " · " + percent + "%";
            int width = Math.Max(1, bounds.Width - 36);
            GrokTheme.DrawUiTextLeft(g, status, font, progress.IsFailed ? GrokTheme.TextDestructive : GrokTheme.TextSecondary,
                bounds.X + 18, bounds.Y + 58, width, 19);

            // 自绘 4px 圆角轨道，完成后沿用同一矩形显示文件操作，不移动上下气泡。
            var track = new Rectangle(bounds.X + 18, bounds.Y + 80, width, 4);
            using (var background = new SolidBrush(GrokTheme.Level3Hot))
            using (var fill = new SolidBrush(progress.IsFailed ? GrokTheme.TextDestructive : GrokTheme.AccentHot))
            {
                MainForm.DrawRoundedRectangle(g, background, track, 2);
                int filled = (int)Math.Round(width * (percent / 100.0));
                if (filled >= 4) MainForm.DrawRoundedRectangle(g, fill, new Rectangle(track.X, track.Y, filled, track.Height), 2);
                else if (filled > 0) g.FillRectangle(fill, track.X, track.Y, filled, track.Height);
            }
        }
    }
}
