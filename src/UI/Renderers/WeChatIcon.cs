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
        More,
        Mic,
        SoundWave,
        UpArrow,
        WinMin,
        WinMax,
        WinRestore,
        WinClose,
        User,
        Bolt,
        Settings,
        Logout
    }

    /// <summary>
    /// 矢量图标与 Grok Bot 吉祥物绘制引擎 (纯 GDI+ 矢量路径与抗锯齿绘制)
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
                case WeChatIconType.Mic:
                    DrawMicIcon(g, bounds, color);
                    break;
                case WeChatIconType.SoundWave:
                    DrawSoundWaveIcon(g, bounds, color);
                    break;
                case WeChatIconType.UpArrow:
                    DrawUpArrowIcon(g, bounds, color);
                    break;
                case WeChatIconType.WinMin:
                    DrawWinMinIcon(g, bounds, color);
                    break;
                case WeChatIconType.WinMax:
                    DrawWinMaxIcon(g, bounds, color);
                    break;
                case WeChatIconType.WinRestore:
                    DrawWinRestoreIcon(g, bounds, color);
                    break;
                case WeChatIconType.WinClose:
                    DrawWinCloseIcon(g, bounds, color);
                    break;
                case WeChatIconType.User:
                    DrawUserIcon(g, bounds, color);
                    break;
                case WeChatIconType.Bolt:
                    DrawBoltIcon(g, bounds, color);
                    break;
                case WeChatIconType.Settings:
                    DrawSettingsIcon(g, bounds, color);
                    break;
                case WeChatIconType.Logout:
                    DrawLogoutIcon(g, bounds, color);
                    break;
            }
        }

        /// <summary>
        /// 绘制截图同款 Grok/Bot 双眼灵动吉祥物头像（支持正圆或云朵有机轮廓 + 倾斜双胶囊眼）
        /// </summary>
        /// <summary>
        /// 绘制截图同款 Grok/Bot 双眼灵动吉祥物头像（支持正圆或云朵有机轮廓 + 倾斜双胶囊眼）
        /// </summary>
        public static void DrawMascotAvatar(Graphics g, Rectangle r, Color bodyColor, bool isOrganicBlob, bool showOnlineDot, Color bgForDot, string badgeTag = null)
        {
            DrawMascotAvatarShape(g, r, isOrganicBlob ? 1 : 0, bodyColor, showOnlineDot, bgForDot, badgeTag);
        }

        /// <summary>
        /// 绘制多样化有机吉祥物造型（完全对标 CreateNewBot.png 的 14 种有机形态）
        /// </summary>
        public static void DrawMascotAvatarShape(Graphics g, Rectangle r, int shapeIndex, Color bodyColor, bool showOnlineDot, Color bgForDot, string badgeTag = null)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float x = r.X;
            float y = r.Y;
            float w = r.Width;
            float h = r.Height;

            using (SolidBrush bodyBrush = new SolidBrush(bodyColor))
            {
                switch (shapeIndex)
                {
                    case 1: // 1. 云朵三连球 (对标 Karina / Alist)
                        g.FillEllipse(bodyBrush, x + w * 0.08f, y + h * 0.14f, w * 0.84f, h * 0.82f);
                        g.FillEllipse(bodyBrush, x + w * 0.02f, y + h * 0.04f, w * 0.62f, h * 0.56f);
                        g.FillEllipse(bodyBrush, x + w * 0.28f, y + h * 0.34f, w * 0.68f, h * 0.62f);
                        break;
                    case 2: // 2. 圆角方块 (Squircle)
                        using (GraphicsPath p2 = new GraphicsPath())
                        {
                            float rad = w * 0.32f;
                            p2.AddArc(x, y, rad * 2, rad * 2, 180, 90);
                            p2.AddArc(x + w - rad * 2, y, rad * 2, rad * 2, 270, 90);
                            p2.AddArc(x + w - rad * 2, y + h - rad * 2, rad * 2, rad * 2, 0, 90);
                            p2.AddArc(x, y + h - rad * 2, rad * 2, rad * 2, 90, 90);
                            p2.CloseFigure();
                            g.FillPath(bodyBrush, p2);
                        }
                        break;
                    case 3: // 3. 四角星 / 十字花
                        g.FillEllipse(bodyBrush, x + w * 0.05f, y + h * 0.26f, w * 0.90f, h * 0.48f);
                        g.FillEllipse(bodyBrush, x + w * 0.26f, y + h * 0.05f, w * 0.48f, h * 0.90f);
                        g.FillEllipse(bodyBrush, x + w * 0.20f, y + h * 0.20f, w * 0.60f, h * 0.60f);
                        break;
                    case 4: // 4. 爆米花 / 四叶果
                        g.FillEllipse(bodyBrush, x + w * 0.08f, y + h * 0.08f, w * 0.48f, h * 0.48f);
                        g.FillEllipse(bodyBrush, x + w * 0.44f, y + h * 0.06f, w * 0.50f, h * 0.50f);
                        g.FillEllipse(bodyBrush, x + w * 0.06f, y + h * 0.44f, w * 0.50f, h * 0.50f);
                        g.FillEllipse(bodyBrush, x + w * 0.42f, y + h * 0.42f, w * 0.52f, h * 0.52f);
                        g.FillEllipse(bodyBrush, x + w * 0.22f, y + h * 0.22f, w * 0.56f, h * 0.56f);
                        break;
                    case 5: // 5. 花生 / 双头豆
                        g.FillEllipse(bodyBrush, x + w * 0.05f, y + h * 0.15f, w * 0.52f, h * 0.62f);
                        g.FillEllipse(bodyBrush, x + w * 0.42f, y + h * 0.22f, w * 0.54f, h * 0.64f);
                        g.FillEllipse(bodyBrush, x + w * 0.25f, y + h * 0.24f, w * 0.50f, h * 0.50f);
                        break;
                    case 6: // 6. 不规则果冻 Blob
                        g.FillEllipse(bodyBrush, x + w * 0.08f, y + h * 0.18f, w * 0.72f, h * 0.76f);
                        g.FillEllipse(bodyBrush, x + w * 0.32f, y + h * 0.06f, w * 0.62f, h * 0.58f);
                        g.FillEllipse(bodyBrush, x + w * 0.16f, y + h * 0.12f, w * 0.76f, h * 0.70f);
                        break;
                    case 7: // 7. 倒水滴 / 瓜子
                        using (GraphicsPath p7 = new GraphicsPath())
                        {
                            p7.AddArc(x + w * 0.10f, y + h * 0.32f, w * 0.80f, h * 0.64f, 0, 180);
                            p7.AddBezier(x + w * 0.10f, y + h * 0.64f, x + w * 0.16f, y + h * 0.28f, x + w * 0.44f, y + h * 0.06f, x + w * 0.50f, y + h * 0.06f);
                            p7.AddBezier(x + w * 0.50f, y + h * 0.06f, x + w * 0.56f, y + h * 0.06f, x + w * 0.84f, y + h * 0.28f, x + w * 0.90f, y + h * 0.64f);
                            p7.CloseFigure();
                            g.FillPath(bodyBrush, p7);
                        }
                        break;
                    case 8: // 8. 扁鹅卵石 (Pebble)
                        g.FillEllipse(bodyBrush, x + w * 0.04f, y + h * 0.16f, w * 0.92f, h * 0.68f);
                        g.FillEllipse(bodyBrush, x + w * 0.16f, y + h * 0.12f, w * 0.68f, h * 0.72f);
                        break;
                    case 9: // 9. 圆角三角 (Rounded Triangle)
                        using (GraphicsPath p9 = new GraphicsPath())
                        {
                            float r9 = w * 0.24f;
                            p9.AddArc(x + w * 0.5f - r9, y + h * 0.08f, r9 * 2, r9 * 2, 210, 120);
                            p9.AddArc(x + w * 0.92f - r9 * 2, y + h * 0.88f - r9 * 2, r9 * 2, r9 * 2, 330, 120);
                            p9.AddArc(x + w * 0.08f, y + h * 0.88f - r9 * 2, r9 * 2, r9 * 2, 90, 120);
                            p9.CloseFigure();
                            g.FillPath(bodyBrush, p9);
                        }
                        break;
                    case 10: // 10. 倾斜梯形楔
                        using (GraphicsPath p10 = new GraphicsPath())
                        {
                            float r10 = w * 0.22f;
                            p10.AddArc(x + w * 0.24f, y + h * 0.08f, r10 * 2, r10 * 2, 180, 90);
                            p10.AddArc(x + w * 0.84f - r10 * 2, y + h * 0.14f, r10 * 2, r10 * 2, 270, 90);
                            p10.AddArc(x + w * 0.92f - r10 * 2, y + h * 0.86f - r10 * 2, r10 * 2, r10 * 2, 0, 90);
                            p10.AddArc(x + w * 0.10f, y + h * 0.82f - r10 * 2, r10 * 2, r10 * 2, 90, 90);
                            p10.CloseFigure();
                            g.FillPath(bodyBrush, p10);
                        }
                        break;
                    case 11: // 11. 六瓣花形
                        g.FillEllipse(bodyBrush, x + w * 0.08f, y + h * 0.28f, w * 0.84f, h * 0.44f);
                        g.FillEllipse(bodyBrush, x + w * 0.28f, y + h * 0.08f, w * 0.44f, h * 0.84f);
                        g.FillEllipse(bodyBrush, x + w * 0.14f, y + h * 0.14f, w * 0.72f, h * 0.72f);
                        break;
                    case 12: // 12. 倾斜石块
                        using (GraphicsPath p12 = new GraphicsPath())
                        {
                            float r12 = w * 0.26f;
                            p12.AddArc(x + w * 0.16f, y + h * 0.06f, r12 * 2, r12 * 2, 180, 90);
                            p12.AddArc(x + w * 0.86f - r12 * 2, y + h * 0.20f, r12 * 2, r12 * 2, 270, 90);
                            p12.AddArc(x + w * 0.80f - r12 * 2, y + h * 0.88f - r12 * 2, r12 * 2, r12 * 2, 0, 90);
                            p12.AddArc(x + w * 0.06f, y + h * 0.74f - r12 * 2, r12 * 2, r12 * 2, 90, 90);
                            p12.CloseFigure();
                            g.FillPath(bodyBrush, p12);
                        }
                        break;
                    case 13: // 13. 南瓜 Pumpkin (对标截图带把手橘南瓜)
                        // 顶部小绿把手
                        using (Pen stemPen = new Pen(Color.FromArgb(90, 160, 60), Math.Max(2f, w * 0.08f)))
                        {
                            stemPen.StartCap = LineCap.Round;
                            stemPen.EndCap = LineCap.Round;
                            g.DrawLine(stemPen, x + w * 0.50f, y + h * 0.18f, x + w * 0.52f, y + h * 0.04f);
                        }
                        // 三条南瓜瓣椭圆
                        g.FillEllipse(bodyBrush, x + w * 0.04f, y + h * 0.20f, w * 0.56f, h * 0.74f);
                        g.FillEllipse(bodyBrush, x + w * 0.40f, y + h * 0.20f, w * 0.56f, h * 0.74f);
                        g.FillEllipse(bodyBrush, x + w * 0.18f, y + h * 0.16f, w * 0.64f, h * 0.80f);
                        break;
                    default: // 0. 标准正圆
                        g.FillEllipse(bodyBrush, r);
                        break;
                }
            }

            // 绘制标志性的两颗倾斜黑色胶囊灵动眼睛（向右下微看，与截图 Karina / Alist迅雷 完全一致）
            GraphicsState state = g.Save();
            float cx = r.X + r.Width * 0.54f;
            float cy = r.Y + r.Height * (shapeIndex == 13 ? 0.55f : 0.48f);
            g.TranslateTransform(cx, cy);
            g.RotateTransform(-24f);

            float eyeW = Math.Max(2.2f, r.Width * 0.115f);
            float eyeH = Math.Max(5.0f, r.Height * 0.28f);
            float eyeGap = Math.Max(2.2f, r.Width * 0.09f);

            Color eyeColor = (bodyColor.R > 220 && bodyColor.G > 220 && bodyColor.B > 220) ? Color.FromArgb(20, 20, 22) : Color.FromArgb(16, 16, 18);
            using (Pen eyePen = new Pen(eyeColor, eyeW))
            {
                eyePen.StartCap = LineCap.Round;
                eyePen.EndCap = LineCap.Round;
                float halfLen = (eyeH - eyeW) / 2f;
                // 左眼
                g.DrawLine(eyePen, -eyeGap, -halfLen, -eyeGap, halfLen);
                // 右眼（略微偏下一点点增加灵动感）
                g.DrawLine(eyePen, eyeGap, -halfLen + 0.5f, eyeGap, halfLen + 0.5f);
            }
            g.Restore(state);

            // 可选左上角微型黑底徽章（对应截图第4项左上角的圆形小标）
            if (!string.IsNullOrEmpty(badgeTag))
            {
                int bw = Math.Max(15, (int)(r.Width * 0.48f));
                Rectangle badgeRect = new Rectangle(r.X - 2, r.Y - 1, bw, bw);
                using (SolidBrush bBg = new SolidBrush(Color.FromArgb(24, 24, 26)))
                using (Pen bPen = new Pen(Color.FromArgb(55, 55, 58), 1f))
                {
                    g.FillEllipse(bBg, badgeRect);
                    g.DrawEllipse(bPen, badgeRect);
                }
                using (Font bf = new Font("Microsoft YaHei", 5.0f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(210, 210, 210)))
                {
                    StringFormat sf = new StringFormat(StringFormatFlags.NoWrap) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(badgeTag, bf, tb, badgeRect, sf);
                }
            }

            // 右下角状态蓝点（对应截图 Karina 红色头像右下角的 #4D9BFF 蓝点）
            if (showOnlineDot)
            {
                int dotSize = Math.Max(8, (int)(r.Width * 0.25f));
                int dotX = r.Right - dotSize - 1;
                int dotY = r.Bottom - dotSize - 1;
                using (SolidBrush ringBrush = new SolidBrush(bgForDot))
                using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(77, 155, 255)))
                {
                    g.FillEllipse(ringBrush, dotX - 2, dotY - 2, dotSize + 4, dotSize + 4);
                    g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                }
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
            using (Pen p = new Pen(color, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float rad = Math.Min(r.Width, r.Height) * 0.20f;
                if (rad < 3.5f) rad = 3.5f;
                g.DrawEllipse(p, cx - rad - 1.2f, cy - rad - 1.2f, rad * 2f, rad * 2f);
                g.DrawLine(p, cx + rad * 0.5f - 1f, cy + rad * 0.5f - 1f, cx + rad * 1.35f, cy + rad * 1.35f);
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

        // 14. Microphone Icon (Grok bottom input bar)
        public static void DrawMicIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.6f))
            using (SolidBrush b = new SolidBrush(color))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;

                // Capsule head
                Rectangle headRect = new Rectangle((int)(cx - 2.5f), (int)(cy - 6.5f), 5, 8);
                MainForm.DrawRoundedRectangle(g, b, headRect, 2);

                // U-cup arc
                g.DrawArc(p, cx - 5f, cy - 4.5f, 10f, 8.5f, 0, 180);

                // Bottom stand
                g.DrawLine(p, cx, cy + 4f, cx, cy + 6.5f);
            }
        }

        // 15. SoundWave Icon (Grok bottom-right purple circle button)
        public static void DrawSoundWaveIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;

                g.DrawLine(p, cx - 5.5f, cy - 2f, cx - 5.5f, cy + 2f);
                g.DrawLine(p, cx - 2.0f, cy - 5.5f, cx - 2.0f, cy + 5.5f);
                g.DrawLine(p, cx + 1.8f, cy - 3.8f, cx + 1.8f, cy + 3.8f);
                g.DrawLine(p, cx + 5.3f, cy - 1.8f, cx + 5.3f, cy + 1.8f);
            }
        }

        // 16. Up Arrow Icon (Grok signature upward arrow ↑ for send button)
        public static void DrawUpArrowIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 2.2f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;

                // Vertical stem
                g.DrawLine(p, cx, cy + 6.0f, cx, cy - 6.0f);
                // 90-degree arrowhead
                g.DrawLines(p, new PointF[] {
                    new PointF(cx - 5.0f, cy - 1.2f),
                    new PointF(cx, cy - 6.2f),
                    new PointF(cx + 5.0f, cy - 1.2f)
                });
            }
        }

        // 17. Frameless Window Minimize Icon (—，对标 Alis3.png: 13px 宽水平细线)
        public static void DrawWinMinIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.2f))
            {
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                g.DrawLine(p, cx - 6.5f, cy, cx + 6.5f, cy);
            }
        }

        // 18. Frameless Window Maximize Icon (□，对标 Alis3.png: 13x13px 空心正方形)
        public static void DrawWinMaxIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.2f))
            {
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                g.DrawRectangle(p, cx - 6.5f, cy - 6.5f, 13f, 13f);
            }
        }

        // 18b. Frameless Window Restore Icon (双层重叠方块，对标还原态)
        public static void DrawWinRestoreIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.2f))
            {
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                // 后层方块
                g.DrawRectangle(p, cx - 3.5f, cy - 6.5f, 10f, 10f);
                // 前层方块底色遮罩与边框
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(18, 18, 18)))
                {
                    g.FillRectangle(bg, cx - 6.5f, cy - 3.5f, 10f, 10f);
                }
                g.DrawRectangle(p, cx - 6.5f, cy - 3.5f, 10f, 10f);
            }
        }

        // 19. Frameless Window Close Icon (×，对标 Alis3.png: 13x13px 交叉细线)
        public static void DrawWinCloseIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.2f))
            {
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                g.DrawLine(p, cx - 6.5f, cy - 6.5f, cx + 6.5f, cy + 6.5f);
                g.DrawLine(p, cx + 6.5f, cy - 6.5f, cx - 6.5f, cy + 6.5f);
            }
        }

        // 20. Menu User / Nick Icon
        public static void DrawUserIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.4f))
            {
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                g.DrawEllipse(p, cx - 3.5f, cy - 7f, 7f, 7f);
                g.DrawArc(p, cx - 7f, cy - 1f, 14f, 13f, 200, 140);
            }
        }

        // 21. Menu Bolt / Flash Icon
        public static void DrawBoltIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.4f))
            {
                p.LineJoin = LineJoin.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                PointF[] pts = new PointF[] {
                    new PointF(cx + 1f, cy - 7f),
                    new PointF(cx - 5f, cy),
                    new PointF(cx, cy),
                    new PointF(cx - 1f, cy + 7f),
                    new PointF(cx + 5f, cy),
                    new PointF(cx, cy)
                };
                g.DrawPolygon(p, pts);
            }
        }

        // 22. Menu Settings / Gear Icon
        public static void DrawSettingsIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.4f))
            {
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                g.DrawEllipse(p, cx - 3f, cy - 3f, 6f, 6f);
                g.DrawEllipse(p, cx - 6f, cy - 6f, 12f, 12f);
            }
        }

        // 23. Menu Logout / Door Icon
        public static void DrawLogoutIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.4f))
            {
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                g.DrawLines(p, new PointF[] {
                    new PointF(cx - 1f, cy - 6f),
                    new PointF(cx + 6f, cy - 6f),
                    new PointF(cx + 6f, cy + 6f),
                    new PointF(cx - 1f, cy + 6f)
                });
                g.DrawLine(p, cx + 2f, cy, cx - 6f, cy);
                g.DrawLine(p, cx - 3f, cy - 3f, cx - 6f, cy);
                g.DrawLine(p, cx - 3f, cy + 3f, cx - 6f, cy);
            }
        }
    }

    #endregion
}
