using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 会话状态与列表编排 Presenter
    /// <para>【职责】负责维护全员群聊、在线节点与离线历史会话的状态机维护、模糊搜索过滤与切页防抖。</para>
    /// <para>【线程安全】跨线程网络回调（上线/更新/下线）通过注入的 ISynchronizeInvoke 统一安全封送到 UI 主线程。</para>
    /// </summary>
    public class SessionPresenter
    {
        // 分组标题是列表中的说明行，不能作为会话选中。
        internal enum GroupHeading { Online, Offline }

        /// <summary>全员广播会话显示标识常量</summary>
        public const string BROADCAST_TARGET = "📢 局域网全员群聊";

        private readonly NetworkService _netService;
        private readonly SessionManager _sessionManager;
        private readonly ISynchronizeInvoke _sync;

        private readonly List<object> _allSessionItems = new List<object>();
        private readonly List<object> _filteredSessionItems = new List<object>();
        // 保留本次运行中见过的好友。旧会话仍从持久化聊天记录恢复。
        private readonly Dictionary<string, PeerInfo> _knownPeers = new Dictionary<string, PeerInfo>(StringComparer.OrdinalIgnoreCase);
        private string _searchFilter = "";

        private string _activeChatKey = SessionManager.BROADCAST_KEY;
        private PeerInfo _selectedPeer = null;
        private string _selectedTitle;
        private bool _selectedOnline;
        private int _broadcastUnreadCount = 0;

        /// <summary>会话列表数据源刷新通知事件（主界面据此执行 BeginUpdate/EndUpdate 重绑）</summary>
        public event Action SessionsUpdated;
        /// <summary>当前活跃会话切换事件 (参数：sessionKey, peerInfo, 标题文本, 输入框占位符, 是否显示群聊昵称)</summary>
        public event Action<string, PeerInfo, string, string, bool> ActiveSessionChanged;
        /// <summary>新节点加入局域网通知事件</summary>
        public event Action<PeerInfo> PeerJoinedNotice;

        /// <summary>获取或设置当前活跃的本地会话键。</summary>
        public string ActiveChatKey
        {
            get { return _activeChatKey; }
            set { _activeChatKey = value; }
        }

        /// <summary>获取或设置当前选中的单聊好友节点（若处于群聊则为 null）</summary>
        public PeerInfo SelectedPeer
        {
            get { return _selectedPeer; }
            set { _selectedPeer = value; }
        }

        /// <summary>获取或设置群聊广播的未读消息计数</summary>
        public int BroadcastUnreadCount
        {
            get { return _broadcastUnreadCount; }
            set { _broadcastUnreadCount = value; }
        }

        /// <summary>获取包含全员群聊、在线好友与历史离线节点的全部会话项集合</summary>
        public List<object> AllSessionItems
        {
            get { return _allSessionItems; }
        }

        /// <summary>获取经过搜索关键字过滤后的可见会话项列表</summary>
        public List<object> FilteredSessionItems
        {
            get { return _filteredSessionItems; }
        }

        /// <summary>获取或设置当前搜索过滤词（赋值后自动执行匹配并对外派发 SessionsUpdated）</summary>
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

        /// <summary>
        /// 初始化会话 Presenter 并挂接网络服务节点发现监听
        /// </summary>
        /// <param name="netService">网络通信服务</param>
        /// <param name="sessionManager">会话持久化与未读管理器</param>
        /// <param name="sync">跨线程 UI 同步器</param>
        public SessionPresenter(NetworkService netService, SessionManager sessionManager, ISynchronizeInvoke sync)
        {
            _netService = netService;
            _sessionManager = sessionManager;
            _sync = sync;

            if (_netService != null)
            {
                _netService.OnPeerJoined += (peer) => SafeInvoke(() => {
                    RememberPeer(peer);
                    RefreshSessions();
                    if (PeerJoinedNotice != null) PeerJoinedNotice(peer);
                });
                _netService.OnPeerUpdated += (peer) => SafeInvoke(() => {
                    RememberPeer(peer);
                    RefreshSessions();
                });
                _netService.OnPeerLeft += (peerId) => SafeInvoke(() => RefreshSessions());
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
        /// 完整重建会话列表项：置顶局域网全员群聊、按在线状态排列节点并追加历史离线好友
        /// </summary>
        public void RefreshSessions()
        {
            var peers = _netService != null ? _netService.GetActivePeers() : new List<PeerInfo>();

            _allSessionItems.Clear();
            _allSessionItems.Add(BROADCAST_TARGET);
            _allSessionItems.Add(GroupHeading.Online);
            HashSet<string> addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool activeHistoryMerged = false;
            foreach (var p in peers)
            {
                if (_sessionManager != null && _sessionManager.LinkPeer(p.Id, p.IP) &&
                    string.Equals(_activeChatKey, GetSessionKey(p), StringComparison.OrdinalIgnoreCase))
                    activeHistoryMerged = true;
            }

            // 重启后的新节点与尚未超时的旧节点可能短暂共存，优先显示最近确认在线的节点。
            foreach (var group in peers.GroupBy(GetSessionKey, StringComparer.OrdinalIgnoreCase))
            {
                addedIds.Add(group.Key);
                var peer = group.OrderByDescending(item => item.LastSeen).First();
                _allSessionItems.Add(peer);
                RememberPeer(peer);
            }

            _allSessionItems.Add(GroupHeading.Offline);
            if (_sessionManager != null)
            {
                var keys = _sessionManager.GetAllSessionKeys();
                foreach (var k in keys)
                {
                    if (string.IsNullOrEmpty(k) || k.Equals(SessionManager.BROADCAST_KEY, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!addedIds.Contains(k) && !_knownPeers.ContainsKey(k))
                    {
                        var lastMsg = _sessionManager.GetLastMessage(k);
                        var address = SessionManager.GetArchivedAddress(k);
                        string label = address != null ? address.ToString() : k.Substring(0, Math.Min(k.Length, 8));
                        PeerInfo offlinePeer = new PeerInfo
                        {
                            Id = k,
                            NickName = _sessionManager.GetArchivedNick(k) ?? "历史会话 (" + label + ")",
                            HostName = "离线",
                            IP = address,
                            LastMessage = _sessionManager.GetLastMessageSnippet(k, 24),
                            LastMessageTime = lastMsg != null ? lastMsg.Timestamp : DateTime.MinValue
                        };
                        _allSessionItems.Add(offlinePeer);
                        addedIds.Add(k);
                    }
                }
            }

            foreach (var item in _knownPeers)
                if (!addedIds.Contains(item.Key)) _allSessionItems.Add(item.Value);

            var activePeer = _allSessionItems.OfType<PeerInfo>().FirstOrDefault(peer =>
                string.Equals(GetSessionKey(peer), _activeChatKey, StringComparison.OrdinalIgnoreCase));
            if (activePeer != null) SelectSession(activePeer, activeHistoryMerged);

            ApplyFilter();
            if (SessionsUpdated != null) SessionsUpdated();
        }

        internal IEnumerable<IPAddress> GetKnownAddresses()
        {
            return _knownPeers.Values.Select(peer => peer.IP)
                .Concat(_sessionManager.GetAllSessionKeys().Select(SessionManager.GetArchivedAddress))
                .Where(ip => ip != null);
        }

        private void RememberPeer(PeerInfo peer)
        {
            string key = GetSessionKey(peer);
            // TCP 占位名不能覆盖已获得的好友昵称。
            if (peer.IP != null && peer.Id == peer.IP.ToString() && _knownPeers.ContainsKey(key)) return;
            _knownPeers[key] = new PeerInfo { Id = key, IP = peer.IP, NickName = GetPeerDisplayName(peer), HostName = "离线" };
        }

        internal string GetPeerDisplayName(PeerInfo peer)
        {
            if (peer.IP != null && peer.Id == peer.IP.ToString())
            {
                string key = GetSessionKey(peer);
                PeerInfo known;
                if (_knownPeers.TryGetValue(key, out known)) return known.NickName;
                string archivedNick = _sessionManager == null ? null : _sessionManager.GetArchivedNick(key);
                if (archivedNick != null) return archivedNick;
            }
            return peer.NickName ?? "未知好友";
        }

        internal string GetSessionKey(PeerInfo peer)
        {
            if (peer == null) return SessionManager.BROADCAST_KEY;
            return SessionManager.GetLanKey(peer.Id, peer.IP);
        }

        /// <summary>
        /// 执行会话模糊搜索过滤（匹配昵称、IP 地址与主机名）
        /// </summary>
        public void ApplyFilter()
        {
            string filter = _searchFilter.Trim().ToLowerInvariant();
            _filteredSessionItems.Clear();
            GroupHeading? heading = null;

            foreach (var item in _allSessionItems)
            {
                if (string.IsNullOrEmpty(filter))
                {
                    _filteredSessionItems.Add(item);
                }
                else if (item is GroupHeading)
                {
                    heading = (GroupHeading)item;
                }
                else if (item is string)
                {
                    if (((string)item).ToLowerInvariant().Contains(filter))
                        _filteredSessionItems.Add(item);
                }
                else if (item is PeerInfo)
                {
                    PeerInfo p = (PeerInfo)item;
                    string nick = GetPeerDisplayName(p).ToLowerInvariant();
                    string ip = (p.IP != null ? p.IP.ToString() : "").ToLowerInvariant();
                    string host = (p.HostName ?? "").ToLowerInvariant();
                    if (nick.Contains(filter) || ip.Contains(filter) || host.Contains(filter))
                    {
                        if (heading.HasValue)
                        {
                            _filteredSessionItems.Add(heading.Value);
                            heading = null;
                        }
                        _filteredSessionItems.Add(p);
                    }
                }
            }
        }

        /// <summary>
        /// 切换当前选中的会话（带相同会话防抖保护与未读清空）
        /// </summary>
        /// <param name="item">被选中的会话对象（string 或 PeerInfo）</param>
        public void SelectSession(object item)
        {
            SelectSession(item, false);
        }

        private void SelectSession(object item, bool historyChanged)
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
                newKey = GetSessionKey(newPeer);
                string nick = GetPeerDisplayName(newPeer);
                titleText = GrokTheme.SanitizeUiText(nick);
                placeholder = GrokTheme.SanitizeUiText(string.Format("给 {0} 发消息", nick));
                showGroupSender = false;
            }
            else
            {
                return;
            }

            // 相同会话防抖保护：避免重复点击引起视口闪烁与滚动位置重置
            bool online = newPeer != null && newPeer.TcpPort > 0;
            _selectedPeer = newPeer;
            ClearActiveChatUnread(newKey);
            if (!historyChanged && string.Equals(newKey, previousKey, StringComparison.OrdinalIgnoreCase) &&
                titleText == _selectedTitle && online == _selectedOnline)
            {
                return;
            }

            _activeChatKey = newKey;
            _selectedTitle = titleText;
            _selectedOnline = online;

            if (ActiveSessionChanged != null)
            {
                ActiveSessionChanged(newKey, newPeer, titleText, placeholder, showGroupSender);
            }
        }

        /// <summary>直接指定好友节点切换聊天会话</summary>
        /// <param name="peer">目标节点</param>
        public void SelectPeerChat(PeerInfo peer)
        {
            if (peer == null) return;
            SelectSession(peer);
        }

        /// <summary>从列表中删除指定好友的会话与历史缓存</summary>
        /// <param name="peer">待移除的节点</param>
        public void DeletePeerSession(PeerInfo peer)
        {
            if (peer == null) return;
            string peerId = peer.Id;

            if (_sessionManager != null)
            {
                _sessionManager.DeleteSession(GetSessionKey(peer));
            }
            _knownPeers.Remove(GetSessionKey(peer));

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

            if (string.Equals(_activeChatKey, GetSessionKey(peer), StringComparison.OrdinalIgnoreCase))
            {
                SelectSession(BROADCAST_TARGET);
            }

            ApplyFilter();
            if (SessionsUpdated != null) SessionsUpdated();
        }

        /// <summary>清空局域网全员群聊的历史消息</summary>
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

        /// <summary>清除当前处于焦点的会话的未读角标计数</summary>
        public void ClearActiveChatUnread()
        {
            ClearActiveChatUnread(_activeChatKey);
        }

        private void ClearActiveChatUnread(string key)
        {
            if (key == SessionManager.BROADCAST_KEY)
            {
                _broadcastUnreadCount = 0;
            }
            else if (_selectedPeer != null)
            {
                _selectedPeer.UnreadCount = 0;
                if (_netService != null)
                {
                    foreach (PeerInfo peer in _netService.GetActivePeers())
                    {
                        if (GetSessionKey(peer) == key) peer.UnreadCount = 0;
                    }
                }
            }
        }

        /// <summary>获取当前局域网处于活跃在线状态的节点总数</summary>
        public int GetOnlineCount()
        {
            return _netService != null ? _netService.GetActivePeers().Count : 0;
        }

        /// <summary>在过滤列表中查找指定会话对象的索引位置</summary>
        /// <param name="key">待查找的会话键，可为升级前的旧键。</param>
        /// <returns>匹配的行索引，未找到返回 -1</returns>
        public int FindSessionIndex(string key)
        {
            if (string.IsNullOrEmpty(key)) return -1;
            if (_sessionManager != null) key = _sessionManager.ResolveKey(key);
            for (int i = 0; i < _filteredSessionItems.Count; i++)
            {
                object item = _filteredSessionItems[i];
                if (!(item is GroupHeading) &&
                    string.Equals(GetSessionKey(item as PeerInfo), key, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
    }
}
