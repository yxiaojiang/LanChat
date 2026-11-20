using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using LanChat;

/// <summary>
/// 从原生 LanChat 程序导出 Linux 桌面图标。依赖 WinForms 构建提供的 GDI+ 绘制器，
/// 仅在构建期运行，生成的透明 PNG 供 Electron 按逻辑尺寸显示。
/// </summary>
internal static class ExportLinuxDesktopAssets
{
    /// <summary>生成首包用到的图标，以三倍像素密度覆盖高 DPI。</summary>
    /// <param name="args">PNG 输出目录。</param>
    /// <returns>成功为 0，参数缺失为 2。写入错误由调用者处理。</returns>
    public static int Main(string[] args)
    {
        if (args.Length != 1) return 2;
        string output = Directory.CreateDirectory(args[0]).FullName;
        var muted = ColorTranslator.FromHtml("#A0A0A5");
        foreach (var icon in new[] { WeChatIconType.Search, WeChatIconType.Refresh, WeChatIconType.Logout,
            WeChatIconType.Add, WeChatIconType.UpArrow, WeChatIconType.Folder, WeChatIconType.Box,
            WeChatIconType.WinClose, WeChatIconType.WinMin, WeChatIconType.WinMax, WeChatIconType.WinRestore })
        {
            Draw(output, icon.ToString(), 20, (g, bounds) =>
                WeChatIcon.Draw(g, icon, bounds, false, icon == WeChatIconType.UpArrow ? Color.White : muted));
        }
        Draw(output, "avatar", 82, (g, bounds) =>
            WeChatIcon.DrawMascotAvatar(g, bounds, ColorTranslator.FromHtml("#FC2C3E"), false, true, ColorTranslator.FromHtml("#0E0E0E")));
        Draw(output, "peer", 48, (g, bounds) =>
            WeChatIcon.DrawMascotAvatar(g, bounds, ColorTranslator.FromHtml("#FF9F0A"), true, false, Color.Transparent));
        Draw(output, "group", 48, (g, bounds) =>
            WeChatIcon.DrawMascotAvatar(g, bounds, ColorTranslator.FromHtml("#8E64DC"), false, false, Color.Transparent));
        return 0;
    }

    private static void Draw(string output, string name, int size, Action<Graphics, Rectangle> render)
    {
        using (var bitmap = new Bitmap(size * 3, size * 3, PixelFormat.Format32bppArgb))
        using (var canvas = Graphics.FromImage(bitmap))
        {
            canvas.SmoothingMode = SmoothingMode.AntiAlias;
            canvas.PixelOffsetMode = PixelOffsetMode.HighQuality;
            canvas.ScaleTransform(3, 3);
            render(canvas, new Rectangle(0, 0, size, size));
            bitmap.Save(Path.Combine(output, name + ".png"), ImageFormat.Png);
        }
    }
}
