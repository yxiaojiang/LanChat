const { app, BrowserWindow, clipboard, dialog, ipcMain, Menu, nativeImage, Notification, screen, session, shell, Tray } = require('electron');
const { execFile } = require('node:child_process');
const { randomUUID } = require('node:crypto');
const { mkdir, open, readFile, realpath, stat, unlink } = require('node:fs/promises');
const os = require('node:os');
const { basename, dirname, isAbsolute, join, resolve } = require('node:path');
const { fileURLToPath, pathToFileURL } = require('node:url');
const appearance = require('./appearance.js');
const Service = require('./service.cjs');
const { createTrayManager } = require('./tray-manager.cjs');

const page = join(__dirname, 'index.html');
const pageUrl = pathToFileURL(page).href;
const settingsPage = join(__dirname, 'settings.html');
const settingsPageUrl = pathToFileURL(settingsPage).href;
const allowedFilePaths = new Set();
const pendingDrops = new Map();
const pendingPastes = new Map();
// 主进程持有截图清理责任，直到后端确认历史已引用该路径。
const sendingPastes = new Map();
const pasteOperations = new Set();
// 首次成功读取的非空表情清单由主进程复用，避免重连时重复传输整包数据。
let emojiPackCache = null;
const maxFiles = 32;
const maxPastedImageBytes = 64 * 1024 * 1024;
let window;
let settingsWindow;
let service;
let restarting;
let exiting = false;
let stopped = false;
let windowBackground = '#121212';
let trayManager;
let traySettings = { keepAliveInTray: true, trayPreview: true };
let settingsWindowLoaded = false;
let pendingSettingsEvent = null;

function recordMessagePaths(message) {
  if (message?.file && typeof message.path === 'string' && isAbsolute(message.path)) {
    allowedFilePaths.add(resolve(message.path));
  }
}

function forward(event) {
  if (window && !window.isDestroyed()) window.webContents.send('lanchat:event', event);
}

function startService() {
  allowedFilePaths.clear();
  pendingDrops.clear();
  const child = new Service(join(process.resourcesPath, 'lanchat-bin'));
  service = child;
  child.on('event', event => {
    if (service !== child) return;
    if (event?.event === 'message') {
      const paste = sendingPastes.get(event.data.path);
      if (paste && event.data.historySaved === true) paste.saved = true;
      recordMessagePaths(event.data);
      if ((!window || !window.isFocused()) && trayManager) trayManager.notifyMessage(event.data);
    }
    forward(event);
  });
  child.on('disconnect', error => {
    if (service === child && !exiting) forward({ event: 'disconnect', data: error.message });
  });
}

function showWindow() {
  if (!window || window.isDestroyed()) {
    window = new BrowserWindow({
      width: 1040, height: 720, minWidth: 880, minHeight: 560,
      backgroundColor: '#121212', show: false, autoHideMenuBar: true,
      icon: join(__dirname, 'assets/app.png'),
      webPreferences: {
        preload: join(__dirname, 'preload.cjs'),
        sandbox: true,
        contextIsolation: true,
        nodeIntegration: false,
        spellcheck: false
      }
    });
    window.removeMenu();
    window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
    window.webContents.on('will-navigate', event => event.preventDefault());
    window.webContents.on('will-attach-webview', event => event.preventDefault());
    window.once('ready-to-show', () => {
      window.show();
      trayManager?.windowRestored();
      forward({ event: 'window-restored' });
    });
    bindWindowLifecycle(window);
    window.loadFile(page).catch(error => {
      console.error(error);
      app.quit();
    });
  } else {
    if (window.isMinimized()) window.restore();
    window.show();
    window.focus();
    trayManager?.windowRestored();
    forward({ event: 'window-restored' });
  }
}

// 绑定主窗生命周期：后台驻留只拦截普通关窗，显式退出仍必须进入 app.quit() 清理链。
function bindWindowLifecycle(targetWindow, lifecycle = {}) {
  const getStopped = lifecycle.getStopped || (() => stopped);
  const getTraySettings = lifecycle.getTraySettings || (() => traySettings);
  const getTrayManager = lifecycle.getTrayManager || (() => trayManager);
  const send = lifecycle.forward || forward;
  const notifyRestored = () => {
    getTrayManager()?.windowRestored?.();
    send({ event: 'window-restored' });
  };

  targetWindow.on('close', event => {
    if (getStopped()) return;
    if (getTraySettings()?.keepAliveInTray === false) {
      event.preventDefault();
      if (lifecycle.quit) lifecycle.quit();
      else app.quit();
      return;
    }
    event.preventDefault();
    if (getTrayManager()?.canHideToTray?.()) {
      targetWindow.hide();
      getTrayManager()?.notifyMinimized?.();
    } else {
      targetWindow.minimize();
    }
  });
  targetWindow.on('restore', notifyRestored);
  targetWindow.on('focus', notifyRestored);
  return targetWindow;
}

// 按 1260×820 基准计算设置窗口客户区尺寸，并适配当前显示器工作区与居中位置。
function computeSettingsBounds(workArea, parentBounds = null) {
  const minWidth = Math.min(480, Math.max(240, workArea.width - 16));
  const minHeight = Math.min(400, Math.max(200, workArea.height - 40));
  const width = Math.max(minWidth, Math.min(1260, workArea.width - 40));
  const height = Math.max(minHeight, Math.min(820, workArea.height - 40));
  const originX = parentBounds
    ? Math.round(parentBounds.x + (parentBounds.width - width) / 2)
    : Math.round(workArea.x + (workArea.width - width) / 2);
  const originY = parentBounds
    ? Math.round(parentBounds.y + (parentBounds.height - height) / 2)
    : Math.round(workArea.y + (workArea.height - height) / 2);
  const x = Math.max(workArea.x, Math.min(originX, workArea.x + workArea.width - width));
  const y = Math.max(workArea.y, Math.min(originY, workArea.y + workArea.height - height));
  return { width, height, x, y };
}

function computeSettingsMinSize(bounds) {
  return {
    minWidth: Math.min(720, bounds.width),
    minHeight: Math.min(500, bounds.height)
  };
}

function dispatchSettingsEvent(event) {
  if (!settingsWindow || settingsWindow.isDestroyed()) return;
  if (!settingsWindowLoaded) {
    pendingSettingsEvent = event;
    return;
  }
  settingsWindow.webContents.send('lanchat:event', event);
}

function openSettingsWindow(event = null) {
  if (settingsWindow && !settingsWindow.isDestroyed()) {
    if (settingsWindow.isMinimized()) settingsWindow.restore();
    settingsWindow.show();
    settingsWindow.focus();
    if (event) dispatchSettingsEvent(event);
    return;
  }
  const hasParent = window && !window.isDestroyed();
  const parentBounds = hasParent ? window.getBounds() : null;
  const display = parentBounds ? screen.getDisplayMatching(parentBounds) : screen.getPrimaryDisplay();
  const bounds = computeSettingsBounds(display.workArea, parentBounds);
  const minSize = computeSettingsMinSize(bounds);
  settingsWindowLoaded = false;
  if (event) pendingSettingsEvent = event;
  settingsWindow = new BrowserWindow({
    parent: hasParent ? window : undefined,
    modal: Boolean(hasParent),
    useContentSize: true,
    width: bounds.width,
    height: bounds.height,
    x: bounds.x,
    y: bounds.y,
    minWidth: minSize.minWidth,
    minHeight: minSize.minHeight,
    title: 'LanChat 设置',
    backgroundColor: windowBackground,
    show: false,
    autoHideMenuBar: true,
    icon: join(__dirname, 'assets/app.png'),
    webPreferences: {
      preload: join(__dirname, 'preload.cjs'),
      sandbox: true,
      contextIsolation: true,
      nodeIntegration: false,
      spellcheck: false
    }
  });
  settingsWindow.removeMenu();
  settingsWindow.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  settingsWindow.webContents.on('will-navigate', event => event.preventDefault());
  settingsWindow.webContents.on('will-attach-webview', event => event.preventDefault());
  settingsWindow.webContents.once('did-finish-load', () => {
    settingsWindowLoaded = true;
    const pending = pendingSettingsEvent;
    pendingSettingsEvent = null;
    if (pending && settingsWindow && !settingsWindow.isDestroyed())
      settingsWindow.webContents.send('lanchat:event', pending);
  });
  settingsWindow.once('ready-to-show', () => {
    if (!settingsWindow || settingsWindow.isDestroyed()) return;
    const wa = display.workArea;
    const outer = settingsWindow.getBounds();
    const clampedWidth = Math.min(outer.width, wa.width);
    const clampedHeight = Math.min(outer.height, wa.height);
    const clampedX = Math.max(wa.x, Math.min(outer.x, wa.x + wa.width - clampedWidth));
    const clampedY = Math.max(wa.y, Math.min(outer.y, wa.y + wa.height - clampedHeight));
    if (clampedWidth !== outer.width || clampedHeight !== outer.height || clampedX !== outer.x || clampedY !== outer.y) {
      settingsWindow.setMinimumSize(Math.min(720, clampedWidth), Math.min(500, clampedHeight));
      settingsWindow.setBounds({ x: clampedX, y: clampedY, width: clampedWidth, height: clampedHeight });
    }
    settingsWindow.show();
  });
  settingsWindow.on('closed', () => {
    settingsWindow = null;
    settingsWindowLoaded = false;
    pendingSettingsEvent = null;
    if (window && !window.isDestroyed() && !exiting) {
      window.focus();
      forward({ event: 'settings-closed' });
    }
  });
  settingsWindow.loadFile(settingsPage).catch(error => {
    console.error(error);
    settingsWindow?.close();
  });
}

function handle(name, operation, allowSettings = false) {
  ipcMain.handle('lanchat:' + name, async (event, ...args) => {
    if (!isAllowedRequest(event, window, settingsWindow, pageUrl, settingsPageUrl, allowSettings) || exiting) {
      throw new Error('无效的桌面请求');
    }
    return operation(...args);
  });
}

// 统一校验主窗与设置窗的 IPC 来源，避免设置窗误用未放行的请求。
function isAllowedRequest(event, mainWindow, settingsWindowRef, mainUrl, settingsUrl, allowSettings) {
  const sender = event?.sender;
  const frame = event?.senderFrame;
  const fromMain = sender === mainWindow?.webContents && frame === sender?.mainFrame && frame?.url === mainUrl;
  const fromSettings = allowSettings && sender === settingsWindowRef?.webContents &&
    frame === sender?.mainFrame && frame?.url === settingsUrl;
  return Boolean(fromMain || fromSettings);
}

function text(value, limit) {
  if (typeof value !== 'string' || !value.trim() || value.length > limit)
    throw new Error('输入内容无效');
  return value;
}

function fileRecipients(chat) {
  const list = Array.isArray(chat) ? chat : [chat];
  if (!list.length || list.length > 16 || new Set(list).size !== list.length)
    throw new Error('请选择 1 至 16 个文件接收节点');
  if (list.some(item => item === 'BROADCAST')) throw new Error('请选择文件接收节点');
  return list.map(item => text(item, 256));
}

function isIpv4(value) {
  const parts = typeof value === 'string' ? value.trim().split('.') : [];
  return parts.length === 4 && parts.every(item => /^\d{1,3}$/.test(item) && Number(item) <= 255);
}

async function validateLocalFiles(paths) {
  if (!Array.isArray(paths) || !paths.length || paths.length > maxFiles)
    throw new Error(`一次最多发送 ${maxFiles} 个文件`);
  return Promise.all(paths.map(async path => {
    if (!isAbsolute(text(path, 4096))) throw new Error('请选择本地文件');
    const file = await realpath(path);
    const info = await stat(file);
    if (!info.isFile()) throw new Error('只能发送普通文件');
    return { path: file, name: basename(file), size: info.size };
  }));
}

// 优先读取桌面文件管理器的文件格式，普通文本中的路径仍按文本粘贴。
async function readClipboardPaths(items) {
  const formats = ['electron application/osclipboard;format="x-special/gnome-copied-files"', 'text/uri-list'];
  for (const format of formats) {
    const paths = new Set();
    for (const item of items) {
      if (!item.types.includes(format)) continue;
      const content = await (await item.getType(format)).text();
      for (const line of content.replace(/\0+$/, '').split(/\r?\n/)) {
        if (!line.startsWith('file:')) continue;
        try { paths.add(fileURLToPath(line)); } catch { /* 忽略无效或远程地址。 */ }
      }
    }
    if (paths.size) return [...paths];
  }
  return [];
}

async function stagePastedFiles(paths, imageBytes, allowImage) {
  const items = paths.length || imageBytes != null ? null : await clipboard.read();
  const localPaths = paths.length ? paths : await readClipboardPaths(items ?? []);
  const files = localPaths.length ? await validateLocalFiles([...new Set(localPaths)]) : [];
  if (!files.length && allowImage === true) {
    const direct = imageBytes == null ? null : decodeClipboardImage(imageBytes);
    // 内存图像解码为空时，再读系统剪贴板提供的 PNG/JPEG 表示。
    const image = direct ?? await readClipboardImage(items ?? await clipboard.read());
    if (image) files.push(await stageClipboardImage(image));
    else if (imageBytes != null) throw new Error('无法读取剪贴板图片');
  }
  if (exiting) {
    await Promise.all(files.map(deleteOwnedPaste));
    return [];
  }
  return files.map(file => {
    const id = randomUUID();
    pendingPastes.set(id, file);
    return { id, name: file.name, size: file.size, preview: file.preview };
  });
}

async function readClipboardImage(items) {
  let attempted = false;
  for (const type of ['image/png', 'image/jpeg']) {
    for (const item of items) {
      if (!item.types.includes(type)) continue;
      attempted = true;
      const blob = await item.getType(type);
      if (blob.size > maxPastedImageBytes) throw new Error('剪贴板图片超过 64 MiB');
      const image = decodeClipboardImage(await blob.arrayBuffer());
      if (image) return image;
    }
  }
  if (attempted) throw new Error('无法读取剪贴板图片');
  return null;
}

function decodeClipboardImage(bytes) {
  if (!(bytes instanceof ArrayBuffer) || bytes.byteLength > maxPastedImageBytes)
    throw new Error('剪贴板图片格式无效或超过 64 MiB');
  const image = nativeImage.createFromBuffer(Buffer.from(bytes));
  return image.isEmpty() ? null : image;
}

async function stageClipboardImage(image) {
  const png = image.toPNG();
  if (png.length > maxPastedImageBytes) throw new Error('剪贴板图片超过 64 MiB');
  const { width, height } = image.getSize();
  const preview = image.resize(width >= height ? { width: 48 } : { height: 48 }).toDataURL();
  // 只为应用独占创建的图片设置清理标记。
  const directory = join(app.getPath('userData'), 'PastedImages');
  const name = '截图-' + new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-') + '-' + randomUUID().slice(0, 12) + '.png';
  const path = join(directory, name);
  await mkdir(directory, { recursive: true });
  const file = await open(path, 'wx', 0o600);
  try {
    try { await file.writeFile(png); } finally { await file.close(); }
  } catch (error) {
    await unlink(path).catch(() => {});
    throw error;
  }
  return { path, name, size: png.length, preview, managed: true };
}

async function deleteOwnedPaste(file) {
  if (file?.managed && !file.saved) await unlink(file.path).catch(() => {});
}

function trackPasteOperation(operation) {
  pasteOperations.add(operation);
  return operation.finally(() => pasteOperations.delete(operation));
}

async function discardPastedFiles(ids) {
  await Promise.all(ids.map(async id => {
    const file = pendingPastes.get(id);
    pendingPastes.delete(id);
    await deleteOwnedPaste(file);
  }));
}

// 只读取本地导入目录的清单与指定 PNG，渲染进程不接触文件路径。
async function readEmojiPack(directory = join(app.getPath('userData'), 'WeChatEmoji')) {
  try {
    const manifest = join(directory, 'manifest.tsv');
    if ((await stat(manifest)).size > 16 * 1024) return [];
    const lines = (await readFile(manifest, 'utf8')).split(/\r?\n/).slice(0, 128);
    const entries = await Promise.all(lines.map(async line => {
      const [token, name, extra] = line.split('\t');
      if (extra !== undefined || !/^\[[^\[\]\s]{1,24}\]$/.test(token) || !/^[0-9]{3}\.png$/.test(name)) return null;
      const file = join(directory, name);
      try {
        if ((await stat(file)).size > 128 * 1024) return null;
        const bytes = await readFile(file);
        if (bytes.length < 24 || bytes.readBigUInt64BE() !== 0x89504e470d0a1a0an ||
            bytes.readUInt32BE(16) > 64 || bytes.readUInt32BE(20) > 64) return null;
        return [token, 'data:image/png;base64,' + bytes.toString('base64')];
      } catch { return null; }
    }));
    return entries.filter(Boolean);
  } catch { return []; }
}

async function dispatchFiles(chat, files, waitForResult = true) {
  const chatIds = fileRecipients(chat);
  const child = service;
  const onlinePeers = await child.request('peers');
  const peers = chatIds.map(id => onlinePeers.find(item => item.id === id));
  if (exiting) throw new Error('应用正在退出');
  if (service !== child || child.disconnected) throw new Error('通信服务已断开，请重新连接后发送');
  if (peers.some(peer => !peer)) throw new Error('好友当前离线，暂时无法发送文件');
  for (const file of files) {
    if (file.managed) {
      file.remaining = (file.remaining || 0) + peers.length;
      sendingPastes.set(file.path, file);
    }
  }
  const jobs = peers.flatMap(peer => files.map(file => {
    const progress = {
      event: 'progress', operation: randomUUID(), chat: peer.key, peerNick: peer.nick,
      name: file.name, total: file.size, sending: true
    };
    forward({ ...progress, queued: true, received: 0 });
    return sendOneFile(child, peer, file, progress);
  }));
  const result = Promise.all(jobs);
  if (waitForResult) return result;
  void trackPasteOperation(result).catch(error => forward({ event: 'file-error', data: error.message }));
}

async function sendOneFile(child, peer, file, progress) {
  const { path, managed } = file;
  try {
    if (exiting) throw new Error('应用正在退出');
    const request = managed ? { chat: peer.id, path, managedPaste: true } : { chat: peer.id, path };
    const result = await child.request('file', request, progress.operation);
    if (managed && result?.historySaved === true) file.saved = true;
    if (!managed || file.saved) allowedFilePaths.add(resolve(path));
    return { ok: true, chat: peer.id, operation: progress.operation };
  } catch (error) {
    // 文件在并发提交时被删除等失败仍只影响当前目标。
    if (service === child && !child.disconnected)
      forward({ ...progress, queued: false, failed: true, error: error.message });
    return { ok: false, chat: peer.id, operation: progress.operation, error: error.message };
  } finally {
    if (managed) {
      file.remaining = Math.max(0, (file.remaining || 1) - 1);
      // 所有目标均已结算且没有历史引用时才删除托管截图；服务异常退出则保守保留。
      if (file.remaining === 0) {
        if (exiting) {
          // 正常退出要等后端排空并确认历史结果；异常崩溃则保守保留托管截图。
          const stoppedNormally = await child.stop();
          if (stoppedNormally && !file.saved) await deleteOwnedPaste(file);
        } else if (!child.disconnected && !file.saved) {
          await deleteOwnedPaste(file);
        }
        sendingPastes.delete(path);
      }
    }
  }
}

async function sendPastedFiles(chat, ids) {
  if (!Array.isArray(ids) || !ids.length || ids.length > maxFiles || new Set(ids).size !== ids.length)
    throw new Error(`一次最多发送 ${maxFiles} 个文件`);
  const files = ids.map(id => pendingPastes.get(id));
  if (files.some(file => !file)) throw new Error('粘贴的文件已不可用，请重新粘贴');
  const chatIds = fileRecipients(chat);
  for (const id of ids) pendingPastes.delete(id);
  try {
    await dispatchFiles(chatIds, files, false);
    return { ok: true };
  } catch (error) {
    // 退出后不能重新登记草稿，预检已接管的图片在这里结算。
    if (exiting) await Promise.all(files.map(deleteOwnedPaste));
    else ids.forEach((id, i) => pendingPastes.set(id, files[i]));
    throw error;
  }
}

async function sendFiles(chat, paths) {
  fileRecipients(chat);
  return dispatchFiles(chat, await validateLocalFiles(paths));
}

// 使用固定命令和固定参数探测截图工具，避免把用户输入拼接进 Shell。
function triggerScreenshot() {
  const candidates = [
    ['gnome-screenshot', ['--help'], []],
    ['spectacle', ['--help'], []],
    ['xfce4-screenshooter', ['--help'], ['-r']],
    ['flameshot', ['--version'], ['gui']],
    ['mate-screenshot', ['--help'], []]
  ];
  return new Promise(resolve => {
    const probe = index => {
      if (index >= candidates.length) {
        resolve({ ok: false, message: '未找到系统截图工具，请使用系统截图快捷键截图后，在输入框按 Ctrl+V 粘贴' });
        return;
      }
      const [command, probeArgs, launchArgs] = candidates[index];
      execFile(command, probeArgs, { timeout: 1500, windowsHide: true }, error => {
        if (error) {
          probe(index + 1);
          return;
        }
        const child = execFile(command, launchArgs, { detached: true, windowsHide: true }, () => {});
        child.unref?.();
        resolve({ ok: true, message: '已启动系统截图工具，请完成截图后在输入框按 Ctrl+V 粘贴' });
      });
    };
    probe(0);
  });
}

async function reveal(target) {
  const error = await shell.openPath(target);
  if (error) throw new Error(error);
}

// 只打开通信记录登记过的本地绝对路径。原文件丢失时仍允许打开其直接父目录。
async function openPath(target, kind) {
  if (kind !== 'file' && kind !== 'directory') throw new Error('打开类型无效');
  const requested = text(target, 4096);
  if (!isAbsolute(requested)) throw new Error('路径无效');
  if (!allowedFilePaths.has(resolve(requested))) throw new Error('文件路径未在通信记录中登记');
  if (kind === 'file') {
    const file = await realpath(requested);
    if (!(await stat(file)).isFile()) throw new Error('原文件不可用');
    await reveal(file);
    return;
  }
  let folder = '';
  try {
    const file = await realpath(requested);
    const info = await stat(file);
    folder = info.isDirectory() ? file : dirname(file);
  } catch (error) {
    if (error?.code !== 'ENOENT') throw error;
    try {
      folder = await realpath(dirname(requested));
    } catch {
      throw new Error('所在目录不可用');
    }
  }
  if (!(await stat(folder)).isDirectory()) throw new Error('所在目录不可用');
  await reveal(folder);
}

if (process.env.LANCHAT_TEST_MODE === '1') {
  module.exports = { openPath, recordMessagePaths, allowedFilePaths, computeSettingsBounds, computeSettingsMinSize,
    readClipboardPaths, stagePastedFiles, discardPastedFiles, readEmojiPack, bindWindowLifecycle, openSettingsWindow,
    dispatchFiles, triggerScreenshot, isIpv4, isAllowedRequest };
} else if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  app.enableSandbox();
  app.on('second-instance', () => { if (app.isReady() && !exiting) showWindow(); });
  app.on('activate', () => { if (!exiting) showWindow(); });
  app.on('before-quit', event => {
    if (stopped || !service) return;
    event.preventDefault();
    if (exiting) return;
    exiting = true;
    Promise.allSettled([
      service.stop(), discardPastedFiles([...pendingPastes.keys()]), ...pasteOperations
    ]).finally(() => {
      stopped = true;
      trayManager?.destroy();
      app.quit();
    });
  });
  app.whenReady().then(() => {
    session.defaultSession.setPermissionRequestHandler((_contents, _permission, decide) => decide(false));
    session.defaultSession.setPermissionCheckHandler(() => false);
    session.defaultSession.on('will-download', event => event.preventDefault());
    trayManager = createTrayManager({
      Tray, Menu, Notification, BrowserWindow, screen,
      createImage: name => nativeImage.createFromPath(join(__dirname, 'assets', `${name}.png`)),
      onActivateChat: chat => {
        showWindow();
        if (chat) forward({ event: 'activate-chat', data: { chat } });
      },
      onOpenSettings: () => openSettingsWindow(),
      onOpenUpdate: () => openSettingsWindow({ event: 'open-update-check' }),
      onQuit: () => app.quit()
    });
    trayManager.setSettings(traySettings);
    void trayManager.initialize();
    ipcMain.on('lanchat:unread-state', (event, state) => {
      const fromMain = event.sender === window?.webContents && event.senderFrame === event.sender.mainFrame &&
        event.senderFrame.url === pageUrl;
      if (!fromMain || exiting || !state || typeof state !== 'object') return;
      trayManager?.setUnreadState(state);
    });
    handle('state', () => service.request('state'), true);
    handle('history', async chat => {
      const result = await service.request('history', { chat: text(chat, 256) });
      for (const item of result?.messages ?? []) recordMessagePaths(item);
      return result;
    });
    handle('peers', () => service.request('peers'));
    handle('refresh', () => service.request('refresh'), true);
    handle('reconnect', async () => {
      if (!restarting && service.disconnected) {
        restarting = (async () => {
          await service.stop();
          if (exiting) return;
          startService();
          await service.ready;
        })().finally(() => { restarting = null; });
      }
      await restarting;
    });
    handle('open-settings', () => { openSettingsWindow(); });
    handle('close-settings', () => {
      if (settingsWindow && !settingsWindow.isDestroyed()) settingsWindow.close();
    }, true);
    handle('system-info', () => ({
      hostname: os.hostname(),
      osText: `${os.type()} ${os.release()} (${os.arch() === 'arm64' ? 'ARM64 64位' : os.arch() === 'x64' ? 'x86_64 64位' : os.arch()})`,
      arch: os.arch(),
      version: app.getVersion(),
      electronVersion: process.versions.electron
    }), true);
    handle('settings', async () => {
      const current = await service.request('settings');
      if (current) {
        windowBackground = appearance.surfaceHex(18, 18, 18, appearance.normalizeThemeMode(current.themeMode));
        traySettings = {
          keepAliveInTray: current.keepAliveInTray !== false,
          trayPreview: current.trayPreview !== false
        };
        trayManager?.setSettings(traySettings);
      }
      return current;
    }, true);
    handle('save-settings', async (nickname, autoAcceptFiles, downloadDirectory, confirmOverride, historyDirectory, appearanceInput = null, trayInput = null, networkInput = null) => {
      if (typeof autoAcceptFiles !== 'boolean' || typeof confirmOverride !== 'boolean')
        throw new Error('设置值无效');
      if (downloadDirectory !== null && !isAbsolute(text(downloadDirectory, 4096)))
        throw new Error('文件接收目录无效');
      if (historyDirectory !== null && !isAbsolute(text(historyDirectory, 4096)))
        throw new Error('聊天记录目录无效');
      const payload = {
        nickname: text(nickname, 80), autoAcceptFiles, downloadDirectory, confirmOverride, historyDirectory
      };
      if (trayInput !== null && trayInput !== undefined) {
        if (typeof trayInput !== 'object' || Array.isArray(trayInput)) throw new Error('托盘设置无效');
        for (const key of ['keepAliveInTray', 'trayPreview']) {
          if (trayInput[key] !== undefined && typeof trayInput[key] !== 'boolean') throw new Error('托盘设置无效');
          if (trayInput[key] !== undefined) payload[key] = trayInput[key];
        }
      }
      if (networkInput !== null && networkInput !== undefined) {
        if (typeof networkInput !== 'object' || Array.isArray(networkInput)) throw new Error('网络设置无效');
        for (const key of ['tunFilter', 'unicastProbe', 'autoLoadHistory']) {
          if (networkInput[key] !== undefined && typeof networkInput[key] !== 'boolean') throw new Error('网络设置无效');
          if (networkInput[key] !== undefined) payload[key] = networkInput[key];
        }
        if (networkInput.sendShortcutMode !== undefined) {
          if (!Number.isInteger(networkInput.sendShortcutMode) || networkInput.sendShortcutMode < 0 || networkInput.sendShortcutMode > 1)
            throw new Error('发送快捷键模式无效');
          payload.sendShortcutMode = networkInput.sendShortcutMode;
        }
      }
      if (appearanceInput !== null && appearanceInput !== undefined) {
        if (typeof appearanceInput !== 'object' || Array.isArray(appearanceInput))
          throw new Error('外观设置无效');
        if (appearanceInput.themeMode !== undefined && appearanceInput.themeMode !== null) {
          if (!Number.isInteger(appearanceInput.themeMode) || appearanceInput.themeMode < 0 || appearanceInput.themeMode >= 3)
            throw new Error('主题模式无效');
          payload.themeMode = appearanceInput.themeMode;
        }
        if (appearanceInput.accentPreset !== undefined && appearanceInput.accentPreset !== null) {
          if (!Number.isInteger(appearanceInput.accentPreset) || appearanceInput.accentPreset < 0 || appearanceInput.accentPreset >= 11)
            throw new Error('强调色预设无效');
          payload.accentPreset = appearanceInput.accentPreset;
        }
        if (appearanceInput.uiFontFamily !== undefined && appearanceInput.uiFontFamily !== null)
          payload.uiFontFamily = text(appearanceInput.uiFontFamily, 128);
        if (appearanceInput.uiFontSize !== undefined && appearanceInput.uiFontSize !== null) {
          if (typeof appearanceInput.uiFontSize !== 'number' || !Number.isFinite(appearanceInput.uiFontSize) ||
              appearanceInput.uiFontSize < 8 || appearanceInput.uiFontSize > 24)
            throw new Error('主界面字号无效');
          payload.uiFontSize = appearanceInput.uiFontSize;
        }
        if (appearanceInput.chatFontFamily !== undefined && appearanceInput.chatFontFamily !== null)
          payload.chatFontFamily = text(appearanceInput.chatFontFamily, 128);
        if (appearanceInput.chatFontSize !== undefined && appearanceInput.chatFontSize !== null) {
          if (typeof appearanceInput.chatFontSize !== 'number' || !Number.isFinite(appearanceInput.chatFontSize) ||
              appearanceInput.chatFontSize < 8 || appearanceInput.chatFontSize > 26)
            throw new Error('聊天框字号无效');
          payload.chatFontSize = appearanceInput.chatFontSize;
        }
      }
      const result = await service.request('save-settings', payload);
      if (result && !result.overridePath) {
        windowBackground = appearance.surfaceHex(18, 18, 18, appearance.normalizeThemeMode(result.themeMode));
        traySettings = {
          keepAliveInTray: result.keepAliveInTray !== false,
          trayPreview: result.trayPreview !== false
        };
        trayManager?.setSettings(traySettings);
        forward({ event: 'settings-updated', data: result });
      }
      return result;
    }, true);
    handle('select-settings-directory', async kind => {
      if (kind !== 'downloads' && kind !== 'history') throw new Error('目录类型无效');
      const title = kind === 'downloads' ? '选择文件接收目录' : '选择聊天记录目录';
      const hostWindow = settingsWindow && !settingsWindow.isDestroyed() ? settingsWindow : window;
      const selection = await dialog.showOpenDialog(hostWindow, { title, properties: ['openDirectory'] });
      return selection.canceled ? null : selection.filePaths[0];
    }, true);
    handle('clear-all-histories', () => service.request('clear-all-histories'), true);
    handle('probe-ip', ip => {
      if (!isIpv4(ip)) throw new Error('无效的 IPv4 地址');
      return service.request('probe-ip', { ip: ip.trim() });
    }, true);
    handle('send-text', (chat, message) => service.request('text', { chat: text(chat, 256), text: text(message, 32768) }));
    handle('select-files', async chat => {
      fileRecipients(chat);
      const selection = await dialog.showOpenDialog(window, {
        title: '选择要发送的文件', properties: ['openFile', 'multiSelections']
      });
      return selection.canceled ? [] : sendFiles(chat, selection.filePaths);
    });
    handle('drop-files', sendFiles);
    handle('emoji-pack', async () => {
      // 新版表情随 lanchat-bin/lanchat.dll 编译，旧版开发目录仍保留文件回退。
      if (Array.isArray(emojiPackCache) && emojiPackCache.length) return emojiPackCache;
      let embedded = [];
      try { embedded = await service.request('emoji-pack'); } catch { embedded = []; }
      if (Array.isArray(embedded) && embedded.length) {
        emojiPackCache = embedded;
        return embedded;
      }
      const fallback = await readEmojiPack();
      if (fallback.length) emojiPackCache = fallback;
      return fallback;
    });
    handle('stage-paste', (...args) => trackPasteOperation(stagePastedFiles(...args)));
    handle('discard-paste', ids => {
      if (!Array.isArray(ids)) throw new Error('附件列表无效');
      return trackPasteOperation(discardPastedFiles(ids));
    });
    handle('send-paste', (...args) => trackPasteOperation(sendPastedFiles(...args)));
    handle('stage-drop', async paths => {
      const files = await validateLocalFiles(paths);
      const token = randomUUID();
      if (pendingDrops.size >= 10) pendingDrops.delete(pendingDrops.keys().next().value);
      pendingDrops.set(token, files);
      return token;
    });
    handle('confirm-drop', async (chat, token) => {
      const key = text(token, 128);
      const files = pendingDrops.get(key);
      pendingDrops.delete(key);
      if (!files || !chat) return [];
      return dispatchFiles(chat, files);
    });
    handle('trigger-screenshot', triggerScreenshot);
    handle('delete-session', (chat, removePeer) => {
      if (typeof removePeer !== 'boolean') throw new Error('删除参数无效');
      return service.request('delete-session', { chat: text(chat, 256), removePeer });
    });
    handle('open-path', openPath);
    handle('open-downloads', async () => {
      const current = await service.request('settings');
      const directory = text(current.downloadDirectory, 4096);
      if (!isAbsolute(directory)) throw new Error('文件接收目录无效');
      await mkdir(directory, { recursive: true });
      await reveal(await realpath(directory));
    }, true);
    handle('open-history', async () => {
      const current = await service.request('settings');
      const directory = text(current.historyDirectory, 4096);
      if (!isAbsolute(directory)) throw new Error('聊天记录目录无效');
      await mkdir(directory, { recursive: true });
      await reveal(await realpath(directory));
    }, true);
    handle('quit', () => { setImmediate(() => app.quit()); });
    startService();
    showWindow();
  }).catch(error => { console.error(error); app.quit(); });
}
