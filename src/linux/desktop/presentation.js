// 主窗客户区的展示规则。度量对齐 WinForms，行内表情使用调用方提供的 DOM。
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.LanChatPresentation = api;
})(typeof globalThis === 'undefined' ? this : globalThis, function () {
  const GROUP = 'BROADCAST';
  const EMOJI_TOKEN = /\[[^\[\]\s]{1,24}\]/g;
  const graphemes = new Intl.Segmenter(undefined, { granularity: 'grapheme' });
  // 未导入本地表情时继续使用两端原有的 Unicode 表情。
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
  let emojiImages = new Map();
  let sendShortcutMode = 0;

  function setSendShortcutMode(value) {
    sendShortcutMode = Number(value) === 1 ? 1 : 0;
  }

  function setEmojiPack(entries) {
    emojiImages = new Map(entries);
  }

  function emojiEntries() {
    return emojiImages.size ? emojiImages : EMOJIS.map(symbol => [symbol, '']);
  }

  function drawMessageText(element, text, document, keepTokensTogether = false) {
    if ((!emojiImages.size && !keepTokensTogether) || !text.includes('[')) {
      element.textContent = text;
      return;
    }
    let start = 0;
    for (const match of text.matchAll(EMOJI_TOKEN)) {
      const imageUrl = emojiImages.get(match[0]);
      if (!imageUrl && !keepTokensTogether) continue;
      element.append(text.slice(start, match.index));
      const token = document.createElement('span');
      token.textContent = match[0];
      if (keepTokensTogether) token.className = 'summary-token';
      if (imageUrl) {
        token.classList.add('message-emoji');
        token.title = match[0];
        const image = document.createElement('img');
        image.alt = '';
        // 保留原令牌供复制。图片解码失败时，恢复原文字与自然宽度。
        image.addEventListener('error', () => {
          token.classList.remove('message-emoji');
          token.textContent = match[0];
        }, { once: true });
        image.src = imageUrl;
        token.append(image);
      }
      element.append(token);
      start = match.index + match[0].length;
    }
    element.append(text.slice(start));
  }

  function* visibleElements(text, keepTokensTogether) {
    let start = 0;
    if (keepTokensTogether) {
      // 只有接收方才知道资源是否存在，合法令牌即使缺图也作为一个元素保留。
      for (const match of text.matchAll(EMOJI_TOKEN)) {
        for (const { segment } of graphemes.segment(text.slice(start, match.index))) yield segment;
        yield match[0];
        start = match.index + match[0].length;
      }
    }
    for (const { segment } of graphemes.segment(text.slice(start))) yield segment;
  }

  function clip(text, limit, keepTokensTogether = true) {
    const value = text || '';
    if (value.length <= limit) return value;
    let count = 0;
    let end = 0;
    for (const segment of visibleElements(value, keepTokensTogether)) {
      count += 1;
      if (count > limit) return value.slice(0, end) + '...';
      if (count < limit) end += segment.length;
    }
    return value;
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
    return clip(name || '局域网节点', 13, false);
  }

  function chipText(nick) {
    const value = (nick || '').trim();
    return (value || '?').slice(0, 2);
  }

  function snippet(key, entry, messages, fallback) {
    const limit = key === GROUP ? 20 : 16;
    const last = messages.get(key)?.at(-1);
    if (last) {
      const body = last.file ? `[文件] ${last.name || ''}` : (last.text || '');
      const text = key === GROUP && last.nick ? `${last.nick}: ${body}` : body;
      return clip(text, limit);
    }
    if (fallback) return clip(fallback, limit);
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

  function shouldSendOnEnter(event) {
    return event.key === 'Enter' && !event.isComposing && event.keyCode !== 229 &&
      !event.shiftKey && !event.altKey && !event.metaKey &&
      (sendShortcutMode === 1 ? Boolean(event.ctrlKey) : !event.ctrlKey);
  }

  function shouldNewlineOnCtrlEnter(event) {
    return sendShortcutMode === 0 && event.key === 'Enter' && Boolean(event.ctrlKey) && !event.isComposing && event.keyCode !== 229 &&
      !event.shiftKey && !event.altKey && !event.metaKey;
  }

  function composePlaceholder(active, peer) {
    return active === GROUP ? '给 局域网全员群聊 发消息' : `给 ${peer?.nick || '好友'} 发消息`;
  }

  return {
    GROUP, EMOJIS, MENUS, setEmojiPack, emojiEntries, drawMessageText, clip, formatFileSize, unreadText, groupTitle, displayName, chipText,
    snippet, sessionRows, showTime, formatTimestamp, decorate, setSendShortcutMode, shouldSendOnEnter,
    shouldNewlineOnCtrlEnter, composePlaceholder
  };
});
