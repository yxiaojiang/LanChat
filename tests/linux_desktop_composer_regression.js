const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { createComposerBox } = require('../src/linux/desktop/composer-box.js');

function makeText(value) { return { nodeType: 3, nodeValue: value, childNodes: [], parentNode: null }; }

function makeElement(tagName) {
  const listeners = new Map();
  const node = {
    nodeType: 1, tagName, dataset: {}, childNodes: [], children: [], parentNode: null,
    hidden: false, disabled: false, style: {}, value: '', selectionStart: 0, selectionEnd: 0,
    append(...items) { for (const item of items) { item.parentNode = node; node.childNodes.push(item); if (item.nodeType === 1) node.children.push(item); } },
    replaceChildren(...items) { node.childNodes = []; node.children = []; node.append(...items); },
    contains(item) { return item === node || node.childNodes.some(child => child.contains?.(item) || child === item); },
    addEventListener(type, listener) { if (!listeners.has(type)) listeners.set(type, []); listeners.get(type).push(listener); },
    dispatch(type, event = {}) { for (const listener of listeners.get(type) || []) listener({ target: node, preventDefault() {}, ...event }); },
    dispatchEvent(event) { node.dispatch(event.type, event); return true; },
    focus() {}, getBoundingClientRect() { return { left: 0, width: 20 }; }
  };
  Object.defineProperty(node, 'textContent', { get: () => node.childNodes.map(child => child.nodeValue || child.textContent || '').join('') });
  return node;
}

function makeDocument() {
  let currentRange = null;
  return {
    createTextNode: makeText,
    createElement: tag => makeElement(tag),
    createRange() {
      return {
        setStart(container, offset) { this.startContainer = container; this.startOffset = offset; },
        setEnd(container, offset) { this.endContainer = container; this.endOffset = offset; },
        deleteContents() {}, insertNode() {}, collapse() {}
      };
    },
    getSelection() { return { rangeCount: currentRange ? 1 : 0, getRangeAt: () => currentRange, removeAllRanges() { currentRange = null; }, addRange(range) { currentRange = range; } }; },
    setRange(range) { currentRange = range; }
  };
}

function makeNative() {
  const node = makeElement('TEXTAREA');
  const originalAppend = node.append;
  node.setRangeSelection = (start, end) => { node.selectionStart = start; node.selectionEnd = end; };
  node.setSelectionRange = node.setRangeSelection;
  node.setRangeText = (text, start, end) => {
    node.value = node.value.slice(0, start) + text + node.value.slice(end);
    node.setRangeSelection(start + text.length, start + text.length);
  };
  void originalAppend;
  return node;
}

function makeStorage() {
  const values = new Map();
  return { getItem: key => values.get(key) || null, setItem: (key, value) => values.set(key, String(value)) };
}

async function testComposer() {
  const native = makeNative();
  const rich = makeElement('DIV');
  const document = makeDocument();
  const storage = makeStorage();
  const box = createComposerBox({ native, rich, document, storage, maxLength: 12 });
  let inputEvents = 0;
  box.addEventListener('input', () => { inputEvents += 1; });

  // 未核销 CP-2B 时默认仍是原生 textarea，粘贴会按选区裁剪并派发 input。
  assert.equal(box.isRich(), false);
  native.value = '甲乙'; native.setRangeSelection(1, 2);
  box.insertText('丙\r\n丁');
  assert.equal(native.value, '甲丙\n丁');
  assert.equal(inputEvents, 1);

  box.setEmojiImages([['[敲打]', 'data:image/png;base64,AA==']]);
  box.value = '甲[敲打]乙';
  assert.equal(box.toggleRich(), true);
  assert.equal(box.value, '甲[敲打]乙');
  const tokenOf = () => rich.childNodes.find(child => child.dataset?.emojiToken === '[敲打]');
  let token = tokenOf();
  assert.ok(token, '富文本中应保留整枚表情原子节点');

  // 点击图片左半/右半只能把光标收拢到 token 外侧。
  rich.dispatch('mousedown', { target: token, clientX: 1 });
  assert.equal(box.selectionStart, 1);
  rich.dispatch('mousedown', { target: token, clientX: 19 });
  assert.equal(box.selectionStart, 5);

  // Backspace 删除整枚 token；composition/229 期间不得抢走中文 IME 的按键。
  let prevented = false;
  rich.dispatch('keydown', { key: 'Backspace', preventDefault: () => { prevented = true; } });
  assert.equal(prevented, true);
  assert.equal(box.value, '甲乙');
  box.value = '甲[敲打]乙';
  token = tokenOf();
  rich.dispatch('mousedown', { target: token, clientX: 19 });
  prevented = false;
  rich.dispatch('keydown', { key: 'Backspace', isComposing: true, preventDefault: () => { prevented = true; } });
  assert.equal(prevented, false);
  assert.equal(box.value, '甲[敲打]乙');

  // copy/cut 必须交出可读的 [表情名] 纯文本；随后一键回退原生输入框并持久化开关。
  document.setRange({ startContainer: rich, startOffset: 0, endContainer: rich, endOffset: 3 });
  const clipboard = { value: '', setData(type, value) { if (type === 'text/plain') this.value = value; } };
  rich.dispatch('copy', { clipboardData: clipboard, preventDefault() {} });
  assert.equal(clipboard.value, '甲[敲打]乙');
  rich.dispatch('cut', { clipboardData: clipboard, preventDefault() {} });
  assert.equal(clipboard.value, '甲[敲打]乙');
  assert.equal(box.value, '');
  box.value = '甲[敲打]乙';
  token = tokenOf();
  rich.dispatch('mousedown', { target: token, clientX: 1 });
  rich.dispatch('keydown', { key: 'Delete', preventDefault() {} });
  assert.equal(box.value, '甲乙');

  // 嵌套块级容器与 br 必须还原为单个换行，光标偏移只能按叶子节点长度累加。
  const firstLine = makeElement('DIV');
  const middleLine = makeElement('DIV');
  const lastLine = makeElement('DIV');
  const firstText = makeText('甲');
  const middleText = makeText('乙');
  const afterBreak = makeText('丙');
  const lastText = makeText('丁');
  firstLine.append(firstText);
  middleLine.append(middleText, makeElement('BR'), afterBreak);
  lastLine.append(lastText);
  rich.replaceChildren(firstLine, middleLine, lastLine);
  document.setRange({ startContainer: afterBreak, startOffset: 1, endContainer: lastText, endOffset: 1 });
  assert.equal(box.value, '甲\n乙\n丙\n丁');
  assert.equal(box.selectionStart, 5);
  assert.equal(box.selectionEnd, 7);

  // setRangeText 必须尊重调用方传入的显式范围，而不是读取当前选区覆盖。
  box.toggleRich();
  native.value = 'abcdef';
  native.setRangeSelection(0, 0);
  box.setRangeText('X', 2, 4, 'end');
  assert.equal(native.value, 'abXef');
  assert.equal(box.toggleRich(), true);

  box.value = '甲[敲打]乙';
  assert.equal(box.toggleRich(), false);
  assert.equal(native.hidden, false);
  assert.equal(storage.getItem('lanchat.composer.native'), '1');

  // 持久化富文本重启恢复时显示正确轨道；离线禁用状态切换轨道后不得被解锁。
  storage.setItem('lanchat.composer.native', '0');
  const native2 = makeNative();
  const rich2 = makeElement('DIV');
  const restored = createComposerBox({ native: native2, rich: rich2, document: makeDocument(), storage });
  assert.equal(restored.isRich(), true);
  assert.equal(rich2.hidden, false);
  assert.equal(native2.hidden, true);
  restored.dataset.draftChat = 'OFFLINE';
  restored.disabled = true;
  assert.equal(native2.disabled, true);
  assert.equal(rich2.inert, true);
  assert.equal(rich2.contentEditable, 'false');
  assert.equal(rich2.dataset.disabled, 'true');
  assert.equal(restored.toggleRich(), false);
  assert.equal(restored.disabled, true);
  assert.equal(native2.dataset.draftChat, 'OFFLINE');
  assert.equal(rich2.dataset.draftChat, 'OFFLINE');
  restored.disabled = false;
  assert.equal(native2.disabled, false);
  assert.equal(rich2.inert, false);
  assert.equal(rich2.contentEditable, 'true');

  // 粘贴适配器与富文本门禁必须保留在源码/调用链中，且 CP-2B 不能被宿主机测试冒充核销。
  const preload = readFileSync(join(__dirname, '../src/linux/desktop/preload.cjs'), 'utf8');
  const main = readFileSync(join(__dirname, '../src/linux/desktop/main.cjs'), 'utf8');
  const renderer = readFileSync(join(__dirname, '../src/linux/desktop/renderer.js'), 'utf8');
  const ui = readFileSync(join(__dirname, 'linux_desktop_ui_regression.js'), 'utf8');
  assert.match(preload, /pasteAdapter\.readText/);
  assert.match(preload, /pasteAdapter\.insertText/);
  assert.match(preload, /composerInput/);
  assert.doesNotMatch(preload, /stagePaste\s*:/);
  assert.match(preload, /readClipboardText/);
  assert.match(preload, /stageClipboard/);
  assert.match(preload, /target\?\.inert/);
  assert.match(preload, /dataset\?\.disabled/);
  assert.match(main, /handle\('read-clipboard-text', \(\) => clipboard\.readText\(\)\)/);
  assert.match(main, /stagePastedFiles\(\[\], null, true\)/);
  assert.doesNotMatch(renderer, /navigator\?\.clipboard\?\.read/);
  assert.match(renderer, /api\.stageClipboard/);
  assert.match(ui, /linux_desktop_composer_regression/);
  assert.match(readFileSync(join(__dirname, '../CRDocs/DEFERRED.md'), 'utf8'), /fcitx5 与 ibus/);
  console.log('linux desktop composer regression: ok');
}

if (require.main === module) testComposer().catch(error => { console.error(error); process.exitCode = 1; });
module.exports = testComposer;
