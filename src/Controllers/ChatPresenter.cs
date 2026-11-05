using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace LanChat
{
    /// <summary>
    /// 聊天消息编排 Presenter：负责文本消息的异步网络收发、广播与私聊路由、系统消息注入及未读统计
    /// </summary>
    public class ChatPresenter
    {
        private readonly NetworkService _netService;
        private readonly SessionManager _sessionManager;
        private readonly SessionPresenter _sessionPresenter;
        private readonly ISynchronizeInvoke _sync;

        private string _lastBroadcastSnippet = "";
        private string _lastBroadcastTimeText = "";

        public event Action<ChatMessage, string> MessageAppended;
        public event Action<ChatMessage, bool /* wasActivelyViewing */> MessageNotificationTriggered;
        public event Action ActiveChatCleared;

        public Func<ChatMessage, bool> IsActivelyViewingPredicate { get; set; }

        public string LastBroadcastSnippet
        {
            get { return _lastBroadcastSnippet; }
            set { _lastBroadcastSnippet = value; }
        }

        public string LastBroadcastTimeText
        {
            get { return _lastBroadcastTimeText; }
            set { _lastBroadcastTimeText = value; }
        }

        public ChatPresenter(
            NetworkService netService,
            SessionManager sessionManager,
            SessionPresenter sessionPresenter,
            ISynchronizeInvoke sync)
        {
            _netService = netService;
            _sessionManager = sessionManager;
            _sessionPresenter = sessionPresenter;
            _sync = sync;

            if (_netService != null)
            {
                _netService.OnMessageReceived += (msg) => SafeInvoke(() => HandleReceivedMessage(msg));
            }
        }

        private void SafeInvoke(Action action)
        {
            if (action == null) return;
            if (_sync != null && _sync.InvokeRequired)
            {
                try { _sync.BeginInvoke(action, null); } catch { }
            }
            else
            {
                action();
            }
        }

        public void SendTextMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            string myNick = _netService != null ? _netService.NickName : Environment.UserName;
            PeerInfo target = _sessionPresenter.SelectedPeer;

            if (target == null)
            {
                // 全员群聊广播
                var peers = _netService != null ? _netService.GetActivePeers() : new List<PeerInfo>();
                ChatMessage msg = new ChatMessage
                {
                    SenderId = _netService != null ? _netService.LocalId : "",
                    SenderNick = myNick,
                    TargetId = null,
                    Content = text,
                    Timestamp = DateTime.Now,
                    IsSelf = true,
                    IsBroadcast = true
                };

                if (_sessionManager != null)
                {
                    _sessionManager.AddMessage(SessionManager.BROADCAST_KEY, msg);
                }

                _lastBroadcastSnippet = string.Format("我: {0}", text);
                _lastBroadcastTimeText = DateTime.Now.ToString("HH:mm");

                if (MessageAppended != null)
                {
                    MessageAppended(msg, SessionManager.BROADCAST_KEY);
                }

                new Thread(() => {
                    if (_netService != null)
                    {
                        foreach (var peer in peers)
                        {
                            _netService.SendTextMessage(peer, text, true);
                        }
                    }
                }) { IsBackground = true }.Start();
            }
            else
            {
                // 好友私聊
                ChatMessage msg = new ChatMessage
                {
                    SenderId = _netService != null ? _netService.LocalId : "",
                    SenderNick = myNick,
                    TargetId = target.Id,
                    Content = text,
                    Timestamp = DateTime.Now,
                    IsSelf = true,
                    IsBroadcast = false
                };

                target.LastMessage = text;
                target.LastMessageTime = DateTime.Now;

                if (_sessionManager != null)
                {
                    _sessionManager.AddMessage(target.Id, msg);
                }

                if (MessageAppended != null)
                {
                    MessageAppended(msg, target.Id);
                }

                new Thread(() => {
                    if (_netService != null)
                    {
                        bool ok = _netService.SendTextMessage(target, text, false);
                        if (!ok)
                        {
                            SafeInvoke(() => AppendChatSystem(string.Format("向 {0} 发送私聊消息失败，对方可能已离线", target.NickName)));
                        }
                    }
                }) { IsBackground = true }.Start();
            }
        }

        public void HandleReceivedMessage(ChatMessage msg)
        {
            string convKey = msg.IsBroadcast ? SessionManager.BROADCAST_KEY : msg.SenderId;

            if (!msg.IsBroadcast && _netService != null)
            {
                var peers = _netService.GetActivePeers();
                foreach (var p in peers)
                {
                    if (p.Id == msg.SenderId)
                    {
                        p.LastMessage = msg.Content;
                        p.LastMessageTime = msg.Timestamp;
                        break;
                    }
                }
            }

            if (_sessionManager != null)
            {
                _sessionManager.AddMessage(convKey, msg);
            }

            bool wasActivelyViewing = false;
            if (IsActivelyViewingPredicate != null)
            {
                wasActivelyViewing = IsActivelyViewingPredicate(msg);
            }

            if (!wasActivelyViewing)
            {
                if (msg.IsBroadcast)
                {
                    _sessionPresenter.BroadcastUnreadCount++;
                    _lastBroadcastSnippet = string.Format("{0}: {1}", msg.SenderNick, msg.Content);
                    _lastBroadcastTimeText = msg.Timestamp.ToString("HH:mm");
                }
                else if (_netService != null)
                {
                    var peers = _netService.GetActivePeers();
                    foreach (var p in peers)
                    {
                        if (p.Id == msg.SenderId)
                        {
                            p.UnreadCount++;
                            break;
                        }
                    }
                }

                if (MessageNotificationTriggered != null)
                {
                    MessageNotificationTriggered(msg, wasActivelyViewing);
                }
            }

            if (MessageAppended != null)
            {
                MessageAppended(msg, convKey);
            }
        }

        public void AppendChatSystem(string text)
        {
            ChatMessage msg = new ChatMessage
            {
                SenderNick = "系统",
                Content = text,
                Timestamp = DateTime.Now,
                IsSystem = true
            };

            string activeKey = _sessionPresenter.ActiveChatKey;
            if (_sessionManager != null)
            {
                _sessionManager.AddMessage(activeKey, msg);
            }

            if (MessageAppended != null)
            {
                MessageAppended(msg, activeKey);
            }
        }

        public void ClearCurrentChat()
        {
            string activeKey = _sessionPresenter.ActiveChatKey;
            if (_sessionManager != null)
            {
                _sessionManager.ClearHistory(activeKey);
            }

            if (ActiveChatCleared != null)
            {
                ActiveChatCleared();
            }
        }

        public List<ChatMessage> GetActiveMessages()
        {
            return _sessionManager != null
                ? _sessionManager.GetMessages(_sessionPresenter.ActiveChatKey)
                : new List<ChatMessage>();
        }
    }
}
