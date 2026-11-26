function makeNode(tagName, id = '') {
  const listeners = new Map();
  const attrs = new Map();
  const classes = new Set();
  const cssVars = new Map();
  const children = [];
  const content = [];
  const node = {
    tagName,
    id,
    hidden: false,
    disabled: false,
    open: false,
    returnValue: '',
    value: '',
    isConnected: true,
    dataset: {},
    style: {
      setProperty(name, val) { cssVars.set(name, String(val)); },
      getPropertyValue(name) { return cssVars.get(name) || ''; }
    },
    get className() { return [...classes].join(' '); },
    set className(val) {
      classes.clear();
      for (const part of String(val || '').split(/\s+/).filter(Boolean)) classes.add(part);
    },
    get textContent() { return content.map(child => typeof child === 'string' ? child : child.textContent).join(''); },
    set textContent(value) { this.replaceChildren(String(value)); },
    clientHeight: 115,
    scrollHeight: 115,
    scrollTop: 0,
    parentElement: null,
    children,
    get childElementCount() { return children.length; },
    classList: {
      add(name) { classes.add(name); },
      remove(name) { classes.delete(name); },
      toggle(name, force) {
        if (force === undefined) {
          if (classes.has(name)) classes.delete(name);
          else classes.add(name);
          return classes.has(name);
        }
        if (force) classes.add(name);
        else classes.delete(name);
        return Boolean(force);
      },
      contains(name) { return classes.has(name); }
    },
    setAttribute(name, value) { attrs.set(name, String(value)); },
    getAttribute(name) { return attrs.get(name); },
    append(...nodes) {
      for (const child of nodes) {
        if (typeof child === 'string') content.push(child);
        else if (typeof child === 'object' && child) {
          if (child.tagName === 'FRAGMENT') {
            this.append(...child.childContent);
            child.replaceChildren();
          } else {
            child.parentElement = this;
            children.push(child);
            content.push(child);
          }
        }
      }
    },
    replaceChildren(...nodes) {
      children.length = 0;
      content.length = 0;
      this.append(...nodes);
    },
    replaceWith(replacement) {
      const parent = this.parentElement;
      parent.children.splice(parent.children.indexOf(this), 1, replacement);
      const siblings = parent.childContent;
      siblings.splice(siblings.indexOf(this), 1, replacement);
      replacement.parentElement = parent;
      this.parentElement = null;
    },
    get childContent() { return content; },
    contains(target) {
      if (target === this) return true;
      return children.some(child => child.contains?.(target));
    },
    querySelectorAll(selector) {
      const cls = selector?.startsWith('.') ? selector.slice(1) : '';
      const out = [];
      const walk = cur => {
        for (const child of cur.children || []) {
          if (!cls || child.classList?.contains(cls)) out.push(child);
          walk(child);
        }
      };
      walk(this);
      return out;
    },
    querySelector(selector) { return this.querySelectorAll(selector)[0] || null; },
    getBoundingClientRect() { return { left: 10, top: 100, width: 256, height: 180 }; },
    showModal() { this.open = true; },
    close(val) {
      if (val !== undefined) this.returnValue = String(val);
      this.open = false;
      this.dispatch('close', {});
    },
    focus() {},
    select() {},
    addEventListener(type, fn, options) {
      if (!listeners.has(type)) listeners.set(type, new Map());
      listeners.get(type).set(fn, Boolean(options?.once));
    },
    removeEventListener(type, fn) {
      listeners.get(type)?.delete(fn);
    },
    dispatch(type, event = {}) {
      const map = listeners.get(type);
      if (!map) return;
      for (const [fn, once] of [...map.entries()]) {
        if (once) map.delete(fn);
        fn({ preventDefault() {}, stopPropagation() {}, ...event });
      }
    }
  };
  return node;
}

function makeDocument(nodes) {
  const doc = makeNode('DOCUMENT');
  doc.documentElement = makeNode('HTML');
  doc.body = makeNode('BODY');
  doc.createElement = tag => makeNode(tag.toUpperCase());
  doc.createDocumentFragment = () => makeNode('FRAGMENT');
  doc.getElementById = id => nodes.get(id) || null;
  doc.activeElement = null;
  return doc;
}

function makeOverlayDom() {
  const win = makeNode('WINDOW');
  win.innerWidth = 1040;
  win.innerHeight = 720;
  const menu = makeNode('DIV', 'menu');
  const emoji = makeNode('DIV', 'emoji');
  const emojiGrid = makeNode('DIV', 'emoji-grid');
  emoji.append(emojiGrid);
  const confirm = makeNode('DIALOG', 'confirm');
  const attach = makeNode('BUTTON', 'attach');
  attach.append(makeNode('IMG'));
  menu.hidden = true;
  emoji.hidden = true;
  const nodes = new Map([['menu', menu], ['emoji', emoji], ['emoji-grid', emojiGrid], ['confirm', confirm], ['attach', attach]]);
  const doc = makeDocument(nodes);
  doc.querySelectorAll = () => [];
  doc.activeElement = attach;
  return { doc, win, menu, emoji, emojiGrid, confirm, attach };
}

function makeSettingsDom() {
  const win = makeNode('WINDOW');
  win.innerWidth = 1260;
  win.innerHeight = 820;
  const ids = [
    'settings-nav', 'settings-search', 'settings-search-clear', 'settings-viewport', 'settings-panels',
    'settings-banner', 'settings-menu', 'settings-nick-dialog', 'nick-form', 'nick-input', 'nick-error',
    'nick-cancel', 'settings-feedback-dialog', 'feedback-form', 'feedback-input', 'feedback-error',
    'feedback-cancel', 'settings-confirm-dialog', 'settings-confirm-title', 'settings-confirm-body',
    'settings-confirm-accept', 'settings-confirm-cancel'
  ];
  const nodes = new Map(ids.map(id => [id, makeNode(id === 'feedback-input' ? 'TEXTAREA' : 'DIV', id)]));
  const feedbackHost = makeNode('DIV');
  feedbackHost.append(nodes.get('feedback-input'));
  nodes.get('settings-viewport').parentElement = makeNode('DIV');
  nodes.get('settings-nav').parentElement = makeNode('DIV');
  return { doc: makeDocument(nodes), win, nodes, feedbackHost };
}

function makeRendererDom() {
  const win = makeNode('WINDOW');
  win.innerWidth = 1040;
  win.innerHeight = 720;
  const ids = [
    'search', 'search-clear', 'refresh', 'nickname', 'nickname-chip', 'chats',
    'open-settings', 'quit', 'conversation', 'avatar', 'title', 'connection',
    'messages', 'attachments-host', 'attachments', 'status', 'compose', 'attach', 'open-downloads',
    'send-file', 'take-screenshot', 'clear-chat', 'message', 'send', 'recipient', 'recipients',
    'recipient-confirm', 'recipient-cancel'
  ];
  const nodes = new Map(ids.map(id => [id, makeNode(id === 'message' ? 'TEXTAREA' : 'DIV', id)]));
  nodes.get('attachments-host').append(nodes.get('attachments'));
  for (const id of ['chats', 'messages', 'message', 'recipients']) {
    if (!nodes.get(id).parentElement) nodes.get(id).parentElement = makeNode('DIV');
  }
  return { doc: makeDocument(nodes), win, nodes };
}

async function withGlobals({ document, window, testMode = false }, callback) {
  const originalDocument = global.document;
  const originalWindow = global.window;
  const originalTestMode = process.env.LANCHAT_TEST_MODE;
  global.document = document;
  global.window = window;
  if (testMode) process.env.LANCHAT_TEST_MODE = '1';
  try {
    return await callback();
  } finally {
    if (originalTestMode === undefined) delete process.env.LANCHAT_TEST_MODE;
    else process.env.LANCHAT_TEST_MODE = originalTestMode;
    if (originalDocument === undefined) delete global.document;
    else global.document = originalDocument;
    if (originalWindow === undefined) delete global.window;
    else global.window = originalWindow;
  }
}

module.exports = { makeNode, makeOverlayDom, makeSettingsDom, makeRendererDom, withGlobals };
