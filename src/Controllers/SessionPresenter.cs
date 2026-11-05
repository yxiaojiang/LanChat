using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace LanChat
{
    /// <summary>
    /// 会话状态与列表编排 Presenter：负责维护全员群聊、在线节点与离线历史会话的状态与筛选
    /// </summary>
    public class SessionPresenter
    {
        public const string BROADCAST_TARGET = "📢 局域网全员群聊";

        private readonly NetworkService _netService;
        private readonly SessionManager _sessionManager;
        private readonly ISynchronizeInvoke _sync;

        private readonly List<object> _allSessionItems = new List<object>();
        private readonly List<object> _filteredSessionItems = new List<object>();
        private string _searchFilter = "";

        private string _activeChatKey = "BROADCAST";
        private PeerInfo _selectedPeer = null;
        private int _broadcastUnreadCount = 0;

        public event Action SessionsUpdated;
        public event Action<string, PeerInfo, string, string, bool> ActiveSessionChanged;
        public event Action<PeerInfo> PeerJoinedNotice;

        public string ActiveChatKey
        {
            get { return _activeChatKey; }
            set { _activeChatKey = value; }
        }

        public PeerInfo SelectedPeer
        {
            get { return _selectedPeer; }
            set { _selectedPeer = value; }
        }

        public int BroadcastUnreadCount
        {
            get { return _broadcastUnreadCount; }
            set { _broadcastUnreadCount = value; }
        }

        public List<object> AllSessionItems
        {
            get { return _allSessionItems; }
        }

        public List<object> FilteredSessionItems
        {
            get { return _filteredSessionItems; }
        }

        public string SearchFilter
        {
            get { return _searchFilter; }
            set
            {
                _searchFilter = value ?? "";
                ApplyFilter();
                if (SessionsUpdated != null) SessionsUpdated();
            }
        }

        public SessionPresenter(NetworkService netService, SessionManager sessionManager, ISynchronizeInvoke sync)
        {
            _netService = netService;
            _sessionManager = sessionManager;
            _sync = sync;

            if (_netService != null)
            {
                _netService.OnPeerJoined += (peer) => SafeInvoke(() => {
                    RefreshSessions();
                    if (PeerJoinedNotice != null) PeerJoinedNotice(peer);
                });
                _netService.OnPeerUpdated += (peer) => SafeInvoke(() => RefreshSessions());
                _netService.OnPeerLeft += (peerId) => SafeInvoke(() => RefreshSessions());
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

        public void RefreshSessions()
        {
            var peers = _netService != null ? _netService.GetActivePeers() : new List<PeerInfo>();

            _allSessionItems.Clear();
            _allSessionItems.Add(BROADCAST_TARGET);
            HashSet<string> addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in peers)
            {
                _allSessionItems.Add(p);
                if (!string.IsNullOrEmpty(p.Id)) addedIds.Add(p.Id);
            }

            // 恢复历史聊天记录中的好友会话 (对标微信/QQ，重启或离线仍保留会话条目与历史查阅)
            if (_sessionManager != null)
            {
                var keys = _sessionManager.GetAllSessionKeys();
                foreach (var k in keys)
                {
                    if (string.IsNullOrEmpty(k) || k.Equals(SessionManager.BROADCAST_KEY, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!addedIds.Contains(k))
                    {
                        var lastMsg = _sessionManager.GetLastMessage(k);
                        string nick = (lastMsg != null && !string.IsNullOrEmpty(lastMsg.SenderNick) && !lastMsg.IsSelf) ? lastMsg.SenderNick : "历史好友 (" + k + ")";
                        PeerInfo offlinePeer = new PeerInfo
                        {
                            Id = k,
                            NickName = nick,
                            HostName = "离线",
                            LastMessage = _sessionManager.GetLastMessageSnippet(k, 24),
                            LastMessageTime = lastMsg != null ? lastMsg.Timestamp : DateTime.MinValue
                        };
                        _allSessionItems.Add(offlinePeer);
                        addedIds.Add(k);
                    }
                }
            }

            ApplyFilter();
            if (SessionsUpdated != null) SessionsUpdated();
        }

        public void ApplyFilter()
        {
            string filter = _searchFilter.Trim().ToLowerInvariant();
            _filteredSessionItems.Clear();

            foreach (var item in _allSessionItems)
            {
                if (string.IsNullOrEmpty(filter))
                {
                    _filteredSessionItems.Add(item);
                }
                else
                {
                    if (item is string)
                    {
                        if (((string)item).ToLowerInvariant().Contains(filter))
                            _filteredSessionItems.Add(item);
                    }
                    else if (item is PeerInfo)
                    {
                        PeerInfo p = (PeerInfo)item;
                        string nick = (p.NickName ?? "").ToLowerInvariant();
                        string ip = (p.IP != null ? p.IP.ToString() : "").ToLowerInvariant();
                        string host = (p.HostName ?? "").ToLowerInvariant();
                        if (nick.Contains(filter) || ip.Contains(filter) || host.Contains(filter))
                        {
                            _filteredSessionItems.Add(p);
                        }
                    }
                }
            }
        }

        public void SelectSession(object item)
        {
            if (item == null) return;

            string previousKey = _activeChatKey;
            string newKey;
            PeerInfo newPeer;
            string titleText;
            string placeholder;
            bool showGroupSender;

            if (item is string)
            {
                string sItem = (string)item;
                newPeer = null;
                newKey = "BROADCAST";
                _broadcastUnreadCount = 0;
                titleText = GrokTheme.SanitizeUiText(sItem);
                placeholder = GrokTheme.SanitizeUiText(string.Format("给 {0} 发消息", sItem));
                showGroupSender = (sItem == BROADCAST_TARGET);
            }
            else if (item is PeerInfo)
            {
                newPeer = (PeerInfo)item;
                newKey = newPeer.Id;
                newPeer.UnreadCount = 0;
                titleText = GrokTheme.SanitizeUiText(newPeer.NickName ?? "在线节点");
                placeholder = GrokTheme.SanitizeUiText(string.Format("给 {0} 发消息", newPeer.NickName ?? "好友"));
                showGroupSender = false;
            }
            else
            {
                return;
            }

            _selectedPeer = newPeer;
            _activeChatKey = newKey;

            if (ActiveSessionChanged != null)
            {
                ActiveSessionChanged(newKey, newPeer, titleText, placeholder, showGroupSender);
            }
        }

        public void SelectPeerChat(PeerInfo peer)
        {
            if (peer == null) return;
            SelectSession(peer);
        }

        public void DeletePeerSession(PeerInfo peer)
        {
            if (peer == null) return;
            string peerId = peer.Id;

            if (_sessionManager != null)
            {
                _sessionManager.DeleteSession(peerId);
            }

            if (_netService != null)
            {
                _netService.RemovePeer(peerId);
            }

            for (int i = _allSessionItems.Count - 1; i >= 0; i--)
            {
                object it = _allSessionItems[i];
                if (it is PeerInfo && ((PeerInfo)it).Id == peerId)
                {
                    _allSessionItems.RemoveAt(i);
                }
            }

            if (string.Equals(_activeChatKey, peerId, StringComparison.OrdinalIgnoreCase))
            {
                SelectSession(BROADCAST_TARGET);
            }

            ApplyFilter();
            if (SessionsUpdated != null) SessionsUpdated();
        }

        public void ClearBroadcastSession()
        {
            if (_sessionManager != null)
            {
                _sessionManager.ClearHistory(SessionManager.BROADCAST_KEY);
            }
            if (_activeChatKey == "BROADCAST")
            {
                SelectSession(BROADCAST_TARGET);
            }
            ApplyFilter();
            if (SessionsUpdated != null) SessionsUpdated();
        }

        public void ClearActiveChatUnread()
        {
            if (_activeChatKey == "BROADCAST")
            {
                _broadcastUnreadCount = 0;
            }
            else if (_selectedPeer != null)
            {
                _selectedPeer.UnreadCount = 0;
            }
        }

        public int GetOnlineCount()
        {
            return _netService != null ? _netService.GetActivePeers().Count : 0;
        }

        public int FindSessionIndex(object selected)
        {
            if (selected == null) return -1;
            string selStr = selected.ToString();
            for (int i = 0; i < _filteredSessionItems.Count; i++)
            {
                if (_filteredSessionItems[i].ToString() == selStr)
                    return i;
            }
            return -1;
        }
    }
}
