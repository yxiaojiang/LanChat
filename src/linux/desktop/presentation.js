// 主窗客户区的纯展示规则。度量对齐 WinForms 的 GrokTheme 与会话绘制，不访问 DOM 或网络。
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.LanChatPresentation = api;
})(typeof globalThis === 'undefined' ? this : globalThis, function () {
  const GROUP = 'BROADCAST';
  // 与 MainForm.Chat.cs 的表情表保持同一顺序和数量。
  const EMOJIS = [
    '😊', '😃', '😄', '😁', '😆', '😅', '😂', '🤣', '😇', '😉',
    '😋', '😍', '🥰', '😘', '😜', '🤔', '🤫', '🤐', '😴', '😷',
    '🥳', '🤩', '🥺', '😭', '😱', '😨', '😰', '😡', '🤬', '🤡',
    '👍', '👎', '👏', '🙏', '🤝', '🎉', '🔥', '❤️', '💯', '✨',
    '💪', '✌️', '🖐️', '✋', '👊', '☕', '💻', '📱', '🍺', '🎁'
  ];
  const MENUS = {
    peer: ['发送文件...', '删除'],
    group: ['发送文件给所有人...', '清空群聊记录'],
    attach: ['选择并发送文件...', '插入 Emoji 表情...', '打开接收文件夹', '清空当前对话记录']
  };

  function clip(text, limit) {
    const value = text || '';
    return value.length > limit ? value.slice(0, limit - 1) + '...' : value;
  }

  function formatFileSize(bytes) {
    const size = Number(bytes) || 0;
    if (size < 1024) return size + ' B';
    if (size < 1024 * 1024) return (size / 1024).toFixed(1) + ' KB';
    if (size < 1024 * 1024 * 1024) return (size / (1024 * 1024)).toFixed(2) + ' MB';
    return (size / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
  }

  function unreadText(count) {
    if (!count || count <= 0) return '';
    return count > 99 ? '99+' : String(count);
  }

  function groupTitle(onlineCount) {
    return onlineCount > 0 ? `局域网全员群聊 (${onlineCount})` : '局域网全员群聊';
  }

  function displayName(name) {
    return clip(name || '局域网节点', 13);
  }

  function chipText(nick) {
    const value = (nick || '').trim();
    return (value || '?').slice(0, 2);
  }

  function snippet(key, entry, messages, fallback) {
    const last = messages.get(key)?.at(-1);
    if (last) {
      const body = last.file ? `[文件] ${last.name || ''}` : (last.text || '');
      const text = key === GROUP && last.nick ? `${last.nick}: ${body}` : body;
      return clip(text, key === GROUP ? 20 : 16);
    }
    if (fallback) return fallback;
    if (key === GROUP) return '局域网会话';
    return entry?.online ? (entry.ip || '与在线节点聊天') : '离线';
  }

  function sessionRows({ peers, messages, filter }) {
    const people = [...peers.values()].filter(peer => peer.key && peer.key !== GROUP);
    const onlineCount = people.filter(peer => peer.online).length;
    const rows = [
      {
        key: GROUP, title: groupTitle(onlineCount), fullName: '局域网全员群聊',
        pinned: true, online: true,
        snippet: snippet(GROUP, null, messages, peers.get(GROUP)?.summary)
      },
      ...people
        .sort((a, b) => Number(Boolean(b.online)) - Number(Boolean(a.online)) || (a.nick || '').localeCompare(b.nick || ''))
        .map(peer => ({
          key: peer.key, title: displayName(peer.nick), fullName: peer.nick || '局域网节点',
          pinned: false, online: Boolean(peer.online), ip: peer.ip, host: peer.host, id: peer.id,
          snippet: snippet(peer.key, peer, messages, peer.summary)
        }))
    ];
    const needle = (filter || '').trim().toLocaleLowerCase();
    return rows.filter(row => `${row.key === GROUP ? '局域网全员群聊' : row.fullName} ${row.ip || ''} ${row.host || ''}`
      .toLocaleLowerCase().includes(needle));
  }

  // 间隔严格大于 3 分钟才插入时间条，和聊天控件的 TotalMinutes 判断一致。
  function showTime(previous, message) {
    if (!message?.timestamp) return false;
    if (!previous?.timestamp) return true;
    const delta = Date.parse(message.timestamp) - Date.parse(previous.timestamp);
    return Number.isFinite(delta) && delta > 3 * 60 * 1000;
  }

  function formatTimestamp(value, now = new Date()) {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    const clock = `${date.getHours()}:${String(date.getMinutes()).padStart(2, '0')}`;
    const sameDay = date.getFullYear() === now.getFullYear() && date.getMonth() === now.getMonth() &&
      date.getDate() === now.getDate();
    return sameDay ? `今天 ${clock}` : `${date.getMonth() + 1}月${date.getDate()}日 ${clock}`;
  }

  function decorate(previous, message, showNames) {
    const time = showTime(previous, message);
    const key = message.self ? 'SELF' : `PEER:${message.nick || ''}`;
    const previousKey = !previous || previous.system ? '' : previous.self ? 'SELF' : `PEER:${previous.nick || ''}`;
    return {
      showTime: time,
      timeLabel: time ? formatTimestamp(message.timestamp) : '',
      showSender: Boolean(showNames) && !message.self && !message.system && !(key === previousKey && !time)
    };
  }

  // Ctrl+Enter 换行是第 3 步采纳的输入契约；WinForms 单行框会吞掉组合键，这里不照搬。
  function shouldSendOnEnter(event) {
    return event.key === 'Enter' && !event.isComposing && event.keyCode !== 229 &&
      !event.shiftKey && !event.ctrlKey && !event.altKey && !event.metaKey;
  }

  function shouldNewlineOnCtrlEnter(event) {
    return event.key === 'Enter' && Boolean(event.ctrlKey) && !event.isComposing && event.keyCode !== 229 &&
      !event.shiftKey && !event.altKey && !event.metaKey;
  }

  function composePlaceholder(active, peer) {
    return active === GROUP ? '给 局域网全员群聊 发消息' : `给 ${peer?.nick || '好友'} 发消息`;
  }

  return {
    GROUP, EMOJIS, MENUS, clip, formatFileSize, unreadText, groupTitle, displayName, chipText,
    snippet, sessionRows, showTime, formatTimestamp, decorate, shouldSendOnEnter,
    shouldNewlineOnCtrlEnter, composePlaceholder
  };
});
