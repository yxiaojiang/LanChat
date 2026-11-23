const { contextBridge, ipcRenderer, webUtils } = require('electron');

contextBridge.exposeInMainWorld('lanchat', {
  state: () => ipcRenderer.invoke('lanchat:state'),
  history: chat => ipcRenderer.invoke('lanchat:history', chat),
  peers: () => ipcRenderer.invoke('lanchat:peers'),
  refresh: () => ipcRenderer.invoke('lanchat:refresh'),
  reconnect: () => ipcRenderer.invoke('lanchat:reconnect'),
  settings: () => ipcRenderer.invoke('lanchat:settings'),
  saveSettings: (...args) => ipcRenderer.invoke('lanchat:save-settings', ...args),
  selectSettingsDirectory: kind => ipcRenderer.invoke('lanchat:select-settings-directory', kind),
  sendText: (chat, text) => ipcRenderer.invoke('lanchat:send-text', chat, text),
  selectFiles: chat => ipcRenderer.invoke('lanchat:select-files', chat),
  confirmDrop: (chat, token) => ipcRenderer.invoke('lanchat:confirm-drop', chat, token),
  deleteSession: (chat, removePeer) => ipcRenderer.invoke('lanchat:delete-session', chat, removePeer),
  openPath: (target, kind) => ipcRenderer.invoke('lanchat:open-path', target, kind),
  openDownloads: () => ipcRenderer.invoke('lanchat:open-downloads'),
  quit: () => { void ipcRenderer.invoke('lanchat:quit'); },
  onEvent: callback => { ipcRenderer.on('lanchat:event', (_event, frame) => callback(frame)); }
});

// 只有真实的系统拖放事件能在隔离层把本地文件路径交给主进程。
window.addEventListener('dragover', event => event.preventDefault());
window.addEventListener('drop', event => {
  event.preventDefault();
  if (!event.isTrusted || !event.dataTransfer.files.length) return;
  const target = event.target.closest('[data-file-chat]');
  const chat = target?.dataset.fileChat;
  if (!chat) return;
  if (chat === 'OFFLINE') {
    target.dispatchEvent(new CustomEvent('file-error', { detail: '好友当前离线，暂时无法发送文件' }));
    return;
  }
  const paths = Array.from(event.dataTransfer.files, file => webUtils.getPathForFile(file)).filter(Boolean);
  if (!paths.length) return;
  if (chat === 'BROADCAST') {
    ipcRenderer.invoke('lanchat:stage-drop', paths).then(
      token => target.dispatchEvent(new CustomEvent('file-drop', { detail: token })),
      error => target.dispatchEvent(new CustomEvent('file-error', { detail: error.message }))
    );
    return;
  }
  ipcRenderer.invoke('lanchat:drop-files', chat, paths).then(
    results => {
      const failure = results.find(item => !item.ok);
      if (failure) target.dispatchEvent(new CustomEvent('file-error', { detail: failure.error }));
    },
    error => target.dispatchEvent(new CustomEvent('file-error', { detail: error.message }))
  );
});
