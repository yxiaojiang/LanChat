const { app, BrowserWindow, clipboard, dialog, ipcMain, nativeImage, screen, session, shell } = require('electron');
const { randomUUID } = require('node:crypto');
const { mkdir, open, readFile, realpath, stat, unlink } = require('node:fs/promises');
const os = require('node:os');
const { basename, dirname, isAbsolute, join, resolve } = require('node:path');
const { fileURLToPath, pathToFileURL } = require('node:url');
const appearance = require('./appearance.js');
const Service = require('./service.cjs');

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
const maxFiles = 32;
const maxPastedImageBytes = 64 * 1024 * 1024;
let window;
let settingsWindow;
let service;
let restarting;
let exiting = false;
let stopped = false;
let windowBackground = '#121212';

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
    window.once('ready-to-show', () => window.show());
    window.on('close', event => {
      if (stopped) return;
      event.preventDefault();
      app.quit();
    });
    window.loadFile(page).catch(error => {
      console.error(error);
      app.quit();
    });
  } else {
    if (window.isMinimized()) window.restore();
    window.show();
    window.focus();
  }
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

function openSettingsWindow() {
  if (settingsWindow && !settingsWindow.isDestroyed()) {
    if (settingsWindow.isMinimized()) settingsWindow.restore();
    settingsWindow.show();
    settingsWindow.focus();
    return;
  }
  const hasParent = window && !window.isDestroyed();
  const parentBounds = hasParent ? window.getBounds() : null;
  const display = parentBounds ? screen.getDisplayMatching(parentBounds) : screen.getPrimaryDisplay();
  const bounds = computeSettingsBounds(display.workArea, parentBounds);
  const minSize = computeSettingsMinSize(bounds);
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
    const fromMain = event.sender === window?.webContents && event.senderFrame === event.sender.mainFrame &&
      event.senderFrame.url === pageUrl;
    const fromSettings = allowSettings && event.sender === settingsWindow?.webContents &&
      event.senderFrame === event.sender.mainFrame && event.senderFrame.url === settingsPageUrl;
    if ((!fromMain && !fromSettings) || exiting) throw new Error('无效的桌面请求');
    return operation(...args);
  });
}

function text(value, limit) {
  if (typeof value !== 'string' || !value.trim() || value.length > limit)
    throw new Error('输入内容无效');
  return value;
}

function fileRecipient(chat) {
  if (chat === 'BROADCAST') throw new Error('请选择文件接收节点');
  return text(chat, 256);
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
  fileRecipient(chat);
  const child = service;
  const peer = (await child.request('peers')).find(item => item.id === chat);
  if (exiting) throw new Error('应用正在退出');
  if (service !== child || child.disconnected) throw new Error('通信服务已断开，请重新连接后发送');
  if (!peer) throw new Error('好友当前离线，暂时无法发送文件');
  const batch = files.map(file => ({ file, progress: {
    event: 'progress', operation: randomUUID(), chat: peer.key, peerNick: peer.nick,
    name: file.name, total: file.size, sending: true
  } }));
  // 先显示整批的待发送气泡，仍沿用逐个发送，避免一次打开多条大文件流。
  for (const { progress } of batch) forward({ ...progress, queued: true, received: 0 });
  const send = async () => {
    const results = [];
    for (const { file, progress } of batch) {
      const { path, managed } = file;
      try {
        if (exiting) throw new Error('应用正在退出');
        if (managed) sendingPastes.set(path, file);
        const result = await child.request('file', managed ? { chat, path, managedPaste: true } : { chat, path }, progress.operation);
        if (managed && result?.historySaved === true) file.saved = true;
        if (!managed || file.saved) allowedFilePaths.add(resolve(path));
        results.push({ ok: true });
      } catch (error) {
        // 文件在排队时被删除等失败可能发生在通信服务发出首帧之前。
        if (service === child && !child.disconnected)
          forward({ ...progress, queued: false, failed: true, error: error.message });
        results.push({ ok: false, error: error.message });
      } finally {
        if (managed) {
          // 请求可能先被断开，管道仍有已经落盘的消息事件，等子进程关闭后再清理。
          // 子进程异常退出时无法确认落盘与回执之间的状态，保留可能已被引用的图片。
          const canDelete = !sendingPastes.has(path) || !child.disconnected || await child.stop();
          sendingPastes.delete(path);
          if (canDelete) await deleteOwnedPaste(file);
        }
      }
    }
    return results;
  };
  const result = send();
  if (waitForResult) return result;
  void trackPasteOperation(result).catch(error => forward({ event: 'file-error', data: error.message }));
}

async function sendPastedFiles(chat, ids) {
  if (!Array.isArray(ids) || !ids.length || ids.length > maxFiles || new Set(ids).size !== ids.length)
    throw new Error(`一次最多发送 ${maxFiles} 个文件`);
  const files = ids.map(id => pendingPastes.get(id));
  if (files.some(file => !file)) throw new Error('粘贴的文件已不可用，请重新粘贴');
  for (const id of ids) pendingPastes.delete(id);
  try {
    await dispatchFiles(chat, files, false);
  } catch (error) {
    // 退出后不能重新登记草稿，预检已接管的图片在这里结算。
    if (exiting) await Promise.all(files.map(deleteOwnedPaste));
    else ids.forEach((id, i) => pendingPastes.set(id, files[i]));
    throw error;
  }
}

async function sendFiles(chat, paths) {
  fileRecipient(chat);
  return dispatchFiles(chat, await validateLocalFiles(paths));
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
    readClipboardPaths, stagePastedFiles, discardPastedFiles, readEmojiPack };
} else if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  app.enableSandbox();
  app.on('second-instance', () => { if (app.isReady() && !exiting) showWindow(); });
  app.on('before-quit', event => {
    if (stopped || !service) return;
    event.preventDefault();
    if (exiting) return;
    exiting = true;
    Promise.allSettled([
      service.stop(), discardPastedFiles([...pendingPastes.keys()]), ...pasteOperations
    ]).finally(() => {
      stopped = true;
      app.quit();
    });
  });
  app.whenReady().then(() => {
    session.defaultSession.setPermissionRequestHandler((_contents, _permission, decide) => decide(false));
    session.defaultSession.setPermissionCheckHandler(() => false);
    session.defaultSession.on('will-download', event => event.preventDefault());
    handle('state', () => service.request('state'), true);
    handle('history', async chat => {
      const result = await service.request('history', { chat: text(chat, 256) });
      for (const item of result?.messages ?? []) recordMessagePaths(item);
      return result;
    });
    handle('peers', () => service.request('peers'));
    handle('refresh', () => service.request('refresh'));
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
      }
      return current;
    }, true);
    handle('save-settings', async (nickname, autoAcceptFiles, downloadDirectory, confirmOverride, historyDirectory, appearanceInput = null) => {
      if (typeof autoAcceptFiles !== 'boolean' || typeof confirmOverride !== 'boolean')
        throw new Error('设置值无效');
      if (downloadDirectory !== null && !isAbsolute(text(downloadDirectory, 4096)))
        throw new Error('文件接收目录无效');
      if (historyDirectory !== null && !isAbsolute(text(historyDirectory, 4096)))
        throw new Error('聊天记录目录无效');
      const payload = {
        nickname: text(nickname, 80), autoAcceptFiles, downloadDirectory, confirmOverride, historyDirectory
      };
      if (appearanceInput !== null && appearanceInput !== undefined) {
        if (typeof appearanceInput !== 'object' || Array.isArray(appearanceInput))
          throw new Error('外观设置无效');
        if (appearanceInput.themeMode !== undefined && appearanceInput.themeMode !== null) {
          if (!Number.isInteger(appearanceInput.themeMode) || appearanceInput.themeMode < 0 || appearanceInput.themeMode >= 3)
            throw new Error('主题模式无效');
          payload.themeMode = appearanceInput.themeMode;
        }
        if (appearanceInput.accentPreset !== undefined && appearanceInput.accentPreset !== null) {
          if (!Number.isInteger(appearanceInput.accentPreset) || appearanceInput.accentPreset < 0 || appearanceInput.accentPreset >= 5)
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
    handle('send-text', (chat, message) => service.request('text', { chat: text(chat, 256), text: text(message, 32768) }));
    handle('select-files', async chat => {
      fileRecipient(chat);
      const selection = await dialog.showOpenDialog(window, {
        title: '选择要发送的文件', properties: ['openFile', 'multiSelections']
      });
      return selection.canceled ? [] : sendFiles(chat, selection.filePaths);
    });
    handle('drop-files', sendFiles);
    handle('emoji-pack', async () => {
      // 新版表情随 lanchat-bin/lanchat.dll 编译，旧版开发目录仍保留文件回退。
      try { return await service.request('emoji-pack'); } catch { return readEmojiPack(); }
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
