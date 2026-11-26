const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const testDesktopIpc = require('./linux_desktop_ipc_regression.js');
const testDesktopLifecycle = require('./linux_desktop_lifecycle_regression.js');
const testDesktopSync2A = require('./linux_desktop_sync2a_regression.js');
const testDesktopSettings = require('./linux_desktop_settings_regression.js');
const testDesktopUpdate = require('./linux_desktop_update_regression.js');
const testDesktopComposer = require('./linux_desktop_composer_regression.js');
const { makeNode, makeOverlayDom, makeSettingsDom, makeRendererDom, withGlobals } = require('./linux_desktop_test_helpers.js');
const presentation = require('../src/linux/desktop/presentation.js'); const settingsModel = require('../src/linux/desktop/settings-model.js');
const root = join(__dirname, '..'); const read = path => readFileSync(join(root, path), 'utf8');
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
for (const element of ['👨‍👩‍👧‍👦', '👍🏽', '🇨🇳', '[敲打]']) {
  const prefix = '甲'.repeat(14);
  const session = '测试私聊';
  const summarize = text => presentation.snippet(session, null, new Map([[session, [{ text }]]]));
  assert.equal(summarize(`${prefix}${element}乙`), `${prefix}${element}乙`);
  assert.equal(summarize(`${prefix}${element}乙丙`), `${prefix}${element}...`);
  assert.equal(summarize(`${prefix}乙${element}丙`), `${prefix}乙...`);
}
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
const conversationView = read('src/linux/desktop/conversation-view.js');
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
const mainScripts = [...html.matchAll(/<script src="([^"]+)" defer>/g)].map(match => match[1]);
for (const path of ['session-sidebar.js', 'conversation-view.js']) {
  assert.equal(mainScripts.includes(path) && mainScripts.indexOf(path) < mainScripts.indexOf('renderer.js'), true);
}
const sidebarBottom = html.match(/<div class="sidebar-bottom">([\s\S]*?)<\/div>/)?.[1];
assert.equal((sidebarBottom?.match(/<button\b[^>]*\bid="([^"]+)"/g) || []).at(-1)?.includes('id="refresh"'), true);
assert.match(css, /\.session-heading\s*\{[^}]*\bfont-weight:\s*700\s*;/);
assert.equal(css.includes('scrollbar-width: thin'), false);
assert.equal(renderer.includes('lastIndexOf(message)'), false);
assert.equal(conversationView.includes('createDocumentFragment'), true);
assert.equal(conversationView.includes('BoxWhite.png'), true);
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
  'src/linux/desktop/appearance.js', 'src/linux/desktop/presentation.js', 'src/linux/desktop/scrollbar.js', 'src/linux/desktop/overlay.js',
  'src/linux/desktop/renderer.js', 'src/linux/desktop/composer-box.js', 'src/linux/desktop/settings-model.js', 'src/linux/desktop/settings-pages.js',
  'src/linux/desktop/session-sidebar.js', 'src/linux/desktop/conversation-view.js', 'tests/linux_desktop_ui_regression.js',
  'src/linux/desktop/settings.js', 'src/linux/desktop/desktop.css', 'src/linux/desktop/settings.css',
  'tests/linux_desktop_settings_regression.js',
  'src/linux/desktop/main.cjs', 'src/linux/desktop/preload.cjs', 'src/linux/desktop/service.cjs',
  'src/linux/LanChat.Linux/DesktopService.cs', 'tests/linux_desktop_test_helpers.js',
  'tests/linux_desktop_lifecycle_regression.js', 'tests/linux_desktop_composer_regression.js', 'src/linux/desktop/tray-manager.cjs',
  'src/linux/desktop/update-service.cjs', 'tests/linux_desktop_update_regression.js'
]) {
  const lines = read(path).split(/\r?\n/).length;
  assert.equal(lines < 1500, true, `${path} 超过 1500 行`);
  assert.equal(lines < 1000, true, `${path} 已到 1000 行，新增行为前需要先拆分`);
}
function testSettingsModelAndMarkup() {
  assert.deepEqual(
    settingsModel.NAV_ITEMS.map(item => item.title),
    ['通用', '个人主页', '外观', '快捷键', '文件传输', '聊天记录', '设备连接', '关于与更新']
  );
  assert.equal(settingsModel.filterNavItems('').length, 8);
  assert.deepEqual(settingsModel.filterNavItems('记录').map(item => item.title), ['聊天记录']);
  assert.equal(settingsModel.filterNavItems('不存在').length, 0);
  const short = '/home/user/ReceivedFiles';
  assert.equal(settingsModel.formatShortPath(short), short);
  const longPath = '/home/user/very-long-directory-name/sub/LanChat/ReceivedFiles';
  const clipped = settingsModel.formatShortPath(longPath);
  assert.equal(clipped.length, 44);
  assert.equal(clipped.startsWith('...'), true);
  assert.equal(clipped.endsWith('ReceivedFiles'), true);
  assert.deepEqual(settingsModel.validateNickname('  阿兰  '), { ok: true, value: '阿兰' });
  assert.equal(settingsModel.validateNickname('').ok, false);
  assert.equal(settingsModel.validateNickname('a|b').ok, false);
  assert.equal(settingsModel.validateNickname('a\nb').ok, false);
  assert.equal(settingsModel.validateNickname('x'.repeat(81)).ok, false);
  assert.equal(settingsModel.sliderIndexForValue(settingsModel.UI_FONT_SIZES, 10.0), 1);
  assert.equal(settingsModel.sliderIndexForValue(settingsModel.UI_FONT_SIZES, 14.0), 4);
  assert.equal(settingsModel.SHORTCUTS.length, 4);
  assert.deepEqual(settingsModel.SHORTCUTS[0], { title: '发送消息', keys: 'Enter' });
  assert.deepEqual(settingsModel.SHORTCUTS[1], { title: '多行换行输入', keys: 'Ctrl + Enter / Shift + Enter' });
  assert.equal(settingsModel.ACCENTS.length, 11);
  assert.equal(settingsModel.THEMES.length, 3);
  assert.equal(settingsModel.buildDiagnostics({ version: '2.0.17~experiment.5' }).includes('LanChat v2.0.17~experiment.5'), true);
  const appearance = require('../src/linux/desktop/appearance.js');
  assert.equal(appearance.surfaceHex(18, 18, 18, 0), '#121212');
  assert.equal(appearance.surfaceHex(18, 18, 18, 1), '#1d1d1e');
  assert.equal(appearance.surfaceHex(18, 18, 18, 2), '#121721');
  const vars = appearance.computeCssVars({
    themeMode: 2,
    accentPreset: 1,
    uiFontFamily: 'SimHei',
    uiFontSize: 14.0,
    chatFontFamily: 'Consolas',
    chatFontSize: 13.0
  });
  assert.equal(vars['--sidebar'], '#0e131d');
  assert.equal(vars['--viewport'], '#121721');
  assert.equal(vars['--accent'], '#a16b39');
  assert.equal(vars['--selection'], '#714826');
  assert.equal(vars['--ui-font-delta'], '4.0pt');
  assert.equal(vars['--chat-font-size'], '13.0pt');
  assert.equal(vars['--ui-font-family'].includes('SimHei'), true);
  assert.equal(vars['--chat-font-family'].includes('Consolas'), true);
  const fallbackVars = appearance.computeCssVars({ themeMode: 99, accentPreset: -1, uiFontSize: 99, chatFontSize: 2 });
  assert.equal(fallbackVars['--viewport'], '#121212');
  assert.equal(fallbackVars['--accent'], '#ffffff');
  assert.equal(fallbackVars['--ui-font-delta'], '0.0pt');
  assert.equal(fallbackVars['--chat-font-size'], '10.5pt');
  const settingsHtml = read('src/linux/desktop/settings.html');
  const settingsCss = read('src/linux/desktop/settings.css');
  const settingsPages = read('src/linux/desktop/settings-pages.js');
  const settingsJs = read('src/linux/desktop/settings.js');
  for (const id of ['id="settings-search"', 'id="settings-nav"', 'id="settings-viewport"', 'id="settings-panels"',
      'id="settings-menu"', 'id="settings-nick-dialog"', 'id="settings-feedback-dialog"', 'id="settings-confirm-dialog"']) {
    assert.equal(settingsHtml.includes(id), true, id);
  }
  assert.equal(settingsHtml.includes('feedback-scroll'), true);
  assert.equal(settingsCss.includes('.settings-textarea-box textarea::-webkit-scrollbar'), true);
  assert.equal(settingsCss.includes('scrollbar-width: none'), true);
  assert.equal(html.includes('id="settings"'), false);
  assert.equal(renderer.includes('openSettings'), true);
  assert.equal(renderer.includes('histories-cleared'), true);
  assert.equal(renderer.includes('settings-updated'), true);
  assert.equal(renderer.includes('settingsDialog'), false);
  for (const token of ['270px', '900px', '54px', '#26262b', '#1fd67e', '280px', '140px', '82px', '16px']) {
    assert.equal(settingsCss.includes(token), true, token);
  }
  for (const token of ['overridePath', 'backupPath', '使用备用位置', 'selectSettingsDirectory', 'clearAllHistories', 'openDownloads', 'openHistory']) {
    assert.equal(settingsJs.includes(token) || settingsPages.includes(token), true, token);
  }
  assert.equal(main.includes('confirmOverride'), true);
  for (const token of ['open-settings', 'close-settings', 'system-info', 'open-history', 'clear-all-histories']) {
    assert.equal(main.includes(token), true, token);
  }
  for (const token of ['openSettings', 'closeSettings', 'systemInfo', 'openHistory', 'clearAllHistories']) {
    assert.equal(preload.includes(token), true, token);
  }
  assert.equal(service.includes('clear-all-histories'), true);
  assert.equal(service.includes('ClearAllHistories'), true);
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
  const { doc, win, menu, emoji, emojiGrid, attach } = makeOverlayDom();
  const attachImg = attach.children[0];
  return withGlobals({ document: doc, window: win }, async () => {
    delete require.cache[require.resolve('../src/linux/desktop/overlay.js')];
    const overlay = require('../src/linux/desktop/overlay.js');

    overlay.openMenu([{ label: '测试', run() {} }], { x: 20, y: 20 }, attach);
    assert.equal(menu.hidden, false);
    doc.dispatch('pointerdown', { target: attachImg });
    assert.equal(menu.hidden, true);
    await new Promise(resolve => setTimeout(resolve, 70));
    doc.dispatch('pointerup', { target: attachImg });
    assert.equal(overlay.consumeAttachToggle(), true);
    overlay.openMenu([{ label: '测试', run() {} }], { x: 20, y: 20 }, attach);
    doc.dispatch('pointerdown', { target: attach });
    await new Promise(resolve => setTimeout(resolve, 70));
    doc.dispatch('pointerup', { target: menu });
    await new Promise(resolve => setTimeout(resolve, 10));
    assert.equal(overlay.consumeAttachToggle(), false);
    presentation.setEmojiPack([['[敲打]', 'data:image/png;base64,AAAA']]);
    let picked;
    overlay.openEmoji(attach, token => { picked = token; }, attach);
    const sections = () => emojiGrid.children.filter(node => node.className === 'emoji-section');
    assert.equal(sections().length, 1);
    assert.equal(sections()[0].children[0].children[0].src, 'data:image/png;base64,AAAA');
    sections()[0].children[0].dispatch('click');
    assert.equal(picked, '[敲打]');
    presentation.setEmojiPack(Array.from({ length: 13 }, (_item, index) => [`[表情${index}]`, 'data:image/png;base64,AAAA']));
    for (let index = 0; index < 13; index += 1) {
      overlay.openEmoji(attach, () => {}, attach);
      sections().at(-1).children[index].dispatch('click');
    }
    overlay.openEmoji(attach, () => {}, attach);
    assert.equal(sections()[0].children.length, 12);
    assert.equal(sections()[1].children.length, 13);
    overlay.closeEmoji(false);
    presentation.setEmojiPack([['[敲打]', 'data:image/png;base64,AAAA']]);
    const bubble = makeNode('DIV');
    presentation.drawMessageText(bubble, '[敲打]', doc);
    assert.equal(bubble.children[0].textContent, '[敲打]');
    assert.equal(bubble.children[0].children[0].src, 'data:image/png;base64,AAAA');
    presentation.setEmojiPack([]);
    const summary = makeNode('SMALL');
    presentation.drawMessageText(summary, '收到[敲打]...', doc, true);
    assert.equal(summary.textContent, '收到[敲打]...');
    assert.equal(summary.children[0].className, 'summary-token');
    assert.equal(summary.children[0].children.length, 0);
    presentation.setEmojiPack([]);
  });
}
async function testSettingsController() {
  const { doc, win, nodes, feedbackHost } = makeSettingsDom();
  return withGlobals({ document: doc, window: win, testMode: true }, async () => {
    const appearance = require('../src/linux/desktop/appearance.js');
    const scrollbar = require('../src/linux/desktop/scrollbar.js');
    const pages = require('../src/linux/desktop/settings-pages.js');
    const settingsApp = require('../src/linux/desktop/settings.js');
    const saveCalls = [];
    let nextSaveImpl = async () => ({});
    const fakeApi = {
      settings: async () => ({
        nickname: '初始用户',
        autoAcceptFiles: true,
        downloadDirectory: '/home/u/Downloads',
        historyDirectory: '/home/u/.local/share/lanchat/ChatHistory',
        settingsPath: '/etc/lanchat/settings.json',
        themeMode: 0,
        accentPreset: 0,
        uiFontFamily: 'Microsoft YaHei UI',
        uiFontSize: 10.0,
        chatFontFamily: 'Microsoft YaHei UI',
        chatFontSize: 10.5
      }),
      state: async () => ({ id: 'node-1', ip: '10.0.0.2', tcpPort: 38889 }),
      systemInfo: async () => ({ hostname: 'box', osText: 'Linux', arch: 'x64', version: '2.0.17~experiment.5', electronVersion: '45.4.1' }),
      saveSettings: async (...args) => {
        saveCalls.push(args);
        return nextSaveImpl(...args);
      }
    };
    const ctrl = settingsApp.createSettingsController({
      document: doc, window: win, api: fakeApi, model: settingsModel, pages, appearance, scroll: scrollbar
    });
    await ctrl.ready;
    // 1. 反馈框绑定自定义浮动滑块，溢出时显示滑块。
    const feedbackThumb = feedbackHost.children.at(-1);
    assert.equal(feedbackThumb.className, 'float-thumb');
    assert.equal(feedbackThumb.hidden, true);
    const feedbackInput = nodes.get('feedback-input');
    feedbackInput.clientHeight = 150;
    feedbackInput.scrollHeight = 320;
    feedbackInput.dispatch('input');
    assert.equal(feedbackThumb.hidden, false);
    feedbackInput.scrollHeight = 150;
    ctrl.openFeedbackDialog();
    assert.equal(feedbackThumb.hidden, true);
    nodes.get('feedback-cancel').dispatch('click');
    // 2. 只读设置备用路径确认：取消分支。
    nextSaveImpl = async () => ({ backupPath: '/tmp/bak/settings.json', overridePath: '/tmp/ovr/settings.json' });
    const cancelTask = ctrl.persistSettings({ nickname: '被取消昵称', themeMode: 2 });
    await new Promise(resolve => setTimeout(resolve, 0));
    const confirmDlg = nodes.get('settings-confirm-dialog');
    assert.equal(confirmDlg.open, true);
    assert.equal(nodes.get('settings-confirm-body').textContent.includes('/tmp/ovr/settings.json'), true);
    assert.equal(nodes.get('settings-confirm-accept').textContent, '使用备用位置');
    nodes.get('settings-confirm-cancel').dispatch('click');
    const cancelResult = await cancelTask;
    assert.deepEqual(cancelResult, { ok: false, cancelled: true });
    assert.equal(saveCalls.length, 1);
    assert.equal(ctrl.committed.nickname, '初始用户');
    assert.equal(ctrl.committed.themeMode, 0);
    assert.equal(doc.documentElement.style.getPropertyValue('--viewport'), '#121212');
    assert.equal(nodes.get('settings-banner').textContent.includes('已取消更改'), true);
    // 3. 只读设置备用路径确认：确认分支。
    saveCalls.length = 0;
    nextSaveImpl = async (_nick, _auto, _dl, confirmOverride, _hist, app) => {
      if (!confirmOverride) return { backupPath: '/tmp/bak/settings.json', overridePath: '/tmp/ovr/settings.json' };
      return {
        nickname: '已确认昵称',
        autoAcceptFiles: false,
        downloadDirectory: '/home/u/Downloads',
        historyDirectory: '/home/u/.local/share/lanchat/ChatHistory',
        settingsPath: '/tmp/ovr/settings.json',
        ...app
      };
    };
    const acceptTask = ctrl.persistSettings({ nickname: '已确认昵称', autoAcceptFiles: false });
    await new Promise(resolve => setTimeout(resolve, 0));
    assert.equal(confirmDlg.open, true);
    nodes.get('settings-confirm-accept').dispatch('click');
    const acceptResult = await acceptTask;
    assert.equal(acceptResult.ok, true);
    assert.equal(saveCalls.length, 2);
    assert.equal(saveCalls[0][3], false);
    assert.equal(saveCalls[1][3], true);
    assert.equal(ctrl.committed.nickname, '已确认昵称');
    assert.equal(ctrl.committed.settingsPath, '/tmp/ovr/settings.json');
    // 4. 选择相同历史目录不提示已迁移。
    saveCalls.length = 0;
    nextSaveImpl = async () => ({
      nickname: '已确认昵称',
      autoAcceptFiles: false,
      downloadDirectory: '/home/u/Downloads',
      historyDirectory: '/home/u/.local/share/lanchat/ChatHistory',
      settingsPath: '/tmp/ovr/settings.json'
    });
    await ctrl.persistSettings({ historyDirectory: '/home/u/.local/share/lanchat/ChatHistory/' });
    assert.equal(nodes.get('settings-banner').textContent.includes('已迁移'), false);
    assert.equal(nodes.get('settings-banner').textContent, '所选目录与当前目录相同，无需迁移');

    // 选择不同历史目录时提示已迁移。
    nextSaveImpl = async (_n, _a, _d, _c, hist) => ({
      nickname: '已确认昵称',
      autoAcceptFiles: false,
      downloadDirectory: '/home/u/Downloads',
      historyDirectory: hist,
      settingsPath: '/tmp/ovr/settings.json'
    });
    await ctrl.persistSettings({ historyDirectory: '/home/u/NewChatHistory' });
    assert.equal(nodes.get('settings-banner').textContent, '聊天记录已迁移并保存设置');
    // 5. 外观四项即时应用、通用页与外观页字号联动，以及只读参数落盘。
    saveCalls.length = 0;
    nextSaveImpl = async (nick, auto, _d, _c, _h, app, tray) => ({
      nickname: nick,
      autoAcceptFiles: auto,
      downloadDirectory: '/home/u/Downloads',
      historyDirectory: '/home/u/NewChatHistory',
      settingsPath: '/tmp/ovr/settings.json',
      ...app,
      ...tray
    });
    await ctrl.updateAppearance({
      themeMode: 2,
      accentPreset: 1,
      uiFontFamily: 'SimHei',
      uiFontSize: 14.0,
      chatFontFamily: 'Consolas',
      chatFontSize: 13.0
    });
    assert.equal(doc.documentElement.style.getPropertyValue('--viewport'), '#121721');
    assert.equal(doc.documentElement.style.getPropertyValue('--accent'), '#a16b39');
    assert.equal(doc.documentElement.style.getPropertyValue('--ui-font-delta'), '4.0pt');
    assert.equal(doc.documentElement.style.getPropertyValue('--chat-font-size'), '13.0pt');
    assert.equal(doc.documentElement.style.getPropertyValue('--chat-font-family').includes('Consolas'), true);
    assert.equal(saveCalls[0][5].themeMode, 2);
    assert.equal(saveCalls[0][5].accentPreset, 1);
    assert.equal(saveCalls[0][5].uiFontFamily, 'SimHei');
    assert.equal(saveCalls[0][5].uiFontSize, 14.0);
    assert.equal(saveCalls[0][5].chatFontFamily, 'Consolas');
    assert.equal(saveCalls[0][5].chatFontSize, 13.0);
    const sliders = nodes.get('settings-panels').querySelectorAll('.stepped-slider');
    assert.equal(sliders.length, 3);
    assert.equal(sliders[0].getAttribute('aria-valuenow'), '4');
    assert.equal(sliders[1].getAttribute('aria-valuenow'), '4');
    // 6. 两个托盘开关均通过保存 RPC 持久化。
    saveCalls.length = 0;
    const switches = nodes.get('settings-panels').querySelectorAll('.toggle-switch');
    const trayPreviewSwitch = switches.find(el => el.getAttribute('aria-label') === '托盘未读悬浮预览');
    assert.ok(trayPreviewSwitch);
    trayPreviewSwitch.dispatch('click');
    assert.equal(saveCalls.length, 1);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(ctrl.committed.trayPreview, false);
    saveCalls.length = 0;
    const keepAliveSwitch = switches.find(el => el.getAttribute('aria-label') === '后台网络驻留运行');
    assert.ok(keepAliveSwitch);
    keepAliveSwitch.dispatch('click');
    assert.equal(saveCalls.length, 1);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(ctrl.committed.keepAliveInTray, false);
  });
}
async function testMainRendererRaceAndAppearance() {
  const { doc, win, nodes } = makeRendererDom();
  return withGlobals({ document: doc, window: win, testMode: true }, async () => {
    const appearance = require('../src/linux/desktop/appearance.js');
    const scrollbar = require('../src/linux/desktop/scrollbar.js');
    const overlayStub = { openMenu() {}, closeMenu() {}, openEmoji() {}, consumeAttachToggle: () => false, confirmAction: async () => false };
    win.LanChatOverlay = overlayStub;
    let resolveLateHistory;
    let historyCalls = 0;
    let onEventHandler = null; const unreadStates = [];
    const imageUrl = 'data:image/png;base64,AAAA';
    const prefix = '甲'.repeat(14);
    const archived = {
      key: 'archive-address', ip: '10.0.0.3', nick: '归档昵称', summary: `${prefix}[敲打]乙丙`
    };
    const fakeApi = {
      state: async () => ({
        id: 'self-1', nick: '本机', ip: '10.0.0.1', tcpPort: 38889, sequence: 10, peers: [], sessions: [archived]
      }),
      settings: async () => ({
        nickname: '本机', autoAcceptFiles: true, themeMode: 1, accentPreset: 2,
        uiFontFamily: 'KaiTi', uiFontSize: 12.0, chatFontFamily: 'Arial', chatFontSize: 11.5
      }),
      emojiPack: async () => [['[敲打]', imageUrl]],
      history: async () => {
        historyCalls += 1;
        if (historyCalls === 1) return { sequence: 10, messages: [{ sequence: 1, chat: 'BROADCAST', text: '旧消息', timestamp: '2026-11-23T05:00:00' }] };
        return new Promise(resolve => { resolveLateHistory = resolve; });
      },
      onEvent: cb => { onEventHandler = cb; }, syncUnreadState: state => unreadStates.push(state),
      quit() {}
    };
    const mainApp = require('../src/linux/desktop/renderer.js');
    const ctrl = mainApp.createMainController({
      document: doc, window: win, api: fakeApi, presentation, appearance, overlay: overlayStub, scroll: scrollbar
    });
    await ctrl.initPromise;
    // 1. 启动即恢复外观设置（重启保持）。
    assert.equal(doc.documentElement.style.getPropertyValue('--viewport'), '#1d1d1e');
    assert.equal(doc.documentElement.style.getPropertyValue('--accent'), '#fc2c3e');
    assert.equal(doc.documentElement.style.getPropertyValue('--ui-font-delta'), '2.0pt');
    assert.equal(doc.documentElement.style.getPropertyValue('--chat-font-size'), '11.5pt');
    assert.equal(ctrl.messages.get('BROADCAST').length, 1);
    const nav = nodes.get('chats');
    const rows = () => nav.children.map(row => row.dataset.key || row.textContent);
    assert.deepEqual(rows(), ['BROADCAST', '在线', '离线', archived.key]);
    const summary = key => nav.children.find(row => row.dataset.key === key).querySelector('.chat-copy').children[1];
    const archivedSummary = summary(archived.key);
    const emoji = archivedSummary.querySelector('.message-emoji');
    assert.equal(archivedSummary.textContent, `${prefix}[敲打]...`);
    assert.equal(emoji.children[0].src, imageUrl);
    emoji.children[0].dispatch('error');
    assert.equal(archivedSummary.querySelector('.message-emoji'), null);
    assert.equal(archivedSummary.querySelector('.summary-token').textContent, '[敲打]');
    assert.equal(archivedSummary.textContent, `${prefix}[敲打]...`); doc.hidden = true; // 后台当前会话的新消息必须进入托盘未读链。
    onEventHandler({ event: 'message', data: { chat: 'BROADCAST', sequence: 11, self: false, nick: '后台节点', text: '当前会话后台新消息', timestamp: '2026-11-26T05:00:00' } }); assert.equal(unreadStates.at(-1).count, 1); doc.hidden = false; onEventHandler({ event: 'window-restored' }); assert.equal(unreadStates.at(-1).count, 0);
    // 2. settings-updated 即时刷新主窗外观与昵称。
    onEventHandler({
      event: 'settings-updated',
      data: {
        nickname: '新昵称', themeMode: 2, accentPreset: 4,
        uiFontFamily: 'SimSun', uiFontSize: 9.0, chatFontFamily: 'Consolas', chatFontSize: 15.0
      }
    });
    assert.equal(nodes.get('nickname').textContent, '新昵称');
    assert.equal(doc.documentElement.style.getPropertyValue('--viewport'), '#121721');
    assert.equal(doc.documentElement.style.getPropertyValue('--accent'), '#f69412');
    assert.equal(doc.documentElement.style.getPropertyValue('--ui-font-delta'), '-1.0pt');
    assert.equal(doc.documentElement.style.getPropertyValue('--chat-font-size'), '15.0pt');
    assert.equal(doc.documentElement.style.getPropertyValue('--chat-font-family').includes('Consolas'), true);
    // 3. P1-2：histories-cleared 必须作废进行中的 historyLoads，晚到的 history 响应不得把已清空的气泡写回去。
    const inFlight = ctrl.loadHistory('BROADCAST');
    assert.equal(ctrl.historyLoads.has('BROADCAST'), true);
    onEventHandler({ event: 'histories-cleared' });
    assert.equal(ctrl.historyLoads.size, 0);
    assert.equal(ctrl.messages.size, 0);
    assert.equal(nodes.get('messages').children.length, 0);
    resolveLateHistory({
      sequence: 10,
      messages: [{ sequence: 1, chat: 'BROADCAST', text: '不应回流的气泡', timestamp: '2026-11-23T05:00:00' }]
    });
    await inFlight;
    assert.equal(ctrl.messages.has('BROADCAST'), false);
    assert.equal(nodes.get('messages').children.length, 0);
    assert.equal(nodes.get('status').textContent, '已彻底清空所有本地聊天记录');
    // 晚到的失败响应同样不得覆盖已清空状态。
    let rejectLateHistory;
    fakeApi.history = () => new Promise((_resolve, reject) => { rejectLateHistory = reject; });
    const failingInFlight = ctrl.loadHistory('BROADCAST');
    onEventHandler({ event: 'histories-cleared' });
    rejectLateHistory(new Error('late failure'));
    await failingInFlight;
    assert.equal(nodes.get('status').textContent, '已彻底清空所有本地聊天记录');
    // 4. 同名文件的双向进度互不覆盖，完成后在原位转为可打开的文件消息。
    const chat = 'peer-address';
    const peer = { key: chat, id: 'peer-1', nick: '好友', ip: '10.0.0.2', port: 38889 };
    fakeApi.peers = async () => [peer];
    fakeApi.history = async () => ({ sequence: 10, messages: [] });
    onEventHandler({ event: 'peers' });
    await new Promise(resolve => setImmediate(resolve));
    ctrl.switchChat(chat);
    await new Promise(resolve => setImmediate(resolve));
    const progress = { event: 'progress', chat, name: '同名.txt', total: 100, peerNick: peer.nick };
    onEventHandler({ ...progress, sending: true, operation: 'first', queued: true });
    onEventHandler({ ...progress, sending: true, operation: 'second', queued: true });
    onEventHandler({ ...progress, sending: true, operation: 'first', received: 42 });
    onEventHandler({ ...progress, sending: false, transfer: 'incoming', received: 65 });
    const feed = nodes.get('messages');
    const progressViews = () => feed.querySelectorAll('.file-progress');
    assert.deepEqual(progressViews().map(view => view.children[0].value), [42, 0, 65]);
    assert.equal(progressViews()[1].children[1].textContent, '等待发送 · 0%');
    assert.equal(progressViews()[2].children[1].textContent, '接收中 · 65%');
    onEventHandler({ ...progress, sending: false, transfer: 'incoming', received: 70, speed: 1.25 });
    assert.equal(progressViews()[2].children[1].textContent, '接收中 · 70% · 1.25 MB/s');
    await ctrl.loadHistory(chat);
    assert.deepEqual(progressViews().map(view => view.children[0].value), [42, 0, 70]);
    onEventHandler({ ...progress, sending: true, operation: 'first', completed: true, received: 100 });
    onEventHandler({ event: 'message', data: { chat, file: true, sequence: 12, self: true, name: '同名.txt', operation: 'first', path: '/tmp/sent.txt' } });
    assert.equal(feed.querySelectorAll('.file-card').length, 3);
    assert.equal(feed.querySelectorAll('.file-actions').length, 1);
    onEventHandler({ ...progress, sending: true, operation: 'second', failed: true, error: '文件不可用' });
    onEventHandler({ event: 'message', data: { chat, file: true, sequence: 13, self: false, name: '同名 (1).txt', transfer: 'incoming', path: '/tmp/received.txt' } });
    assert.equal(feed.querySelectorAll('.file-card').length, 3);
    assert.equal(feed.querySelectorAll('.file-actions').length, 2);
    assert.equal(progressViews()[0].children[1].textContent, '发送失败 · 文件不可用');
    // 粘贴的截图显示缩略图，只在提交时发送文件引用。
    let sent;
    fakeApi.sendPastedFiles = async (recipient, ids) => { sent = { recipient, ids }; };
    fakeApi.sendText = async () => { assert.fail('只有附件时不应发送文字'); };
    const input = nodes.get('message');
    input.dispatch('files-pasted', { detail: JSON.stringify({ chat, files: [
      { id: 'clipboard-file', name: '截图.png', size: 3, preview: 'data:image/png;base64,AAAA' }
    ] }) });
    assert.equal(input.value, '');
    assert.equal(nodes.get('send').disabled, false);
    assert.equal(nodes.get('attachments-host').hidden, false);
    assert.equal(nodes.get('attachments').children[0].children[0].className, 'attachment-preview');
    assert.equal(sent, undefined);
    ctrl.switchChat('BROADCAST');
    assert.equal(nodes.get('attachments-host').hidden, true);
    ctrl.switchChat(chat);
    assert.equal(nodes.get('attachments-host').hidden, false);
    nodes.get('compose').dispatch('submit');
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(sent, { recipient: peer.id, ids: ['clipboard-file'] });
    assert.equal(nodes.get('attachments-host').hidden, true);
    // 分组标题不参与键盘导航，搜索只渲染命中的分组。
    assert.deepEqual(rows(), ['BROADCAST', '在线', chat, '离线', archived.key]);
    assert.deepEqual(nav.children.filter(row => row.className === 'session-heading').map(row => row.dataset.online), ['true', 'false']);
    const selected = () => nav.children.find(row => row.getAttribute('aria-current') === 'true')?.dataset.key;
    nav.dispatch('keydown', { key: 'ArrowDown' });
    assert.equal(selected(), archived.key);
    nav.dispatch('keydown', { key: 'ArrowUp' });
    assert.equal(selected(), chat);
    const search = nodes.get('search');
    search.value = archived.nick;
    search.dispatch('input');
    assert.deepEqual(rows(), ['离线', archived.key]);
    search.value = peer.nick;
    search.dispatch('input');
    assert.deepEqual(rows(), ['在线', chat]);
    search.value = '无匹配';
    search.dispatch('input');
    assert.deepEqual(rows(), []);
    nodes.get('search-clear').dispatch('click');
    // 点击刷新后，地址占位须保留运行期昵称和启动时加载的归档昵称。
    fakeApi.refresh = async () => [peer, archived].map(item => ({
      ...item, id: item.ip, nick: `好友 (${item.ip})`, port: 38889
    }));
    nodes.get('refresh').dispatch('click');
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(nodes.get('status').textContent, '已请求刷新局域网节点');
    for (const item of [peer, archived]) {
      const row = nav.children.find(button => button.dataset.key === item.key);
      assert.equal(row.getAttribute('aria-label'), item.nick);
    }
    // 实时群聊摘要包含发送者前缀，已导入与缺少资源的令牌可在同一行显示。
    const groupPrefix = `甲: ${'乙'.repeat(14)}`;
    onEventHandler({ event: 'message', data: {
      chat: presentation.GROUP, sequence: 14, nick: '甲', text: `${'乙'.repeat(14)}[敲打][未知表情]丙丁`
    } });
    const groupSummary = summary(presentation.GROUP);
    assert.equal(groupSummary.textContent, `${groupPrefix}[敲打][未知表情]...`);
    assert.deepEqual(groupSummary.querySelectorAll('.summary-token').map(token => token.children[0]?.src || token.textContent),
      [imageUrl, '[未知表情]']);
    presentation.setEmojiPack([]);
  });
  }
Promise.resolve()
  .then(() => { testSettingsModelAndMarkup(); })
  .then(() => { testScrollbarBehavior(); })
  .then(() => testOverlayAttachToggle())
  .then(() => testDesktopIpc())
  .then(() => testDesktopLifecycle())
  .then(() => testDesktopSync2A())
  .then(() => testDesktopSettings())
  .then(() => testDesktopUpdate())
  .then(() => testDesktopComposer())
  .then(() => testSettingsController())
  .then(() => testMainRendererRaceAndAppearance())
  .then(() => {
    console.log('linux desktop ui regression: ok');
  }, error => {
    console.error(error);
    process.exitCode = 1;
  });
