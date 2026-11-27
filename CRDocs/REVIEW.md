# LC-LINUX-003 阶段 2B / 实机初测 — 最近 3 轮审核记录

阶段 4 的完整历史已归档至 [`archive/2026-11-26_LC-LINUX-003_阶段4在线更新审核/REVIEW.md`](archive/2026-11-26_LC-LINUX-003_阶段4在线更新审核/REVIEW.md)。本文件只保留当前任务最近 3 轮交接与审核记录。

## Round 3 独立复审（通过：1 项 P0、2 项 P1 与 2 项 P2 全部闭环）

- **任务编号**：`LC-LINUX-003`（Linux 实机初测阻断回退整改复审）
- **审核轮次**：Round 3 独立复审
- **审核分支**：`codex/v4.0-cross-platform-unification`
- **独立审核方**：独立审核者（Antigravity / Gemini）
- **审核日期**：2026-11-27
- **审核结论**：**APPROVED**（仅代表 Windows 宿主机源码审查、diff 核验与严格 DOM Observer / IPC / 生命周期 / 设置回归范围放行；真实 Linux Electron 重新打包与实机启动/托盘/IME/LAN 验收仍需在物理 Linux 现场核销）
- **审核依据**：`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`、`.claude/Rules.md`，以及本轮完整 `git diff` 与独立复跑门禁实证。

### 一、Round 3 五项问题逐项复核结果

| 问题 ID | 原级别 | Round 3 复核 | 复核依据与独立核验证据 |
| --- | :---: | :---: | --- |
| `LC-LINUX-003-S5-P0-1` | **P0** | **RESOLVED** | 核验 `src/linux/desktop/renderer.js:35-39, 800-807`、`src/linux/desktop/composer-box.js:344` 与 `tests/linux_desktop_scroll_observer_regression.js`：已改为仅对真实 DOM 节点 `#message` 与 `#message-rich` 调用 `scroll.bind()`，不再将 `compose` 代理对象传入监听器；`composer-box.js` 已补齐 `clientHeight` 转发；新增专项回归注入严格校验 `target?.nodeType === 1` 的 `StrictResizeObserver` / `StrictMutationObserver`，覆盖主窗启动、双轨观察与模式切换并接入 `linux_desktop_ui_regression.js` 实跑通过。 |
| `LC-LINUX-003-S5-P1-1` | **P1** | **RESOLVED** | 核验 `src/linux/desktop/main.cjs:180, 239` 与 `tests/linux_desktop_lifecycle_regression.js`：`openSettingsWindow` 改为 `window && !window.isDestroyed() && window.isVisible()` 才绑定 `parent`/`modal: true`，关闭设置窗时亦仅对可见主窗调用 `window.focus()`；生命周期回归覆盖隐藏主窗打开设置窗与关闭回焦断言通过。 |
| `LC-LINUX-003-S5-P1-2` | **P1** | **RESOLVED** | 核验 `src/linux/desktop/tray-manager.cjs:38-42, 131-152, 189-208`、`src/linux/desktop/main.cjs:621-624` 与 `tests/linux_desktop_lifecycle_regression.js`：捕获依赖缺失与 `Tray` 构造/绑定异常，销毁半成品并清零 `tray` 与 `statusNotifierHostAvailable`，`canHideToTray()` 要求 `tray` 存在，主进程补充 `.catch()` 兜底；`ThrowingTray` 单测确认不抛未捕获异步拒绝并自动降级为任务栏最小化。 |
| `LC-LINUX-003-S5-P2-1` | **P2** | **RESOLVED** | 核验 `src/linux/desktop/main.cjs:356-364` 与 `tests/linux_desktop_ipc_regression.js`：`decodeClipboardImage` 兼容 `ArrayBuffer` 与 `ArrayBuffer.isView(bytes)`（按 `byteOffset`/`byteLength` 构造 Buffer），保留 `64 MiB` 上限校验；带偏移 `Uint8Array` 与超限拒绝单测通过。 |
| `LC-LINUX-003-S5-P2-2` | **P2** | **RESOLVED** | 核验 `src/linux/desktop/settings.js:837-838`、`src/linux/desktop/renderer.js:145` 及对应单测：`Escape` 拦截已覆盖 `settings-update-dialog?.open`；`updateHeader()` 在 `connecting` 期间保持“正在连接通信服务…”，连接成功后转为监听地址。 |

### 二、Round 3 独立复跑命令与门禁结果（Windows 宿主机）

| # | 验证命令 | 退出码 | 实测结果 |
| :---: | --- | :---: | --- |
| 1 | `node --check`（`src/linux/desktop/*.js,*.cjs` + `tests/linux_desktop_*.js` 共 25 文件） | `0` | 全部 25 个文件语法通过 |
| 2 | `node tests/linux_desktop_scroll_observer_regression.js` | `0` | `linux desktop scroll observer regression: ok`（严格 `ResizeObserver`/`MutationObserver` 校验 + 主窗启动 + `connecting` 文案） |
| 3 | `node tests/linux_desktop_ui_regression.js` | `0` | 串联 IPC（含 `Uint8Array`/64 MiB）、生命周期（含隐藏主窗设置窗、托盘失败降级）、设置（含更新弹窗 `Escape`）、更新、富文本、严格 Observer 与 UI 回归全部通过 |
| 4 | `py tests/test_linux_flow.py` | `0` | `Ran 22 tests ... OK` |
| 5 | 文件行数门禁与 `git diff --check` | `0` | 全部 `< 1000` 行：`settings.js` 965、`renderer.js` 817、`main.cjs` 813、`linux_desktop_ui_regression.js` 681、`composer-box.js` 359、`tray-manager.cjs` 238、`linux_desktop_scroll_observer_regression.js` 65；`git diff --check` 零报错 |

### 三、红线与未验证边界声明

- **红线守恒**：本轮 `git diff` 仅涉及 `src/linux/desktop/{composer-box.js,main.cjs,renderer.js,settings.js,tray-manager.cjs}`、`tests/linux_desktop_*.js` 与 `CRDocs/*`；版本号（`2.0.26`）、LANC/JSONL 协议、共享 C# 代码、安装目录均未改动，未执行 `Publish.bat` 或 `git push`。
- **未验证边界**：上述全部为 Windows 宿主机源码审查与 Node DOM/IPC 桩回归证据，不代表 Linux 实机已通过。需重新运行 `python Tools/Scripts/BuildLinuxDeb.py` 生成包含本轮修复的新 2.0.26 Linux 安装包，并在物理 Linux 机器上实测安装启动、托盘、设置窗、输入法与双机 LAN 核销 `CRDocs/DEFERRED.md`。

---

## Round 3 独立审核结论（打回：Linux 实机启动阻断缺陷 + 全链路复核 1 项 P0、2 项 P1、2 项 P2）

- **任务编号**：`LC-LINUX-003`（Linux 实机初测阻断回退与桌面主流程全链路复核）
- **审核轮次**：Round 3 独立审核
- **审核分支**：`codex/v4.0-cross-platform-unification`
- **独立审核方**：独立审核者（Antigravity / Gemini）
- **审核日期**：2026-11-27
- **审核结论**：**REJECTED**（用户 Linux 实机安装 2.0.26 `.deb` 后启动复现主界面完全瘫痪——顶部永久停在“正在连接通信服务…”，输入框、设置及底部按钮全部不可点击，仅搜索框可用；经源码全链路核查，确认 **1 项 P0 致命启动缺陷**、**2 项 P1 真机高风险缺陷**及 **2 项 P2 体验/兼容建议**，打回执行者整改并补齐防复发回归后复审）
- **审核依据**：用户 Linux 实机故障现象、`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/DEFERRED.md`、`.claude/Rules.md`，以及对 `src/linux/desktop/` 全量源码、`src/linux/LanChat.Linux/`（`Program.cs`、`DesktopService.cs`、`LinuxProfile.cs`、`LanChat.Linux.csproj`）和 `packaging/build-electron.py`、`packaging/lanchat-launcher.sh` 的全链路源码审计。

### 一、问题分级清单（P0 / P1 / P2）

| 问题 ID | 严重度 | 位置 | 问题描述与代码证据 | 整改要求 |
| --- | :---: | --- | --- | --- |
| `LC-LINUX-003-S5-P0-1` | **P0（阻断）** | `src/linux/desktop/renderer.js:35-37, 798`<br>`src/linux/desktop/scrollbar.js:21, 40-43`<br>`src/linux/desktop/composer-box.js:344-346` | 1. **主界面初始化必崩**：`renderer.js:35` 将 `compose` 改为 `createComposerBox(...)` 返回的普通 JS 代理对象，但 `renderer.js:798` 仍执行 `composeScroll = scroll.bind(compose)`。在真实 Electron/Chromium 环境下，`scrollbar.js:40` 的 `new ResizeObserver(schedule).observe(view)` 与第 42 行的 `new MutationObserver(schedule).observe(view, ...)` 严格要求真实 DOM `Element`/`Node`，传入普通对象会直接抛出 `TypeError: parameter 1 is not of type 'Element'`，导致 `createMainController()` 在第 798 行中止，后续 `drawChats()`、`api.onEvent()` 与 `initialize()` 全部未执行，界面永久停留在 `index.html` 初始静态态（顶部“正在连接通信服务…”、设置与输入区全 `disabled`、仅搜索框可用）；<br>2. **宿主机测试漏检原因**：Node 桩环境缺少 `ResizeObserver`/`MutationObserver`，`scrollbar.js:40-43` 被跳过；<br>3. **连带缺陷**：`composer-box.js:344-346` 缺失 `clientHeight` 转发（导致 `scrollbar.js:21` 算出 `NaN`），且原生 `<textarea id="message">` 与富文本 `<div id="message-rich">` 切换时滚动条未覆盖双真实 DOM 节点。 | 1. 停止将非 DOM 代理对象传给 `scroll.bind()`：改为对真实 DOM 节点 `byId('message')` 与 `byId('message-rich')` 分别绑定（或由 `scrollbar.js` / `composer-box.js` 严格操作真实 DOM 元素并在切换模式与输入变化时同步刷新，且保证两个模式共用或正确显隐浮动滑块、无重复重叠滑块）；<br>2. 在 `composer-box.js` 补齐 `clientHeight` 转发；<br>3. 在宿主机回归测试（如 `tests/linux_desktop_ui_regression.js` / `tests/linux_desktop_composer_regression.js`）中注入严格校验 DOM 节点类型的模拟 `ResizeObserver` 和 `MutationObserver`，并在 `createMainController()` 启动链路下实测通过，杜绝同类漏检。 |
| `LC-LINUX-003-S5-P1-1` | **P1（阻断）** | `src/linux/desktop/main.cjs:180-190, 239`<br>`src/linux/desktop/tray-manager.cjs:121-122` | 主窗口关闭时默认隐藏到托盘（`window.hide()`）。此时从托盘右键点击“系统设置…”或“检查新版本…”，`openSettingsWindow` 仅判断 `hasParent = window && !window.isDestroyed()`（未检查 `window.isVisible()`），会创建 `parent: hiddenWindow, modal: true` 的模态子窗。在 Linux GTK/X11/Wayland 桌面环境下，挂载在隐藏父窗上的模态窗口极易不可见、无法聚焦或卡死；且关闭设置窗时 `main.cjs:239` 对隐藏主窗执行 `window.focus()`。 | 仅当主窗存在且可见（`window && !window.isDestroyed() && window.isVisible()`）时才设置 `parent` 与 `modal: true`（或托盘打开设置/更新前先恢复显示主窗）；关闭设置窗时仅在主窗可见时再调用 `window.focus()`。补充对应主进程单元测试覆盖“主窗隐藏时从托盘打开设置窗”路径。 |
| `LC-LINUX-003-S5-P1-2` | **P1（阻断）** | `src/linux/desktop/main.cjs:616`<br>`src/linux/desktop/tray-manager.cjs:128-137, 174-180` | `main.cjs:616` 使用 `void trayManager.initialize()` 启动托盘初始化且无异常捕获；`tray-manager.cjs:130` 创建 `new Tray(...)` 若在缺少托盘/AppIndicator 支持的精简 Linux 桌面环境抛出异常，将形成主进程 `UnhandledPromiseRejection`，存在导致 Electron 主进程崩溃或异常弹窗的风险。 | 为 `trayManager.initialize()` / `createTray()` 增加安全异常兜底：托盘创建失败时记录错误日志并保持 `tray = null`、`canHideToTray() === false`（自动降级为关窗最小化到任务栏），绝不向主进程抛出未捕获异步拒绝。补充失败降级单元测试。 |
| `LC-LINUX-003-S5-P2-1` | **P2（建议）** | `src/linux/desktop/main.cjs:356-361` | `decodeClipboardImage(bytes)` 仅校验 `bytes instanceof ArrayBuffer`。Electron 跨进程 IPC 传递二进制数据时，主进程收到的对象可能是 `Uint8Array` 等 `ArrayBufferView`，导致直接粘贴内存截图时误报“剪贴板图片格式无效或超过 64 MiB”。 | 兼容 `ArrayBuffer` 与 `ArrayBuffer.isView(bytes)`（对视图使用 `Buffer.from(bytes.buffer, bytes.byteOffset, bytes.byteLength)`），保持 `64 MiB` 上限校验不变，补充对应单测。 |
| `LC-LINUX-003-S5-P2-2` | **P2（建议）** | `src/linux/desktop/settings.js:837`<br>`src/linux/desktop/renderer.js:143` | 1. `settings.js:837` 的 `Escape` 键拦截列表遗漏 `byId('settings-update-dialog')?.open`，更新弹窗打开时按 `Esc` 会直接关闭整个设置窗；<br>2. `renderer.js:143` 的 `updateHeader()` 在 `connecting === true` 且 `!online` 时将顶部状态写成“通信服务已断开”，启动连接瞬间会产生误导闪烁。 | 1. `settings.js:837` 增加 `byId('settings-update-dialog')?.open` 判定；<br>2. `renderer.js:143` 在 `connecting` 时显示“正在连接通信服务…”，仅在 `!connecting && !online` 时显示“通信服务已断开”。 |

### 二、全链路排查已确认无异常范围

- **其他 `scroll.bind()` 调用点**：已核查 `settings.js:915-917`（`navEl`、`viewport`、`feedback-input`）与 `overlay.js:21`（`emojiGrid`），均传入真实 DOM 元素，不存在同类崩溃。
- **C# 后端与 JSONL IPC 管道**：已核查 `src/linux/LanChat.Linux/`（`Program.cs`、`DesktopService.cs`、`LinuxProfile.cs`、`LanChat.Linux.csproj`）及共享 C# 服务，无非 CLI 路径误写 `stdout` 污染 IPC 的情况，序列化全部采用 `JsonObject`/`JsonArray`/`JsonNode` 保持裁剪安全。
- **打包与资源清单**：已核查 `packaging/build-electron.py`、`packaging/lanchat-launcher.sh`、`src/linux/desktop/package.json` 及 `assets/LinuxDesktop/`，前端引用的图标资源、`lanchat-bin` 路径与 `chrome-sandbox` 权限均对齐。

### 三、整改与交接约束

- 本轮仅修复上述 `OPEN` 缺陷并补齐宿主机防复发回归测试；不改版本号（`2.0.26`）、LANC/JSONL 协议与安装目录，不执行 `Publish.bat`、覆盖安装或 `git push`。
- 严格遵守手写单文件 `< 1000` 行预警与 `1500` 行上限、全中文注释、`git diff --check` 零告警。
- 宿主机 Node 回归（含严格 `ResizeObserver`/`MutationObserver` 校验）通过后，按 `CRDocs` 规则回写 `IMPLEMENTATION.md` 并将 `STATUS.md` 置为 `WAITING_FOR_GROK`，交由独立审核者复审；复审通过后再重新构建 Linux `.deb` 包交由用户实机验证。

---

## Round 3 执行者整改交接（待独立复审）

- **日期/状态**：2026-11-27；五项 Round 3 `OPEN` 问题已完成代码整改与宿主机回归，当前交接状态为 `WAITING_FOR_GROK`，不宣称 `APPROVED` 或 Linux 实机通过。
- **整改摘要**：P0 改为对 `#message`/`#message-rich` 两个真实 DOM 节点分别绑定滚动监听并补齐 `clientHeight`；P1-1 收紧隐藏主窗的设置窗父子关系与回焦；P1-2 为托盘构造、菜单绑定和初始化 Promise 增加异常降级；P2-1 支持 `ArrayBufferView` 且保留 64 MiB；P2-2 修正更新弹窗 Escape 与 connecting 文案。严格 Observer、主窗启动和双轨滑块测试已加入宿主机回归。
- **证据与边界**：详见 [`IMPLEMENTATION.md`](IMPLEMENTATION.md#round-3-执行者整改交接2026-11-27待独立复审)；Windows/Node/缓存 SDK 证据不替代真实 Linux Electron、托盘、IME、DPI、ELF、系统剪贴板、物理 LAN 与升级回退。

### 交给独立审核者的提示词

读取全部 CRDocs 与 `.claude/Rules.md`，独立检查上述五个问题的实际代码、调用链、diff 和测试；复跑严格 `ResizeObserver`/`MutationObserver`、主窗启动、隐藏主窗设置窗、托盘构造失败、ArrayBufferView/64 MiB、Escape 与 connecting 文案用例。确认未改版本号、LANC/JSONL、安装目录且没有把宿主机 Mock 写成 Linux 实机通过后，按规则在 `REVIEW.md` 顶部写入复审结论并更新 `STATUS.md`。

## Round 2 独立复审（通过：1 项 P0、2 项 P1 与 1 项 P2 全部闭环）

- **任务编号**：`LC-LINUX-003`（阶段 2B.1～2B.3，D3 = A，受 `CP-2B` 门禁控制）
- **审核轮次**：Round 2 独立复审
- **审核分支**：`codex/v4.0-cross-platform-unification`
- **独立审核方**：独立审核者（Antigravity / Gemini）
- **审核日期**：2026-11-26
- **审核结论**：**APPROVED**（仅代表 Windows 宿主机编译、源码调用链审查与 DOM/IPC 桩回归验证范围放行；真实 Linux Electron、`fcitx5`/`ibus` 中文预编辑、Xorg/Wayland/XWayland、真实系统剪贴板与多 DPI 仍未验证，`CP-2B` 继续保留于 `CRDocs/DEFERRED.md` 为 `[OPEN]`）
- **审核依据**：`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`、`.claude/Plan.md`，以及工作区实际源码与全部宿主机独立复跑实证。

### 一、Round 1 问题逐项复核结果

| 问题 ID | 原级别 | Round 2 复核 | 复核依据与独立核验证据 |
| --- | :---: | :---: | --- |
| `LC-LINUX-003-2B-P0-1` | **P0** | **RESOLVED** | 核验 `src/linux/desktop/preload.cjs:148-149`、`src/linux/desktop/main.cjs:776-777`、`src/linux/desktop/renderer.js:106-122`：已彻底移除带 `paths` 的 `stagePaste` 暴露，改由主进程无参 `read-clipboard-text`（`clipboard.readText()`）与 `stage-clipboard`（`trackPasteOperation(stagePastedFiles([], null, true))`）受控提供；右键菜单不再调用受权限拒绝阻断的 `navigator.clipboard`。 |
| `LC-LINUX-003-2B-P1-1` | **P1** | **RESOLVED** | 核验 `src/linux/desktop/composer-box.js:26-111` 与 `src/linux/desktop/preload.cjs:5-99`：统一按叶子文本节点、`<br>` 与块级容器边界计算文本与偏移，消除递归后重复累加；`tests/linux_desktop_composer_regression.js:117-132` 实测嵌套 `<div>`/`<br>` 文本 `'甲\n乙\n丙\n丁'` 与偏移 `[5, 7]` 通过。 |
| `LC-LINUX-003-2B-P1-2` | **P1** | **RESOLVED** | 核验 `src/linux/desktop/composer-box.js:194-202, 279-313, 354-356` 与 `preload.cjs:174`：初始化按 `useRich` 同步双轨显隐；统一 `isDisabled` 同步原生 `disabled` 与富文本 `inert`/`contentEditable`/`dataset.disabled` 且粘贴入口一致拦截；双轨同步 `dataset.draftChat`；`setRangeText(text, start, end)` 尊重显式起止范围；对应回归断言全部通过。 |
| `LC-LINUX-003-2B-P2-1` | **P2** | **RESOLVED** | 核验 `src/linux/desktop/desktop.css:153` 已添加 `caret-color: var(--accent)`；`composer-box.js:226-234` 已清理无用 `before` 变量。 |

### 二、Round 2 独立复跑命令与门禁结果（Windows 宿主机）

| # | 验证命令 | 退出码 | 实测结果 |
| :---: | --- | :---: | --- |
| 1 | `git branch --show-current` | `0` | `codex/v4.0-cross-platform-unification` |
| 2 | `cmd /c "Tools\Scripts\Compile.bat < NUL"` | `0` | `LanChat.exe compiled successfully.`（核验后已恢复 `release/*.exe`） |
| 3 | `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | `0` | 全部 8 组回归通过 |
| 4 | `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | `0` | `Native Windows compile: ok` 及全部回归通过 |
| 5 | `powershell -NoProfile -ExecutionPolicy Bypass -File tests/file_transfer_stability_regression.ps1` | `0` | 通过 |
| 6 | `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_composer_regression.js` | `0` | `linux desktop composer regression: ok` |
| 7 | `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js` | `0` | `LC-CC-004`、lifecycle、composer、ui 全部通过 |
| 8 | `node --check`（24 文件）+ `python tests/test_linux_flow.py`（22 项）+ `py_compile`（3 文件）+ `git diff --check` | `0` | 全部通过 |
| 9 | 文件行数门禁（按编辑器行号） | `0` | 全部 `< 1000` 行：`main.cjs` 822、`renderer.js` 816、`tests/linux_desktop_ui_regression.js` **696**（`<= 700`）、`composer-box.js` **360**（`<= 360`）、`preload.cjs` 235、`desktop.css` 201、`tests/linux_desktop_composer_regression.js` **193**（`<= 260`）、`index.html` 99 |

### 三、红线与未验证边界声明

- **红线守恒**：未进入阶段 5；未修改 LANC / Type 1 / Type 2 / JSONL、版本号（`2.0.26`）或发布链；Electron 主进程无新增 LAN Socket；未执行 `Publish.bat`、覆盖安装或 `git push`；富文本默认关闭、保留一键原生回退开关。
- **未验证边界**：Windows 宿主机无 Linux/WSL，上述全部“Linux”命名回归均为宿主机 Node DOM/IPC 桩或 Python 纯逻辑证据；真实 Linux Electron、`fcitx5`/`ibus` 中文预编辑、Xorg/Wayland/XWayland、真实系统剪贴板与多 DPI 继续保留于 `CRDocs/DEFERRED.md`（`LC-LINUX-003-CP-2B` 为 `[OPEN]`），待阶段 5 Linux 实机核销前不得默认开启富文本。

---

## Round 1 执行者整改交接（等待独立复审）

- **日期/状态**：2026-11-26，`WAITING_FOR_GROK`；保留下方 Round 1 独立审核的原始 `REJECTED` 结论，整改结果待独立审核者确认，不自行写入 `APPROVED`。
- **范围**：仅 `LC-LINUX-003` 阶段 2B.1～2B.3；不进入阶段 5，不改 LANC/JSONL、版本号、安装目录和延期边界。
- **P0-1 整改**：从 `preload.cjs` 的 `window.lanchat` 移除带 `paths` 的 `stagePaste`；新增无参 `readClipboardText` 与 `stageClipboard` 受控 IPC。`main.cjs` 由主进程直接调用 `clipboard.readText()`，附件通过 `stagePastedFiles([], null, true)` 读取系统剪贴板图片/文件；`renderer.js` 右键粘贴链路不再调用 `navigator.clipboard`，也不再接收任意路径参数。
- **P1-1 整改**：`composer-box.js` 与 `preload.cjs` 的文本序列化和偏移遍历统一按叶子文本节点长度、`<br>` 单个换行及块级容器边界换行计算，移除递归后的重复 `nodeText` 累加；补充嵌套 `<div>`/`<br>` 多行偏移断言。
- **P1-2 整改**：初始化按 `useRich` 设置双轨显隐；统一 `isDisabled` 同步 `native.disabled`、`rich.inert`、`rich.contentEditable`、`rich.dataset.disabled`，切换模式同步 `dataset.draftChat`，`preload.cjs` 粘贴判禁用覆盖统一状态，`setRangeText(text, start, end)` 保留显式范围。
- **P2-1 收口**：`.composer-rich` 增加 `caret-color: var(--accent)`，移除 `onMouseDown` 无用 `before` 变量。
- **新增回归断言**：持久化富文本恢复、离线切换不解锁、嵌套多行偏移、指定范围 `setRangeText`、无带 `paths` 的 `stagePaste` 暴露，以及主进程受控剪贴板 IPC 静态链路。
- **宿主机验证**：`Compile.bat`（0，完成后恢复 `release/*.exe`）；三套 PowerShell 回归（0）；24 个 JS/CJS `--check`；7 个 Linux Node 回归（0）；`tests/test_linux_flow.py` 22 项；14 个 Python 文件 `py_compile`；行数门禁；`git diff --check`（0）。
- **证据边界**：以上为 Windows 宿主机编译、DOM/IPC 桩、Node/Python 回归和静态证据；`tests/linux_desktop_regression.py` 因需传入 Linux ELF 路径未实跑，真实 Linux Electron、Xorg/Wayland/XWayland、`fcitx5`/`ibus`、系统剪贴板、多 DPI、物理 LAN 仍未验证，`CP-2B` 继续保持 `CRDocs/DEFERRED.md` 的 `[OPEN]`。

### 交给独立审核者的提示词

`读取 CRDocs，独立复审 LC-LINUX-003 阶段 2B Round 1 整改：直接检查 main.cjs/preload.cjs/renderer.js 的无参系统剪贴板 IPC 与 stagePastedFiles([], null, true) 调用链，确认渲染进程不再暴露带 paths 的 stagePaste；检查 composer-box.js/preload.cjs 的叶子文本、<br>、块级换行、嵌套多行偏移、统一 isDisabled、dataset.draftChat、持久化显隐和显式 setRangeText 范围；独立复跑 Node/CJS 语法、全套 Node 回归、三套 PowerShell、Python 回归、行数与 git diff 门禁。Windows 桩证据不得替代真实 Linux Electron 或 CP-2B 的 fcitx5/ibus 实机核销；完成后回写 REVIEW.md 顶部并按结论更新 STATUS.md。`

## Round 1 独立审核结论（打回：1 项 P0 + 2 项 P1 阻断缺陷 + 1 项 P2 建议）

- **任务编号**：`LC-LINUX-003`（阶段 2B.1～2B.3，D3 = A，受 `CP-2B` 门禁控制）
- **审核轮次**：Round 1 独立审核
- **审核分支**：`codex/v4.0-cross-platform-unification`
- **独立审核方**：独立审核者（Antigravity / Gemini）
- **审核日期**：2026-11-26
- **审核结论**：**REJECTED**（对应 `CRDocs/README.md` 中的 `CHANGES_REQUESTED`；仅针对 Windows 宿主机编译、源码调用链审查与 DOM/IPC 桩回归范围，发现 **1 项 P0 阻断缺陷**、**2 项 P1 阻断缺陷**与 **1 项 P2 建议**，打回执行者整改复审）
- **审核依据**：`.claude/Rules.md`、`.claude/Plan.md`、`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`，以及工作区实际源码、完整 `git diff` 与 10 项独立复跑实证。

### 一、问题分级清单（P0 / P1 / P2）

| 问题 ID | 严重度 | 位置 | 问题描述与代码证据 | 整改要求 |
| --- | :---: | --- | --- | --- |
| `LC-LINUX-003-2B-P0-1` | **P0（阻断）** | `src/linux/desktop/preload.cjs:90`<br>`src/linux/desktop/renderer.js:106-122`<br>`src/linux/desktop/main.cjs:592-593` | 1. `preload.cjs:90` 直接向渲染进程 `window.lanchat` 暴露了 `stagePaste: (paths, bytes, allowImage) => ipcRenderer.invoke('lanchat:stage-paste', paths, bytes, allowImage)`，允许渲染层绕过 `event.isTrusted` 约束传入任意本机文件路径数组 `paths`，破坏了原有主进程粘贴安全隔离边界；<br>2. `renderer.js:106-122` 右键菜单“粘贴”与“粘贴附件”依赖 `navigator.clipboard.readText()` / `navigator.clipboard.read()`，但 `main.cjs:592-593` 已设置 `setPermissionRequestHandler(..., decide(false))` 与 `setPermissionCheckHandler(() => false)` 全面拒绝渲染层剪贴板读权限，导致右键粘贴在真实 Electron 中必抛 `NotAllowedError` 失效。 | 1. 从 `preload.cjs` 移除可传任意 `paths` 的 `stagePaste` 暴露；<br>2. 右键菜单“粘贴”与“粘贴附件”改走主进程受控无参 IPC（如主进程直接调用 Electron `clipboard.readText()` 与复用 `stagePastedFiles([], null, true)` 读取系统剪贴板图片/文件），既守住不暴露任意路径参数的安全边界，又不被渲染层权限拒绝阻断；<br>3. 补充回归断言验证 `preload.cjs` 不暴露带 `paths` 参数的 `stagePaste` 且右键粘贴链路可运行。 |
| `LC-LINUX-003-2B-P1-1` | **P1（阻断）** | `src/linux/desktop/composer-box.js:29-34, 40-57`<br>`src/linux/desktop/preload.cjs:8-13, 25-39` | `composer-box.js:51-52` 与 `preload.cjs:33-34` 在 `visit(node)` 中对非表情元素节点先递归累加全部子节点长度，随后又执行 `total += nodeText(node).length;` 重复累加一遍，导致含子容器（如 `contenteditable` 换行产生的 `<div>`）时偏移量被双倍计算，选区、粘贴、删除和点击收拢全部错位；且 `nodeText`/`textOf` 未还原 `<br>` 或块级容器换行符。 | 1. 修正 `visit(node)`：仅对叶子文本节点（`node.nodeType === 3`）累加 `nodeValue.length`（及 `<br>` 记 1），元素节点递归子节点后不再重复累加 `nodeText(node).length`；<br>2. 修正 `nodeText`/`textOf` 对 `<br>` 与块级换行的还原，并补充嵌套 `<div>`/`<br>` 多行选区偏移回归断言。 |
| `LC-LINUX-003-2B-P1-2` | **P1（阻断）** | `src/linux/desktop/composer-box.js:23, 225-244, 257-258, 277`<br>`src/linux/desktop/renderer.js:158, 451, 596`<br>`src/linux/desktop/preload.cjs:115` | 1. **持久化重启恢复失效**：`composer-box.js:23` 读取 `storage` 恢复 `useRich`，但第 277 行硬写 `rich.hidden = true; native.hidden = false;`，重启后若 `useRich === true` 会显示空原生框而隐藏真正活跃的富文本容器，导致输入假死；<br>2. **禁用态与 `draftChat` 切换脱节**：`set disabled` 仅写 `rich.inert` 而 `get disabled` 读 `active().disabled`（`rich.disabled` 恒为 `false`），导致 `toggleRich()` 第 232 行切回原生时误解锁离线会话，且 `preload.cjs:115` 在富文本离线会话下无法拦截粘贴；切换模式时 `dataset.draftChat` 未同步到目标元素；<br>3. **`setRangeText` 忽略起止参数**：`composer-box.js:244` 将 `setRangeText` 指向单参 `replaceSelection(text)`，丢弃了 `renderer.js:451, 596` 传入的 `(start, end)`。 | 1. 初始化第 277 行按 `useRich` 设置 `rich.hidden = !useRich; native.hidden = useRich;`；<br>2. 内部维护统一 `isDisabled` 状态，同步 `native.disabled`、`rich.inert`、`rich.contentEditable` 与 `rich.dataset.disabled`，`preload.cjs:115` 同步判禁用；同步维护两端 `dataset.draftChat`；<br>3. `setRangeText(text, start, end)` 尊重显式传入的 `start`/`end` 范围；补充持久化恢复、离线切换不解锁及指定范围 `setRangeText` 回归断言。 |
| `LC-LINUX-003-2B-P2-1` | **P2（建议）** | `src/linux/desktop/desktop.css:153`<br>`src/linux/desktop/composer-box.js:174, 179` | `.composer-rich` 缺少主题色光标样式 `caret-color: var(--accent)`；`onMouseDown` 残留无用局部变量 `const before = ...; void before;`。 | 建议补充 `.composer-rich { caret-color: var(--accent); }` 并清理死变量。 |

### 二、独立复跑命令与门禁实测（Windows 宿主机）

| # | 验证命令 | 退出码 | 实测结果 |
| :---: | --- | :---: | --- |
| 1 | `git branch --show-current` | `0` | `codex/v4.0-cross-platform-unification` |
| 2 | `cmd /c "Tools\Scripts\Compile.bat < NUL"` | `0` | `LanChat.exe compiled successfully.`（核验后已还原 `release/*.exe`） |
| 3 | `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | `0` | 全部 8 组回归通过 |
| 4 | `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | `0` | `Native Windows compile: ok` 及全部回归通过 |
| 5 | `powershell -NoProfile -ExecutionPolicy Bypass -File tests/file_transfer_stability_regression.ps1` | `0` | 通过 |
| 6 | `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_composer_regression.js` | `0` | `linux desktop composer regression: ok` |
| 7 | `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js` | `0` | `LC-CC-004`、lifecycle、composer、ui 全部通过 |
| 8 | `node --check`（24 文件）+ `python tests/test_linux_flow.py`（22 项）+ `py_compile`（3 文件）+ `git diff --check` | `0` | 全部通过 |
| 9 | 文件行数门禁（按编辑器行号） | `0` | 全部 `< 1000` 行：`renderer.js` 819、`tests/linux_desktop_ui_regression.js` **696**（`<= 700`）、`composer-box.js` **282**（`<= 360`）、`desktop.css` 201、`preload.cjs` 175、`tests/linux_desktop_composer_regression.js` **133**（`<= 260`）、`index.html` 99 |

### 三、红线与未验证边界声明

- **红线检查**：阶段边界/协议/版本号/发布链守恒，但 **`LC-LINUX-003-2B-P0-1`** 触碰 IPC 粘贴安全边界，必须整改后复审。
- **未验证边界**：Windows 宿主机无 Linux/WSL，真实 Linux Electron、`fcitx5`/`ibus` 中文预编辑、Xorg/Wayland/XWayland、真实系统剪贴板与多 DPI 均未验证，`CP-2B`（`LC-LINUX-003-CP-2B`）继续保留于 `CRDocs/DEFERRED.md` 为 `[OPEN]`。

---

## Round 1 实施交接

- **日期/状态**：2026-11-26，`WAITING_FOR_GROK`。
- **范围**：阶段 2B.1～2B.3；`preload.cjs` 通用粘贴适配器、`composer-box.js` 富文本输入框、专项 Node 回归及 UI 回归链注册。
- **实现结果**：支持原生 `<textarea>` 与 `contenteditable` 的文本读取、选区插入、最大长度裁剪和 `input` 派发；富文本模式支持 PNG 表情基线、表情原子光标/删除、复制/剪切令牌还原、IME 保护、暗色右键菜单和一键回退。
- **验证结果**：Windows 宿主机 Node 语法、`linux_desktop_composer_regression.js`、`linux_desktop_ui_regression.js` 通过；`composer-box.js` 282 行、专项回归 133 行、UI 回归 696 行。
- **边界**：当前默认保持原生 `<textarea>`；富文本仅显式启用。尚未在真实 Linux Electron 的 `fcitx5`/`ibus`、Xorg/Wayland、真实系统剪贴板和真实 DPI 下验收，不能写成 CP-2B 已通过。

### 交给独立审核者的提示词

`读取 CRDocs，独立审核 LC-LINUX-003 阶段 2B.1～2B.3：直接检查 preload.cjs 与 composer-box.js 的调用链、contenteditable/textarea 双轨、复制剪切令牌、光标收拢、Backspace/Delete 原子删除、IME 保护、右键暗色粘贴菜单及回退开关；独立复跑 Node 语法、专项回归、linux_desktop_ui_regression.js、完整宿主机验证和行数/git diff 门禁。Windows DOM/IPC 桩只能作为宿主机证据，CP-2B 必须继续保留为真实 Linux fcitx5/ibus 待核销项；完成后回写 REVIEW.md 顶部并按结论更新 STATUS.md。`
