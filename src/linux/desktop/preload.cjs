const { contextBridge, ipcRenderer, webUtils } = require('electron');

// 粘贴适配器只依赖 DOM 通用能力，不读取 textarea 专属的 value/selectionStart/setRangeText。
const pasteAdapter = {
  isBlock(node) {
    return new Set(['ADDRESS', 'ARTICLE', 'ASIDE', 'BLOCKQUOTE', 'DD', 'DIV', 'DL', 'DT', 'FIELDSET', 'FIGURE', 'FOOTER', 'FORM', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'HEADER', 'HR', 'LI', 'MAIN', 'NAV', 'OL', 'P', 'PRE', 'SECTION', 'TABLE', 'TR', 'UL']).has(String(node?.tagName || '').toUpperCase());
  },
  isRichTarget(target) {
    return Boolean(target?.isContentEditable || target?.dataset?.composerInput === 'true');
  },
  textOf(node) {
    if (!node) return '';
    if (node.dataset?.emojiToken) return node.dataset.emojiToken;
    if (node.nodeType === 3) return node.nodeValue || '';
    if (String(node.tagName || '').toUpperCase() === 'BR') return '\n';
    let result = '';
    for (const [index, child] of Array.from(node.childNodes || []).entries()) {
      if (index > 0 && pasteAdapter.isBlock(child) && result && !result.endsWith('\n')) result += '\n';
      result += pasteAdapter.textOf(child);
    }
    return result;
  },
  readText(target) {
    return pasteAdapter.isRichTarget(target) ? pasteAdapter.textOf(target) : String(target?.value || '');
  },
  selection(target) {
    if (!pasteAdapter.isRichTarget(target)) {
      return [target?.selectionStart ?? pasteAdapter.readText(target).length, target?.selectionEnd ?? pasteAdapter.readText(target).length];
    }
    const documentRef = target?.ownerDocument || (typeof document !== 'undefined' ? document : null);
    const selection = documentRef?.getSelection?.();
    const range = selection?.rangeCount ? selection.getRangeAt(0) : null;
    if (!range || !target.contains?.(range.startContainer)) return [pasteAdapter.readText(target).length, pasteAdapter.readText(target).length];
    const offset = (container, position) => {
      let total = 0;
      let lastWasBreak = false;
      const addBoundary = (child, index) => {
        if (index > 0 && pasteAdapter.isBlock(child) && total > 0 && !lastWasBreak) {
          total += 1;
          lastWasBreak = true;
        }
      };
      const consume = node => {
        if (node?.nodeType === 3) {
          const text = node.nodeValue || '';
          total += text.length;
          lastWasBreak = text.endsWith('\n');
          return;
        }
        if (node?.dataset?.emojiToken) {
          total += node.dataset.emojiToken.length;
          lastWasBreak = false;
          return;
        }
        if (String(node?.tagName || '').toUpperCase() === 'BR') {
          total += 1;
          lastWasBreak = true;
          return;
        }
        for (const [index, child] of Array.from(node?.childNodes || []).entries()) {
          addBoundary(child, index);
          consume(child);
        }
      };
      const visit = node => {
        if (node === container) {
          if (node.nodeType === 3) {
            const text = node.nodeValue || '';
            total += Math.min(position, text.length);
            lastWasBreak = text.slice(0, position).endsWith('\n');
          } else if (String(node.tagName || '').toUpperCase() === 'BR') {
            total += Math.min(position, 1);
            lastWasBreak = position > 0;
          } else if (node.dataset?.emojiToken) {
            total += Math.min(position, node.dataset.emojiToken.length);
            lastWasBreak = false;
          } else {
            const children = Array.from(node.childNodes || []);
            for (const [index, child] of children.slice(0, position).entries()) {
              addBoundary(child, index);
              consume(child);
            }
          }
          return true;
        }
        if (node.dataset?.emojiToken) { total += node.dataset.emojiToken.length; lastWasBreak = false; return false; }
        if (node.nodeType === 3) { consume(node); return false; }
        if (String(node.tagName || '').toUpperCase() === 'BR') { total += 1; lastWasBreak = true; return false; }
        for (const [index, child] of Array.from(node.childNodes || []).entries()) {
          addBoundary(child, index);
          if (visit(child)) return true;
        }
        return false;
      };
      visit(target);
      return total;
    };
    return [offset(range.startContainer, range.startOffset), offset(range.endContainer, range.endOffset)];
  },
  maxLength(target) {
    const value = Number(target?.maxLength ?? target?.dataset?.maxLength ?? 32768);
    return Number.isFinite(value) && value > 0 ? value : 32768;
  },
  insertText(target, text, start, end) {
    const value = pasteAdapter.readText(target);
    const range = [start, end].every(Number.isFinite) ? [start, end] : pasteAdapter.selection(target);
    const available = Math.max(0, pasteAdapter.maxLength(target) - value.length + range[1] - range[0]);
    const inserted = String(text || '').replace(/\r\n?/g, '\n').slice(0, available);
    if (!pasteAdapter.isRichTarget(target)) {
      target.setRangeText(inserted, range[0], range[1], 'end');
      pasteAdapter.dispatchInput(target);
      return inserted;
    }
    const documentRef = target.ownerDocument || (typeof document !== 'undefined' ? document : null);
    const selection = documentRef?.getSelection?.();
    const domRange = selection?.rangeCount ? selection.getRangeAt(0) : null;
    if (!domRange || !target.contains?.(domRange.startContainer)) return '';
    domRange.deleteContents();
    domRange.insertNode(documentRef.createTextNode(inserted));
    domRange.collapse(false);
    selection.removeAllRanges();
    selection.addRange(domRange);
    pasteAdapter.dispatchInput(target);
    return inserted;
  },
  dispatchInput(target) {
    const EventCtor = target?.ownerDocument?.defaultView?.Event || (typeof Event === 'function' ? Event : null);
    target?.dispatchEvent?.(EventCtor ? new EventCtor('input', { bubbles: true }) : { type: 'input', bubbles: true });
  }
};

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
  readClipboardText: () => ipcRenderer.invoke('lanchat:read-clipboard-text'),
  stageClipboard: () => ipcRenderer.invoke('lanchat:stage-clipboard'),
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
  const isComposer = target?.id === 'message' || target?.dataset?.composerInput === 'true';
  const isDisabled = target?.disabled || target?.inert || target?.dataset?.disabled === 'true';
  if (!event.isTrusted || !isComposer || !chat || !data || isDisabled) return;
  const paths = Array.from(data.files, file => webUtils.getPathForFile(file)).filter(Boolean);
  // 只有 PNG/JPEG 能走内存直传。其他图像由主进程查找系统剪贴板提供的兼容格式。
  const image = Array.from(data.files).find(file =>
    (file.type === 'image/png' || file.type === 'image/jpeg') && !webUtils.getPathForFile(file)
  );
  const plain = data.getData('text/plain');
  const fileFormat = data.types.includes('text/uri-list') || data.types.includes('x-special/gnome-copied-files');
  if (!paths.length && !image && !fileFormat && plain && !/^\s*(?:file:\/|\/)/m.test(plain)) return;
  event.preventDefault();
  const initialValue = pasteAdapter.readText(target);
  const [start, end] = pasteAdapter.selection(target);
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
    const current = pasteAdapter.readText(target);
    const [currentStart, currentEnd] = pasteAdapter.selection(target);
    const from = current === initialValue ? start : currentStart;
    const to = current === initialValue ? end : currentEnd;
    pasteAdapter.insertText(target, plain, from, to);
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
