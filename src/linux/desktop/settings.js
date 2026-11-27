// 设置对话框交互控制器。负责侧栏搜索导航、IPC 持久化同步、外观即时应用、只读配置确认、暗色弹层与键盘无障碍。
(function (root, factory) {
  const api = factory(root);
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.LanChatSettingsApp = api;
  if (!(typeof process === 'object' && process?.env?.LANCHAT_TEST_MODE === '1') && typeof document !== 'undefined') {
    api.createSettingsController();
  }
})(typeof globalThis === 'undefined' ? this : globalThis, function (root) {
  const OFFICIAL_RELEASE_URL = 'https://github.com/yxiaojiang/LanChat/releases/latest';

  function normalizeDir(value) {
    const text = String(value || '').trim();
    if (!text) return '';
    return text.length > 1 ? text.replace(/[\\/]+$/, '') : text;
  }

  function createSettingsController(deps = {}) {
    const doc = deps.document || root.document;
    const win = deps.window || root.window || root;
    const api = deps.api !== undefined ? deps.api : win.lanchat;
    const model = deps.model || root.LanChatSettingsModel;
    const pages = deps.pages || root.LanChatSettingsPages;
    const appearance = deps.appearance || root.LanChatAppearance;
    const scroll = deps.scroll || root.LanChatScroll;
    const byId = id => doc.getElementById(id);

    const navEl = byId('settings-nav');
    const searchInput = byId('settings-search');
    const searchClear = byId('settings-search-clear');
    const viewport = byId('settings-viewport');
    const panelsHost = byId('settings-panels');
    const bannerEl = byId('settings-banner');
    const menuEl = byId('settings-menu');

    const committed = {
      nickname: 'LanChat',
      autoAcceptFiles: true,
      downloadDirectory: 'ReceivedFiles',
      historyDirectory: 'ChatHistory',
      settingsPath: '',
      themeMode: 0,
      accentPreset: 0,
      uiFontFamily: 'Microsoft YaHei UI',
      uiFontSize: 10.0,
      chatFontFamily: 'Microsoft YaHei UI',
      chatFontSize: 10.5,
      keepAliveInTray: true,
      trayPreview: true,
      tunFilter: true,
      unicastProbe: true,
      autoLoadHistory: true,
      sendShortcutMode: 0
    };

    const runtime = {
      id: '0000-0000',
      ip: '127.0.0.1',
      tcpPort: 38889,
      hostname: 'localhost',
      osText: 'Linux',
      arch: 'x64',
      version: '3.0.0',
      electronVersion: '45.4.1',
      interfaces: [],
      connected: false
    };

    const updateState = {
      webUrl: OFFICIAL_RELEASE_URL, savePath: '', installCommand: '', canDownload: false, downloading: false
    };

    // 与 WinForms 面板一致的展示态（语言、链接打开方式及展示型开关等本地 UI 状态）。
    const display = {
      languagePill: model.LANGUAGES[0].pill,
      linkPill: model.LINK_MODES[0].pill,
      chunkCheck: true
    };

    let activeTab = 0;
    let saving = false;
    let queuedAppearance = null;
    let pendingAppearance = null;
    let menuRestoreTarget = null;
    let bannerTimer = 0;
    let navScroll = null;
    let pageScroll = null;
    let feedbackScroll = null;

    // 跨页联动控件引用（通用与文件传输共享自动接收/接收目录，通用与外观共享主界面字号）。
    const syncRefs = {
      autoSwitches: [],
      downloadDescs: [],
      historyDescs: [],
      uiSliders: [],
      chatSliders: [],
      themeLabels: [],
      accentLabels: [],
      uiFontLabels: [],
      chatFontLabels: [],
      nickLabels: [],
      traySwitches: [],
      previewSwitches: [],
      discoverySwitches: [],
      unicastSwitches: [],
      historyLoadSwitches: [],
      shortcutModeLabels: [],
      shortcutBadges: []
    };

    function showBanner(message, isError = false, autoHideMs = 0) {
      if (bannerTimer) {
        clearTimeout(bannerTimer);
        bannerTimer = 0;
      }
      if (!message) {
        bannerEl.hidden = true;
        bannerEl.textContent = '';
        bannerEl.classList.remove('error');
        return;
      }
      bannerEl.textContent = message;
      bannerEl.classList.toggle('error', isError);
      bannerEl.hidden = false;
      if (autoHideMs > 0) {
        bannerTimer = setTimeout(() => showBanner(''), autoHideMs);
      }
    }

    function applyAppearanceState(source = committed) {
      const normalized = appearance.normalizeSettings(source);
      appearance.applyAppearance(doc.documentElement, normalized);
      syncAppearanceControls(normalized);
    }

    function syncAppearanceControls(source = committed) {
      const normalized = appearance.normalizeSettings(source);
      for (const el of syncRefs.themeLabels) el.textContent = model.THEMES[normalized.themeMode];
      for (const el of syncRefs.accentLabels) el.textContent = model.ACCENTS[normalized.accentPreset].label;
      for (const el of syncRefs.uiFontLabels) el.textContent = appearance.fontDisplayLabel(normalized.uiFontFamily);
      for (const el of syncRefs.chatFontLabels) el.textContent = appearance.fontDisplayLabel(normalized.chatFontFamily);
      const uiIdx = model.sliderIndexForValue(model.UI_FONT_SIZES, normalized.uiFontSize);
      for (const slider of syncRefs.uiSliders) slider.setIndex(uiIdx);
      const chatIdx = model.sliderIndexForValue(model.CHAT_FONT_SIZES, normalized.chatFontSize);
      for (const slider of syncRefs.chatSliders) slider.setIndex(chatIdx);
    }

    function closeMenu(restoreFocus = true) {
      if (menuEl.hidden) return;
      menuEl.hidden = true;
      menuEl.replaceChildren();
      const target = menuRestoreTarget;
      menuRestoreTarget = null;
      if (restoreFocus && target && target.isConnected) target.focus();
    }

    function openMenu(anchor, items) {
      closeMenu(false);
      menuRestoreTarget = anchor;
      menuEl.replaceChildren();
      const buttons = [];
      for (const item of items) {
        const button = doc.createElement('button');
        button.type = 'button';
        button.setAttribute('role', 'menuitem');
        if (item.swatch) {
          const dot = doc.createElement('span');
          dot.className = 'menu-swatch';
          dot.style.backgroundColor = item.swatch;
          button.append(dot);
        }
        const label = doc.createElement('span');
        label.textContent = item.label;
        button.append(label);
        if (item.checked) {
          const check = doc.createElement('span');
          check.className = 'menu-check';
          check.textContent = '✓';
          button.append(check);
        }
        button.addEventListener('click', () => {
          closeMenu(true);
          item.run();
        });
        menuEl.append(button);
        buttons.push(button);
      }
      menuEl.hidden = false;
      const anchorRect = anchor.getBoundingClientRect();
      const menuRect = menuEl.getBoundingClientRect();
      const left = Math.max(8, Math.min(anchorRect.left, win.innerWidth - menuRect.width - 8));
      let top = anchorRect.bottom + 4;
      if (top + menuRect.height > win.innerHeight - 8) {
        top = Math.max(8, anchorRect.top - menuRect.height - 4);
      }
      menuEl.style.left = `${left}px`;
      menuEl.style.top = `${top}px`;
      buttons[0]?.focus();
    }

    function openConfirmModal({ title, body, confirmLabel = '确定', cancelLabel = '取消', danger = false, alertOnly = false }) {
      const dialog = byId('settings-confirm-dialog');
      if (dialog.open) return Promise.resolve(false);
      const previousFocus = doc.activeElement;
      byId('settings-confirm-title').textContent = title;
      byId('settings-confirm-body').textContent = body;
      const acceptBtn = byId('settings-confirm-accept');
      const cancelBtn = byId('settings-confirm-cancel');
      acceptBtn.textContent = confirmLabel;
      acceptBtn.className = danger ? 'btn-primary danger' : 'btn-primary';
      cancelBtn.textContent = cancelLabel;
      cancelBtn.hidden = alertOnly;
      dialog.returnValue = 'cancel';
      dialog.showModal();
      (alertOnly ? acceptBtn : cancelBtn).focus();
      return new Promise(resolve => {
        dialog.addEventListener('close', () => {
          if (previousFocus && previousFocus.isConnected) previousFocus.focus();
          resolve(dialog.returnValue === 'ok');
        }, { once: true });
      });
    }

    async function copyToClipboard(text) {
      if (!text) return false;
      try {
        if (win.navigator?.clipboard?.writeText) {
          await win.navigator.clipboard.writeText(text);
          return true;
        }
      } catch {}
      const helper = doc.createElement('textarea');
      helper.value = text;
      helper.style.position = 'fixed';
      helper.style.opacity = '0';
      doc.body.append(helper);
      helper.select();
      const ok = typeof doc.execCommand === 'function' ? doc.execCommand('copy') : false;
      helper.remove();
      return ok;
    }

    // 所有设置写回统一经此入口处理，保证只读配置备份与二次确认链路与 DesktopService 一致。
    async function persistSettings(patch) {
      if (!api) {
        showBanner('桌面桥接不可用，当前仅展示界面布局', true, 3000);
        return { ok: false, error: '桌面桥接不可用' };
      }
      if (saving) return { ok: false, error: '正在保存上一项设置，请稍候' };
      saving = true;
      const previousHistory = committed.historyDirectory;
      const nextNick = patch.nickname !== undefined ? patch.nickname : committed.nickname;
      const nextAuto = patch.autoAcceptFiles !== undefined ? patch.autoAcceptFiles : committed.autoAcceptFiles;
      const nextDownload = patch.downloadDirectory !== undefined ? patch.downloadDirectory : null;
      const nextHistory = patch.historyDirectory !== undefined ? patch.historyDirectory : null;
      const nextKeepAlive = patch.keepAliveInTray !== undefined ? Boolean(patch.keepAliveInTray) : committed.keepAliveInTray;
      const nextTrayPreview = patch.trayPreview !== undefined ? Boolean(patch.trayPreview) : committed.trayPreview;
      const nextTunFilter = patch.tunFilter !== undefined ? Boolean(patch.tunFilter) : committed.tunFilter;
      const nextUnicastProbe = patch.unicastProbe !== undefined ? Boolean(patch.unicastProbe) : committed.unicastProbe;
      const nextAutoLoadHistory = patch.autoLoadHistory !== undefined ? Boolean(patch.autoLoadHistory) : committed.autoLoadHistory;
      const nextSendShortcutMode = patch.sendShortcutMode !== undefined ? Number(patch.sendShortcutMode) : committed.sendShortcutMode;
      const appearancePayload = appearance.normalizeSettings({ ...committed, ...patch });
      const trayPayload = { keepAliveInTray: nextKeepAlive, trayPreview: nextTrayPreview };
      const networkPayload = {
        tunFilter: nextTunFilter, unicastProbe: nextUnicastProbe,
        autoLoadHistory: nextAutoLoadHistory, sendShortcutMode: nextSendShortcutMode
      };
      try {
        let result = await api.saveSettings(nextNick, nextAuto, nextDownload, false, nextHistory, appearancePayload, trayPayload, networkPayload);
        if (result?.overridePath) {
          const confirmed = await openConfirmModal({
            title: '原设置只读',
            body: `原设置只读，已备份到\n${result.backupPath}\n\n是否改用以下备用位置保存修改？\n${result.overridePath}`,
            confirmLabel: '使用备用位置',
            cancelLabel: '取消'
          });
          if (!confirmed) {
            pendingAppearance = null;
            queuedAppearance = null;
            applyAppearanceState(committed);
            syncBoundControls();
            showBanner('已取消更改，原只读设置保持不变', false, 3500);
            return { ok: false, cancelled: true };
          }
          result = await api.saveSettings(nextNick, nextAuto, nextDownload, true, nextHistory, appearancePayload, trayPayload, networkPayload);
        }
        Object.assign(committed, result);
        pendingAppearance = null;
        applyAppearanceState(committed);
        syncBoundControls();
        const sameHistory = Boolean(nextHistory) && normalizeDir(nextHistory) === normalizeDir(previousHistory);
        const migratedHistory = Boolean(nextHistory) && !sameHistory &&
          normalizeDir(result?.historyDirectory) !== normalizeDir(previousHistory);
        const message = migratedHistory
          ? '聊天记录已迁移并保存设置'
          : (sameHistory ? '所选目录与当前目录相同，无需迁移' : '设置已保存');
        showBanner(message, false, 2400);
        return { ok: true, result };
      } catch (error) {
        pendingAppearance = null;
        queuedAppearance = null;
        applyAppearanceState(committed);
        syncBoundControls();
        showBanner(error.message, true);
        return { ok: false, error: error.message };
      } finally {
        saving = false;
      }
    }

    async function updateAppearance(patch) {
      const nextPreview = appearance.normalizeSettings({ ...(pendingAppearance || committed), ...patch });
      pendingAppearance = nextPreview;
      applyAppearanceState(nextPreview);
      if (!api) {
        Object.assign(committed, nextPreview);
        pendingAppearance = null;
        showBanner('桌面桥接不可用，当前仅展示界面布局', true, 3000);
        return { ok: false, error: '桌面桥接不可用' };
      }
      if (saving) {
        queuedAppearance = { ...(queuedAppearance || {}), ...patch };
        return { ok: true, queued: true };
      }
      let res = await persistSettings(nextPreview);
      while (queuedAppearance) {
        const nextPatch = queuedAppearance;
        queuedAppearance = null;
        const merged = appearance.normalizeSettings({ ...committed, ...nextPatch });
        res = await persistSettings(merged);
        if (!res.ok) break;
      }
      return res;
    }

    function syncBoundControls() {
      for (const el of syncRefs.autoSwitches) {
        el.setAttribute('aria-checked', String(Boolean(committed.autoAcceptFiles)));
      }
      for (const el of syncRefs.downloadDescs) {
        el.textContent = model.formatShortPath(committed.downloadDirectory);
        el.title = committed.downloadDirectory || '';
      }
      for (const el of syncRefs.historyDescs) {
        el.textContent = model.formatShortPath(committed.historyDirectory);
        el.title = committed.historyDirectory || '';
      }
      for (const el of syncRefs.nickLabels) {
        el.textContent = committed.nickname || 'LanChat';
      }
      for (const el of syncRefs.traySwitches) {
        el.setAttribute('aria-checked', String(Boolean(committed.keepAliveInTray)));
      }
      for (const el of syncRefs.previewSwitches) {
        el.setAttribute('aria-checked', String(Boolean(committed.trayPreview)));
      }
      for (const el of syncRefs.discoverySwitches) {
        el.setAttribute('aria-checked', String(Boolean(committed.tunFilter)));
      }
      for (const el of syncRefs.unicastSwitches) {
        el.setAttribute('aria-checked', String(Boolean(committed.unicastProbe)));
      }
      for (const el of syncRefs.historyLoadSwitches) {
        el.setAttribute('aria-checked', String(Boolean(committed.autoLoadHistory)));
      }
      for (const el of syncRefs.shortcutModeLabels) {
        el.textContent = committed.sendShortcutMode === 1 ? 'Ctrl + Enter 发送' : 'Enter 发送';
      }
      for (const [index, el] of syncRefs.shortcutBadges.entries()) {
        el.textContent = index === 0
          ? (committed.sendShortcutMode === 1 ? 'Ctrl + Enter' : 'Enter')
          : (committed.sendShortcutMode === 1 ? 'Enter' : 'Ctrl + Enter / Shift + Enter');
      }
      syncAppearanceControls(pendingAppearance || committed);
    }

    async function pickDirectory(kind) {
      if (!api) {
        showBanner('桌面桥接不可用，无法打开系统目录选择框', true, 3000);
        return;
      }
      try {
        const folder = await api.selectSettingsDirectory(kind);
        if (!folder) return;
        await persistSettings(kind === 'history' ? { historyDirectory: folder } : { downloadDirectory: folder });
      } catch (error) {
        showBanner(error.message, true);
      }
    }

    async function toggleAutoAccept(next) {
      const res = await persistSettings({ autoAcceptFiles: next });
      if (!res.ok) syncBoundControls();
    }

    async function toggleKeepAlive(next) {
      const res = await persistSettings({ keepAliveInTray: next });
      if (!res.ok) syncBoundControls();
    }

    async function toggleTrayPreview(next) {
      const res = await persistSettings({ trayPreview: next });
      if (!res.ok) syncBoundControls();
    }

    async function toggleTunFilter(next) {
      const res = await persistSettings({ tunFilter: next });
      if (!res.ok) syncBoundControls();
    }

    async function toggleUnicastProbe(next) {
      const res = await persistSettings({ unicastProbe: next });
      if (!res.ok) syncBoundControls();
    }

    async function toggleAutoLoadHistory(next) {
      const res = await persistSettings({ autoLoadHistory: next });
      if (!res.ok) syncBoundControls();
    }

    async function setSendShortcutMode(mode) {
      const res = await persistSettings({ sendShortcutMode: mode });
      if (!res.ok) syncBoundControls();
    }

    async function probeIp(input, output, button) {
      const value = String(input.value || '').trim();
      const parts = value.split('.');
      const valid = parts.length === 4 && parts.every(part => /^\d{1,3}$/.test(part) && Number(part) <= 255);
      if (!valid) {
        output.textContent = '请输入有效的 IPv4 地址';
        output.classList.add('error');
        return;
      }
      if (!api?.probeIp) {
        output.textContent = '当前桌面桥接不可用';
        output.classList.add('error');
        return;
      }
      button.disabled = true;
      output.classList.remove('error');
      output.textContent = '正在探测…';
      try {
        const result = await api.probeIp(value);
        output.textContent = result?.message || '探测请求已发送';
        output.classList.toggle('error', result?.ok === false);
      } catch (error) {
        output.textContent = error.message;
        output.classList.add('error');
      } finally {
        button.disabled = false;
      }
    }

    async function openDownloads() {
      if (!api) return;
      try {
        await api.openDownloads();
      } catch (error) {
        showBanner(error.message, true);
      }
    }

    async function openHistory() {
      if (!api) return;
      try {
        await api.openHistory();
      } catch (error) {
        showBanner(error.message, true);
      }
    }

    async function clearAllHistories(btn) {
      const accepted = await openConfirmModal({
        title: '清空记录确认',
        body: '确定要彻底清空本机所有本地聊天记录吗？此操作将删除所有往期历史且无法撤销。',
        confirmLabel: '立即清空',
        danger: true
      });
      if (!accepted) return;
      if (!api) {
        showBanner('桌面桥接不可用，无法清空历史', true, 3000);
        return;
      }
      try {
        await api.clearAllHistories();
        btn.textContent = '已清空 ✓';
        showBanner('已彻底清空所有本地聊天记录', false, 2400);
      } catch (error) {
        showBanner(error.message, true);
      }
    }

    function openNickDialog() {
      const dialog = byId('settings-nick-dialog');
      if (dialog.open) return;
      const input = byId('nick-input');
      const errorEl = byId('nick-error');
      const previousFocus = doc.activeElement;
      input.value = committed.nickname || '';
      errorEl.hidden = true;
      errorEl.textContent = '';
      dialog.showModal();
      input.focus();
      input.select();
      dialog.addEventListener('close', () => {
        if (previousFocus && previousFocus.isConnected) previousFocus.focus();
      }, { once: true });
    }

    function openFeedbackDialog() {
      const dialog = byId('settings-feedback-dialog');
      if (dialog.open) return;
      const input = byId('feedback-input');
      const errorEl = byId('feedback-error');
      const previousFocus = doc.activeElement;
      input.value = '';
      errorEl.hidden = true;
      errorEl.textContent = '';
      dialog.showModal();
      feedbackScroll?.refresh();
      input.focus();
      dialog.addEventListener('close', () => {
        if (previousFocus && previousFocus.isConnected) previousFocus.focus();
      }, { once: true });
    }

    function updateProgress(data = {}) {
      const bar = byId('settings-update-progress-bar');
      const percent = Number.isFinite(Number(data.percent)) ? Math.max(0, Math.min(100, Number(data.percent))) : 0;
      if (bar) bar.style.width = `${percent}%`;
      const status = byId('settings-update-status');
      if (status && updateState.downloading) {
        const transferred = Number(data.transferred) || 0;
        const total = Number(data.total) || 0;
        status.textContent = total ? `正在下载更新… ${percent}%（${transferred} / ${total} 字节）` : `正在下载更新… ${transferred} 字节`;
      }
    }

    function showUpdateNotice() {
      const dialog = byId('settings-update-dialog');
      if (!dialog) {
        showBanner('更新弹窗不可用', true);
        return;
      }
      if (dialog.open) return;
      updateState.webUrl = OFFICIAL_RELEASE_URL;
      updateState.savePath = '';
      updateState.installCommand = '';
      updateState.canDownload = false;
      updateState.downloading = false;
      byId('settings-update-title').textContent = '检查更新';
      byId('settings-update-version').textContent = `当前版本 v${runtime.version}`;
      byId('settings-update-status').textContent = '正在检查 GitHub Release...';
      byId('settings-update-warning').textContent = '';
      byId('settings-update-warning').hidden = true;
      byId('settings-update-notes').textContent = '正在读取...';
      byId('settings-update-progress-bar').style.width = '0%';
      byId('settings-update-download').hidden = false;
      byId('settings-update-download').disabled = true;
      byId('settings-update-copy-install').hidden = true;
      byId('settings-update-open-location').hidden = true;
      dialog.showModal();
      void checkForUpdates();
    }

    async function checkForUpdates() {
      const status = byId('settings-update-status');
      const warning = byId('settings-update-warning');
      const download = byId('settings-update-download');
      if (!api?.checkUpdate) {
        status.textContent = '当前桌面桥接不可用';
        warning.textContent = '无法检查更新，只能打开 GitHub Release 页面。';
        warning.hidden = false;
        download.hidden = true;
        return;
      }
      try {
        const result = await api.checkUpdate();
        updateState.webUrl = result.webUrl || OFFICIAL_RELEASE_URL;
        updateState.canDownload = result.canDownload === true;
        byId('settings-update-version').textContent = `当前 v${result.currentVersion} · 最新 v${result.latestVersion}`;
        byId('settings-update-title').textContent = result.hasUpdate ? (result.title || '发现新版本') : '当前已是最新版本';
        byId('settings-update-notes').textContent = result.notes || '此 Release 未提供更新日志。';
        if (!result.hasUpdate) {
          status.textContent = `当前已是最新版本 (v${result.currentVersion})，无需更新`;
          download.hidden = true;
          return;
        }
        if (!result.canDownload) {
          status.textContent = '已发现新版本，但未取得可验证的更新清单。';
          warning.textContent = result.warning || 'SHA256SUMS.json 不完整，只允许打开 GitHub Release 网页。';
          warning.hidden = false;
          download.hidden = true;
          return;
        }
        status.textContent = `发现 ${result.kind === 'deb' ? '.deb 安装包' : '便携包'}：${result.assetName}`;
        download.textContent = `⚡ 立即下载更新（${result.kind === 'deb' ? '.deb' : '便携包'}）`;
        download.disabled = false;
      } catch (error) {
        status.textContent = '检查更新失败';
        warning.textContent = error.message;
        warning.hidden = false;
        download.hidden = true;
      }
    }

    async function downloadUpdate() {
      const download = byId('settings-update-download');
      if (!api?.downloadUpdate || updateState.downloading) return;
      updateState.downloading = true;
      download.disabled = true;
      byId('settings-update-copy-install').hidden = true;
      byId('settings-update-open-location').hidden = true;
      byId('settings-update-status').textContent = '正在准备下载...';
      try {
        const result = await api.downloadUpdate();
        updateState.savePath = result.savePath || '';
        updateState.installCommand = result.installCommand || '';
        byId('settings-update-status').textContent = `下载完成：${result.assetName || updateState.savePath}`;
        byId('settings-update-open-location').hidden = !updateState.savePath;
        byId('settings-update-copy-install').hidden = !updateState.installCommand;
      } catch (error) {
        byId('settings-update-status').textContent = '下载失败，未保留坏包';
        byId('settings-update-warning').textContent = error.message;
        byId('settings-update-warning').hidden = false;
      } finally {
        updateState.downloading = false;
        download.disabled = false;
      }
    }

    async function openReleasePage() {
      if (!api?.openReleasePage) return;
      try { await api.openReleasePage(updateState.webUrl || OFFICIAL_RELEASE_URL); } catch (error) { showBanner(error.message, true); }
    }

    async function openUpdateLocation() {
      if (!updateState.savePath || !api?.openUpdateLocation) return;
      try { await api.openUpdateLocation(updateState.savePath); } catch (error) { showBanner(error.message, true); }
    }

    async function copyInstallAndQuit() {
      if (!updateState.installCommand || !api?.quit) return;
      if (!await copyToClipboard(updateState.installCommand)) {
        showBanner('复制安装命令失败，请手动复制后再退出', true);
        return;
      }
      byId('settings-update-status').textContent = '安装命令已复制，正在退出 LanChat 以释放配置锁...';
      api.quit();
    }

    function renderAllPanels() {
      syncRefs.autoSwitches = [];
      syncRefs.downloadDescs = [];
      syncRefs.historyDescs = [];
      syncRefs.uiSliders = [];
      syncRefs.chatSliders = [];
      syncRefs.themeLabels = [];
      syncRefs.accentLabels = [];
      syncRefs.uiFontLabels = [];
      syncRefs.chatFontLabels = [];
      syncRefs.nickLabels = [];
      syncRefs.traySwitches = [];
      syncRefs.previewSwitches = [];
      syncRefs.discoverySwitches = [];
      syncRefs.unicastSwitches = [];
      syncRefs.historyLoadSwitches = [];
      syncRefs.shortcutModeLabels = [];
      syncRefs.shortcutBadges = [];
      const built = pages.buildAll({
        committed,
        runtime,
        display,
        syncRefs,
        activeTab,
        hasBridge: Boolean(api),
        actions: {
          openMenu,
          updateAppearance: patch => { void updateAppearance(patch); },
          pickDirectory: kind => { void pickDirectory(kind); },
          openDownloads: () => { void openDownloads(); },
          openHistory: () => { void openHistory(); },
          toggleAutoAccept: next => { void toggleAutoAccept(next); },
          toggleKeepAlive: next => { void toggleKeepAlive(next); },
          toggleTrayPreview: next => { void toggleTrayPreview(next); },
          toggleTunFilter: next => { void toggleTunFilter(next); },
          toggleUnicastProbe: next => { void toggleUnicastProbe(next); },
          toggleAutoLoadHistory: next => { void toggleAutoLoadHistory(next); },
          setSendShortcutMode: mode => { void setSendShortcutMode(mode); },
          refreshNetwork: async () => {
            if (!api?.refresh) return;
            try {
              await api.refresh();
              const state = await api.state();
              if (state) {
                runtime.ip = state.ip || runtime.ip;
                runtime.tcpPort = state.tcpPort || runtime.tcpPort;
                runtime.interfaces = Array.isArray(state.interfaces) ? state.interfaces : [];
              }
              renderAllPanels();
            } catch (error) {
              showBanner(error.message, true);
            }
          },
          probeIp: (input, output, button) => { void probeIp(input, output, button); },
          clearAllHistories: btn => { void clearAllHistories(btn); },
          openNickDialog,
          openFeedbackDialog,
          showUpdateNotice,
          copyText: copyToClipboard
        }
      });
      panelsHost.replaceChildren(...built);
      syncBoundControls();
      pageScroll?.refresh();
    }

    function renderNav() {
      navEl.replaceChildren();
      const items = model.filterNavItems(searchInput.value);
      if (!items.length) {
        const empty = doc.createElement('div');
        empty.className = 'nav-empty';
        empty.textContent = '未找到匹配的设置项';
        navEl.append(empty);
        navScroll?.refresh();
        return;
      }
      let lastGroup = '';
      for (const item of items) {
        if (item.group !== lastGroup) {
          lastGroup = item.group;
          const groupLabel = doc.createElement('div');
          groupLabel.className = 'nav-group-label';
          groupLabel.textContent = item.group;
          navEl.append(groupLabel);
        }
        const btn = doc.createElement('button');
        btn.type = 'button';
        btn.className = 'nav-item';
        btn.dataset.tabId = String(item.id);
        btn.dataset.icon = item.icon;
        btn.setAttribute('role', 'tab');
        btn.setAttribute('aria-selected', String(item.id === activeTab));
        const icon = doc.createElement('img');
        icon.className = 'nav-icon';
        icon.src = `assets/${item.icon}${item.id === activeTab ? 'Active' : ''}.png`;
        icon.alt = '';
        const label = doc.createElement('span');
        label.textContent = item.title;
        btn.append(icon, label);
        btn.addEventListener('click', () => switchTab(item.id));
        navEl.append(btn);
      }
      navScroll?.refresh();
    }

    function switchTab(tabId) {
      if (tabId < 0 || tabId > 7) return;
      closeMenu(false);
      activeTab = tabId;
      for (const btn of navEl.querySelectorAll('.nav-item')) {
        const selected = Number(btn.dataset.tabId) === activeTab;
        btn.setAttribute('aria-selected', String(selected));
        const icon = btn.querySelector('.nav-icon');
        if (icon && btn.dataset.icon) {
          icon.src = `assets/${btn.dataset.icon}${selected ? 'Active' : ''}.png`;
        }
      }
      for (const page of panelsHost.querySelectorAll('.settings-page')) {
        page.hidden = Number(page.dataset.tabId) !== activeTab;
      }
      viewport.scrollTop = 0;
      pageScroll?.refresh();
    }

    navEl.addEventListener('keydown', event => {
      if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
      const items = [...navEl.querySelectorAll('.nav-item')];
      if (!items.length) return;
      const currentIdx = items.indexOf(doc.activeElement);
      const nextIdx = currentIdx < 0
        ? 0
        : event.key === 'ArrowDown'
          ? (currentIdx + 1) % items.length
          : (currentIdx - 1 + items.length) % items.length;
      event.preventDefault();
      const target = items[nextIdx];
      target.focus();
      switchTab(Number(target.dataset.tabId));
    });

    menuEl.addEventListener('keydown', event => {
      const buttons = [...menuEl.querySelectorAll('button')];
      if (!buttons.length) return;
      const idx = buttons.indexOf(doc.activeElement);
      if (event.key === 'Escape') {
        event.preventDefault();
        event.stopPropagation();
        closeMenu(true);
      } else if (event.key === 'ArrowDown' || (event.key === 'Tab' && !event.shiftKey)) {
        event.preventDefault();
        buttons[(idx < 0 ? 0 : idx + 1) % buttons.length]?.focus();
      } else if (event.key === 'ArrowUp' || (event.key === 'Tab' && event.shiftKey)) {
        event.preventDefault();
        buttons[(idx <= 0 ? buttons.length : idx) - 1]?.focus();
      } else if (event.key === 'Home') {
        event.preventDefault();
        buttons[0]?.focus();
      } else if (event.key === 'End') {
        event.preventDefault();
        buttons.at(-1)?.focus();
      }
    });

    doc.addEventListener('pointerdown', event => {
      if (!menuEl.hidden && !menuEl.contains(event.target) && !menuRestoreTarget?.contains(event.target)) {
        closeMenu(false);
      }
    });

    win.addEventListener('resize', () => {
      closeMenu(false);
      pageScroll?.refresh();
      navScroll?.refresh();
      feedbackScroll?.refresh();
    });

    doc.addEventListener('keydown', event => {
      if (event.key !== 'Escape') return;
      if (!menuEl.hidden) {
        event.preventDefault();
        closeMenu(true);
        return;
      }
      if (byId('settings-nick-dialog').open || byId('settings-feedback-dialog').open || byId('settings-confirm-dialog').open ||
          byId('settings-update-dialog')?.open) {
        return;
      }
      if (doc.activeElement === searchInput && searchInput.value) {
        event.preventDefault();
        searchInput.value = '';
        searchClear.hidden = true;
        renderNav();
        return;
      }
      if (api?.closeSettings) {
        event.preventDefault();
        void api.closeSettings();
      }
    });

    searchInput.addEventListener('input', () => {
      searchClear.hidden = !searchInput.value;
      renderNav();
    });

    searchClear.addEventListener('click', () => {
      searchInput.value = '';
      searchClear.hidden = true;
      renderNav();
      searchInput.focus();
    });

    byId('nick-cancel').addEventListener('click', () => byId('settings-nick-dialog').close());
    byId('nick-form').addEventListener('submit', async event => {
      event.preventDefault();
      const check = model.validateNickname(byId('nick-input').value);
      const errorEl = byId('nick-error');
      if (!check.ok) {
        errorEl.textContent = check.error;
        errorEl.hidden = false;
        return;
      }
      errorEl.hidden = true;
      const res = await persistSettings({ nickname: check.value });
      if (res.ok) {
        byId('settings-nick-dialog').close();
      } else {
        errorEl.textContent = res.cancelled ? '已取消更改，原只读设置保持不变' : (res.error || '保存失败');
        errorEl.hidden = false;
      }
    });

    byId('feedback-cancel').addEventListener('click', () => byId('settings-feedback-dialog').close());
    byId('feedback-form').addEventListener('submit', event => {
      event.preventDefault();
      const errorEl = byId('feedback-error');
      if (!byId('feedback-input').value.trim()) {
        errorEl.textContent = '请先输入您的反馈或问题描述。';
        errorEl.hidden = false;
        return;
      }
      byId('settings-feedback-dialog').close();
      void openConfirmModal({
        title: '反馈成功',
        body: '感谢您的宝贵反馈！我们将持续精进优化 LanChat。',
        confirmLabel: '知道了',
        alertOnly: true
      });
    });

    byId('settings-confirm-cancel').addEventListener('click', () => byId('settings-confirm-dialog').close('cancel'));
    byId('settings-confirm-accept').addEventListener('click', event => {
      event.preventDefault();
      byId('settings-confirm-dialog').close('ok');
    });
    byId('settings-update-later')?.addEventListener('click', () => byId('settings-update-dialog').close());
    byId('settings-update-download')?.addEventListener('click', () => { void downloadUpdate(); });
    byId('settings-update-release')?.addEventListener('click', () => { void openReleasePage(); });
    byId('settings-update-open-location')?.addEventListener('click', () => { void openUpdateLocation(); });
    byId('settings-update-copy-install')?.addEventListener('click', () => { void copyInstallAndQuit(); });

    async function bootstrap() {
      navScroll = scroll.bind(navEl);
      pageScroll = scroll.bind(viewport);
      feedbackScroll = scroll.bind(byId('feedback-input'));
      applyAppearanceState(committed);
      renderNav();
      renderAllPanels();
      if (!api) return;
      api.onEvent?.(event => {
        if (event?.event === 'open-update-check') showUpdateNotice();
        if (event?.event === 'update-progress') updateProgress(event.data);
      });
      try {
        const [settingsData, stateData, sysData] = await Promise.all([
          api.settings(),
          api.state(),
          api.systemInfo()
        ]);
        if (settingsData) Object.assign(committed, appearance.normalizeSettings(settingsData), settingsData);
        if (stateData) {
          runtime.id = stateData.id || runtime.id;
          runtime.ip = stateData.ip || runtime.ip;
          runtime.tcpPort = stateData.tcpPort || runtime.tcpPort;
            runtime.interfaces = Array.isArray(stateData.interfaces) ? stateData.interfaces : [];
          runtime.connected = true;
        }
        if (sysData) {
          runtime.hostname = sysData.hostname || runtime.hostname;
          runtime.osText = sysData.osText || runtime.osText;
          runtime.arch = sysData.arch || runtime.arch;
          runtime.version = sysData.version || runtime.version;
          runtime.electronVersion = sysData.electronVersion || runtime.electronVersion;
        }
        applyAppearanceState(committed);
        renderAllPanels();
      } catch (error) {
        showBanner(error.message, true);
      }
    }

    const ready = bootstrap();
    return {
      ready,
      committed,
      runtime,
      persistSettings,
      updateAppearance,
      pickDirectory,
      openFeedbackDialog,
      applyAppearanceState
    };
  }

  return { createSettingsController, normalizeDir };
});
