using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace LanChat
{
    /// <summary>
    /// 聊天记录独立持久化与自动恢复服务 (低耦合架构设计)
    /// 遵循业界标准即时通讯规范 (WeChat/QQ 存储设计)：
    /// 1. 默认存储于受系统保护的用户数据目录：%AppData%\LanChat\ChatHistory\
    ///    便携模式下存储于可执行文件同级 ChatHistory\
    /// 2. 支持用户在设置面板中任意自定义修改存储路径，并提供完整数据平滑迁移能力
    /// 3. 采用高性能 JSON Lines (.jsonl) 格式流式追加，单条消息 O(1) 极速落盘，抗异常退出与损坏
    /// 4. 软件升级、更新或重装重启后，全自动扫描并恢复所有广播与好友会话历史
    /// </summary>
    public static class ChatHistoryService
    {
        private static readonly object _lock = new object();
        private static readonly char[] _invalidChars = Path.GetInvalidFileNameChars();
        // Linux 启动时由已取得锁的配置选定原历史目录，Windows 沿用既有路径逻辑。
        internal static string ProfileStorageDirectory { get; set; }

        /// <summary>
        /// 获取当前生效的聊天记录存储绝对路径
        /// </summary>
        public static string StorageDirectory
        {
            get
            {
                lock (_lock)
                {
                    if (ProfileStorageDirectory != null) return ProfileStorageDirectory;
                    string configured = AppSettings.ChatHistoryDirectory;
                    if (!string.IsNullOrEmpty(configured))
                    {
                        try
                        {
                            if (!Directory.Exists(configured))
                            {
                                Directory.CreateDirectory(configured);
                            }
                            return configured;
                        }
                        catch { }
                    }

                    // 默认路径：优先便携模式，次选用户 %AppData%
                    string defaultDir;
                    string localSettings = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                    if (File.Exists(localSettings))
                    {
                        defaultDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ChatHistory");
                    }
                    else
                    {
                        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        defaultDir = Path.Combine(appData, "LanChat", "ChatHistory");
                    }

                    try
                    {
                        if (!Directory.Exists(defaultDir))
                        {
                            Directory.CreateDirectory(defaultDir);
                        }
                    }
                    catch { }

                    return defaultDir;
                }
            }
        }

        /// <summary>
        /// 追加保存单条聊天消息至对应会话的持久化文件
        /// </summary>
        public static void SaveMessage(string sessionKey, ChatMessage msg)
        {
            if (msg == null) return;
            try { AppendMessage(sessionKey, msg); }
            catch { }
        }

        // Linux 桌面需要向用户报告落盘错误，共用同一写入格式。
        internal static void AppendMessage(string sessionKey, ChatMessage msg)
        {
            if (string.IsNullOrEmpty(sessionKey)) sessionKey = SessionManager.BROADCAST_KEY;
            lock (_lock)
            {
                string dir = StorageDirectory;
                Directory.CreateDirectory(dir);
                string fullPath = Path.Combine(dir, GetFileNameForSession(sessionKey));
                string line = SerializeMessage(sessionKey, msg);
                using (var stream = new FileStream(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                {
                    // 旧记录可能缺少最后的换行。保留原字节，另起一行使新版记录可独立读取。
                    if (stream.Length != 0)
                    {
                        stream.Seek(-1, SeekOrigin.End);
                        if (stream.ReadByte() != '\n') stream.WriteByte((byte)'\n');
                    }
                    using (var writer = new StreamWriter(stream, Encoding.UTF8)) writer.WriteLine(line);
                }
            }
        }

        /// <summary>
        /// 加载指定会话的全部历史聊天记录
        /// </summary>
        public static List<ChatMessage> LoadHistory(string sessionKey)
        {
            if (string.IsNullOrEmpty(sessionKey)) sessionKey = SessionManager.BROADCAST_KEY;

            List<ChatMessage> list = new List<ChatMessage>();
            lock (_lock)
            {
                try
                {
                    string dir = StorageDirectory;
                    string fileName = GetFileNameForSession(sessionKey);
                    string fullPath = Path.Combine(dir, fileName);

                    if (!File.Exists(fullPath)) return list;

                    using (StreamReader reader = new StreamReader(fullPath, Encoding.UTF8))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrEmpty(line)) continue;
                            string sk;
                            ChatMessage msg = DeserializeMessage(line, out sk);
                            if (msg != null)
                            {
                                list.Add(msg);
                            }
                        }
                    }
                }
                catch { }
            }
            return list;
        }

        /// <summary>
        /// 启动时扫描存储目录，全量恢复所有会话的历史聊天记录
        /// </summary>
        public static Dictionary<string, List<ChatMessage>> LoadAllHistories()
        {
            Dictionary<string, List<ChatMessage>> result = new Dictionary<string, List<ChatMessage>>(StringComparer.OrdinalIgnoreCase);

            lock (_lock)
            {
                try
                {
                    string dir = StorageDirectory;
                    if (!Directory.Exists(dir)) return result;

                    string[] files = Directory.GetFiles(dir, "history_*.jsonl");
                    foreach (string file in files)
                    {
                        try
                        {
                            using (StreamReader reader = new StreamReader(file, Encoding.UTF8))
                            {
                                string line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    if (string.IsNullOrEmpty(line)) continue;
                                    string sk;
                                    ChatMessage msg = DeserializeMessage(line, out sk);
                                    if (msg != null && !string.IsNullOrEmpty(sk))
                                    {
                                        List<ChatMessage> list;
                                        if (!result.TryGetValue(sk, out list))
                                        {
                                            list = new List<ChatMessage>();
                                            result[sk] = list;
                                        }
                                        list.Add(msg);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return result;
        }

        /// <summary>
        /// 清空指定会话的本地持久化记录
        /// </summary>
        public static void ClearSessionHistory(string sessionKey)
        {
            if (string.IsNullOrEmpty(sessionKey)) sessionKey = SessionManager.BROADCAST_KEY;

            lock (_lock)
            {
                try
                {
                    string dir = StorageDirectory;
                    string fileName = GetFileNameForSession(sessionKey);
                    string fullPath = Path.Combine(dir, fileName);

                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 清空所有会话的本地历史记录
        /// </summary>
        public static void ClearAllHistories()
        {
            lock (_lock)
            {
                try
                {
                    string dir = StorageDirectory;
                    if (Directory.Exists(dir))
                    {
                        string[] files = Directory.GetFiles(dir, "history_*.jsonl");
                        foreach (string f in files)
                        {
                            try { File.Delete(f); } catch { }
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 修改聊天记录存储路径，支持将已有历史记录文件完整迁移至新位置
        /// </summary>
        public static bool ChangeStorageDirectory(string newDir, bool migrateExisting)
        {
            if (string.IsNullOrEmpty(newDir)) return false;

            lock (_lock)
            {
                try
                {
                    string oldDir = StorageDirectory;
                    if (string.Equals(Path.GetFullPath(oldDir), Path.GetFullPath(newDir), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (!Directory.Exists(newDir))
                    {
                        Directory.CreateDirectory(newDir);
                    }

                    if (migrateExisting && Directory.Exists(oldDir))
                    {
                        string[] files = Directory.GetFiles(oldDir, "history_*.jsonl");
                        foreach (string file in files)
                        {
                            try
                            {
                                string dest = Path.Combine(newDir, Path.GetFileName(file));
                                File.Copy(file, dest, true);
                            }
                            catch { }
                        }
                    }

                    AppSettings.ChatHistoryDirectory = newDir;
                    AppSettings.Save();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 根据会话 Key 生成安全的文件名
        /// </summary>
        private static string GetFileNameForSession(string sessionKey)
        {
            if (string.IsNullOrEmpty(sessionKey) || sessionKey.Equals(SessionManager.BROADCAST_KEY, StringComparison.OrdinalIgnoreCase))
            {
                return "history_BROADCAST.jsonl";
            }

            StringBuilder sb = new StringBuilder("history_peer_");
            foreach (char c in sessionKey)
            {
                if (Array.IndexOf(_invalidChars, c) >= 0 || c == ':' || c == '/' || c == '\\')
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }
            sb.Append(".jsonl");
            return sb.ToString();
        }

        /// <summary>
        /// 消息序列化为单行 JSON
        /// </summary>
        private static string SerializeMessage(string sessionKey, ChatMessage msg)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.AppendFormat("\"SessionKey\":\"{0}\",", EscapeJson(sessionKey));
            sb.AppendFormat("\"SenderId\":\"{0}\",", EscapeJson(msg.SenderId ?? string.Empty));
            sb.AppendFormat("\"SenderNick\":\"{0}\",", EscapeJson(msg.SenderNick ?? string.Empty));
            sb.AppendFormat("\"TargetId\":\"{0}\",", EscapeJson(msg.TargetId ?? string.Empty));
            sb.AppendFormat("\"PeerNick\":\"{0}\",", EscapeJson(msg.PeerNick ?? string.Empty));
            if (msg.RemoteAddress != null)
                sb.AppendFormat("\"RemoteAddress\":\"{0}\",", msg.RemoteAddress);
            sb.AppendFormat("\"Content\":\"{0}\",", EscapeJson(msg.Content ?? string.Empty));
            sb.AppendFormat("\"Timestamp\":\"{0}\",", msg.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
            sb.AppendFormat("\"IsSelf\":{0},", msg.IsSelf ? "true" : "false");
            sb.AppendFormat("\"IsBroadcast\":{0},", msg.IsBroadcast ? "true" : "false");
            sb.AppendFormat("\"IsSystem\":{0},", msg.IsSystem ? "true" : "false");
            sb.AppendFormat("\"IsFile\":{0},", msg.IsFile ? "true" : "false");
            sb.AppendFormat("\"FileName\":\"{0}\",", EscapeJson(msg.FileName ?? string.Empty));
            sb.AppendFormat("\"FilePath\":\"{0}\",", EscapeJson(msg.FilePath ?? string.Empty));
            sb.AppendFormat(CultureInfo.InvariantCulture, "\"FileSize\":{0}", msg.FileSize);
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// 单行 JSON 反序列化为 ChatMessage
        /// </summary>
        private static ChatMessage DeserializeMessage(string line, out string sessionKey)
        {
            sessionKey = SessionManager.BROADCAST_KEY;
            if (string.IsNullOrWhiteSpace(line)) return null;
            line = line.Trim();
            if (line[0] != '{' || line[line.Length - 1] != '}') return null;

            try
            {
                ChatMessage msg = new ChatMessage();

                string sk = ExtractString(line, "SessionKey");
                if (!string.IsNullOrEmpty(sk)) sessionKey = sk;

                msg.SenderId = ExtractString(line, "SenderId");
                msg.SenderNick = ExtractString(line, "SenderNick");
                msg.TargetId = ExtractString(line, "TargetId");
                msg.PeerNick = ExtractString(line, "PeerNick");
                IPAddress address;
                if (IPAddress.TryParse(ExtractString(line, "RemoteAddress"), out address)) msg.RemoteAddress = address;
                msg.Content = ExtractString(line, "Content");

                string tsStr = ExtractString(line, "Timestamp");
                DateTime dt;
                if (!string.IsNullOrEmpty(tsStr) && DateTime.TryParse(tsStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                {
                    msg.Timestamp = dt;
                }
                else
                {
                    msg.Timestamp = DateTime.Now;
                }

                msg.IsSelf = ExtractBool(line, "IsSelf", false);
                msg.IsBroadcast = ExtractBool(line, "IsBroadcast", false);
                msg.IsSystem = ExtractBool(line, "IsSystem", false);
                msg.IsFile = ExtractBool(line, "IsFile", false);
                msg.FileName = ExtractString(line, "FileName");
                msg.FilePath = ExtractString(line, "FilePath");
                msg.FileSize = ExtractLong(line, "FileSize", 0);

                return msg;
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractString(string json, string key)
        {
            string pattern = "\"" + key + "\"\\s*:\\s*\"((?:[^\x22\\\\]|\\\\.)*)\"";
            Match m = Regex.Match(json, pattern);
            if (m.Success)
            {
                return UnescapeJson(m.Groups[1].Value);
            }
            return string.Empty;
        }

        private static bool ExtractBool(string json, string key, bool def)
        {
            string pattern = "\"" + key + "\"\\s*:\\s*(true|false)";
            Match m = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            if (m.Success)
            {
                return bool.Parse(m.Groups[1].Value);
            }
            return def;
        }

        private static long ExtractLong(string json, string key, long def)
        {
            string pattern = "\"" + key + "\"\\s*:\\s*([0-9]+)";
            Match m = Regex.Match(json, pattern);
            if (m.Success)
            {
                long val;
                if (long.TryParse(m.Groups[1].Value, out val))
                {
                    return val;
                }
            }
            return def;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        private static string UnescapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return Regex.Replace(s, @"\\[nrt""\\]", match =>
            {
                switch (match.Value[1])
                {
                    case 'n': return "\n";
                    case 'r': return "\r";
                    case 't': return "\t";
                    default: return match.Value.Substring(1);
                }
            });
        }
    }
}
