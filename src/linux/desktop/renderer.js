const api = window.lanchat;
const byId = id => document.getElementById(id);
const group = 'BROADCAST';
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

function status(line, error = false) {
  const output = byId('status');
  output.textContent = line;
  output.classList.toggle('error', error);
}

function setPeers(list) {
  for (const peer of peers.values()) peer.online = false;
  for (const peer of list) peers.set(peer.key, { ...peers.get(peer.key), ...peer, online: true });
  drawChats();
  updateHeader();
}

function drawChats() {
  const filter = byId('search').value.toLocaleLowerCase();
  const nav = byId('chats');
  nav.replaceChildren();
  const entries = [
    { key: group, nick: '局域网全员群聊', online: true, summary: peers.get(group)?.summary },
    ...Array.from(peers.values()).filter(peer => peer.key !== group)
      .sort((a, b) => Number(b.online) - Number(a.online) || a.nick.localeCompare(b.nick))
  ];
  for (const entry of entries) {
    if (!`${entry.nick} ${entry.ip ?? ''}`.toLocaleLowerCase().includes(filter)) continue;
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'chat';
    button.setAttribute('aria-current', String(entry.key === active));
    const avatar = document.createElement('img');
    avatar.src = entry.key === group ? 'assets/group.png' : 'assets/peer.png';
    avatar.alt = '';
    const labels = document.createElement('div');
    const title = document.createElement('strong');
    const badge = unread.get(entry.key);
    title.textContent = entry.nick + (badge ? `（${badge} 条未读）` : '');
    const summary = document.createElement('small');
    summary.textContent = messages.get(entry.key)?.at(-1)?.text || entry.summary ||
      (entry.online ? (entry.ip ?? '与在线节点聊天') : '离线');
    labels.append(title, summary);
    button.append(avatar, labels);
    button.addEventListener('click', () => switchChat(entry.key));
    nav.append(button);
  }
}

function updateHeader() {
  const peer = peers.get(active);
  byId('title').textContent = active === group ? '局域网全员群聊' : peer?.nick ?? '局域网节点';
  byId('avatar').src = active === group ? 'assets/group.png' : 'assets/peer.png';
  byId('connection').textContent = !online ? '通信服务已断开' : active === group ? address :
    peer?.online ? `${peer.ip}:${peer.port}` : '节点离线';
  const available = online && (active === group || peer?.online);
  byId('conversation').dataset.fileChat = online && peer?.online ? peer.id : '';
  byId('attach').disabled = !available;
  byId('refresh').disabled = connecting;
  byId('refresh').title = byId('refresh').ariaLabel = online ? '刷新节点' : '重连通信服务';
  byId('open-settings').disabled = !online;
  compose.disabled = !online;
  byId('send').disabled = !available || !compose.value.trim() || sending.has(active);
}

function addBubble(message) {
  const row = document.createElement('div');
  row.className = message.self ? 'message-row self' : 'message-row';
  const meta = document.createElement('small');
  meta.textContent = [message.nick, message.time].filter(Boolean).join('  ');
  const bubble = document.createElement('div');
  bubble.className = message.file ? 'bubble file' : 'bubble';
  bubble.textContent = message.file
    ? `文件 · ${message.name} · ${message.size} B${message.exists ? '' : ' · 原文件不可用'}`
    : message.text;
  row.append(meta, bubble);
  feed.append(row);
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
    addBubble(message);
    if (nearBottom || message.self) feed.scrollTop = feed.scrollHeight;
  } else if (!message.self) {
    unread.set(message.chat, (unread.get(message.chat) ?? 0) + 1);
  }
  if (message.storageError) status(message.storageError, true);
  drawChats();
}

function drawHistory() {
  feed.replaceChildren();
  for (const item of messages.get(active) ?? []) addBubble(item);
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

function switchChat(id) {
  drafts.set(active, compose.value);
  active = id;
  compose.value = drafts.get(id) ?? '';
  unread.delete(active);
  drawHistory();
  drawTransfers();
  drawChats();
  updateHeader();
  if (online) compose.focus();
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
  list.hidden = !list.childElementCount;
}

async function sendText() {
  const chat = active;
  const value = compose.value;
  if (byId('send').disabled || !value.trim()) return;
  sending.add(chat);
  updateHeader();
  try {
    const result = await api.sendText(chat === group ? group : peers.get(chat)?.id, value.trim());
    if (chat === active && compose.value === value) compose.value = '';
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
  dialog.returnValue = '';
  dialog.showModal();
  return new Promise(resolve => dialog.addEventListener('close', () => resolve(dialog.returnValue), { once: true }));
}

byId('attach').addEventListener('click', async () => {
  try {
    const chat = active === group ? await pickRecipient() : peers.get(active)?.id;
    if (!chat) return;
    const results = await api.selectFiles(chat);
    const failure = results.find(item => !item.ok);
    if (failure) status(failure.error, true);
  } catch (error) {
    status(error.message, true);
  }
});
byId('conversation').addEventListener('file-error', event => status(event.detail, true));
byId('compose').addEventListener('submit', event => { event.preventDefault(); void sendText(); });
compose.addEventListener('input', updateHeader);
compose.addEventListener('keydown', event => {
  if (event.key !== 'Enter' || event.isComposing || event.keyCode === 229 || event.shiftKey || event.ctrlKey) return;
  event.preventDefault();
  void sendText();
});
byId('search').addEventListener('input', drawChats);
byId('quit').addEventListener('click', api.quit);

// 更换只读配置需要再次确认。输入变化后应重新显示原路径的写入结果。
let pickedDirectory = null;
let pickedHistory = null;
let confirmOverride = false;
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
    if (!settingsDialog.open) settingsDialog.showModal();
  } catch (error) {
    status(error.message, true);
  }
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
    byId('nickname').textContent = result.nickname;
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
  connecting = true;
  updateHeader();
  try {
    if (reconnect) await api.reconnect();
    const state = await api.state();
    address = state.ip + ':' + state.tcpPort;
    byId('nickname').textContent = state.nick;
    online = true;
    lastSequence = state.sequence;
    historyLoads.clear();
    messages.clear();
    peers.clear();
    for (const conversation of state.sessions) {
      peers.set(conversation.key, { ...conversation, nick: conversation.nick || '局域网节点', online: false });
    }
    setPeers(state.peers);
    switchChat(peers.has(active) ? active : group);
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
      switchChat(key);
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

drawChats();
api.onEvent(event => {
  if (connecting) deferredEvents.push(event);
  else handleEvent(event);
});
void initialize();
