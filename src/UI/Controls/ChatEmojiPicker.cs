using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 微信比例的聊天表情选择器。
    /// 依赖 SessionListBox 的暗色自绘滚动模型与 EmojiPack；弹层失焦即销毁，位图由进程缓存持有。
    /// 视觉边界是选择与预览，不改变网络消息格式，仍向输入模型写入可读表情令牌。
    /// </summary>
    internal sealed class ChatEmojiPicker : Form
    {
        private static readonly string[] UnicodeEntries =
        {
            "😊", "😃", "😄", "😁", "😆", "😅", "😂", "🤣", "😇", "😉",
            "😋", "😍", "🥰", "😘", "😜", "🤔", "🤫", "🤐", "😴", "😷",
            "🥳", "🤩", "🥺", "😭", "😱", "😨", "😰", "😡", "🤬", "🤡",
            "👍", "👎", "👏", "🙏", "🤝", "🎉", "🔥", "❤️", "💯", "✨",
            "💪", "✌️", "🖐️", "✋", "👊", "☕", "💻", "📱", "🍺", "🎁"
        };
        private static readonly Lazy<Dictionary<string, Bitmap>> Images =
            new Lazy<Dictionary<string, Bitmap>>(LoadImages, true);
        private static readonly List<string> ImportedOrder = new List<string>();
        private static readonly List<string> RecentOrder = new List<string>();
        private const int Cell = 48;
        private const int Columns = 8;
        private const int RecentCount = 12;
        private const int PickerWidth = 496;
        private const int PickerHeight = 548;
        private const int GridSliderReserve = 16;
        private readonly string[] _entries;
        private readonly string[] _recentEntries;
        private readonly SessionListBox _recentGrid;
        private readonly SessionListBox _allGrid;
        private readonly ToolTip _tip = new ToolTip();
        private int _recentHover = -1;
        private int _allHover = -1;
        private int _recentColumn;
        private int _allColumn;

        private ChatEmojiPicker(Control anchor, Action<string> select)
        {
            _entries = Images.Value.Count == 0 ? UnicodeEntries : ImportedOrder.ToArray();
            _recentEntries = BuildRecentEntries(_entries);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = GrokTheme.Level2;
            ClientSize = new Size(PickerWidth, PickerHeight);
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

            Rectangle reference = anchor.RectangleToScreen(anchor.ClientRectangle);
            Rectangle work = Screen.FromRectangle(reference).WorkingArea;
            int width = Math.Min(ClientSize.Width, Math.Max(Columns * 40 + 24, work.Width - 20));
            ClientSize = new Size(width, Math.Min(ClientSize.Height, Math.Max(360, work.Height - 20)));
            using (GraphicsPath border = GrokTheme.RoundedRect(new Rectangle(0, 0, Width, Height), 16))
                Region = new Region(border);

            Controls.Add(CreateTitleBar());
            Controls.Add(CreateSectionLabel("最近使用", 52));
            _recentGrid = CreateGrid(_recentEntries);
            _recentGrid.Location = new Point(12, 80);
            _recentGrid.Size = new Size(Columns * Cell + GridSliderReserve, Math.Max(Cell, Rows(_recentEntries) * Cell));
            Controls.Add(_recentGrid);

            int allTitleTop = _recentGrid.Bottom + 4;
            Controls.Add(CreateSectionLabel("所有表情", allTitleTop));
            _allGrid = CreateGrid(_entries);
            _allGrid.Location = new Point(12, allTitleTop + 28);
            _allGrid.Size = new Size(Columns * Cell + GridSliderReserve, Math.Max(Cell, Height - _allGrid.Top - 12));
            Controls.Add(_allGrid);

            AttachGrid(_recentGrid, _recentEntries, false, select);
            AttachGrid(_allGrid, _entries, true, select);
            Shown += (sender, e) => _allGrid.Focus();
            KeyPreview = true;
            KeyDown += (sender, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            Deactivate += (sender, e) => Close();
            Disposed += (sender, e) => _tip.Dispose();

            Location = new Point(Math.Max(work.Left, Math.Min(reference.Left, work.Right - Width)),
                reference.Top - Height - 8 >= work.Top ? reference.Top - Height - 8 :
                Math.Min(work.Bottom - Height, reference.Bottom + 8));
        }

        /// <summary>为无边框弹层启用 16px 圆角阴影，并与系统窗口合成保持一致。</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ClassStyle |= 0x00020000;
                return parameters;
            }
        }

        /// <summary>绘制暗色底板和 1px #2C2C32 外边框，边缘由窗口 Region 裁剪。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var border = new Pen(GrokTheme.Border, 1f))
            using (GraphicsPath path = GrokTheme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 16))
                e.Graphics.DrawPath(border, path);
        }

        /// <summary>选择器失去焦点时重绘悬停和选中态，避免暗色状态残留。</summary>
        protected override void OnDeactivate(EventArgs e)
        {
            if (_recentGrid != null) _recentGrid.Invalidate();
            if (_allGrid != null) _allGrid.Invalidate();
            base.OnDeactivate(e);
        }

        /// <summary>在输入工具按钮旁显示选择器，并在点击后回调可传输的表情令牌。</summary>
        internal static void Open(Form owner, Control anchor, Action<string> onPick)
        {
            ChatEmojiPicker popup = null;
            popup = new ChatEmojiPicker(anchor, token =>
            {
                popup.Close();
                onPick(token);
            });
            popup.Show(owner);
        }

        /// <summary>查找内置或兼容旧版外部目录中的表情位图；位图由进程缓存持有。</summary>
        internal static Bitmap FindImage(string token)
        {
            Bitmap image;
            return Images.Value.TryGetValue(token, out image) ? image : null;
        }

        /// <summary>记录最近选择的令牌，保持进程内真实最近使用顺序并去除重复项。</summary>
        private static void RecordRecent(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            RecentOrder.Remove(token);
            RecentOrder.Insert(0, token);
            if (RecentOrder.Count > RecentCount) RecentOrder.RemoveRange(RecentCount, RecentOrder.Count - RecentCount);
        }

        private static string[] BuildRecentEntries(string[] entries)
        {
            var available = new HashSet<string>(entries, StringComparer.Ordinal);
            var result = new List<string>();
            foreach (string token in RecentOrder)
            {
                if (available.Contains(token)) result.Add(token);
                if (result.Count == RecentCount) break;
            }
            return result.ToArray();
        }

        private static Dictionary<string, Bitmap> LoadImages()
        {
            var images = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
            foreach (EmojiPackEntry entry in EmojiPack.GetEntries()) AddImage(images, entry.Token, entry.Png);
            if (images.Count > 0) return images;

            // 兼容旧版导入目录：没有内置资源时仍允许开发版和旧安装包显示表情。
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LanChat", "WeChatEmoji");
            string manifest = Path.Combine(directory, "manifest.tsv");
            if (!File.Exists(manifest)) return images;
            try
            {
                if (new FileInfo(manifest).Length > 16 * 1024) return images;
                foreach (string line in File.ReadLines(manifest))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length != 2 || !Regex.IsMatch(parts[0], @"^\[[^\[\]\s]{1,24}\]$") ||
                        !Regex.IsMatch(parts[1], @"^[0-9]{3}\.png$") || images.ContainsKey(parts[0])) continue;
                    try { AddImage(images, parts[0], File.ReadAllBytes(Path.Combine(directory, parts[1]))); }
                    catch (IOException) { }
                    catch (ArgumentException) { }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return images;
        }

        private static void AddImage(Dictionary<string, Bitmap> images, string token, byte[] bytes)
        {
            if (images.ContainsKey(token) || bytes == null || bytes.Length > 128 * 1024) return;
            using (var stream = new MemoryStream(bytes, false))
            using (Image image = Image.FromStream(stream))
            {
                if (image.Width > 64 || image.Height > 64) return;
                images.Add(token, new Bitmap(image));
                ImportedOrder.Add(token);
            }
        }

        private Panel CreateTitleBar()
        {
            var title = new Panel { Location = new Point(0, 0), Size = new Size(Width, 52), BackColor = GrokTheme.Level2 };
            title.Paint += (sender, e) =>
            {
                using (Font font = GrokTheme.UiFont(10.5f, FontStyle.Bold))
                    GrokTheme.DrawUiTextLeft(e.Graphics, "表情", font, GrokTheme.TextPrimary, 16, 0, 120, title.Height);
                using (Pen pen = new Pen(GrokTheme.Border, 1f)) e.Graphics.DrawLine(pen, 12, title.Height - 1, title.Width - 12, title.Height - 1);
            };
            return title;
        }

        private Panel CreateSectionLabel(string text, int top)
        {
            var label = new Panel { Location = new Point(12, top), Size = new Size(Width - 24, 28), BackColor = GrokTheme.Level2 };
            label.Paint += (sender, e) =>
            {
                using (Font font = GrokTheme.UiFont(9.5f))
                    GrokTheme.DrawUiTextLeft(e.Graphics, text, font, GrokTheme.TextSecondary, 4, 0, label.Width - 8, label.Height);
            };
            return label;
        }

        private SessionListBox CreateGrid(string[] entries)
        {
            var grid = new SessionListBox
            {
                ItemHeight = Cell, DrawMode = DrawMode.OwnerDrawFixed, IntegralHeight = false,
                BorderStyle = BorderStyle.None, BackColor = GrokTheme.Level2, ForeColor = GrokTheme.TextPrimary,
                Tag = entries, AccessibleName = "表情网格"
            };
            for (int row = 0; row < Rows(entries); row++)
                grid.Items.Add(string.Join(" ", entries, row * Columns, Math.Min(Columns, entries.Length - row * Columns)));
            return grid;
        }

        private void AttachGrid(SessionListBox grid, string[] entries, bool all, Action<string> select)
        {
            grid.DrawItem += DrawRow;
            grid.MouseMove += (sender, e) =>
            {
                int index = FindCell(grid, entries, e.Location);
                if (all ? index == _allHover : index == _recentHover) return;
                if (all) _allHover = index; else _recentHover = index;
                _tip.SetToolTip(grid, index >= 0 ? entries[index] : null);
                grid.Invalidate();
            };
            grid.MouseLeave += (sender, e) => { if (all) _allHover = -1; else _recentHover = -1; grid.Invalidate(); };
            grid.MouseClick += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    int index = FindCell(grid, entries, e.Location);
                    if (index >= 0)
                    {
                        RecordRecent(entries[index]);
                        select(entries[index]);
                    }
                }
            };
            grid.KeyDown += (sender, e) =>
            {
                int column = all ? _allColumn : _recentColumn;
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    int index = Math.Min(entries.Length - 1, Math.Max(0, grid.SelectedIndex) * Columns + column);
                    if (index >= 0)
                    {
                        RecordRecent(entries[index]);
                        select(entries[index]);
                    }
                }
                else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
                {
                    e.SuppressKeyPress = true;
                    int count = Math.Min(Columns, entries.Length - Math.Max(0, grid.SelectedIndex) * Columns);
                    column = Math.Max(0, Math.Min(count - 1, column + (e.KeyCode == Keys.Left ? -1 : 1)));
                    if (all) _allColumn = column; else _recentColumn = column;
                    grid.Invalidate();
                }
            };
            grid.LostFocus += (sender, e) => grid.Invalidate();
            grid.GotFocus += (sender, e) => grid.Invalidate();
            if (entries.Length > 0) grid.SelectedIndex = 0;
        }

        private int FindCell(SessionListBox grid, string[] entries, Point location)
        {
            if (location.X < 0 || location.X >= Columns * Cell) return -1;
            int row = grid.IndexFromPoint(location);
            int index = row * Columns + location.X / Cell;
            return row >= 0 && index < entries.Length ? index : -1;
        }

        private void DrawRow(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var grid = (SessionListBox)sender;
            string[] entries = (string[])grid.Tag;
            int hover = grid == _allGrid ? _allHover : _recentHover;
            int column = grid == _allGrid ? _allColumn : _recentColumn;
            int first = e.Index * Columns;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            using (var background = new SolidBrush(GrokTheme.Level2))
            using (var selected = new SolidBrush(GrokTheme.Level3))
            using (var font = new Font("Segoe UI Emoji", 16f))
            {
                e.Graphics.FillRectangle(background, e.Bounds);
                for (int i = 0; i < Columns && first + i < entries.Length; i++)
                {
                    int index = first + i;
                    Rectangle cell = new Rectangle(i * Cell, e.Bounds.Top, Cell, Cell);
                    if (index == hover || (grid.Focused && e.Index == grid.SelectedIndex && index == Math.Min(entries.Length - 1, first + column)))
                        MainForm.DrawRoundedRectangle(e.Graphics, selected, Rectangle.Inflate(cell, -2, -2), 10);
                    Bitmap image = FindImage(entries[index]);
                    if (image != null) e.Graphics.DrawImage(image, Rectangle.Inflate(cell, -8, -8));
                    else TextRenderer.DrawText(e.Graphics, entries[index], font, cell, GrokTheme.TextPrimary,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
        }

        private static int Rows(string[] entries)
        {
            return Math.Max(1, (entries.Length + Columns - 1) / Columns);
        }
    }
}
