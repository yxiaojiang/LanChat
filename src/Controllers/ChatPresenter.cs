using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 聊天消息编排 Presenter
    /// <para>【职责】负责文本消息的异步网络收发、广播与私聊路由、系统消息注入及未读统计。</para>
    /// <para>【线程安全】跨线程接收的消息通过注入的 ISynchronizeInvoke 安全调度回 UI 主线程。</para>
    /// </summary>
    public class ChatPresenter
    {
        private readonly NetworkService _netService;
        private readonly SessionManager _sessionManager;
        private readonly SessionPresenter _sessionPresenter;
        private readonly ISynchronizeInvoke _sync;

        private string _lastBroadcastSnippet = "";
        private string _lastBroadcastTimeText = "";

        /// <summary>消息已追加并成功落盘历史通知事件 (参数：message, convKey)</summary>
        public event Action<ChatMessage, string> MessageAppended;
        /// <summary>非当前视口收到消息时的托盘与系统通知触发事件 (参数：message, wasActivelyViewing)</summary>
        public event Action<ChatMessage, bool /* wasActivelyViewing */> MessageNotificationTriggered;
        /// <summary>当前活跃会话消息清空通知事件</summary>
        public event Action ActiveChatCleared;

        /// <summary>获取或设置判定当前消息是否正在被用户当前激活视口查阅的委托</summary>
        public Func<ChatMessage, bool> IsActivelyViewingPredicate { get; set; }

        /// <summary>获取或设置群聊广播最后一条消息的文本摘要</summary>
        public string LastBroadcastSnippet
        {
            get { return _lastBroadcastSnippet; }
            set { _lastBroadcastSnippet = value; }
        }

        /// <summary>获取或设置群聊广播最后一条消息的时间文本 (HH:mm)</summary>
        public string LastBroadcastTimeText
        {
            get { return _lastBroadcastTimeText; }
            set { _lastBroadcastTimeText = value; }
        }

        /// <summary>
        /// 初始化聊天消息编排 Presenter 并监听底层网络消息到达
        /// </summary>
        /// <param name="netService">网络通信服务</param>
        /// <param name="sessionManager">会话持久化管理器</param>
        /// <param name="sessionPresenter">会话状态 Presenter</param>
        /// <param name="sync">UI 跨线程同步器</param>
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

        /// <summary>跨线程安全调用：防 Disposed 异常与死锁</summary>
        private void SafeInvoke(Action action)
        {
            if (action == null) return;
            Control c = _sync as Control;
            if (c != null && (c.IsDisposed || !c.IsHandleCreated)) return;
            if (_sync != null && _sync.InvokeRequired)
            {
                try { _sync.BeginInvoke(action, null); } catch { }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// 发送纯文本消息：根据当前选中节点自动路由为局域网 UDP/TCP 组播广播或单节点直接私聊
        /// </summary>
        /// <param name="text">待发送的正文内容</param>
        public void SendTextMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            string myNick = _netService != null ? _netService.NickName : Environment.UserName;
            PeerInfo target = _sessionPresenter.SelectedPeer;

            if (target == null)
            {
                // 1. 全员群聊广播
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

                // 异步后台线程分发网络封包，不阻塞 UI 响应
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
                // 2. 好友私聊
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

        /// <summary>
        /// 处理来自底层网络协议栈接收到的文本消息：会话归类、持久化、未读累加与通知触发
        /// </summary>
        /// <param name="msg">收到的消息实体模型</param>
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

            // 若用户当前没有在看该会话，则累加未读数并触发通知
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

        /// <summary>
        /// 在当前激活会话中插入一条灰色系统居中通知消息（如加入网络、IP绑定状态等）
        /// </summary>
        /// <param name="text">系统通知文案</param>
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

        /// <summary>清空当前激活会话的历史聊天记录</summary>
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

        /// <summary>获取当前活跃会话的全量已缓存历史消息集合</summary>
        /// <returns>消息列表</returns>
        public List<ChatMessage> GetActiveMessages()
        {
            return _sessionManager != null
                ? _sessionManager.GetMessages(_sessionPresenter.ActiveChatKey)
                : new List<ChatMessage>();
        }
    }
}
