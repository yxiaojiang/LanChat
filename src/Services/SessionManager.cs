using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace LanChat
{
    /// <summary>
    /// 会话与聊天消息历史协调服务
    /// 统一管理各好友/广播群聊的历史消息、摘要与活跃会话上下文
    /// 由主窗体持有并在启动时加载，依赖 ChatHistoryService；局域网私聊地址与协议节点标识独立。
    /// </summary>
    public class SessionManager
    {
        public const string BROADCAST_KEY = "BROADCAST";

        private readonly object _lock = new object();
        private readonly Dictionary<string, List<ChatMessage>> _histories = new Dictionary<string, List<ChatMessage>>(StringComparer.OrdinalIgnoreCase);
        // 旧文件原样保留。别名只改变读取时的分组，清理会话时也会覆盖所有来源文件。
        private readonly Dictionary<string, string> _legacyAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private const string LanPrefix = "LAN-";
        private static readonly Regex LegacyPresence = new Regex(
            @"^(?:好友 \[(?<nick>.*)\] \((?<ip>[\d.]+)\) 加入了局域网|发现节点上线: (?<nick>.*) \(.* - (?<ip>[\d.]+)\))$");
        private static readonly Regex LegacyIpKey = new Regex(@"^(?<ip>(?:[0-9]{1,3}\.){3}[0-9]{1,3})(?::[0-9]+)?$");
        private string _activeSessionKey = BROADCAST_KEY;

        /// <summary>以对端的 IPv4 地址定位局域网私聊。地址缺失时保留原会话标识。</summary>
        /// <param name="peerId">对端节点标识或已归档的会话键。</param>
        /// <param name="address">本地发现或从 TCP 连接取得的地址。</param>
        /// <returns>用于本地记录的会话键。</returns>
        internal static string GetLanKey(string peerId, IPAddress address)
        {
            return address != null && address.AddressFamily == AddressFamily.InterNetwork &&
                !address.Equals(IPAddress.Any) ? LanPrefix + address.ToString() : peerId;
        }

        internal static IPAddress GetArchivedAddress(string key)
        {
            IPAddress address;
            return key.StartsWith(LanPrefix, StringComparison.OrdinalIgnoreCase) &&
                IPAddress.TryParse(key.Substring(LanPrefix.Length), out address) ? address : null;
        }

        internal string ResolveKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return BROADCAST_KEY;
            lock (_lock)
            {
                string resolved;
                return _legacyAliases.TryGetValue(key, out resolved) ? resolved : key;
            }
        }

        // 同一节点 ID 的旧记录可以直接关联。已归档到另一个地址的记录保留其原归属。
        internal bool LinkPeer(string peerId, IPAddress address)
        {
            string key = GetLanKey(peerId, address);
            if (string.IsNullOrEmpty(peerId) || peerId == key || peerId.Equals(BROADCAST_KEY, StringComparison.OrdinalIgnoreCase))
                return false;

            lock (_lock)
            {
                if (!_histories.ContainsKey(peerId) || _legacyAliases.ContainsKey(peerId)) return false;
                MergeLegacyHistory(peerId, key);
                return true;
            }
        }

        /// <summary>
        /// 当前激活的会话标识（"BROADCAST" 或 目标 Peer.Id）
        /// </summary>
        public string ActiveSessionKey
        {
            get
            {
                lock (_lock)
                {
                    return _activeSessionKey;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (string.IsNullOrEmpty(value)) value = BROADCAST_KEY;
                    _activeSessionKey = value;
                }
            }
        }

        /// <summary>
        /// 是否处于全员广播群聊
        /// </summary>
        public bool IsBroadcastActive
        {
            get
            {
                lock (_lock)
                {
                    return _activeSessionKey == BROADCAST_KEY;
                }
            }
        }

        /// <summary>
        /// 消息追加事件（会话Key, 消息实体）
        /// </summary>
        public event Action<string, ChatMessage> MessageAdded;

        /// <summary>
        /// 启动时从持久化存储恢复全量历史记录
        /// </summary>
        public void LoadPersistedHistories()
        {
            lock (_lock)
            {
                try
                {
                    var loaded = ChatHistoryService.LoadAllHistories();
                    _histories.Clear();
                    _legacyAliases.Clear();
                    var addressesByNick = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                    var addressesById = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                    var nicksBySenderId = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in loaded)
                    {
                        _histories[kvp.Key] = kvp.Value;
                        bool hasAddress = GetArchivedAddress(kvp.Key) != null;
                        foreach (ChatMessage msg in kvp.Value)
                        {
                            // 仅有发出消息的旧私聊也可能在群聊中留下同一发送者 ID 对应的昵称。
                            if (!msg.IsSelf) RememberValue(nicksBySenderId, msg.SenderId, GetPeerNick(msg));
                            if (msg.RemoteAddress != null)
                                RememberValue(addressesById, msg.SenderId, GetLanKey(null, msg.RemoteAddress));
                            if (hasAddress)
                            {
                                RememberValue(addressesById, msg.IsSelf ? msg.TargetId : msg.SenderId, kvp.Key);
                            }
                            if (!msg.IsSystem || msg.RemoteAddress != null || string.IsNullOrEmpty(msg.Content)) continue;

                            Match match = LegacyPresence.Match(msg.Content);
                            IPAddress address;
                            if (match.Success && IPAddress.TryParse(match.Groups["ip"].Value, out address))
                            {
                                RememberValue(addressesByNick, match.Groups["nick"].Value, GetLanKey(null, address));
                            }
                        }
                    }

                    foreach (var kvp in loaded)
                    {
                        if (kvp.Key.Equals(BROADCAST_KEY, StringComparison.OrdinalIgnoreCase) || GetArchivedAddress(kvp.Key) != null)
                            continue;

                        string destination = FindLegacyDestination(kvp.Key, kvp.Value, addressesById, addressesByNick, nicksBySenderId);
                        if (destination != null) MergeLegacyHistory(kvp.Key, destination);
                    }
                }
                catch { }
            }
        }

        private static void RememberValue(Dictionary<string, HashSet<string>> index, string key, string value)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return;
            HashSet<string> values;
            if (!index.TryGetValue(key, out values))
            {
                values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                index.Add(key, values);
            }
            values.Add(value);
        }

        private static string FindLegacyDestination(string key, List<ChatMessage> messages,
            Dictionary<string, HashSet<string>> addressesById, Dictionary<string, HashSet<string>> addressesByNick,
            Dictionary<string, HashSet<string>> nicksBySenderId)
        {
            Match match = LegacyIpKey.Match(key);
            IPAddress address;
            if (match.Success && IPAddress.TryParse(match.Groups["ip"].Value, out address)) return GetLanKey(null, address);

            HashSet<string> keys;
            if (addressesById.TryGetValue(key, out keys)) return keys.Count == 1 ? keys.First() : null;

            // 旧 GUID 文件没有地址字段。只有历史上线记录能佐证唯一地址时才按旧昵称关联。
            var nicks = new HashSet<string>(messages.Select(GetPeerNick).Where(nick => nick != null), StringComparer.OrdinalIgnoreCase);
            HashSet<string> observedNicks;
            if (nicksBySenderId.TryGetValue(key, out observedNicks)) nicks.UnionWith(observedNicks);
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string nick in nicks)
            {
                if (addressesByNick.TryGetValue(nick, out keys)) candidates.UnionWith(keys);
            }
            return candidates.Count == 1 ? candidates.First() : null;
        }

        private void MergeLegacyHistory(string source, string destination)
        {
            List<ChatMessage> target;
            if (!_histories.TryGetValue(destination, out target)) target = new List<ChatMessage>();
            _histories[destination] = target.Concat(_histories[source]).OrderBy(msg => msg.Timestamp).ToList();
            _histories.Remove(source);
            _legacyAliases[source] = destination;
        }

        private static string GetPeerNick(ChatMessage msg)
        {
            if (msg.IsSystem) return null;
            string nick = string.IsNullOrWhiteSpace(msg.PeerNick) && !msg.IsSelf ? msg.SenderNick : msg.PeerNick;
            return string.IsNullOrWhiteSpace(nick) || nick == "系统" ? null : nick;
        }

        // 离线标题从最近一条有对端身份的消息恢复，尾部系统通知和本人昵称均不参与。
        internal string GetArchivedNick(string key)
        {
            lock (_lock)
            {
                List<ChatMessage> messages;
                if (!_histories.TryGetValue(ResolveKey(key), out messages)) return null;
                for (int i = messages.Count - 1; i >= 0; i--)
                {
                    string nick = GetPeerNick(messages[i]);
                    if (nick != null) return nick;
                }
                return null;
            }
        }

        /// <summary>
        /// 追加单条消息至指定会话历史（默认同步追加落盘）
        /// </summary>
        public void AddMessage(string sessionKey, ChatMessage msg, bool persist = true)
        {
            if (msg == null) return;

            lock (_lock)
            {
                sessionKey = ResolveKey(sessionKey);
                List<ChatMessage> list;
                if (!_histories.TryGetValue(sessionKey, out list))
                {
                    list = new List<ChatMessage>();
                    _histories[sessionKey] = list;
                }
                list.Add(msg);
                if (persist) ChatHistoryService.SaveMessage(sessionKey, msg);
            }

            var handler = MessageAdded;
            if (handler != null)
            {
                try { handler(sessionKey, msg); } catch { }
            }
        }

        /// <summary>将已经在会话内存中的文件气泡写入历史。清空后的旧气泡不会重新写回。</summary>
        internal bool PersistExistingMessage(string sessionKey, ChatMessage message, bool confirmStorage = false)
        {
            lock (_lock)
            {
                sessionKey = ResolveKey(sessionKey);
                List<ChatMessage> messages;
                if (!_histories.TryGetValue(sessionKey, out messages) || !messages.Contains(message)) return false;
                if (confirmStorage) return ChatHistoryService.TryAppendFileReference(sessionKey, message);
                ChatHistoryService.SaveMessage(sessionKey, message);
                return true;
            }
        }

        /// <summary>
        /// 获取指定会话的全部历史消息（副本）
        /// </summary>
        public List<ChatMessage> GetMessages(string sessionKey)
        {
            lock (_lock)
            {
                sessionKey = ResolveKey(sessionKey);
                List<ChatMessage> list;
                if (_histories.TryGetValue(sessionKey, out list))
                {
                    return new List<ChatMessage>(list);
                }
                return new List<ChatMessage>();
            }
        }

        /// <summary>
        /// 获取指定会话的最后一条消息，不存在则返回 null
        /// </summary>
        public ChatMessage GetLastMessage(string sessionKey)
        {
            lock (_lock)
            {
                sessionKey = ResolveKey(sessionKey);
                List<ChatMessage> list;
                if (_histories.TryGetValue(sessionKey, out list) && list.Count > 0)
                {
                    return list[list.Count - 1];
                }
                return null;
            }
        }

        /// <summary>
        /// 获取指定会话的最后一条消息摘要（常用于会话列表副标题）
        /// </summary>
        public string GetLastMessageSnippet(string sessionKey, int maxLength = 20)
        {
            ChatMessage last = GetLastMessage(sessionKey);
            if (last == null) return string.Empty;

            string snippet;
            if (last.IsFile)
            {
                snippet = "[文件] " + last.FileName;
            }
            else
            {
                snippet = last.Content ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(last.SenderNick) && sessionKey == BROADCAST_KEY)
            {
                snippet = last.SenderNick + ": " + snippet;
            }

            if (snippet.Length > maxLength)
            {
                snippet = snippet.Substring(0, maxLength - 1) + "...";
            }
            return snippet;
        }

        /// <summary>
        /// 清空指定会话的历史记录（内存与本地持久化文件同步清理）
        /// </summary>
        public void ClearHistory(string sessionKey)
        {
            DeleteSession(sessionKey);
        }

        /// <summary>
        /// 删除指定会话（从历史列表彻底移除，并清理本地持久化文件）
        /// </summary>
        public void DeleteSession(string sessionKey)
        {
            lock (_lock)
            {
                sessionKey = ResolveKey(sessionKey);
                _histories.Remove(sessionKey);
                ChatHistoryService.ClearSessionHistory(sessionKey);
                var sources = _legacyAliases.Where(pair => pair.Value.Equals(sessionKey, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.Key).ToList();
                foreach (string source in sources)
                {
                    ChatHistoryService.ClearSessionHistory(source);
                    _legacyAliases.Remove(source);
                }
            }
        }

        /// <summary>
        /// 清空全部会话的历史记录（内存与本地持久化文件同步清理）
        /// </summary>
        public void ClearAllHistories()
        {
            lock (_lock)
            {
                _histories.Clear();
                _legacyAliases.Clear();
                ChatHistoryService.ClearAllHistories();
            }
        }

        /// <summary>
        /// 获取当前所有存在历史记录的会话键列表
        /// </summary>
        public List<string> GetAllSessionKeys()
        {
            lock (_lock)
            {
                return new List<string>(_histories.Keys);
            }
        }
    }
}
