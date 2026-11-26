const assert = require('node:assert/strict');
const http = require('node:http');
const { createHash } = require('node:crypto');
const { mkdtemp, readFile, readdir, rm, stat } = require('node:fs/promises');
const os = require('node:os');
const { join } = require('node:path');
const { createUpdateService } = require('../src/linux/desktop/update-service.cjs');
const { makeNode, makeSettingsDom, withGlobals } = require('./linux_desktop_test_helpers.js');

const bytes = Buffer.from('LanChat update package');
const digest = createHash('sha256').update(bytes).digest('hex');
async function startMock(state) {
  const server = http.createServer((request, response) => {
    const path = new URL(request.url, 'http://127.0.0.1').pathname;
    if (path === '/api') {
      response.writeHead(state.apiStatus || 200, { 'content-type': 'application/json' });
      response.end(JSON.stringify(state.release));
      return;
    }
    if (path === '/latest') {
      response.writeHead(302, { location: 'https://github.com/yxiaojiang/LanChat/releases/tag/v2.0.28' });
      response.end();
      return;
    }
    if (path.endsWith('/SHA256SUMS.json')) {
      if (!state.manifest) { response.writeHead(404); response.end('{}'); return; }
      response.writeHead(200, { 'content-type': 'application/json' });
      response.end(JSON.stringify(state.manifest));
      return;
    }
    if (path.endsWith('.deb') || path.endsWith('.tar.gz')) {
      response.writeHead(200, { 'content-length': String(bytes.length) });
      response.flushHeaders();
      if (state.downloadMode === 'stuck') return;
      if (state.downloadMode === 'slow') {
        const chunks = [bytes.subarray(0, 6), bytes.subarray(6, 12), bytes.subarray(12)];
        let index = 0;
        const write = () => { if (response.destroyed) return; if (index === chunks.length) return response.end(); response.write(chunks[index++]); setTimeout(write, 120); };
        write();
        return;
      }
      response.end(bytes);
      return;
    }
    response.writeHead(404); response.end();
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  return { server, base: `http://127.0.0.1:${server.address().port}` };
}
function release(base, tag = 'v2.0.27', asset = 'lanchat_2.0.27_amd64.deb') {
  const version = tag.replace(/^v/i, '');
  const names = new Set([asset, `lanchat_${version}_amd64.deb`, `lanchat_${version}_arm64.deb`,
    `lanchat_${version}_amd64_portable.tar.gz`]);
  return {
    tag_name: tag, name: `LanChat ${tag}`, body: '修复局域网更新与维护入口。',
    html_url: `https://github.com/yxiaojiang/LanChat/releases/tag/${tag}`,
    assets: [
      ...[...names].map(name => ({ name, browser_download_url: `${base}/download/${name}` })),
      { name: 'SHA256SUMS.json', browser_download_url: `${base}/download/SHA256SUMS.json` }
    ]
  };
}
function manifest(asset, hash = digest, arch = 'amd64') {
  return { [arch]: { artifacts: { [asset]: hash } } };
}
function multiManifest() {
  return {
    amd64: { artifacts: {
      'lanchat_2.0.27_amd64.deb': digest,
      'lanchat_2.0.27_amd64_portable.tar.gz': digest
    } },
    arm64: { artifacts: { 'lanchat_2.0.27_arm64.deb': digest } }
  };
}
function service(options, base, homeDir) {
  return createUpdateService({
    currentVersion: '2.0.26', apiUrl: `${base}/api`, latestUrl: `${base}/latest`,
    downloadRoot: `${base}/download/`, homeDir, ...options
  });
}
async function testLatestAndAssets(base, homeDir, state) {
  state.release = release(base, 'v2.0.26', 'lanchat_2.0.26_amd64.deb');
  const current = service({ env: { LANCHAT_INSTALLED: '1' }, arch: 'x64' }, base, homeDir);
  assert.equal((await current.checkUpdate()).hasUpdate, false);
  state.release = release(base);
  const x64 = service({ env: { LANCHAT_INSTALLED: '1' }, arch: 'x64' }, base, homeDir);
  const result = await x64.checkUpdate();
  assert.equal(result.hasUpdate, true);
  assert.equal(result.assetName, 'lanchat_2.0.27_amd64.deb');
  assert.equal(result.sha256Verified, true);
  assert.equal(result.canDownload, true);
  const downloaded = await x64.downloadUpdate();
  assert.equal(downloaded.sha256Verified, true);
  assert.equal(downloaded.installCommand, `sudo apt install '${downloaded.savePath}'`);
  assert.equal((await readFile(downloaded.savePath)).toString(), bytes.toString());
  let openedPath = '';
  await x64.openUpdateLocation(downloaded.savePath, path => { openedPath = path; });
  assert.equal(openedPath, downloaded.savePath);
  await assert.rejects(() => x64.openUpdateLocation(join(homeDir, 'escape.deb'), () => {}), /路径无效/);
  const folder = await stat(join(homeDir, '.cache', 'lanchat', 'updates'));
  assert.equal(folder.isDirectory(), true);
  const arm = service({ env: { LANCHAT_INSTALLED: '1' }, arch: 'arm64' }, base, homeDir);
  const armResult = await arm.checkUpdate();
  assert.equal(armResult.assetName, 'lanchat_2.0.27_arm64.deb');
  assert.equal(armResult.canDownload, true);
  const portable = service({ env: { LANCHAT_INSTALLED: '0' }, arch: 'x64' }, base, homeDir);
  const portableResult = await portable.checkUpdate();
  assert.equal(portableResult.assetName, 'lanchat_2.0.27_amd64_portable.tar.gz');
  assert.equal(portableResult.canDownload, true);
  return state;
}
async function testFallbackAndWarnings(base, homeDir, state) {
  state.apiStatus = 403;
  state.release = release(base, 'v2.0.28', 'lanchat_2.0.28_amd64.deb');
  state.manifest = manifest('lanchat_2.0.28_amd64.deb');
  const fallback = service({ env: { LANCHAT_INSTALLED: '1' }, arch: 'x64' }, base, homeDir);
  const result = await fallback.checkUpdate();
  assert.equal(result.latestVersion, '2.0.28');
  assert.equal(result.sha256Verified, true);
  state.manifest = null;
  const missing = service({ env: { LANCHAT_INSTALLED: '1' }, arch: 'x64' }, base, homeDir);
  const blocked = await missing.checkUpdate();
  assert.equal(blocked.canDownload, false);
  assert.match(blocked.warning, /SHA256SUMS/);
  let opened = '';
  await missing.openReleasePage(blocked.webUrl, url => { opened = url; });
  assert.match(opened, /github\.com\/yxiaojiang\/LanChat\/releases\/tag\/v2\.0\.28/);
  await assert.rejects(() => missing.openReleasePage('https://example.com/releases/tag/v2.0.28', () => {}), /只允许/);
}
async function testTamperedHash(base, homeDir, state) {
  state.apiStatus = 200;
  state.release = release(base, 'v2.0.29', 'lanchat_2.0.29_amd64.deb');
  state.manifest = manifest('lanchat_2.0.29_amd64.deb', '0'.repeat(64));
  const tampered = service({ env: { LANCHAT_INSTALLED: '1' }, arch: 'x64' }, base, homeDir);
  assert.equal((await tampered.checkUpdate()).canDownload, true);
  await assert.rejects(() => tampered.downloadUpdate(), /SHA-256/);
  assert.deepEqual(await readdir(join(homeDir, '.cache', 'lanchat', 'updates')), ['lanchat_2.0.27_amd64.deb']);
}
async function testStreamTimeoutModes(base, homeDir, state) {
  state.release = release(base, 'v2.0.30', 'lanchat_2.0.30_amd64.deb'); state.manifest = manifest('lanchat_2.0.30_amd64.deb'); state.downloadMode = 'slow';
  const slow = service({ env: { LANCHAT_INSTALLED: '1' }, arch: 'x64', requestTimeout: 200, downloadIdleTimeout: 300 }, base, homeDir);
  assert.equal((await slow.checkUpdate()).canDownload, true);
  assert.equal((await slow.downloadUpdate()).sha256Verified, true);
  state.release = release(base, 'v2.0.31', 'lanchat_2.0.31_amd64.deb'); state.manifest = manifest('lanchat_2.0.31_amd64.deb'); state.downloadMode = 'stuck';
  const stuck = service({ env: { LANCHAT_INSTALLED: '1' }, arch: 'x64', requestTimeout: 200, downloadIdleTimeout: 60 }, base, homeDir);
  await stuck.checkUpdate();
  await assert.rejects(() => stuck.downloadUpdate(), /空闲超时/);
  assert.equal((await readdir(join(homeDir, '.cache', 'lanchat', 'updates'))).some(name => name.endsWith('.part')), false);
  state.downloadMode = 'normal';
}
async function testStaticWiring() {
  const { readFileSync } = require('node:fs');
  const root = join(__dirname, '..');
  const read = name => readFileSync(join(root, name), 'utf8');
  const main = read('src/linux/desktop/main.cjs');
  const preload = read('src/linux/desktop/preload.cjs');
  const html = read('src/linux/desktop/settings.html');
  const settings = read('src/linux/desktop/settings.js');
  const updater = read('src/linux/desktop/update-service.cjs');
  assert.match(main, /handle\('check-update'.*true\)/);
  assert.equal(main.includes("handle('open-release-page'") && main.includes('safeUrl => shell.openExternal(safeUrl)), true'), true);
  for (const key of ['checkUpdate', 'downloadUpdate', 'openUpdateLocation', 'openReleasePage']) assert.match(preload, new RegExp(key));
  assert.match(html, /settings-update-modal/);
  assert.match(settings, /textContent = result\.notes/);
  assert.match(settings, /当前已是最新版本/);
  assert.equal(settings.includes('innerHTML'), false);
  assert.match(updater, /let downloading = false/);
}
async function testSettingsUpdateDom() {
  const { doc, win, nodes } = makeSettingsDom();
  for (const id of ['settings-update-dialog', 'settings-update-title', 'settings-update-version', 'settings-update-status', 'settings-update-warning', 'settings-update-notes', 'settings-update-progress-bar', 'settings-update-release', 'settings-update-open-location', 'settings-update-download', 'settings-update-copy-install', 'settings-update-later']) nodes.set(id, makeNode('DIV', id));
  const official = 'https://github.com/yxiaojiang/LanChat/releases/latest';
  let eventHandler; let checkCount = 0; let opened = ''; let copied = ''; let quit = 0; let finishDownload;
  win.navigator = { clipboard: { writeText: async text => { copied = text; } } };
  return withGlobals({ document: doc, window: win, testMode: true }, async () => {
    const settingsApp = require('../src/linux/desktop/settings.js'); const model = require('../src/linux/desktop/settings-model.js');
    const pages = require('../src/linux/desktop/settings-pages.js'); const appearance = require('../src/linux/desktop/appearance.js'); const scroll = require('../src/linux/desktop/scrollbar.js');
    const api = {
      settings: async () => ({}), state: async () => ({}), systemInfo: async () => ({}), onEvent: fn => { eventHandler = fn; },
      checkUpdate: async () => { checkCount += 1; if (checkCount === 1) throw new Error('网络失败'); if (checkCount === 2) return { hasUpdate: true, currentVersion: '2.0.26', latestVersion: '2.0.27', canDownload: false, warning: '清单缺失', webUrl: '' }; return { hasUpdate: true, currentVersion: '2.0.26', latestVersion: '2.0.27', canDownload: true, webUrl: 'https://github.com/yxiaojiang/LanChat/releases/tag/v2.0.27', assetName: 'lanchat_2.0.27_amd64.deb', kind: 'deb' }; },
      openReleasePage: async url => { opened = url; }, downloadUpdate: () => new Promise(resolve => { finishDownload = () => resolve({ savePath: '/tmp/lanchat.deb', installCommand: "sudo apt install '/tmp/lanchat.deb'", assetName: 'lanchat_2.0.27_amd64.deb' }); }), quit: () => { quit += 1; }
    };
    const ctrl = settingsApp.createSettingsController({ document: doc, window: win, api, model, pages, appearance, scroll });
    await ctrl.ready;
    const dialog = nodes.get('settings-update-dialog');
    const trigger = async () => { eventHandler({ event: 'open-update-check' }); await new Promise(resolve => setImmediate(resolve)); };
    await trigger();
    nodes.get('settings-update-release').dispatch('click'); await new Promise(resolve => setImmediate(resolve)); assert.equal(opened, official);
    dialog.close(); await trigger(); assert.equal(nodes.get('settings-update-download').hidden, true);
    dialog.close();
    await trigger();
    nodes.get('settings-update-download').dispatch('click'); await new Promise(resolve => setImmediate(resolve));
    eventHandler({ event: 'update-progress', data: { percent: 42, transferred: 42, total: 100 } });
    assert.equal(nodes.get('settings-update-progress-bar').style.width, '42%'); assert.match(nodes.get('settings-update-status').textContent, /42%/);
    finishDownload(); await new Promise(resolve => setImmediate(resolve)); nodes.get('settings-update-copy-install').dispatch('click'); await new Promise(resolve => setImmediate(resolve));
    assert.match(copied, /sudo apt install/); assert.equal(quit, 1);
  });
}
async function testLinuxDesktopUpdate() {
  const homeDir = await mkdtemp(join(os.tmpdir(), 'lanchat-update-regression-'));
  const state = { release: null, manifest: null, apiStatus: 200 };
  const mock = await startMock(state);
  try {
    state.release = release(mock.base);
    state.manifest = multiManifest();
    await testLatestAndAssets(mock.base, homeDir, state);
    await testFallbackAndWarnings(mock.base, homeDir, state);
    await testTamperedHash(mock.base, homeDir, state);
    await testStreamTimeoutModes(mock.base, homeDir, state);
    await testStaticWiring();
    await testSettingsUpdateDom();
    return true;
  } finally {
    await new Promise(resolve => mock.server.close(resolve));
    await rm(homeDir, { recursive: true, force: true });
  }
}

if (require.main === module) testLinuxDesktopUpdate().then(() => console.log('linux desktop update regression: ok'));
module.exports = testLinuxDesktopUpdate;
