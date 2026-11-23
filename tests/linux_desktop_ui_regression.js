const assert = require('node:assert/strict');
const { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } = require('node:fs');
const Module = require('node:module');
const { tmpdir } = require('node:os');
const { join, resolve } = require('node:path');
const presentation = require('../src/linux/desktop/presentation.js');

const root = join(__dirname, '..');
const read = path => readFileSync(join(root, path), 'utf8');
const now = new Date(2026, 10, 23, 15, 4, 0);

assert.equal(presentation.formatFileSize(1023), '1023 B');
assert.equal(presentation.formatFileSize(1024), '1.0 KB');
assert.equal(presentation.formatFileSize(1536), '1.5 KB');
assert.equal(presentation.formatFileSize(1048576), '1.00 MB');
assert.equal(presentation.formatFileSize(1024 ** 3), '1.00 GB');
assert.equal(presentation.unreadText(0), '');
assert.equal(presentation.unreadText(99), '99');
assert.equal(presentation.unreadText(100), '99+');
assert.equal(presentation.groupTitle(0), '局域网全员群聊');
assert.equal(presentation.groupTitle(3), '局域网全员群聊 (3)');
assert.equal(presentation.displayName('1234567890123'), '1234567890123');
assert.equal(presentation.displayName('12345678901234'), '123456789012...');
assert.equal(presentation.chipText(' 阿兰 '), '阿兰');
assert.equal(presentation.chipText(''), '?');
assert.equal(presentation.clip('123456789012345678901', 20), '1234567890123456789...');
assert.equal(presentation.formatTimestamp('2026-11-23T05:51:00', now), '今天 5:51');
assert.equal(presentation.formatTimestamp('2026-11-22T05:51:00', now), '11月22日 5:51');

const earlier = { timestamp: '2026-11-23T05:00:00' };
assert.equal(presentation.showTime(null, earlier), true);
assert.equal(presentation.showTime(earlier, { timestamp: '2026-11-23T05:03:00' }), false);
assert.equal(presentation.showTime(earlier, { timestamp: '2026-11-23T05:03:01' }), true);
assert.equal(presentation.decorate(earlier, { timestamp: '2026-11-23T05:04:00', nick: '甲' }, true).showSender, true);
assert.equal(presentation.decorate(
  { timestamp: '2026-11-23T05:04:00', nick: '甲' },
  { timestamp: '2026-11-23T05:04:10', nick: '甲' }, true).showSender, false);

assert.equal(presentation.shouldSendOnEnter({ key: 'Enter' }), true);
assert.equal(presentation.shouldSendOnEnter({ key: 'Enter', ctrlKey: true }), false);
assert.equal(presentation.shouldSendOnEnter({ key: 'Enter', shiftKey: true }), false);
assert.equal(presentation.shouldSendOnEnter({ key: 'Enter', isComposing: true }), false);
assert.equal(presentation.shouldSendOnEnter({ key: 'Enter', keyCode: 229 }), false);
assert.equal(presentation.shouldNewlineOnCtrlEnter({ key: 'Enter', ctrlKey: true }), true);
assert.equal(presentation.shouldNewlineOnCtrlEnter({ key: 'Enter' }), false);
assert.equal(presentation.shouldNewlineOnCtrlEnter({ key: 'Enter', ctrlKey: true, shiftKey: true }), false);
assert.equal(presentation.shouldNewlineOnCtrlEnter({ key: 'Enter', ctrlKey: true, isComposing: true }), false);
assert.equal(presentation.shouldNewlineOnCtrlEnter({ key: 'Enter', ctrlKey: true, keyCode: 229 }), false);
assert.equal(presentation.composePlaceholder(presentation.GROUP, null), '给 局域网全员群聊 发消息');
assert.equal(presentation.composePlaceholder('LAN-10.0.0.2', { nick: '测试好友' }), '给 测试好友 发消息');
assert.deepEqual(presentation.MENUS.peer, ['发送文件...', '删除']);
assert.deepEqual(presentation.MENUS.group, ['发送文件给所有人...', '清空群聊记录']);
assert.equal(presentation.EMOJIS.length, 50);
assert.equal(presentation.EMOJIS[0], '😊');
assert.equal(presentation.EMOJIS.at(-1), '🎁');

const peers = new Map([
  ['LAN-10.0.0.8', { key: 'LAN-10.0.0.8', nick: '离线甲', ip: '10.0.0.8', host: 'alpha-box', online: false }],
  ['LAN-10.0.0.2', { key: 'LAN-10.0.0.2', nick: '在线乙', ip: '10.0.0.2', host: 'beta-box', online: true }]
]);
const messages = new Map([
  [presentation.GROUP, [{ nick: '丙', text: '大家好，这是一条会超长的群聊预览文本' }]]
]);
const rows = presentation.sessionRows({ peers, messages, filter: '' });
assert.deepEqual(rows.map(row => row.key), [presentation.GROUP, 'LAN-10.0.0.2', 'LAN-10.0.0.8']);
assert.equal(rows[0].title, '局域网全员群聊 (1)');
assert.equal(rows[0].pinned, true);
assert.equal(rows[0].snippet.endsWith('...'), true);
assert.equal(rows[2].snippet, '离线');
assert.equal(presentation.sessionRows({ peers, messages, filter: ' 乙 ' }).length, 1);
assert.equal(presentation.sessionRows({ peers, messages, filter: 'alpha-box' }).length, 1);

const longHistory = Array.from({ length: 2000 }, (_item, index) => ({
  timestamp: `2026-11-23T05:00:${String(index % 60).padStart(2, '0')}`, nick: '甲', text: `第 ${index} 条`
}));
const started = Date.now();
let previous = null;
let shown = 0;
for (const message of longHistory) {
  if (presentation.decorate(previous, message, true).showTime) shown += 1;
  previous = message;
}
assert.equal(shown > 0, true);
assert.equal(Date.now() - started < 1000, true);

const css = read('src/linux/desktop/desktop.css');
const html = read('src/linux/desktop/index.html');
const renderer = read('src/linux/desktop/renderer.js');
const main = read('src/linux/desktop/main.cjs');
const preload = read('src/linux/desktop/preload.cjs');
const service = read('src/linux/LanChat.Linux/DesktopService.cs');
for (const token of ['420px', '72px', '96px', 'width: 4px', '#7946d7', '340px', '#34343c', '#646470', '256px', '#2e2e34', 'touch-action: none', '0 10px 0 0']) {
  assert.equal(css.includes(token), true, token);
}
for (const id of ['id="chats"', 'id="messages"', 'id="menu"', 'id="emoji"', 'id="confirm"', 'id="nickname-chip"', 'scroll-host', 'role="dialog"']) {
  assert.equal(html.includes(id), true, id);
}
assert.equal(html.includes('<label class="search">'), false);
assert.equal(css.includes('scrollbar-width: thin'), false);
assert.equal(renderer.includes('lastIndexOf(message)'), false);
assert.equal(renderer.includes('createDocumentFragment'), true);
assert.equal(renderer.includes('BoxWhite.png'), true);
assert.equal(renderer.includes('shouldNewlineOnCtrlEnter'), true);
assert.equal(renderer.includes('composePlaceholder'), true);
assert.equal(renderer.includes('confirmDrop'), true);
assert.equal(renderer.includes('deleteSession'), true);
assert.equal(renderer.includes('openDownloads'), true);
assert.equal(main.includes("case 'open-path'") || main.includes("'open-path'"), true);
assert.equal(main.includes('open-downloads'), true);
assert.equal(main.includes('delete-session'), true);
assert.equal(main.includes('stage-drop'), true);
assert.equal(main.includes('confirm-drop'), true);
assert.equal(preload.includes('deleteSession'), true);
assert.equal(preload.includes('openPath'), true);
assert.equal(preload.includes('confirmDrop'), true);
assert.equal(service.includes('delete-session'), true);
assert.equal(service.includes('RemovePeer'), true);
assert.equal(service.includes('["path"]'), true);
assert.equal(service.includes('timestamp'), true);
assert.equal(service.indexOf('Publish("peers")', service.indexOf('case "delete-session"')) > service.indexOf('case "quit"'), true);

for (const path of [
  'src/linux/desktop/presentation.js', 'src/linux/desktop/scrollbar.js', 'src/linux/desktop/overlay.js',
  'src/linux/desktop/renderer.js', 'src/linux/desktop/desktop.css', 'src/linux/desktop/main.cjs',
  'src/linux/desktop/preload.cjs', 'src/linux/desktop/service.cjs'
]) {
  const lines = read(path).split(/\r?\n/).length;
  assert.equal(lines < 1500, true, `${path} 超过 1500 行`);
  assert.equal(lines < 1000, true, `${path} 已到 1000 行，新增行为前需要先拆分`);
}

async function testMainPathPolicy() {
  const opened = [];
  const originalLoad = Module._load;
  process.env.LANCHAT_TEST_MODE = '1';
  Module._load = function (request, parent, isMain) {
    if (request === 'electron') {
      return {
        app: {}, BrowserWindow: function () {}, dialog: {}, ipcMain: {}, session: {},
        shell: { openPath: async target => { opened.push(target); return ''; } }
      };
    }
    return originalLoad.apply(this, arguments);
  };
  let mainApi;
  try {
    mainApi = require('../src/linux/desktop/main.cjs');
  } finally {
    Module._load = originalLoad;
    delete process.env.LANCHAT_TEST_MODE;
  }

  const sandboxDir = mkdtempSync(join(tmpdir(), 'lanchat-open-path-'));
  try {
    const existing = join(sandboxDir, 'received.txt');
    const unrecorded = join(sandboxDir, 'other.txt');
    writeFileSync(existing, 'ok');
    writeFileSync(unrecorded, 'nope');

    await assert.rejects(() => mainApi.openPath(unrecorded, 'file'), /未在通信记录中登记/);
    mainApi.recordMessagePaths({ file: true, path: existing });
    await mainApi.openPath(existing, 'file');
    assert.equal(opened.at(-1), resolve(existing));

    rmSync(existing);
    await assert.rejects(() => mainApi.openPath(existing, 'file'));
    await mainApi.openPath(existing, 'directory');
    assert.equal(opened.at(-1), resolve(sandboxDir));

    const missingParentFile = join(sandboxDir, 'gone-dir', 'lost.txt');
    mainApi.recordMessagePaths({ file: true, path: missingParentFile });
    await assert.rejects(() => mainApi.openPath(missingParentFile, 'directory'), /所在目录不可用/);
  } finally {
    rmSync(sandboxDir, { recursive: true, force: true });
  }
}

function makeNode(tagName, id = '') {
  const listeners = new Map();
  const attrs = new Map();
  const children = [];
  return {
    tagName,
    id,
    hidden: false,
    disabled: false,
    isConnected: true,
    dataset: {},
    style: {},
    className: '',
    textContent: '',
    clientHeight: 115,
    scrollHeight: 115,
    scrollTop: 0,
    parentElement: null,
    children,
    classList: {
      add() {},
      remove() {},
      contains() { return false; }
    },
    setAttribute(name, value) { attrs.set(name, String(value)); },
    getAttribute(name) { return attrs.get(name); },
    append(...nodes) {
      for (const node of nodes) {
        if (typeof node === 'object' && node) {
          node.parentElement = this;
          children.push(node);
        }
      }
    },
    replaceChildren(...nodes) {
      children.length = 0;
      this.append(...nodes);
    },
    contains(target) {
      if (target === this) return true;
      return children.some(child => child.contains?.(target));
    },
    querySelectorAll() { return []; },
    querySelector() { return null; },
    getBoundingClientRect() { return { left: 10, top: 100, width: 256, height: 180 }; },
    focus() {},
    addEventListener(type, fn) {
      if (!listeners.has(type)) listeners.set(type, new Set());
      listeners.get(type).add(fn);
    },
    removeEventListener(type, fn) {
      listeners.get(type)?.delete(fn);
    },
    dispatch(type, event = {}) {
      for (const fn of [...(listeners.get(type) ?? [])]) fn(event);
    }
  };
}

function testScrollbarBehavior() {
  const originalDocument = global.document;
  global.document = { createElement: tag => makeNode(tag.toUpperCase()) };
  try {
    const scrollbar = require('../src/linux/desktop/scrollbar.js');
    const host = makeNode('DIV');
    const textarea = makeNode('TEXTAREA', 'message');
    host.append(textarea);
    const controller = scrollbar.bind(textarea);
    const thumb = host.children.at(-1);
    assert.equal(thumb.hidden, true);

    for (const key of ['ArrowUp', 'ArrowDown', 'Home', 'End', 'PageUp', 'PageDown']) {
      let prevented = false;
      textarea.dispatch('keydown', { key, target: textarea, preventDefault() { prevented = true; } });
      assert.equal(prevented, false, `textarea 不应拦截 ${key}`);
    }

    textarea.scrollHeight = 230;
    textarea.dispatch('input');
    assert.equal(thumb.hidden, false);
    assert.equal(thumb.style.height, '57.5px');
    textarea.scrollHeight = 115;
    controller.refresh();
    assert.equal(thumb.hidden, true);

    const feedHost = makeNode('DIV');
    const feed = makeNode('SECTION', 'messages');
    feed.clientHeight = 200;
    feed.scrollHeight = 600;
    feedHost.append(feed);
    scrollbar.bind(feed);
    let preventedPlain = false;
    feed.dispatch('keydown', { key: 'ArrowDown', target: feed, preventDefault() { preventedPlain = true; } });
    assert.equal(preventedPlain, true);
    assert.equal(feed.scrollTop, 48);

    let preventedShift = false;
    feed.dispatch('keydown', { key: 'ArrowDown', target: feed, shiftKey: true, preventDefault() { preventedShift = true; } });
    assert.equal(preventedShift, false);
    let preventedIme = false;
    feed.dispatch('keydown', { key: 'ArrowDown', target: feed, isComposing: true, preventDefault() { preventedIme = true; } });
    assert.equal(preventedIme, false);
  } finally {
    if (originalDocument === undefined) delete global.document;
    else global.document = originalDocument;
  }
}

async function testOverlayAttachToggle() {
  const originalDocument = global.document;
  const originalWindow = global.window;
  const doc = makeNode('DOCUMENT');
  const win = makeNode('WINDOW');
  win.innerWidth = 1040;
  win.innerHeight = 720;
  const menu = makeNode('DIV', 'menu');
  const emoji = makeNode('DIV', 'emoji');
  const confirm = makeNode('DIALOG', 'confirm');
  const attach = makeNode('BUTTON', 'attach');
  const attachImg = makeNode('IMG');
  attach.append(attachImg);
  menu.hidden = true;
  emoji.hidden = true;
  const nodes = new Map([['menu', menu], ['emoji', emoji], ['confirm', confirm], ['attach', attach]]);
  doc.createElement = tag => makeNode(tag.toUpperCase());
  doc.getElementById = id => nodes.get(id) || null;
  doc.querySelectorAll = () => [];
  doc.activeElement = attach;
  global.document = doc;
  global.window = win;
  try {
    delete require.cache[require.resolve('../src/linux/desktop/overlay.js')];
    const overlay = require('../src/linux/desktop/overlay.js');

    overlay.openMenu([{ label: '测试', run() {} }], { x: 20, y: 20 }, attach);
    assert.equal(menu.hidden, false);
    doc.dispatch('pointerdown', { target: attachImg });
    assert.equal(menu.hidden, true);
    await new Promise(resolve => setTimeout(resolve, 70));
    doc.dispatch('pointerup', { target: attachImg });
    assert.equal(overlay.consumeAttachToggle(), true);

    // 指针按住后拖离按钮松手，不应吞掉后续下一次点击。
    overlay.openMenu([{ label: '测试', run() {} }], { x: 20, y: 20 }, attach);
    doc.dispatch('pointerdown', { target: attach });
    await new Promise(resolve => setTimeout(resolve, 70));
    doc.dispatch('pointerup', { target: menu });
    await new Promise(resolve => setTimeout(resolve, 10));
    assert.equal(overlay.consumeAttachToggle(), false);
  } finally {
    if (originalDocument === undefined) delete global.document;
    else global.document = originalDocument;
    if (originalWindow === undefined) delete global.window;
    else global.window = originalWindow;
  }
}

Promise.resolve()
  .then(() => { testScrollbarBehavior(); })
  .then(() => testOverlayAttachToggle())
  .then(() => testMainPathPolicy())
  .then(() => {
    console.log('linux desktop ui regression: ok');
  }, error => {
    console.error(error);
    process.exitCode = 1;
  });
