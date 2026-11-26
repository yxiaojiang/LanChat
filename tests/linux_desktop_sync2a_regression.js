const assert = require('node:assert/strict');
const { readFileSync, statSync } = require('node:fs');
const { join } = require('node:path');
const { makeRendererDom, withGlobals } = require('./linux_desktop_test_helpers.js');
const presentation = require('../src/linux/desktop/presentation.js');
const appearance = require('../src/linux/desktop/appearance.js');
const scroll = require('../src/linux/desktop/scrollbar.js');

const root = join(__dirname, '..');
const read = file => readFileSync(join(root, file), 'utf8');
const lines = file => read(file).replace(/(?:\r?\n)+$/, '').split(/\r?\n/).length;

async function testRealRendererContracts() {
  const { doc, win, nodes } = makeRendererDom();
  return withGlobals({ document: doc, window: win, testMode: true }, async () => {
    let onEvent;
    let confirmRequest;
    const selected = [];
    const peer = { key: 'peer-1', id: 'peer-1', nick: '好友', ip: '10.0.0.2', port: 38889, online: true };
    const api = {
      state: async () => ({ id: 'self', nick: '本机', ip: '10.0.0.1', tcpPort: 38889, sequence: 0, peers: [peer], sessions: [] }),
      settings: async () => null, emojiPack: async () => [], history: async () => ({ sequence: 0, messages: [] }),
      onEvent: callback => { onEvent = callback; }, syncUnreadState() {},
      selectFiles: async chat => { selected.push(chat); return []; }, sendText: async () => ({ confirmed: 1, total: 1 }),
      sendPastedFiles: async () => ({ ok: true }), confirmDrop: async () => [], deleteSession: async () => []
    };
    const overlay = {
      openMenu() {}, closeMenu() {}, openEmoji() {}, consumeAttachToggle: () => false,
      confirmAction: async request => { confirmRequest = request; return false; }
    };
    const renderer = require('../src/linux/desktop/renderer.js');
    const controller = renderer.createMainController({ document: doc, window: win, api, presentation, appearance, overlay, scroll });
    await controller.initPromise;
    for (const id of ['open-downloads', 'send-file', 'take-screenshot', 'clear-chat']) assert.equal(nodes.get(id).disabled, false);
    nodes.get('send-file').dispatch('click');
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(selected, [['peer-1']]);
    controller.switchChat('peer-1', true, true);
    nodes.get('clear-chat').dispatch('click');
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(confirmRequest.title, '清空确认');
    assert.match(confirmRequest.message, /好友“好友”的当前会话/);
    assert.equal(confirmRequest.message.includes('删除好友'), false);
    controller.switchChat('BROADCAST', true, true);
    const many = Array.from({ length: 17 }, (_item, index) => ({
      key: `peer-${index}`, id: `peer-${index}`, nick: `好友${index}`, ip: `10.0.0.${index + 2}`, port: 38889
    }));
    api.peers = async () => many;
    onEvent({ event: 'peers' });
    await new Promise(resolve => setImmediate(resolve));
    nodes.get('send-file').dispatch('click');
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(nodes.get('recipient').open, true);
    const choices = nodes.get('recipients').children;
    for (const choice of choices) choice.dispatch('click');
    assert.equal(nodes.get('status').textContent, '最多选择 16 个在线节点');
    nodes.get('recipient').close('');
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(selected, [['peer-1']]);
  });
}

module.exports = async function testLinuxDesktopSync2a() {
  const profile = read('src/linux/LanChat.Linux/LinuxProfile.cs');
  const service = read('src/linux/LanChat.Linux/DesktopService.cs');
  const cli = read('src/linux/LanChat.Linux/Program.cs');
  const main = read('src/linux/desktop/main.cjs');
  const preload = read('src/linux/desktop/preload.cjs');
  const renderer = read('src/linux/desktop/renderer.js');
  const overlay = read('src/linux/desktop/overlay.js');
  const pages = read('src/linux/desktop/settings-pages.js');
  const settings = read('src/linux/desktop/settings.js');
  const html = read('src/linux/desktop/index.html');
  const css = read('src/linux/desktop/desktop.css');
  const assets = read('packaging/ExportLinuxDesktopAssets.cs');
  const build = read('packaging/build-electron.py');

  assert.match(profile, /accentPreset\.Value >= 11/);
  assert.match(main, /accentPreset >= 11/);
  assert.match(service, /case "probe-ip"/);
  assert.match(service, /_runtimeAddresses/);
  assert.match(service, /\.ToArray\(\)/);
  assert.match(preload, /probeIp/);
  assert.match(pages, /手动连接 IPv4 节点/);
  assert.match(settings, /api\.probeIp/);
  assert.match(cli, /ProfileStorageDirectory = profile\.HistoryDirectory/);
  assert.match(cli, /SessionManager\.BROADCAST_KEY/);
  assert.match(cli, /SessionManager\.GetLanKey\(message\.IsSelf \? message\.TargetId : message\.SenderId, message\.RemoteAddress\)/);
  assert.match(main, /Promise\.all\(jobs\)/);
  assert.match(main, /fileRecipients/);
  assert.match(main, /file\.remaining/);
  assert.match(renderer, /pickRecipient\(true\)/);
  assert.match(renderer, /confirmForget\(active, false\)/);
  assert.match(renderer, /最多选择 16 个在线节点/);
  assert.match(renderer, /onlinePeers\.length === 1/);
  assert.match(renderer, /!recipient \|\| \(Array\.isArray\(recipient\) && !recipient\.length\)/);
  assert.match(renderer, /id\)\.disabled = !available/);
  assert.match(main, /dispatchFiles\(chatIds, files, false\)/);
  assert.match(main, /const stoppedNormally = await child\.stop\(\)/);
  assert.match(renderer, /Ctrl\+L 必须复用现有二次确认/);
  assert.match(overlay, /最近使用/);
  assert.match(overlay, /recentOrder\.splice\(12\)/);
  assert.match(css, /width: min\(496px/);
  assert.match(css, /height: min\(548px/);
  assert.match(css, /height: 188px/);
  assert.match(css, /margin: 0 28px 26px/);
  assert.match(css, /grid-template-columns: repeat\(8, 48px\)/);
  assert.match(css, /border: 1\.5px solid transparent/);
  assert.match(html, /<textarea id="message"/);
  for (const id of ['open-downloads', 'send-file', 'take-screenshot', 'clear-chat'])
    assert.match(html, new RegExp(`id="${id}"`));
  assert.match(main, /execFile/);
  assert.match(main, /trigger-screenshot/);
  assert.match(assets, /WeChatIconType\.Scissors/);
  assert.match(build, /"Scissors"/);
  assert.ok(statSync(join(root, 'assets/LinuxDesktop/Scissors.png')).size > 0);
  assert.ok(lines('tests/linux_desktop_sync2a_regression.js') <= 320);
  assert.ok(lines('tests/linux_desktop_ui_regression.js') <= 700);
  assert.ok(statSync(join(root, 'tests/linux_desktop_sync2a_regression.js')).size > 0);
  await testRealRendererContracts();
  return true;
};

if (require.main === module) {
  module.exports().then(() => console.log('linux desktop sync 2A regression: ok'));
}
