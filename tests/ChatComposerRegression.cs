using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 在隔离回环中验证微信表情文字、视觉行和 PNG 附件，依赖 WinForms、GDI+ 与现有网络服务。
    /// 使用独立输出目录和内存剪贴板对象，运行后退出。
    /// </summary>
    internal static class ChatComposerRegression
    {
        private const string Content = "甲👨‍👩‍👧‍👦👍🏽🇨🇳❤️1️⃣\r\n[敲打]乙";

        /// <summary>运行显示、剪贴板和 TCP 图文及历史回归。</summary>
        /// <param name="args">输出目录与是否要求已导入本机微信表情。</param>
        /// <returns>全部通过时返回 0。断言失败由测试进程报告。</returns>
        [STAThread]
        public static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            VerifyLayout(bool.Parse(args[1]));
            VerifyEmojiPaint();
            VerifyClipboardPriority();
            VerifyAppearancePaletteAndTransferRate();
            string source = SaveClipboardImage(args[0]);
            VerifyLoopback(args[0], source);
            Console.WriteLine("聊天表情布局、PNG 与位图粘贴数据、LANC 文本和图片回环及历史回读通过");
            VerifyManagedTransfers(args[0], source);
            return 0;
        }

        private static void VerifyLayout(bool expectPack)
        {
            string[] graphemes = { "甲", "👨‍👩‍👧‍👦", "👍🏽", "🇨🇳", "❤️", "1️⃣" };
            int start = 0;
            foreach (string grapheme in graphemes)
            {
                int next = ChatTextLayout.NextBoundary(Content, start);
                Require(next - start == grapheme.Length, "表情被分在多个可见元素中");
                start = next;
            }
            Require(ChatTextLayout.NextBoundary("[不存在]", 0) == 1, "未知表情名称应保留为文字");
            using (var canvas = new Bitmap(240, 80))
            using (Graphics graphics = Graphics.FromImage(canvas))
            using (Font font = GrokTheme.UiFont(11f))
            {
                var lines = ChatTextLayout.BreakLines(graphics, Content, font, 4);
                foreach (var line in lines)
                    Require(Content.Substring(line.StartChar, line.Length) == line.Text, "视觉行索引与原文不一致");
                Require(lines[1].Text == graphemes[1] && lines[5].Text == graphemes[5], "狭窄折行拆开了组合表情");
                Require(lines[6].StartChar == Content.IndexOf('['), "CRLF 后文字索引错误");
                Require(ChatTextLayout.Measure(graphics, "😀", font) > 0, "Unicode 表情宽度为零");
                ChatTextLayout.Draw(graphics, "😀", font, Brushes.White, 0, 0);
                Require(Enumerable.Range(0, 35 * 35).Any(i => canvas.GetPixel(i % 35, i / 35).A > 0), "Unicode 表情没有绘制");
                if (expectPack)
                {
                    var image = ChatEmojiPicker.FindImage("[敲打]");
                    Require(image != null && image.Width == 64 && image.Height == 64, "未加载本机微信敲打原图");
                    Require(lines[6].Text == "[敲打]", "狭窄折行拆开了常用微信表情");
                    ChatTextLayout.Draw(graphics, "[敲打]", font, Brushes.White, 40, 0);
                    Require(Enumerable.Range(0, 35 * 35).Any(i =>
                    {
                        Color pixel = canvas.GetPixel(40 + i % 35, i / 35);
                        return pixel.A > 100 && pixel.R > pixel.B + 50;
                    }), "未绘制彩色微信表情");
                }
            }
        }

        private static void VerifyEmojiPaint()
        {
            const float x = 5;
            const float y = 9;
            const string prefix = "AV甲 ";
            const string suffix = " 乙";
            using (var actual = new Bitmap(240, 60))
            using (var expected = new Bitmap(actual.Width, actual.Height))
            using (Graphics graphics = Graphics.FromImage(actual))
            using (Graphics reference = Graphics.FromImage(expected))
            using (Font font = GrokTheme.UiFont(11f))
            using (var emojiFont = new Font("Segoe UI Emoji", font.SizeInPoints))
            using (StringFormat format = StringFormat.GenericTypographic)
            {
                Require(emojiFont.Name == "Segoe UI Emoji", "回归需要系统 Segoe UI Emoji 字体");
                format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                float prefixWidth = reference.MeasureString(prefix, font, 10000, format).Width;
                float suffixWidth = reference.MeasureString(suffix, font, 10000, format).Width;
                float emojiY = y + (font.GetHeight(reference) - emojiFont.GetHeight(reference)) / 2f;
                foreach (string emoji in new[] { "✋🏽", "✌🏽", "❤‍🔥", "✋", "✨", "☕" })
                {
                    Require(ChatTextLayout.NextBoundary(emoji, 0) == emoji.Length, "表情边界拆分：" + emoji);
                    float emojiWidth = reference.MeasureString(emoji, emojiFont, 10000, format).Width;
                    Require(Math.Abs(ChatTextLayout.Measure(graphics, emoji, font) - emojiWidth) < 0.01f,
                        "表情测量未使用整段 Emoji 字体：" + emoji);
                    string mixed = prefix + emoji + suffix;
                    graphics.Clear(Color.Transparent);
                    reference.Clear(Color.Transparent);
                    float drawWidth = ChatTextLayout.Draw(graphics, mixed, font, Brushes.White, x, y);
                    Require(Math.Abs(drawWidth - (prefixWidth + emojiWidth + suffixWidth)) < 0.01f,
                        "混合文本绘制宽度与整段测量不一致：" + emoji);
                    // 对照同一坐标下整段交给 GDI+ 的绘制结果，覆盖字形、基线和后续文字位置。
                    reference.DrawString(prefix, font, Brushes.White, new PointF(x, y), format);
                    reference.DrawString(emoji, emojiFont, Brushes.White, new PointF(x + prefixWidth, emojiY), format);
                    reference.DrawString(suffix, font, Brushes.White, new PointF(x + prefixWidth + emojiWidth, y), format);
                    string failure = "表情绘制未使用整段 Emoji 字体：" + emoji;
                    for (int row = 0; row < actual.Height; row++)
                        for (int column = 0; column < actual.Width; column++)
                            Require(actual.GetPixel(column, row) == expected.GetPixel(column, row), failure);
                }
            }
            Console.WriteLine("BMP 肤色、ZWJ 和独立表情的 Segoe UI Emoji 整段测量及 GDI+ 像素对照通过");
        }

        private static void VerifyClipboardPriority()
        {
            using (var bitmap = new Bitmap(8, 8))
            {
                var mixed = new DataObject();
                mixed.SetData(DataFormats.UnicodeText, "Excel 单元格");
                mixed.SetData(DataFormats.Bitmap, bitmap);
                MethodInfo hasText = typeof(ChatInputBox).GetMethod("HasClipboardText", BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo hasImage = typeof(ChatInputBox).GetMethod("HasClipboardAttachment", BindingFlags.NonPublic | BindingFlags.Static);
                bool text = (bool)hasText.Invoke(null, new object[] { mixed });
                bool image = (bool)hasImage.Invoke(null, new object[] { mixed, text });
                Require(text && !image, "图文混合剪贴板未优先保留文字");

                var imageOnly = new DataObject(DataFormats.Bitmap, bitmap);
                text = (bool)hasText.Invoke(null, new object[] { imageOnly });
                image = (bool)hasImage.Invoke(null, new object[] { imageOnly, text });
                Require(!text && image, "纯位图剪贴板未保留附件入口");
            }
            Console.WriteLine("LC-CC-005: Windows 图文混合剪贴板优先级断言通过");
        }

        private static void VerifyAppearancePaletteAndTransferRate()
        {
            int[] expected = { 0xFFFFFF, 0xA16B39, 0xFC2C3E, 0xF9670E, 0xF69412, 0x14C370,
                0x13B8A3, 0x1884FC, 0x905AFB, 0xFB3499, 0x777777 };
            for (int i = 0; i < expected.Length; i++)
                Require((GrokTheme.GetAccentSwatch(i).ToArgb() & 0xFFFFFF) == expected[i], "外观调色盘颜色不完整");
            Require(FileTransferController.FormatTransferRate(0.5) == "512 KB/s", "低速传输速率格式化错误");
            Require(FileTransferController.FormatTransferRate(12.345) == "12.3 MB/s", "高速传输速率格式化错误");
            Console.WriteLine("外观 11 色调色盘与发送/接收速率格式化断言通过");
        }

        private static string SaveClipboardImage(string directory)
        {
            string source = Path.Combine(directory, "截图-测试.png");
            using (var bitmap = new Bitmap(64, 48))
            {
                using (var image = ChatInputBox.ReadClipboardImage(new DataObject(DataFormats.Bitmap, bitmap)))
                    Require(image != null && image.Width == 64, "位图格式读取失败");
            }
            using (var bitmap = new Bitmap(64, 48))
            using (var png = new MemoryStream())
            {
                bitmap.SetPixel(12, 10, Color.FromArgb(137, 230, 88, 55));
                bitmap.Save(png, ImageFormat.Png);
                using (var image = ChatInputBox.ReadClipboardImage(new DataObject("PNG", png.ToArray())))
                {
                    Require(image is Bitmap && ((Bitmap)image).GetPixel(12, 10).A == 137, "PNG 透明度丢失");
                    image.Save(source, ImageFormat.Png);
                }
            }
            Require(ChatInputBox.ReadClipboardImage(new DataObject(DataFormats.UnicodeText, "普通文本")) == null, "文字被误识别成图片");
            var files = ChatInputBox.ReadFileDrop(new DataObject(DataFormats.FileDrop, new[] { source }));
            Require(files != null && files.Single() == source, "原有文件粘贴格式失效");
            return source;
        }

        private static void VerifyLoopback(string directory, string source)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                string received = Path.Combine(directory, "received");
                using (var sender = new NetworkService("发送端", directory))
                using (var receiver = new NetworkService("接收端", received))
                using (var fileDone = new ManualResetEvent(false))
                {
                    ChatMessage message = null;
                    string file = null;
                    receiver.OnMessageReceived += value => message = value;
                    receiver.OnFileReceived += value => file = value.FilePath;
                    var peer = new PeerInfo { Id = receiver.LocalId, IP = IPAddress.Loopback, TcpPort = ((IPEndPoint)listener.LocalEndpoint).Port };
                    MethodInfo handler = typeof(NetworkService).GetMethod("HandleIncomingTcpClient", BindingFlags.NonPublic | BindingFlags.Instance);
                    var accept = new Thread(() =>
                    {
                        for (int index = 0; index < 2; index++)
                            using (TcpClient client = listener.AcceptTcpClient())
                                handler.Invoke(receiver, new object[] { client });
                    }) { IsBackground = true };
                    accept.Start();
                    Require(sender.SendTextMessage(peer, Content, false), "表情文本未确认");
                    bool ok = false;
                    sender.SendFileAsync(peer, source, null, (success, detail) => { ok = success; fileDone.Set(); });
                    Require(fileDone.WaitOne(8000) && ok && accept.Join(3000), "截图文件未确认");
                    Require(message != null && message.Content == Content, "LANC 表情文本发生变化");
                    Require(file != null && File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(file)), "PNG 传输字节发生变化");
                    ChatHistoryService.ProfileStorageDirectory = Path.Combine(directory, "history");
                    ChatHistoryService.SaveMessage("test", message);
                    Require(ChatHistoryService.LoadHistory("test").Single().Content == Content, "历史回读更改了表情文本");
                }
            }
            finally { listener.Stop(); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void VerifyManagedTransfers(string directory, string source)
        {
            ChatHistoryService.ProfileStorageDirectory = Path.Combine(directory, "managed-history");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                using (var network = new NetworkService("截图发送方", directory))
                using (var noUi = new Control())
                {
                    var sessions = new SessionManager();
                    var controller = new FileTransferController(network, sessions, noUi);
                    var peer = new PeerInfo { Id = "附件接收方", IP = IPAddress.Loopback, TcpPort = ((IPEndPoint)listener.LocalEndpoint).Port };
                    string key = SessionManager.GetLanKey(peer.Id, peer.IP);
                    string pasteDirectory = Path.Combine(directory, "PastedImages");
                    Directory.CreateDirectory(pasteDirectory);
                    var bytes = File.ReadAllBytes(source);
                    foreach (string scenario in new[] { "拒收", "断连", "成功", "存储失败", "退出", "用户文件" })
                    {
                        string path = Path.Combine(pasteDirectory, "截图-" + scenario + ".png");
                        File.WriteAllBytes(path, bytes);
                        bool managed = scenario != "用户文件";
                        // 存储目录被同名文件占用，模拟对端确认后本地历史写入失败。
                        string history = ChatHistoryService.ProfileStorageDirectory;
                        if (scenario == "存储失败")
                        {
                            ChatHistoryService.ProfileStorageDirectory = Path.Combine(directory, "blocked-history");
                            File.WriteAllText(ChatHistoryService.ProfileStorageDirectory, "");
                        }
                        var sender = managed ? controller : new FileTransferController(network, sessions, null);
                        Require(sender.SendFiles(peer, new[] { path }, file => managed && file == path) == 1, "文件未进入发送队列");
                        var pending = listener.BeginAcceptTcpClient(null, null);
                        using (var ready = pending.AsyncWaitHandle)
                            Require(ready.WaitOne(5000), "截图发送未连接对端");
                        using (TcpClient client = listener.EndAcceptTcpClient(pending))
                        using (NetworkStream stream = client.GetStream())
                        using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                        {
                            client.ReceiveTimeout = 5000;
                            Require(Encoding.ASCII.GetString(reader.ReadBytes(4)) == "LANC" && reader.ReadByte() == 2, "文件报价帧不兼容");
                            reader.ReadString();
                            reader.ReadString();
                            reader.ReadString();
                            Require(reader.ReadString() == Path.GetFileName(path) && reader.ReadInt64() == bytes.Length, "文件报价字段不兼容");
                            if (scenario == "拒收" || scenario == "用户文件") stream.WriteByte(0);
                            else
                            {
                                stream.WriteByte(1);
                                Require(reader.ReadBytes(bytes.Length).SequenceEqual(bytes), "对端收到的截图字节不一致");
                                if (scenario == "退出")
                                {
                                    controller.DiscardOutgoingPastes();
                                    Require(!File.Exists(path), "退出时未清理等待最终确认的截图");
                                }
                                if (scenario != "断连") stream.WriteByte(1);
                            }
                        }
                        if (scenario == "成功")
                        {
                            Require(SpinWait.SpinUntil(() => ChatHistoryService.LoadHistory(key).Exists(msg => msg.FilePath == path), 5000),
                                "UI 句柄不可用时仍应保存成功截图的引用");
                            controller.DiscardOutgoingPastes();
                            Require(File.ReadAllBytes(path).SequenceEqual(bytes), "退出删除了历史引用的截图");
                        }
                        else if (!managed)
                        {
                            var message = sessions.GetLastMessage(key);
                            Require(SpinWait.SpinUntil(() => message.TransferProgress.IsFailed, 5000), "用户文件拒收未完成");
                            Require(File.ReadAllBytes(path).SequenceEqual(bytes), "拒收误删了用户原始文件");
                        }
                        else
                        {
                            Require(SpinWait.SpinUntil(() => !File.Exists(path), 5000), scenario + "未清理应用截图");
                            Require(!ChatHistoryService.LoadHistory(key).Exists(msg => msg.FilePath == path), scenario + "产生失效历史引用");
                        }
                        ChatHistoryService.ProfileStorageDirectory = history;
                    }
                    Require(ChatHistoryService.LoadHistory(key).Count == 1, "只应保留成功截图的历史行");
                }
            }
            finally { listener.Stop(); }
            Console.WriteLine("LC-CC-004: Windows 拒收、断连、存储失败与退出清理通过，保留成功截图和用户原文件");
        }
    }
}
