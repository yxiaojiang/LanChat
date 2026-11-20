const api = window.lanchat;
const byId = id => document.getElementById(id);
const group = 'BROADCAST';
const messages = new Map();
const drafts = new Map();
const unread = new Map();
const sending = new Set();
const peers = new Map();
const historyLoads = new Map();
const compose = byId('message');
const feed = byId('messages');
const dialog = byId('recipient');
let active = group;
let online = false;
let address = '';
let lastSequence = 0;

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
  byId('refresh').disabled = !online;
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
  drawChats();
  updateHeader();
  if (online) compose.focus();
  void loadHistory(active);
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
byId('refresh').addEventListener('click', async () => {
  try {
    setPeers(await api.refresh());
    status('已请求刷新局域网节点');
  } catch (error) {
    status(error.message, true);
  }
});

// 初始快照与事件之间可能有重叠，使用服务序号去重，保留快照等待期间收到的事件。
async function initialize() {
  const deferred = [];
  let waiting = true;
  api.onEvent(event => {
    if (waiting) deferred.push(event);
    else handleEvent(event);
  });
  try {
    const state = await api.state();
    address = state.ip + ':' + state.tcpPort;
    byId('nickname').textContent = state.nick;
    online = true;
    lastSequence = state.sequence;
    for (const conversation of state.sessions) {
      peers.set(conversation.key, { ...conversation, nick: conversation.nick || '局域网节点', online: false });
    }
    setPeers(state.peers);
    switchChat(group);
  } catch (error) {
    status(error.message, true);
    updateHeader();
  } finally {
    waiting = false;
    for (const event of deferred) handleEvent(event);
  }
}

function handleEvent(event) {
  if (event.event === 'peers') api.peers().then(setPeers, () => {});
  else if (event.event === 'message') remember(event.data);
  else if (event.event === 'linked') {
    const { oldKey, key } = event.data;
    peers.delete(oldKey);
    messages.delete(oldKey);
    historyLoads.delete(oldKey);
    const oldDraft = active === oldKey ? compose.value : drafts.get(oldKey);
    if (oldDraft && !drafts.has(key)) drafts.set(key, oldDraft);
    drafts.delete(oldKey);
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
    const action = event.sending ? '发送' : '接收';
    const percentage = event.total ? Math.round(100 * event.received / event.total) : 0;
    status(event.failed ? `${action}失败 · ${event.name} · ${event.error ?? ''}` :
      event.completed ? `${action}完成 · ${event.name}` : `${action}中 · ${event.name} · ${percentage}%`, event.failed);
  } else if (event.event === 'disconnect') {
    online = false;
    status(event.data, true);
    updateHeader();
  }
}

drawChats();
void initialize();
