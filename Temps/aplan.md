
根据项目根目录的 [Rules.md](file:///d:/TEmp/LanChat/.claude/Rules.md) 及 10 份核心记忆文档、计划文档与 `main` 分支（`v2.0.26`）最新双端源码的逐行交叉审计，现输出 **“Windows 当前功能同步到 Linux”完整差异矩阵与分阶段实施计划**。

> **说明（遵守项目规则）**：当前处于规划与审计阶段，**未修改任何源码、未提交、未发布**。文中涉及专业术语时均附带通俗解释（像搭积木一样把前后端分工讲清楚）。

---

## 一、总体结论（先看全局大盘）

可以把当前的 LanChat 想象成**“共用同一颗心脏（共享 C# 网络核心），但穿了两套不同外套（Windows 原生 WinForms 外套 vs Linux Electron 网页桌面外套）”**的双胞胎应用：
- **底层通信与数据心脏已高度共用**：Linux 的后台通信进程（`ELF`，即 Linux 下无需安装运行库就能直接跑的原生二进制程序）通过 [LanChat.Linux.csproj](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/LanChat.Linux.csproj) 直接源码链接了 Windows 的 `NetworkService*.cs`、`Models.cs`、`EmojiPack.cs`、`SessionManager.cs` 和 `ChatHistoryService.cs`。
- **为什么还会出现功能不同步？** 因为从 `v2.0.18` 到 `v2.0.26` 期间，Windows 端经历了多轮“外科手术式升级”（如 `LC-UI-INPUT-008/009/010` 微信式 214px 高输入区与内联表情渲染、`v2.0.21` 群聊文件多选 16 人发送等），同时 `LC-LINUX-002` 第 3 步按切片推进时，**切片 3（托盘、通知、更新维护）与节点管理弹窗尚未动工**，且八页设置中有 **11 个开关/下拉框在双端（或 Linux 端）只是“画在墙上的开关”（仅展示/假功能）**。

### 核心审计盘点（共 43 个审计子项）

1. **明确缺失（11 项，需补齐代码与 IPC）**：
   - **桌面生命周期（5 项）**：关闭窗口隐藏到托盘（当前直接退出）、托盘图标恢复主窗口、托盘右键菜单真正退出、托盘未读悬浮预览/闪烁、后台收到消息/文件时的系统桌面通知及点击通知跳转对应会话。
   - **节点管理 GUI（3 项）**：连接节点窗口（含分类芯片与节点卡片流）、手动输入 IPv4 直连握手 GUI 与 `DesktopService` IPC（目前仅 CLI `/add` 有）、创建新节点窗口（14 种形态 + 11 色调色盘）。
   - **输入区与聊天交互代差（2 项）**：
     - Windows 在 `v2.0.24~2.0.26` 已升级为 **214px 微信式高输入区 + 5 个展开的 48×48 工具栏按钮（含截图按钮）+ 输入框内联 PNG 表情基线对齐 + 2px 主题色光标 + 点击表情内部自动收拢光标到两侧 + Backspace/Delete 整枚原子删表情 + 输入框暗色右键菜单**；而 Linux 仍停留在 **64px 旧版单行胶囊 + 单个 `+` 号菜单 + 原生 `<textarea>` 显示 `[敲打]` 纯文本**。
     - Windows 表情选择器已升级为 **496×548 微信比例浮层（含“最近使用”12 项 + “所有表情” 8 列 48px 网格）**；Linux 仍是旧版 384px 单网格，无“最近使用”。
   - **文件传输与发布维护（1 项主流程 + 2 项子能力）**：
     - Windows 在 `v2.0.21` 已支持**群聊多选最多 16 名在线成员群发文件**（单在线好友免弹窗直发）；Linux 群聊选人弹窗 `<dialog id="recipient">` 仍只能**单选 1 人**且 1 人在线也弹窗。
     - Linux 客户端运行时**缺少根据 `amd64`/`arm64` 架构与安装/便携形态自动匹配 GitHub Release 资产及校验 `SHA256SUMS.json`** 的更新下载流。
2. **仅展示/假功能（11 项专项识别，界面有控件但无业务/IPC/持久化）**：
   - **双端均未接线的 10 项“样板开关/下拉”**：`语言`、`链接打开方式`、`托盘未读悬浮预览`（设置开关本身为空回调）、`后台网络驻留运行`（空回调）、`网络协议通道`（死胶囊）、`64KB 分块传输校验`（空回调）、`自动加载历史消息`（空回调）、`TUN/TAP 过滤`（空回调，底层写死常开）、`多网卡并发单播探测`（空回调，底层写死常开）、`快捷键配置`（静态只读徽章，且上面写的 `Ctrl+F` 搜索联系人和 `Ctrl+L` 清空当前聊天在双端主窗都**没绑键盘事件**）。
   - **Linux 端未接线的 1 项**：`检查更新 >`（Windows 已接通 `UpdateService`，Linux 点击仅弹“将在后续维护切片接入”占位框；另注：`发送反馈 >` 在双端均为纯本地感谢弹窗，无落盘或网络提交）。
3. **代码已有但未验收（17 项，严禁将宿主机静态/桩测试记为 Linux 实机通过）**：
   - 包含 Xorg/原生 Wayland/XWayland 三种会话表现、真实中文输入法（`fcitx5`/`ibus`）预编辑、100%/125%/150% DPI 叠图、2000 条长历史性能、最新 `v2.0.26` ELF 在 Linux 网络命名空间下的 `delete`/`history`/`clear-all-histories` 实跑、系统剪贴板图片/文件粘贴实机链路、双架构物理机 `.deb` 升级/回退、便携包、独立 CLI，以及物理 Windows 与物理 Linux 真实局域网（含切网/TUN）互通。
   - **还发现 3 处隐藏的代码级细微偏差**：
     1) **并发限流被前端串行卡住**：共享 C# 核心已支持同目标 2 路、全局 6 路并发，但 Linux 主进程 [main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs#L357-L362) `dispatchFiles` 用了 `for...of` + `await child.request('file')` 逐个串行等待，限制了多文件并发吞吐；
     2) **无聊天历史的离线节点刷新漏探（`LC-FR-007`）**：Windows `SessionPresenter._knownPeers` 会记住运行期上线过但未聊天的节点 IP 供刷新探测，而 Linux [DesktopService.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/DesktopService.cs#L340-L341) `KnownAddresses()` 只查有聊天记录的归档会话；
     3) **独立 CLI 未同步写入 JSONL 聊天历史**：Linux CLI（[Program.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/Program.cs#L70-L186)）虽与 GUI 共用 `LinuxProfile` 配置锁与接收目录，但收发消息时未设置 `ChatHistoryService.ProfileStorageDirectory` 落盘历史。
4. **已同步（4 项，代码及前期实机/QEMU 基线已证实等价）**：
   - 群聊/私聊基础文本收发与 `LANC` 封包、主窗会话侧栏关键字模糊搜索、同名文件并发接收独立临时文件与自动编号落盘、主窗及设置窗的 `4px/5px` 自绘暗灰浮动滑块（Zero White Scrollbar 契约）。

---

## 二、完整功能差异矩阵（按“明确缺失 → 仅展示/假功能 → 代码已有但未验收 → 已同步”排序）

> **术语小贴士**：
> - `IPC`（进程间通信）：就像餐厅里“前台服务员（Electron 界面）”和“后厨（C# 网络通信服务）”之间递菜单的传菜口。
> - `LANC`：LanChat 自定义的局域网通信暗号（4 字节魔数头 `LANC` + `0x01` 文字或 `0x02` 文件流）。
> - `JSONL`：一行存一条聊天记录的文本文件格式（`history_*.jsonl`），方便追加且不怕断电损坏。

### 第一部分：明确缺失（Linux 源码中没有对应功能或落后于 Windows 当前基线）

| 优先级 | 功能 | Windows 入口/源码 | Linux 当前入口/源码 | 当前状态 | 是否需要新增 IPC/API | 是否影响 LANC/JSONL 协议 | 依赖项 | 验收方式 |
| :---: | --- | --- | --- | :---: | --- | :---: | --- | --- |
| **P0** | **桌面关闭到托盘**（拦截窗口右上角关闭，隐藏窗口保持后台网络监听；无托盘时回退保留入口） | [MainForm.Tray.cs](file:///d:/TEmp/LanChat/src/MainForm.Tray.cs#L285-L303) (`MainForm_FormClosing`) | [main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs#L80-L84) (`window.on('close')` 直接调 `app.quit()`) | **明确缺失** | 需要（Electron 主进程新增 `Tray` 管理与托盘可用性/驻留设置联动） | 否 | Electron `Tray`、桌面 StatusNotifier / AppIndicator 支持探测 | 在 Xorg / Wayland / XWayland 实测点击关闭按钮：有托盘且开启驻留时隐藏窗口、ELF 保持 38888/38889 监听；无托盘时最小化或提示退出 |
| **P0** | **托盘恢复主窗口**（点击托盘图标、托盘菜单“显示主界面”或再次运行启动器唤回同一窗口与 ELF） | [MainForm.Tray.cs](file:///d:/TEmp/LanChat/src/MainForm.Tray.cs#L223-L283) (`RestoreFromTray`) | [main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs#L450)（仅有 `second-instance` 唤醒，无托盘恢复） | **明确缺失** | 需要（主进程托盘 `click` / 菜单恢复并通知渲染端清当前会话未读） | 否 | 阶段 1 托盘模块 | 隐藏窗口后分别通过托盘点击、托盘菜单、应用启动器二次启动恢复窗口，核对 ELF PID 不变、焦点恢复 |
| **P0** | **托盘菜单真正退出**（托盘右键菜单提供“显示主界面 / 系统设置... / 检查新版本... / 退出 LanChat”） | [MainForm.Tray.cs](file:///d:/TEmp/LanChat/src/MainForm.Tray.cs#L242-L260) | [index.html](file:///d:/TEmp/LanChat/src/linux/desktop/index.html#L36)（仅主窗左下角有“退出 LanChat”按钮，无托盘菜单） | **明确缺失** | 需要（主进程构建托盘 `Menu` 并绑定 `exiting = true; app.quit()`） | 否（复用退出 `BYE` 广播） | 阶段 1 托盘模块 | 从托盘菜单点击“退出 LanChat”，对端立即收到 `BYE` 下线，`ss -tulnp` 确认 38888/38889 端口释放且无残留进程 |
| **P0** | **系统通知**（窗口隐藏或非前台查看时，收到新消息或新文件弹出系统桌面通知） | [MainForm.Chat.cs](file:///d:/TEmp/LanChat/src/MainForm.Chat.cs#L110-L128)、[MainForm.Files.cs](file:///d:/TEmp/LanChat/src/MainForm.Files.cs#L467-L486) | 无（`src/linux/desktop/` 未调用 `Notification`） | **明确缺失** | 需要（主进程监听 `message` 事件并在 `!window.isVisible() \|\| !window.isFocused()` 时触发 Electron `Notification`） | 否 | Electron `Notification`、freedesktop 桌面通知服务 | 窗口最小化或隐藏至托盘时，由对端发来私聊、群聊和文件，验证系统通知横幅标题与摘要正确弹出 |
| **P0** | **通知点击进入对应会话**（点击系统通知横幅后恢复主窗口并自动切换到发起该消息的群聊/私聊会话） | [MainForm.Tray.cs](file:///d:/TEmp/LanChat/src/MainForm.Tray.cs#L527-L535) (`SwitchToSession`) | 无 | **明确缺失** | 需要（主进程向渲染层发送 `lanchat:event` `{ event: 'activate-chat', data: chatKey }`，渲染层调 `switchChat`） | 否 | 阶段 1 系统通知模块 | 当前停留在群聊并隐藏窗口，对端发来私聊触发通知，点击通知后主窗恢复前台且自动切到该好友私聊并清零未读 |
| **P1** | **未读托盘预览与闪烁提醒**（新消息时托盘图标闪烁/高亮、Tooltip 汇总未读条数、悬停/点击展示 320px 未读会话预览浮窗并支持点击跳转） | [MainForm.Tray.cs](file:///d:/TEmp/LanChat/src/MainForm.Tray.cs#L418-L658)、[GrokTrayPreviewPopup.cs](file:///d:/TEmp/LanChat/src/UI/Controls/GrokTrayPreviewPopup.cs#L39-L416) | 无（仅主窗侧栏有红点角标） | **明确缺失** | 需要（渲染层汇总 `unread-summary` 同步主进程更新托盘图标/Tooltip/菜单未读项，Xorg 下可选无边框预览子窗体） | 否 | 阶段 1 托盘模块、阶段 3 托盘预览开关持久化 | 后台收到多会话未读消息，核对托盘图标闪烁、Tooltip 文案 `LanChat: N条未读 (...)`、未读预览列表点击直达目标会话 |
| **P1** | **连接节点窗口与手动 IPv4 直连 GUI**（760×600 暗黑弹窗：节点搜索框、4 个分类过滤芯片、双列在线节点推荐卡片、右上角扫描刷新、底部手动 IPv4 直连握手） | [GrokConnectNodeForm.cs](file:///d:/TEmp/LanChat/src/UI/Controls/GrokConnectNodeForm.cs#L13-L430)、[MainForm.Chat.cs](file:///d:/TEmp/LanChat/src/MainForm.Chat.cs#L472-L490) | GUI 无此窗口；仅 CLI [Program.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/Program.cs#L296-L303) 有 `/add <IP>` | **明确缺失** | **需要**（`DesktopService.cs` 新增 `probe-ip` 方法调用 `_network.ProbeIp`；`preload.cjs`/`main.cjs` 暴露 `probeIp(ip)`） | 否（复用共享 `ProbeIp` 的 UDP `HELLO` + TCP `LANC\x01` 握手） | `NetworkService.ProbeIp` | 打开“连接节点”弹窗，测试搜索过滤、分类芯片切换、点击卡片进入私聊、输入跨网段/回环 IPv4 点击“连接握手”并在聊天流收到握手成功/失败系统消息 |
| **P2** | **新建节点窗口**（680×620 暗黑弹窗：96×96 动态吉祥物预览、名称/IP 输入框、14 种有机形态网格、11 色调色盘） | [GrokCreateNodeForm.cs](file:///d:/TEmp/LanChat/src/UI/Controls/GrokCreateNodeForm.cs#L12-L298)、[MainForm.Chat.cs](file:///d:/TEmp/LanChat/src/MainForm.Chat.cs#L492-L517) | 无 | **明确缺失** | 需要（复用 `probe-ip` IPC；若输入 IPv4 则发起直连握手，否则按 Windows 基线写入系统提示并导出 14 形态图标或 Canvas/SVG 绘制） | 否 | 阶段 2 连接节点 IPC | 打开“创建新节点”弹窗，切换 14 种形态与 11 色实时预览，输入 IP 或节点名提交，核对握手或系统提示与 Windows 一致 |
| **P1** | **微信式高输入区（214/188px）、展开式底部 5 联工具栏及输入框内联表情基线对齐、光标收拢与原子删表情** | [MainForm.Designer.cs](file:///d:/TEmp/LanChat/src/MainForm.Designer.cs#L432-L561)、[ChatInputBox.cs](file:///d:/TEmp/LanChat/src/UI/Controls/ChatInputBox.cs#L258-L843) (`LC-UI-INPUT-008/009/010`) | [index.html](file:///d:/TEmp/LanChat/src/linux/desktop/index.html#L57-L63)、[desktop.css](file:///d:/TEmp/LanChat/src/linux/desktop/desktop.css#L148-L155)（仍为 64px 旧胶囊 + 单个 `+` 号菜单 + `<textarea>` 显示 `[敲打]` 纯文本） | **明确缺失** | 截图快捷按钮需新增主进程 `trigger-screenshot` IPC（调用系统截图或提示剪贴板粘贴）；内联表情编辑在渲染层实现 | 否（发送时仍还原为 `[表情名]` 纯文本） | `EmojiPack` 已接通的数据源 | 核对输入区 214/188px 布局、底部展开工具按钮、输入 `[敲打]` 渲染为两倍非透明高内联 PNG 且底边对齐正文基线、点击表情内部光标收拢至两侧、Backspace/Delete 整枚删除、右键弹出暗色粘贴/粘贴附件菜单 |
| **P1** | **微信比例表情选择器（496×548、最近使用 12 项 + 所有表情 8 列 48px 网格 + 强调色悬停框）** | [ChatEmojiPicker.cs](file:///d:/TEmp/LanChat/src/UI/Controls/ChatEmojiPicker.cs#L16-L343) (`LC-CC-007` & `LC-UI-INPUT-009`) | [overlay.js](file:///d:/TEmp/LanChat/src/linux/desktop/overlay.js#L250-L273)、[desktop.css](file:///d:/TEmp/LanChat/src/linux/desktop/desktop.css#L168-L173)（384px 旧版单网格，无“最近使用”） | **明确缺失** | 否（渲染层维护 `recentOrder` 最多 12 项与双网格 DOM，复用 `scrollbar.js`） | 否 | `emoji-pack` IPC | 打开表情面板，核对 496×548 尺寸、“最近使用”与“所有表情”分组、8 列 48px 单元格、1.5px 强调色悬停/键盘焦点框、方向键导航 |
| **P1** | **群聊文件发送多选接收人（最多 16 人）与单在线好友免弹窗直发** | [MainForm.Files.cs](file:///d:/TEmp/LanChat/src/MainForm.Files.cs#L296-L430) (`PickFileRecipients(allowMultiple: true)`, `MaxFileRecipients = 16`) | [renderer.js](file:///d:/TEmp/LanChat/src/linux/desktop/renderer.js#L310-L334) (`pickRecipient` 仅支持点击单个按钮单选 1 人，且 1 人在线也弹窗) | **明确缺失** | 需要（`main.cjs` 的 `select-files` / `send-paste` / `confirm-drop` 支持传入 `chatIds[]` 数组（1~16 人），并对托管截图按接收人数引用计数清理） | 否 | `FileTransferController` 引用计数语义对齐 | 群聊下当仅 1 名好友在线时发文件直接发送不弹窗；多名好友在线时弹出可多选列表（上限 16 人），勾选 2 人发送文件/粘贴截图，核对两个私聊会话均出现进度气泡且截图在两路均完成后才清理 |
| **P1** | **Linux 运行时 `amd64`/`arm64` 更新资产自动选择与 SHA-256 校验下载** | [UpdateService.cs](file:///d:/TEmp/LanChat/src/Services/UpdateService.cs#L40-L197)、[GrokUpdateForm.cs](file:///d:/TEmp/LanChat/src/UI/Controls/GrokUpdateForm.cs#L108-L233) | 无（仅构建脚本有双架构文件名，客户端无下载与架构匹配代码） | **明确缺失** | 需要（主进程新增 `check-update`、`download-update`、`apply-update` IPC） | 否 | GitHub Releases `SHA256SUMS.json` 与双架构资产 | 在 `x64` 与 `arm64` 环境下触发更新检查，验证自动选中对应架构的 `.deb`（安装版）或 `_portable.tar.gz`（便携版），流式下载进度条与 SHA-256 校验通过 |

---

### 第二部分：仅展示/假功能（重点识别的 11 项“只有界面、没有真实功能”条目）

| 优先级 | 功能（11 项专项识别） | Windows 入口/源码 | Linux 当前入口/源码 | 当前状态 | 是否需要新增 IPC/API | 是否影响 LANC/JSONL 协议 | 依赖项 | 验收方式 |
| :---: | --- | --- | --- | :---: | --- | :---: | --- | --- |
| **P0** | **1. 检查更新**（通用页 & 关于页 `检查更新 >`） | [UpdateService.cs](file:///d:/TEmp/LanChat/src/Services/UpdateService.cs#L40-L171)（Windows **已接通** GitHub API + 302 降级 + MSI 下载安装） | [settings.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings.js#L420-L427) (`showUpdateNotice` 仅弹“将在后续维护切片接入”静态提示框) | **仅展示/假功能**（Linux 端假功能，Windows 为真功能） | **需要**（主进程实现 GitHub Releases API + 302 降级探测，返回最新版本、日志与本机架构资产直链，弹窗支持下载与打开安装） | 否 | 阶段 4 更新模块 | 点击通用页、关于页或托盘菜单“检查更新”，最新版时提示“当前已是最新版本”，有新版时弹出 520×440 更新窗展示日志与下载进度 |
| **P0** | **2. 后台网络驻留运行**（通用页 → 权限与偏好） | [GeneralSettingsPanel.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SettingsPanels/GeneralSettingsPanel.cs#L186) (`new ModernToggleSwitch(true, (val) => { })` 空回调) | [settings-pages.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-pages.js#L234-L236) (`createToggleSwitch(display.keepAlive, () => {})` 空回调) | **仅展示/假功能**（双端均为空回调，未持久化） | **需要**（`LinuxProfile` / `DesktopService` / `save-settings` 新增 `keepAliveInTray` 布尔字段并持久化至 `settings.json`，控制主窗 `close` 事件是隐藏到托盘还是直接退出） | 否 | 阶段 1 托盘生命周期 | 开启开关时关闭主窗口隐藏到托盘；关闭开关时关闭主窗口直接退出进程并释放端口；重启后开关状态保持 |
| **P1** | **3. 托盘未读悬浮预览**（通用页 → 权限与偏好） | [GeneralSettingsPanel.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SettingsPanels/GeneralSettingsPanel.cs#L170) (`new ModernToggleSwitch(true, (val) => { })` 空回调；主窗托盘预览常开不受其控制) | [settings-pages.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-pages.js#L230-L232) (`createToggleSwitch(display.trayPreview, () => {})` 空回调) | **仅展示/假功能**（双端开关均为空回调，未持久化） | **需要**（`LinuxProfile` / `DesktopService` / `save-settings` 新增 `trayPreview` 布尔字段持久化，主进程托盘据此决定是否启用未读悬浮预览卡片） | 否 | 阶段 1 未读托盘预览 | 关闭该开关后后台收到未读消息不再弹出托盘悬浮预览卡片，开启后恢复弹出，重启后配置保持 |
| **P1** | **4. 快捷键配置**（快捷键页 4 项徽章：`Enter`、`Ctrl+Enter / Shift+Enter`、`Ctrl+F`、`Ctrl+L`） | [ShortcutsSettingsPanel.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SettingsPanels/ShortcutsSettingsPanel.cs#L44-L64)（纯 GDI+ 静态绘制，且主窗未绑定 `Ctrl+F` / `Ctrl+L`） | [settings-model.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-model.js#L44-L49)、[settings-pages.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-pages.js#L354-L364)（纯静态 `<span>`，且主窗未绑 `Ctrl+F` / `Ctrl+L`，也不可配置发送键） | **仅展示/假功能** | **需要**（1. 在主窗 `renderer.js` 真实绑定 `Ctrl+F` 聚焦搜索框、`Ctrl+L` 触发清空当前聊天；2. 在 `settings.json` 新增 `sendShortcutMode` 支持切换 `Enter 发送` / `Ctrl+Enter 发送` 并实时同步主窗与快捷键页） | 否 | `LinuxProfile`、`renderer.js` | 在主窗口按 `Ctrl+F` 焦点立即跳入左侧搜索框，按 `Ctrl+L` 清空当前会话，在快捷键页切换发送键模式后主窗按新快捷键发送/换行且重启保持 |
| **P1** | **5. 自动加载历史消息**（聊天记录页） | [ChatHistorySettingsPanel.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SettingsPanels/ChatHistorySettingsPanel.cs#L87) (`new ModernToggleSwitch(true, null)` 空回调) | [settings-pages.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-pages.js#L398-L400) (`createToggleSwitch(display.autoLoadHistory, () => {})` 空回调) | **仅展示/假功能** | **需要**（`LinuxProfile` / `DesktopService` 新增 `autoLoadHistory` 持久化；为 `false` 时启动跳过 `_sessions.LoadPersistedHistories()`，不自动载入往期磁盘历史到会话视图） | 否（磁盘 JSONL 格式与原文件不破坏） | `LinuxProfile`、`DesktopService` | 关闭“自动加载历史消息”并重启应用，验证启动后不自动加载旧聊天记录；重新开启并重启后恢复自动加载往期历史 |
| **P1** | **6. TUN/TAP 过滤**（设备连接页 `TUN / TAP 虚拟网卡穿透过滤`） | [NetworkDeviceSettingsPanel.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SettingsPanels/NetworkDeviceSettingsPanel.cs#L67) (`new ModernToggleSwitch(true, null)` 空回调，底层常开) | [settings-pages.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-pages.js#L419-L421) (`createToggleSwitch(display.tunFilter, () => {})` 空回调) | **仅展示/假功能** | **需要**（`LinuxProfile` / `DesktopService` 新增 `tunFilter` 持久化并传给共享 `NetworkService.EnableTunFilter`；切换时立即触发 `RefreshPeers` 重新绑定网卡） | 否 | `NetworkService.Interface.cs` | 开启时过滤 `tun/tap/vpn` 虚拟网卡；关闭时允许虚拟网卡参与绑定与发现（便于 WireGuard/ZeroTier 组网），重启保持 |
| **P1** | **7. 多网卡并发单播探测**（设备连接页） | [NetworkDeviceSettingsPanel.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SettingsPanels/NetworkDeviceSettingsPanel.cs#L71) (`new ModernToggleSwitch(true, null)` 空回调，底层常开) | [settings-pages.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-pages.js#L422-L424) (`createToggleSwitch(display.unicastProbe, () => {})` 空回调) | **仅展示/假功能** | **需要**（`LinuxProfile` / `DesktopService` 新增 `unicastProbe` 持久化并传给共享 `NetworkService.EnableSubnetSweep`；关闭时 `RefreshPeers` 仅发 UDP 广播与已知地址探测，不扫 1~254 子网） | 否 | `NetworkService.Discovery.cs` | 关闭该开关后点击刷新，抓包/日志验证仅发送 UDP 广播与已知节点探测而不触发 254 IP 单播扫描；开启后恢复全子网并发单播探测 |
| **P2** | **8. 64KB 分块传输校验**（文件传输页） | [FileTransferSettingsPanel.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SettingsPanels/FileTransferSettingsPanel.cs#L101) (`new ModernToggleSwitch(true, null)` 空回调) | [settings-pages.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-pages.js#L380-L382) (`createToggleSwitch(display.chunkCheck, () => {})` 空回调) | **仅展示/假功能** | **需要**（`LinuxProfile` / `DesktopService` 新增 `chunkCheck` 持久化并传给 `NetworkService.VerifyChunkFlush`：开启时接收落盘执行严格长度核验与 `fs.Flush(flushToDisk: true)` 硬件刷盘校验） | 否（严禁改动 LANC Type 2 分块报文字节） | `NetworkService.Transfer.cs` | 切换开关验证 `settings.json` 持久化生效，开启状态下传输文件落盘前执行严格字节数与磁盘刷盘校验 |
| **P2** | **9. 语言**（通用页：跟随系统简体中文 / English (US) / 繁体中文） | [GeneralSettingsPanel.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SettingsPanels/GeneralSettingsPanel.cs#L250-L258)（仅修改 `anchor.Text`，无持久化无翻译） | [settings-pages.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-pages.js#L196-L202)（仅修改内存 `display.languagePill`，无持久化无翻译） | **仅展示/假功能** | **需要**（`LinuxProfile` / `DesktopService` 新增 `language` 字段持久化，前端新增轻量级三语文案表，切换后立即刷新主窗与设置窗文案并重启保持） | 否 | `LinuxProfile`、`appearance.js`/`settings-model.js` | 在设置页切换简中/英文/繁中，验证主窗与设置窗导航及核心文案即时切换，重启应用后保持所选语言 |
| **P2** | **10. 链接打开方式**（通用页：智能打开(默认浏览器) / 内置极速预览） | [GeneralSettingsPanel.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SettingsPanels/GeneralSettingsPanel.cs#L260-L267)（仅修改 `anchor.Text`，气泡内链接未识别） | [settings-pages.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-pages.js#L203-L209)（仅改内存 `display.linkPill`，气泡内链接未识别） | **仅展示/假功能** | **需要**（`LinuxProfile` 新增 `linkOpenMode` 持久化；`conversation-view.js` 识别 `https?://` 链接，主进程新增 `open-external-url` 按模式调 `shell.openExternal` 或应用内预览卡片） | 否 | `conversation-view.js`、`main.cjs` | 聊天气泡收到 `https://...` 链接时可点击：选“智能打开”时调系统默认浏览器打开，选“内置极速预览”时弹出应用内安全链接信息预览框 |
| **P2** | **11. 网络协议通道**（通用页 `"直接连接 (LAN)"` & 设备连接页 `"● 局域网在线"` 胶囊） | [GeneralSettingsPanel.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SettingsPanels/GeneralSettingsPanel.cs#L191)、[NetworkDeviceSettingsPanel.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SettingsPanels/NetworkDeviceSettingsPanel.cs#L63)（点击无菜单弹出） | [settings-pages.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-pages.js#L237-L239)、[settings-pages.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-pages.js#L417)（`getItems` 为 `null`，点击无反应） | **仅展示/假功能** | **需要**（`DesktopService` 的 `state` 返回已绑定物理/虚拟网卡明细列表 `interfaces[]`，点击下拉胶囊展示真实网卡绑定列表与“立即重绑刷新物理通道”操作） | 否 | `NetworkService.Interface.cs` | 点击“网络协议通道”或“局域网在线”下拉胶囊，弹出当前生效网卡 IP/掩码列表并可点击触发网卡重绑与网段刷新 |

---

### 第三部分：代码已有但未验收（源码与宿主机测试已存在，但尚无最新 Linux 实机、ELF、桌面环境或真实 LAN 证据）

> **严正声明**：以下 17 项虽然已有 C# / Electron 源码并通过了 Windows 宿主机上的 Node DOM 桩测试、Edge Headless 静态测量或早期 QEMU 实验轮次，但**绝不能等同于 Linux 实机通过**，必须在阶段 5 中逐项补齐真实 Linux 机器、最新 ELF 与真实双机 LAN 证据。

| 优先级 | 功能 | Windows 入口/源码 | Linux 当前入口/源码 | 当前状态 | 是否需要新增 IPC/API | 是否影响 LANC/JSONL 协议 | 依赖项 | 验收方式 |
| :---: | --- | --- | --- | :---: | --- | :---: | --- | --- |
| **P0** | **Wayland / Xorg / XWayland 等效窗口与桌面行为** | [MainForm.cs](file:///d:/TEmp/LanChat/src/MainForm.cs#L297-L405) | [lanchat-launcher.sh](file:///d:/TEmp/LanChat/packaging/lanchat-launcher.sh#L30-L35)、[main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs#L62-L184) | **代码已有但未验收** | 配合阶段 1 补齐托盘探测回退 | 否 | Ubuntu 22.04/24.04 GNOME Xorg & Wayland 实机 | 在 Xorg、原生 Wayland（`--ozone-platform=wayland`）、XWayland（`--ozone-platform=x11`）下分别实测窗口缩放、贴靠、最大化/还原、设置模态窗居中及托盘/通知表现并截图取证 |
| **P0** | **Enter 发送、Shift+Enter / Ctrl+Enter 换行** | [MainForm.Chat.cs](file:///d:/TEmp/LanChat/src/MainForm.Chat.cs#L383-L402) | [presentation.js](file:///d:/TEmp/LanChat/src/linux/desktop/presentation.js#L181-L189)、[renderer.js](file:///d:/TEmp/LanChat/src/linux/desktop/renderer.js#L492-L507) | **代码已有但未验收** | 配合阶段 3 支持发送快捷键模式配置 | 否 | Linux 实机键盘与输入法环境 | 在 Linux Electron 实机验证普通 `Enter` 发送、`Shift+Enter` 与 `Ctrl+Enter` 换行、多行自动增高与浮动滑块出现 |
| **P0** | **真实中文 IME 预编辑（fcitx5 / ibus）** | [ChatInputBox.cs](file:///d:/TEmp/LanChat/src/UI/Controls/ChatInputBox.cs#L738-L782) | [presentation.js](file:///d:/TEmp/LanChat/src/linux/desktop/presentation.js#L181-L189) (`!event.isComposing && event.keyCode !== 229`) | **代码已有但未验收** | 否（阶段 2 升级内联表情输入框时须保持 `compositionstart`/`compositionend` 期间不打断选区） | 否 | Linux 实机 `fcitx5` 与 `ibus` 中文输入法 | 在 Xorg 与原生 Wayland 下分别用 `fcitx5` 和 `ibus` 实测聊天框、昵称修改、反馈框的拼音预编辑、空格/回车选词不误发、换行与退格 |
| **P0** | **文件选择、多选和拖放发送** | [MainForm.Files.cs](file:///d:/TEmp/LanChat/src/MainForm.Files.cs#L214-L270) | [main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs#L560-L600)、[preload.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/preload.cjs#L69-L96) | **代码已有但未验收** | 配合阶段 2 扩展群聊多选接收人 | 否 | Linux 桌面 GTK 文件选择器与文件管理器（Nautilus 等） | 在 Linux 实机测试点击发送文件多选（1~32 个文件、超 32 个报错）及从桌面文件管理器直接拖拽单/多文件到私聊与群聊窗口发送 |
| **P0** | **失败、取消、断线和退出清理（含托管截图生命周期）** | [NetworkService.Transfer.cs](file:///d:/TEmp/LanChat/src/NetworkService.Transfer.cs#L228-L242)、[FileTransferController.cs](file:///d:/TEmp/LanChat/src/Controllers/FileTransferController.cs#L218-L267) | [main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs#L342-L386)、[DesktopService.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/DesktopService.cs#L376-L424) | **代码已有但未验收** | 否 | 否 | 最新 ELF 与 Electron 实机 | 在 Linux 实机与网络命名空间实测：对方拒收、传输中途断网/强退 ELF、发送粘贴截图时退出应用，验证 `.downloading` 临时文件清理、历史已引用截图保留、未引用截图删除、用户原文件不误删 |
| **P0** | **deb 安装、升级、回退（双架构物理实机）** | WiX MSI 安装包 | [build-electron.py](file:///d:/TEmp/LanChat/packaging/build-electron.py#L118-L152)、[BuildLinuxDeb.py](file:///d:/TEmp/LanChat/Tools/Scripts/BuildLinuxDeb.py) | **代码已有但未验收** | 否 | 否 | 物理 `x86_64` 与物理 `ARM64` Linux 主机、旧公开包/候选包样本 | 在物理 `x86_64` 与物理 `ARM64` 实机执行旧包 → 新 `.deb` 升级、`apt install --allow-downgrades` 回退及普通卸载重装，核对 `dpkg -l`、ELF 机器码（62/183）、用户历史与设置保留、旧 Python/Tk GUI 恢复 |
| **P0** | **Windows 与 Linux 真实 LAN 互通** | `src/NetworkService*.cs` | `src/linux/LanChat.Linux/`（共享同一 C# 源码） | **代码已有但未验收** | 否 | 否 | 物理 Windows 主机 + 物理 `x86_64` / `ARM64` Linux 主机 + 真实局域网路由器/交换机 | 真实双机/三机局域网下实测自动发现、改昵称广播、双向群聊/私聊、双向大文件/0字节/同名并发文件 SHA-256 校验、含 TUN 代理/多物理网卡/切网恢复及退出端口释放 |
| **P1** | **节点识别码和设备信息查看/复制** | [ProfileSettingsPanel.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SettingsPanels/ProfileSettingsPanel.cs#L69-L111)、[AboutSettingsPanel.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SettingsPanels/AboutSettingsPanel.cs#L69-L83) | [settings-pages.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-pages.js#L285-L295)、[main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs#L490-L496) | **代码已有但未验收** | 否 | 否 | Linux Electron 实机 | 在 Linux 实机打开设置 → 个人主页/设备连接/关于与更新，核对 Node UUID、主机名、内核架构、IP:端口展示，点击“复制 ID >”和“复制诊断信息 >”并粘贴核验 |
| **P1** | **节点重启、改名、离线归档和重新上线（含 `LC-FR-007` 补齐）** | [NetworkService.Discovery.cs](file:///d:/TEmp/LanChat/src/NetworkService.Discovery.cs#L193-L394)、[SessionPresenter.cs](file:///d:/TEmp/LanChat/src/Controllers/SessionPresenter.cs#L201-L214) | [DesktopService.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/DesktopService.cs#L340-L348)、[renderer.js](file:///d:/TEmp/LanChat/src/linux/desktop/renderer.js#L71-L82) | **代码已有但未验收**（需顺手在 `DesktopService.cs` 补齐运行期无历史已发现 IP 缓存 `_knownAddresses`） | 否（内部补齐 `DesktopService._knownAddresses`） | 否 | 最新 ELF 与真实双机 LAN | 实测对端无 `BYE` 强退重启（同 IP 换 `LocalId`）、在线改昵称、离线掉入“离线”分组（含未聊过天节点的刷新重探 `LC-FR-007`）、重新上线后会话合并与草稿保留 |
| **P1** | **会话分组、未读、搜索和键盘导航** | [SessionPresenter.cs](file:///d:/TEmp/LanChat/src/Controllers/SessionPresenter.cs#L137-L199)、[MainForm.Chat.cs](file:///d:/TEmp/LanChat/src/MainForm.Chat.cs#L174-L368) | [session-sidebar.js](file:///d:/TEmp/LanChat/src/linux/desktop/session-sidebar.js#L22-L140)、[desktop.css](file:///d:/TEmp/LanChat/src/linux/desktop/desktop.css#L75-L90) | **代码已有但未验收** | 否 | 否 | Linux Electron 实机 | 在 Linux 实机验证 420px 侧栏与 `<850px` 窄窗自适应、“在线/离线”6px 圆点与 8px 间距、未读红点 `99+`、上下方向键导航与焦点恢复 |
| **P1** | **草稿隔离（文本草稿与剪贴板附件草稿按会话隔离）** | [MainForm.Files.cs](file:///d:/TEmp/LanChat/src/MainForm.Files.cs#L16) | [renderer.js](file:///d:/TEmp/LanChat/src/linux/desktop/renderer.js#L22-L23) (`drafts` & `attachments`) | **代码已有但未验收** | 否 | 否 | Linux Electron 实机 | 在群聊和好友 A 私聊分别输入未发送文字并粘贴图片附件，来回切换会话，确认文字草稿与附件缩略图严格隔离不串台 |
| **P1** | **内置 109 表情包回显、Unicode 组合表情和缺图回退** | [EmojiPack.cs](file:///d:/TEmp/LanChat/src/Services/EmojiPack.cs)、[ChatTextLayout.cs](file:///d:/TEmp/LanChat/src/UI/Renderers/ChatTextLayout.cs) | [DesktopService.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/DesktopService.cs#L172-L175)、[presentation.js](file:///d:/TEmp/LanChat/src/linux/desktop/presentation.js#L33-L89) | **代码已有但未验收** | 否 | 否 | 最新 ELF 与 Linux Electron 实机 | 实机验证双端收发 109 个 `[微信表情]`、家庭/肤色/国旗 Unicode 组合表情，在气泡与侧栏摘要正确显图且不截断半个令牌，未知 `[自定义]` 或坏图回退显示原文字 |
| **P1** | **剪贴板图片/文件附件粘贴（Ctrl+V）** | [ChatInputBox.cs](file:///d:/TEmp/LanChat/src/UI/Controls/ChatInputBox.cs#L845-L962)、[MainForm.Files.cs](file:///d:/TEmp/LanChat/src/MainForm.Files.cs#L85-L171) | [preload.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/preload.cjs#L31-L66)、[main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs#L221-L318) | **代码已有但未验收** | 配合阶段 2 增加输入框暗色右键“粘贴/粘贴附件”菜单 | 否 | Linux Xorg/Wayland 截图工具与文件管理器 | 在 Linux 实机使用系统截图工具截图后 `Ctrl+V`、在文件管理器复制文件后 `Ctrl+V`、复制普通路径文本后 `Ctrl+V`，验证缩略图预览、移除与发送 |
| **P1** | **逐文件进度、速率与并发限流（需解除 `main.cjs` 串行 `await`）** | [FileTransferController.cs](file:///d:/TEmp/LanChat/src/Controllers/FileTransferController.cs#L104-L137)、[NetworkService.Transfer.cs](file:///d:/TEmp/LanChat/src/NetworkService.Transfer.cs#L15-L16) | [DesktopService.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/DesktopService.cs#L462-L495)、[main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs#L357-L362)、[conversation-view.js](file:///d:/TEmp/LanChat/src/linux/desktop/conversation-view.js#L10-L16) | **代码已有但未验收**（需将 `main.cjs` `dispatchFiles` 改为并发派发，交由 C# 6/2 队列限流） | 否（调整 `main.cjs` `dispatchFiles` 内部调度） | 否 | 最新 ELF 与真实双机 LAN | 一次选择 10 个文件或向多目标群发，验证气泡各自独立显示百分比与 `KB/s`/`MB/s` 速率，底层按单目标 2 路、全局 6 路并发流转且无重复完成气泡 |
| **P1** | **打开文件和打开所在目录 + 自动接收与接收/历史目录迁移 + 外观持久化** | [FileTransferController.cs](file:///d:/TEmp/LanChat/src/Controllers/FileTransferController.cs#L363-L408)、`SettingsPanels/*.cs` | [main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs#L409-L558)、[LinuxProfile.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/LinuxProfile.cs#L93-L241)、[settings.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings.js#L229-L384) | **代码已有但未验收** | 否 | 否 | 最新 ELF（跑通 `tests/linux_desktop_regression.py` 全套 5 用例）与 Linux 实机 | 在 Linux 网络命名空间对最新 ELF 跑通 `settings`/`history`/`transfer`/`crash`/`delete`；在实机点击文件卡片“打开文件/打开目录”、在设置窗切换 3 主题/11 色/字体字号、迁移接收与历史目录、只读备用确认、清空全部历史 |
| **P1** | **便携包与独立 CLI（含 CLI 历史落盘一致性加固）** | `LanChat.exe`（便携同级 `settings.json`） | [build-electron.py](file:///d:/TEmp/LanChat/packaging/build-electron.py#L207-L212)、[lanchat-launcher.sh](file:///d:/TEmp/LanChat/packaging/lanchat-launcher.sh#L1-L37)、[Program.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/Program.cs#L37-L575) | **代码已有但未验收**（需在 `Program.cs` 绑定 `ChatHistoryService.ProfileStorageDirectory` 使 CLI 与 GUI 历史落盘一致） | 否 | 否 | 双架构 Linux 实机 | 在 `x86_64` 与 `ARM64` 实机分别验证 `portable.tar.gz` 解压即用、同级数据优先、`lanchat --cli` 与独立 `lanchat-cli` 无头运行 `/list` `/chat` `/send` `/add` `/nick` `/quit` 及 GUI/CLI 配置锁互斥与历史互通 |
| **P1** | **旧版 Python/Linux 客户端与 Windows 旧版兼容** | `NetworkService*.cs`、`SessionManager.cs` | [LinuxProfile.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/LinuxProfile.cs#L344-L392) | **代码已有但未验收** | 否 | 否 | 旧公开版 `v2.0.15`/`v2.0.16` Python 客户端及旧 ELF 样本 | 在实机演练旧 Python 版与新版 Linux/Windows 互发文字、特殊转义字符、文件传输，以及新版修改昵称后回退到旧 Python GUI 读取配置与 `history_*.jsonl` |

---

### 第四部分：已同步（Linux 已有等价实现且基础验证已覆盖）

| 优先级 | 功能 | Windows 入口/源码 | Linux 当前入口/源码 | 当前状态 | 是否需要新增 IPC/API | 是否影响 LANC/JSONL 协议 | 依赖项 | 验收方式 |
| :---: | --- | --- | --- | :---: | --- | :---: | --- | --- |
| **P0** | **群聊和私聊基础消息收发、3 分钟时间条与气泡右键复制/全选** | [ChatPresenter.cs](file:///d:/TEmp/LanChat/src/Controllers/ChatPresenter.cs#L93-L246)、[WeChatChatView.cs](file:///d:/TEmp/LanChat/src/UI/Controls/WeChatChatView.cs) | [DesktopService.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/DesktopService.cs#L183-L188)、[conversation-view.js](file:///d:/TEmp/LanChat/src/linux/desktop/conversation-view.js#L18-L197) | **已同步** | 否 | 否 | 共享 `NetworkService` | 宿主机回归与前期实机已通过，阶段 5 纳入双机 LAN 终验 |
| **P0** | **同名文件并发接收保护（独立 `.downloading` 临时文件 + 串行自动编号 `文件名 (1).ext`）** | [NetworkService.cs](file:///d:/TEmp/LanChat/src/NetworkService.cs#L202-L224)、[NetworkService.Transfer.cs](file:///d:/TEmp/LanChat/src/NetworkService.Transfer.cs#L177-L227) | `LanChat.Linux.csproj` 共享同一源码 | **已同步** | 否 | 否 | 共享 `NetworkService` | Windows 回环与 Ubuntu 24.04 两架构 QEMU 已通过，阶段 5 纳入真实双机复验 |
| **P1** | **主窗侧栏会话搜索（按昵称 / IPv4 / 主机名模糊过滤、`Esc` 与 `×` 清空）** | [MainForm.Chat.cs](file:///d:/TEmp/LanChat/src/MainForm.Chat.cs#L47-L68)、[SessionPresenter.cs](file:///d:/TEmp/LanChat/src/Controllers/SessionPresenter.cs#L238-L276) | [session-sidebar.js](file:///d:/TEmp/LanChat/src/linux/desktop/session-sidebar.js#L107-L127)、[presentation.js](file:///d:/TEmp/LanChat/src/linux/desktop/presentation.js#L148-L149) | **已同步** | 否（阶段 3 补齐 `Ctrl+F` 快捷键聚焦） | 否 | 无 | Node 模拟回归已通过，阶段 5 实机复核 |
| **P1** | **全界面自绘暗灰浮动滑块（Zero White Scrollbar：静止 4px `#34343C`、悬停/拖拽 5px `#646470`、无溢出隐藏）** | [SessionListBox.cs](file:///d:/TEmp/LanChat/src/UI/Controls/SessionListBox.cs)、[WeChatChatView.cs](file:///d:/TEmp/LanChat/src/UI/Controls/WeChatChatView.cs) | [scrollbar.js](file:///d:/TEmp/LanChat/src/linux/desktop/scrollbar.js#L1-L87)、[desktop.css](file:///d:/TEmp/LanChat/src/linux/desktop/desktop.css#L79-L81) | **已同步** | 否 | 否 | 无 | Node 模拟与 Chromium 计算样式测量已通过，阶段 5 实机复核鼠标拖拽 |

---

## 三、分阶段实施计划（阶段 0 ～ 阶段 5）

> **代码行数与解耦前置预警（严格遵守 `.claude/Rules.md` 第 10 条）**：
> 当前手写文件中，[tests/linux_desktop_ui_regression.js](file:///d:/TEmp/LanChat/tests/linux_desktop_ui_regression.js) 已达 **924 行**（逼近 1000 行预警线，且该文件第 148 行硬性断言所有被检文件 `< 1000` 行！），[settings.css](file:///d:/TEmp/LanChat/src/linux/desktop/settings.css) **742 行**、[settings.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings.js) **704 行**、[renderer.js](file:///d:/TEmp/LanChat/src/linux/desktop/renderer.js) **689 行**、[main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs) **624 行**、[Program.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/Program.cs) **576 行**、[DesktopService.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/DesktopService.cs) **502 行**。
> 因此，新增业务**严禁盲目塞入上述大文件**，必须按职责拆分为高内聚的独立模块（如 `tray-manager.cjs`、`update-service.cjs`、`node-dialogs.js`、`composer-box.js`），并将新增回归测试拆入独立测试脚本，确保全工程手写源码 100% 保持在 **850 行以下**（远低于 1000 行预警线与 1500 行硬上限）。

---

### 阶段 0：基线冻结和功能矩阵

1. **修改文件**：
   - `PlanDocs/LC-LINUX-002/BASELINE.md`（追加 `v2.0.26` Windows 最新输入区、群聊多选、设置项接线状态与 SHA-256 基线对照表）
   - `PlanDocs/LC-LINUX-002/STATUS.md`（登记阶段 0~5 同步实施路线）
   - `.claude/Plan.md` 与 `.claude/SessionBrief.md`（锁定同步实施批次）
2. **代码边界**：
   - 纯文档与基线校验，不修改任何 `.cs` / `.js` / `.css` 生产代码。
   - 固化 `v2.0.26` 现有产物（Windows `LanChat.exe`/`LanChat.msi`、Linux `amd64`/`arm64` `.deb`/`portable.tar.gz`/`lanchat-cli`）与旧版升级样本（`v2.0.16` 公开版、`LC-LINUX-001` 候选版、`handoff-experiment.5`）的 SHA-256 清单。
3. **IPC/API 设计**：
   - 冻结以下阶段 1~4 拟新增的 IPC 命名契约与 `DesktopService` JSONL 协议版本（保持 `Version = 2` 向后兼容扩展字段，不破坏现有请求/响应结构）。
4. **测试用例**：
   - 运行现有基线回归确认零破损：
     - `cmd /c "Tools\Scripts\Compile.bat < NUL"`
     - `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js`
     - `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`
     - `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`
5. **回滚方案**：
   - `git checkout -- PlanDocs/ .claude/` 即可一键还原文档更改。
6. **完成标准**：
   - 43 项差异矩阵、SHA-256 样本清单与现有 4 套宿主机自动化回归 100% 绿灯通过。

---

### 阶段 1：桌面生命周期、托盘、通知

1. **修改文件**：
   - **新增** `src/linux/desktop/tray-manager.cjs`（约 260 行）：独立封装 Electron `Tray` 创建、托盘可用性探测、450ms 未读图标闪烁定时器、Tooltip 未读汇总、托盘右键菜单（“显示主界面”“系统设置...”“检查新版本...”“退出 LanChat”）、Xorg 未读悬浮预览小窗（320px 宽）以及系统 `Notification` 派发与点击回调。
   - **修改** [src/linux/desktop/main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs)（当前 624 行 → 预计 650 行）：接入 `tray-manager.cjs`；将 `window.on('close')` 改为检查 `keepAliveInTray && trayManager.canHideToTray()`（满足时 `event.preventDefault(); window.hide();` 并首次发“已最小化到托盘”通知；否则正常退出或最小化）；转发 `activate-chat`、`open-update-check` 事件；处理渲染层上报的 `unread-state` IPC。
   - **修改** [src/linux/desktop/preload.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/preload.cjs)（当前 96 行 → 预计 110 行）：新增 `syncUnreadState: summary => ipcRenderer.invoke('lanchat:unread-state', summary)`。
   - **修改** [src/linux/desktop/renderer.js](file:///d:/TEmp/LanChat/src/linux/desktop/renderer.js)（当前 689 行 → 预计 725 行）：在未读数变化（收到新消息/文件、切换会话、清空历史）时调用 `api.syncUnreadState(...)` 上报未读会话列表（含 `key`、`title`、`count`、`snippet`、`time`）；在 `handleEvent` 中响应 `activate-chat`（调用 `switchChat(event.data, true)` 切换会话并聚焦输入框）和 `window-restored`（清除当前活跃会话未读）。
   - **修改** [packaging/ExportLinuxDesktopAssets.cs](file:///d:/TEmp/LanChat/packaging/ExportLinuxDesktopAssets.cs) 与 [packaging/build-electron.py](file:///d:/TEmp/LanChat/packaging/build-electron.py)：导出 `tray.png`（常态托盘图标）与 `tray-unread.png` / `tray-blank.png`（未读闪烁图标）。
   - **新增** `tests/linux_desktop_lifecycle_regression.js`（约 220 行）：专门测试托盘关闭隐藏、无托盘回退、托盘恢复、托盘退出、未读闪烁/Tooltip 计算、通知点击跳转会话。
2. **代码边界**：
   - `tray-manager.cjs` 只负责主进程与操作系统的托盘/通知/预览窗交互，不接触网络 Socket 或本地历史文件。
   - 严格遵守 `FINAL.md` 桌面等效契约：**仅在确认托盘已成功创建且当前桌面环境非已知无托盘黑名单（或用户已通过托盘交互确认）时，关闭按钮才隐藏窗口；若托盘创建失败或不可见，关闭窗口直接回退为最小化到任务栏（或退出），绝不允许出现“窗口隐藏了但托盘也没图标”的幽灵进程**。
3. **IPC/API 设计**：
   - **渲染层 → 主进程**：
     - `lanchat:unread-state`：参数 `{ total: number, items: Array<{ key: string, title: string, count: number, snippet: string, time: string, broadcast: boolean }> }`。主进程据此更新 `tray.setToolTip(...)`（格式对齐 Windows：`LanChat: 3条未读 (局域网全员群聊、阿兰)`，63 字符截断）、启停 450ms 闪烁定时器、刷新托盘预览数据及托盘菜单顶部的快捷未读跳转项。
   - **主进程 → 渲染层（`lanchat:event`）**：
     - `{ event: 'activate-chat', data: 'LAN-192.168.1.10' }`：用户点击系统通知横幅或托盘未读预览条目时发出，主进程先 `showWindow()`，渲染层切换到该 `chatKey` 并清空该会话未读。
     - `{ event: 'window-restored' }`：主窗口从隐藏/最小化恢复到前台时发出，渲染层清除当前正在查看会话的未读角标。
4. **测试用例**：
   - 单元测试（Node 桩测试 `tests/linux_desktop_lifecycle_regression.js`）：
     1) `keepAliveInTray=true` 且托盘可用时，触发 `window.close` 仅调用 `window.hide()`，不触发 `app.quit()`，且首次弹出“LanChat 已最小化到托盘”通知；
     2) 托盘不可用或 `keepAliveInTray=false` 时，触发 `window.close` 正常走退出或最小化回退；
     3) 后台收到私聊/群聊/文件事件时，触发 `Notification`，模拟点击该通知后主窗口 `show()` + `focus()` 且渲染层 `active` 切换为对应 `chatKey`、未读清零；
     4) 多会话未读上报后，Tooltip 字符串与 Windows `UpdateTrayTooltipText()` 逐字一致，未读清零后停止闪烁并恢复 `"LanChat 局域网通讯"`；
     5) 点击托盘菜单“退出 LanChat”置 `exiting = true`，调用 `service.stop()` 并释放进程。
5. **回滚方案**：
   - 删除 `src/linux/desktop/tray-manager.cjs` 与 `tests/linux_desktop_lifecycle_regression.js`，还原 `main.cjs`、`preload.cjs`、`renderer.js`，恢复关闭窗口即退出的原始行为。
6. **完成标准**：
   - 全部生命周期 Node 回归通过；所有手写文件 `< 850` 行；在 Linux 桌面环境（Xorg/Wayland）可完成关闭隐藏、托盘/启动器恢复、通知点击跳会话与托盘真退出。

---

### 阶段 2：节点管理 GUI（含输入区、表情面板、群聊多选与 Windows 最新 UI 基线同步）

1. **修改文件**：
   - **修改** [src/linux/LanChat.Linux/DesktopService.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/DesktopService.cs)（当前 502 行 → 预计 545 行）：
     - 新增 `case "probe-ip":` 调用 `_network.ProbeIp(ip, (ok, msg) => ...)` 并异步返回 `{ ok, message, peers: PeerList() }`；
     - 修复 `LC-FR-007`：新增 `private readonly HashSet<IPAddress> _runtimeAddresses = new();`，在 `OnPeer` 中记录运行期发现的在线节点 IPv4，`KnownAddresses()` 合并 `_runtimeAddresses` 与归档地址，确保未聊过天的节点离线后点刷新也能被定向探测。
   - **修改** [src/linux/LanChat.Linux/Program.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/Program.cs)（当前 576 行 → 预计 595 行）：
     - 在 `RunMain` 启动时绑定 `ChatHistoryService.ProfileStorageDirectory = profile.HistoryDirectory;`，并在 CLI 收发文本/文件成功时调用 `ChatHistoryService.SaveMessage(...)`，确保 **Linux GUI 与独立 CLI 的聊天历史数据路径和落盘行为 100% 一致**。
   - **新增** `src/linux/desktop/node-dialogs.js`（约 280 行）：
     - 实现对标 Windows [GrokConnectNodeForm.cs](file:///d:/TEmp/LanChat/src/UI/Controls/GrokConnectNodeForm.cs)（760×600 连接节点窗口：顶部搜索胶囊、`⟳ 扫描刷新`、4 个分类过滤芯片 `全部 / 局域网在线 / 常用节点 / 手动直连`、双列在线节点卡片、底部手动输入 IPv4 `连接握手`）；
     - 实现对标 Windows [GrokCreateNodeForm.cs](file:///d:/TEmp/LanChat/src/UI/Controls/GrokCreateNodeForm.cs)（680×620 创建新节点窗口：96×96 动态吉祥物预览、名称/IP 输入框、14 种有机形态选择网格、11 色调色盘）；
     - 升级群聊文件选人弹窗 `<dialog id="recipient">`：对齐 Windows `PickFileRecipients(allowMultiple: true)`，当仅 1 名在线好友时免弹窗直发；多名在线好友时支持**多选（最多 16 名在线成员 `MaxFileRecipients = 16`）** + “确定发送”按钮。
   - **新增** `src/linux/desktop/composer-box.js`（约 340 行）：
     - 对齐 Windows `LC-UI-INPUT-008/009/010`（[ChatInputBox.cs](file:///d:/TEmp/LanChat/src/UI/Controls/ChatInputBox.cs)）：支持将输入区内的有效 `[表情名]` 渲染为**内联两倍非透明高 PNG 表情**（底边贴合正文基线、紧凑宽度），同时 `getMessageText()` 严格还原原始 `[表情名]` 字符串；
     - 实现鼠标点击表情内部按中点自动将光标收拢至表情左侧或右侧（对齐 `SnapCaretOutsideEmoji`）；
     - 实现 `Backspace` / `Delete` 整枚原子删除相邻表情令牌（对齐 `TryDeleteAdjacentEmojiToken`）；
     - 实现 2px 主题强调色光标与输入区暗色右键菜单（`粘贴` / `粘贴附件` / `复制` / `剪切` / `全选`），并在 `compositionstart` / `compositionend` 期间保护中文 IME 预编辑串不中断。
   - **修改** [src/linux/desktop/overlay.js](file:///d:/TEmp/LanChat/src/linux/desktop/overlay.js)（当前 300 行 → 预计 350 行）：
     - 将表情选择器升级为 Windows `LC-CC-007` 微信比例 `496×548` 暗色浮层：含顶部“表情”标题栏、“最近使用”（最多 12 项）与“所有表情”（8 列 48px 网格），悬停/键盘聚焦绘制亮底 + `1.5px` 强调色圆角框。
   - **修改** [src/linux/desktop/index.html](file:///d:/TEmp/LanChat/src/linux/desktop/index.html) 与 [src/linux/desktop/desktop.css](file:///d:/TEmp/LanChat/src/linux/desktop/desktop.css)（当前 191 行 → 预计 290 行）：
     - 同步 Windows 微信式 `214px` 底部输入区（`188px` 圆角 18px 胶囊）：上方多行编辑区，下方展开快捷工具栏按钮（表情、打开接收文件夹、选择并发送文件、屏幕截图、清空当前对话，以及连接/新建节点入口）+ 右下角 `44×44` 发送圆钮；新增连接节点与新建节点 `<dialog>` 容器。
   - **修改** [src/linux/desktop/main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs) 与 [src/linux/desktop/preload.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/preload.cjs)：
     - 新增 `probe-ip` IPC；
     - 将 `dispatchFiles` 中的 `for...of await` 串行发送改为并发提交给 `DesktopService`（由共享 C# 核心的单目标 2 路 / 全局 6 路队列统一限流），并支持 `send-paste` / `select-files` / `confirm-drop` 传入 `chatIds[]` 多接收人数组（对托管粘贴截图使用 `remaining` 引用计数，全部接收人结算后才删除未入历史的临时截图）。
   - **修改** [packaging/ExportLinuxDesktopAssets.cs](file:///d:/TEmp/LanChat/packaging/ExportLinuxDesktopAssets.cs)：导出 `Scissors.png`、`Bolt.png` 及 14 种吉祥物有机形态预览图（或由前端 Canvas 按同一几何方程渲染）。
2. **代码边界**：
   - `node-dialogs.js` 独立管理节点连接、新建节点和群聊多选接收人弹窗；`composer-box.js` 独立管理富文本/内联表情输入框，从而使 `renderer.js` 保持在 750 行以内。
   - 所有 IPv4 校验与 TCP/UDP 握手仍在共享 C# `NetworkService.ProbeIp` 中执行，前端不碰任何 Socket。
3. **IPC/API 设计**：
   - **`DesktopService.cs` 新增请求 `probe-ip`**：
     - 请求：`{ "id": "req-1", "method": "probe-ip", "ip": "192.168.1.100" }`
     - 响应：`{ "id": "req-1", "result": { "ok": true, "message": "成功连通 192.168.1.100！", "peers": [...] } }`
   - **`preload.cjs` / `main.cjs` 扩展**：
     - `probeIp: ip => ipcRenderer.invoke('lanchat:probe-ip', ip)`
     - `selectFiles: chats => ipcRenderer.invoke('lanchat:select-files', chats)`（兼容单个 `string` 或 `string[]`，上限 16 个接收人）
     - `sendPastedFiles: (chats, ids) => ipcRenderer.invoke('lanchat:send-paste', chats, ids)`
     - `confirmDrop: (chats, token) => ipcRenderer.invoke('lanchat:confirm-drop', chats, token)`
4. **测试用例**：
   - 在 `tests/linux_desktop_regression.py` 扩展 `probe-ip` 与 `LC-FR-007`（无历史运行期节点离线后刷新重探）以及 CLI 收发消息写 `history_*.jsonl` 断言；
   - 新增 `tests/linux_desktop_node_composer_regression.js`：
     1) 连接节点窗口搜索过滤、4 分类芯片切换、手动 IPv4 校验与 `probeIp` 调用后系统消息插入；
     2) 群聊发文件：1 名在线好友时免弹窗直发；多名在线好友时支持勾选 1~16 人（超 16 人拦截提示），多目标共享托管截图引用计数清理；
     3) 表情选择器 496×548、“最近使用”12 项去重排序、输入框内联表情渲染、`getMessageText()` 还原 `[敲打]`、点击表情内部光标收拢至左右边界、`Backspace`/`Delete` 整枚删除、中文 IME `isComposing` 保护。
5. **回滚方案**：
   - 还原 `DesktopService.cs`、`Program.cs`、`index.html`、`desktop.css`、`renderer.js`、`overlay.js`，移除 `node-dialogs.js` 与 `composer-box.js`，回退至 `<textarea>` 输入框与单选接收人。
6. **完成标准**：
   - 连接/新建节点弹窗、群聊 16 人多选发文件、496×548 表情面板、214/188px 输入区与内联表情编辑行为与 Windows `v2.0.26` 全面对齐，Node 回归与 C# 编译 0 警告通过。

---

### 阶段 3：设置页真实业务接线（彻底消灭 10 项静态假开关/下拉）

1. **修改文件**：
   - **修改** [src/NetworkService.cs](file:///d:/TEmp/LanChat/src/NetworkService.cs)、[src/NetworkService.Interface.cs](file:///d:/TEmp/LanChat/src/NetworkService.Interface.cs)、[src/NetworkService.Discovery.cs](file:///d:/TEmp/LanChat/src/NetworkService.Discovery.cs)、[src/NetworkService.Transfer.cs](file:///d:/TEmp/LanChat/src/NetworkService.Transfer.cs)（每文件仅增加 5~15 行属性开关，默认均为 `true` 保持现有行为 100% 不变）：
     - `public bool EnableTunFilter { get; set; } = true;`（控制 `DiscoverInterfaces` 是否将 `IsVirtualOrTun` 标为过滤，关闭时允许 TUN/VPN 网卡参与发现与发送）
     - `public bool EnableSubnetSweep { get; set; } = true;`（控制 `RefreshPeers` 是否启动 `SweepInterfaces` 1~254 并发单播探测）
     - `public bool VerifyChunkFlush { get; set; } = true;`（控制接收文件流关闭前是否执行 `fs.Flush(true)` 硬件刷盘与严格字节数校验）
     - `public NetworkInterfaceDetail[] GetActiveInterfacesSnapshot()`（供设置页“网络协议通道 / 物理 IP 绑定”下拉胶囊查看当前绑定的真实网卡列表）
   - **修改** [src/linux/LanChat.Linux/LinuxProfile.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/LinuxProfile.cs)（当前 401 行 → 预计 475 行）：
     - 在 `settings.json` 读取与 `SaveSettings` 中新增持久化属性（带默认值，兼容旧配置文件）：
       - `Language`（默认 `"zh-CN"`，可选 `"en-US"`、`"zh-HK"`）
       - `LinkOpenMode`（默认 `0` 智能浏览器打开，`1` 内置预览）
       - `TrayPreview`（默认 `true`）
       - `KeepAliveInTray`（默认 `true`）
       - `SendShortcutMode`（默认 `0` 即 `Enter` 发送，`1` 即 `Ctrl+Enter` 发送）
       - `ChunkCheck`（默认 `true`）
       - `AutoLoadHistory`（默认 `true`）
       - `TunFilter`（默认 `true`）
       - `UnicastProbe`（默认 `true`）
   - **修改** [src/linux/LanChat.Linux/DesktopService.cs](file:///d:/TEmp/LanChat/src/linux/LanChat.Linux/DesktopService.cs)（预计 545 行 → 590 行）：
     - 启动时仅在 `_profile.AutoLoadHistory` 为 `true` 时执行 `_sessions.LoadPersistedHistories()`；
     - `SettingsFrame()` 与 `SaveSettings()` 读写上述 9 个新增设置字段，并在 `SaveSettings` 时同步更新 `_network.EnableTunFilter`、`_network.EnableSubnetSweep`、`_network.VerifyChunkFlush`（若 `TunFilter` 或 `UnicastProbe` 变更则立即调用 `_network.RefreshPeers(KnownAddresses())`）；
     - `state` 响应附带 `interfaces` 数组（网卡名、IPv4、掩码、是否虚拟网卡）。
   - **修改** [src/linux/desktop/settings-model.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-model.js)、[src/linux/desktop/settings-pages.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings-pages.js)、[src/linux/desktop/settings.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings.js)：
     - 将原本放在 `display` 内存假状态里的 `language`、`linkOpenMode`、`trayPreview`、`keepAliveInTray`、`chunkCheck`、`autoLoadHistory`、`tunFilter`、`unicastProbe`、`sendShortcutMode` 全部迁入 `committed` 持久化模型，任何一项切换均走 `persistSettings(...)` 落盘（含只读配置备用路径二次确认）并广播 `settings-updated` 事件；
     - “网络协议通道”与“物理以太网 / Wi-Fi IP 绑定”下拉胶囊点击弹出当前绑定网卡列表与“重新绑定并扫描局域网”动作；
     - “快捷键”页的发送/换行项支持点击下拉切换 `Enter 发送 / Ctrl+Enter 换行` 与 `Ctrl+Enter 发送 / Enter 换行`，且主窗 `renderer.js` 真实绑定 `Ctrl+F`（聚焦 `#search`）与 `Ctrl+L`（清空当前会话）；
     - `conversation-view.js` 识别气泡内 `https?://` 链接，点击时根据 `linkOpenMode` 调用系统默认浏览器打开或弹出应用内安全预览卡片。
   - **（可选同步加固 Windows 设置面板）**：若需 Windows 设置面板的对应开关也同步落盘 `AppSettings.cs`，可在不破坏 Windows 现有默认行为的前提下将相同属性加入 `AppSettings.cs`，默认开启。
2. **代码边界**：
   - 所有新增设置项在 `settings.json` 缺失时**一律取与当前行为完全相同的默认值**（如 `TunFilter=true`、`UnicastProbe=true`、`AutoLoadHistory=true`、`KeepAliveInTray=true`），确保旧用户升级、未知键保留和回退零破坏。
3. **IPC/API 设计**：
   - **`settings` / `save-settings` JSON 载荷扩展**：
     ```json
     {
       "nickname": "LanChat",
       "autoAcceptFiles": true,
       "downloadDirectory": "/home/user/Downloads/LanChat_Received",
       "historyDirectory": "/home/user/.local/share/lanchat/ChatHistory",
       "themeMode": 0,
       "accentPreset": 0,
       "uiFontFamily": "Microsoft YaHei UI",
       "uiFontSize": 10.0,
       "chatFontFamily": "Microsoft YaHei UI",
       "chatFontSize": 10.5,
       "language": "zh-CN",
       "linkOpenMode": 0,
       "trayPreview": true,
       "keepAliveInTray": true,
       "sendShortcutMode": 0,
       "chunkCheck": true,
       "autoLoadHistory": true,
       "tunFilter": true,
       "unicastProbe": true
     }
     ```
   - **主进程新增 `open-external-url`**：仅放行 `http:` / `https:` 协议的 URL 调用 `shell.openExternal(url)`，拒绝 `file:` / `javascript:` 等危险协议。
4. **测试用例**：
   - 在 `tests/linux_desktop_regression.py` 的 `settings_regression` 中增加全部 9 个新增设置项的落盘、只读备用确认、重启恢复，以及 `autoLoadHistory=false` 启动不预载旧历史、`tunFilter` / `unicastProbe` 切换生效断言；
   - 在 Node 测试中验证：点击 `keepAliveInTray`、`trayPreview`、`tunFilter`、`unicastProbe`、`chunkCheck`、`autoLoadHistory`、`language`、`linkOpenMode`、`sendShortcutMode` 均触发 `saveSettings` RPC（更新旧测试中“后台驻留不触发 RPC”的断言为“触发真实持久化 RPC”），且主窗 `Ctrl+F` / `Ctrl+L` 键盘事件生效。
5. **回滚方案**：
   - 还原 `LinuxProfile.cs`、`DesktopService.cs`、`settings-*.js`，旧 `settings.json` 因原本即支持保留未知键，回滚后不会导致任何配置解析报错。
6. **完成标准**：
   - 除阶段 4 的“检查更新”外，其余 10 项原本“只有界面、没有真实功能”的设置控件全部具备真实业务逻辑、IPC 通信与 `settings.json` 持久化，且重启保持。

---

### 阶段 4：更新与维护入口

1. **修改文件**：
   - **新增** `src/linux/desktop/update-service.cjs`（约 260 行）：
     - 在 Electron 主进程实现双轨更新探测（对齐 Windows [UpdateService.cs](file:///d:/TEmp/LanChat/src/Services/UpdateService.cs#L40-L171)）：
       1) 方案 1：请求 `https://api.github.com/repos/yxiaojiang/LanChat/releases/latest`（8 秒超时，`User-Agent: LanChat-Linux/<version>`），解析 `tag_name`、`name`、`html_url`、`body` 与 `assets`；
       2) 方案 2（403 限流降级）：请求 `https://github.com/yxiaojiang/LanChat/releases/latest` 跟随 302 重定向解析 `/releases/tag/vX.Y.Z`，并构造对应下载直链；
     - **架构与形态自动资产选择**：
       - 识别 `process.arch`：`x64` 映射为 `amd64`，`arm64` 映射为 `arm64`；
       - 识别运行形态：`process.env.LANCHAT_INSTALLED === '1'` 时优先选择 `lanchat_<ver>_<arch>.deb`，便携模式优先选择 `lanchat_<ver>_<arch>_portable.tar.gz`，同时提供 `lanchat-cli_<ver>_<arch>` 与 `SHA256SUMS.json` 下载校验；
     - 流式下载到临时目录（`/tmp/LanChatUpdate/` 或 `~/Downloads/`），实时计算 SHA-256 并与 Release 中的 `SHA256SUMS.json` 比对；下载完成后支持一键调用系统图形包安装器 / 打开所在目录并复制 `sudo apt install ./lanchat_<ver>_<arch>.deb` 命令。
   - **修改** [src/linux/desktop/main.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/main.cjs) 与 [src/linux/desktop/preload.cjs](file:///d:/TEmp/LanChat/src/linux/desktop/preload.cjs)：
     - 暴露 `checkUpdate`、`downloadUpdate`、`openUpdateLocation`、`openReleasePage` IPC（同时允许主窗与设置窗调用）。
   - **修改** [src/linux/desktop/settings.html](file:///d:/TEmp/LanChat/src/linux/desktop/settings.html)、[src/linux/desktop/settings.css](file:///d:/TEmp/LanChat/src/linux/desktop/settings.css)、[src/linux/desktop/settings.js](file:///d:/TEmp/LanChat/src/linux/desktop/settings.js)：
     - 将 `showUpdateNotice()` 替换为对标 Windows [GrokUpdateForm.cs](file:///d:/TEmp/LanChat/src/UI/Controls/GrokUpdateForm.cs) 的 `520×440` 暗黑软件更新模态框（版本对比横幅、魅紫闪电图标、更新日志滚动框、6px 平滑进度条、`⚡ 立即下载更新 (.deb / 便携包)`、`🌐 GitHub 发布页`、`稍后再说`），并支持从托盘菜单“检查新版本...”直接拉起。
   - **新增** `tests/linux_desktop_update_regression.js`（约 180 行）：使用本地 Mock HTTP 服务测试 GitHub API 解析、403 降级 302 重定向、`amd64`/`arm64` 资产匹配、SHA-256 校验成功/失败拦截与版本比对。
2. **代码边界**：
   - 更新网络请求只在 Electron 主进程 `update-service.cjs` 发起，渲染进程保持严格 CSP（`default-src 'none'; connect-src 'none'`），不向渲染层开放外网权限；所有远端返回的 `releaseName`、`body` 均用 `textContent` 渲染，严禁 `innerHTML`。
3. **IPC/API 设计**：
   - `lanchat:check-update` → 返回 `{ hasUpdate: boolean, currentVersion: string, latestVersion: string, title: string, notes: string, assetName: string, downloadUrl: string, webUrl: string, kind: 'deb' | 'portable' }`
   - `lanchat:download-update` → 流式发送 `{ event: 'update-progress', data: { percent, transferred, total } }`，完成后返回 `{ savePath: string, sha256Verified: boolean, installCommand: string }`
4. **测试用例**：
   - `tests/linux_desktop_update_regression.js`：
     1) 当前版本等于或高于远端版本时返回 `hasUpdate: false`，界面提示“当前已是最新版本 (v2.0.26)，无需更新”；
     2) 远端版本更高时，在 `x64` 已安装环境选中 `lanchat_<ver>_amd64.deb`，在 `arm64` 已安装环境选中 `lanchat_<ver>_arm64.deb`，在便携环境选中 `lanchat_<ver>_<arch>_portable.tar.gz`；
     3) 模拟 API 返回 `403 Forbidden` 时自动无缝降级到 302 重定向探测最新 Tag；
     4) 下载完成后比对 `SHA256SUMS.json`，摘要篡改时拒绝提供安装并删除坏包。
5. **回滚方案**：
   - 删除 `update-service.cjs` 与 `tests/linux_desktop_update_regression.js`，将 `settings.js` 的 `showUpdateNotice` 还原为静态版本提示。
6. **完成标准**：
   - 通用页、关于页与托盘菜单的“检查更新”全部接通真实版本探测、架构资产匹配、SHA-256 校验下载与安装引导，Mock 回归 100% 通过。

---

### 阶段 5：Linux ELF、桌面环境和真实 LAN 验收

1. **修改文件**：
   - [packaging/ExportLinuxDesktopAssets.cs](file:///d:/TEmp/LanChat/packaging/ExportLinuxDesktopAssets.cs)、[packaging/build-electron.py](file:///d:/TEmp/LanChat/packaging/build-electron.py)、[Tools/Scripts/BuildLinuxDeb.py](file:///d:/TEmp/LanChat/Tools/Scripts/BuildLinuxDeb.py)（构建包含阶段 1~4 全部功能的双架构 `.deb`、`portable.tar.gz`、`lanchat-cli` 与 `SHA256SUMS.json`）
   - [PlanDocs/LC-LINUX-002/INTEGRATION.md](file:///d:/TEmp/LanChat/PlanDocs/LC-LINUX-002/INTEGRATION.md)、[CRDocs/DEFERRED.md](file:///d:/TEmp/LanChat/CRDocs/DEFERRED.md)、[CRDocs/STATUS.md](file:///d:/TEmp/LanChat/CRDocs/STATUS.md)、[CRDocs/REVIEW.md](file:///d:/TEmp/LanChat/CRDocs/REVIEW.md)（随实机证据逐项将 `DEFERRED.md` 中的 `[OPEN]` 迁回并核销）
2. **代码边界**：
   - 阶段 5 为构建打包与真机/真网实测验收阶段，仅针对实机暴露的平台兼容性问题做外科式修复；严禁在无实机截图、日志、哈希和 `dpkg -l` 输出的情况下把任何延期项标记为通过。
3. **IPC/API 设计**：
   - 无新增 IPC；重点核验全量 IPC 在真实 Linux Pipe、Seccomp 沙盒（`NoNewPrivs: 1`, `Seccomp: 2`）及高并发 64KB 文件流下的稳定性。
4. **测试用例（5 大实机与跨端验收矩阵）**：
   1) **最新 ELF 网络命名空间全量回归（关闭 `LC-LINUX-002-ENV-001` & `LC-FR-ENV-002`）**：
      - 在 Linux 非 root 仅回环网络命名空间对最新构建的 `amd64` 与 `arm64` ELF 运行 `python3 tests/linux_desktop_regression.py <elf>` 全量用例（`settings`、`history`、`transfer`、`crash`、`delete` 及新增的 `probe-ip` / `LC-FR-007`）。
   2) **Linux 三类桌面会话、多 DPI、输入法与 2000 条历史性能验收（关闭 `LC-LINUX-002-GUI-001`、`LC-UI-INPUT-008-ENV-001`、`LC-CC-ENV-001`、`LC-LINUX-002-SLICE-3`）**：
      - 在 **GNOME Xorg**、**原生 GNOME Wayland**（`lanchat --ozone-platform=wayland`）、**GNOME XWayland**（`lanchat --ozone-platform=x11`）分别验证：
        - 托盘图标显示、关闭隐藏到托盘、托盘/启动器恢复、未读闪烁与预览、系统通知点击跳转对应会话、托盘退出释放端口；
        - 连接节点弹窗、手动 IPv4 直连、新建节点弹窗、八页设置全部开关持久化与重启保持、检查更新弹窗下载；
        - `fcitx5` 与 `ibus` 在聊天输入框（含内联表情前后）、昵称框、反馈框的中文预编辑、候选词确认、`Enter` 发送与 `Shift+Enter`/`Ctrl+Enter` 换行；
        - 系统截图工具 `Ctrl+V` 粘贴图片、文件管理器 `Ctrl+V` 粘贴文件及拖拽发送；
        - 100% / 125% / 150% DPI 与 880×560 窄窗截图叠图对比，以及 2000 条历史会话切换、滚动与 PSS 内存采样。
   3) **双架构物理机安装、旧包升级、回退与便携/CLI 验收（关闭 `FINAL.md` 第 4 节门槛）**：
      - 在物理 `x86_64` 与物理 `ARM64` 主机上分别核验 `dpkg --print-architecture`、ELF 机器码（62 / 183）；
      - 演练公开版 `v2.0.16` / 候选包 → 新版 `.deb` 升级 → `apt install --allow-downgrades` 回退 → 旧 Python/Tk GUI 启动与新版追加的 JSONL 历史互读；
      - 验证 `portable.tar.gz` 解压即用及 `lanchat --cli` / `lanchat-cli` 无头 SSH 操作。
   4) **物理 Windows 与物理 Linux 真实局域网（含切网/TUN/旧版）互通验收（关闭 `LC-FR-ENV-001` & `LC-FR-007`）**：
      - 物理 Windows 主机（`v2.0.26+` 及公开版 `v2.0.16`）与物理 `x86_64`、物理 `ARM64` Linux 主机接入同一局域网：
        - 互验冷启动发现、在线改昵称、对端无 `BYE` 重启归并、群聊/私聊（含 109 微信表情与 Unicode 组合表情）、双向文件传输（0B、64KB、64MB、同名并发、群聊多选接收人）的 SHA-256；
        - 实测 Wi-Fi/有线切网、开启 Clash/WireGuard TUN 虚拟网卡下的穿透绑定与设置页 TUN 过滤开关切换。
5. **回滚方案**：
   - 若新版 `.deb` 在特定发行版出现依赖或桌面兼容阻塞，用户可执行 `sudo apt install --allow-downgrades ./lanchat_2.0.16_*.deb`（或上一稳定版 `.deb`）一键无损回退；由于 `LinuxProfile` 在首次修改前生成了 `.before-electron.bak` 并保持原 `ChatHistory/history_*.jsonl` 格式不变，用户历史与已接收文件 100% 完好可查。
6. **完成标准**：
   - `CRDocs/DEFERRED.md` 中所有 Linux / 跨端 OPEN 项均附带实机日志、截图、SHA-256 与命令输出核销归档，独立代码审核结论为 `APPROVED`。

---

## 四、硬性架构守恒契约核对（6 条红线逐一锁定）

| 硬性守恒契约 | 本计划保障措施 |
| --- | --- |
| **1. LANC 封包兼容** | 严禁改动 `MAGIC ("LANC")`、UDP `38888` 六段公告格式（`CMD\|ID\|Nick\|Host\|Port\|IP`）、TCP `packetType 1`（文字帧）与 `packetType 2`（文件报价头 + `0x01`/`0x00` 握手确认字节 + 64KB 正文流 + `0x01`/`0x00` 落盘最终确认字节）。表情继续传输原始 `[名称]` 文本；群聊多选发文件在发送端拆分为向所选在线节点逐一发起标准 Type 2 单播文件流，对端无需任何协议变更。 |
| **2. JSONL 历史格式兼容** | 严禁改动 `ChatHistoryService.SerializeMessage` 的字段名、顺序与转义规则（`history_BROADCAST.jsonl`、`history_peer_LAN-<IPv4>.jsonl`）。迁移历史目录继续采用只读复制原文件、保留旧目录给旧版回退策略。 |
| **3. Windows 行为不回归** | 每个阶段涉及到共享 C# 文件（如阶段 3 在 `NetworkService` 增加默认值为 `true` 的 `EnableTunFilter`、`EnableSubnetSweep`、`VerifyChunkFlush` 属性）后，**必须立即在宿主机复跑 `Tools/Scripts/Compile.bat`、`tests/chat_composer_regression.ps1 -ExpectLocalPack`、`tests/friend_refresh_regression.ps1` 与 `tests/file_transfer_stability_regression.ps1`**，确保 Windows 编译与行为零回归。 |
| **4. 共享 C# 网络核心不被重复实现** | Electron 主进程（Node.js）**绝不创建任何局域网 UDP/TCP Socket**（仅阶段 4 检查 GitHub Releases 更新使用 HTTPS）。手动 IPv4 直连（`probe-ip`）、网卡重绑、文件流收发全部通过 stdin/stdout JSONL 管道调用同一个 C# ELF (`DesktopService`) 执行。 |
| **5. Linux GUI 与 CLI 数据路径一致** | GUI（`DesktopService`）与 CLI（`Program.cs`）共用同一个 `LinuxProfile` 实例：按完全相同的优先级选定 `settings.json`、`DownloadDirectory` 与 `HistoryDirectory`，共用 `~/.local/share/lanchat/locks/<SHA256>` 文件锁互斥；并在阶段 2 为 CLI `Program.cs` 补齐 `ChatHistoryService.ProfileStorageDirectory = profile.HistoryDirectory`，使 CLI 与 GUI 读写同一份 JSONL 历史。 |
| **6. 手写文件 1000 行预警、1500 行上限** | 阶段 1~4 新增功能按职责拆分为 `tray-manager.cjs`（~260 行）、`node-dialogs.js`（~280 行）、`composer-box.js`（~340 行）、`update-service.cjs`（~260 行）及独立测试文件，并将新文件全部注册进 `tests/linux_desktop_ui_regression.js` 的 `< 1000` 行自动门禁检查列表中，杜绝任何文件突破 1000 行预警线。 |

---

## 五、推荐实施顺序、风险清单、实机环境需求与开发任务拆解

### 1. 推荐实施顺序

建议严格按 **“先立规矩锁基线 → 再通桌面生命周期 → 补齐节点与输入区 GUI → 接通设置真业务 → 闭环更新维护 → 双架构实机与真网总验”** 的顺序推进：
1. **第 1 步（阶段 0）**：冻结基线与自动化门禁，拆分 `tests/linux_desktop_ui_regression.js`（当前 924 行）的测试辅助函数，腾出测试行数空间；
2. **第 2 步（阶段 1 + 阶段 3 的驻留/托盘预览开关）**：优先实现 `tray-manager.cjs`（托盘、关闭隐藏、恢复、通知点击跳会话、真退出），并同时把设置页的“后台网络驻留运行”和“托盘未读悬浮预览”两个开关真正接上，一次跑通桌面生命周期闭环；
3. **第 3 步（阶段 2）**：补齐 `DesktopService` 的 `probe-ip` 与 `LC-FR-007` 离线地址缓存、CLI 历史落盘一致性、连接/新建节点弹窗、群聊 16 人多选发文件、496×548 表情选择器以及 214/188px 微信式高输入区与内联表情编辑；
4. **第 4 步（阶段 3 剩余项）**：将设置页其余 8 项假开关/下拉（TUN 过滤、单播探测、自动加载历史、64KB 刷盘校验、快捷键 `Ctrl+F`/`Ctrl+L` 与发送键模式、语言、链接打开方式、网络协议通道网卡列表）全部接通持久化与业务回调；
5. **第 5 步（阶段 4）**：实现 `update-service.cjs` 与 520×440 软件更新弹窗，打通 `amd64`/`arm64` 资产选择与 SHA-256 校验下载；
6. **第 6 步（阶段 5）**：运行 `Tools/Scripts/BuildLinuxDeb.py` 产出双架构候选包，在物理 Linux 双架构实机与物理 Windows 主机上执行全矩阵现场验收。

---

### 2. 风险清单与应对预案

| 编号 | 风险描述 | 影响级别 | 应对与缓解方案 |
| :---: | --- | :---: | --- |
| **R1** | **GNOME / 原生 Wayland 缺少传统系统托盘扩展**（部分 Ubuntu/GNOME 默认未启用 AppIndicator 扩展时托盘图标不可见） | **高 (P0)** | `tray-manager.cjs` 启动时检测 StatusNotifier/AppIndicator 可用性；若桌面无托盘支持或用户未确认托盘可见，`window.on('close')` 自动回退为最小化到 Dock/任务切换器或提示退出，且始终可通过桌面启动器二次运行（`second-instance`）唤回窗口，确保窗口 100% 可恢复。 |
| **R2** | **Wayland 合成器焦点防抢占策略（Focus Stealing Prevention）及通知点击限制** | **中 (P1)** | 点击系统通知或托盘恢复时，依次调用 `window.restore()`、`window.show()`、`window.focus()`；若 Wayland 合成器仅高亮任务栏图标未直接切前台，渲染层仍立即完成 `switchChat(chatKey)` 切到目标会话，用户从 Dock 点回窗口即可直接看到目标对话。 |
| **R3** | **Web 输入框实现内联表情 PNG 时打断中文 IME（`fcitx5` / `ibus`）预编辑或破坏纯文本复制** | **高 (P0)** | `composer-box.js` 严格监听 `compositionstart` / `compositionend`（及 `event.isComposing`、`keyCode === 229`），在中文拼音预编辑未上屏期间**绝对禁止重排 DOM 或修改 Selection**；内联表情 DOM 节点保留原始 `[表情名]` 文本或 `data-token` 属性，拦截 `copy`/`cut` 事件还原纯文本 `[表情名]`，保持与 Windows `GetMessageText()` 完全一致。 |
| **R4** | **手写测试与源码触碰 1000 行软门禁**（如 `tests/linux_desktop_ui_regression.js` 已 924 行，`settings.css` 已 742 行） | **高 (P1)** | 严禁往 `linux_desktop_ui_regression.js` 继续堆砌新用例；阶段 1~4 的新增测试分别放入 `linux_desktop_lifecycle_regression.js`、`linux_desktop_node_composer_regression.js`、`linux_desktop_update_regression.js`，并在总入口串联执行。 |
| **R5** | **宿主机缺少 .NET 10 全局安装或 Linux 运行环境** | **中 (P1)** | 复用项目内已缓存的 `bin/lc-linux-002/cache/dotnet`（.NET SDK 10.0.404）与 `bin/lc-linux-002/cache/electron-v45.4.1-linux-*.zip` 构建双架构 ELF 与 `.deb`；实机验收交由用户提供的物理 Linux 环境执行。 |

---

### 3. 需要用户提供的 Linux 实机验收环境

进入 **阶段 5** 时，需要用户协助提供或执行以下实机环境取证（虚拟机结果仅作开发期回归，不替代实机放行）：
1. **物理 `x86_64` Linux 主机（1 台）**：
   - 推荐系统：Ubuntu 24.04 LTS（或 22.04 LTS）桌面版；
   - 桌面会话：具备 **GNOME Wayland（原生）** 与 **GNOME Xorg** 登录会话切换能力；
   - 输入法：安装有 `fcitx5`（如 fcitx5-chinese-addons）或 `ibus`（ibus-libpinyin）；
   - 显示缩放：支持切换 100%、125%、150% 缩放。
2. **物理 `ARM64` (`aarch64`) Linux 主机（1 台）**：
   - 如已验证过第 2 步的 **银河麒麟桌面操作系统 V10 国防版（020）ARM64**（`glibc 2.31`、`libssl1.1`）或 Ubuntu 22.04/24.04 ARM64 物理设备；
   - 用于验证 `lanchat_<ver>_arm64.deb` 升级、回退、`dpkg -l` 状态、桌面启动、剪贴板粘贴与无头 CLI。
3. **物理 Windows 主机（1 台，与上述 Linux 主机处于同一真实局域网）**：
   - 运行当前公开版 `v2.0.16` / 最新版 `v2.0.26+` Windows 客户端；
   - 包含真实物理网卡（Wi-Fi / 有线以太网）及可选的 TUN 代理软件（如 Clash / WireGuard）环境，用于与两台物理 Linux 主机进行真实 LAN 发现、群聊/私聊、文件 SHA-256 与切网互通联测。

---

### 4. 可直接拆分为后续开发任务的任务列表（Task Breakdown）

后续你只需指定任意一个任务编号，即可直接开始对应的外科式代码开发与独立审核：

- **`TASK-LINUX-SYNC-01`（阶段 1：托盘、通知与桌面生命周期闭环）**
  - **范围**：新增 `tray-manager.cjs`、导出托盘图标、实现关闭隐藏到托盘（含无托盘安全回退）、托盘点击/菜单恢复、托盘未读闪烁与 Tooltip/预览、后台新消息/新文件系统通知及点击跳转对应会话、托盘菜单真退出，并接通设置页“后台网络驻留运行”与“托盘未读悬浮预览”持久化开关。
  - **交付物**：源码改动 + `tests/linux_desktop_lifecycle_regression.js` 自动化回归。
- **`TASK-LINUX-SYNC-02`（阶段 2A：节点管理 GUI、手动 IPv4 直连与群聊多选发文件）**
  - **范围**：`DesktopService.cs` 新增 `probe-ip` IPC 与 `LC-FR-007` 运行期无历史节点离线地址缓存；`Program.cs` CLI 绑定 `ChatHistoryService.ProfileStorageDirectory` 落盘历史；前端新增 `node-dialogs.js` 实现 760×600“连接节点”弹窗、680×620“创建新节点”弹窗；升级群聊选人弹窗支持 1~16 名在线成员多选与单好友免弹窗直发；解除 `main.cjs` `dispatchFiles` 串行 `await` 限制。
  - **交付物**：C# 与前端源码改动 + 节点与多选发文件回归测试。
- **`TASK-LINUX-SYNC-03`（阶段 2B：微信式 214/188px 高输入区、内联表情对齐与 496×548 表情选择器）**
  - **范围**：前端新增 `composer-box.js` 与升级 `overlay.js`、`index.html`、`desktop.css`，对齐 Windows `LC-CC-007` 与 `LC-UI-INPUT-008/009/010`（496×548 含“最近使用”12 项 + 8 列 48px 网格表情选择器、214/188px 输入区与展开式底部快捷工具栏、输入框内联表情 PNG 基线对齐、点击表情内部光标收拢至两侧、Backspace/Delete 整枚原子删表情、2px 主题色光标与输入框暗色右键菜单）。
  - **交付物**：前端源码改动 + `tests/linux_desktop_node_composer_regression.js` 自动化回归。
- **`TASK-LINUX-SYNC-04`（阶段 3：八页设置其余 8 项假功能真实业务接线）**
  - **范围**：在 `NetworkService`、`LinuxProfile`、`DesktopService` 与 `settings-*.js` 中接通并持久化 `TUN/TAP 过滤`、`多网卡并发单播探测`、`自动加载历史消息`、`64KB 分块传输刷盘校验`、`快捷键 Ctrl+F / Ctrl+L 及发送键模式切换`、`语言（简/英/繁）`、`链接打开方式（系统浏览器/内置预览）`、`网络协议通道（真实绑定网卡列表与重绑刷新）`。
  - **交付物**：双端/Linux 源码改动 + Python/Node 设置持久化回归。
- **`TASK-LINUX-SYNC-05`（阶段 4：Linux 在线更新检查、双架构资产匹配与 520×440 更新弹窗）**
  - **范围**：新增 `update-service.cjs`（GitHub API + 302 降级、`amd64`/`arm64` 与 `.deb`/`portable` 自动资产匹配、`SHA256SUMS.json` 校验流式下载）与 520×440 软件更新模态框，接通通用页、关于页与托盘菜单的“检查更新”入口。
  - **交付物**：更新模块源码 + `tests/linux_desktop_update_regression.js` Mock 回归。
- **`TASK-LINUX-SYNC-06`（阶段 5：双架构候选包构建与 Linux 实机/ELF/真实 LAN 现场验收）**
  - **范围**：运行 `BuildLinuxDeb.py` 构建最新 `amd64`/`arm64` `.deb`、便携包与独立 CLI；在 Linux 命名空间跑通 `tests/linux_desktop_regression.py`；配合用户物理 `x86_64`、`ARM64` 与 Windows 实机逐项完成 Xorg/Wayland/XWayland、`fcitx5`/`ibus`、多 DPI、升级回退与真实双机 LAN 验收并清零 `CRDocs/DEFERRED.md`。