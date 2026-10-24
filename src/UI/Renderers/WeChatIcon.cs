using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace LanChat
{
    #region Vector Icon Definitions & Renderer

    /// <summary>
    /// 微信暗黑风格矢量图标类型定义
    /// </summary>
    public enum WeChatIconType
    {
        Chat,
        Contacts,
        Folder,
        Box,
        Refresh,
        Add,
        Menu,
        Emoji,
        Scissors,
        Clear,
        Search,
        Call,
        More
    }

    /// <summary>
    /// 微信暗黑矢量图标绘制引擎 (纯 GDI+ 矢量路径与抗锯齿绘制)
    /// </summary>
    public static class WeChatIcon
    {
        public static void Draw(Graphics g, WeChatIconType type, Rectangle bounds, bool active, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            switch (type)
            {
                case WeChatIconType.Chat:
                    DrawChatIcon(g, bounds, active, color);
                    break;
                case WeChatIconType.Contacts:
                    DrawContactsIcon(g, bounds, color);
                    break;
                case WeChatIconType.Folder:
                    DrawFolderIcon(g, bounds, color);
                    break;
                case WeChatIconType.Box:
                    DrawBoxIcon(g, bounds, color);
                    break;
                case WeChatIconType.Refresh:
                    DrawRefreshIcon(g, bounds, color);
                    break;
                case WeChatIconType.Add:
                    DrawAddIcon(g, bounds, color);
                    break;
                case WeChatIconType.Menu:
                    DrawMenuIcon(g, bounds, color);
                    break;
                case WeChatIconType.Emoji:
                    DrawEmojiIcon(g, bounds, color);
                    break;
                case WeChatIconType.Scissors:
                    DrawScissorsIcon(g, bounds, color);
                    break;
                case WeChatIconType.Clear:
                    DrawClearIcon(g, bounds, color);
                    break;
                case WeChatIconType.Search:
                    DrawSearchIcon(g, bounds, color);
                    break;
                case WeChatIconType.Call:
                    DrawCallIcon(g, bounds, color);
                    break;
                case WeChatIconType.More:
                    DrawMoreIcon(g, bounds, color);
                    break;
            }
        }

        // 1. WeChat Chat speech bubble icon (solid filled green when active, outlined otherwise)
        public static void DrawChatIcon(Graphics g, Rectangle r, bool active, Color color)
        {
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            float w = r.Width * 0.72f;
            float h = r.Height * 0.62f;

            using (GraphicsPath path = new GraphicsPath())
            {
                // Oval body
                path.AddArc(cx - w / 2f, cy - h / 2f, w, h, 200, 310);
                // Bottom left tail
                path.AddLine(cx - w * 0.38f, cy + h * 0.42f, cx - w * 0.54f, cy + h * 0.64f);
                path.AddLine(cx - w * 0.54f, cy + h * 0.64f, cx - w * 0.18f, cy + h * 0.46f);
                path.CloseFigure();

                if (active)
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(0, 163, 97)))
                    {
                        g.FillPath(b, path);
                    }
                }
                else
                {
                    using (Pen p = new Pen(color, 1.8f))
                    {
                        p.LineJoin = LineJoin.Round;
                        g.DrawPath(p, path);
                    }
                }
            }
        }

        // 2. WeChat Contacts icon (person profile + 3 horizontal lines, exactly per uploaded screenshot)
        public static void DrawContactsIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;

                float x0 = r.X + r.Width * 0.18f;
                float y0 = r.Y + r.Height * 0.18f;
                float s = r.Width * 0.64f;

                // Head circle
                g.DrawEllipse(p, x0 + s * 0.18f, y0 + s * 0.04f, s * 0.34f, s * 0.34f);

                // Shoulders arc
                g.DrawArc(p, x0 + s * 0.02f, y0 + s * 0.46f, s * 0.66f, s * 0.42f, 180, 180);

                // 3 horizontal contact lines on the right
                float lx1 = x0 + s * 0.76f;
                float lx2 = x0 + s * 1.05f;
                g.DrawLine(p, lx1, y0 + s * 0.16f, lx2, y0 + s * 0.16f);
                g.DrawLine(p, lx1, y0 + s * 0.36f, lx2, y0 + s * 0.36f);
                g.DrawLine(p, lx1, y0 + s * 0.56f, lx2 - s * 0.08f, y0 + s * 0.56f);
            }
        }

        // 3. Folder icon
        public static void DrawFolderIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.7f))
            {
                p.LineJoin = LineJoin.Round;
                float x = r.X + r.Width * 0.16f;
                float y = r.Y + r.Height * 0.22f;
                float w = r.Width * 0.68f;
                float h = r.Height * 0.56f;

                GraphicsPath path = new GraphicsPath();
                path.AddLine(x, y + 3, x + w * 0.35f, y + 3);
                path.AddLine(x + w * 0.42f, y + 6, x + w, y + 6);
                path.AddLine(x + w, y + h, x, y + h);
                path.CloseFigure();
                g.DrawPath(p, path);
            }
        }

        // 4. Cube / Box icon
        public static void DrawBoxIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.6f))
            {
                p.LineJoin = LineJoin.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float w = r.Width * 0.32f;
                float h = r.Height * 0.22f;

                PointF top = new PointF(cx, cy - h * 1.5f);
                PointF topRight = new PointF(cx + w, cy - h * 0.5f);
                PointF botRight = new PointF(cx + w, cy + h * 0.9f);
                PointF bot = new PointF(cx, cy + h * 1.8f);
                PointF botLeft = new PointF(cx - w, cy + h * 0.9f);
                PointF topLeft = new PointF(cx - w, cy - h * 0.5f);
                PointF center = new PointF(cx, cy);

                g.DrawPolygon(p, new PointF[] { top, topRight, botRight, bot, botLeft, topLeft });
                g.DrawLine(p, center, top);
                g.DrawLine(p, center, botRight);
                g.DrawLine(p, center, botLeft);
            }
        }

        // 5. Refresh / Radar icon (clockwise double arc arrows)
        public static void DrawRefreshIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float sz = r.Width * 0.32f;

                RectangleF rf = new RectangleF(cx - sz, cy - sz, sz * 2, sz * 2);
                g.DrawArc(p, rf, 40, 120);
                g.DrawArc(p, rf, 220, 120);

                // Arrows
                g.DrawLine(p, cx + sz * 0.7f, cy + sz * 0.3f, cx + sz * 0.7f, cy + sz * 0.8f);
                g.DrawLine(p, cx + sz * 0.7f, cy + sz * 0.8f, cx + sz * 0.2f, cy + sz * 0.8f);

                g.DrawLine(p, cx - sz * 0.7f, cy - sz * 0.3f, cx - sz * 0.7f, cy - sz * 0.8f);
                g.DrawLine(p, cx - sz * 0.7f, cy - sz * 0.8f, cx - sz * 0.2f, cy - sz * 0.8f);
            }
        }

        // 6. Plus / Add icon
        public static void DrawAddIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float len = r.Width * 0.22f;

                g.DrawLine(p, cx - len, cy, cx + len, cy);
                g.DrawLine(p, cx, cy - len, cx, cy + len);
            }
        }

        // 7. Menu Hamburger icon (3 horizontal lines)
        public static void DrawMenuIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 2.0f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float len = r.Width * 0.28f;

                g.DrawLine(p, cx - len, cy - 5, cx + len, cy - 5);
                g.DrawLine(p, cx - len, cy, cx + len, cy);
                g.DrawLine(p, cx - len, cy + 5, cx + len, cy + 5);
            }
        }

        // 8. Emoji smiley icon
        public static void DrawEmojiIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float rad = r.Width * 0.35f;

                // Face circle
                g.DrawEllipse(p, cx - rad, cy - rad, rad * 2, rad * 2);

                // Eyes
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.FillEllipse(b, cx - rad * 0.45f, cy - rad * 0.35f, 2.2f, 2.2f);
                    g.FillEllipse(b, cx + rad * 0.25f, cy - rad * 0.35f, 2.2f, 2.2f);
                }

                // Smile curve
                g.DrawArc(p, cx - rad * 0.5f, cy - rad * 0.2f, rad, rad * 0.8f, 20, 140);
            }
        }

        // 9. Scissors / Screenshot icon
        public static void DrawScissorsIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.5f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;

                // Two blades
                g.DrawLine(p, cx - 6, cy - 7, cx + 6, cy + 6);
                g.DrawLine(p, cx - 6, cy + 7, cx + 6, cy - 6);

                // Two handles
                g.DrawEllipse(p, cx - 9, cy - 9, 5, 5);
                g.DrawEllipse(p, cx - 9, cy + 4, 5, 5);
            }
        }

        // 10. Clear / Broom icon
        public static void DrawClearIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;

                // Handle
                g.DrawLine(p, cx + 5, cy - 6, cx - 1, cy);
                // Bristles
                g.DrawLine(p, cx - 5, cy + 5, cx + 3, cy - 1);
                g.DrawLine(p, cx - 6, cy + 6, cx - 1, cy + 7);
                g.DrawLine(p, cx - 6, cy + 6, cx - 7, cy + 2);
            }
        }

        // 11. Search Magnifying Glass
        public static void DrawSearchIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.5f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float x = r.X + 2;
                float y = r.Y + 2;
                g.DrawEllipse(p, x, y, 9, 9);
                g.DrawLine(p, x + 7.5f, y + 7.5f, x + 12f, y + 12f);
            }
        }

        // 12. Call Handset Icon
        public static void DrawCallIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.6f))
            {
                p.LineJoin = LineJoin.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                GraphicsPath path = new GraphicsPath();
                path.AddArc(cx - 6, cy - 6, 12, 12, 190, 80);
                g.DrawPath(p, path);
            }
        }

        // 13. More Dots Icon (···)
        public static void DrawMoreIcon(Graphics g, Rectangle r, Color color)
        {
            using (SolidBrush b = new SolidBrush(color))
            {
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                g.FillEllipse(b, cx - 6, cy - 1.5f, 3, 3);
                g.FillEllipse(b, cx - 1.5f, cy - 1.5f, 3, 3);
                g.FillEllipse(b, cx + 3f, cy - 1.5f, 3, 3);
            }
        }
    }

    #endregion
}
