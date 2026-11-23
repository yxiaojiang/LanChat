const { app, BrowserWindow, dialog, ipcMain, session, shell } = require('electron');
const { randomUUID } = require('node:crypto');
const { mkdir, realpath, stat } = require('node:fs/promises');
const { dirname, isAbsolute, join, resolve } = require('node:path');
const { pathToFileURL } = require('node:url');
const Service = require('./service.cjs');

const page = join(__dirname, 'index.html');
const pageUrl = pathToFileURL(page).href;
const allowedFilePaths = new Set();
const pendingDrops = new Map();
let window;
let service;
let restarting;
let exiting = false;
let stopped = false;

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
    if (event?.event === 'message') recordMessagePaths(event.data);
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

function handle(name, operation) {
  ipcMain.handle('lanchat:' + name, async (event, ...args) => {
    if (event.sender !== window?.webContents || event.senderFrame !== event.sender.mainFrame ||
        event.senderFrame.url !== pageUrl || exiting) throw new Error('无效的桌面请求');
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
  if (!Array.isArray(paths) || !paths.length || paths.length > 32)
    throw new Error('一次最多发送 32 个文件');
  return Promise.all(paths.map(async path => {
    if (!isAbsolute(text(path, 4096))) throw new Error('请选择本地文件');
    const file = await realpath(path);
    if (!(await stat(file)).isFile()) throw new Error('只能发送普通文件');
    return file;
  }));
}

async function dispatchFiles(chat, files) {
  fileRecipient(chat);
  const results = [];
  for (const path of files) {
    try {
      await service.request('file', { chat, path });
      allowedFilePaths.add(resolve(path));
      results.push({ ok: true });
    } catch (error) {
      results.push({ ok: false, error: error.message });
    }
  }
  return results;
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
  module.exports = { openPath, recordMessagePaths, allowedFilePaths };
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
    service.stop().finally(() => {
      stopped = true;
      app.quit();
    });
  });
  app.whenReady().then(() => {
    session.defaultSession.setPermissionRequestHandler((_contents, _permission, decide) => decide(false));
    session.defaultSession.setPermissionCheckHandler(() => false);
    session.defaultSession.on('will-download', event => event.preventDefault());
    handle('state', () => service.request('state'));
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
    handle('settings', () => service.request('settings'));
    handle('save-settings', (nickname, autoAcceptFiles, downloadDirectory, confirmOverride, historyDirectory) => {
      if (typeof autoAcceptFiles !== 'boolean' || typeof confirmOverride !== 'boolean')
        throw new Error('设置值无效');
      if (downloadDirectory !== null && !isAbsolute(text(downloadDirectory, 4096)))
        throw new Error('文件接收目录无效');
      if (historyDirectory !== null && !isAbsolute(text(historyDirectory, 4096)))
        throw new Error('聊天记录目录无效');
      return service.request('save-settings', {
        nickname: text(nickname, 80), autoAcceptFiles, downloadDirectory, confirmOverride, historyDirectory
      });
    });
    handle('select-settings-directory', async kind => {
      if (kind !== 'downloads' && kind !== 'history') throw new Error('目录类型无效');
      const title = kind === 'downloads' ? '选择文件接收目录' : '选择聊天记录目录';
      const selection = await dialog.showOpenDialog(window, { title, properties: ['openDirectory'] });
      return selection.canceled ? null : selection.filePaths[0];
    });
    handle('send-text', (chat, message) => service.request('text', { chat: text(chat, 256), text: text(message, 32768) }));
    handle('select-files', async chat => {
      fileRecipient(chat);
      const selection = await dialog.showOpenDialog(window, {
        title: '选择要发送的文件', properties: ['openFile', 'multiSelections']
      });
      return selection.canceled ? [] : sendFiles(chat, selection.filePaths);
    });
    handle('drop-files', sendFiles);
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
    });
    handle('quit', () => { setImmediate(() => app.quit()); });
    startService();
    showWindow();
  }).catch(error => { console.error(error); app.quit(); });
}
