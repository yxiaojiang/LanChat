// 设置对话框的纯数据模型与展示规则。与 WinForms 八个设置面板保持同步，不依赖 DOM 或 IPC。
(function (root, factory) {
  const api = factory(root);
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.LanChatSettingsModel = api;
})(typeof globalThis === 'undefined' ? this : globalThis, function (root) {
  const appearance = root.LanChatAppearance || (typeof require === 'function' ? require('./appearance.js') : null);

  // 侧边栏导航条目定义，顺序、分组及图标与 GrokSettingsDialog.cs 保持一致。
  const NAV_ITEMS = [
    { id: 0, title: '通用', icon: 'Settings', group: '设置' },
    { id: 1, title: '个人主页', icon: 'User', group: '设置' },
    { id: 2, title: '外观', icon: 'Emoji', group: '设置' },
    { id: 3, title: '快捷键', icon: 'Menu', group: '设置' },
    { id: 4, title: '文件传输', icon: 'Folder', group: '功能' },
    { id: 5, title: '聊天记录', icon: 'Clear', group: '功能' },
    { id: 6, title: '设备连接', icon: 'Contacts', group: '网络与系统' },
    { id: 7, title: '关于与更新', icon: 'Refresh', group: '关于' }
  ];

  // 通用页展示型预设选项与外观持久化预设选项（与 WinForms 面板保持一致）。
  const LANGUAGES = [
    { label: '跟随系统 (简体中文)', pill: '跟随系统 (简体中文)' },
    { label: 'English (US)', pill: 'English (US)' },
    { label: '繁体中文 (中国香港)', pill: '繁体中文' }
  ];

  const LINK_MODES = [
    { label: '智能打开 (默认浏览器)', pill: '智能打开' },
    { label: '内置极速预览', pill: '内置极速预览' }
  ];

  const THEMES = appearance.THEMES;
  const ACCENTS = appearance.ACCENTS;
  const UI_FONT_PRESETS = appearance.UI_FONT_PRESETS;
  const CHAT_FONT_PRESETS = appearance.CHAT_FONT_PRESETS;
  const UI_FONT_STYLES = UI_FONT_PRESETS.map(item => item.label);
  const CHAT_FONT_STYLES = CHAT_FONT_PRESETS.map(item => item.label);
  const SIZE_LABELS = appearance.SIZE_LABELS;
  const UI_FONT_SIZES = appearance.UI_FONT_SIZES;
  const CHAT_FONT_SIZES = appearance.CHAT_FONT_SIZES;

  // 快捷键页只读展示条目，与主窗实际输入按键一致。
  const SHORTCUTS = [
    { title: '发送消息', keys: 'Enter' },
    { title: '多行换行输入', keys: 'Ctrl + Enter / Shift + Enter' },
    { title: '快速搜索联系人', keys: 'Ctrl + F' },
    { title: '清空当前聊天', keys: 'Ctrl + L' }
  ];

  // 按侧边栏搜索关键词过滤设置页条目，保持大小写不敏感。
  function filterNavItems(query) {
    const needle = String(query || '').trim().toLocaleLowerCase();
    if (!needle) return NAV_ITEMS.slice();
    return NAV_ITEMS.filter(item => item.title.toLocaleLowerCase().includes(needle));
  }

  // 长路径保留末尾目录名，与 WinForms 44 字符截断口径一致。
  function formatShortPath(value) {
    const text = String(value || '');
    return text.length > 44 ? '...' + text.slice(text.length - 41) : text;
  }

  // 校验昵称是否满足 LinuxProfile 与 LANC 报文约束（1~80 字符、不含竖线与换行）。
  function validateNickname(value) {
    const trimmed = typeof value === 'string' ? value.trim() : '';
    if (!trimmed || trimmed.length > 80 || trimmed.includes('|') || trimmed.includes('\r') || trimmed.includes('\n')) {
      return { ok: false, error: '昵称须为 1 至 80 个字符，且不能包含竖线或换行' };
    }
    return { ok: true, value: trimmed };
  }

  // 匹配刻度滑块最接近的档位索引，防止浮点误差导致落点偏移。
  function sliderIndexForValue(values, current) {
    if (!Array.isArray(values) || !values.length) return 0;
    let bestIndex = 0;
    let minDiff = Infinity;
    for (let i = 0; i < values.length; i++) {
      const diff = Math.abs(values[i] - Number(current));
      if (diff < minDiff) {
        minDiff = diff;
        bestIndex = i;
      }
    }
    return bestIndex;
  }

  // 构造用于剪贴板复制的设备与运行诊断摘要。
  function buildDiagnostics({ version, osText, machine, arch, electronVersion, address }) {
    return [
      `LanChat v${version || '3.0.0'}`,
      `OS: ${osText || 'Linux'}`,
      `Machine: ${machine || 'unknown'}`,
      `Arch: ${arch || 'unknown'}`,
      `Electron: ${electronVersion || '45.4.1'}`,
      `Service: ${address || '未连接'}`
    ].join('\n');
  }

  return {
    NAV_ITEMS,
    LANGUAGES,
    LINK_MODES,
    THEMES,
    ACCENTS,
    UI_FONT_PRESETS,
    CHAT_FONT_PRESETS,
    UI_FONT_STYLES,
    CHAT_FONT_STYLES,
    SIZE_LABELS,
    UI_FONT_SIZES,
    CHAT_FONT_SIZES,
    SHORTCUTS,
    filterNavItems,
    formatShortPath,
    validateNickname,
    sliderIndexForValue,
    buildDiagnostics
  };
});
