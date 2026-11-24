const assert = require('node:assert/strict');
const { mkdtempSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } = require('node:fs');
const Module = require('node:module');
const { tmpdir } = require('node:os');
const { join, resolve } = require('node:path');
const { pathToFileURL } = require('node:url');
const { runInNewContext } = require('node:vm');

async function testPreloadImagePaste() {
  const listeners = new Map();
  const calls = [];
  const events = [];
  const attachment = { id: '截图' };
  const target = { id: 'message', dataset: { draftChat: '测试会话' }, dispatchEvent: event => events.push(event) };
  const electron = {
    contextBridge: { exposeInMainWorld() {} },
    ipcRenderer: {
      async invoke(channel, paths, bytes, allowImage) {
        calls.push([channel, [...paths], bytes, allowImage]);
        return [attachment];
      }
    },
    webUtils: { getPathForFile: file => file.path || '' }
  };
  runInNewContext(readFileSync(join(__dirname, '../src/linux/desktop/preload.cjs'), 'utf8'), {
    require: () => electron,
    window: { addEventListener: (name, listener) => listeners.set(name, listener) },
    CustomEvent
  });
  const paste = async (files, plain = '') => {
    calls.length = 0;
    events.length = 0;
    let prevented = false;
    listeners.get('paste')({
      isTrusted: true, target,
      clipboardData: { files, types: plain ? ['text/plain'] : ['Files'], getData: () => plain },
      preventDefault: () => { prevented = true; }
    });
    await new Promise(resolve => setImmediate(resolve));
    return prevented;
  };
  const unsupported = type => ({
    type,
    arrayBuffer: async () => assert.fail(type + ' 原始字节不应通过 IPC')
  });
  for (const type of ['image/webp', 'image/bmp', 'image/gif']) {
    const file = unsupported(type);
    assert.equal(await paste([file]), true);
    assert.deepEqual(calls, [['lanchat:stage-paste', [], null, true]]);
    assert.equal(events[0].type, 'files-pasted');
    assert.deepEqual(JSON.parse(events[0].detail), { chat: '测试会话', files: [attachment] });
    assert.equal(await paste([file], '普通文字'), false);
    assert.deepEqual(calls, []);
  }
  for (const type of ['image/png', 'image/jpeg']) {
    const bytes = new ArrayBuffer(3);
    const file = { type, size: bytes.byteLength, arrayBuffer: async () => bytes };
    assert.equal(await paste([unsupported('image/webp'), file]), true);
    assert.deepEqual(calls, [['lanchat:stage-paste', [], bytes, true]]);
  }
  assert.equal(await paste([{ type: 'image/gif', path: '本地文件' }]), true);
  assert.deepEqual(calls, [['lanchat:stage-paste', ['本地文件'], null, true]]);
}

async function testMainPathPolicy() {
  const opened = [];
  const sandboxDir = mkdtempSync(join(tmpdir(), 'lanchat-open-path-'));
  const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/b1sAAAAASUVORK5CYII=', 'base64');
  const preview = 'data:image/png;base64,' + png.toString('base64');
  const image = {
    isEmpty: () => false, toPNG: () => png, getSize: () => ({ width: 64, height: 48 }),
    resize: options => { assert.deepEqual(options, { width: 48 }); return { toDataURL: () => preview }; }
  };
  // 桩只把两个测试样本识别为有效图片，用不同字节区分 JPEG 候选和解码后保存的 PNG。
  const jpeg = Buffer.from([0xff, 0xd8, 0xff, 0xd9]);
  const invalid = Buffer.from('无法解码');
  const decoded = [];
  let systemClipboard = [];
  let clipboardReads = 0;
  const originalLoad = Module._load;
  process.env.LANCHAT_TEST_MODE = '1';
  Module._load = function (request, parent, isMain) {
    if (request === 'electron') {
      return {
        app: { getPath: () => sandboxDir }, BrowserWindow: function () {}, dialog: {}, ipcMain: {}, session: {},
        clipboard: { read: async () => { clipboardReads++; return systemClipboard; } },
        nativeImage: { createFromBuffer: bytes => {
          decoded.push(bytes);
          return bytes.equals(png) || bytes.equals(jpeg) ? image : { isEmpty: () => true };
        } },
        shell: { openPath: async target => { opened.push(target); return ''; } }
      };
    }
    return originalLoad.apply(this, arguments);
  };
  let mainApi;
  try {
    mainApi = require('../src/linux/desktop/main.cjs');
  } finally {
    Module._load = originalLoad;
    delete process.env.LANCHAT_TEST_MODE;
  }

  try {
    const existing = join(sandboxDir, 'received.txt');
    const unrecorded = join(sandboxDir, 'other.txt');
    writeFileSync(existing, 'ok');
    writeFileSync(unrecorded, 'nope');

    const rawGnome = 'electron application/osclipboard;format="x-special/gnome-copied-files"';
    const firstFile = join(sandboxDir, '中文 #1.txt');
    const fileUri = pathToFileURL(firstFile).href;
    const clipboardItem = (type, data) => ({ types: [type], getType: async () => new Blob([data]) });
    const readClipboard = mainApi.readClipboardPaths;
    assert.deepEqual(await readClipboard([clipboardItem(rawGnome, `copy\n${fileUri}\n${fileUri}\0`)]), [firstFile]);
    assert.deepEqual(await readClipboard([clipboardItem('text/uri-list', `# 文件\r\n${fileUri}\r\nhttps://example.com/`)]), [firstFile]);
    assert.deepEqual(await readClipboard([clipboardItem('text/plain', firstFile)]), []);
    assert.deepEqual(await mainApi.stagePastedFiles([], null, false), []);
    assert.equal(clipboardReads, 1);
    const [attachment] = await mainApi.stagePastedFiles([], Uint8Array.from(png).buffer, true);
    assert.equal(clipboardReads, 1);
    const pasted = join(sandboxDir, 'PastedImages');
    assert.equal(attachment.preview, preview);
    assert.deepEqual(readFileSync(join(pasted, attachment.name)), png);
    await mainApi.discardPastedFiles([attachment.id]);
    assert.equal(readdirSync(pasted).includes(attachment.name), false);

    for (const type of ['image/webp', 'image/bmp', 'image/gif']) {
      const unsupported = { types: [type], getType: async () => assert.fail(type + ' 不应交给原生解码器') };
      systemClipboard = [unsupported];
      decoded.length = 0;
      assert.deepEqual(await mainApi.stagePastedFiles([], null, true), []);
      assert.deepEqual(decoded, []);
      systemClipboard.push(clipboardItem('image/png', png));
      assert.deepEqual(await mainApi.stagePastedFiles([], null, false), []);
      assert.deepEqual(decoded, []);
      const [fallback] = await mainApi.stagePastedFiles([], null, true);
      assert.deepEqual(decoded, [png]);
      assert.deepEqual(readFileSync(join(pasted, fallback.name)), png);
      await mainApi.discardPastedFiles([fallback.id]);
    }

    decoded.length = 0;
    systemClipboard = [{
      types: ['image/jpeg', 'image/png'],
      getType: async type => new Blob([type === 'image/png' ? invalid : jpeg])
    }];
    const [fallbackJpeg] = await mainApi.stagePastedFiles([], null, true);
    assert.deepEqual(decoded, [invalid, jpeg]);
    assert.deepEqual(readFileSync(join(pasted, fallbackJpeg.name)), png);
    await mainApi.discardPastedFiles([fallbackJpeg.id]);

    decoded.length = 0;
    systemClipboard = [clipboardItem('image/jpeg', jpeg)];
    const [fallbackMemory] = await mainApi.stagePastedFiles([], Uint8Array.from(invalid).buffer, true);
    assert.deepEqual(decoded, [invalid, jpeg]);
    await mainApi.discardPastedFiles([fallbackMemory.id]);
    systemClipboard = [clipboardItem('image/png', invalid), clipboardItem('image/jpeg', invalid)];
    await assert.rejects(() => mainApi.stagePastedFiles([], null, true), /无法读取剪贴板图片/);
    assert.deepEqual(readdirSync(pasted), []);

    const [regular] = await mainApi.stagePastedFiles([existing], null, true);
    await mainApi.discardPastedFiles([regular.id]);
    assert.equal(readFileSync(existing, 'utf8'), 'ok');

    const emojiDir = join(sandboxDir, 'WeChatEmoji');
    mkdirSync(emojiDir);
    writeFileSync(join(emojiDir, '001.png'), png);
    writeFileSync(join(emojiDir, 'manifest.tsv'), '[敲打]\t001.png\n[忽略]\t../001.png\n');
    assert.deepEqual(await mainApi.readEmojiPack(), [['[敲打]', preview]]);

    await assert.rejects(() => mainApi.openPath(unrecorded, 'file'), /未在通信记录中登记/);
    mainApi.recordMessagePaths({ file: true, path: existing });
    await mainApi.openPath(existing, 'file');
    assert.equal(opened.at(-1), resolve(existing));

    rmSync(existing);
    await assert.rejects(() => mainApi.openPath(existing, 'file'));
    await mainApi.openPath(existing, 'directory');
    assert.equal(opened.at(-1), resolve(sandboxDir));

    const missingParentFile = join(sandboxDir, 'gone-dir', 'lost.txt');
    mainApi.recordMessagePaths({ file: true, path: missingParentFile });
    await assert.rejects(() => mainApi.openPath(missingParentFile, 'directory'), /所在目录不可用/);

    assert.deepEqual(
      mainApi.computeSettingsBounds({ x: 0, y: 0, width: 1920, height: 1080 }, { x: 440, y: 180, width: 1040, height: 720 }),
      { width: 1260, height: 820, x: 330, y: 130 }
    );
    assert.deepEqual(
      mainApi.computeSettingsBounds({ x: 0, y: 0, width: 1280, height: 800 }, { x: 100, y: 40, width: 1040, height: 720 }),
      { width: 1240, height: 760, x: 0, y: 20 }
    );
    const clamped = mainApi.computeSettingsBounds({ x: 100, y: 50, width: 1024, height: 768 }, { x: 900, y: 600, width: 880, height: 560 });
    assert.equal(clamped.width, 984);
    assert.equal(clamped.height, 728);
    assert.equal(clamped.x >= 100 && clamped.x + clamped.width <= 1124, true);
    assert.equal(clamped.y >= 50 && clamped.y + clamped.height <= 818, true);

    for (const small of [
      { x: 0, y: 0, width: 640, height: 480 },
      { x: 0, y: 0, width: 500, height: 420 },
      { x: 0, y: 0, width: 420, height: 360 }
    ]) {
      const smallBounds = mainApi.computeSettingsBounds(small, null);
      const minSize = mainApi.computeSettingsMinSize(smallBounds);
      assert.equal(smallBounds.width <= small.width, true);
      assert.equal(smallBounds.height <= small.height, true);
      assert.equal(minSize.minWidth <= smallBounds.width, true);
      assert.equal(minSize.minHeight <= smallBounds.height, true);
    }
  } finally {
    rmSync(sandboxDir, { recursive: true, force: true });
  }
}

module.exports = async function testDesktopIpc() {
  await testPreloadImagePaste();
  await testMainPathPolicy();
};
