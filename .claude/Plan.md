# Plan.md — 当前活跃计划（V4.0 双端功能统一详细执行清单）

> 2026-11-26 · 分支 `codex/v4.0-cross-platform-unification` · 话题 `LC-LINUX-003`（`ADOPTED`）· 基线 `V3.0`（程序输出版本 `2.0.26`）。历史完成项见 `Plan-archive.md`。

## 已采纳决定与定稿依据

- **定稿文档**：[LC-LINUX-003 定稿](../PlanDocs/LC-LINUX-003/FINAL.md) · [第 2 轮提案](../PlanDocs/LC-LINUX-003/PROPOSAL-R2.md) · [第 2 轮审核](../PlanDocs/LC-LINUX-003/REVIEW-R2.md) · [决定记录](../PlanDocs/DECISIONS.md)
- **D1 = A（阶段 3）**：B 类 4 项设置（`TunFilter`、`UnicastProbe`、`AutoLoadHistory`、`SendShortcutMode`）在 Windows `AppSettings.cs` 与 Linux `LinuxProfile.cs` 使用统一 JSON 键名、保持与现行行为一致的默认值，并在双端设置面板同步接线持久化。
- **D2 = A（阶段 2A）**：轻量“手动输入 IPv4 直连握手（`probe-ip`）”入口放入设置中心 **“设备连接”页** 卡片顶部，不改动主窗左侧栏现有视觉布局。
- **D3 = A（阶段 2A / 2B）**：阶段 2A 先交付 214/188px 微信式高输入区外壳、展开工具栏、496×548 表情选择器与群聊 1～16 人多选并保留原生 `<textarea>`；阶段 2B 作为高风险独立专项重构 `preload.cjs` 粘贴适配器与 `composer-box.js`，必须通过真实 Linux `fcitx5`/`ibus` 实测（`CP-2B`）后才默认开启并保留一键回退开关。

## 全局执行铁律（每完成一步必检）

1. **分支与发布红线**：必须在 `codex/v4.0-cross-platform-unification` 分支开发；除非用户明确指令，**严禁改动发布版本号、严禁热替换安装、严禁执行 `Publish.bat` 或 git push**。
2. **协议与架构守恒**：严禁修改 `LANC` 魔数/报文结构与 `JSONL` 历史格式；Electron 主进程绝不创建局域网 UDP/TCP Socket（仅阶段 4 更新可用 HTTPS），渲染进程保持严格 `default-src 'none'` CSP；GUI 与 CLI 共用 `LinuxProfile` 路径与文件锁。
3. **行数与注释门禁**：所有手写 `.js` / `.cjs` / `.css` / `.cs` 必须 `< 1000` 行（绝不触碰 1500 行硬上限，新文件务必注册进 `tests/linux_desktop_ui_regression.js` 行数门禁）；全部注释使用中文并遵守 [Rules.md](Rules.md) 第 7 条与第 9 条。
4. **双端回归与诚实取证**：只要改动共享 C# 文件（`src/NetworkService*.cs`、`src/Services/*.cs`、`src/UI/...`），必须立即复跑 `cmd /c "Tools\Scripts\Compile.bat < NUL"` 及 Windows 回归脚本；严禁把宿主机 Node/Python 桩测试写成 Linux 实机通过。
5. **明确不做（严禁越界实现）**：
   - 不做 Windows 已无可见入口的“新建节点（14 种形态调色盘）”空壳窗，不在聊天工具栏塞旧版节点按钮；
   - 不做三语国际化词典（`语言`）、不做破坏纯内网与 CSP 的“内置极速网页预览”（`链接打开方式`）、不在 `NetworkService.Transfer.cs` 加降低吞吐的 `fs.Flush(true)`（`64KB 分块传输校验` 改为只读能力展示）、不另造复杂协议通道切换逻辑（`网络协议通道` 仅展示绑定网卡/端口或触发 `refresh`）；
   - 不承诺非 root 静默覆盖安装 `.deb`。

---

## 分阶段详细执行清单

### 阶段 0 · 基线冻结、测试拆解与既有 ELF 摸底

- [x] **0.1 核对分支与基线回归**：确认 `git rev-parse --abbrev-ref HEAD` 为 `codex/v4.0-cross-platform-unification`；在宿主机跑通 4 套既有基线回归并留存结果：
  - `cmd /c "Tools\Scripts\Compile.bat < NUL"`
  - `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`
- [x] **0.2 拆分 `tests/linux_desktop_ui_regression.js`（当前 924 行）**：抽出公共 DOM/Stub 辅助函数至 `tests/linux_desktop_test_helpers.js`，将 `linux_desktop_ui_regression.js` 压至 700 行以内，并确保原有全部断言 100% 通过。
- [x] **0.3 登记 `v2.0.26` 基线与 `CP-0` 状态**：核对 `PlanDocs/LC-LINUX-002/BASELINE.md`；若有可用 Linux 环境，立即对现存 `v2.0.26` ELF 跑通 `python3 tests/linux_desktop_regression.py <elf>` 完成 `CP-0` 摸底；若当前宿主机无 Linux 环境，保持 `CRDocs/DEFERRED.md` 中的 `LC-LINUX-002-ENV-001` 为 `[OPEN]`，不阻塞阶段 1 编码（首次接入 Linux 环境时优先补跑，绝不以宿主机测试核销）。

### 阶段 1 · 桌面生命周期、托盘、通知与 A 类托盘开关

- [x] **1.1 导出托盘图标资源**：修改 `packaging/ExportLinuxDesktopAssets.cs` 与 `packaging/build-electron.py`，导出并校验 `tray.png`（常态）、`tray-unread.png`（未读提示）、`tray-blank.png`（Xorg 闪烁空帧），同步更新 `assets/LinuxDesktop/*.png`。
- [x] **1.2 持久化 A 类托盘开关（`KeepAliveInTray`、`TrayPreview`）**：
  - 修改 `src/linux/LanChat.Linux/LinuxProfile.cs`：新增 `KeepAliveInTray`（默认 `true`）、`TrayPreview`（默认 `true`），读取兼容缺失键，写入保留未知键与只读备用位置二次确认；
  - 修改 `src/linux/LanChat.Linux/DesktopService.cs`：`SettingsFrame()` 与 `SaveSettings()` 读写 `keepAliveInTray` / `trayPreview`；
  - 修改 `src/linux/desktop/main.cjs`（`save-settings` 透传布尔校验）、`src/linux/desktop/settings.js` 与 `src/linux/desktop/settings-pages.js`（将通用页“后台网络驻留运行”和“托盘未读悬浮预览”从 `display` 假状态迁入 `committed` 持久化模型）。
- [x] **1.3 新增 `src/linux/desktop/tray-manager.cjs`（≤ 320 行）**：
  - 封装 Electron `Tray` 创建与桌面可用性探测（**审核护栏 2**：使用 `child_process.execFile` 固定参数数组查询 D-Bus `org.kde.StatusNotifierWatcher` 的 `IsStatusNotifierHostRegistered`，超时 1500ms；禁止拼 Shell；命令缺失、报错或超时一律按 `statusNotifierHostAvailable = false` 处理）；
  - 记录真实托盘交互确认标志 `trayInteractionConfirmed`（用户点击托盘或菜单后置 `true`），`canHideToTray()` 仅在 `statusNotifierHostAvailable || trayInteractionConfirmed` 时返回 `true`；
  - 构建托盘菜单（“显示主界面”“系统设置...”“检查新版本...”“退出 LanChat”）、Tooltip 汇总（对齐 Windows `LanChat: N条未读 (...)`，63 字符截断）、系统 `Notification` 派发与点击触发 `activate-chat` 回调；
  - 450ms 未读图标闪烁与 320px 未读悬浮预览小窗仅在 Xorg 且 `trayPreview === true` 时作为可选增强启用（原生 Wayland 不依赖图标闪烁或全局坐标悬浮预览）。
- [x] **1.4 接入主进程与渲染层生命周期**：
  - 修改 `src/linux/desktop/main.cjs`：接入 `tray-manager.cjs`；`window.on('close')` 中，非显式退出且 `keepAliveInTray` 为真时，若 `trayManager.canHideToTray()` 则 `event.preventDefault(); window.hide();`（首次发“已最小化到托盘”通知），否则保守调用 `event.preventDefault(); window.minimize();` 保留 Dock/任务栏入口；仅在 `keepAliveInTray === false` 或显式退出时真正 `app.quit()`；处理 `lanchat:unread-state`；后台/失焦收到新 `message` 时触发 `Notification`；转发 `activate-chat`、`window-restored`、`open-update-check`；
  - 修改 `src/linux/desktop/preload.cjs`（新增 `syncUnreadState`）与 `src/linux/desktop/renderer.js`（未读变化上报摘要，响应 `activate-chat` 调用 `switchChat(key, true)` 并清未读，响应 `window-restored` 清当前会话未读）。
- [x] **1.5 编写阶段 1 回归并注册行数门禁**：新增 `tests/linux_desktop_lifecycle_regression.js`（≤ 300 行），覆盖托盘可用隐藏、D-Bus 超时/失败保守最小化、`keepAliveInTray=false` 退出、通知点击切会话、Tooltip/闪烁、托盘真退出、两个设置开关持久化；将其纳入 `tests/linux_desktop_ui_regression.js` `< 1000` 行门禁并跑绿全套测试。
- [ ] **1.6 检查点 `CP-1`（待 Linux 桌面实测）**：若有 Linux 桌面环境立即验证 GNOME Xorg / 原生 Wayland / XWayland 的托盘可见性、无 AppIndicator 最小化不失联与通知跳转；若无则保留在 `CRDocs/DEFERRED.md` 待阶段 5 现场核销。

### 阶段 2A · 低风险核心功能同步与现存缺陷修复（含 D2 = A）

- [x] **2A.1 修复 R1（11 色强调色保存缺陷）**：将 `src/linux/desktop/main.cjs:523` 与 `src/linux/LanChat.Linux/LinuxProfile.cs:112` 的强调色校验上限从 `5` 改为 `11`（允许 `0..10`），在 Node 与 Python 回归中补充 `accentPreset = 10` 的保存与重启恢复断言。
- [x] **2A.2 后端 `probe-ip`、`LC-FR-007` 与 CLI 历史一致性**：
  - 修改 `src/linux/LanChat.Linux/DesktopService.cs`：新增 `case "probe-ip":` 调用 `_network.ProbeIp(ip, ...)` 异步返回 `{ ok, message, peers }`；新增 `_runtimeAddresses` 记录运行期发现过的节点 IPv4 并合入 `KnownAddresses()`（补齐 `LC-FR-007`）；
  - 修改 `src/linux/LanChat.Linux/Program.cs`：启动时设置 `ChatHistoryService.ProfileStorageDirectory = profile.HistoryDirectory`，在 CLI 收发文本/文件成功时调用 `ChatHistoryService.SaveMessage(...)` 落盘（**审核护栏 3**：私聊键必须经 `SessionManager.GetLanKey(peerId, peerIp)` 生成，群聊用 `SessionManager.BROADCAST_KEY`，与 `DesktopService.cs:406-408` 严格一致）。
- [x] **2A.3 解除串行发文件 + 群聊 1～16 人多选接收人 + 单好友直发**：
  - 修改 `src/linux/desktop/main.cjs`：`dispatchFiles` 取消 `for...of await` 串行阻塞，改为并发提交给 `DesktopService`（由共享 C# 单目标 2 路 / 全局 6 路队列限流）；扩展 `select-files` / `send-paste` / `confirm-drop` 支持 `chatIds[]`（1～16 人），托管粘贴截图使用引用计数（所有接收人结算且无历史引用后才删临时文件）；
  - 修改 `src/linux/desktop/preload.cjs`、`renderer.js`、`index.html`：升级 `<dialog id="recipient">` 为复选多选弹窗（上限 16 人 + 超限提示 + 确定发送），仅 1 名在线好友时免弹窗直发。
- [x] **2A.4 轻量 IPv4 直连入口（D2 = A）**：
  - 在 `main.cjs` 注册 `lanchat:probe-ip`（`allowSettings = true`，校验合法 IPv4）并在 `preload.cjs` 暴露 `probeIp(ip)`；
  - 在 `src/linux/desktop/settings-pages.js`（“设备连接”页顶部卡片）与 `settings.js` 增加 IPv4 输入框 + `连接握手` 按钮，展示握手成功/失败状态，不改动主窗左侧栏布局。
- [x] **2A.5 496×548 表情面板 + 214/188px 工具栏外壳（保留 `<textarea>`）+ `Ctrl+F`/`Ctrl+L`**：
  - 修改 `src/linux/desktop/overlay.js` 与 `desktop.css`：升级表情选择器为 496×548 微信比例暗色浮层，包含“最近使用”（最多 12 项去重）与“所有表情”（8 列 48px 网格）及 1.5px 强调色悬停/焦点框；
  - 修改 `packaging/ExportLinuxDesktopAssets.cs` 与 `build-electron.py` 导出 `Scissors.png`；
  - 修改 `src/linux/desktop/index.html`、`desktop.css`、`renderer.js`：对齐 214/188px 微信式底部输入区（上方保留原生 `<textarea id="message">` 保持与 `preload.cjs:31-66` 粘贴兼容，下方展开 5 个 48×48 工具按钮：表情、打开接收目录、选择文件、屏幕截图、清空对话 + 右下 44×44 发送圆钮）；
  - 在 `main.cjs` 新增 `trigger-screenshot`（用 `execFile` 探测系统截图命令/Portal，不可用时返回友好提示“请使用系统截图快捷键截图后，在输入框按 Ctrl+V 粘贴”）；
  - 在 `renderer.js` 真实绑定 `Ctrl+F`（聚焦 `#search`）与 `Ctrl+L`（**审核护栏 1**：必须走 `confirmForget` 二次确认弹窗，严禁一键静默清空）。
- [x] **2A.6 阶段 2A 回归与行数门禁**：新增 `tests/linux_desktop_sync2a_regression.js`（≤ 320 行）并在 `tests/linux_desktop_regression.py` 扩展 `probe-ip`、`LC-FR-007`、11 色与 CLI 历史落盘测试；复跑全部 C# 编译与 Node/Python 回归，校验所有手写文件 `< 1000` 行。

### 阶段 3 · 设置页收口：B 类 4 项双端同键同步接线（D1 = A）

- [ ] **3.1 共享 `NetworkService` 最小扩展（不改 `NetworkService.Transfer.cs` 加刷盘）**：
  - 修改 `src/NetworkService.cs`、`src/NetworkService.Interface.cs`、`src/NetworkService.Discovery.cs`：新增 `public bool EnableTunFilter { get; set; } = true;`（控制 `DiscoverInterfaces` 过滤虚拟网卡）、`public bool EnableSubnetSweep { get; set; } = true;`（控制 `RefreshPeers` 是否启动 1～254 并发扫描）及只读 `GetActiveInterfacesSnapshot()`；默认值保持现行行为 100% 不变。
- [ ] **3.2 双端同键持久化与真实业务逻辑接线**：
  - 统一 JSON 键名：`TunFilter`（bool，默认 `true`）、`UnicastProbe`（bool，默认 `true`）、`AutoLoadHistory`（bool，默认 `true`）、`SendShortcutMode`（int，默认 `0`：`0=Enter 发送 / Shift+Enter 换行`，`1=Ctrl+Enter 发送 / Enter 换行`）；
  - **Windows 端**：修改 `src/Services/AppSettings.cs` 读写上述 4 键；修改 `src/MainForm.cs` / `MainForm.Chat.cs`（按 `AutoLoadHistory` 控制启动加载历史、按 `SendShortcutMode` 控制发送与换行快捷键、主窗响应 `Ctrl+F` / `Ctrl+L` 走二次确认）以及 `NetworkDeviceSettingsPanel.cs`、`ChatHistorySettingsPanel.cs`、`ShortcutsSettingsPanel.cs`、`GeneralSettingsPanel.cs` 接通真实回调；
  - **Linux 端**：修改 `src/linux/LanChat.Linux/LinuxProfile.cs`、`DesktopService.cs`（启动按 `AutoLoadHistory` 控制 `_sessions.LoadPersistedHistories()`；保存同步 `_network.EnableTunFilter`/`EnableSubnetSweep` 并在变更时 `RefreshPeers`；`state` 附带只读 `interfaces` 列表）及 `main.cjs`、`settings-pages.js`、`settings.js`、`renderer.js`、`presentation.js`（持久化 4 项并在主窗实时生效）。
- [ ] **3.3 C 类 4 项诚实只读收口**：确保通用页 `语言`、`链接打开方式`、文件传输页 `64KB 分块传输校验`、通用/设备页 `网络协议通道`（展示当前绑定网卡/端口摘要并可触发 `refresh`）与基线声明一致，不误导用户、不添加假业务。
- [ ] **3.4 双端全量回归验证**：立即跑通 `Compile.bat`、Windows `chat_composer_regression.ps1` / `friend_refresh_regression.ps1` / `file_transfer_stability_regression.ps1`，以及 Linux Node/Python 设置持久化与快捷键回归；确认全部文件 `< 1000` 行。

### 阶段 4 · Linux 在线更新与维护入口（含 R5 + 护栏 4）

- [ ] **4.1 新增 `src/linux/desktop/update-service.cjs`（≤ 280 行）**：
  - 双轨版本探测：`https://api.github.com/repos/yxiaojiang/LanChat/releases/latest`（8s 超时）+ 403 限流降级 `releases/latest` 302 重定向解析最新 Tag；
  - 自动按 `process.arch`（`x64->amd64`、`arm64->arm64`）与 `LANCHAT_INSTALLED` 选中 `.deb` 或 `_portable.tar.gz`；
  - 严格按 `release/linux-electron/SHA256SUMS.json` 嵌套结构 `manifest[arch].artifacts[assetName]` 校验；403 降级时同样必须拉取对应 Tag 的 `SHA256SUMS.json`，若不可获取或缺字段则**禁止静默跳过**，返回未校验警告并只允许打开 GitHub Release 网页；
  - 流式下载至 `${XDG_CACHE_HOME:-~/.cache}/lanchat/updates`（强制目录权限 `0o700`，禁止 `/tmp/LanChatUpdate/`），写 `.part` 临时文件并在 SHA-256 核验通过后原子重命名，校验失败立即删除坏包；
  - 生成退锁安装命令（**审核护栏 4**：单引号包裹规范化绝对路径 `sudo apt install '<normalizedAbsoluteSavePath>'`）。
- [ ] **4.2 接入更新 IPC 与 520×440 更新弹窗**：修改 `main.cjs` / `preload.cjs` 暴露 `checkUpdate`、`downloadUpdate`、`openUpdateLocation`、`openReleasePage`（渲染层保持 `default-src 'none'` CSP，远端文案全用 `textContent`）；修改 `settings.html` / `settings.css` / `settings.js` 实现 520×440 更新弹窗与“复制安装命令并退出 LanChat”退锁流，并打通托盘菜单“检查新版本...”。
- [ ] **4.3 新增 `tests/linux_desktop_update_regression.js`（≤ 220 行）**：Mock HTTP 覆盖最新版提示、双架构与安装/便携资产匹配、403 降级强制拉取清单、清单缺失阻断警告、篡改哈希删除坏包及单引号命令格式，注册进 `< 1000` 行门禁并跑绿。

### 阶段 2B · 内联表情富文本输入框专项（D3 = A，受 `CP-2B` 门禁控制）

- [ ] **2B.1 重构 `src/linux/desktop/preload.cjs:31-66` 粘贴适配器**：抽离文本读取、光标/选区范围插入、最大长度裁剪与 `input` 事件派发接口，消除对原生 `<textarea>` 的 `value`/`selectionStart`/`setRangeText` 强耦合，确保同时兼容 `<textarea>` 与 `contenteditable`。
- [ ] **2B.2 新增 `src/linux/desktop/composer-box.js`（≤ 360 行）**：实现输入框内联 PNG 表情基线对齐、点击表情内部光标收拢至左右两侧、`Backspace`/`Delete` 整枚原子删表情、2px 主题色光标、暗色右键粘贴/粘贴附件菜单、`copy`/`cut` 还原 `[表情名]` 纯文本、`compositionstart`/`compositionend` 中文 IME 保护，以及**一键回退原生 `<textarea>` 保护开关**（`CP-2B` 实测通过前保持可回退/不盲目锁死）。
- [ ] **2B.3 专项自动化回归与 `CP-2B` 实机门禁**：新增 `tests/linux_desktop_composer_regression.js`（≤ 260 行）验证令牌还原、光标收拢、原子删除、剪贴板粘贴与 `isComposing` 保护；待真实 Linux `fcitx5` 与 `ibus` 下实测通过后才默认启用。

### 阶段 5 · 双架构构建与物理实机 / 真实 LAN 总验收

- [ ] **5.1 构建双架构候选产物**：运行 `python Tools/Scripts/BuildLinuxDeb.py` 生成最新 `amd64`/`arm64` `.deb`、`_portable.tar.gz`、`lanchat-cli` 与 `SHA256SUMS.json`，校验 ELF64 机器码（62 / 183）。
- [ ] **5.2 Linux 网络命名空间 ELF 全量回归（关闭 `CP-0` / `LC-LINUX-002-ENV-001` / `LC-FR-ENV-002`）**：对最新 `amd64` 与 `arm64` ELF 跑通 `python3 tests/linux_desktop_regression.py <elf>` 全量用例。
- [ ] **5.3 物理 `x86_64` 与物理 `ARM64` 实机桌面验收（关闭 `CP-1`、`CP-2B`、`LC-LINUX-002-GUI-001`）**：覆盖 GNOME Xorg / 原生 Wayland / XWayland 托盘与通知、`fcitx5` 与 `ibus` 中文预编辑、100%/125%/150% DPI 叠图、2000 条长历史性能、旧 `v2.0.16` 升级与 `apt install --allow-downgrades` 回退、便携包与无头 CLI。
- [ ] **5.4 物理 Windows 与 Linux 真实局域网总验与审核收口**：实测双向发现、切网、TUN 过滤切换、双向大文件/同名并发/群聊多选 SHA-256 与旧版互通，逐项核销 `CRDocs/DEFERRED.md` 并进入 `CRDocs` 独立代码审核。

---

## 当前基线与入口索引

- 版本：`V3.0`（程序输出版本 `2.0.26`）
- 审核与延期：`CRDocs/STATUS.md` · `CRDocs/REVIEW.md` · `CRDocs/DEFERRED.md`
- 计划与定稿：`PlanDocs/LC-LINUX-003/FINAL.md` · `PlanDocs/V4.0-UNIFICATION.md` · `PlanDocs/DECISIONS.md`
- 上下文：`contexts/Windows.md` · `contexts/Linux.md` · `contexts/Protocol.md`
