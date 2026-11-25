// 主题、强调色、界面/聊天字体族与两套字号的统一映射，算法对齐 WinForms GrokTheme 与 AppSettings。
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.LanChatAppearance = api;
})(typeof globalThis === 'undefined' ? this : globalThis, function () {
  const THEMES = ['深色 (Grok Dark)', '石墨深色', '午夜蓝'];

  const ACCENTS = [
    { label: '白 (#FFFFFF)', swatch: '#FFFFFF', accent: '#ffffff', deep: '#b8b8b8', hot: '#ffffff' },
    { label: '棕 (#A16B39)', swatch: '#A16B39', accent: '#a16b39', deep: '#714826', hot: '#c08a5a' },
    { label: '红 (#FC2C3E)', swatch: '#FC2C3E', accent: '#fc2c3e', deep: '#b11f2c', hot: '#ff6472' },
    { label: '橙红 (#F9670E)', swatch: '#F9670E', accent: '#f9670e', deep: '#b33f06', hot: '#ff934e' },
    { label: '橙 (#F69412)', swatch: '#F69412', accent: '#f69412', deep: '#b36507', hot: '#ffb64a' },
    { label: '翠绿 (#14C370)', swatch: '#14C370', accent: '#14c370', deep: '#0e8c50', hot: '#45df95' },
    { label: '青 (#13B8A3)', swatch: '#13B8A3', accent: '#13b8a3', deep: '#0c7f71', hot: '#4bd8c7' },
    { label: '蓝 (#1884FC)', swatch: '#1884FC', accent: '#1884fc', deep: '#0d53a8', hot: '#56a7ff' },
    { label: '魅紫 (#905AFB)', swatch: '#905AFB', accent: '#905afb', deep: '#5c2baf', hot: '#b18bff' },
    { label: '粉 (#FB3499)', swatch: '#FB3499', accent: '#fb3499', deep: '#b31f6a', hot: '#ff78bb' },
    { label: '灰 (#777777)', swatch: '#777777', accent: '#777777', deep: '#4d4d4d', hot: '#999999' }
  ];

  const FONT_PRESETS = [
    {
      family: 'Microsoft YaHei UI',
      label: '微软雅黑',
      ui: true,
      chat: true,
      css: '"Microsoft YaHei UI", "Noto Sans CJK SC", "Noto Sans SC", system-ui, sans-serif'
    },
    {
      family: 'Segoe UI',
      label: 'Segoe UI',
      ui: true,
      chat: true,
      css: '"Segoe UI", "Noto Sans CJK SC", "Noto Sans SC", system-ui, sans-serif'
    },
    {
      family: 'SimSun',
      label: '宋体',
      ui: true,
      chat: true,
      css: '"SimSun", "Noto Serif CJK SC", "Source Han Serif SC", serif'
    },
    {
      family: 'SimHei',
      label: '黑体',
      ui: true,
      chat: true,
      css: '"SimHei", "Noto Sans CJK SC", "WenQuanYi Zen Hei", sans-serif'
    },
    {
      family: 'KaiTi',
      label: '楷体',
      ui: true,
      chat: true,
      css: '"KaiTi", "STKaiti", "AR PL UKai CN", "Noto Serif CJK SC", serif'
    },
    {
      family: 'Arial',
      label: 'Arial',
      ui: true,
      chat: true,
      css: 'Arial, "Helvetica Neue", "Noto Sans CJK SC", sans-serif'
    },
    {
      family: 'Consolas',
      label: 'Consolas',
      ui: false,
      chat: true,
      css: 'Consolas, "JetBrains Mono", "Noto Sans Mono CJK SC", monospace'
    }
  ];

  const UI_FONT_PRESETS = FONT_PRESETS.filter(item => item.ui);
  const CHAT_FONT_PRESETS = FONT_PRESETS.filter(item => item.chat);
  const SIZE_LABELS = ['小', '默认', '中', '大', '特大'];
  const UI_FONT_SIZES = [9.0, 10.0, 11.0, 12.0, 14.0];
  const CHAT_FONT_SIZES = [9.5, 10.5, 11.5, 13.0, 15.0];

  function toHexByte(n) {
    return Math.max(0, Math.min(255, Math.round(n))).toString(16).padStart(2, '0');
  }

  function rgbHex(r, g, b) {
    return `#${toHexByte(r)}${toHexByte(g)}${toHexByte(b)}`;
  }

  // 与 GrokTheme.Surface(red, green, blue, mode) 完全一致。
  function surfaceHex(red, green, blue, mode) {
    if (mode === 1) return rgbHex(red + 11, green + 11, blue + 12);
    if (mode === 2) return rgbHex(red, green + 5, blue + 15);
    return rgbHex(red, green, blue);
  }

  function parseHexRgb(hex) {
    const clean = String(hex || '').replace(/^#/, '');
    return [
      parseInt(clean.slice(0, 2), 16),
      parseInt(clean.slice(2, 4), 16),
      parseInt(clean.slice(4, 6), 16)
    ];
  }

  function buttonPrimaryHoverHex(presetIndex) {
    const entry = ACCENTS[presetIndex] || ACCENTS[0];
    if (presetIndex === 0) return entry.accent;
    const [r, g, b] = parseHexRgb(entry.deep);
    return rgbHex(r + 8, g + 8, b + 8);
  }

  function normalizeThemeMode(value) {
    const n = Number(value);
    return Number.isInteger(n) && n >= 0 && n < THEMES.length ? n : 0;
  }

  function normalizeAccentPreset(value) {
    const n = Number(value);
    return Number.isInteger(n) && n >= 0 && n < ACCENTS.length ? n : 0;
  }

  function findFontPreset(value) {
    const text = String(value || '').trim();
    if (!text) return null;
    const lower = text.toLocaleLowerCase();
    return FONT_PRESETS.find(
      item => item.family.toLocaleLowerCase() === lower || item.label.toLocaleLowerCase() === lower
    ) || null;
  }

  function normalizeFontFamily(value) {
    const text = String(value || '').trim();
    if (!text) return 'Microsoft YaHei UI';
    const preset = findFontPreset(text);
    return preset ? preset.family : text;
  }

  function fontDisplayLabel(family) {
    const preset = findFontPreset(family);
    return preset ? preset.label : (String(family || '').trim() || '微软雅黑');
  }

  function fontCssStack(family) {
    const preset = findFontPreset(family);
    if (preset) return preset.css;
    const clean = String(family || '').trim().replace(/["\\\r\n]/g, '');
    return clean ? `"${clean}", "Microsoft YaHei UI", "Noto Sans CJK SC", sans-serif` : FONT_PRESETS[0].css;
  }

  function normalizeFontSize(value, min, max, fallback) {
    const n = Number(value);
    if (!Number.isFinite(n) || n < min || n > max) return fallback;
    return Math.round(n * 10) / 10;
  }

  function normalizeSettings(settings = {}) {
    return {
      themeMode: normalizeThemeMode(settings.themeMode),
      accentPreset: normalizeAccentPreset(settings.accentPreset),
      uiFontFamily: normalizeFontFamily(settings.uiFontFamily),
      uiFontSize: normalizeFontSize(settings.uiFontSize, 8.0, 24.0, 10.0),
      chatFontFamily: normalizeFontFamily(settings.chatFontFamily),
      chatFontSize: normalizeFontSize(settings.chatFontSize, 8.0, 26.0, 10.5)
    };
  }

  function computeCssVars(settings = {}) {
    const norm = normalizeSettings(settings);
    const mode = norm.themeMode;
    const accent = ACCENTS[norm.accentPreset];
    const uiDelta = (norm.uiFontSize - 10.0).toFixed(1);
    return {
      '--sidebar': surfaceHex(14, 14, 14, mode),
      '--viewport': surfaceHex(18, 18, 18, mode),
      '--card': surfaceHex(28, 28, 31, mode),
      '--capsule': surfaceHex(32, 32, 36, mode),
      '--pill': surfaceHex(32, 32, 34, mode),
      '--level3': surfaceHex(40, 40, 44, mode),
      '--level3-hot': surfaceHex(46, 46, 52, mode),
      '--hover': surfaceHex(34, 34, 38, mode),
      '--pinned': surfaceHex(24, 21, 32, mode),
      '--pinned-border': surfaceHex(42, 36, 56, mode),
      '--pinned-hover': surfaceHex(32, 28, 42, mode),
      '--border': surfaceHex(44, 44, 50, mode),
      '--border-inner': surfaceHex(38, 38, 43, mode),
      '--separator': surfaceHex(44, 44, 52, mode),
      '--peer-bubble': surfaceHex(30, 30, 33, mode),
      '--input-plus': surfaceHex(42, 42, 46, mode),
      '--input-plus-hover': surfaceHex(56, 56, 62, mode),
      '--search-box': surfaceHex(30, 30, 34, mode),
      '--about-logo-bg': surfaceHex(36, 36, 42, mode),
      '--about-logo-border': surfaceHex(52, 52, 60, mode),
      '--accent': accent.accent,
      '--accent-hot': accent.hot,
      '--selection': accent.deep,
      '--send-bg': norm.accentPreset === 0 ? accent.accent : accent.deep,
      '--send-hover': norm.accentPreset === 0 ? accent.hot : buttonPrimaryHoverHex(norm.accentPreset),
      '--btn-primary-hover': buttonPrimaryHoverHex(norm.accentPreset),
      '--ui-font-family': fontCssStack(norm.uiFontFamily),
      '--ui-font-delta': `${uiDelta}pt`,
      '--chat-font-family': fontCssStack(norm.chatFontFamily),
      '--chat-font-size': `${norm.chatFontSize.toFixed(1)}pt`
    };
  }

  function applyAppearance(rootElement, settings = {}) {
    const vars = computeCssVars(settings);
    if (rootElement?.style?.setProperty) {
      for (const [name, value] of Object.entries(vars)) {
        rootElement.style.setProperty(name, value);
      }
    }
    return vars;
  }

  return {
    THEMES,
    ACCENTS,
    FONT_PRESETS,
    UI_FONT_PRESETS,
    CHAT_FONT_PRESETS,
    SIZE_LABELS,
    UI_FONT_SIZES,
    CHAT_FONT_SIZES,
    surfaceHex,
    normalizeThemeMode,
    normalizeAccentPreset,
    normalizeFontFamily,
    fontDisplayLabel,
    fontCssStack,
    normalizeFontSize,
    normalizeSettings,
    computeCssVars,
    applyAppearance
  };
});
