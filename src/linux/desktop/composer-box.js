// Linux 输入框富文本适配器：默认保留原生 textarea，CP-2B 通过后才建议启用富文本模式。
(function (root, factory) {
  const api = factory(root);
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.LanChatComposerBox = api;
})(typeof globalThis === 'undefined' ? this : globalThis, function (root) {
  const TOKEN = /\[[^\[\]\s]{1,24}\]/g;
  const FALLBACK_KEY = 'lanchat.composer.native';
  function richModeStored(storage) {
    try { return storage?.getItem(FALLBACK_KEY) === '0'; } catch { return false; }
  }
  function createComposerBox(options = {}) {
    const native = options.native;
    const rich = options.rich;
    const doc = options.document || root.document;
    const storage = options.storage || root.localStorage;
    const maxLength = Number(options.maxLength || native?.maxLength || 32768);
    const listeners = new Map();
    let emojiImages = new Map();
    let useRich = Boolean(rich && richModeStored(storage));
    let isDisabled = Boolean(native?.disabled || rich?.inert || rich?.dataset?.disabled === 'true');
    let composing = false;
    let contextMenu = null;
    function active() { return useRich && rich ? rich : native; }
    function tokenLength(node) { return node?.dataset?.emojiToken?.length || 0; }
    function isBlock(node) {
      return new Set(['ADDRESS', 'ARTICLE', 'ASIDE', 'BLOCKQUOTE', 'DD', 'DIV', 'DL', 'DT', 'FIELDSET', 'FIGURE', 'FOOTER', 'FORM', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'HEADER', 'HR', 'LI', 'MAIN', 'NAV', 'OL', 'P', 'PRE', 'SECTION', 'TABLE', 'TR', 'UL']).has(String(node?.tagName || '').toUpperCase());
    }
    function nodeText(node) {
      if (!node) return '';
      if (node.nodeType === 3) return node.nodeValue || '';
      if (tokenLength(node)) return node.dataset.emojiToken;
      if (String(node.tagName || '').toUpperCase() === 'BR') return '\n';
      let result = '';
      for (const [index, child] of Array.from(node.childNodes || []).entries()) {
        if (index > 0 && isBlock(child) && result && !result.endsWith('\n')) result += '\n';
        result += nodeText(child);
      }
      return result;
    }
    function readText() { return useRich ? nodeText(rich) : String(native?.value || ''); }
    function clampText(text, start, end) {
      const room = Math.max(0, maxLength - readText().length + end - start);
      return String(text || '').replace(/\r\n?/g, '\n').slice(0, room);
    }
    function offsetOf(container, offset) {
      let total = 0;
      let found = false;
      let lastWasBreak = false;
      function addBoundary(child, index) {
        if (index > 0 && isBlock(child) && total > 0 && !lastWasBreak) {
          total += 1;
          lastWasBreak = true;
        }
      }
      function consume(node) {
        if (node?.nodeType === 3) {
          const text = node.nodeValue || '';
          total += text.length;
          lastWasBreak = text.endsWith('\n');
          return;
        }
        if (tokenLength(node)) {
          total += tokenLength(node);
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
      }
      function visit(node) {
        if (node === container) {
          if (node.nodeType === 3) {
            const text = node.nodeValue || '';
            total += Math.min(offset, text.length);
            lastWasBreak = text.slice(0, offset).endsWith('\n');
          } else if (String(node.tagName || '').toUpperCase() === 'BR') {
            total += Math.min(offset, 1);
            lastWasBreak = offset > 0;
          } else if (tokenLength(node)) {
            total += Math.min(offset, tokenLength(node));
            lastWasBreak = false;
          } else {
            const children = Array.from(node.childNodes || []);
            for (const [index, child] of children.slice(0, offset).entries()) {
              addBoundary(child, index);
              consume(child);
            }
          }
          found = true;
          return true;
        }
        if (node.nodeType === 3) { consume(node); return false; }
        if (tokenLength(node)) { consume(node); return false; }
        if (String(node.tagName || '').toUpperCase() === 'BR') { consume(node); return false; }
        for (const [index, child] of Array.from(node.childNodes || []).entries()) {
          addBoundary(child, index);
          if (visit(child)) return true;
        }
        return false;
      }
      visit(rich);
      return found ? total : readText().length;
    }
    function selection() {
      if (!useRich) return [native?.selectionStart ?? readText().length, native?.selectionEnd ?? readText().length];
      const range = doc?.getSelection?.()?.rangeCount ? doc.getSelection().getRangeAt(0) : null;
      if (!range || !rich.contains?.(range.startContainer)) return [readText().length, readText().length];
      return [offsetOf(range.startContainer, range.startOffset), offsetOf(range.endContainer, range.endOffset)];
    }
    function locate(node, target, state = { length: 0 }) {
      if (node.nodeType === 3) {
        const length = (node.nodeValue || '').length;
        if (target <= state.length + length) return [node, Math.max(0, target - state.length)];
        state.length += length;
        return null;
      }
      if (tokenLength(node)) {
        const parent = node.parentNode;
        const index = Array.from(parent?.childNodes || []).indexOf(node);
        if (target <= state.length) return [parent, Math.max(0, index)];
        if (target < state.length + tokenLength(node)) return [parent, target - state.length < tokenLength(node) / 2 ? index : index + 1];
        state.length += tokenLength(node);
        return null;
      }
      for (const child of Array.from(node.childNodes || [])) {
        const found = locate(child, target, state);
        if (found) return found;
      }
      return [node, (node.childNodes || []).length];
    }
    function setSelection(start, end = start) {
      if (!useRich) {
        native.focus?.();
        native.setSelectionRange?.(start, end);
        return;
      }
      const range = doc?.createRange?.();
      const selectionObject = doc?.getSelection?.();
      if (!range || !selectionObject) return;
      const begin = locate(rich, start);
      const finish = locate(rich, end);
      range.setStart(...begin);
      range.setEnd(...finish);
      selectionObject.removeAllRanges();
      selectionObject.addRange(range);
    }
    function emit(type, detail) {
      const event = typeof Event === 'function' ? new Event(type, { bubbles: true }) : { type, detail };
      if (detail !== undefined) event.detail = detail;
      active()?.dispatchEvent?.(event);
    }
    function render(text) {
      if (!rich || !doc?.createTextNode) return;
      rich.replaceChildren();
      let start = 0;
      for (const match of String(text).matchAll(TOKEN)) {
        const imageUrl = emojiImages.get(match[0]);
        if (!imageUrl) continue;
        if (match.index > start) rich.append(doc.createTextNode(text.slice(start, match.index)));
        const token = doc.createElement('span');
        token.dataset.emojiToken = match[0];
        token.className = 'composer-emoji';
        token.title = match[0];
        token.contentEditable = 'false';
        const image = doc.createElement('img');
        image.src = imageUrl;
        image.alt = '';
        image.draggable = false;
        token.append(image);
        rich.append(token);
        start = match.index + match[0].length;
      }
      if (start < text.length) rich.append(doc.createTextNode(text.slice(start)));
    }
    function setText(value, caret = String(value || '').length) {
      const text = String(value || '').slice(0, maxLength);
      if (useRich) {
        render(text);
        setSelection(caret, caret);
      } else if (native) {
        native.value = text;
        native.setSelectionRange?.(caret, caret);
      }
      if (native && rich) native.value = text;
    }
    function replaceSelection(text, start, end) {
      const range = Number.isFinite(start) && Number.isFinite(end) ? [start, end] : selection();
      const value = readText();
      const from = Math.max(0, Math.min(value.length, range[0]));
      const to = Math.max(from, Math.min(value.length, range[1]));
      const inserted = clampText(text, from, to);
      setText(value.slice(0, from) + inserted + value.slice(to), from + inserted.length);
      emit('input');
    }
    function deleteAdjacent(direction) {
      const [start, end] = selection();
      const value = readText();
      if (start !== end) return replaceSelection('');
      const index = direction < 0 ? start - 1 : start;
      if (index < 0 || index >= value.length) return;
      let tokenStart = -1;
      for (const match of value.matchAll(TOKEN)) {
        if (index >= match.index && index < match.index + match[0].length) { tokenStart = match.index; break; }
      }
      const from = tokenStart >= 0 ? tokenStart : direction < 0 ? start - 1 : start;
      const to = tokenStart >= 0 ? tokenStart + value.slice(tokenStart).match(TOKEN)[0].length : direction < 0 ? start : start + 1;
      setText(value.slice(0, from) + value.slice(to), from);
      emit('input');
    }
    function eventTargetEmoji(target) {
      let item = target;
      while (item && item !== rich) {
        if (tokenLength(item)) return item;
        item = item.parentNode;
      }
      return null;
    }
    function onMouseDown(event) {
      const token = useRich ? eventTargetEmoji(event.target) : null;
      if (!token) return;
      event.preventDefault();
      const start = offsetOf(token, 0);
      const rect = token.getBoundingClientRect?.();
      setSelection(event.clientX < (rect?.left + rect?.width / 2 || 0) ? start : start + tokenLength(token));
      active().focus?.();
    }
    function onKeyDown(event) {
      const imeKey = composing || event.isComposing || event.keyCode === 229;
      if (!useRich || imeKey) return;
      if (event.key === 'Backspace') { event.preventDefault(); deleteAdjacent(-1); }
      else if (event.key === 'Delete') { event.preventDefault(); deleteAdjacent(1); }
    }
    function onPaste(event) {
      // 文件/图片粘贴由 preload 适配器接管；普通文本交还浏览器，保留 IME 原生行为。
      if (!useRich || event.clipboardData?.files?.length) return;
    }
    function onCopy(event) {
      const [start, end] = selection();
      if (start === end) return;
      event.preventDefault();
      event.clipboardData?.setData('text/plain', readText().slice(start, end));
    }
    function onCut(event) {
      onCopy(event);
      const [start, end] = selection();
      if (start !== end) replaceSelection('');
    }
    function bind(element) {
      if (!element) return;
      element.addEventListener('mousedown', onMouseDown);
      element.addEventListener('keydown', onKeyDown);
      element.addEventListener('paste', onPaste);
      element.addEventListener('copy', onCopy);
      element.addEventListener('cut', onCut);
      element.addEventListener('compositionstart', () => { composing = true; });
      element.addEventListener('compositionend', () => { composing = false; });
      element.addEventListener('contextmenu', event => {
        event.preventDefault();
        contextMenu?.(element, { x: event.clientX, y: event.clientY });
      });
    }
    function addListener(type, listener) {
      const wrapped = event => {
        if (event.target && event.target !== active()) return;
        listener(event);
      };
      listeners.set(listener, wrapped);
      native?.addEventListener(type, wrapped);
      rich?.addEventListener(type, wrapped);
    }
    function syncDisabled(value) {
      isDisabled = Boolean(value);
      if (native) native.disabled = isDisabled;
      if (rich) {
        rich.inert = isDisabled;
        rich.contentEditable = isDisabled ? 'false' : 'true';
        rich.dataset.disabled = String(isDisabled);
      }
    }
    function syncDraftChat(value) {
      if (value === undefined) return;
      if (native) native.dataset.draftChat = value;
      if (rich) rich.dataset.draftChat = value;
    }
    function syncVisibility() {
      if (native && rich) {
        rich.hidden = !useRich;
        native.hidden = useRich;
      }
    }
    function toggleRich() {
      const value = readText();
      const draftChat = native?.dataset?.draftChat ?? rich?.dataset?.draftChat;
      useRich = Boolean(rich && !useRich);
      if (useRich) render(value);
      syncVisibility();
      syncDraftChat(draftChat);
      syncDisabled(isDisabled);
      if (native && rich) native.value = value;
      try { storage?.setItem(FALLBACK_KEY, useRich ? '0' : '1'); } catch { /* 无持久化权限时仍可临时切换 */ }
      setText(value);
      active().focus?.();
      emit('composer-mode', { rich: useRich });
      return useRich;
    }
    function proxy() {
      const dataset = new Proxy({}, {
        get: (_target, key) => active()?.dataset?.[key],
        set: (_target, key, value) => { syncDraftChat(key === 'draftChat' ? value : active()?.dataset?.draftChat); if (key !== 'draftChat') { if (native) native.dataset[key] = String(value); if (rich) rich.dataset[key] = String(value); } return true; }
      });
      const methods = {
        addEventListener: addListener,
        setRangeText: replaceSelection,
        insertText: replaceSelection,
        setEmojiImages(entries) { emojiImages = new Map(entries || []); if (useRich) { const range = selection(); render(readText()); setSelection(...range); } },
        setContextMenu(handler) { contextMenu = handler; },
        toggleRich,
        isRich: () => useRich,
        focus: () => active().focus?.(),
        dispatchEvent: event => active().dispatchEvent(event),
        get value() { return readText(); },
        set value(value) { setText(value); },
        get selectionStart() { return selection()[0]; },
        get selectionEnd() { return selection()[1]; },
        get maxLength() { return maxLength; },
         get disabled() { return isDisabled; },
         set disabled(value) { syncDisabled(value); },
        get id() { return active().id; },
        get tagName() { return active().tagName; },
        get parentElement() { return active().parentElement; },
        get hidden() { return Boolean(active().hidden); },
        set hidden(value) { active().hidden = Boolean(value); },
        get isContentEditable() { return useRich; },
         get dataset() { return dataset; },
        get style() { return active().style; },
        get clientHeight() { return active().clientHeight || 23; },
        get scrollHeight() { return active().scrollHeight || 23; },
        get scrollTop() { return active().scrollTop || 0; },
        set scrollTop(value) { active().scrollTop = value; },
        get placeholder() { return active().dataset.placeholder || ''; },
        set placeholder(value) { if (native) native.placeholder = value; if (rich) rich.dataset.placeholder = value; }
      };
      return methods;
    }
    bind(native);
    bind(rich);
    syncVisibility();
    syncDraftChat(native?.dataset?.draftChat ?? rich?.dataset?.draftChat);
    syncDisabled(isDisabled);
    return proxy();
  }
  return { createComposerBox };
});
