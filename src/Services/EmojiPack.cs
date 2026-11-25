using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace LanChat
{
    /// <summary>
    /// 内置微信静态表情资源包。
    /// 依赖编译器嵌入的 LanChat.WeChatEmoji.pack 资源；Windows 用于 GDI+ 渲染，Linux
    /// 通信服务用于向 Electron 返回 data URL。边界是只读资源解析，不负责网络传输与 UI 生命周期。
    /// </summary>
    internal static class EmojiPack
    {
        private const string ResourceName = "LanChat.WeChatEmoji.pack";
        private const string Magic = "LCEMOJI1";
        private const int MaxEntries = 256;
        private const int MaxTokenBytes = 128;
        private const int MaxPngBytes = 128 * 1024;
        private static readonly Lazy<List<EmojiPackEntry>> Entries =
            new Lazy<List<EmojiPackEntry>>(LoadEmbedded, true);

        /// <summary>返回程序集内置的表情条目快照；资源缺失或损坏时返回空列表。</summary>
        internal static IList<EmojiPackEntry> GetEntries()
        {
            return Entries.Value;
        }

        /// <summary>返回 Linux Electron 可直接显示的 PNG data URL 清单。</summary>
        internal static List<string[]> GetDataUrls()
        {
            var result = new List<string[]>();
            foreach (EmojiPackEntry entry in Entries.Value)
                result.Add(new[] { entry.Token, "data:image/png;base64," + Convert.ToBase64String(entry.Png) });
            return result;
        }

        private static List<EmojiPackEntry> LoadEmbedded()
        {
            var result = new List<EmojiPackEntry>();
            try
            {
                Assembly assembly = typeof(EmojiPack).Assembly;
                using (Stream stream = assembly.GetManifestResourceStream(ResourceName))
                {
                    if (stream == null) return result;
                    using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                    {
                        if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != Magic) return result;
                        int count = reader.ReadInt32();
                        if (count <= 0 || count > MaxEntries) return result;
                        for (int i = 0; i < count; i++)
                        {
                            int tokenLength = reader.ReadUInt16();
                            int imageLength = reader.ReadInt32();
                            if (tokenLength <= 0 || tokenLength > MaxTokenBytes ||
                                imageLength <= 24 || imageLength > MaxPngBytes) return new List<EmojiPackEntry>();
                            byte[] tokenBytes = reader.ReadBytes(tokenLength);
                            byte[] png = reader.ReadBytes(imageLength);
                            if (tokenBytes.Length != tokenLength || png.Length != imageLength ||
                                !IsToken(tokenBytes) || !IsPng(png)) return new List<EmojiPackEntry>();
                            string token = Encoding.UTF8.GetString(tokenBytes);
                            if (result.Exists(item => item.Token == token)) return new List<EmojiPackEntry>();
                            result.Add(new EmojiPackEntry(token, png));
                        }
                    }
                }
            }
            catch (IOException) { return new List<EmojiPackEntry>(); }
            catch (UnauthorizedAccessException) { return new List<EmojiPackEntry>(); }
            catch (ArgumentException) { return new List<EmojiPackEntry>(); }
            return result;
        }

        private static bool IsToken(byte[] bytes)
        {
            string token = Encoding.UTF8.GetString(bytes);
            if (token.Length < 3 || token.Length > 26 || token[0] != '[' || token[token.Length - 1] != ']') return false;
            for (int i = 1; i < token.Length - 1; i++)
                if (token[i] == '[' || token[i] == ']' || char.IsWhiteSpace(token[i])) return false;
            return true;
        }

        private static bool IsPng(byte[] bytes)
        {
            if (bytes.Length < 33) return false;
            if (bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47 ||
                bytes[4] != 0x0D || bytes[5] != 0x0A || bytes[6] != 0x1A || bytes[7] != 0x0A ||
                ReadUInt32(bytes, 8) != 13 || bytes[12] != (byte)'I' || bytes[13] != (byte)'H' ||
                bytes[14] != (byte)'D' || bytes[15] != (byte)'R') return false;
            uint width = ReadUInt32(bytes, 16);
            uint height = ReadUInt32(bytes, 20);
            return width > 0 && width <= 64 && height > 0 && height <= 64;
        }

        private static uint ReadUInt32(byte[] bytes, int index)
        {
            return ((uint)bytes[index] << 24) | ((uint)bytes[index + 1] << 16) |
                ((uint)bytes[index + 2] << 8) | bytes[index + 3];
        }
    }

    /// <summary>
    /// 内置表情的一项不可变数据。生命周期由 EmojiPack 的惰性资源缓存持有。
    /// </summary>
    internal sealed class EmojiPackEntry
    {
        internal string Token { get; private set; }
        internal byte[] Png { get; private set; }

        internal EmojiPackEntry(string token, byte[] png)
        {
            Token = token;
            Png = png;
        }
    }
}
