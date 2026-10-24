using System;
using System.Collections.Generic;

namespace LanChat
{
    /// <summary>
    /// 会话与聊天消息历史协调服务
    /// 统一管理各好友/广播群聊的历史消息、摘要与活跃会话上下文
    /// 纯领域应用服务，无任何 UI 强绑定
    /// </summary>
    public class SessionManager
    {
        public const string BROADCAST_KEY = "BROADCAST";

        private readonly object _lock = new object();
        private readonly Dictionary<string, List<ChatMessage>> _histories = new Dictionary<string, List<ChatMessage>>();
        private string _activeSessionKey = BROADCAST_KEY;

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
        /// 追加单条消息至指定会话历史
        /// </summary>
        public void AddMessage(string sessionKey, ChatMessage msg)
        {
            if (string.IsNullOrEmpty(sessionKey)) sessionKey = BROADCAST_KEY;
            if (msg == null) return;

            lock (_lock)
            {
                List<ChatMessage> list;
                if (!_histories.TryGetValue(sessionKey, out list))
                {
                    list = new List<ChatMessage>();
                    _histories[sessionKey] = list;
                }
                list.Add(msg);
            }

            var handler = MessageAdded;
            if (handler != null)
            {
                try { handler(sessionKey, msg); } catch { }
            }
        }

        /// <summary>
        /// 获取指定会话的全部历史消息（副本）
        /// </summary>
        public List<ChatMessage> GetMessages(string sessionKey)
        {
            if (string.IsNullOrEmpty(sessionKey)) sessionKey = BROADCAST_KEY;
            lock (_lock)
            {
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
            if (string.IsNullOrEmpty(sessionKey)) sessionKey = BROADCAST_KEY;
            lock (_lock)
            {
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
        /// 清空指定会话的历史记录
        /// </summary>
        public void ClearHistory(string sessionKey)
        {
            if (string.IsNullOrEmpty(sessionKey)) sessionKey = BROADCAST_KEY;
            lock (_lock)
            {
                List<ChatMessage> list;
                if (_histories.TryGetValue(sessionKey, out list))
                {
                    list.Clear();
                }
            }
        }
    }
}
