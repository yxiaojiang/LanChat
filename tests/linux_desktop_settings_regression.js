const assert = require('node:assert/strict');
const Module = require('node:module');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const presentation = require('../src/linux/desktop/presentation.js');
const settingsModel = require('../src/linux/desktop/settings-model.js');
const { makeSettingsDom, withGlobals } = require('./linux_desktop_test_helpers.js');

const root = join(__dirname, '..');
const read = path => readFileSync(join(root, path), 'utf8');

function loadMainForAuthorization() {
  const mainPath = require.resolve('../src/linux/desktop/main.cjs');
  const originalLoad = Module._load;
  const originalTestMode = process.env.LANCHAT_TEST_MODE;
  const electronStub = {
    app: {}, BrowserWindow: {}, clipboard: {}, dialog: {}, ipcMain: {}, Menu: {}, nativeImage: {},
    Notification: {}, screen: {}, session: {}, shell: {}, Tray: {}
  };
  Module._load = function (request, parent, isMain) {
    if (request === 'electron') return electronStub;
    if (request.endsWith('/service.cjs') || request === './service.cjs') return function StubService() {};
    if (request.endsWith('/tray-manager.cjs') || request === './tray-manager.cjs') return { createTrayManager() {} };
    return originalLoad.call(this, request, parent, isMain);
  };
  try {
    process.env.LANCHAT_TEST_MODE = '1';
    delete require.cache[mainPath];
    return require(mainPath);
  } finally {
    if (originalTestMode === undefined) delete process.env.LANCHAT_TEST_MODE;
    else process.env.LANCHAT_TEST_MODE = originalTestMode;
    Module._load = originalLoad;
    delete require.cache[mainPath];
  }
}

async function testSettingsControllerStub() {
  const { doc, win, nodes } = makeSettingsDom();
  return withGlobals({ document: doc, window: win, testMode: true }, async () => {
    const appearance = require('../src/linux/desktop/appearance.js');
    const scrollbar = require('../src/linux/desktop/scrollbar.js');
    const pages = require('../src/linux/desktop/settings-pages.js');
    const settingsApp = require('../src/linux/desktop/settings.js');
    const saveCalls = [];
    let refreshCalls = 0;
    const fakeApi = {
      settings: async () => ({
        nickname: '初始用户', autoAcceptFiles: true, downloadDirectory: '/home/u/Downloads',
        historyDirectory: '/home/u/.local/share/lanchat/ChatHistory', settingsPath: '/etc/lanchat/settings.json',
        themeMode: 0, accentPreset: 0, uiFontFamily: 'Microsoft YaHei UI', uiFontSize: 10.0,
        chatFontFamily: 'Microsoft YaHei UI', chatFontSize: 10.5, keepAliveInTray: true,
        trayPreview: true, tunFilter: true, unicastProbe: false, autoLoadHistory: true, sendShortcutMode: 0
      }),
      state: async () => ({ id: 'node-1', ip: '10.0.0.2', tcpPort: 38889, interfaces: [] }),
      systemInfo: async () => ({ hostname: 'box', osText: 'Linux', arch: 'x64', version: '2.0.17~experiment.5', electronVersion: '45.4.1' }),
      refresh: async () => { refreshCalls += 1; },
      saveSettings: async (...args) => {
        saveCalls.push(args);
        return {
          nickname: args[0], autoAcceptFiles: args[1], downloadDirectory: args[2],
          historyDirectory: args[4] || '/home/u/.local/share/lanchat/ChatHistory', settingsPath: '/etc/lanchat/settings.json',
          ...args[5], ...args[6], ...args[7]
        };
      }
    };
    const ctrl = settingsApp.createSettingsController({
      document: doc, window: win, api: fakeApi, model: settingsModel, pages, appearance, scroll: scrollbar
    });
    await ctrl.ready;

    const refreshButton = nodes.get('settings-panels').querySelectorAll('.action-link')
      .find(button => button.textContent === '刷新绑定 >');
    assert.ok(refreshButton);
    refreshButton.dispatch('click');
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(refreshCalls, 1);
    assert.deepEqual(ctrl.runtime.interfaces, []);

    const modePill = nodes.get('settings-panels').querySelectorAll('.pill-dropdown')
      .find(button => button.textContent.includes('Enter 发送'));
    assert.ok(modePill);
    const badges = () => nodes.get('settings-panels').querySelectorAll('.shortcut-badge').slice(0, 2).map(node => node.textContent);
    assert.deepEqual(badges(), ['Enter', 'Ctrl + Enter / Shift + Enter']);
    modePill.dispatch('click');
    const modeItem = nodes.get('settings-menu').children.find(button => button.textContent.includes('Ctrl + Enter 发送 / Enter 换行'));
    assert.ok(modeItem);
    modeItem.dispatch('click');
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(ctrl.committed.sendShortcutMode, 1);
    assert.deepEqual(badges(), ['Ctrl + Enter', 'Enter']);
    const networkPayload = saveCalls.at(-1)[7];
    assert.deepEqual(networkPayload, { tunFilter: true, unicastProbe: false, autoLoadHistory: true, sendShortcutMode: 1 });
  });
}

async function testSettingsWindowRefreshAuthorization() {
  const main = loadMainForAuthorization();
  const settingsWebContents = {};
  const settingsFrame = { url: 'file:///settings.html' };
  settingsWebContents.mainFrame = settingsFrame;
  const settingsWindow = { webContents: settingsWebContents };
  const event = { sender: settingsWebContents, senderFrame: settingsFrame };
  assert.equal(main.isAllowedRequest(event, { webContents: {} }, settingsWindow, 'file:///index.html', 'file:///settings.html', true), true);
  assert.equal(main.isAllowedRequest(event, { webContents: {} }, settingsWindow, 'file:///index.html', 'file:///settings.html', false), false);
}

async function testLinuxDesktopSettings() {
  const profile = read('src/linux/LanChat.Linux/LinuxProfile.cs');
  const service = read('src/linux/LanChat.Linux/DesktopService.cs');
  const main = read('src/linux/desktop/main.cjs');
  const settings = read('src/linux/desktop/settings.js');
  const pages = read('src/linux/desktop/settings-pages.js');
  const renderer = read('src/linux/desktop/renderer.js');
  const windowsSettings = read('src/Services/AppSettings.cs');
  const windowsChat = read('src/MainForm.Chat.cs');

  for (const key of ['TunFilter', 'UnicastProbe', 'AutoLoadHistory', 'SendShortcutMode']) {
    assert.match(profile, new RegExp(`next\\["${key}"\\]`));
  }
  assert.match(profile, /service\.EnableTunFilter = TunFilter/);
  assert.match(profile, /service\.EnableSubnetSweep = UnicastProbe/);
  assert.match(service, /\["interfaces"\] = InterfaceList\(\)/);
  assert.match(service, /_profile\.AutoLoadHistory/);
  assert.match(main, /handle\('refresh', \(\) => service\.request\('refresh'\), true\)/);
  assert.match(settings, /networkPayload/);
  assert.match(settings, /shortcutBadges/);
  assert.match(pages, /committed\.sendShortcutMode === 1/);
  assert.match(pages, /内置预览尚未接入/);
  assert.match(pages, /固定 64KB 分块并校验/);
  assert.match(renderer, /presentation\.setSendShortcutMode/);
  for (const key of ['TunFilter', 'UnicastProbe', 'AutoLoadHistory', 'SendShortcutMode']) {
    assert.match(windowsSettings, new RegExp(`"${key}"`));
  }
  assert.match(windowsChat, /AppSettings\.SendShortcutMode/);

  presentation.setSendShortcutMode(0);
  assert.equal(presentation.shouldSendOnEnter({ key: 'Enter' }), true);
  assert.equal(presentation.shouldSendOnEnter({ key: 'Enter', ctrlKey: true }), false);
  assert.equal(presentation.shouldNewlineOnCtrlEnter({ key: 'Enter', ctrlKey: true }), true);
  presentation.setSendShortcutMode(1);
  assert.equal(presentation.shouldSendOnEnter({ key: 'Enter' }), false);
  assert.equal(presentation.shouldSendOnEnter({ key: 'Enter', ctrlKey: true }), true);
  assert.equal(presentation.shouldNewlineOnCtrlEnter({ key: 'Enter', ctrlKey: true }), false);
  presentation.setSendShortcutMode(0);

  await testSettingsWindowRefreshAuthorization();
  await testSettingsControllerStub();
  return true;
}

if (require.main === module) {
  testLinuxDesktopSettings().then(() => console.log('linux desktop settings regression: ok'));
}

module.exports = testLinuxDesktopSettings;
