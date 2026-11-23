# LC-LINUX-002-STEP-3-SLICE-2 — Round 2：P1 与 P2 整改交付

> 执行者：Gemini；审核者：Grok（本文件仅记录执行者实施与验证）
> 日期：2026-11-23；状态：已交接并经用户确认审核通过（APPROVED）
> 工程：D:/TEmp/LanChat
> 交付形态：未提交工作区，未修改 `CRDocs/REVIEW.md` 审核历史，未修改 `handoff-experiment.2` 至 `handoff-experiment.5`

## 本轮范围

按 Round 1 审核意见修复全部 2 条 P1 与可一并处理的 P2，不扩大到托盘恢复、系统通知或检查更新维护入口，不改 LANC 封包结构。

## P1 整改

### LC-S3-001 外观四项按 WinForms `AppSettings` 落盘，即时作用主窗与设置窗并重启保持

1. **配置读取与落盘（`LinuxProfile.cs` / `DesktopService.cs`）**：
   - 按 `src/Services/AppSettings.cs` 同一键名与校验范围读写 `ThemeMode`（`0..2`，默认 `0`）、`AccentPreset`（`0..4`，默认 `0`）、`UiFontFamily`（默认 `"Microsoft YaHei UI"`）、`UiFontSize`（`8.0..24.0`，默认 `10.0`）、`ChatFontFamily`（默认 `"Microsoft YaHei UI"`）、`ChatFontSize`（`8.0..26.0`，默认 `10.5`）。
   - `SaveSettings(...)` 保留既有未知键并支持只读配置 `overridePath` 二次确认与重启恢复；外观字段设为可选参数，不破坏已有 CLI/回归调用。
2. **统一设计令牌与即时渲染（`appearance.js` / `desktop.css` / `settings.css`）**：
   - `appearance.js` 严格复现 `GrokTheme.Surface(r, g, b, mode)` 三套深色主题、五套强调色、七种字体栈映射、`--ui-font-delta`（`uiSize - 10.0` pt）与 `--chat-font-size`，供主窗与设置窗共用。
   - 主窗 `renderer.js` 在 `initialize()` 并行拉取 `api.settings()` 恢复外观，收到 `settings-updated` 后立即刷新外观变量与输入框高度；设置窗 `settings.js` 启动时及 `updateAppearance()` 时立即应用外观变量（支持快速拖拽合并持久化，只读确认取消时回退到 `committed`）。
   - 通用页「字体大小」滑块与外观页「主界面字体大小」滑块绑定同一 `committed.uiFontSize` 并双向同步。
3. **展示项边界与文档修正**：
   - 语言、链接打开方式、协议通道、快捷键徽章、托盘预览、后台网络驻留运行（保持空回调 `() => {}`）、分块校验、自动加载、TUN 过滤、单播探测继续仅做本地展示，未新增 RPC。
   - 修正 `settings.js`、`settings-model.js` 与 `PlanDocs/LC-LINUX-002/INTEGRATION.md` 中关于外观项「Windows 未接线」的过时说明。

### LC-S3-002 `histories-cleared` 作废进行中的 `historyLoads`

- `src/linux/desktop/renderer.js` 在 `handleEvent` 收到 `histories-cleared` 时先执行 `historyLoads.clear()`，再清空 `messages`、`unread` 与摘要并重画；在途 `loadHistory(chat)` 于 `await api.history(chat)` 返回或异常后检测到 `historyLoads.get(chat) !== operation` 即放弃写回气泡与错误状态。

## P2 整改

1. **反馈框浮动滑块**：`settings.html` 为 `#feedback-input` 套用 `.scroll-host.feedback-scroll` 与 `.scroll-view`；`settings.css` 隐藏原生滚动条并设置复选框 `accent-color: var(--accent)`；`settings.js` 绑定 `LanChatScroll` 并在打开反馈框时刷新滑块。
2. **小工作区尺寸夹紧**：`main.cjs` 修正 `computeSettingsBounds` 小工作区下限、新增 `computeSettingsMinSize(bounds)` 并在 `ready-to-show` 按外框 `getBounds()` 二次夹紧至 `display.workArea`；`settings.css` 增加 `@media (max-width: 600px)`。
3. **真实确认/取消及竞态自动化测试**：`settings.js` 与 `renderer.js` 支持 `LANCHAT_TEST_MODE=1` 注入；`tests/linux_desktop_ui_regression.js` 新增 `testSettingsController()` 与 `testMainRendererRaceAndAppearance()`。
4. **快捷键页对齐**：`settings-model.js` 将「多行换行输入」更新为 `Ctrl + Enter / Shift + Enter`。
5. **相同历史目录提示**：`settings.js` 对比归一化后的目标目录与原目录，相同目录提示「所选目录与当前目录相同，无需迁移」。
6. **Linux ELF 用例补充**：`tests/linux_desktop_regression.py` 补充外观 6 项重启持久化与 `clear-all-histories` 断言（待 Linux 环境运行）。

## 实际文件与行数门禁（均 < 1000 行）

| 文件 | 本轮作用 | 行数 | 门禁 |
| --- | --- | :---: | :---: |
| `src/linux/LanChat.Linux/LinuxProfile.cs` | 读写与校验 6 项外观配置，兼容只读备份与未知键 | 397 | PASS |
| `src/linux/LanChat.Linux/DesktopService.cs` | `settings` / `save-settings` 透传可选外观字段 | 453 | PASS |
| `src/linux/desktop/appearance.js` | 主窗与设置窗共用的主题/强调色/字体 CSS 变量计算 | 228 | PASS |
| `src/linux/desktop/main.cjs` | 外观 IPC 校验、`settings-updated` 广播、小工作区窗口夹紧 | 418 | PASS |
| `src/linux/desktop/renderer.js` | 启动/热更新外观应用、`histories-cleared` 作废 `historyLoads` | 765 | PASS |
| `src/linux/desktop/settings-model.js` | 外观预设接入与快捷键 `Ctrl + Enter / Shift + Enter` 对齐 | 111 | PASS |
| `src/linux/desktop/settings-pages.js` | 外观控件接通、通用字号联动、展示项保持无 RPC/空回调 | 499 | PASS |
| `src/linux/desktop/settings.js` | 外观持久化、同目录判定、反馈滑块绑定、可测注入 | 700 | PASS |
| `src/linux/desktop/desktop.css` | 主题/强调色/界面与聊天字体 CSS 变量化 | 170 | PASS |
| `src/linux/desktop/settings.css` | 主题/字号变量化、反馈框隐藏原生滚动条、窄屏适配 | 742 | PASS |
| `src/linux/desktop/index.html` | 引入 `appearance.js` | 85 | PASS |
| `src/linux/desktop/settings.html` | 引入 `appearance.js`，反馈框接入 `.scroll-host` | 91 | PASS |
| `tests/linux_desktop_ui_regression.js` | 新增外观、只读确认/取消、同目录、反馈滑块、竞态测试 | 817 | PASS |
| `tests/linux_desktop_regression.py` | 补充外观重启持久化与 `clear-all-histories` 用例 | 421 | PASS |
| `PlanDocs/LC-LINUX-002/INTEGRATION.md` | 修正切片 2 接线说明与测试边界 | 142 | PASS |

## 验证与未验证边界

1. `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js`：退出码 0，输出 `linux desktop ui regression: ok`（模拟 DOM 与桩 IPC，非 Electron 实机）。
2. `python -m py_compile packaging/build-electron.py packaging/build_linux.py tests/linux_desktop_regression.py`：退出码 0（仅语法检查）。
3. `python packaging/build_linux.py <temp>`（使用 `bin/lc-linux-002/cache/dotnet`）：`linux-x64` 与 `linux-arm64` 均 0 警告 0 错误编出并校验 ELF64 机器码（62 / 183），临时输出已清理（未在 Linux 上执行该 ELF）。
4. Edge Headless `--dump-dom`（`settings.html` `1260×820`、`index.html` `1040×720`）：静态装载与 `.feedback-scroll .float-thumb` 挂载正常（无预加载桥与 ELF）。
5. **未在 Linux / ELF 上验证**：`tests/linux_desktop_regression.py` 在 Linux 网络命名空间对最新 ELF 实跑、Xorg/Wayland/XWayland 实机 Electron 联调、GTK 目录框、fcitx5/ibus 输入法与 2000 条历史性能，均已转入 `CRDocs/DEFERRED.md` 跟踪。

## 异议与理由

无异议。
