const { app, BrowserWindow, dialog, ipcMain, session } = require('electron');
const { realpath, stat } = require('node:fs/promises');
const { isAbsolute, join } = require('node:path');
const { pathToFileURL } = require('node:url');
const Service = require('./service.cjs');

const page = join(__dirname, 'index.html');
const pageUrl = pathToFileURL(page).href;
let window;
let service;
let exiting = false;
let stopped = false;

function forward(event) {
  if (window && !window.isDestroyed()) window.webContents.send('lanchat:event', event);
}

function startService() {
  const child = new Service(join(process.resourcesPath, 'lanchat-bin'));
  service = child;
  child.on('event', event => {
    if (service === child) forward(event);
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

async function sendFiles(chat, paths) {
  fileRecipient(chat);
  if (!Array.isArray(paths) || paths.length > 32) throw new Error('一次最多发送 32 个文件');
  const files = await Promise.all(paths.map(async path => {
    if (!isAbsolute(text(path, 4096))) throw new Error('请选择本地文件');
    const file = await realpath(path);
    if (!(await stat(file)).isFile()) throw new Error('只能发送普通文件');
    return file;
  }));
  const results = [];
  for (const path of files) {
    try {
      await service.request('file', { chat, path });
      results.push({ ok: true });
    } catch (error) {
      results.push({ ok: false, error: error.message });
    }
  }
  return results;
}

if (!app.requestSingleInstanceLock()) {
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
    handle('history', chat => service.request('history', { chat: text(chat, 256) }));
    handle('peers', () => service.request('peers'));
    handle('refresh', () => service.request('refresh'));
    handle('send-text', (chat, message) => service.request('text', { chat: text(chat, 256), text: text(message, 32768) }));
    handle('select-files', async chat => {
      fileRecipient(chat);
      const selection = await dialog.showOpenDialog(window, {
        title: '选择要发送的文件', properties: ['openFile', 'multiSelections']
      });
      return selection.canceled ? [] : sendFiles(chat, selection.filePaths);
    });
    handle('drop-files', sendFiles);
    handle('quit', () => { setImmediate(() => app.quit()); });
    startService();
    showWindow();
  }).catch(error => { console.error(error); app.quit(); });
}
