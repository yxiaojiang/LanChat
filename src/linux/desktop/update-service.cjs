// 在线更新服务：只负责 GitHub 版本探测、清单校验与本地更新包落盘。
// 元数据请求采用短连接硬超时；安装包响应头返回后改用流式空闲读超时。
// 下载始终写入受限目录的 .part 文件，校验成功后才原子改名。
const http = require('node:http');
const https = require('node:https');
const { createHash } = require('node:crypto');
const { Transform } = require('node:stream');
const { pipeline } = require('node:stream/promises');
const { createWriteStream } = require('node:fs');
const { chmod, mkdir, rename, stat, unlink } = require('node:fs/promises');
const os = require('node:os');
const { basename, isAbsolute, join, relative, resolve } = require('node:path');
const API_URL = 'https://api.github.com/repos/yxiaojiang/LanChat/releases/latest';
const RELEASES_LATEST_URL = 'https://github.com/yxiaojiang/LanChat/releases/latest';
const DOWNLOAD_ROOT = 'https://github.com/yxiaojiang/LanChat/releases/download/';
const REQUEST_TIMEOUT = 8000;
const DOWNLOAD_IDLE_TIMEOUT = 30000;
function cleanVersion(value) {
  const match = String(value || '').match(/\d+(?:\.\d+)+/);
  return match ? match[0] : '';
}
function versionParts(value) {
  const version = cleanVersion(value);
  return version ? version.split('.').map(Number) : [];
}
function isNewer(latest, current) {
  const a = versionParts(latest);
  const b = versionParts(current);
  if (!a.length || !b.length) return false;
  for (let index = 0; index < Math.max(a.length, b.length); index += 1) {
    const left = a[index] || 0;
    const right = b[index] || 0;
    if (left !== right) return left > right;
  }
  return false;
}
function mapArchitecture(arch) {
  if (arch === 'x64') return 'amd64';
  if (arch === 'arm64') return 'arm64';
  throw new Error(`暂不支持 ${arch} 架构的 Linux 更新包`);
}
function assetNameFor(version, arch, installed) {
  const clean = cleanVersion(version);
  if (!clean) throw new Error('Release 版本号无效');
  return installed ? `lanchat_${clean}_${arch}.deb` : `lanchat_${clean}_${arch}_portable.tar.gz`;
}
function buildInstallCommand(savePath) {
  const normalized = resolve(savePath);
  if (!isAbsolute(normalized) || normalized.includes("'")) throw new Error('安装包路径无法安全引用');
  return `sudo apt install '${normalized}'`;
}
function githubReleaseUrl(value) {
  let parsed;
  try { parsed = new URL(value); } catch { throw new Error('GitHub Release 地址无效'); }
  if (parsed.protocol !== 'https:' || parsed.hostname !== 'github.com' || parsed.username || parsed.password || parsed.search || parsed.hash ||
      parsed.pathname !== '/yxiaojiang/LanChat/releases/latest' && !/^\/yxiaojiang\/LanChat\/releases\/tag\/[A-Za-z0-9._-]+$/.test(parsed.pathname)) {
    throw new Error('只允许打开 LanChat 的 GitHub Release 页面');
  }
  return parsed.toString();
}
function withinDirectory(directory, target) {
  const rest = relative(directory, target);
  return Boolean(rest) && !rest.startsWith('..') && !isAbsolute(rest);
}
function armIdleReadTimeout(response, timeout) {
  if (!(timeout > 0)) return;
  let timer = setTimeout(() => response.destroy(new Error('更新下载空闲超时')), timeout);
  const reset = () => {
    clearTimeout(timer);
    timer = setTimeout(() => response.destroy(new Error('更新下载空闲超时')), timeout);
  };
  const clear = () => clearTimeout(timer);
  response.__lanChatIdle = { reset, clear };
  response.once('end', clear);
  response.once('error', clear);
  response.once('close', clear);
}
function requestUrl(url, { headers = {}, timeout = REQUEST_TIMEOUT, followRedirects = true, depth = 0,
  stream = false, idleTimeout = DOWNLOAD_IDLE_TIMEOUT } = {}) {
  return new Promise((resolveRequest, reject) => {
    let parsed;
    try { parsed = new URL(url); } catch { reject(new Error('更新地址无效')); return; }
    const transport = parsed.protocol === 'https:' ? https : parsed.protocol === 'http:' ? http : null;
    if (!transport) { reject(new Error('更新地址协议无效')); return; }
    const request = transport.get(parsed, { headers }, response => {
      const location = response.headers.location;
      if (followRedirects && location && response.statusCode >= 300 && response.statusCode < 400) {
        response.resume();
        if (depth >= 4) { reject(new Error('更新地址重定向次数过多')); return; }
        requestUrl(new URL(location, parsed).toString(), { headers, timeout, followRedirects, depth: depth + 1, stream, idleTimeout })
          .then(resolveRequest, reject);
        return;
      }
      if (stream) {
        clearTimeout(deadline);
        request.setTimeout(0);
        armIdleReadTimeout(response, idleTimeout);
      }
      resolveRequest({ response, status: response.statusCode || 0, headers: response.headers, url: parsed.toString() });
    });
    const deadline = setTimeout(() => request.destroy(new Error('更新请求超时')), timeout);
    if (!stream) request.setTimeout(timeout, () => request.destroy(new Error('更新请求超时')));
    request.once('close', () => clearTimeout(deadline));
    request.once('error', reject);
  });
}
async function readResponse(response, limit = 4 * 1024 * 1024) {
  const chunks = [];
  let length = 0;
  for await (const chunk of response) {
    length += chunk.length;
    if (length > limit) throw new Error('更新响应过大');
    chunks.push(chunk);
  }
  return Buffer.concat(chunks).toString('utf8');
}
async function requestJson(url, options = {}) {
  const result = await requestUrl(url, options);
  const text = await readResponse(result.response);
  let body;
  try { body = JSON.parse(text); } catch { body = null; }
  return { ...result, body };
}
function tagFromRedirect(location) {
  let parsed;
  try { parsed = new URL(location, RELEASES_LATEST_URL); } catch { return ''; }
  const match = parsed.pathname.match(/^\/yxiaojiang\/LanChat\/releases\/tag\/([^/]+)$/);
  return match ? decodeURIComponent(match[1]) : '';
}

function makeDownloadUrl(tag, name, base = DOWNLOAD_ROOT) {
  return `${base}${encodeURIComponent(tag)}/${encodeURIComponent(name)}`;
}

function createUpdateService(options = {}) {
  const env = options.env || process.env;
  const currentVersion = options.currentVersion || '0.0.0';
  const arch = mapArchitecture(options.arch || process.arch);
  const installed = (options.installed ?? env.LANCHAT_INSTALLED === '1') === true || options.installed === '1';
  const apiUrl = options.apiUrl || API_URL;
  const latestUrl = options.latestUrl || RELEASES_LATEST_URL;
  const downloadRoot = options.downloadRoot || DOWNLOAD_ROOT;
  const requestTimeout = options.requestTimeout ?? REQUEST_TIMEOUT;
  const downloadIdleTimeout = options.downloadIdleTimeout ?? DOWNLOAD_IDLE_TIMEOUT;
  const home = options.homeDir || os.homedir();
  const cacheHome = resolve(options.cacheHome || env.XDG_CACHE_HOME || join(home, '.cache'));
  const updatesDir = resolve(join(cacheHome, 'lanchat', 'updates'));
  const userAgent = `LanChat-Linux/${cleanVersion(currentVersion) || currentVersion}`;
  let lastCheck = null;
  let downloading = false;

  async function latestFromApi() {
    const response = await requestJson(apiUrl, { timeout: requestTimeout,
      headers: { 'User-Agent': userAgent, Accept: 'application/vnd.github+json' } });
    if (response.status === 403) return latestFromRedirect();
    if (response.status < 200 || response.status >= 300 || !response.body) throw new Error(`GitHub Release 请求失败（HTTP ${response.status}）`);
    const tag = String(response.body.tag_name || '');
    const latestVersion = cleanVersion(tag);
    if (!latestVersion) throw new Error('GitHub Release 缺少有效版本号');
    return {
      tag, latestVersion, title: String(response.body.name || tag), notes: String(response.body.body || ''),
      webUrl: githubReleaseUrl(response.body.html_url || `https://github.com/yxiaojiang/LanChat/releases/tag/${encodeURIComponent(tag)}`),
      assets: Array.isArray(response.body.assets) ? response.body.assets : [], fallback: false
    };
  }

  async function latestFromRedirect() {
    const response = await requestJson(latestUrl, {
      timeout: requestTimeout, headers: { 'User-Agent': userAgent }, followRedirects: false
    });
    if (response.status < 300 || response.status >= 400 || !response.headers.location)
      throw new Error(`GitHub Release 降级探测失败（HTTP ${response.status}）`);
    const tag = tagFromRedirect(response.headers.location);
    const latestVersion = cleanVersion(tag);
    if (!latestVersion) throw new Error('GitHub Release 重定向未包含有效 Tag');
    const webUrl = githubReleaseUrl(new URL(response.headers.location, latestUrl).toString());
    return { tag, latestVersion, title: `LanChat v${latestVersion}`, notes: '', webUrl, assets: [], fallback: true };
  }

  function assetUrl(release, name) {
    const asset = release.assets.find(item => item && item.name === name && typeof item.browser_download_url === 'string');
    return asset ? asset.browser_download_url : makeDownloadUrl(release.tag, name, downloadRoot);
  }

  async function readManifest(release, assetName) {
    const manifestUrl = assetUrl(release, 'SHA256SUMS.json');
    const response = await requestJson(manifestUrl, { timeout: requestTimeout,
      headers: { 'User-Agent': userAgent, Accept: 'application/json' } });
    if (response.status < 200 || response.status >= 300 || !response.body)
      throw new Error('无法获取对应 Tag 的 SHA256SUMS.json，已禁止下载');
    const expected = response.body?.[arch]?.artifacts?.[assetName];
    if (typeof expected !== 'string' || !/^[a-f0-9]{64}$/i.test(expected))
      throw new Error(`SHA256SUMS.json 缺少 ${arch}.artifacts.${assetName}，已禁止下载`);
    return { manifestUrl, expected: expected.toLowerCase() };
  }

  async function checkUpdate() {
    const release = await latestFromApi();
    const base = {
      hasUpdate: isNewer(release.latestVersion, currentVersion), currentVersion: cleanVersion(currentVersion),
      latestVersion: release.latestVersion, title: release.title, notes: release.notes, webUrl: release.webUrl,
      kind: installed ? 'deb' : 'portable', assetName: '', downloadUrl: '', sha256Verified: false,
      canDownload: false, warning: '', manifestUrl: ''
    };
    if (!base.hasUpdate) { lastCheck = { release, result: base }; return base; }
    const assetName = assetNameFor(release.latestVersion, arch, installed);
    const result = { ...base, assetName, downloadUrl: assetUrl(release, assetName) };
    try {
      const manifest = await readManifest(release, assetName);
      Object.assign(result, { sha256Verified: true, canDownload: true, manifestUrl: manifest.manifestUrl });
    } catch (error) {
      result.warning = error.message;
    }
    lastCheck = { release, result };
    return result;
  }

  async function downloadUpdate(onProgress = options.onProgress) {
    if (!lastCheck?.result?.hasUpdate) throw new Error('请先检查可用更新');
    if (!lastCheck.result.canDownload || !lastCheck.result.sha256Verified)
      throw new Error(lastCheck.result.warning || '更新包未通过 SHA-256 校验，只允许打开 GitHub Release 页面');
    if (downloading) throw new Error('已有更新下载正在进行');
    downloading = true;
    const savePath = resolve(join(updatesDir, lastCheck.result.assetName));
    const partPath = `${savePath}.part`;
    try {
      // 清单在真正写盘前再次读取，避免检查后资产被替换或清单失配。
      const manifest = await readManifest(lastCheck.release, lastCheck.result.assetName);
      await mkdir(updatesDir, { recursive: true });
      await chmod(updatesDir, 0o700);
      let transferred = 0;
      // 收到响应头后由 requestUrl 清除 8 秒硬截止，改由每块数据刷新空闲计时器。
      const result = await requestUrl(lastCheck.result.downloadUrl, {
        timeout: requestTimeout, idleTimeout: downloadIdleTimeout, stream: true,
        headers: { 'User-Agent': userAgent }
      });
      if (result.status < 200 || result.status >= 300) throw new Error(`更新包下载失败（HTTP ${result.status}）`);
      const total = Number(result.headers['content-length']) || 0;
      const digest = createHash('sha256');
      const transform = new Transform({ transform(chunk, _encoding, callback) {
        result.response.__lanChatIdle?.reset(); transferred += chunk.length; digest.update(chunk);
        onProgress?.({ percent: total ? Math.min(100, Math.round(transferred * 100 / total)) : 0, transferred, total });
        callback(null, chunk);
      } });
      await pipeline(result.response, transform, createWriteStream(partPath, { flags: 'w', mode: 0o600 }));
      if (digest.digest('hex').toLowerCase() !== manifest.expected) throw new Error('更新包 SHA-256 校验失败，坏包已删除');
      await rename(partPath, savePath);
      const normalized = resolve(savePath);
      return { savePath: normalized, sha256Verified: true, installCommand: lastCheck.result.kind === 'deb' ? buildInstallCommand(normalized) : '', assetName: lastCheck.result.assetName };
    } catch (error) {
      await unlink(partPath).catch(() => {});
      throw error;
    } finally {
      downloading = false;
    }
  }

  async function openUpdateLocation(savePath, opener) {
    const normalized = resolve(String(savePath || ''));
    if (!isAbsolute(normalized) || !withinDirectory(updatesDir, normalized) || basename(normalized) !== lastCheck?.result?.assetName)
      throw new Error('更新包路径无效');
    if (!(await stat(normalized)).isFile()) throw new Error('更新包不存在');
    await opener(normalized);
    return normalized;
  }

  async function openReleasePage(url, opener) {
    const safe = githubReleaseUrl(url);
    await opener(safe);
    return safe;
  }

  return { checkUpdate, downloadUpdate, openUpdateLocation, openReleasePage, updatesDir,
    getArchitecture: () => arch, getAssetName: () => assetNameFor(currentVersion, arch, installed) };
}

module.exports = { API_URL, createUpdateService, assetNameFor, buildInstallCommand, cleanVersion,
  isNewer, mapArchitecture, githubReleaseUrl };
