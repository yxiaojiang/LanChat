const api = window.lanchat;
const presentation = window.LanChatPresentation;
const byId = id => document.getElementById(id);
const group = presentation.GROUP;
const messages = new Map();
const drafts = new Map();
const unread = new Map();
const sending = new Set();
const transfers = new Map();
const peers = new Map();
const historyLoads = new Map();
const compose = byId('message');
const feed = byId('messages');
const dialog = byId('recipient');
const settingsDialog = byId('settings');
let active = group;
let online = false;
let address = '';
let lastSequence = 0;
let connecting = true;
let deferredEvents = [];
let connectionGeneration = 0;
let composeScroll = null;

function status(line, error = false) {
  const output = byId('status');
  output.textContent = line;
  output.classList.toggle('error', error);
}

function paintNick(nick) {
  const name = nick || 'LanChat';
  byId('nickname').textContent = name;
  byId('nickname-chip').textContent = presentation.chipText(name);
}

function setPeers(list) {
  for (const peer of peers.values()) peer.online = false;
  for (const peer of list) peers.set(peer.key, { ...peers.get(peer.key), ...peer, online: true });
  drawChats();
  updateHeader();
}

function focusSession(id) {
  const button = [...byId('chats').querySelectorAll('.chat')].find(item => item.dataset.key === id);
  button?.focus();
}

function drawChats() {
  const restoreKey = document.activeElement?.classList?.contains('chat') ? document.activeElement.dataset.key : '';
  const nav = byId('chats');
  const previousScrollTop = nav.scrollTop;
  nav.replaceChildren();
  const rows = presentation.sessionRows({ peers, messages, filter: byId('search').value });
  for (const entry of rows) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = entry.pinned ? 'chat pinned' : 'chat';
    button.dataset.key = entry.key;
    button.setAttribute('aria-current', String(entry.key === active));
    const count = presentation.unreadText(unread.get(entry.key));
    button.setAttribute('aria-label', [entry.fullName, entry.pinned ? '置顶' : '', count ? `${count} 条未读` : ''].filter(Boolean).join('，'));
    const card = document.createElement('span');
    card.className = 'chat-card';
    const avatarWrap = document.createElement('span');
    avatarWrap.className = 'avatar-wrap';
    const avatar = document.createElement('img');
    avatar.src = entry.key === group ? 'assets/group.png' : 'assets/peer.png';
    avatar.alt = '';
    avatarWrap.append(avatar);
    if (count) {
      const badge = document.createElement('span');
      badge.className = 'badge';
      badge.textContent = count;
      avatarWrap.append(badge);
    }
    const labels = document.createElement('span');
    labels.className = 'chat-copy';
    const titleRow = document.createElement('span');
    titleRow.className = 'title-row';
    const title = document.createElement('strong');
    title.textContent = entry.title;
    titleRow.append(title);
    if (entry.pinned) {
      const pin = document.createElement('em');
      pin.className = 'pin';
      pin.textContent = '置顶';
      titleRow.append(pin);
    }
    const summary = document.createElement('small');
    summary.textContent = entry.snippet;
    labels.append(titleRow, summary);
    card.append(avatarWrap, labels);
    button.append(card);
    button.addEventListener('click', () => switchChat(entry.key, true));
    button.addEventListener('contextmenu', event => {
      event.preventDefault();
      const point = { x: event.clientX, y: event.clientY };
      switchChat(entry.key, false);
      const current = [...nav.querySelectorAll('.chat')].find(item => item.dataset.key === entry.key);
      window.LanChatOverlay.openMenu(entry.key === group ? groupMenu() : peerMenu(entry), point, current);
    });
    nav.append(button);
  }
  nav.scrollTop = previousScrollTop;
  if (restoreKey) focusSession(restoreKey);
}

function syncComposeHeight() {
  compose.style.height = '23px';
  if (compose.value) {
    compose.style.height = `${Math.min(115, Math.max(23, compose.scrollHeight))}px`;
  }
  composeScroll?.refresh();
}

function updateHeader() {
  const peer = peers.get(active);
  byId('title').textContent = active === group
    ? presentation.groupTitle([...peers.values()].filter(item => item.key !== group && item.online).length)
    : peer?.nick ?? '局域网节点';
  byId('avatar').src = active === group ? 'assets/group.png' : 'assets/peer.png';
  byId('connection').textContent = !online ? '通信服务已断开' : active === group ? address :
    peer?.online ? `${peer.ip}:${peer.port}` : '节点离线';
  const available = online && (active === group || peer?.online);
  byId('conversation').dataset.fileChat = !online ? '' : active === group ? group : peer?.online ? peer.id : 'OFFLINE';
  byId('attach').disabled = !online;
  byId('refresh').disabled = !api || connecting;
  byId('refresh').title = byId('refresh').ariaLabel = online ? '刷新节点' : '重连通信服务';
  byId('open-settings').disabled = !online;
  compose.disabled = !online;
  compose.placeholder = presentation.composePlaceholder(active, peer);
  byId('send').disabled = !available || !compose.value.trim() || sending.has(active);
}

function appendRendered(message, previous, target = feed) {
  const view = presentation.decorate(previous, message, message.chat === group);
  if (view.showTime) {
    const time = document.createElement('div');
    time.className = 'time-header';
    time.textContent = view.timeLabel;
    target.append(time);
  }
  const row = document.createElement('div');
  row.className = message.system ? 'message-row system' : message.self ? 'message-row self' : 'message-row';
  if (message.system) {
    row.textContent = message.text || '';
    target.append(row);
    return;
  }
  if (view.showSender) {
    const sender = document.createElement('div');
    sender.className = 'sender';
    sender.textContent = message.nick || '节点';
    row.append(sender);
  }
  if (message.file) row.append(fileCard(message));
  else {
    const bubble = document.createElement('div');
    bubble.className = 'bubble';
    bubble.tabIndex = -1;
    bubble.textContent = message.text || '';
    row.append(bubble);
  }
  target.append(row);
}

function fileCard(message) {
  const card = document.createElement('article');
  card.className = 'file-card';
  const badge = document.createElement('span');
  badge.className = 'file-badge';
  badge.setAttribute('aria-hidden', 'true');
  const badgeIcon = document.createElement('img');
  badgeIcon.src = 'assets/BoxWhite.png';
  badgeIcon.alt = '';
  badge.append(badgeIcon);
  const name = document.createElement('strong');
  name.textContent = message.name || '未知文件';
  const size = document.createElement('small');
  size.textContent = presentation.formatFileSize(message.size) + (message.exists === false ? ' · 原文件不可用' : '');
  const actions = document.createElement('div');
  actions.className = 'file-actions';
  actions.append(fileAction('打开文件', message, 'file', message.exists === false), fileAction('打开目录', message, 'directory', false));
  card.append(badge, name, size, actions);
  return card;
}

function fileAction(label, message, kind, unavailable) {
  const button = document.createElement('button');
  button.type = 'button';
  button.textContent = label;
  button.disabled = unavailable || !message.path;
  button.addEventListener('click', async () => {
    try {
      await api.openPath(message.path, kind);
    } catch (error) {
      status(error.message, true);
    }
  });
  return button;
}

function remember(message) {
  if (message.sequence <= lastSequence) return;
  lastSequence = message.sequence;
  if (!messages.has(message.chat)) messages.set(message.chat, []);
  messages.get(message.chat).push(message);
  if (message.chat !== group && !peers.has(message.chat)) {
    peers.set(message.chat, { key: message.chat, nick: message.peerNick || (!message.self && message.nick) || '局域网节点', online: false });
  }
  if (message.chat === active) {
    const nearBottom = feed.scrollHeight - feed.scrollTop - feed.clientHeight < 32;
    const list = messages.get(message.chat);
    appendRendered(message, list.length > 1 ? list[list.length - 2] : null);
    if (nearBottom || message.self) feed.scrollTop = feed.scrollHeight;
  } else if (!message.self) {
    unread.set(message.chat, (unread.get(message.chat) ?? 0) + 1);
  }
  if (message.storageError) status(message.storageError, true);
  drawChats();
}

function drawHistory() {
  const fragment = document.createDocumentFragment();
  let previous = null;
  for (const item of messages.get(active) ?? []) {
    appendRendered(item, previous, fragment);
    previous = item;
  }
  feed.replaceChildren(fragment);
  feed.scrollTop = feed.scrollHeight;
}

async function loadHistory(chat) {
  if (!online || historyLoads.has(chat)) return;
  const operation = Symbol();
  historyLoads.set(chat, operation);
  try {
    const page = await api.history(chat);
    if (historyLoads.get(chat) !== operation) return;
    const newer = (messages.get(chat) ?? []).filter(message => message.sequence > page.sequence);
    messages.set(chat, [...page.messages, ...newer]);
    if (active === chat) drawHistory();
    drawChats();
  } catch (error) {
    if (historyLoads.get(chat) === operation) status(error.message, true);
  } finally {
    if (historyLoads.get(chat) === operation) historyLoads.delete(chat);
  }
}

function switchChat(id, focusCompose = true, force = false) {
  if (!force && id === active) {
    if (unread.delete(active)) {
      drawChats();
      updateHeader();
    }
    if (online && focusCompose) compose.focus();
    else if (!focusCompose) focusSession(id);
    return;
  }
  drafts.set(active, compose.value);
  active = id;
  compose.value = drafts.get(id) ?? '';
  syncComposeHeight();
  unread.delete(active);
  drawHistory();
  drawTransfers();
  drawChats();
  updateHeader();
  if (online && focusCompose) compose.focus();
  else focusSession(id);
  void loadHistory(active);
}

function drawTransfers() {
  const list = byId('transfers');
  list.replaceChildren();
  for (const transfer of transfers.values()) {
    if (transfer.chat !== active) continue;
    const row = document.createElement('div');
    const action = transfer.sending ? '发送' : '接收';
    const percentage = transfer.total ? Math.round(100 * transfer.received / transfer.total) : 0;
    row.textContent = transfer.unknown ? `${action}结果未确认 · ${transfer.name}` :
      transfer.failed ? `${action}失败 · ${transfer.name} · ${transfer.error ?? ''}` :
      transfer.completed ? `${action}完成 · ${transfer.name}` :
      `${action}中 · ${transfer.name} · ${percentage}%`;
    list.append(row);
  }
  const empty = !list.childElementCount;
  list.hidden = empty;
  list.parentElement.hidden = empty;
}

async function sendText() {
  const chat = active;
  const value = compose.value;
  if (!value.trim()) return;
  if (online && chat !== group && !peers.get(chat)?.online) {
    status('好友当前离线，暂时无法发送消息', true);
    return;
  }
  if (byId('send').disabled) return;
  sending.add(chat);
  updateHeader();
  try {
    const result = await api.sendText(chat === group ? group : peers.get(chat)?.id, value.trim());
    if (chat === active && compose.value === value) {
      compose.value = '';
      syncComposeHeight();
    }
    if (chat !== active && drafts.get(chat) === value) drafts.delete(chat);
    status(`已确认 ${result.confirmed} / ${result.total} 个节点`);
  } catch (error) {
    status(error.message, true);
  } finally {
    sending.delete(chat);
    updateHeader();
  }
}

function pickRecipient() {
  if (dialog.open) return Promise.resolve('');
  const options = byId('recipients');
  options.replaceChildren();
  for (const peer of peers.values()) {
    if (!peer.online) continue;
    const button = document.createElement('button');
    button.value = peer.id;
    button.className = 'pill';
    button.textContent = peer.nick + ' · ' + peer.ip;
    options.append(button);
  }
  if (!options.childElementCount) {
    status('没有在线节点', true);
    return Promise.resolve('');
  }
  const previousFocus = document.activeElement;
  dialog.returnValue = '';
  dialog.showModal();
  (options.querySelector('button') || byId('recipient-cancel')).focus();
  return new Promise(resolve => dialog.addEventListener('close', () => {
    if (previousFocus && previousFocus.isConnected) previousFocus.focus();
    resolve(dialog.returnValue);
  }, { once: true }));
}

// 群聊菜单沿用 Windows 文案。WinForms 实际仍先选一个接收人，并不把文件广播给所有节点。
async function sendFilesFor(chatKey) {
  try {
    let chat = '';
    if (chatKey === group) {
      chat = await pickRecipient();
    } else {
      const peer = peers.get(chatKey);
      if (!peer?.online || !peer?.id) {
        status('好友当前离线，暂时无法发送文件', true);
        return;
      }
      chat = peer.id;
    }
    if (!chat) return;
    const results = await api.selectFiles(chat);
    const failure = results.find(item => !item.ok);
    if (failure) status(failure.error, true);
  } catch (error) {
    status(error.message, true);
  }
}

function insertEmoji(symbol) {
  const start = compose.selectionStart ?? compose.value.length;
  const end = compose.selectionEnd ?? start;
  compose.setRangeText(symbol, start, end, 'end');
  syncComposeHeight();
  compose.focus();
  updateHeader();
}

function groupMenu() {
  const labels = presentation.MENUS.group;
  return [
    { label: labels[0], icon: 'Folder', disabled: !online, run: () => { void sendFilesFor(group); } },
    { separator: true },
    { label: labels[1], icon: 'ClearDanger', danger: true, disabled: !online, run: () => { void confirmForget(group, false); } }
  ];
}

function peerMenu(entry) {
  const labels = presentation.MENUS.peer;
  return [
    { label: labels[0], icon: 'Folder', disabled: !online, run: () => { void sendFilesFor(entry.key); } },
    { separator: true },
    { label: labels[1], icon: 'ClearDanger', danger: true, disabled: !online, run: () => { void confirmForget(entry.key, true); } }
  ];
}

function attachMenu(anchor) {
  const labels = presentation.MENUS.attach;
  const rect = anchor.getBoundingClientRect();
  window.LanChatOverlay.openMenu([
    { label: labels[0], icon: 'Folder', disabled: !online, run: () => { void sendFilesFor(active); } },
    { label: labels[1], icon: 'Emoji', disabled: !online, run: () => window.LanChatOverlay.openEmoji(anchor, insertEmoji, anchor) },
    { label: labels[2], icon: 'Folder', disabled: !online, run: () => { void openDownloads(); } },
    { separator: true },
    { label: labels[3], icon: 'ClearDanger', danger: true, disabled: !online, run: () => { void forgetChat(active, false); } }
  ], { x: rect.left, y: rect.top - 8, above: true }, anchor);
}

async function copyText(text) {
  if (!text) return;
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      return;
    }
  } catch {}
  const helper = document.createElement('textarea');
  helper.value = text;
  helper.style.position = 'fixed';
  helper.style.opacity = '0';
  document.body.append(helper);
  helper.select();
  document.execCommand('copy');
  helper.remove();
}

function selectBubbleText(bubble) {
  const selection = window.getSelection();
  if (!selection) return;
  const range = document.createRange();
  range.selectNodeContents(bubble);
  selection.removeAllRanges();
  selection.addRange(range);
}

function bubbleSelectedText(bubble) {
  const selection = window.getSelection();
  if (!selection || selection.isCollapsed || !selection.rangeCount) return '';
  const range = selection.getRangeAt(0);
  return bubble.contains(range.commonAncestorContainer) ? selection.toString() : '';
}

async function openDownloads() {
  try {
    await api.openDownloads();
  } catch (error) {
    status(error.message, true);
  }
}

async function confirmForget(key, removePeer) {
  const name = peers.get(key)?.nick || '未知节点';
  const accepted = await window.LanChatOverlay.confirmAction(removePeer ? {
    title: '删除确认',
    message: `确定要删除好友“${name}”及全部历史消息吗？\n删除后历史记录将无法恢复。`,
    confirmLabel: '删除'
  } : {
    title: '清空确认',
    message: '确定要清空局域网全员群聊的全部历史消息吗？\n删除后历史记录将无法恢复。',
    confirmLabel: '清空'
  });
  if (accepted) await forgetChat(key, removePeer);
}

async function forgetChat(key, removePeer) {
  const token = Symbol();
  historyLoads.set(key, token);
  try {
    const list = await api.deleteSession(key, removePeer);
    if (historyLoads.get(key) !== token) return;
    messages.set(key, []);
    unread.delete(key);
    if (peers.has(key)) peers.get(key).summary = '';
    for (const [id, item] of transfers) if (item.chat === key) transfers.delete(id);
    if (removePeer) {
      drafts.delete(key);
      peers.delete(key);
    }
    setPeers(list);
    if (removePeer) peers.delete(key);
    if (removePeer && active === key) switchChat(group, true);
    else {
      if (active === key) drawHistory();
      drawTransfers();
      drawChats();
      updateHeader();
    }
  } catch (error) {
    if (historyLoads.get(key) === token) historyLoads.delete(key);
    status(error.message, true);
    void loadHistory(key);
  } finally {
    if (historyLoads.get(key) === token) historyLoads.delete(key);
  }
}

byId('attach').addEventListener('click', () => {
  if (window.LanChatOverlay.consumeAttachToggle()) return;
  attachMenu(byId('attach'));
});
byId('conversation').addEventListener('file-error', event => status(event.detail, true));
byId('conversation').addEventListener('file-drop', async event => {
  try {
    const chat = await pickRecipient();
    const results = await api.confirmDrop(chat, event.detail);
    if (!chat || !results?.length) return;
    const failure = results.find(item => !item.ok);
    if (failure) status(failure.error, true);
  } catch (error) {
    status(error.message, true);
  }
});
feed.addEventListener('contextmenu', event => {
  const bubble = event.target.closest('.bubble');
  if (!bubble) return;
  event.preventDefault();
  const full = bubble.textContent || '';
  const selected = bubbleSelectedText(bubble);
  window.LanChatOverlay.openMenu([
    { label: '复制', icon: 'Copy', hint: 'Ctrl+C', run: () => { void copyText(selected || full); } },
    { label: '复制全文', icon: 'Copy', run: () => { void copyText(full); } },
    { separator: true },
    { label: '全选', icon: 'User', hint: 'Ctrl+A', keepSelection: true, run: () => selectBubbleText(bubble) }
  ], { x: event.clientX, y: event.clientY }, bubble);
});
feed.addEventListener('keydown', event => {
  if (!(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey || event.key.toLowerCase() !== 'a') return;
  const anchorNode = window.getSelection()?.anchorNode;
  const anchorEl = anchorNode?.nodeType === 1 ? anchorNode : anchorNode?.parentElement;
  const bubble = document.activeElement?.closest?.('.bubble') || anchorEl?.closest?.('.bubble');
  if (!bubble || !feed.contains(bubble)) return;
  event.preventDefault();
  selectBubbleText(bubble);
});
byId('compose').addEventListener('submit', event => { event.preventDefault(); void sendText(); });
compose.addEventListener('input', () => {
  syncComposeHeight();
  updateHeader();
});
compose.addEventListener('keydown', event => {
  if (presentation.shouldNewlineOnCtrlEnter(event)) {
    event.preventDefault();
    const start = compose.selectionStart ?? compose.value.length;
    const end = compose.selectionEnd ?? start;
    const atEnd = end === compose.value.length;
    compose.setRangeText('\n', start, end, 'end');
    syncComposeHeight();
    if (atEnd) compose.scrollTop = compose.scrollHeight;
    updateHeader();
    return;
  }
  if (!presentation.shouldSendOnEnter(event)) return;
  event.preventDefault();
  void sendText();
});
byId('search').addEventListener('input', () => {
  window.LanChatOverlay.closeMenu();
  byId('search-clear').hidden = !byId('search').value;
  drawChats();
  byId('search').focus();
});
byId('search').addEventListener('keydown', event => {
  if (event.key !== 'Escape' || !byId('search').value) return;
  event.preventDefault();
  byId('search').value = '';
  byId('search-clear').hidden = true;
  drawChats();
  byId('search').focus();
});
byId('search-clear').addEventListener('click', () => {
  byId('search').value = '';
  byId('search-clear').hidden = true;
  drawChats();
  byId('search').focus();
});
byId('chats').addEventListener('keydown', event => {
  if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
  const rows = [...byId('chats').querySelectorAll('.chat')];
  if (!rows.length) return;
  const index = rows.findIndex(button => button.dataset.key === active);
  const next = index < 0
    ? (event.key === 'ArrowDown' ? rows[0] : rows.at(-1))
    : rows[index + (event.key === 'ArrowDown' ? 1 : -1)];
  if (!next) return;
  event.preventDefault();
  switchChat(next.dataset.key, false);
});
byId('chats').addEventListener('scroll', () => window.LanChatOverlay.closeMenu());
byId('quit').addEventListener('click', () => api?.quit());

// 更换只读配置需要再次确认。输入变化后应重新显示原路径的写入结果。
let pickedDirectory = null;
let pickedHistory = null;
let confirmOverride = false;
let settingsTrigger = null;
function resetSettingsConfirmation() {
  confirmOverride = false;
  byId('settings-save').textContent = '保存';
  byId('settings-message').hidden = true;
}
function settingsMessage(text) {
  byId('settings-message').textContent = text;
  byId('settings-message').hidden = false;
}
byId('open-settings').addEventListener('click', async () => {
  try {
    const current = await api.settings();
    byId('settings-nickname').value = current.nickname;
    byId('settings-auto-accept').checked = current.autoAcceptFiles;
    byId('settings-directory').textContent = current.downloadDirectory;
    byId('settings-history').textContent = current.historyDirectory;
    pickedDirectory = null;
    pickedHistory = null;
    resetSettingsConfirmation();
    settingsTrigger = document.activeElement;
    if (!settingsDialog.open) settingsDialog.showModal();
  } catch (error) {
    status(error.message, true);
  }
});
settingsDialog.addEventListener('close', () => {
  if (settingsTrigger && settingsTrigger.isConnected) settingsTrigger.focus();
  settingsTrigger = null;
});
byId('settings-form').addEventListener('input', resetSettingsConfirmation);
byId('settings-cancel').addEventListener('click', () => settingsDialog.close());
async function pickSettingsDirectory(kind) {
  try {
    const folder = await api.selectSettingsDirectory(kind);
    if (!folder) return;
    if (kind === 'history') {
      pickedHistory = folder;
      byId('settings-history').textContent = folder;
    } else {
      pickedDirectory = folder;
      byId('settings-directory').textContent = folder;
    }
    resetSettingsConfirmation();
  } catch (error) {
    settingsMessage(error.message);
  }
}
byId('settings-pick-directory').addEventListener('click', () => pickSettingsDirectory('downloads'));
byId('settings-pick-history').addEventListener('click', () => pickSettingsDirectory('history'));
byId('settings-form').addEventListener('submit', async event => {
  event.preventDefault();
  const save = byId('settings-save');
  if (save.disabled) return;
  const controls = Array.from(event.currentTarget.elements);
  for (const control of controls) control.disabled = true;
  try {
    const result = await api.saveSettings(byId('settings-nickname').value, byId('settings-auto-accept').checked,
      pickedDirectory, confirmOverride, pickedHistory);
    if (result.overridePath) {
      confirmOverride = true;
      settingsMessage(`原设置只读，已备份到\n${result.backupPath}\n\n可改用\n${result.overridePath}`);
      save.textContent = '使用备用位置';
      return;
    }
    paintNick(result.nickname);
    settingsDialog.close();
    status('设置已保存');
  } catch (error) {
    settingsMessage(error.message);
  } finally {
    for (const control of controls) control.disabled = false;
  }
});
byId('refresh').addEventListener('click', async () => {
  if (!online) {
    await initialize(true);
    return;
  }
  try {
    setPeers(await api.refresh());
    status('已请求刷新局域网节点');
  } catch (error) {
    status(error.message, true);
  }
});

// 从重启后的服务重新读取会话和历史，保留输入草稿与未确认的传输状态。
async function initialize(reconnect = false) {
  if (!api) {
    connecting = false;
    online = false;
    status('桌面桥接不可用', true);
    updateHeader();
    return;
  }
  connecting = true;
  updateHeader();
  try {
    if (reconnect) await api.reconnect();
    const state = await api.state();
    address = state.ip + ':' + state.tcpPort;
    paintNick(state.nick);
    online = true;
    lastSequence = state.sequence;
    historyLoads.clear();
    messages.clear();
    peers.clear();
    for (const conversation of state.sessions) {
      peers.set(conversation.key, { ...conversation, nick: conversation.nick || '局域网节点', online: false });
    }
    setPeers(state.peers);
    switchChat(peers.has(active) ? active : group, true, true);
    if (reconnect) status('通信服务已重新连接');
  } catch (error) {
    online = false;
    status(error.message, true);
  } finally {
    connecting = false;
    for (const event of deferredEvents) handleEvent(event);
    deferredEvents = [];
    updateHeader();
  }
}

function handleEvent(event) {
  if (event.event === 'peers' && online) {
    const generation = connectionGeneration;
    api.peers().then(list => { if (online && generation === connectionGeneration) setPeers(list); }, () => {});
  }
  else if (event.event === 'message') remember(event.data);
  else if (event.event === 'linked') {
    const { oldKey, key } = event.data;
    peers.delete(oldKey);
    messages.delete(oldKey);
    historyLoads.delete(oldKey);
    const oldDraft = active === oldKey ? compose.value : drafts.get(oldKey);
    if (oldDraft && !drafts.has(key)) drafts.set(key, oldDraft);
    drafts.delete(oldKey);
    if (unread.has(oldKey)) {
      unread.set(key, (unread.get(key) ?? 0) + unread.get(oldKey));
      unread.delete(oldKey);
    }
    if (active === oldKey) {
      active = key;
      compose.value = drafts.get(key) ?? '';
      syncComposeHeight();
      switchChat(key, true, true);
    }
    else if (active === key) {
      historyLoads.delete(key);
      void loadHistory(key);
    }
    drawChats();
  }
  else if (event.event === 'progress') {
    const key = JSON.stringify([connectionGeneration, event.sending, event.transfer, event.chat]);
    transfers.set(key, event);
    if (!peers.has(event.chat)) {
      peers.set(event.chat, { key: event.chat, nick: event.peerNick || '局域网节点', online: false });
      drawChats();
    }
    if (event.completed || event.failed) {
      const finished = Array.from(transfers).filter(([, item]) => item.completed || item.failed);
      for (const [id] of finished.slice(0, Math.max(0, finished.length - 50))) transfers.delete(id);
    }
    drawTransfers();
  } else if (event.event === 'disconnect') {
    online = false;
    connectionGeneration++;
    for (const item of transfers.values()) {
      if (!item.completed && !item.failed) item.unknown = true;
    }
    drawTransfers();
    status(event.data, true);
    updateHeader();
  }
}

window.LanChatScroll.bind(byId('chats'));
window.LanChatScroll.bind(byId('messages'));
window.LanChatScroll.bind(byId('transfers'));
composeScroll = window.LanChatScroll.bind(compose);
window.LanChatScroll.bind(byId('recipients'));
drawChats();
if (!api) {
  connecting = false;
  status('桌面桥接不可用', true);
  updateHeader();
} else {
  api.onEvent(event => {
    if (connecting) deferredEvents.push(event);
    else handleEvent(event);
  });
  void initialize();
}
