const { execFile: systemExecFile } = require('node:child_process');
const { join } = require('node:path');

// 托盘适配器只负责桌面托盘、通知和可选的 Xorg 预览，不接触局域网通信。
function clip(text, limit = 63) {
  const value = String(text || '');
  return value.length <= limit ? value : value.slice(0, Math.max(0, limit - 3)) + '...';
}

function createTrayManager(options = {}) {
  const {
    Tray, Menu, Notification, BrowserWindow, screen,
    createImage = name => join(__dirname, 'assets', `${name}.png`),
    iconDirectory = join(__dirname, 'assets'),
    platform = process.platform,
    sessionType = process.env.XDG_SESSION_TYPE || '',
    execFile = systemExecFile,
    setIntervalFn = setInterval,
    clearIntervalFn = clearInterval,
    setTimeoutFn = setTimeout,
    clearTimeoutFn = clearTimeout,
    onActivateChat = () => {},
    onOpenSettings = () => {},
    onOpenUpdate = () => {},
    onQuit = () => {}
  } = options;
  let tray = null;
  let previewWindow = null;
  let previewTimer = null;
  let flashTimer = null;
  let minimizeNotified = false;
  let statusNotifierHostAvailable = false;
  let trayInteractionConfirmed = false;
  let settings = { keepAliveInTray: true, trayPreview: true };
  let unread = { count: 0, summary: '' };
  const xorg = platform === 'linux' && ['x11', 'xorg'].includes(String(sessionType).toLowerCase());

  function recordTrayFailure(error) {
    tray = null;
    statusNotifierHostAvailable = false;
    console.error('托盘初始化失败，已降级为任务栏最小化', error);
  }

  // 根据未读数量生成与 Windows 端一致的短提示，避免托盘实现暴露过长摘要。
  function tooltip() {
    if (!unread.count) return 'LanChat';
    const suffix = unread.summary ? ` (${unread.summary})` : '';
    return clip(`LanChat: ${unread.count}条未读${suffix}`);
  }

  function image(name) {
    return createImage(name, iconDirectory);
  }

  function markInteraction() {
    trayInteractionConfirmed = true;
  }

  function updateIcon() {
    if (!tray) return;
    tray.setToolTip(tooltip());
    tray.setImage(image(unread.count ? 'tray-unread' : 'tray'));
  }

  function stopFlash() {
    if (flashTimer) clearIntervalFn(flashTimer);
    flashTimer = null;
    if (tray) tray.setImage(image(unread.count ? 'tray-unread' : 'tray'));
  }

  function startFlash() {
    if (!xorg || !settings.trayPreview || !unread.count || flashTimer || !tray) return;
    let blank = false;
    flashTimer = setIntervalFn(() => {
      if (!unread.count || !settings.trayPreview) {
        stopFlash();
        return;
      }
      blank = !blank;
      tray.setImage(image(blank ? 'tray-blank' : 'tray-unread'));
    }, 450);
  }

  function hidePreview() {
    if (previewTimer) clearTimeoutFn(previewTimer);
    previewTimer = null;
    if (previewWindow && !previewWindow.isDestroyed()) previewWindow.hide();
  }

  function escapeHtml(value) {
    return String(value || '').replace(/[&<>"']/g, char => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[char]));
  }

  // Xorg 才使用全局坐标预览；原生 Wayland 不依赖此增强能力。
  function showPreview() {
    if (!xorg || !settings.trayPreview || !unread.count || !BrowserWindow || !tray) return;
    if (!previewWindow || previewWindow.isDestroyed()) {
      previewWindow = new BrowserWindow({
        width: 320, height: 88, show: false, frame: false, resizable: false,
        skipTaskbar: true, alwaysOnTop: true, backgroundColor: '#202024',
        webPreferences: { sandbox: true, contextIsolation: true, nodeIntegration: false }
      });
      previewWindow.on('closed', () => { previewWindow = null; });
    }
    const body = escapeHtml(unread.summary || '收到新消息');
    const css = encodeURIComponent('body{margin:0;padding:16px;background:#202024;color:#f2f2f6;font:14px sans-serif;border:1px solid #34343c;border-radius:10px}strong{color:#a16b39}');
    const html = '<!doctype html><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src \'none\'; style-src data:"><link rel="stylesheet" href="data:text/css,' + css + '"><strong>LanChat</strong><div>' + body + '</div>';
    void previewWindow.loadURL('data:text/html;charset=utf-8,' + encodeURIComponent(html));
    const trayBounds = tray.getBounds?.();
    const display = trayBounds && screen?.getDisplayNearestPoint ? screen.getDisplayNearestPoint({ x: trayBounds.x, y: trayBounds.y }) : null;
    const area = display?.workArea || { x: 0, y: 0, width: 1280, height: 800 };
    const x = Math.max(area.x, Math.min((trayBounds?.x || area.x) - 280, area.x + area.width - 320));
    const y = Math.max(area.y, (trayBounds?.y || area.y) - 96);
    previewWindow.setBounds({ x, y, width: 320, height: 88 });
    previewWindow.showInactive();
    if (previewTimer) clearTimeoutFn(previewTimer);
    previewTimer = setTimeoutFn(hidePreview, 2400);
  }

  function buildMenu() {
    return Menu.buildFromTemplate([
      { label: '显示主界面', click: () => { markInteraction(); hidePreview(); onActivateChat(null); } },
      { label: '系统设置...', click: () => { markInteraction(); onOpenSettings(); } },
      { label: '检查新版本...', click: () => { markInteraction(); onOpenUpdate(); } },
      { type: 'separator' },
      { label: '退出 LanChat', click: () => { markInteraction(); onQuit(); } }
    ]);
  }

  function createTray() {
    if (tray) return tray;
    if (!Tray || !Menu) {
      recordTrayFailure(new Error('托盘依赖不可用'));
      return null;
    }
    let candidate = null;
    try {
      candidate = new Tray(image('tray'));
      candidate.setToolTip(tooltip());
      candidate.setContextMenu(buildMenu());
      candidate.on('click', () => { markInteraction(); hidePreview(); onActivateChat(null); });
      candidate.on('mouse-enter', showPreview);
      candidate.on('mouse-leave', hidePreview);
      tray = candidate;
      return tray;
    } catch (error) {
      candidate?.destroy?.();
      recordTrayFailure(error);
      return null;
    }
  }

  function probeStatusNotifierHost() {
    if (platform !== 'linux') return Promise.resolve(false);
    return new Promise(resolve => {
      let settled = false;
      const finish = value => {
        if (settled) return;
        settled = true;
        resolve(Boolean(value));
      };
      const timeout = setTimeoutFn(() => finish(false), 1500);
      try {
        execFile('busctl', [
          '--user', 'get-property', 'org.kde.StatusNotifierWatcher',
          '/StatusNotifierWatcher', 'org.kde.StatusNotifierWatcher', 'IsStatusNotifierHostRegistered'
        ], { timeout: 1500, windowsHide: true }, (error, stdout) => {
          clearTimeoutFn(timeout);
          finish(!error && /\btrue\b/i.test(String(stdout || '')));
        });
      } catch {
        clearTimeoutFn(timeout);
        finish(false);
      }
    });
  }

  function notify(title, body, chat) {
    if (!Notification) return;
    try {
      const item = new Notification({ title, body: clip(body, 240), icon: image('tray') });
      item.on?.('click', () => onActivateChat(chat || null));
      item.show();
    } catch {}
  }

  return {
    async initialize() {
      try {
        statusNotifierHostAvailable = await probeStatusNotifierHost();
        if (!createTray()) return { statusNotifierHostAvailable: false };
        updateIcon();
        startFlash();
      } catch (error) {
        recordTrayFailure(error);
      }
      return { statusNotifierHostAvailable };
    },
    setSettings(next = {}) {
      if (typeof next.keepAliveInTray === 'boolean') settings.keepAliveInTray = next.keepAliveInTray;
      if (typeof next.trayPreview === 'boolean') settings.trayPreview = next.trayPreview;
      updateIcon();
      if (settings.trayPreview) startFlash();
      else stopFlash();
    },
    canHideToTray() { return Boolean(tray && (statusNotifierHostAvailable || trayInteractionConfirmed)); },
    setUnreadState(next = {}) {
      unread = { count: Math.max(0, Number(next.count) || 0), summary: String(next.summary || '') };
      updateIcon();
      if (unread.count) startFlash();
      else stopFlash();
    },
    notifyMessage(message) {
      if (!message || message.self) return;
      const name = message.nick || message.peerNick || '局域网节点';
      notify(`LanChat · ${name}`, message.text || message.name || '收到新消息', message.chat);
      startFlash();
    },
    notifyMinimized() {
      if (minimizeNotified) return;
      minimizeNotified = true;
      notify('LanChat', '主界面已最小化到系统托盘', null);
    },
    windowRestored() { hidePreview(); },
    destroy() {
      stopFlash();
      hidePreview();
      if (previewWindow && !previewWindow.isDestroyed()) previewWindow.destroy();
      previewWindow = null;
      tray?.destroy();
      tray = null;
    },
    get tray() { return tray; },
    get statusNotifierHostAvailable() { return statusNotifierHostAvailable; },
    get trayInteractionConfirmed() { return trayInteractionConfirmed; }
  };
}

module.exports = { clip, createTrayManager };
