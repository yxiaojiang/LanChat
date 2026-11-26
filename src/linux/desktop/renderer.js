(function (root, factory) {
  const api = factory(root);
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.LanChatMainApp = api;
  if (!(typeof process === 'object' && process?.env?.LANCHAT_TEST_MODE === '1') && typeof document !== 'undefined') {
    api.createMainController();
  }
})(typeof globalThis === 'undefined' ? this : globalThis, function (root) {
const { createSessionSidebar } = typeof module === 'object' && module.exports
  ? require('./session-sidebar.js') : root.LanChatSessionSidebar;
const { createConversationView } = typeof module === 'object' && module.exports
  ? require('./conversation-view.js') : root.LanChatConversationView;

// 主窗协调通信服务、会话状态与输入草稿，视图通过回调触发主窗操作。
function createMainController(deps = {}) {
const doc = deps.document || root.document;
const win = deps.window || root.window || root;
const api = deps.api !== undefined ? deps.api : win.lanchat;
const presentation = deps.presentation || root.LanChatPresentation;
const appearance = deps.appearance || root.LanChatAppearance;
const overlay = deps.overlay || root.LanChatOverlay;
const scroll = deps.scroll || root.LanChatScroll;
const byId = id => doc.getElementById(id);
const group = presentation.GROUP;
const messages = new Map();
const drafts = new Map();
const attachments = new Map();
const unread = new Map();
const sending = new Set();
const transfers = new Map();
const peers = new Map();
const historyLoads = new Map();
const compose = byId('message');
const feed = byId('messages');
const dialog = byId('recipient');
let active = group;
let online = false;
let address = '';
let lastSequence = 0;
let connecting = true;
let deferredEvents = [];
let connectionGeneration = 0;
let composeScroll = null;
const sidebar = createSessionSidebar({
  doc, presentation, overlay, switchChat, redraw: drawChats,
  menuFor: entry => entry.key === group ? groupMenu() : peerMenu(entry)
});
const conversation = createConversationView({
  doc, win, feed, presentation, overlay, status,
  openPath: (path, kind) => api.openPath(path, kind)
});

function applyAppearance(settingsData) {
  if (!appearance || !settingsData) return;
  appearance.applyAppearance(doc.documentElement, settingsData);
  syncComposeHeight();
}

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
  for (const peer of list) {
    const previous = peers.get(peer.key);
    // 旧版 TCP 确认只能得到地址，占位名不得覆盖已知或归档昵称。
    const nick = peer.id === peer.ip && previous?.nick ? previous.nick : peer.nick;
    peers.set(peer.key, { ...previous, ...peer, nick, online: true });
  }
  drawChats();
  updateHeader();
}

function drawChats() {
  sidebar.render({ peers, messages, unread, active });
  const count = [...unread.values()].reduce((total, value) => total + value, 0);
  const first = unread.keys().next().value;
  api?.syncUnreadState?.({
    count,
    summary: first ? (first === group ? '局域网全员群聊' : peers.get(first)?.nick || '局域网节点') : ''
  });
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
  for (const id of ['open-downloads', 'send-file', 'take-screenshot', 'clear-chat'])
    byId(id).disabled = !available;
  byId('refresh').disabled = !api || connecting;
  byId('refresh').title = byId('refresh').ariaLabel = online ? '刷新好友列表' : '重连通信服务';
  byId('open-settings').disabled = !online;
  compose.disabled = !online;
  compose.placeholder = presentation.composePlaceholder(active, peer);
  compose.dataset.draftChat = active;
  const hasFiles = Boolean(attachments.get(active)?.length);
  byId('send').disabled = !available || (!compose.value.trim() && !hasFiles) || sending.has(active);
  byId('send').title = byId('send').ariaLabel = hasFiles ? '发送附件' : '发送消息';
}

function appendMessage(message) {
  if (!messages.has(message.chat)) messages.set(message.chat, []);
  const list = messages.get(message.chat);
  const previous = list.at(-1);
  list.push(message);
  if (message.chat !== group && !peers.has(message.chat)) {
    peers.set(message.chat, { key: message.chat, nick: message.peerNick || (!message.self && message.nick) || '局域网节点', online: false });
  }
  if (message.chat === active) conversation.appendMessage(message, previous);
}

// 发送用本地请求号，接收用对端传输号和会话键。相同文件名和并行收发均独立更新。
function transferKey(sendingFile, chat, transfer, operation) {
  return JSON.stringify([connectionGeneration, sendingFile, sendingFile && operation ? operation : [chat, transfer]]);
}

function remember(message) {
  if (message.sequence <= lastSequence) return;
  lastSequence = message.sequence;
  const key = transferKey(message.self, message.chat, message.transfer, message.operation);
  const pending = message.file && transfers.get(key);
  if (pending && pending.chat === message.chat) {
    const list = messages.get(message.chat);
    list[list.indexOf(pending)] = message;
    if (message.chat === active) conversation.replaceFileCard(pending, message);
  } else {
    if (pending) {
      const previousChat = messages.get(pending.chat);
      previousChat.splice(previousChat.indexOf(pending), 1);
      if (pending.chat === active) drawHistory(false);
    }
    appendMessage(message);
  }
  if (pending) transfers.delete(key);
  // 窗口隐藏、最小化或失焦时，即使是当前会话的新消息也必须进入托盘未读链。
  const windowInactive = Boolean(doc.hidden) || (typeof doc.hasFocus === 'function' && !doc.hasFocus());
  if (!message.self && (message.chat !== active || windowInactive)) {
    unread.set(message.chat, (unread.get(message.chat) ?? 0) + 1);
  }
  if (message.storageError) status(message.storageError, true);
  drawChats();
}

function showTransfer(event) {
  const key = transferKey(event.sending, event.chat, event.transfer, event.operation);
  const pending = transfers.get(key);
  if (pending) {
    if (pending.transferState.failed || pending.transferState.completed) return;
    pending.transferState = { ...pending.transferState, ...event, queued: event.queued === true };
    conversation.paintProgress(pending);
  } else {
    const message = {
      chat: event.chat, self: event.sending, nick: event.peerNick, peerNick: event.peerNick,
      file: true, name: event.name, size: event.total, timestamp: new Date().toISOString(), transferState: event
    };
    transfers.set(key, message);
    appendMessage(message);
    drawChats();
  }
  if (event.failed) pruneTransfers();
}

function pruneTransfers() {
  const terminal = [...transfers].filter(([, item]) => item.transferState.failed || item.transferState.unknown);
  let redraw = false;
  for (const [key, item] of terminal.slice(0, Math.max(0, terminal.length - 50))) {
    transfers.delete(key);
    const list = messages.get(item.chat);
    list.splice(list.indexOf(item), 1);
    redraw ||= item.chat === active;
  }
  if (redraw) drawHistory(false);
}

function drawHistory(scrollToBottom = true) {
  conversation.render(messages.get(active) ?? [], scrollToBottom);
}

async function loadHistory(chat) {
  if (!online || historyLoads.has(chat)) return;
  const operation = Symbol();
  historyLoads.set(chat, operation);
  try {
    const page = await api.history(chat);
    if (historyLoads.get(chat) !== operation) return;
    const newer = (messages.get(chat) ?? []).filter(message => message.transferState || message.sequence > page.sequence);
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
    else if (!focusCompose) sidebar.focusSession(id);
    return;
  }
  drafts.set(active, compose.value);
  active = id;
  compose.value = drafts.get(id) ?? '';
  syncComposeHeight();
  unread.delete(active);
  drawHistory();
  drawAttachments();
  drawChats();
  updateHeader();
  if (online && focusCompose) compose.focus();
  else sidebar.focusSession(id);
  void loadHistory(active);
}

function drawAttachments() {
  const list = byId('attachments');
  const files = attachments.get(active) ?? [];
  list.replaceChildren();
  for (const file of files) {
    const chat = active;
    const item = doc.createElement('div');
    item.className = 'attachment';
    item.title = `${file.name} · ${presentation.formatFileSize(file.size)}`;
    const icon = doc.createElement('img');
    icon.src = file.preview || 'assets/BoxWhite.png';
    if (file.preview) icon.className = 'attachment-preview';
    icon.alt = '';
    const name = doc.createElement('span');
    name.textContent = file.name;
    const remove = doc.createElement('button');
    remove.type = 'button';
    remove.textContent = '×';
    remove.setAttribute('aria-label', `移除附件 ${file.name}`);
    remove.addEventListener('click', () => {
      if (sending.has(chat)) return;
      removeAttachments(chat, [file]);
      void api.discardPastedFiles([file.id]).catch(error => status(error.message, true));
      compose.focus();
    });
    item.append(icon, name, remove);
    list.append(item);
  }
  byId('attachments-host').hidden = !files.length;
}

function removeAttachments(chat, files) {
  const removed = new Set(files.map(file => file.id));
  const remaining = (attachments.get(chat) ?? []).filter(file => !removed.has(file.id));
  if (remaining.length) attachments.set(chat, remaining);
  else attachments.delete(chat);
  if (chat === active) drawAttachments();
  updateHeader();
}

async function sendCompose() {
  const chat = active;
  const value = compose.value;
  const files = attachments.get(chat) ?? [];
  if (!value.trim() && !files.length) return;
  if (online && chat !== group && !peers.get(chat)?.online) {
    status('好友当前离线，暂时无法发送消息', true);
    return;
  }
  if (byId('send').disabled) return;
  sending.add(chat);
  updateHeader();
  try {
    if (files.length) {
      const recipient = chat === group ? await pickRecipient(true) : peers.get(chat)?.id;
      if (!recipient || (Array.isArray(recipient) && !recipient.length)) return;
      await api.sendPastedFiles(recipient, files.map(file => file.id));
      removeAttachments(chat, files);
    }
    if (!value.trim()) return;
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

function pickRecipient(multiple = false) {
  if (dialog.open) return Promise.resolve(null);
  const options = byId('recipients');
  const confirm = byId('recipient-confirm');
  const selected = new Set();
  confirm.hidden = !multiple;
  confirm.disabled = true;
  options.replaceChildren();
  for (const peer of peers.values()) {
    if (!peer.online) continue;
    const button = doc.createElement('button');
    button.value = peer.id;
    button.className = 'pill';
    button.setAttribute('aria-pressed', 'false');
    button.textContent = peer.nick + ' · ' + peer.ip;
    button.addEventListener('click', event => {
      if (!multiple) {
        dialog.returnValue = peer.id;
        dialog.close();
        return;
      }
      event.preventDefault();
      if (selected.has(peer.id)) selected.delete(peer.id);
       else if (selected.size < 16) selected.add(peer.id);
       else status('最多选择 16 个在线节点', true);
      button.setAttribute('aria-pressed', String(selected.has(peer.id)));
      confirm.disabled = selected.size === 0;
      confirm.textContent = selected.size ? `发送到 ${selected.size} 个节点` : '发送到选中节点';
    });
    options.append(button);
  }
  if (!options.childElementCount) {
    status('没有在线节点', true);
    return Promise.resolve(null);
  }
  const onlinePeers = [...peers.values()].filter(peer => peer.online);
  if (multiple && onlinePeers.length === 1) return Promise.resolve([onlinePeers[0].id]);
  const previousFocus = doc.activeElement;
  confirm.hidden = !multiple;
  confirm.disabled = multiple;
  confirm.textContent = '发送到选中节点';
  dialog.returnValue = '';
  dialog.showModal();
  (options.querySelector('button') || (multiple ? confirm : byId('recipient-cancel'))).focus();
  if (multiple) {
    confirm.addEventListener('click', event => {
      if (selected.size === 0) return;
      event.preventDefault();
      dialog.returnValue = JSON.stringify([...selected]);
      dialog.close();
    }, { once: true });
  }
  return new Promise(resolve => dialog.addEventListener('close', () => {
    confirm.hidden = true;
    if (previousFocus && previousFocus.isConnected) previousFocus.focus();
    if (!multiple) resolve(dialog.returnValue);
    else {
       try { resolve(dialog.returnValue ? JSON.parse(dialog.returnValue) : null); }
       catch { resolve(null); }
    }
  }, { once: true }));
}

async function sendFilesFor(chatKey) {
  try {
    let chat = '';
    if (chatKey === group) {
      chat = await pickRecipient(true);
    } else {
      const peer = peers.get(chatKey);
      if (!peer?.online || !peer?.id) {
        status('好友当前离线，暂时无法发送文件', true);
        return;
      }
      chat = peer.id;
    }
    if (!chat || (Array.isArray(chat) && !chat.length)) return;
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
  if (compose.value.length - (end - start) + symbol.length > compose.maxLength) {
    compose.focus();
    return;
  }
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
  overlay.openMenu([
    { label: labels[0], icon: 'Folder', disabled: !online, run: () => { void sendFilesFor(active); } },
    { label: labels[1], icon: 'Emoji', disabled: !online, run: () => overlay.openEmoji(anchor, insertEmoji, anchor) },
    { label: labels[2], icon: 'Folder', disabled: !online, run: () => { void openDownloads(); } },
    { separator: true },
    { label: labels[3], icon: 'ClearDanger', danger: true, disabled: !online, run: () => { void forgetChat(active, false); } }
  ], { x: rect.left, y: rect.top - 8, above: true }, anchor);
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
  const target = key === group ? '局域网全员群聊' : `好友“${name}”的当前会话`;
  const accepted = await overlay.confirmAction(removePeer ? {
    title: '删除确认',
    message: `确定要删除好友“${name}”及全部历史消息吗？\n删除后历史记录将无法恢复。`,
    confirmLabel: '删除'
  } : {
    title: '清空确认',
    message: `确定要清空${target}的全部历史消息吗？\n好友不会被删除。`,
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
      const files = attachments.get(key) ?? [];
      removeAttachments(key, files);
      if (files.length) void api.discardPastedFiles(files.map(file => file.id)).catch(() => {});
      drafts.delete(key);
      peers.delete(key);
    }
    setPeers(list);
    if (removePeer) peers.delete(key);
    if (removePeer && active === key) switchChat(group, true);
    else {
      if (active === key) drawHistory();
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
  overlay.openEmoji(byId('attach'), insertEmoji, byId('attach'));
});
byId('open-downloads').addEventListener('click', () => { void openDownloads(); });
byId('send-file').addEventListener('click', () => { void sendFilesFor(active); });
byId('take-screenshot').addEventListener('click', async () => {
  try {
    const result = await api.triggerScreenshot();
    status(result.message, !result.ok);
  } catch (error) {
    status(error.message, true);
  }
});
byId('clear-chat').addEventListener('click', () => { void confirmForget(active, false); });
byId('conversation').addEventListener('file-error', event => status(event.detail, true));
byId('conversation').addEventListener('file-drop', async event => {
  try {
    const chat = await pickRecipient(true);
    if (!chat || (Array.isArray(chat) && !chat.length)) return;
    const results = await api.confirmDrop(chat, event.detail);
    if (!results?.length) return;
    const failure = results.find(item => !item.ok);
    if (failure) status(failure.error, true);
  } catch (error) {
    status(error.message, true);
  }
});
byId('compose').addEventListener('submit', event => { event.preventDefault(); void sendCompose(); });
compose.addEventListener('files-pasted', event => {
  const { chat, files } = JSON.parse(event.detail);
  const existing = attachments.get(chat) ?? [];
  const available = Math.max(0, 32 - existing.length);
  attachments.set(chat, [...existing, ...files.slice(0, available)]);
  if (files.length > available) {
    void api.discardPastedFiles(files.slice(available).map(file => file.id)).catch(() => {});
    status('一次最多发送 32 个文件', true);
  }
  if (chat === active) drawAttachments();
  updateHeader();
});
compose.addEventListener('paste-error', event => status(event.detail, true));
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
  void sendCompose();
});
doc.addEventListener('keydown', event => {
  if (!(event.ctrlKey || event.metaKey) || event.altKey) return;
  if (event.key.toLowerCase() === 'f') {
    event.preventDefault();
    byId('search').focus();
    byId('search').select();
  } else if (event.key.toLowerCase() === 'l') {
    event.preventDefault();
    // Ctrl+L 必须复用现有二次确认，不允许静默清空当前会话。
    void confirmForget(active, false);
  }
});
byId('quit').addEventListener('click', () => api?.quit());

// 设置窗口独立为 1260×820 模态子窗体，主窗仅负责触发打开与焦点恢复。
let settingsTrigger = null;
byId('open-settings').addEventListener('click', async () => {
  try {
    settingsTrigger = doc.activeElement;
    await api.openSettings();
  } catch (error) {
    status(error.message, true);
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
    const [state, settingsData, emojiPack] = await Promise.all([
      api.state(),
      api.settings ? api.settings() : Promise.resolve(null),
      api.emojiPack ? api.emojiPack().catch(() => []) : []
    ]);
    presentation.setEmojiPack(emojiPack);
    if (settingsData) applyAppearance(settingsData);
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
    for (const item of transfers.values()) {
      if (!messages.has(item.chat)) messages.set(item.chat, []);
      messages.get(item.chat).push(item);
      if (!peers.has(item.chat)) peers.set(item.chat, { key: item.chat, nick: item.peerNick || '局域网节点', online: false });
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
  else if (event.event === 'activate-chat') {
    const key = event.data?.chat || event.data?.key;
    if (key) switchChat(key, true, true);
    else compose.focus();
  }
  else if (event.event === 'window-restored') {
    if (unread.delete(active)) drawChats();
  }
  else if (event.event === 'open-update-check') {
    void api?.openSettings?.();
  }
  else if (event.event === 'message') remember(event.data);
  else if (event.event === 'linked') {
    const { oldKey, key } = event.data;
    peers.delete(oldKey);
    const pending = (messages.get(oldKey) ?? []).filter(item => item.transferState);
    messages.delete(oldKey);
    if (pending.length) {
      if (!messages.has(key)) messages.set(key, []);
      for (const item of pending) {
        const state = item.transferState;
        const previous = transferKey(state.sending, oldKey, state.transfer, state.operation);
        if (transfers.delete(previous)) transfers.set(transferKey(state.sending, key, state.transfer, state.operation), item);
        item.chat = key;
        messages.get(key).push(item);
      }
    }
    historyLoads.delete(oldKey);
    const oldDraft = active === oldKey ? compose.value : drafts.get(oldKey);
    if (oldDraft && !drafts.has(key)) drafts.set(key, oldDraft);
    drafts.delete(oldKey);
    if (attachments.has(oldKey)) {
      attachments.set(key, [...(attachments.get(key) ?? []), ...attachments.get(oldKey)]);
      attachments.delete(oldKey);
      if (active === key) drawAttachments();
    }
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
  else if (event.event === 'progress') showTransfer(event);
  else if (event.event === 'file-error') status(event.data, true);
  else if (event.event === 'settings-updated') {
    if (event.data?.nickname) paintNick(event.data.nickname);
    if (event.data) applyAppearance(event.data);
    status('设置已保存');
  } else if (event.event === 'histories-cleared') {
    historyLoads.clear();
    messages.clear();
    for (const [key, item] of transfers) {
      if (item.transferState.failed || item.transferState.unknown) {
        transfers.delete(key);
      } else {
        if (!messages.has(item.chat)) messages.set(item.chat, []);
        messages.get(item.chat).push(item);
      }
    }
    unread.clear();
    for (const peer of peers.values()) peer.summary = '';
    drawHistory();
    drawChats();
    updateHeader();
    status('已彻底清空所有本地聊天记录');
  } else if (event.event === 'settings-closed') {
    if (settingsTrigger && settingsTrigger.isConnected) settingsTrigger.focus();
    settingsTrigger = null;
  } else if (event.event === 'disconnect') {
    online = false;
    connectionGeneration++;
    for (const item of transfers.values()) {
      const state = item.transferState;
      if (!state.completed && !state.failed) state.unknown = true;
      conversation.paintProgress(item);
    }
    pruneTransfers();
    status(event.data, true);
    updateHeader();
  }
}

scroll.bind(byId('chats'));
scroll.bind(byId('messages'));
scroll.bind(byId('attachments'));
composeScroll = scroll.bind(compose);
scroll.bind(byId('recipients'));
drawChats();
let initPromise = Promise.resolve();
if (!api) {
  connecting = false;
  status('桌面桥接不可用', true);
  updateHeader();
} else {
  api.onEvent(event => {
    if (connecting) deferredEvents.push(event);
    else handleEvent(event);
  });
  initPromise = initialize();
}
return { initPromise, messages, historyLoads, loadHistory, handleEvent, switchChat, applyAppearance };
}
return { createMainController };
});
