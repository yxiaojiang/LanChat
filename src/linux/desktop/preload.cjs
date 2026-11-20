const { contextBridge, ipcRenderer, webUtils } = require('electron');

contextBridge.exposeInMainWorld('lanchat', {
  state: () => ipcRenderer.invoke('lanchat:state'),
  history: chat => ipcRenderer.invoke('lanchat:history', chat),
  peers: () => ipcRenderer.invoke('lanchat:peers'),
  refresh: () => ipcRenderer.invoke('lanchat:refresh'),
  sendText: (chat, text) => ipcRenderer.invoke('lanchat:send-text', chat, text),
  selectFiles: chat => ipcRenderer.invoke('lanchat:select-files', chat),
  quit: () => { void ipcRenderer.invoke('lanchat:quit'); },
  onEvent: callback => { ipcRenderer.on('lanchat:event', (_event, frame) => callback(frame)); }
});

// 只有真实的系统拖放事件能在隔离层把本地文件路径交给主进程。
window.addEventListener('dragover', event => event.preventDefault());
window.addEventListener('drop', event => {
  event.preventDefault();
  if (!event.isTrusted || !event.dataTransfer.files.length) return;
  const target = event.target.closest('[data-file-chat]');
  if (!target?.dataset.fileChat) return;
  const paths = Array.from(event.dataTransfer.files, file => webUtils.getPathForFile(file)).filter(Boolean);
  ipcRenderer.invoke('lanchat:drop-files', target.dataset.fileChat, paths).then(
    results => {
      const failure = results.find(item => !item.ok);
      if (failure) target.dispatchEvent(new CustomEvent('file-error', { detail: failure.error }));
    },
    error => target.dispatchEvent(new CustomEvent('file-error', { detail: error.message }))
  );
});
