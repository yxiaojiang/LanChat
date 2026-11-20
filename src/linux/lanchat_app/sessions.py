"""将临时节点路由和长期会话键关联，独立维护存档、草稿和发送操作。"""

import hashlib
from dataclasses import dataclass

from .history import BROADCAST, HistoryStore


@dataclass
class Session:
    """会话列表快照。无当前路由时显示档案昵称和最后一条消息。"""

    key: str
    nick: str
    address: str
    preview: str
    online: bool


class SessionState:
    """管理单个客户端窗口的历史和操作状态，由 Tk 主线程持有。"""

    def __init__(self, store=None):
        self.store = store if store is not None else HistoryStore()
        self.histories = self.store.load()
        self.drafts = {}
        self.unread = {}
        self.pending = {}
        self.aliases = {}

    def resolve(self, key):
        """返回延迟执行的选择或发送操作应使用的会话键。"""
        return self.aliases.get(key, key)

    @staticmethod
    def key_for(sender_id, address, confirmed):
        """地址已通过物理子网或公告核实时使用 Windows 的 LAN-IPv4 键。"""
        if confirmed:
            return f"LAN-{address}"
        # 网络 ID 可能随重启变化。证据不足时隔离到节点和端点的联合键。
        digest = hashlib.sha256(f"{sender_id}|{address}".encode()).hexdigest()[:32]
        return f"PEER-{digest}"

    def route(self, key, peers):
        """返回会话的最新在线端点。优先公告节点，避免选择同地址的直连占位。"""
        matching = [p for p in peers if self.key_for(p.id, p.ip, p.address_confirmed) == key]
        return max(matching, key=lambda p: (p.announced, p.last_seen), default=None)

    def active_routes(self, peers):
        """以会话键去重，包含每个在线会话的当前路由。"""
        routes = {}
        for peer in sorted(peers, key=lambda p: (p.announced, p.last_seen), reverse=True):
            key = self.key_for(peer.id, peer.ip, peer.address_confirmed)
            routes.setdefault(key, peer)
        return routes

    def list_sessions(self, peers):
        """列出在线节点及有历史的离线档案，按会话键去重。"""
        routes = self.active_routes(peers)
        keys = dict.fromkeys([*routes, *self.histories])
        for key in keys:
            if key == BROADCAST:
                continue
            peer = routes.get(key)
            history = self.histories.get(key, [])
            last_message = next((item for item in reversed(history) if not item.get("is_sys")), {})
            nicknames = (item.get("peer_nick") or (item.get("sender") if not item.get("is_self") else "")
                         for item in reversed(history))
            nick = next((name for name in nicknames if name), "")
            address = key[4:] if key.startswith("LAN-") else last_message.get("remote_address") or ""
            yield Session(key, peer.nick if peer else nick or address or "离线好友",
                          f"{peer.ip}:{peer.port}" if peer else address,
                          last_message.get("text") or (peer.last_msg if peer else ""), peer is not None)

    def reconcile(self, peer):
        """合并同一实测地址的已确认节点和临时会话，返回迁移后的源和目标键。"""
        if not peer.address_confirmed:
            return []
        target = self.key_for(peer.id, peer.ip, True)
        merged = []
        for source_id in dict.fromkeys((peer.id, f"DIRECT-{peer.ip}")):
            source = self.key_for(source_id, peer.ip, False)
            self.store.merge(source, target)
            self.aliases[source] = target
            old_history = self.histories.pop(source, [])
            if old_history:
                target_history = self.histories.setdefault(target, [])
                identities = {item.get("operation_id") for item in target_history if item.get("operation_id")}
                target_history.extend(item for item in old_history if item.get("operation_id") not in identities)
                target_history.sort(key=lambda item: item.get("timestamp", ""))
            draft = self.drafts.pop(source, "")
            if draft:
                active = self.drafts.get(target, "")
                self.drafts[target] = f"{active}\n{draft}" if active and active != draft else active or draft
            count = self.unread.pop(source, 0)
            if count:
                self.unread[target] = self.unread.get(target, 0) + count
            for operation, (key, item) in self.pending.items():
                if key == source:
                    self.pending[operation] = (target, item)
            merged.append((source, target))
        return merged
