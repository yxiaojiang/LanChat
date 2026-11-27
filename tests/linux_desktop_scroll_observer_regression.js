const assert = require('node:assert/strict');
const { makeRendererDom, withGlobals } = require('./linux_desktop_test_helpers.js');
const presentation = require('../src/linux/desktop/presentation.js');

async function run() {
  const { doc, win, nodes } = makeRendererDom();
  nodes.get('message').scrollHeight = 230;
  nodes.get('message-rich').scrollHeight = 230;
  const observedResize = [];
  const observedMutation = [];
  const originalResizeObserver = global.ResizeObserver;
  const originalMutationObserver = global.MutationObserver;
  class StrictResizeObserver {
    constructor(callback) { this.callback = callback; }
    observe(target) {
      assert.equal(target?.nodeType, 1, 'ResizeObserver 只能观察真实 DOM 节点');
      observedResize.push(target);
    }
  }
  class StrictMutationObserver {
    constructor(callback) { this.callback = callback; }
    observe(target) {
      assert.equal(target?.nodeType, 1, 'MutationObserver 只能观察真实 DOM 节点');
      observedMutation.push(target);
    }
  }
  global.ResizeObserver = StrictResizeObserver;
  global.MutationObserver = StrictMutationObserver;
  try {
    await withGlobals({ document: doc, window: win, testMode: true }, async () => {
      const appearance = require('../src/linux/desktop/appearance.js');
      const scrollbar = require('../src/linux/desktop/scrollbar.js');
      let menuItems = [];
      const overlay = { openMenu(items) { menuItems = items; }, closeMenu() {}, openEmoji() {}, consumeAttachToggle: () => false, confirmAction: async () => false };
      const api = {
        state: async () => ({ id: 'self', nick: '本机', ip: '10.0.0.1', tcpPort: 38889, sequence: 1, peers: [], sessions: [] }),
        settings: async () => ({}), emojiPack: async () => [], onEvent() {}, syncUnreadState() {}, quit() {}
      };
      const mainApp = require('../src/linux/desktop/renderer.js');
      const controller = mainApp.createMainController({ document: doc, window: win, api, presentation, appearance, overlay, scroll: scrollbar });
      assert.equal(nodes.get('connection').textContent, '正在连接通信服务…');
      assert.equal(observedResize.includes(nodes.get('message')), true);
      assert.equal(observedResize.includes(nodes.get('message-rich')), true);
      assert.equal(observedMutation.includes(nodes.get('message')), true);
      assert.equal(observedMutation.includes(nodes.get('message-rich')), true);
      assert.equal(observedMutation.every(target => target?.nodeType === 1), true);
      assert.equal(nodes.get('message').parentElement.children.filter(node => node.className === 'float-thumb' && !node.hidden).length, 1);
      nodes.get('message').dispatch('contextmenu', { clientX: 1, clientY: 1 });
      menuItems.at(-1).run();
      assert.equal(nodes.get('message').hidden, true);
      assert.equal(nodes.get('message-rich').hidden, false);
      assert.equal(nodes.get('message').parentElement.children.filter(node => node.className === 'float-thumb' && !node.hidden).length, 1);
      await controller.initPromise;
      assert.equal(nodes.get('connection').textContent, '10.0.0.1:38889');
    });
  } finally {
    if (originalResizeObserver === undefined) delete global.ResizeObserver;
    else global.ResizeObserver = originalResizeObserver;
    if (originalMutationObserver === undefined) delete global.MutationObserver;
    else global.MutationObserver = originalMutationObserver;
  }
}

if (require.main === module) run().then(() => console.log('linux desktop scroll observer regression: ok'));
module.exports = run;
