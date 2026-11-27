const assert = require('node:assert/strict');
const { EventEmitter } = require('node:events');
const { readFileSync } = require('node:fs');
const Module = require('node:module');
const { join } = require('node:path');
const { createTrayManager } = require('../src/linux/desktop/tray-manager.cjs');

class FakeTray extends EventEmitter {
  constructor(image) {
    super();
    this.image = image;
    this.tooltip = '';
    this.menu = null;
    this.destroyed = false;
  }

  setImage(image) { this.image = image; }
  setToolTip(value) { this.tooltip = value; }
  setContextMenu(menu) { this.menu = menu; }
  getBounds() { return { x: 1180, y: 760, width: 24, height: 24 }; }
  destroy() { this.destroyed = true; }
}

class FakeNotification extends EventEmitter {
  constructor(options) { super(); this.options = options; this.shown = false; }
  show() { this.shown = true; }
}

class FakePreview extends EventEmitter {
  constructor() { super(); this.hidden = true; this.destroyed = false; }
  isDestroyed() { return this.destroyed; }
  loadURL(url) { this.url = url; return Promise.resolve(); }
  setBounds(bounds) { this.bounds = bounds; }
  showInactive() { this.hidden = false; }
  hide() { this.hidden = true; }
  destroy() { this.destroyed = true; }
}

// 用最小 Electron 桩加载主进程测试导出的真实生命周期函数，不复制业务分支。
function loadMainWithElectronFakes() {
  const mainPath = require.resolve('../src/linux/desktop/main.cjs');
  class FakeWebContents extends EventEmitter {
    constructor() { super(); this.sent = []; }
    setWindowOpenHandler() {}
    send(channel, payload) { this.sent.push({ channel, payload }); }
  }
  class FakeBrowserWindow extends EventEmitter {
    static instances = [];
    constructor(options = {}) {
      super();
      this.options = options;
      this.webContents = new FakeWebContents();
      this.destroyed = false;
      this.minimized = false;
      this.visible = options.show !== false;
      this.focusCount = 0;
      this.bounds = { x: 0, y: 0, width: 1260, height: 820 };
      FakeBrowserWindow.instances.push(this);
    }
    removeMenu() {}
    isDestroyed() { return this.destroyed; }
    isMinimized() { return this.minimized; }
    restore() { this.minimized = false; }
    show() { this.visible = true; }
    hide() { this.visible = false; }
    isVisible() { return this.visible; }
    focus() { this.focusCount += 1; }
    getBounds() { return this.bounds; }
    setMinimumSize() {}
    setBounds(bounds) { this.bounds = bounds; }
    loadFile() { return Promise.resolve(); }
    close() { this.destroyed = true; this.emit('closed'); }
  }
  const electron = {
    app: { quit() {} }, BrowserWindow: FakeBrowserWindow, clipboard: {}, dialog: {}, ipcMain: {},
    Menu: {}, nativeImage: {}, Notification: {}, screen: {
      getPrimaryDisplay: () => ({ workArea: { x: 0, y: 0, width: 1600, height: 1000 } }),
      getDisplayMatching: () => ({ workArea: { x: 0, y: 0, width: 1600, height: 1000 } })
    }, session: {}, shell: {}, Tray: {}
  };
  const originalLoad = Module._load;
  const originalMode = process.env.LANCHAT_TEST_MODE;
  Module._load = function(request, parent, isMain) {
    if (request === 'electron') return electron;
    if (request.endsWith('service.cjs')) return class FakeService {};
    if (request.endsWith('appearance.js')) return {};
    if (request.endsWith('tray-manager.cjs')) return { createTrayManager() {} };
    return originalLoad.call(this, request, parent, isMain);
  };
  process.env.LANCHAT_TEST_MODE = '1';
  delete require.cache[mainPath];
  try {
    return { main: require(mainPath), FakeBrowserWindow };
  } finally {
    Module._load = originalLoad;
    if (originalMode === undefined) delete process.env.LANCHAT_TEST_MODE;
    else process.env.LANCHAT_TEST_MODE = originalMode;
  }
}

function createHarness(execFile, immediateTimeout = false, TrayType = FakeTray) {
  const intervals = [];
  const images = [];
  const notifications = [];
  const previews = [];
  let menuItems = [];
  const Menu = { buildFromTemplate(items) { menuItems = items; return items; } };
  const timer = callback => {
    if (immediateTimeout) callback();
    return callback;
  };
  const manager = createTrayManager({
    Tray: TrayType,
    Menu,
    Notification: class extends FakeNotification {
      constructor(options) { super(options); notifications.push(this); }
    },
    BrowserWindow: class extends FakePreview {
      constructor(options) { super(options); previews.push(this); }
    },
    screen: { getDisplayNearestPoint: () => ({ workArea: { x: 0, y: 0, width: 1280, height: 800 } }) },
    platform: 'linux',
    sessionType: 'x11',
    createImage: name => { images.push(name); return name; },
    execFile,
    setTimeoutFn: timer,
    clearTimeoutFn: () => {},
    setIntervalFn: (callback, delay) => { intervals.push({ callback, delay }); return callback; },
    clearIntervalFn: () => {},
    onActivateChat: chat => { manager.lastChat = chat; },
    onOpenSettings: () => { manager.openedSettings = true; },
    onOpenUpdate: () => { manager.openedUpdate = true; },
    onQuit: () => { manager.quitRequested = true; }
  });
  return { manager, getTray: () => manager.tray, getMenu: () => menuItems, images, intervals, notifications, previews };
}

async function run() {
  const success = createHarness((_command, args, options, callback) => {
    assert.deepEqual(args, [
      '--user', 'get-property', 'org.kde.StatusNotifierWatcher', '/StatusNotifierWatcher',
      'org.kde.StatusNotifierWatcher', 'IsStatusNotifierHostRegistered'
    ]);
    assert.equal(options.timeout, 1500);
    callback(null, 'b true', '');
  });
  await success.manager.initialize();
  assert.equal(success.manager.canHideToTray(), true);
  success.manager.setUnreadState({ count: 12, summary: '甲'.repeat(80) });
  assert.equal(success.images.includes('tray-unread'), true);
  assert.equal(success.images.includes('tray-blank'), false);
  assert.equal(success.intervals[0].delay, 450);
  success.intervals[0].callback();
  assert.equal(success.images.includes('tray-blank'), true);
  assert.equal(success.getTray().tooltip.length <= 63, true);
  success.getTray().emit('mouse-enter');
  assert.equal(success.previews.length, 1);
  success.getMenu()[0].click();
  assert.equal(success.manager.lastChat, null);
  success.getMenu()[1].click();
  success.getMenu()[2].click();
  success.getMenu().at(-1).click();
  assert.equal(success.manager.openedSettings, true);
  assert.equal(success.manager.openedUpdate, true);
  assert.equal(success.manager.quitRequested, true);
  success.manager.notifyMessage({ chat: 'peer-1', nick: '甲', text: '通知点击测试' });
  success.notifications.at(-1).emit('click');
  assert.equal(success.manager.lastChat, 'peer-1');
  success.manager.destroy();
  assert.equal(success.previews[0].destroyed, true);

  const failure = createHarness((_command, _args, _options, callback) => callback(new Error('busctl 不可用')));
  await failure.manager.initialize();
  assert.equal(failure.manager.canHideToTray(), false);
  failure.getTray().emit('click');
  assert.equal(failure.manager.canHideToTray(), true);
  failure.manager.destroy();

  const timeout = createHarness(() => {}, true);
  await timeout.manager.initialize();
  assert.equal(timeout.manager.canHideToTray(), false);
  timeout.manager.destroy();

  // Tray 构造失败时必须吞掉异常，主窗生命周期应自动走任务栏最小化分支。
  class ThrowingTray {
    constructor() { throw new Error('桌面没有托盘支持'); }
  }
  const creationFailure = createHarness((_command, _args, _options, callback) => callback(new Error('busctl 不可用')), false, ThrowingTray);
  await assert.doesNotReject(() => creationFailure.manager.initialize());
  assert.equal(creationFailure.getTray(), null);
  assert.equal(creationFailure.manager.canHideToTray(), false);
  creationFailure.manager.destroy();

  // 主窗关窗、恢复和聚焦均执行真实绑定分支，确认托盘不可用时保守最小化。
  const { main: mainModule } = loadMainWithElectronFakes();
  const makeWindow = () => {
    const target = new EventEmitter();
    target.hidden = false;
    target.hide = () => { target.hidden = true; };
    target.minimize = () => { target.minimized = true; };
    return target;
  };
  const closeEvent = () => ({ prevented: false, preventDefault() { this.prevented = true; } });
  const manager = { restored: 0, minimized: 0, windowRestored() { this.restored += 1; }, notifyMinimized() { this.minimized += 1; } };
  let forwarded = 0;
  const hiddenWindow = makeWindow();
  mainModule.bindWindowLifecycle(hiddenWindow, {
    getTraySettings: () => ({ keepAliveInTray: true }),
    getTrayManager: () => ({ ...manager, canHideToTray: () => true, windowRestored: () => { manager.restored += 1; }, notifyMinimized: () => { manager.minimized += 1; } }),
    forward: event => { if (event.event === 'window-restored') forwarded += 1; }
  });
  const hiddenClose = closeEvent();
  hiddenWindow.emit('close', hiddenClose);
  assert.equal(hiddenClose.prevented, true);
  assert.equal(hiddenWindow.hidden, true);
  assert.equal(hiddenWindow.minimized, undefined);

  const minimizedWindow = makeWindow();
  mainModule.bindWindowLifecycle(minimizedWindow, {
    getTraySettings: () => ({ keepAliveInTray: true }),
    getTrayManager: () => ({ canHideToTray: () => false }),
    forward: () => {}
  });
  const minimizedClose = closeEvent();
  minimizedWindow.emit('close', minimizedClose);
  assert.equal(minimizedClose.prevented, true);
  assert.equal(minimizedWindow.minimized, true);

  let quitCount = 0;
  const quittingWindow = makeWindow();
  mainModule.bindWindowLifecycle(quittingWindow, {
    getTraySettings: () => ({ keepAliveInTray: false }),
    quit: () => { quitCount += 1; },
    forward: () => {}
  });
  const quittingClose = closeEvent();
  quittingWindow.emit('close', quittingClose);
  assert.equal(quittingClose.prevented, true);
  assert.equal(quitCount, 1);
  hiddenWindow.emit('restore');
  hiddenWindow.emit('focus');
  assert.equal(forwarded, 2);
  assert.equal(manager.restored, 2);

  // 首次创建设置窗时，更新事件只能在 did-finish-load 后派发给设置渲染层。
  const settingsHarness = loadMainWithElectronFakes();
  settingsHarness.main.openSettingsWindow({ event: 'open-update-check' });
  const settingsWindow = settingsHarness.FakeBrowserWindow.instances.at(-1);
  assert.equal(settingsWindow.webContents.sent.length, 0);
  settingsWindow.webContents.emit('did-finish-load');
  assert.deepEqual(settingsWindow.webContents.sent, [{ channel: 'lanchat:event', payload: { event: 'open-update-check' } }]);
  settingsHarness.main.openSettingsWindow({ event: 'open-update-check' });
  assert.equal(settingsWindow.webContents.sent.length, 2);
  settingsWindow.close();

  // 主窗隐藏到托盘后打开设置，不得把隐藏主窗作为模态父窗，也不得关闭时强制聚焦隐藏主窗。
  const hiddenSettingsHarness = loadMainWithElectronFakes();
  hiddenSettingsHarness.main.showWindow();
  const mainWindow = hiddenSettingsHarness.FakeBrowserWindow.instances.at(-1);
  mainWindow.emit('ready-to-show');
  mainWindow.hide();
  hiddenSettingsHarness.main.openSettingsWindow();
  const hiddenParentSettings = hiddenSettingsHarness.FakeBrowserWindow.instances.at(-1);
  assert.equal(hiddenParentSettings.options.parent, undefined);
  assert.equal(hiddenParentSettings.options.modal, false);
  hiddenParentSettings.close();
  assert.equal(mainWindow.focusCount, 0);

  const main = readFileSync('src/linux/desktop/main.cjs', 'utf8');
  const profile = readFileSync('src/linux/LanChat.Linux/LinuxProfile.cs', 'utf8');
  const settings = readFileSync('src/linux/desktop/settings.js', 'utf8');
  assert.match(main, /targetWindow\.minimize\(\)/);
  assert.match(main, /getTraySettings\(\)\?\.keepAliveInTray === false/);
  assert.match(main, /event\.preventDefault\(\)/);
  assert.match(main, /app\.quit\(\)/);
  assert.match(main, /did-finish-load/);
  assert.match(main, /trayPreview/);
  assert.match(profile, /KeepAliveInTray/);
  assert.match(profile, /TrayPreview/);
  assert.match(settings, /api\.saveSettings\(nextNick, nextAuto, nextDownload, false, nextHistory, appearancePayload, trayPayload, networkPayload\)/);
  console.log('linux desktop lifecycle regression: ok');
}

if (require.main === module) {
  run().catch(error => { console.error(error); process.exitCode = 1; });
}

module.exports = run;
