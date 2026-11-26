const { contextBridge, ipcRenderer, webUtils } = require('electron');

contextBridge.exposeInMainWorld('lanchat', {
  state: () => ipcRenderer.invoke('lanchat:state'),
  history: chat => ipcRenderer.invoke('lanchat:history', chat),
  peers: () => ipcRenderer.invoke('lanchat:peers'),
  refresh: () => ipcRenderer.invoke('lanchat:refresh'),
  probeIp: ip => ipcRenderer.invoke('lanchat:probe-ip', ip),
  reconnect: () => ipcRenderer.invoke('lanchat:reconnect'),
  settings: () => ipcRenderer.invoke('lanchat:settings'),
  openSettings: () => ipcRenderer.invoke('lanchat:open-settings'),
  closeSettings: () => ipcRenderer.invoke('lanchat:close-settings'),
  systemInfo: () => ipcRenderer.invoke('lanchat:system-info'),
  syncUnreadState: state => ipcRenderer.send('lanchat:unread-state', state),
  saveSettings: (...args) => ipcRenderer.invoke('lanchat:save-settings', ...args),
  selectSettingsDirectory: kind => ipcRenderer.invoke('lanchat:select-settings-directory', kind),
  sendText: (chat, text) => ipcRenderer.invoke('lanchat:send-text', chat, text),
  emojiPack: () => ipcRenderer.invoke('lanchat:emoji-pack'),
  sendPastedFiles: (chat, ids) => ipcRenderer.invoke('lanchat:send-paste', chat, ids),
  discardPastedFiles: ids => ipcRenderer.invoke('lanchat:discard-paste', ids),
  selectFiles: chat => ipcRenderer.invoke('lanchat:select-files', chat),
  confirmDrop: (chat, token) => ipcRenderer.invoke('lanchat:confirm-drop', chat, token),
  deleteSession: (chat, removePeer) => ipcRenderer.invoke('lanchat:delete-session', chat, removePeer),
  clearAllHistories: () => ipcRenderer.invoke('lanchat:clear-all-histories'),
  openPath: (target, kind) => ipcRenderer.invoke('lanchat:open-path', target, kind),
  openDownloads: () => ipcRenderer.invoke('lanchat:open-downloads'),
  openHistory: () => ipcRenderer.invoke('lanchat:open-history'),
  triggerScreenshot: () => ipcRenderer.invoke('lanchat:trigger-screenshot'),
  checkUpdate: () => ipcRenderer.invoke('lanchat:check-update'),
  downloadUpdate: () => ipcRenderer.invoke('lanchat:download-update'),
  openUpdateLocation: path => ipcRenderer.invoke('lanchat:open-update-location', path),
  openReleasePage: url => ipcRenderer.invoke('lanchat:open-release-page', url),
  quit: () => { void ipcRenderer.invoke('lanchat:quit'); },
  onEvent: callback => { ipcRenderer.on('lanchat:event', (_event, frame) => callback(frame)); }
});

// Chromium 可能只向输入框公开文件路径文本。遇到这类粘贴时由主进程读取原始文件格式。
window.addEventListener('paste', event => {
  const target = event.target;
  const data = event.clipboardData;
  const chat = target.dataset?.draftChat;
  if (!event.isTrusted || target.id !== 'message' || !chat || !data || target.disabled) return;
  const paths = Array.from(data.files, file => webUtils.getPathForFile(file)).filter(Boolean);
  // 只有 PNG/JPEG 能走内存直传。其他图像由主进程查找系统剪贴板提供的兼容格式。
  const image = Array.from(data.files).find(file =>
    (file.type === 'image/png' || file.type === 'image/jpeg') && !webUtils.getPathForFile(file)
  );
  const plain = data.getData('text/plain');
  const fileFormat = data.types.includes('text/uri-list') || data.types.includes('x-special/gnome-copied-files');
  if (!paths.length && !image && !fileFormat && plain && !/^\s*(?:file:\/|\/)/m.test(plain)) return;
  event.preventDefault();
  const initialValue = target.value;
  const start = target.selectionStart;
  const end = target.selectionEnd;
  const stage = async () => {
    if (paths.length || fileFormat || !image) return ipcRenderer.invoke('lanchat:stage-paste', paths, null, !plain);
    if (image.size > 64 * 1024 * 1024) throw new Error('剪贴板图片超过 64 MiB');
    return ipcRenderer.invoke('lanchat:stage-paste', paths, await image.arrayBuffer(), true);
  };
  stage().then(files => {
    if (files.length) {
      target.dispatchEvent(new CustomEvent('files-pasted', { detail: JSON.stringify({ chat, files }) }));
      return;
    }
    // 文件格式缺失时仍允许粘贴普通路径文本，避免把它自动转换成附件。
    if (!plain || target.dataset.draftChat !== chat) return;
    const from = target.value === initialValue ? start : target.selectionStart;
    const to = target.value === initialValue ? end : target.selectionEnd;
    const room = Math.max(0, target.maxLength - target.value.length + to - from);
    target.setRangeText(plain.replace(/\r\n?/g, '\n').slice(0, room), from, to, 'end');
    target.dispatchEvent(new Event('input', { bubbles: true }));
  }, error => target.dispatchEvent(new CustomEvent('paste-error', { detail: error.message })));
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
