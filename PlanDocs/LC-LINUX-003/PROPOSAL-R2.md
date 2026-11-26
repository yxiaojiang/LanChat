# Windows 当前功能同步到 Linux · 差异审计与分阶段实施计划

**LC-LINUX-003 · 第 2 轮 · 提案方 Argon · 2026-11-26**

本提案以当前 `main` 分支（`v2.0.26`）的 Windows WinForms 基线与 Linux Electron + 自包含 C# ELF 源码为依据，结合 [PlanDocs/LC-LINUX-002 定稿](../LC-LINUX-002/FINAL.md)、[CRDocs/DEFERRED.md](../../CRDocs/DEFERRED.md) 及第 1 轮独立审核意见（[REVIEW-R1.md](REVIEW-R1.md)）完成第 2 轮修订。当前仅完成方案修订与交接，未修改业务代码、未提交、未发布。

---

## 要决定的问题

本轮需用户及审核方拍板以下 3 项范围与交互边界决定（不影响阶段 0 与阶段 1 启动）：

| 编号 | 议题 | 可选方案与提案方建议 | 最迟决定时间 |
| :---: | --- | --- | --- |
| **D1** | **R2 B 类 4 项设置的双端同步接线范围**（`TUN/TAP 过滤`、`多网卡并发单播探测`、`自动加载历史消息`、`发送快捷键切换`） | **选项 A（建议）**：4 项均为低复杂度实用项，若实施则在 Windows `AppSettings.cs` 与 Linux `LinuxProfile.cs` 使用统一 JSON 键名，并**双端设置面板同步接线持久化**，确保默认值与当前行为一致，消除配置漂移。<br>**选项 B**：本轮先仅在 Windows/Linux 维持与当前一致的只读/基线展示，将 4 项另案排期。 | 阶段 3 开始前 |
| **D2** | **“手动输入 IPv4 直连握手（`probe-ip`）”的 UI 入口位置** | **选项 A（建议）**：放入设置中心 **“设备连接”页** 卡片顶部，不改动主窗左侧栏现有视觉布局。<br>**选项 B**：放入主窗左侧栏底部刷新按钮旁，方便高频跨网段手动握手。 | 阶段 2A 开始前 |
| **D3** | **阶段 2B（输入框内联 PNG 表情富文本编辑器）是否纳入本轮交付** | **选项 A（建议）**：阶段 2A 先交付 214/188px 微信式高输入区、展开工具栏、496×548 表情面板与群聊多选，**保留原生 `<textarea>`**；阶段 2B 作为高风险独立专项，完成 `preload.cjs` 粘贴适配器重构及真实 Linux `fcitx5`/`ibus` 实测后，再决定是否默认开启。<br>**选项 B**：暂不做 Linux 输入框内联 PNG 表情渲染，输入框统一保留 `[表情名]` 原文显示（气泡与侧栏仍正常显示 PNG 表情）。 | 阶段 2B 启动前 |

---

## 建议决定

### 1. 差异矩阵与缺陷定性（按缺失/缺陷 → 仅展示/假功能 → 待验收 → 已同步）

> **核心修正**：采纳 R1、R2、R4，将“Linux 11 色强调色保存上限写死 5”从“待验收”移出登记为 **现存 P1 代码缺陷**；将 11 项假功能明确拆分为 **A 类必做 / B 类待用户确认双端同步 / C 类明确不做**；剥离 Windows 主界面已无可见入口的“新建节点（14 形态）”空壳窗。

#### 1.1 明确缺失与现存代码缺陷（10 项，共分阶段实施）

| 优先级 | 功能 / 缺陷 | Windows 现行基线 | Linux 当前状态 | 状态定性 | IPC/协议影响 | 归属阶段 |
| :---: | --- | --- | --- | :---: | --- | :---: |
| **P1** | **外观 11 色强调色保存缺陷（R1）** | `AppearanceSettingsPanel.cs` 支持 11 种强调色（`0..10`） | `appearance.js:9-21` 与 `LinuxProfile.cs:31` 读取支持 11 色，但 `main.cjs:523` 与 `LinuxProfile.cs:112` 保存校验仍写死 `accentPreset >= 5`，选第 6～11 色直接报“强调色预设无效” | **现存代码缺陷** | 无新 IPC，修复双处上限 `5 -> 11` | **阶段 2A** |
| **P0** | **桌面关闭隐藏到托盘（含无托盘保守回退）** | `MainForm.Tray.cs:285-303` | `main.cjs:80-84` 直接 `app.quit()` | **明确缺失** | 主进程新增 `tray-manager.cjs` 与 `keepAliveInTray` 联动 | **阶段 1** |
| **P0** | **托盘恢复主窗口与托盘菜单真退出** | `MainForm.Tray.cs:223-283` | 仅主窗左下角退出与 `second-instance`，无 `Tray` | **明确缺失** | 主进程托盘点击/菜单恢复 + 真退出 | **阶段 1** |
| **P0** | **后台系统通知与点击通知进入对应会话** | `MainForm.Chat.cs:110-128`、`MainForm.Tray.cs:527-535` | 无 Electron `Notification` | **明确缺失** | 主进程发 `Notification`；渲染层响应 `activate-chat` | **阶段 1** |
| **P1** | **未读托盘提醒与 Xorg 可选闪烁/悬浮预览** | `MainForm.Tray.cs:418-658`、`GrokTrayPreviewPopup.cs` | 仅主窗红点 | **明确缺失** | 新增 `lanchat:unread-state`；闪烁与 320px 悬浮预览限 Xorg 可选增强 | **阶段 1** |
| **P1** | **手动 IPv4 直连握手（`probe-ip`）轻量入口** | `NetworkService.ProbeIp`（`MainForm.Designer.cs:92,103` 旧按钮已 `Visible = false`） | 仅 CLI `Program.cs:296-303` 有 `/add <IP>`，GUI 无 | **明确缺失** | `DesktopService.cs` 新增 `probe-ip`；入口位置按 D2 选定 | **阶段 2A** |
| **P1** | **群聊文件多选接收人（1～16 人）与单好友免弹窗直发** | `MainForm.Files.cs:296-430` | `renderer.js:310-334` 仅单选 1 人且 1 人在线也弹窗 | **明确缺失** | `main.cjs` 扩展 `chatIds[]` + 托管截图引用计数清理 | **阶段 2A** |
| **P1** | **微信比例 496×548 表情选择器（最近 12 项 + 8 列 48px）** | `ChatEmojiPicker.cs`（`LC-CC-007`） | `overlay.js:250-273` 仍为 384px 单网格，无最近使用 | **明确缺失** | 纯渲染层 `overlay.js` 升级 | **阶段 2A** |
| **P1** | **214/188px 微信式高输入区与展开工具栏（阶段 2A） / 内联表情富文本（阶段 2B）** | `MainForm.Designer.cs:432-561`、`ChatInputBox.cs` | `index.html:57-63` 仍为 64px 单行胶囊 + `<textarea>` | **明确缺失** | 阶段 2A 先对齐 214/188px 工具栏外壳并保留 `<textarea>`；内联 PNG 表情拆入阶段 2B（按 D3） | **阶段 2A / 2B** |
| **P1** | **Linux `amd64`/`arm64` 检查更新与 SHA-256 校验下载** | `UpdateService.cs`、`GrokUpdateForm.cs` | `settings.js:420-427` 仅占位提示框 | **明确缺失** | 新增 `update-service.cjs` 与 `check-update` / `download-update` IPC | **阶段 4** |

> 另有 3 处隐藏源码偏差在 **阶段 2A** 一并修复：
> 1. `main.cjs:357-362` `dispatchFiles` 的 `for...of await` 串行等待压制了共享 C# 核心单目标 2 路 / 全局 6 路并发，改为并发派发交由 C# 队列统一限流；
> 2. `DesktopService.cs:340-341` `KnownAddresses()` 仅查归档会话，漏掉运行期上线过但未聊天的节点 IP（`LC-FR-007`），补充 `_runtimeAddresses` 缓存合并；
> 3. `Program.cs:70-186` 独立 CLI 未绑定 `ChatHistoryService.ProfileStorageDirectory`，补充绑定使 CLI 与 GUI 历史落盘 100% 一致。

#### 1.2 11 项“仅展示/假功能”三类分级清单（R2）

| 类别 | 项目 | Windows 基线 | Linux 当前状态 | 处置方式与阶段 |
| :---: | --- | --- | --- | --- |
| **A 类（必接真业务）** | **1. 检查更新** | `UpdateService.cs` 已接通 | `settings.js:420-427` 占位弹窗 | **阶段 4** 接通 GitHub API + 302 降级 + 嵌套 `SHA256SUMS.json` 校验下载 |
| **A 类（必接真业务）** | **2. 后台网络驻留运行** | `GeneralSettingsPanel.cs:186` 空回调 | `settings-pages.js:234-236` 空回调 | **阶段 1** 持久化 `KeepAliveInTray`，受托盘真实可用性约束（Windows 同步可按需补落盘） |
| **A 类（必接真业务）** | **3. 托盘未读悬浮预览** | `GeneralSettingsPanel.cs:170` 空回调 | `settings-pages.js:230-232` 空回调 | **阶段 1** 持久化 `TrayPreview`，在 Xorg 可选悬浮预览中生效 |
| **A 类（必接真业务）** | **4. 主窗快捷键 `Ctrl+F` / `Ctrl+L`** | `ShortcutsSettingsPanel.cs` 静态展示 | `settings-pages.js:354-364` 静态展示 | **阶段 2A** 在主窗真实绑定 `Ctrl+F`（聚焦搜索）与 `Ctrl+L`（清空当前会话） |
| **B 类（低复杂度可选项，待 D1 拍板）** | **5. TUN/TAP 过滤** | `NetworkDeviceSettingsPanel.cs:67` 空回调 | `settings-pages.js:419-421` 空回调 | 若 D1 选 A，**阶段 3** 在 `AppSettings.cs` 与 `LinuxProfile.cs` 同键持久化并同步双端面板 |
| **B 类（低复杂度可选项，待 D1 拍板）** | **6. 多网卡并发单播探测** | `NetworkDeviceSettingsPanel.cs:71` 空回调 | `settings-pages.js:422-424` 空回调 | 若 D1 选 A，**阶段 3** 在 `AppSettings.cs` 与 `LinuxProfile.cs` 同键持久化并同步双端面板 |
| **B 类（低复杂度可选项，待 D1 拍板）** | **7. 自动加载历史消息** | `ChatHistorySettingsPanel.cs:87` 空回调 | `settings-pages.js:398-400` 空回调 | 若 D1 选 A，**阶段 3** 在 `AppSettings.cs` 与 `LinuxProfile.cs` 同键持久化并同步双端面板 |
| **B 类（低复杂度可选项，待 D1 拍板）** | **8. 发送快捷键切换** | `ShortcutsSettingsPanel.cs` 静态徽章 | `settings-pages.js:354-364` 静态徽章 | 若 D1 选 A，**阶段 3** 在 `AppSettings.cs` 与 `LinuxProfile.cs` 同键持久化 `SendShortcutMode` 并双端生效 |
| **C 类（本轮明确不做）** | **9. 语言（三语 i18n）** | `GeneralSettingsPanel.cs:250-258` 仅改文本 | `settings-pages.js:196-202` 假切换 | **不做**，保持单语基线展示或只读说明，防止半中半英割裂 |
| **C 类（本轮明确不做）** | **10. 链接打开方式（内置预览）** | `GeneralSettingsPanel.cs:260-267` 仅改文本 | `settings-pages.js:203-209` 假切换 | **不做**，保持纯局域网与渲染层 `default-src 'none'` 严格 CSP 边界 |
| **C 类（本轮明确不做）** | **11. 64KB 分块传输校验 & 网络协议通道** | `FileTransferSettingsPanel.cs:101` / `GeneralSettingsPanel.cs:191` 空展示 | `settings-pages.js:380-382` / `237-239,417` 假控件 | **不做**假开关；不加拖慢传输的 `fs.Flush(true)`；改为诚实只读能力/绑定网卡信息展示（可触发既有 `refresh`） |

#### 1.3 代码已有但未验收（16 项，需实机/ELF 证据核销）

含：Wayland/Xorg/XWayland 窗口与桌面等效行为、`Enter`/`Shift+Enter`/`Ctrl+Enter` 换行、`fcitx5`/`ibus` 中文预编辑、文件选择器多选与拖放、失败/取消/断线清理与托管截图生命周期、双架构 `.deb` 升级与回退、物理 Windows 与 Linux 真实 LAN 互通、节点识别码/设备信息复制、节点重启/改名/离线归档与重新上线、会话分组/未读/搜索/键盘导航、文本与附件草稿隔离、109 表情回显与 Unicode 组合表情、系统剪贴板图片/文件粘贴、逐文件进度与 6/2 并发限流、打开文件/目录与接收/历史目录迁移、便携包与独立 CLI 及旧 Python 版兼容。（均延续 [CRDocs/DEFERRED.md](../../CRDocs/DEFERRED.md) 口径，不以宿主机测试冒充实机通过。）

#### 1.4 已同步（4 项）

群聊/私聊基础文本收发与 `LANC` 封包、主窗侧栏会话模糊搜索、同名文件并发接收独立 `.downloading` 保护、全界面 4px/5px 自绘暗灰浮动滑块（Zero White Scrollbar）。

---

### 2. 关键技术设计修正（回应 R3、R4、R5）

1. **托盘真实可见性判定与 Xorg/Wayland 验收分流（R3）**：
   - `trayManager.canHideToTray()` **禁止仅以 `new Tray(...)` 不抛错为准**（默认 Ubuntu GNOME 无 AppIndicator 扩展时，`new Tray()` 成功返回但图标完全不可见）。
   - **判定逻辑**：
     1) 启动时主进程查询会话总线 `org.kde.StatusNotifierWatcher` 的 `IsStatusNotifierHostRegistered` 属性（可通过 `gdbus`/`dbus-send` 一次性探测）；若返回 `true`，记为 `statusNotifierHostAvailable = true`；
     2) 若运行期间用户实际点击过托盘图标或右键菜单，置位 `trayInteractionConfirmed = true`；
     3) **保守回退规则**：仅当 `keepAliveInTray && (statusNotifierHostAvailable || trayInteractionConfirmed)` 为真时，点击关闭窗口才执行 `window.hide()`；否则一律回退为 `window.minimize()`（保留在 Dock / 任务切换器可见可恢复）或弹窗确认退出，且 `second-instance` 始终可唤回同一窗口与 ELF，杜绝窗口失联。
   - **Xorg / Wayland 分流**：450ms 托盘图标闪烁与 320px 未读悬浮预览卡片仅列为 **Xorg 可选增强**；原生 Wayland 以“应用内未读红点 + 系统 `Notification` + 点击通知跳转对应会话（桌面通知服务支持 action 时）”作为验收基准。

2. **输入区升级拆分为阶段 2A 与阶段 2B，补充 `preload.cjs` 适配器与截图探测（R4）**：
   - **阶段 2A（低风险，保留原生 `<textarea id="message">`）**：升级 214/188px 微信式底部输入区容器与展开式快捷工具栏（表情、打开接收目录、选择文件、截图、清空对话），升级 496×548 表情面板。因保留 `<textarea>`，`preload.cjs:31-66` 粘贴拦截与 `fcitx5`/`ibus` 中文输入零风险。
   - **Linux 截图按钮真实行为**：点击截图按钮时，主进程探测系统可用截图工具（优先检查 `xdg-desktop-portal` Screenshot 接口或 `gnome-screenshot` / `spectacle` / `flameshot`）；在 Ubuntu 24.04 移除 `gnome-screenshot` 或 Wayland Portal 不可用/被拒绝时，不崩溃不静默失败，直接给出非阻塞提示“请使用系统截图快捷键（如 PrtSc / Shift+PrtSc）截图后，在输入框按 Ctrl+V 粘贴”。
   - **阶段 2B（高风险专项，受 D3 控制）**：若启用内联 PNG 表情 `composer-box.js`，必须同步改造 `preload.cjs:31-66` 的纯文本回落粘贴逻辑——抽离读写适配器统一封装文本获取、光标/选区范围插入、长度上限与 `input` 派发（不再直接依赖 `target.value`、`selectionStart`、`setRangeText`），保留 `compositionstart`/`compositionend` 保护与一键回退 `<textarea>` 的保护开关，并须先在真实 Linux `fcitx5`/`ibus` 通过预编辑与粘贴验收后方可默认开启。

3. **更新校验、403 降级安全门禁与独占目录退锁流（R5）**：
   - **嵌套清单解析**：严格按 [SHA256SUMS.json](../../release/linux-electron/SHA256SUMS.json) 实际结构 `manifest[arch].artifacts[assetName]`（`arch` 为 `"amd64"` 或 `"arm64"`）匹配校验和。
   - **403 降级强制摘要核验**：当 GitHub API 返回 403 走 `releases/latest` 302 重定向获取版本号 `<ver>` 时，仍必须请求 `https://github.com/yxiaojiang/LanChat/releases/download/v<ver>/SHA256SUMS.json`；**若摘要文件不可获取或缺少对应资产字段，禁止静默跳过校验**，须向用户展示明确的“无法验证安装包 SHA-256 完整性”安全警告并仅提供打开 GitHub Release 网页入口。
   - **用户独占缓存目录**：禁止使用多用户共享 `/tmp/LanChatUpdate/`，改用 `${XDG_CACHE_HOME || ~/.cache}/lanchat/updates`（创建权限强制 `0o700`），流式写入 `.part` 临时文件，SHA-256 核验通过后再原子重命名。
   - **退锁安装流**：非 root Electron 不承诺静默覆盖安装 `.deb`；下载校验完成后，提供“复制安装命令并退出 LanChat”（将 `sudo apt install "<savePath>"` 复制到剪贴板并打开所在目录），引导用户先退出以释放配置锁与 38888/38889 端口后再升级。

4. **全工程遵循行数守恒与硬边界**：
   - 保持 `LANC` 魔数/报文结构、`JSONL` 历史格式、Windows 不回归、共享 C# 网络核心不重写、GUI 与 CLI 共用 `LinuxProfile` 路径与文件锁；
   - 新增逻辑拆分为独立模块（`tray-manager.cjs`、`composer-box.js`、`update-service.cjs` 及独立测试文件），先拆分 `tests/linux_desktop_ui_regression.js`（当前 924 行）辅助函数，确保全部手写源码 `< 1000` 行。

---

## 证据

以下事实均在本轮与第 1 轮核对 `main` 分支（`v2.0.26`）当前源码与构建产物后确认：

1. **R1 现存 Bug 实证**：[appearance.js](../../src/linux/desktop/appearance.js#L9-L21) 与 [settings-model.js](../../src/linux/desktop/settings-model.js#L34) 提供 11 种强调色，`LinuxProfile.cs:31` 读取校验允许 `< 11`，但 [main.cjs:523](../../src/linux/desktop/main.cjs#L523) 与 [LinuxProfile.cs:112](../../src/linux/LanChat.Linux/LinuxProfile.cs#L112) 保存校验仍写死 `>= 5`，选择第 6～11 色必报错。
2. **R2/R4 源码基线实证**：
   - `MainForm.Designer.cs:92,103` 中旧节点按钮均为 `Visible = false`，Windows 当前主窗无连接/新建节点入口；
   - `GeneralSettingsPanel.cs`、`NetworkDeviceSettingsPanel.cs`、`ChatHistorySettingsPanel.cs`、`ShortcutsSettingsPanel.cs` 与 `settings-pages.js` 证实 10 项双端控件为空回调/静态展示，`settings.js:420-427` 检查更新为占位提示，而 Windows `UpdateService.cs:40-171` 已接通真实更新检查；
   - `main.cjs:357-362` `dispatchFiles` 确为 `for...of await` 串行等待；`DesktopService.cs:340-341` `KnownAddresses()` 仅查归档；`Program.cs:70-186` 未绑定 `ChatHistoryService.ProfileStorageDirectory`；
   - `preload.cjs:31-66` 剪贴板粘贴回落强依赖原生 `<textarea id="message">` 的 `target.value`、`selectionStart`、`selectionEnd`、`maxLength` 与 `setRangeText`。
3. **R5 清单实证**：[release/linux-electron/SHA256SUMS.json](../../release/linux-electron/SHA256SUMS.json) 实际结构为 `{ "amd64": { ..., "artifacts": { "lanchat_2.0.26_amd64.deb": "..." } }, "arm64": { ... } }`。
4. **门禁行数实证**：[tests/linux_desktop_ui_regression.js](../../tests/linux_desktop_ui_regression.js#L138-L149) 当前 924 行，且第 138–149 行硬断言 17 个源码与测试文件行数 `< 1000`。

---

## 未核实

以下内容尚未在真实 Linux 桌面与物理局域网环境下实测，将在前置检查点（`CP-0`、`CP-1`、`CP-2B`）与阶段 5 现场取证核销：

1. **CP-0（最新 ELF 命名空间基线）**：当前 `v2.0.26` ELF 在 Linux 非 root 仅回环网络命名空间运行 `tests/linux_desktop_regression.py` 的实际输出（关闭 `LC-LINUX-002-ENV-001`）。
2. **CP-1（桌面托盘与等效行为）**：Ubuntu 22.04/24.04 GNOME（Xorg / 原生 Wayland / XWayland）及银河麒麟 V10 ARM64 上 `org.kde.StatusNotifierWatcher` D-Bus 查询结果、Electron `Tray` 实际可见性、无托盘回退最小化与 `Notification` 点击跳转实测。
3. **CP-2B（中文输入法预编辑，若启用阶段 2B）**：`fcitx5` 与 `ibus` 在原生 `<textarea>` 与 `contenteditable` 中的真实中文预编辑、候选上屏、撤销与 `preload.cjs` 粘贴表现。
4. **阶段 5（双架构物理实机与真网兼容）**：物理 `x86_64`、物理 `ARM64` 与物理 Windows 主机在同一局域网内的自动发现、切网、TUN 虚拟网卡、100%/125%/150% DPI 叠图、2000 条长历史性能、`apt install` 升级/降级回退及旧 Python `v2.0.16` 互通取证。

---

## 建议顺序

采纳 R6，**将 Linux 实跑验证拆为前置检查点，不积压到阶段 5**：

| 阶段 | 实施内容 | 前置/放行检查点 |
| --- | --- | --- |
| **阶段 0 · 基线冻结与既有 ELF 摸底** | 冻结 `v2.0.26` 差异矩阵与 IPC 契约；先拆分 `tests/linux_desktop_ui_regression.js`（924 行）公共辅助函数腾出行数空间；复跑宿主机 4 套基线回归。 | **CP-0**：在 Linux 环境对现有 `v2.0.26` ELF 跑通一次 `linux_desktop_regression.py` 摸底；明确 **D1 / D2 / D3** 的用户决定。 |
| **阶段 1 · 桌面生命周期、托盘与通知（含 A 类托盘开关）** | 新增 `tray-manager.cjs` 与托盘图标导出；按 R3 的 `StatusNotifierWatcher` 真实可见性探测与保守回退实现关闭隐藏/最小化、托盘/启动器恢复、系统通知与点击跳会话、真退出；接通设置页 `KeepAliveInTray` 与 `TrayPreview` 持久化；Xorg 闪烁/悬浮预览列为可选增强。 | `linux_desktop_lifecycle_regression.js` 全绿；**CP-1：尽早在 1 台 Linux 桌面（GNOME Xorg/Wayland）实测托盘可见性与无托盘不失联**。 |
| **阶段 2A · 低风险核心对齐与缺陷修复** | 修复 **R1**（`main.cjs:523` & `LinuxProfile.cs:112` 11 色上限）；`DesktopService` 补 `probe-ip` 与 `LC-FR-007` `_runtimeAddresses`；`Program.cs` CLI 绑定 `ChatHistoryService.ProfileStorageDirectory`；群聊 1～16 人多选发文件（1 人在线直发）+ 托管截图引用计数；解除 `main.cjs` `dispatchFiles` 串行 `await`；按 D2 增加轻量 IPv4 直连入口；升级 496×548 表情面板（最近 12 项）与 214/188px 工具栏外壳（保留 `<textarea>` + 截图工具探测提示）；主窗绑定 `Ctrl+F` / `Ctrl+L`。 | C# 零警告编译、Windows 原生编译与回归零退化、Node/Python 新增回归全绿，所有手写文件 `< 1000` 行。 |
| **阶段 3 · 设置页收口（按 D1 决定执行）** | 若 D1 采纳双端接线，则在 `AppSettings.cs`、`LinuxProfile.cs` 与双端面板同步接通 B 类 4 项（`TunFilter`、`UnicastProbe`、`AutoLoadHistory`、`SendShortcutMode`），保持默认值与现行行为一致且配置键双端统一；C 类 4 项保持诚实只读/说明展示。 | 对应设置持久化、只读备用确认与重启恢复回归全绿；Windows 编译与好友/文件回归全绿。 |
| **阶段 4 · 在线更新与维护入口** | 新增 `update-service.cjs`（GitHub API + 302 降级、`amd64`/`arm64` 与 `.deb`/`portable` 资产匹配、按 R5 解析 `manifest[arch].artifacts[assetName]`、403 降级强制拉取校验清单或阻断警告、`${XDG_CACHE_HOME:-~/.cache}/lanchat/updates` `0o700` 独占目录下载与退锁复制命令引导）+ 520×440 更新弹窗，接通通用页/关于页/托盘菜单。 | `linux_desktop_update_regression.js` Mock 回归全绿；渲染进程严格 CSP 无放宽。 |
| **阶段 2B · 内联表情富文本输入框专项（可与阶段 3/4 并行，受 D3 控制）** | 若 D3 启用：重构 `preload.cjs` 剪贴板粘贴适配器兼容 `contenteditable`，实现 `composer-box.js` 内联 PNG 表情基线对齐、光标收拢与原子删表情，保留回退原生 `<textarea>` 开关。 | **CP-2B**：必须在真实 Linux `fcitx5` 与 `ibus` 下实测预编辑、选词、退格、复制粘贴与发送无异常后才可默认开启。 |
| **阶段 5 · 双架构构建与物理实机/真网总验** | 用 `BuildLinuxDeb.py` 构建双架构 `.deb`、便携包与独立 CLI；在物理 `x86_64`、物理 `ARM64` 与物理 Windows 主机上完成 Xorg/Wayland/XWayland、`fcitx5`/`ibus`、多 DPI、2000 条历史、旧包升级回退、真实 LAN（含 TUN/切网）与 `CRDocs/DEFERRED.md` 清零。 | 实机截图、日志、`dpkg -l`、ELF 机器码与 SHA-256 证据齐全，进入 `CRDocs` 独立代码审核。 |

---

## 明确不做

以下事项明确不纳入本轮实施范围：

1. **Windows 废弃节点空壳窗**：不做“新建节点（14 种形态调色盘）”空壳窗，也不在聊天输入工具栏强加旧版节点弹窗按钮。
2. **R2 C 类 4 项假功能过度造轮子**：
   - 不做单边三语国际化词典（`语言` 保持基线展示）；
   - 不做破坏纯内网边界与 `default-src 'none'` CSP 的“内置极速网页预览”（`链接打开方式` 保持基线展示）；
   - 不在 `NetworkService.Transfer.cs` 中添加降低流式传输吞吐的 `fs.Flush(true)`（`64KB 分块传输校验` 改为只读说明展示）；
   - 不另造复杂通道切换逻辑（`网络协议通道` 仅展示当前绑定网卡/端口只读信息或触发既有 `refresh`）。
3. **非 root 静默覆盖安装 `.deb`**：不承诺在无 root 权限的 Electron 进程中静默执行 `dpkg`/`apt` 覆盖安装，统一走退锁后复制 `sudo apt install` 命令引导。
4. **底层协议与框架重写**：不改动 `LANC` 魔数/报文与 `JSONL` 历史格式；不在 Node.js 重写局域网套接字；不修改业务代码、不发布（本轮仅完成 PlanDocs 提案修订与交接）。

---

## 对上一轮审核的处理

第 1 轮提案与审核原文已归档：[第 1 轮提案](PROPOSAL-R1.md) · [第 1 轮审核](REVIEW-R1.md)。

| 意见编号 | 处理 | 依据与提案改动位置 |
| --- | :---: | --- |
| **R1 · 前置修复 11 色强调色保存上限写死为 5** | **采纳** | 核对 `main.cjs:523` 与 `LinuxProfile.cs:112` 确仍写死 `>= 5`，与 `appearance.js:9-21` 的 11 色冲突。已从“待验收”移出，单列为「建议决定 1.1」首项现存 P1 缺陷及「证据 1」，并在「建议顺序」阶段 2A 修复并补充 `accentPreset = 10` 回归断言。 |
| **R2 · 将 11 项假功能按 A/B/C 三类分级** | **采纳** | 核对 `LC-LINUX-002/FINAL.md` 与 `NetworkService.Transfer.cs`，避免单边造轮子与 `Flush(true)` 降速。已在「建议决定 1.2」明确 A 类必做、B 类待 **D1** 拍板双端同步接线（同键名同步 `AppSettings.cs` 与 Windows 面板）、C 类 4 项写入「明确不做」。 |
| **R3 · 补齐托盘可见性真实判定与 Xorg/Wayland 验收分流** | **采纳** | 默认 GNOME 无 AppIndicator 时 `new Tray()` 不抛错但图标不可见。已在「建议决定 2.1」定义基于 D-Bus `org.kde.StatusNotifierWatcher` (`IsStatusNotifierHostRegistered`) 与真实托盘交互确认的判定口径及最小化保守回退，并将 450ms 闪烁与 320px 悬浮预览限定为 Xorg 可选增强。 |
| **R4 · 拆分阶段 2 为 2A/2B、剥离节点空壳并补 `preload.cjs` 与截图方案** | **采纳** | 核对 `MainForm.Designer.cs:92,103` 与 `preload.cjs:31-66`。已在「明确不做」剔除“新建节点（14 形态）”空壳；将轻量 `probe-ip` 入口位置设为 **D2**；将高风险内联表情输入拆入受 **D3** 控制的阶段 2B 并要求重构 `preload.cjs` 粘贴适配器；阶段 2A 保留 `<textarea>` 并明确 Linux 截图探测与提示降级。 |
| **R5 · 补全嵌套 `SHA256SUMS.json`、403 降级校验与 `0o700` 目录/退锁流** | **采纳** | 核对 `release/linux-electron/SHA256SUMS.json` 实际为 `manifest[arch].artifacts[assetName]`。已在「建议决定 2.3」明确嵌套解析、403 降级必须拉取摘要否则阻断/警告禁止静默跳过、废除 `/tmp/LanChatUpdate/` 改用 `${XDG_CACHE_HOME:-~/.cache}/lanchat/updates`（`0o700`），并明确退锁后复制安装命令。 |
| **R6 · 合规写入 `PlanDocs/PROPOSAL.md` 并前置 Linux 实跑检查点** | **采纳** | 已按 `PlanDocs/README.md` 规定的 7 个二级标题写入本文件，并将第 1 轮稿件归档至 `PlanDocs/LC-LINUX-003/`；在「未核实」与「建议顺序」增设 `CP-0`（既有 `v2.0.26` ELF 摸底）、`CP-1`（阶段 1 桌面托盘实测）与 `CP-2B`（阶段 2B 输入法实测），不再将全部实跑堆到阶段 5。 |
