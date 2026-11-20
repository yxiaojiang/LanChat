using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using LanChat;

/// <summary>
/// 离线生成 Tk 原型使用的透明 PNG，依赖 Windows GDI+ 与现有图标几何。
/// 原型资源独立于客户端发布构建。
/// </summary>
internal static class ExportAssets
{
    /// <summary>按两种强调色和四种缩放导出圆钮、胶囊、浮动滑块、气泡与边角。</summary>
    /// <param name="args">资源输出目录。</param>
    /// <returns>成功时返回 0，参数缺失时返回 2。</returns>
    public static int Main(string[] args)
    {
        if (args.Length != 1) return 2;
        foreach (int percent in new[] { 100, 125, 150, 200 })
        {
            foreach (string accent in new[] { "8E64DC", "5C9CFF" })
            {
                string output = Path.Combine(args[0], percent.ToString(CultureInfo.InvariantCulture), accent);
                Directory.CreateDirectory(output);
                float scale = percent / 100f;
                Color color = ColorTranslator.FromHtml("#" + accent);
                Draw(output, "send", 44, 44, 22, color, scale, WeChatIconType.UpArrow);
                Draw(output, "attach", 36, 36, 18, ColorTranslator.FromHtml("#28282C"), scale, WeChatIconType.Add);
                Draw(output, "pill", 180, 64, 32, ColorTranslator.FromHtml("#202024"), scale, WeChatIconType.None);
                Draw(output, "thumb", 4, 48, 2, Color.FromArgb(204, 56, 56, 64), scale, WeChatIconType.None);
                Draw(output, "thumb-hover", 5, 48, 2.5f, ColorTranslator.FromHtml("#70707C"), scale, WeChatIconType.None);
                Draw(output, "bubble", 160, 38, 14, color, scale, WeChatIconType.None);
                Draw(output, "corner", 28, 28, 14, color, scale, WeChatIconType.None);
            }
        }
        return 0;
    }

    private static void Draw(string output, string name, int width, int height, float radius, Color fill,
                             float scale, WeChatIconType icon)
    {
        // 透明位图按逻辑尺寸和缩放计算，GDI+ 对分数像素边缘做抗锯齿采样。
        using (var bitmap = new Bitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale),
                                       PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (var path = new GraphicsPath())
        using (var brush = new SolidBrush(fill))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.ScaleTransform(scale, scale);
            float diameter = radius * 2;
            path.AddArc(0, 0, diameter, diameter, 180, 90);
            path.AddArc(width - diameter, 0, diameter, diameter, 270, 90);
            path.AddArc(width - diameter, height - diameter, diameter, diameter, 0, 90);
            path.AddArc(0, height - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            graphics.FillPath(brush, path);
            if (icon != WeChatIconType.None)
            {
                int size = icon == WeChatIconType.UpArrow ? 22 : 20;
                WeChatIcon.Draw(graphics, icon, new Rectangle((width - size) / 2, (height - size) / 2, size, size),
                                false, ColorTranslator.FromHtml("#F2F2F6"));
            }
            bitmap.Save(Path.Combine(output, name + ".png"), ImageFormat.Png);
        }
    }
}
