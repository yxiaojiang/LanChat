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
    /// 聊天表情选择器，依赖本地表情目录和会话列表的自绘滚动。
    /// 弹层失焦即销毁，表情位图在进程内复用，只把可读文字交给输入框。
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
        private static readonly Lazy<Dictionary<string, Bitmap>> Images = new Lazy<Dictionary<string, Bitmap>>(LoadImages);
        private static readonly List<string> ImportedOrder = new List<string>();
        private const int Cell = 36;
        private readonly string[] _entries;
        private readonly SessionListBox _grid;
        private readonly ToolTip _tip = new ToolTip();
        private readonly int _columns;
        private int _hover = -1;
        private int _column;

        private ChatEmojiPicker(Control anchor, Action<string> select)
        {
            _entries = Images.Value.Count == 0 ? UnicodeEntries : ImportedOrder.ToArray();
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = GrokTheme.Level2;
            Padding = new Padding(8);

            Rectangle reference = anchor.RectangleToScreen(anchor.ClientRectangle);
            Rectangle work = Screen.FromRectangle(reference).WorkingArea;
            _columns = Math.Max(1, Math.Min(10, (work.Width - 30) / Cell));
            int rows = (_entries.Length + _columns - 1) / _columns;
            int above = Math.Max(0, reference.Top - work.Top - 6);
            int below = Math.Max(0, work.Bottom - reference.Bottom - 6);
            int visibleRows = Math.Max(1, Math.Min(rows, (Math.Max(above, below) - 16) / Cell));
            ClientSize = new Size(_columns * Cell + 28, visibleRows * Cell + 16);
            Location = new Point(Math.Max(work.Left, Math.Min(reference.Left, work.Right - Width)),
                above >= Height || above >= below ? Math.Max(work.Top, reference.Top - Height - 6) : reference.Bottom + 6);

            // 与应用其他浮层使用同一圆角和暗色滑块，网格行由 ListBox 处理滚轮与视口。
            using (GraphicsPath border = GrokTheme.RoundedRect(new Rectangle(0, 0, Width, Height), 16))
                Region = new Region(border);
            _grid = new SessionListBox
            {
                Dock = DockStyle.Fill, AccessibleName = "表情", ItemHeight = Cell,
                DrawMode = DrawMode.OwnerDrawFixed, IntegralHeight = false,
                BorderStyle = BorderStyle.None, BackColor = GrokTheme.Level2
            };
            for (int row = 0; row < rows; row++)
                _grid.Items.Add(string.Join(" ", _entries, row * _columns, Math.Min(_columns, _entries.Length - row * _columns)));
            _grid.DrawItem += DrawRow;
            _grid.MouseMove += (sender, e) =>
            {
                int index = FindCell(e.Location);
                if (index == _hover) return;
                _hover = index;
                _tip.SetToolTip(_grid, index >= 0 ? _entries[index] : null);
                _grid.Invalidate();
            };
            _grid.MouseLeave += (sender, e) => { _hover = -1; _grid.Invalidate(); };
            _grid.MouseClick += (sender, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                int index = FindCell(e.Location);
                if (index >= 0) select(_entries[index]);
            };
            _grid.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    select(_entries[Math.Min(_entries.Length - 1, Math.Max(0, _grid.SelectedIndex) * _columns + _column)]);
                }
                else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
                {
                    e.SuppressKeyPress = true;
                    int count = Math.Min(_columns, _entries.Length - Math.Max(0, _grid.SelectedIndex) * _columns);
                    _column = Math.Max(0, Math.Min(count - 1, _column + (e.KeyCode == Keys.Left ? -1 : 1)));
                    _grid.Invalidate();
                }
            };
            _grid.SelectedIndex = 0;
            Controls.Add(_grid);
            Shown += (sender, e) => _grid.Focus();
            KeyPreview = true;
            KeyDown += (sender, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            Deactivate += (sender, e) => Close();
            Disposed += (sender, e) => _tip.Dispose();
        }

        /// <summary>在工具按钮旁显示表情选择器，选中后关闭并通知输入框。</summary>
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

        /// <summary>查找本机导入的表情位图，未匹配时返回空。位图由进程持有。</summary>
        internal static Bitmap FindImage(string token)
        {
            Bitmap image;
            return Images.Value.TryGetValue(token, out image) ? image : null;
        }

        private static Dictionary<string, Bitmap> LoadImages()
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LanChat", "WeChatEmoji");
            var images = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
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
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(Path.Combine(directory, parts[1]));
                        if (bytes.Length > 128 * 1024) continue;
                        using (var stream = new MemoryStream(bytes))
                        using (Image image = Image.FromStream(stream))
                        {
                            if (image.Width <= 64 && image.Height <= 64)
                            {
                                images.Add(parts[0], new Bitmap(image));
                                ImportedOrder.Add(parts[0]);
                            }
                        }
                    }
                    catch (IOException) { }
                    catch (ArgumentException) { }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return images;
        }

        private int FindCell(Point location)
        {
            if (location.X < 0 || location.X >= _columns * Cell) return -1;
            int row = _grid.IndexFromPoint(location);
            int index = row * _columns + location.X / Cell;
            return row >= 0 && index < _entries.Length ? index : -1;
        }

        private void DrawRow(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            int first = e.Index * _columns;
            using (var background = new SolidBrush(GrokTheme.Level2))
            using (var hover = new SolidBrush(GrokTheme.Level3))
            using (var font = new Font("Segoe UI Emoji", 14f))
            {
                g.FillRectangle(background, e.Bounds);
                for (int i = 0; i < _columns && first + i < _entries.Length; i++)
                {
                    int index = first + i;
                    Rectangle cell = new Rectangle(i * Cell, e.Bounds.Top, Cell, Cell);
                    if (index == _hover || (_grid.Focused && e.Index == _grid.SelectedIndex &&
                        index == Math.Min(_entries.Length - 1, first + _column)))
                        MainForm.DrawRoundedRectangle(g, hover, Rectangle.Inflate(cell, -1, -1), 8);
                    Bitmap image = FindImage(_entries[index]);
                    if (image != null) g.DrawImage(image, Rectangle.Inflate(cell, -5, -5));
                    else TextRenderer.DrawText(g, _entries[index], font, cell, GrokTheme.TextPrimary,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
        }
    }
}
