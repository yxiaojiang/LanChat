# 本轮交付 — Windows 缩放稳定性、11 色外观、传输速率与群聊文件多选（独立审核）

- **日期**：2026-11-25
- **执行者**：Codex（基线提交 `e3e9a58`）
- **审核方**：独立审核方
- **审核状态**：**WAITING_FOR_GROK**（`LC-RV-001` 已返修，等待独立审核者复跑）
- **范围**：头像缩放防闪烁、附件图 11 色外观预设、Windows/Linux 文件气泡当前速率、群聊在线收件人多选与托管截图副本清理，以及无在线好友空列表返修。
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`，以及 `e3e9a58` 实际 diff 与相关调用链。

## 独立复跑与验证边界

### 1. Windows 宿主机独立复跑（均退出码 0）

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：退出码 0（`Native Windows compile: ok`、`LC-FR-001`~`004`/`008`、`LC-CHAT-COMPOSER-001`、`LC-CC-006`、头像 `DoubleBufferedPanel`、群聊收件人多选、0 在线好友空列表与 `null` 快照归一化断言通过）。
2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：退出码 0（包含 `外观 11 色调色盘与发送/接收速率格式化断言通过`、`LC-CC-005`、`LC-CC-004` 及 Emoji/PNG/LANC/历史回归）。
3. `C:\Program Files\nodejs\node.exe` 对 `src/linux/desktop/*.js`、`src/linux/desktop/*.cjs` 与 `tests/linux_desktop_*_regression.js` 共 15 个文件执行 `--check`，并运行 `tests/linux_desktop_ui_regression.js`：全部退出码 0，输出 `LC-CC-004: Linux IPC 发送终态、退出预检与管道晚到确认清理通过` 与 `linux desktop ui regression: ok`。
4. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`：退出码 0，0 警告、0 错误。
5. `git diff --check`：退出码 0，无空白错误；本轮改动手写源码与测试文件均 `< 1000` 行软门禁（最高为 `tests/linux_desktop_ui_regression.js` 907 行）。

### 2. 未验证的跨环境边界（继续保留于 `CRDocs/DEFERRED.md`）

- 真实 Windows 拖拽缩放长时间录屏、真实截图工具（Win+Shift+S / 微信截图）/ Excel / WPS 粘贴。
- Linux Xorg / Wayland / XWayland 实机 Electron、最新 Linux ELF 实跑（`tests/linux_desktop_regression.py`）与真实双机局域网联测。

## 审核结论与维度核对

**WAITING_FOR_GROK**。`LC-RV-001` 已修复：无在线好友时 `PickFileRecipients` 返回 `new List<PeerInfo>()`，入口统一归一化 `null` 快照，调用方继续通过 `Count` 判空；自动化断言和全部宿主机回归已通过，等待独立审核者确认关闭。

| 核对维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. 头像缩放防闪烁 | PASS | `src/MainForm.Designer.cs:210` 将 `_heroProfilePanel` 改为 `DoubleBufferedPanel`，`FriendRefreshRegression.cs:281` 已覆盖类型断言 |
| 2. 11 色配色与索引兼容 | PASS | `GrokTheme.cs`、`AppearanceSettingsPanel.cs`、`AppSettings.cs`（上限 11）、`LinuxProfile.cs`（上限 11）与 `appearance.js` 均严格对齐 11 色 RGB 与索引顺序，越界均做夹紧 |
| 3. 双向传输速率显示 | PASS | `NetworkService.Transfer.cs` 收发均写入 `SpeedMBps`；`FileTransferController.FormatTransferRate` 与 `conversation-view.js` 统一格式化 `KB/s` / `MB/s`；`ChatFileCardRenderer.cs` 与 `DesktopService.cs` 透传/渲染正常 |
| 4. 群聊多选与托管截图清理 | **PASS（返修）** | `PickFileRecipients` 多选与 `SendFilesToPeers` 为 `i > 0` 生成 `"群发-<guid>.png"` 托管副本及失败清理逻辑成立；无在线好友分支返回空列表，`NormalizeRecipientList` 将 `null` 快照归一化，`PromptPeerAndSendFiles` 与 `PickFileRecipient` 的 `Count` 访问不再崩溃 |
| 5. LANC 协议与 JSONL 兼容 | PASS | 未改动 `Models.cs`、`NetworkService*.cs`、`ChatHistoryService.cs`、`SessionManager.cs`，LANC 封包与 JSONL 历史格式完全兼容 |
| 6. 行数门禁与注释规范 | PASS | 全部改动文件 `< 1000` 行软门禁，无空白错误 |

### 问题清单

| 问题 ID | 严重度 | 状态 | 位置与说明 |
| --- | :---: | :---: | --- |
| `LC-RV-001` | **P1** | **RESOLVED，待独立复审** | `src/MainForm.Files.cs` 无在线好友分支已返回 `new List<PeerInfo>()`，并通过 `NormalizeRecipientList` 防御 `null` 快照；`FriendRefreshRegression` 新增 0 在线好友和 `null` 归一化断言，关闭警告后不会再触发 `NullReferenceException`。 |

---

# LC-CC-005 / LC-CC-006 — 独立审核

- **任务编号**：`LC-CC-005` / `LC-CC-006`
- **任务主题**：Windows 图文混合剪贴板优先级、隐藏输入框首次右键暗色菜单（含复制/剪切/全选），以及组合表情像素命中、跨 CRLF 复制、会话草稿隔离与关闭清理回归断言
- **审核方**：独立审核方
- **审核日期**：2026-11-25
- **审核状态**：**APPROVED**（当前开发与 Windows 宿主机回归范围放行）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`，以及工作区实际 diff（`src/UI/Controls/ChatInputBox.cs`、`src/MainForm.Designer.cs`、`tests/ChatComposerRegression.cs`、`tests/FriendRefreshRegression.cs`）。

## 独立复跑与验证边界

### 1. Windows 宿主机独立复跑（均退出码 0）

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：退出码 0，输出 `LC-CC-005: Windows 图文混合剪贴板优先级断言通过` 及既有 Emoji、PNG/位图粘贴、LANC 回环、历史回读与 `LC-CC-004` 回归。
2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：退出码 0，输出包含 `Native Windows compile: ok`、`LC-CHAT-COMPOSER-001`（含隐藏输入框首次右键弹出 `GrokPopupMenu`）及 `LC-CC-006: 组合表情像素命中、跨 CRLF 复制、切会话草稿和关闭清理通过`。
3. `C:\Program Files\nodejs\node.exe` 对 `src/linux/desktop/*.js`、`src/linux/desktop/*.cjs` 与 `tests/linux_desktop_*_regression.js` 共 15 个文件执行 `--check`，并运行 `tests/linux_desktop_ui_regression.js`：全部退出码 0，输出 `LC-CC-004: Linux IPC 发送终态、退出预检与管道晚到确认清理通过` 与 `linux desktop ui regression: ok`。
4. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`：退出码 0，0 警告、0 错误。
5. `git diff --check`：退出码 0，无空白错误；变更文件行数均 `< 1000` 行软门禁（`ChatInputBox.cs` 180 / 编辑器 184 行、`MainForm.Designer.cs` 690 行、`ChatComposerRegression.cs` 291 / 295 行、`FriendRefreshRegression.cs` 466 / 472 行，工作区最高 `tests/linux_desktop_ui_regression.js` 905 / 922 行）。

### 2. 待验证的跨环境实机项（未验证，继续保留）

- **Windows 真实桌面剪贴板**：真实 Windows 截图工具（Win+Shift+S / 微信截图）与 Excel/WPS 单元格的 `Ctrl+V`、`Shift+Insert` 及右键粘贴。
- **Linux 实机、ELF 与双机联测**：Linux Xorg / Wayland / XWayland 下的 Electron 实机联调、最新 Linux ELF 实跑（`tests/linux_desktop_regression.py`）与真实双机局域网联测，继续保留于 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`、`LC-LINUX-002-ENV-001`、`LC-LINUX-002-GUI-001`）。

## 结论与六维核对

**APPROVED（当前范围放行）**。无 P0、无 P1 遗留；`LC-CC-005`、`LC-CC-006` 标记为 **RESOLVED**（Windows 代码实现与宿主机自动化回归闭环，真实桌面工具/Excel/WPS、Linux 实机/ELF 与双机联测保留于 `CRDocs/DEFERRED.md`）。

| 核对维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. 图文混合剪贴板优先级 | PASS | `ChatInputBox.TryPasteAttachment` 优先识别 `FileDrop`，随后以 `HasClipboardText` 检查非空 `UnicodeText`/`Text` 并返回 `false` 交给原生文本粘贴，仅无非空文本时才读取 `PNG`/`Bitmap`；`HasClipboardAttachment` 与 `ShowContextMenuAt` 同步遵循此优先级 |
| 2. 隐藏输入框首次右键与编辑菜单 | PASS | `MainForm.Designer.cs:471-477` 在 `_inputPanel.MouseDown` 右键显框聚焦后直接调用 `_txtInput.ShowContextMenuAt(_inputPanel.PointToScreen(e.Location))`；`ChatInputBox.WndProc` 接管 `WM_CONTEXTMENU` 统一展示暗色 `GrokPopupMenu`，按剪贴板与选区动态提供「粘贴」「粘贴附件」「复制」「剪切」「全选」 |
| 3. `LC-CC-006` 选区、复制与草稿断言 | PASS | `FriendRefreshRegression.cs` 在真实 `WeChatChatView` 上逐像素扫描 `"甲👨‍👩‍👧‍👦👍🏽🇨🇳❤️1️⃣\r\n乙"` 全部视觉行确认 `GetCharIndexAtPoint` 仅落于 `ChatTextLayout.NextBoundary` 边界，验证跨 CRLF 全选复制原文一致、切换会话草稿隔离/恢复及显式关闭清理 |
| 4. LANC 协议与 JSONL 兼容 | PASS | 未改动任何 LANC 封包结构、传输协议或 JSONL 历史格式与字段 |
| 5. 宿主机回归与构建 | PASS | 两套 Windows 回归、原生编译、Node 15 文件 `--check` 与 UI 回归、共享后端 `dotnet build` 及 `git diff --check` 全部退出码 0 |
| 6. 行数门禁与中文注释规范 | PASS | 全部变更文件 `< 1000` 行软门禁，新增注释与 XML 文档均为中文 |

| 问题 ID | 原严重度 | 结论 | 说明 |
| --- | :---: | :---: | --- |
| `LC-CC-005` | P2 | **RESOLVED** | 图文剪贴板优先级与隐藏输入框首次右键暗色菜单（含复制/剪切/全选）已落地并通过回归；真实 Excel/WPS 与截图工具实机验收保留于 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`） |
| `LC-CC-006` | P2 | **RESOLVED** | 组合表情像素命中、跨 CRLF 复制、会话草稿隔离/恢复与关闭清理自动化断言已补齐并通过 |

---

# LC-CC-004 — 独立审核

- **任务编号**：`LC-CC-004`
- **任务主题**：双端应用截图异步发送终态清理、历史引用保护与 Linux 退出并发收尾
- **审核方**：独立审核方
- **审核日期**：2026-11-25
- **审核状态**：**APPROVED**（当前开发与 Windows 宿主机回归范围放行）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`，以及以已放行的 `LC-CC-003` 工作区变更为基线的实际 diff 与完整调用链（`src/Controllers/FileTransferController.cs`、`src/MainForm.Chat.cs`、`src/MainForm.Files.cs`、`src/NetworkService.Transfer.cs`、`src/Services/ChatHistoryService.cs`、`src/Services/SessionManager.cs`、`src/linux/LanChat.Linux/DesktopService.cs`、`src/linux/desktop/{main.cjs,service.cjs}`、`tests/{ChatComposerRegression.cs,linux_desktop_ipc_regression.js}`）。

## 独立复跑与验证边界

### 1. Windows 宿主机独立复跑（均退出码 0）

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：退出码 0，输出包含 `LC-CC-004: Windows 拒收、断连、存储失败与退出清理通过，保留成功截图和用户原文件` 及原有 Emoji/剪贴板/LANC/历史回归。
2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：退出码 0（原生 `csc.exe` 编译、好友刷新/网卡重绑、侧栏交互与截图草稿暂存移除通过）。
3. 独立以 `csc.exe` 编译 `src/`（排除 `linux/`、`obj/`、`bin/`、`Uninstall.cs`）至临时目录并编译运行 `tests/ConcurrentReceive.cs`：退出码 0，输出 `Final ACK: 1, 1; Complete events: 2`，两份同名文件 SHA-256 校验通过（随后清理临时目录）。
4. `C:\Program Files\nodejs\node.exe` 对 `src/linux/desktop/*.js`、`src/linux/desktop/*.cjs` 与 `tests/linux_desktop_*_regression.js` 共 15 个文件执行 `--check`，并运行 `tests/linux_desktop_ui_regression.js`：全部退出码 0，输出 `LC-CC-004: Linux IPC 发送终态、退出预检与管道晚到确认清理通过` 与 `linux desktop ui regression: ok`。
5. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`：退出码 0，0 警告、0 错误。（注：本机未安装 PowerShell 7 `pwsh`，未直接执行 `bin/lc-cc-004-backend/verify.ps1`，`DesktopService.cs` 逻辑已通过源码与调用链静态核对。）
6. `git diff --check`：退出码 0，无空白错误；全部 18 个工作区改动/新增手写源码与测试文件均 `< 1000` 行软门禁（`LC-CC-004` 改动文件最高为 `main.cjs` 598 行 / 编辑器 611 行、`MainForm.Chat.cs` 588 行；工作区最高为 `tests/linux_desktop_ui_regression.js` 905 行 / 编辑器 923 行）。

### 2. 待验证的跨环境实机项（未验证，继续保留）

- **Linux 实机与 ELF**：Linux Xorg / Wayland / XWayland 下的真实 Electron 截图发送、预检/队列期间退出清理、最新 Linux ELF 实跑（`tests/linux_desktop_regression.py`）。
- **真实双机联测**：真实双机端到端截图拒收、断连、成功后历史重开、用户原文件保留，以及新旧客户端互通。上述实机项继续保留于 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`、`LC-LINUX-002-ENV-001`、`LC-LINUX-002-GUI-001`）。

## 结论与六维核对

**APPROVED（当前范围放行）**。无 P0、无 P1 遗留；`LC-CC-004` 标记为 **RESOLVED**（双端代码与宿主机回归闭环，实机/双机补验保留于 `CRDocs/DEFERRED.md` `LC-CC-ENV-001`）。

| 核对维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. Windows 截图所有权移交与退出/历史竞态 | PASS | `MainForm.Chat.cs:418` 经 `TakeDraftImage` 仅移交实际入队截图给 `FileTransferController._outgoingPastes`；完成回调与 `DiscardOutgoingPastes()` 共用 `lock (_outgoingPastes)`：退出先删则回调直接返回不写历史，发送先完则同步调用 `PersistExistingMessage(..., confirmStorage: true)` 写入并复核 JSONL，成功保留、失败删除；`NetworkService.Transfer.cs:384` 仅对托管截图开启 `FileShare.Delete` |
| 2. Linux `send-paste` 与 `before-quit` 交叠 | PASS | `stage-paste`、`discard-paste`、`send-paste` 与后台 `send()` 均纳入 `pasteOperations`；`before-quit` 置 `exiting = true` 后等待 `service.stop()`、`discardPastedFiles` 与全部 `pasteOperations`；预检与队列循环均检查 `exiting`，退出期间不再派发排队附件且立即清理已接管截图而非回写 `pendingPastes` |
| 3. 管道晚到确认与后端异常终止保留 | PASS | `sendingPastes` 跟踪在途托管截图，`child.on('event')` 收到 `historySaved === true` 置 `paste.saved = true`；`finally` 在断连时 `await child.stop()` 等 stdout 管道关闭排空晚到 `message` 事件后再判删；`service.cjs:27,79` 返回 `code === 0`，后端异常退出返回 `false` 时保留截图 |
| 4. 历史引用与用户原文件保护 | PASS | Windows 仅 `StageClipboardImage` 进入 `_draftImagePreviews`，Linux 仅 `stageClipboardImage` 置 `managed: true`；用户文件与普通发送沿用原路径与 `FileShare.Read`，不会被草稿/失败/退出清理误删；已写入 JSONL 的成功截图在退出与无 UI 句柄场景均保留 |
| 5. LANC 协议与 JSONL 兼容 | PASS | LANC 文件报价帧（`LANC` + `2`）、`0x01`/`0x00` 确认字节与 JSONL 记录格式零改动；`managedPaste` 与 `historySaved` 仅用于本地 Electron 与 `DesktopService` 管道 |
| 6. 行数门禁与中文注释规范 | PASS | 全部改动/新增文件 `< 1000` 行软门禁，新增注释均为中文，无空白错误 |

| 问题 ID | 原严重度 | 结论 | 说明 |
| --- | :---: | :---: | --- |
| `LC-CC-004` | P2 | **RESOLVED** | 双端异步传输终态清理、历史引用保护与退出并发收尾代码与宿主机回归通过；Linux 实机/ELF 与双机联测补验保留于 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`） |

---

# LC-CC-003 — 独立复审

- **任务编号**：`LC-CC-003`
- **任务主题**：Linux 侧栏摘要按可见元素截断、归档启动摘要透传、表情回显与缺图回退，以及 `renderer.js` 职责拆分
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-25
- **审核状态**：**APPROVED**（当前开发与宿主机回归范围放行）
- **依据**：`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`、`.claude/Rules.md`，以及工作区实际 diff（`src/linux/LanChat.Linux/DesktopService.cs`、`src/linux/desktop/{desktop.css,index.html,presentation.js,renderer.js,session-sidebar.js,conversation-view.js}`、`tests/linux_desktop_ui_regression.js`）与 `packaging/build-electron.py`。

## 独立复跑与验证边界

### 1. 宿主机回归与专项复跑（本次复审在 Windows 宿主机独立执行，均退出码 0）

1. `C:\Program Files\nodejs\node.exe`（`v24.18.0`）：对 `src/linux/desktop/` 下 13 个 JS/CJS 与 `tests/` 下 2 个回归脚本共 15 个文件逐一执行 `--check`，全部退出码 0；运行 `node tests/linux_desktop_ui_regression.js`，退出码 0，输出 `linux desktop ui regression: ok`。
2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：退出码 0（Windows 组合表情整段测量与 GDI+ 像素对照、本地表情加载、剪贴板数据、LANC 回环与历史回读通过）。
3. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：退出码 0（原生编译、好友刷新与网卡重绑、会话列表键盘/滑块、分组尺寸与截图草稿回归通过）。
4. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`：退出码 0，0 警告、0 错误。
5. 独立专项复跑（隔离临时夹具与 Edge Headless CDP）：
   - **侧栏摘要边界**：复测私聊 16 / 群聊 20 阈值下 `👨‍👩‍👧‍👦`、`👍🏽`、`🇨🇳`、`[敲打]`、`[未知令牌]` 的恰好达限、保留边界与舍弃边界，连续多令牌边界，`displayName` 13 字素边界（`keepTokensTogether = false`），`[文件]` 摘要与空会话兜底，全部通过。
   - **归档启动路径**：加载编译产物 `lanchat.dll` 对隔离 JSONL 历史目录执行 `SessionManager.LoadPersistedHistories()` 与 `DesktopService.ProcessLine("{\"id\":\"req-1\",\"method\":\"state\"}")`，确认私聊（39 字符）、文件（28 字符）及群聊（32 字符）归档 `summary` 均完整未截断返回；并在渲染端确认 `setPeers` 合并在线态时保留归档 `summary`。
   - **表情回显与回退**：在 Edge Headless（加载真实 `index.html`、`desktop.css` 与全部生产 JS，仅以 `/mock-bridge.js` 注入 `window.lanchat`）验证 420px 与 280px 窄侧栏：有效 PNG（`[敲打]`）真实解码（`naturalWidth === 1`）；坏图（`[坏图]`）触发 `error` 事件后移除 `.message-emoji`、保留 `.summary-token`（`display: inline-block`）并恢复非透明文字；缺图（`[未知]`）保留 `.summary-token`；单行 `nowrap` / `text-overflow: ellipsis` / `9.5pt`（`12.7px`）且不折行。
6. `git diff --check` 退出码 0；全量 8 个改动/新增源码与测试文件编辑器行数复核与 `IMPLEMENTATION.md` 一致且均 `< 1000` 行软门禁（`tests/linux_desktop_ui_regression.js` 923、`renderer.js` 690、`DesktopService.cs` 471、`presentation.js` 201、`conversation-view.js` 194、`desktop.css` 192、`session-sidebar.js` 146、`index.html` 90）；`packaging/build-electron.py:200-202` 自动收录 `src/linux/desktop/` 全目录文件，新拆模块打包路径完整。

### 2. Linux 实机与跨机验证边界（未验证，继续保留）

- 宿主机为 Windows，本轮**尚未验证** Linux Xorg / Wayland / XWayland 下的真实 Electron 45 窄侧栏缩略效果、Linux 系统 Emoji 字形与 DPI 缩放表现、真实系统剪贴板、Linux/QEMU 网络命名空间最新 ELF 实跑（`tests/linux_desktop_regression.py`），以及真实双机新旧客户端联测。上述跨环境验收继续保留在 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`、`LC-LINUX-002-ENV-001`、`LC-LINUX-002-GUI-001`）。

## 结论与六维核对

**APPROVED（当前范围放行）**。无 P0、无 P1 遗留；`LC-CC-003` 标记为 **RESOLVED**（宿主机代码与回归闭环，Linux 实机视觉补验保留于 `CRDocs/DEFERRED.md` `LC-CC-ENV-001`）；`LC-CC-006` 中 `renderer.js` 拆分部分已落地，剩余 Windows 选区与草稿用例保留于 `CRDocs/DEFERRED.md`。

| 核对维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. `renderer.js` 职责拆分与打包收录 | PASS | `renderer.js` 由 961 行降至 690 行，拆出 `session-sidebar.js`（146 行）与 `conversation-view.js`（194 行），`index.html` 按序加载，`build-electron.py` 自动收录全目录 |
| 2. 侧栏摘要字素与表情令牌边界 | PASS | `presentation.js` 以 `Intl.Segmenter`（`grapheme`）+ `EMOJI_TOKEN`（`/\[[^\[\]\s]{1,24}\]/g`）计数截断，私聊 16 / 群聊 20 阈值与 `displayName` 13 字素边界成立 |
| 3. 启动归档 `summary` 透传链路 | PASS | `DesktopService.cs:127` 传 `int.MaxValue` 输出完整归档摘要，`renderer.js` `setPeers` 合并在线状态时保留 `previous.summary`，由渲染端统一按可见元素截断 |
| 4. 侧栏表情回显与缺图/坏图回退 | PASS | `drawMessageText(..., true)` 复用气泡渲染逻辑；有图显 PNG，无包或单图缺失保留 `.summary-token` 可读文字，`img` `error` 清理 `.message-emoji` 并恢复文本与自然宽度 |
| 5. LANC 协议、历史格式与 Windows 兼容性 | PASS | 未改动 `Models.cs`、`NetworkService*.cs`、`ChatHistoryService.cs` 及 `SessionManager.GetLastMessageSnippet` 默认 20 上限，LANC 帧与 JSONL 历史完全向后兼容 |
| 6. 行数门禁与中文注释规范 | PASS | 全部改动/新增文件 `< 1000` 行软门禁，新增注释均为中文，无空白错误 |

| 问题 ID | 原严重度 | 结论 | 说明 |
| --- | :---: | :---: | --- |
| `LC-CC-003` | P2 | **RESOLVED** | 代码实现、回归测试与宿主机隔离夹具/Edge Headless 复验通过；Linux Xorg/Wayland/XWayland 实机窄侧栏与 DPI 视觉补验保留于 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`） |

---

# LC-CHAT-COMPOSER-001 — Round 2 独立复审

- **任务编号**：`LC-CHAT-COMPOSER-001`
- **任务主题**：微信表情导入与无资源回退、Unicode 组合表情排版与划选复制、在线/离线分组 8px 间距与圆点、截图粘贴草稿与发送（Round 2 返修复审）
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-24
- **审核状态**：**APPROVED**（当前开发与宿主机回归范围放行）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`（Round 2）、`CRDocs/REVIEW.md`（Round 1）、`CRDocs/DEFERRED.md`，以及工作区实际 diff、完整调用链与 20 个改动/新增源码及测试文件。

## 独立复跑与验证边界

### 1. 宿主机回归（本次复审在 Windows 宿主机独立复跑，均退出码 0）

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：通过。原生 `csc.exe` 生产编译通过；`LC-FR-001` ~ `LC-FR-004` / `LC-FR-008` 与 `LC-CHAT-COMPOSER-001`（真实 WinForms 列表 32/40px 分组间距、单组 32px、翠绿 `#1FD67E` / 珊瑚红 `#FC4455` 圆点、截图草稿暂存/缩略图比例与移除清理）全部通过。
2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：通过。输出 `BMP 肤色、ZWJ 和独立表情的 Segoe UI Emoji 整段测量及 GDI+ 像素对照通过` 与 `聊天表情布局、PNG 与位图粘贴数据、LANC 文本和图片回环及历史回读通过`，验证了 `✋🏽`、`✌🏽`、`❤‍🔥`、`✋`、`✨`、`☕` 的单段测量宽度、混合文本绘制宽度及 240×60 逐像素一致，以及 `[敲打]` 本地 64×64 加载、LANC 回环与历史回读。
3. 使用 `C:\Program Files\nodejs\node.exe` 对全部 7 个改动/新增 JS/CJS 文件（`src/linux/desktop/{main.cjs,overlay.js,preload.cjs,presentation.js,renderer.js}`、`tests/{linux_desktop_ui_regression.js,linux_desktop_ipc_regression.js}`）执行 `--check`，并运行 `tests/linux_desktop_ui_regression.js`（内联调用拆出的 `tests/linux_desktop_ipc_regression.js`）：全部退出码 0，输出 `linux desktop ui regression: ok`。
4. `python -m py_compile Tools/Scripts/import-wechat-emoji.py` 退出码 0；`bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false` 退出码 0（0 警告、0 错误）。
5. `git diff --check` 退出码 0；全量 20 个改动/新增手写源码与测试文件行数复核全部 `< 1000` 行软门禁：
   - 本轮变更文件：`src/UI/Renderers/ChatTextLayout.cs`（209）、`tests/ChatComposerRegression.cs`（188）、`src/linux/desktop/preload.cjs`（96）、`src/linux/desktop/main.cjs`（575）、`tests/linux_desktop_ui_regression.js`（869）、`tests/linux_desktop_ipc_regression.js`（220）；
   - 全工作区最高为 `src/linux/desktop/renderer.js`（961 行，本轮未触碰，已登记 `LC-CC-006` 要求下次扩展前先拆分）。

### 2. 真实双机验证（未验证）

- 尚未在两台物理/虚拟机间实测：双端同包微信表情逐条收发、复制与历史重开；新版与未导入资源端或公开旧版跨机互通（可读 `[名称]`、Unicode 组合表情、截图 PNG 文件卡片打开）；以及真实 Windows 桌面截图工具（Win+Shift+S / 微信截图 / 图像应用）的 `Ctrl+V`、`Shift+Insert`、右键粘贴链路。继续保留在 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`）。

### 3. Linux 实机验收（未验证）

- 宿主机为 Windows，本轮仅完成 Node 桩 IPC/模拟 DOM 回归与共享后端宿主机构建；**尚未验证** Linux/QEMU 最新 ELF 实跑（`tests/linux_desktop_regression.py`）、Linux Electron 45 在 Xorg/Wayland/XWayland 下的真实 `clipboard.read()`、截图工具/文件管理器/浏览器复制图像时的 PNG/JPEG 兼容回退、表情网格暗色滑块与实机分组视觉对齐。继续保留在 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`、`LC-LINUX-002-ENV-001`、`LC-LINUX-002-GUI-001`）。

## 结论

**APPROVED（当前范围放行）**。无 P0、无 P1 遗留。Round 1 阻断放行的全部 2 项 P1（`LC-CC-001`、`LC-CC-002`）均已按要求落地且经源码调用链审查与独立复跑验证闭环；Node IPC 专项用例已按职责拆至 `tests/linux_desktop_ipc_regression.js`（220 行），原入口 `tests/linux_desktop_ui_regression.js` 回落至 869 行；4 项 P2（`LC-CC-003` ~ `LC-CC-006`）与跨环境验收项（`LC-CC-ENV-001`）均已带触发阶段与验收条件登记至 `CRDocs/DEFERRED.md`。LANC 协议封包、网络层、历史存储与旧客户端兼容零破坏；全量 20 个改动/新增文件 `< 1000` 行，新增注释均为中文。

## Round 1 问题逐项复审结论

| 问题 ID | 严重度 | 结论 | 复核依据 |
| --- | :---: | :---: | --- |
| `LC-CC-001` | P1 | **RESOLVED** | `src/UI/Renderers/ChatTextLayout.cs:150-200`：`PaintText` 改为 `for (int i = 0, end; i < text.Length; i = end)` 按 `NextBoundary(text, i)` 推进，结合 `IsEmojiElement(text, i, end)` 覆盖 `0x1F000..0x1FAFF`、`0x2600..0x27BF`、常用 BMP 计时/星标符号及 `\uFE0F`/`\u20E3`/`\u200D`，整段交给 `Segoe UI Emoji` 一次测量与绘制，普通文字保持连续排版；`tests/ChatComposerRegression.cs:76-118` 新增 `VerifyEmojiPaint` 对 `✋🏽`、`✌🏽`、`❤‍🔥`、`✋`、`✨`、`☕` 完成整段宽度、混排宽度与 240×60 逐像素对照并独立复跑通过 |
| `LC-CC-002` | P1 | **RESOLVED** | `src/linux/desktop/preload.cjs:38-52` 仅对 `image/png`、`image/jpeg` 内存图像走 `ArrayBuffer` 直传，非直传格式（WebP/BMP/GIF）有 `plain` 时走原生文本粘贴、无 `plain` 时交由主进程读系统剪贴板；`src/linux/desktop/main.cjs:229-272` 仅解码 `['image/png', 'image/jpeg']`，`decodeClipboardImage` 返回空图时继续尝试下一候选（内存空图亦回退 `clipboard.read()`），全部尝试失败才抛 `无法读取剪贴板图片`，仅有非兼容格式时静默返回空草稿且不建文件；`tests/linux_desktop_ipc_regression.js:9-165` 通过 `vm.runInNewContext` 实测 `preload.cjs` 与 `main.cjs` 全部路径并独立复跑通过 |
| `LC-CC-003` | P2 | **DEFERRED** | 已登记至 `CRDocs/DEFERRED.md`，随 `renderer.js` 拆分与 Linux 侧栏摘要专项处理 |
| `LC-CC-004` | P2 | **DEFERRED** | 已登记至 `CRDocs/DEFERRED.md`，归入双端附件生命周期专项处理异步失败清理与退出竞态 |
| `LC-CC-005` | P2 | **DEFERRED** | 已登记至 `CRDocs/DEFERRED.md`，归入 Windows 输入区交互整改与真实剪贴板验收 |
| `LC-CC-006` | P2 | **DEFERRED** | Node IPC 测试已完成职责拆分（`linux_desktop_ui_regression.js` 降至 869 行）；剩余 `renderer.js` 拆分与 Windows 选区/草稿补齐用例已登记至 `CRDocs/DEFERRED.md` |
| `LC-CC-ENV-001` | — | **DEFERRED** | 保留在 `CRDocs/DEFERRED.md`，待真实双机 LAN、Windows 真实截图工具与 Linux ELF / Electron 实机验收 |

---

# LC-CHAT-COMPOSER-001 — Round 1 独立审核

- **任务编号**：`LC-CHAT-COMPOSER-001`
- **任务主题**：微信表情导入与无资源回退、Unicode 组合表情排版与划选复制、在线/离线分组 8px 间距与圆点、截图粘贴草稿与发送
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-24
- **审核状态**：**REJECTED**（按 `CRDocs/RULES.md` 打回整改；对应 `CRDocs/README.md` 的 `CHANGES_REQUESTED`）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`、`Docs/GrokBot_UI_Design_System.md`，以及工作区全部 18 个已修改文件与 5 个未跟踪新文件（`Tools/Scripts/import-wechat-emoji.py`、`src/UI/Controls/ChatEmojiPicker.cs`、`src/UI/Renderers/ChatTextLayout.cs`、`tests/ChatComposerRegression.cs`、`tests/chat_composer_regression.ps1`）。

## 独立复跑与验证边界

### 1. 宿主机回归（本次审核在 Windows 宿主机独立复跑，均退出码 0）

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：通过。原生 `csc.exe` 生产编译成功；`LC-FR-001` ~ `LC-FR-004` / `LC-FR-008` 及 `LC-CHAT-COMPOSER-001`（真实 WinForms 列表 32/40px 标题行高、单组筛选 32px、`#1FD67E`/`#FC4455` 6px 状态圆点、200×100 截图保存 PNG 及 24×12 缩略图与移除清理）通过。
2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：通过（已确认本机 `%LOCALAPPDATA%\LanChat\WeChatEmoji` 存在 109 行 `manifest.tsv` 与 109 个 `<= 64×64` PNG）。包含 `ChatTextLayout` 窄行折行与 CRLF 索引、`[敲打]` 64×64 加载与彩色绘制、PNG 透明度/Bitmap/FileDrop/文本区分、LANC 隔离回环文本与 PNG 字节一致及 JSONL 历史回读。
3. `"C:\Program Files\nodejs\node.exe"` 逐一 `--check` 检查 `src/linux/desktop/{main.cjs,preload.cjs,presentation.js,overlay.js,renderer.js}` 与 `tests/linux_desktop_ui_regression.js`，并运行 `tests/linux_desktop_ui_regression.js`：全部退出码 0，输出 `linux desktop ui regression: ok`（注：本机 `C:\WINDOWS\system32\node` 为无效占位，须使用 `C:\Program Files\nodejs\node.exe`）。
4. `python -m py_compile Tools/Scripts/import-wechat-emoji.py` 通过；`bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false` 在 Windows 宿主机通过（0 警告、0 错误）。
5. 审核者只读反射验证：对 `WeChatChatView.GetCharIndexAtPoint` 全行逐像素扫描确认 `"甲👨‍👩‍👧‍👦👍🏽🇨🇳❤️1️⃣\r\n[敲打]乙"` 命中 0 次可见元素内拆点；同时在编译产物上**实测复现**了 `LC-CC-001`（`✋🏽` 与 `❤‍🔥` 被 `ChatTextLayout.PaintText` 拆为两段字体绘制与测量）。
6. `git diff --check` 通过；全部 19 个改动/新增源码与测试文件编辑器行数均 `< 1000` 行软门禁（最高为 `tests/linux_desktop_ui_regression.js` 964 行与 `src/linux/desktop/renderer.js` 961 行）。

### 2. 真实双机验证（未验证）

- 尚未在两台物理/虚拟机之间执行：双方导入同包微信表情逐条收发、复制与历史重开；新版与未导入资源端或公开旧版（v2.0.19 及更早 Windows/Linux/Python 客户端）跨机互通验收 `[敲打]` 可读文本、Unicode 组合表情与截图 PNG 文件卡片打开；以及真实 Windows 桌面截图工具（Win+Shift+S / 微信截图 / 图像应用）的 `Ctrl+V`、`Shift+Insert`、右键粘贴链路。见 `CRDocs/DEFERRED.md` 的 `LC-CC-ENV-001`。

### 3. Linux 实机验收（未验证）

- 宿主机为 Windows，本轮仅完成 Node 桩 IPC/模拟 DOM 回归与 C# 后端 Windows 宿主机构建；**尚未验证** Linux/QEMU 最新 ELF 实跑（`tests/linux_desktop_regression.py`）、Linux Electron 45 真实 Xorg/Wayland/XWayland 系统剪贴板（`clipboard.read()`、截图工具、文件管理器、浏览器图片复制）、表情网格暗色滑块交互及实机分组视觉对齐。见 `CRDocs/DEFERRED.md` 的 `LC-CC-ENV-001`、`LC-LINUX-002-ENV-001`、`LC-LINUX-002-GUI-001`。

## 结论

**REJECTED（打回整改）**。无 P0。微信 4.2.0.13 本地导入脚本哈希校验与 Qt 资源解包逻辑清晰，无资源时双端均安全回退为 Unicode 面板与可读 `[名称]` 文本；LANC 封包魔数、文本帧、文件帧与 JSONL 历史格式零破坏，完全兼容旧客户端；双端在线/离线分组 8px 间距与 6px `#1FD67E`/`#FC4455` 状态圆点度量一致；Windows 截图预览、切会话隔离、移除删图、确认发送保图与退出清理闭环基本成立。

但存在 **2 项 P1 阻断放行问题**（另登记 **4 项 P2**）：

1. `LC-CC-001`：Windows `ChatTextLayout.PaintText` 未按可见元素步进，实测会将 BMP 基底组合表情（如 `✋🏽`、`✊🏽`、`✌🏽`、`❤‍🔥`）拆成 UI 字体 + Emoji 字体两段分别绘制与测量，且自带回退面板中的 `✋`/`✨`/`☕` 未路由至 Emoji 字体；
2. `LC-CC-002`：Linux `preload.cjs` 与 `main.cjs` 将 `image/webp`、`image/bmp`、`image/gif` 等非 PNG/JPEG 原始字节直接交给仅支持 PNG/JPEG 解码的 `nativeImage.createFromBuffer`，必然抛出 `无法读取剪贴板图片`，且 `preload.cjs` 抢占 `data.files` 中的非 PNG/JPEG 图像后会跳过系统剪贴板原本可用的 `image/png`。

## 六维核对摘要

| 维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. 微信表情导入与无资源可读回退 | PASS | `Tools/Scripts/import-wechat-emoji.py` 强校验微信 4.2.0.13 `Weixin.dll` SHA-256、Qt 目录结构与 PNG 头，原子写入 `manifest.tsv`；Windows `%LOCALAPPDATA%\LanChat\WeChatEmoji` 与 Linux `app.getPath('userData')/WeChatEmoji`（`package.json` `productName: "LanChat"`）路径一致；无包时双端回退 50 个 Unicode 表情且对端保留 `[名称]` 原文 |
| 2. Unicode 组合表情换行/划选/复制/历史回读 | FAIL | `BreakLines` 保留 CRLF 索引、`WeChatChatView.GetCharIndexAtPoint` 逐像素验证不拆元素、`CopySelectedText` 与 `ChatHistoryService` 完整保留原文；但存在 `LC-CC-001`（P1，`PaintText` 拆分 BMP 基底组合表情）与 `LC-CC-003`（P2，Linux 侧栏 `clip()` 按 UTF-16 截断） |
| 3. 在线/离线分组 8px 间距与 6px 圆点 | PASS | Windows `MeasureItem` 与 `ListSessions_DrawItem` 共用 `SessionHeadingGap`（双组同显离线 40px、单组 32px），`AccentGreen` (`#1FD67E`) / `TextDestructive` (`#FC4455`) 6px 圆点距标题右侧 6px 垂直居中；Linux `desktop.css:75-78` 以 `[data-online="true"] ~ [data-online="false"]` 加 `margin-top: 8px` 及同色同尺寸 `::after` 圆点对齐 |
| 4. 截图粘贴预览/切会话/移除/确认发送/清理 | FAIL | Windows 预览按 24px 等比缩放、按会话隔离、移除/删好友/退出清理未提交 PNG、确认发送保留本地副本且不误删用户文件；但 Linux 端存在 `LC-CC-002`（P1，非 PNG/JPEG 图像必败且屏蔽 `image/png` 回退）；另有 `LC-CC-004`（P2，异步失败传图残留与退出竞态）和 `LC-CC-005`（P2，Windows 文本+位图优先级与首次右键） |
| 5. LANC 协议与旧客户端兼容性 | PASS | `src/Models.cs`、`src/NetworkService*.cs`、`src/Services/ChatHistoryService.cs` 与 `src/linux/LanChat.Linux/` 零改动；微信表情按普通 UTF-8 文本 `[名称]` 传输，截图复用既有 PNG 文件流，旧客户端零兼容破坏 |
| 6. 行数门禁与中文注释规范 | PASS | 全部 19 个改动/新增源码与测试文件 `< 1000` 行软门禁，新增类与公开/内部成员具备全中文注释与 XML 文档；`renderer.js`（961 行）与 `linux_desktop_ui_regression.js`（964 行）逼近 1000 行预警线（见 `LC-CC-006`） |

## P0（阻断级）

无。

## P1（本轮必须修复）

### LC-CC-001 [P1 / OPEN] Windows `ChatTextLayout.PaintText` 按 UTF-16 单码元扫描，拆分 BMP 基底组合表情并遗漏自带回退 BMP 表情

- **位置**：`src/UI/Renderers/ChatTextLayout.cs:150-182`、`src/UI/Controls/ChatEmojiPicker.cs:22-23`
- **复现步骤与实测证据**：
  1. 构造含 BMP 基底肤色或无 `\uFE0F` ZWJ 组合表情的消息：`"✋🏽"`（`U+270B + U+1F3FD`，UTF-16 长度 3）或 `"❤‍🔥"`（`U+2764 + U+200D + U+1F525`，UTF-16 长度 4）；
  2. `ChatTextLayout.NextBoundary(text, 0)` 正确返回 `3` 与 `4`，但在 `PaintText` 中循环为 `for (int i = 0; i < text.Length; i++)`，第 153-154 行仅检查 `(code >= 0x1F000 && code <= 0x1FAFF) || (i + 1 < text.Length && (text[i + 1] == '\uFE0F' || text[i + 1] == '\u20E3'))`；
  3. `i = 0` 时 `0x270B` / `0x2764` 不命中，循环推进至 `i = 1` 命中肤色修饰符 `0x1F3FD` 或后段 `0x1F525`，导致 `PaintText` 先用主 UI 字体绘制 `"✋"` / `"❤"`，再用 `Segoe UI Emoji` 单独绘制 `"🏽"` / `"\u200D🔥"`；
  4. 在本轮编译产物 `ChatComposerRegression.exe` 上反射调用 `ChatTextLayout.Measure` 实测结果：
     - `✋🏽`：`NextBoundary(0) == 3`，`ChatTextLayout.Measure == 33.781` == `Measure(YaHei, "✋") + Measure(SegoeEmoji, "🏽") == 33.781`（整段 `Segoe UI Emoji` 应为 `35.650`）；
     - `❤‍🔥`：`NextBoundary(0) == 4`，`ChatTextLayout.Measure == 32.155` == `Measure(YaHei, "❤") + Measure(SegoeEmoji, "\u200D🔥") == 32.155`（整段 `Segoe UI Emoji` 应为 `36.158`）；
  5. 此外，`ChatEmojiPicker.UnicodeEntries` 自带的无资源回退表情 `✋`（`U+270B`）、`✨`（`U+2728`）、`☕`（`U+2615`）均不在 `0x1F000..0x1FAFF` 且无 `\uFE0F`，同样不会切入 `Segoe UI Emoji` 绘制。
- **影响**：标准 Unicode 肤色组合手势（`✋🏽`、`✊🏽`、`✌🏽`、`☝🏽`、`✍🏽`）及无 `\uFE0F` 的 ZWJ 表情在气泡与侧栏预览中被拆成两段不同字体，出现手势与肤色色块分离且测量宽度失真。
- **修复要求**：
  1. `PaintText` 改为按 `int end = NextBoundary(text, i)` 逐可见元素步进（避免拆进元素内部或落到低代理码元）；
  2. 对整个元素 `[i, end)` 判定本地微信表情或 Unicode 表情（覆盖 `0x1F000..0x1FAFF`、BMP 常用 Emoji 区段如 `0x2600..0x27BF`/`0x2300..0x23FF`/`0x2B50`，或元素内含 `\uFE0F`/`\u20E3`/`\u200D`/肤色修饰符 `0x1F3FB..0x1F3FF`），将整个元素一次性交给 `Segoe UI Emoji` 测量与绘制；
  3. 在 `tests/ChatComposerRegression.cs` 补充 `✋🏽`、`❤‍🔥`、`✋`/`✨`/`☕` 的整段测量与绘制回归断言。

### LC-CC-002 [P1 / OPEN] Linux 剪贴板图像将 WebP/BMP/GIF 直接交由 `nativeImage.createFromBuffer` 导致必然解码失败，且抢占系统剪贴板的 `image/png`

- **位置**：`src/linux/desktop/preload.cjs:37, 45-49`、`src/linux/desktop/main.cjs:230-265`
- **复现链路与依据**：
  1. Electron 官方文档明确 `nativeImage.createFromBuffer(buffer)` 仅原生解码 **PNG** 与 **JPEG**，不支持 WebP、BMP 或 GIF（传入这些编码的原始字节时 `nativeImage.createFromBuffer` 返回空图像，`image.isEmpty() === true`）；
  2. `main.cjs:249` 的 `readClipboardImage` 将 `['image/png', 'image/jpeg', 'image/webp', 'image/bmp']` 直接交由 `stageClipboardImage` -> `nativeImage.createFromBuffer(Buffer.from(bytes))`。当系统剪贴板仅含 `image/bmp` 或 `image/webp` 时必然抛出 `无法读取剪贴板图片`；
  3. 更严重的是 `preload.cjs:37` 使用 `file.type.startsWith('image/')` 匹配 `event.clipboardData.files` 中的首个内存图像，并在第 48 行直接传递 `await image.arrayBuffer()` 给 `lanchat:stage-paste`，致使 `main.cjs:230` 跳过 `clipboard.read()`。当用户在 Linux 浏览器或桌面应用中复制 WebP/GIF 图片时，`clipboardData.files` 往往暴露原始 `image/webp` / `image/gif`，即便系统剪贴板同时提供了转换后的 `image/png`，该路径也会直接把 WebP/GIF 原始字节送进 `nativeImage.createFromBuffer` 导致粘贴报错；
  4. `tests/linux_desktop_ui_regression.js` 中 `nativeImage.createFromBuffer` 为固定返回非空对象的桩实现，未暴露此格式不匹配问题。
- **修复要求**：
  1. `preload.cjs` 仅对 `image/png` 与 `image/jpeg` 直接走内存 `ArrayBuffer` 快路径；遇到其他 `image/*`（如 `image/webp`、`image/gif`、`image/bmp`）时，优先在渲染进程通过 `createImageBitmap` / Canvas 转码为 PNG 字节，或回退交由主进程读取 `clipboard.read()` 中可用的 `image/png` / `image/jpeg`（并注意避免 `plain` 非空时误拦截纯文本）；
  2. `main.cjs` 的 `readClipboardImage` 与 `stageClipboardImage` 对齐 `nativeImage.createFromBuffer` 实际支持的 PNG/JPEG 格式（若保留多 MIME 遍历，应在 `createFromBuffer` 返回空图时继续尝试下一可用 MIME，而非首次命中非支持格式即整体抛错）；
  3. 在 `tests/linux_desktop_ui_regression.js` 补充非 PNG/JPEG 剪贴板图像回退与空图跳过断言。

## P2（可延期或返修顺手处理）

### LC-CC-003 [P2 / OPEN] Linux 侧栏摘要 `clip()` 按 UTF-16 码元截断且未复用表情渲染

- **位置**：`src/linux/desktop/presentation.js:55-58, 91`、`src/linux/desktop/renderer.js:138-139`
- **说明**：Windows 侧栏摘要已改用 `ChatTextLayout.DrawSnippet` 按可见元素截断并显示表情图；Linux 侧栏仍通过 `value.slice(0, limit - 1)` 按 UTF-16 码元硬截断，遇到多码元组合表情（如 `👨‍👩‍👧‍👦`、`👍🏽`、`🇨🇳`）或 `[敲打]` 时可能截断代理对产生孤立替代符或半截令牌（如 `[敲...`）。建议改为按可见字符/表情令牌边界截断。

### LC-CC-004 [P2 / OPEN] 双端异步发送失败的截图 PNG 不会自动清理，且 Linux `send-paste` 与 `before-quit` 存在并发退出残留窗口

- **位置**：`src/MainForm.Chat.cs:419`、`src/Controllers/FileTransferController.cs:127-136`、`src/linux/desktop/main.cjs:399, 520-526`
- **说明**：
  1. 双端在点击确认发送启动异步传输时即解除草稿托管，若后续 TCP 发送失败（如对端拒收或断连），失败消息不写入历史，而 `PastedImages/截图-*.png` 仍留在用户目录；
  2. Linux `send-paste` 在 `await dispatchFiles` 前先从 `pendingPastes` 删除 `id`，若期间触发 `before-quit` 执行 `discardPastedFiles([...pendingPastes.keys()])`，随后 `dispatchFiles` 抛错在 `catch` 中又将 `id` 写回 `pendingPastes`，导致退出时漏删未提交截图。建议在 `catch` 中判断 `if (exiting)` 立即 `unlink` 临时图片。

### LC-CC-005 [P2 / OPEN] Windows 剪贴板同时含文本与位图时的优先级与 Linux 不一致，且空输入栏首次右键未弹出粘贴菜单

- **位置**：`src/UI/Controls/ChatInputBox.cs:50-79`、`src/MainForm.Designer.cs:471-475`
- **说明**：
  1. Linux `preload.cjs:46` 在系统剪贴板路径传入 `allowImage = !plain`，避免同时含文本与位图的富文本/表格复制被误当图片；Windows `ChatInputBox` 未检查 `DataFormats.UnicodeText`，从 Excel/WPS 复制单元格时会按截图粘贴，且剪贴板残留截图时右键输入框会以单一“粘贴附件”菜单替代原生复制/剪切菜单；
  2. Windows `_txtInput` 空态隐藏时首次右键 `_inputPanel` 仅触发 `MouseDown` 显示输入框，需二次右键才弹出暗色粘贴菜单。

### LC-CC-006 [P2 / OPEN] `renderer.js` 与 `linux_desktop_ui_regression.js` 逼近 1000 行软门禁，且 Windows 回归可补齐选区与会话切换用例

- **位置**：`src/linux/desktop/renderer.js`（961 行）、`tests/linux_desktop_ui_regression.js`（964 行）、`tests/ChatComposerRegression.cs`、`tests/FriendRefreshRegression.cs`
- **说明**：两份 JS 文件距 `.claude/Rules.md` 第 10 条 1000 行预警线分别仅余 39 行与 36 行，后续继续加功能/测试前须先按职责拆分；另建议在返修回归中补充 Windows `WeChatChatView` 像素级划选不拆表情、跨 CRLF 复制及截图草稿切换会话保留/退出清理的自动化断言。

## 跨环境验收项（保留在 `CRDocs/DEFERRED.md`）

| 问题 ID | 状态 | 说明 |
| --- | :---: | --- |
| `LC-CC-ENV-001` | OPEN | 真实 Windows 桌面截图粘贴键盘链路、真实双机新旧客户端联测、Linux Xorg/Wayland Electron 剪贴板与最新 ELF 实跑 |

---

# LC-FRIEND-REFRESH-001 — Round 2 独立复审

- **任务编号**：`LC-FRIEND-REFRESH-001`
- **任务主题**：好友刷新延迟、网卡切换与在线/离线分组（Round 2 返修复审）
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-24
- **审核状态**：**APPROVED**
- **依据**：`.claude/Rules.md`、`CRDocs/README.md`、`CRDocs/RULES.md`、`CRDocs/STATUS.md`、`CRDocs/REVIEW.md`（Round 1）、`CRDocs/DEFERRED.md`、`CRDocs/IMPLEMENTATION.md`（Round 2）、工作区完整 diff 与实际调用链。
- **独立复跑与验证边界**：
  - 审核者独立复跑并确认通过：
    1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`（退出码 0；原生 `csc.exe` 生产编译通过；隔离回环与 Win32 消息夹具 `LC-FR-001`、`LC-FR-002`、`LC-FR-003 / LC-FR-008`、`LC-FR-004` 四组检查全部通过）；
    2. 对本轮生成的 `LanChat.exe` 编译并运行 `tests/ConcurrentReceive.cs`（退出码 0，`Final ACK: 1, 1; Complete events: 2`，双同名文件 SHA-256 校验通过）；
    3. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`（退出码 0，0 警告 0 错误）；
    4. `C:\Program Files\nodejs\node.exe` 执行 `--check src/linux/desktop/renderer.js`、`--check tests/linux_desktop_ui_regression.js` 及 `tests/linux_desktop_ui_regression.js`（均退出码 0，输出 `linux desktop ui regression: ok`）；
    5. `git diff --check` 通过，全量 17 个改动/新增源码与测试文件编辑器行数复核通过。
  - **未验证边界（不构成实机验收）**：本轮所有验证均在 Windows 宿主机上完成（生产编译、回环隔离夹具、Win32 原生消息测试、Linux 后端宿主机构建、Node 模拟 DOM 与桩 IPC）。**尚未验证**真实多机 LAN、Wi-Fi/有线实际切网、DHCP 延迟、休眠唤醒、物理多网卡/TUN 路由及公开旧版（v2.0.18 / Python）跨机互通（`LC-FR-ENV-001`），**尚未验证** Linux/QEMU 网络命名空间实跑最新 ELF（`tests/linux_desktop_regression.py`）与真实 Linux Electron 桌面侧栏交互（`LC-FR-ENV-002`）。

## 结论

**APPROVED（当前范围放行）**。无 P0、无 P1 遗留。Round 1 阻断放行的全部 4 项 P1（`LC-FR-001` ~ `LC-FR-004`）均已按要求落地且经源码调用链与独立复跑回归确认闭环；4 项 P2（`LC-FR-005`、`LC-FR-006`、`LC-FR-008`、`LC-FR-009`）已同步闭环；`LC-FR-007` 已按规则明确写入 `CRDocs/DEFERRED.md` 并具备清晰触发阶段与验收条件。LANC 封包魔数、文本帧字段、`[系统自动握手]`、TCP `0x01` 确认字节与 UDP 六段竖线格式保持向后兼容；全部工作区改动文件均低于 1000 行软门禁，新增注释符合全中文规范。

## 七维复核摘要

| 维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. UDP ACK 与 TCP 握手并发归并/昵称/在线态 | PASS | `LC-FR-001` 与 `LC-FR-005` 闭环：`UpdatePeer` 在 `_peerLock` 内按 `(IPv4, TcpPort)` 归并同端点旧身份、累加未读并在锁外派发 `OnPeerLeft`；`TryTcpHandshake` 持 `_discoveryLock -> _peerLock` 校验快照、对象引用与端点独占性，拒绝为已移除或冲突端点续期；匿名 `expected == null` 探测不刷新已知真实身份；Linux `setPeers` 保留运行期与归档昵称 |
| 2. 刷新计时器/重复刷新/切网重绑/线程竞态与泄漏 | PASS | `LC-FR-002` 与 `LC-FR-006` 闭环：启动、改昵称、切网回调与手动刷新统一经 `RefreshPeers()` 先执行 `RebindInterfaces()`，可重试空 `_udpAnySocket` / 失败监听并复用健康快照；锁序严格为 `_discoveryLock -> _peerLock` 无死锁；`Dispose()` 已隔离反订阅异常；`SweepInterfaces` 睡眠后已先短路；`AsyncWaitHandle` 已用 `using` 释放 |
| 3. 非默认 TCP 端口与多网卡/TUN 路由刷新 | PARTIAL (DEFERRED) | 在线非默认端口复核、同 IP 异端口保留与同子网物理网卡优先成立；跨子网 TUN 回落通配与 Linux 运行期无历史离线地址属 P2，已准确登记至 `CRDocs/DEFERRED.md`（`LC-FR-007`），随 `LC-FR-ENV-001` / `LC-FR-ENV-002` 一并验收 |
| 4. WinForms 分组标题鼠标/键盘/右键/滚动/选择 | PASS | `LC-FR-003`、`LC-FR-004`、`LC-FR-008` 闭环：`ClearSessionItems()` 同步清空旧索引，`OnSelectedIndexChanged` 校验旧索引并双向查找非标题会话（全标题落 `-1`）；`WndProc` 低 32 位解包有符号坐标并在溢出时将右缘 12px 滑块热区直交 `OnMouseDown`/`OnMouseUp`（保留当前选择、拦截标题正文与无溢出热区、兼容 x64 负 Y）；`RenderSessionList` 校验 `TopIndex` 边界并在恢复选中后夹紧恢复视口锚点 |
| 5. Linux Electron 刷新入口位置与分组渲染 | PASS | `LC-FR-009` 闭环：`#refresh` 位于 `.sidebar-bottom` 最右（36px），`.session-heading` 显式声明 `font-weight: 700`，`onlineHeading` 状态完整；`tests/linux_desktop_ui_regression.js` 已覆盖入口位置、字重、空/非空分组渲染、过滤、键盘越过标题与刷新占位昵称保护 |
| 6. LANC 帧、TCP 握手与旧客户端兼容 | PASS | LANC 魔数、文本帧结构、`[系统自动握手]`、TCP `0x01` 与 UDP 六段格式未破坏；对端 TCP 握手后补发既有 UDP `ACK`；旧客户端仅回 `0x01` 时单一未冲突端点仍可正常复核，且已明确记录单字节确认在无新身份公告/冲突证据时无法区分同端点换 ID 的协议边界 |
| 7. 行数门禁与中文注释/文档要求 | PASS | 全部 17 个修改/新增源码与测试文件均 `< 1000` 行软门禁；最高为 `src/linux/desktop/renderer.js`（953 行，距 1000 行预警仅余 47 行，后续新增功能前须先按 SRP 拆分）与 `tests/linux_desktop_ui_regression.js`（935 行）；新增与调整注释均为中文 |

## Round 1 问题逐项复审结论

| 问题 ID | 严重度 | 结论 | 复核依据 |
| --- | :---: | :---: | --- |
| `LC-FR-001` | P1 | **RESOLVED** | `src/NetworkService.Discovery.cs:193-275, 505-529`：带 `announcedPort` 的身份公告按 `(IP, TcpPort)` 归并同端点旧节点并迁移未读，锁外派发 `OnPeerLeft`；同 IP 异端口保留；迟到 TCP `0x01` 复核快照、`current == expected` 与端点无其他身份冲突后才续期；`tests/friend_refresh_regression.ps1` 独立复跑通过 |
| `LC-FR-002` | P1 | **RESOLVED** | `src/NetworkService.Discovery.cs:293-319`、`src/NetworkService.Interface.cs:76-113`、`src/NetworkService.cs:168-190`：所有刷新入口统一走 `RefreshPeers()` -> `RebindInterfaces()`，覆盖漏事件、延迟获取与瞬时绑定失败，不变网卡零重建复用快照；夹具独立复跑通过 |
| `LC-FR-003` | P1 | **RESOLVED** | `src/UI/Controls/SessionListBox.cs:39-44, 327-361`、`src/MainForm.Chat.cs:24`：重建清空 `_previousSelectedIndex`，跨标题前校验旧索引范围与非标题性，主方向无有效行时反向查找，仅标题时落 `-1`；缩表 `Home`/`Down`、末尾标题 `End` 与纯标题场景夹具独立复跑通过 |
| `LC-FR-004` | P1 | **RESOLVED** | `src/UI/Controls/SessionListBox.cs:87-108, 226-233, 255-311`：`unchecked((int)m.LParam.ToInt64())` 解析有符号 16 位 `x`/`y`，溢出时右缘 12px 滑块热区直交 `OnMouseDown`/`OnMouseUp` 不篡改当前选择，无溢出与正文区标题点击继续拦截；原生 Win32 消息与 x64 负 Y 坐标夹具独立复跑通过 |
| `LC-FR-005` | P2 | **RESOLVED** | `src/linux/desktop/renderer.js:57-67`：`peer.id === peer.ip && previous?.nick` 时保留运行期及归档昵称，不回退为 `"好友 (<ip>)"`；Node UI 回归独立复跑通过 |
| `LC-FR-006` | P2 | **RESOLVED** | `src/NetworkService.cs:233-239`（反订阅 `try/catch` 保护）、`src/NetworkService.Discovery.cs:426-427`（睡眠后先检停机与快照切换）、`484-488`（`using (var connectWait = ar.AsyncWaitHandle)`） |
| `LC-FR-007` | P2 | **DEFERRED** | 已在 `CRDocs/DEFERRED.md` 准确登记跨子网 TUN 回落与 Linux/切网无历史离线地址现状、触发阶段（`LC-FR-ENV-001` / `LC-FR-ENV-002`）及验收条件，不阻断当前切片 |
| `LC-FR-008` | P2 | **RESOLVED** | `src/MainForm.Chat.cs:18-40` 与 `src/UI/Controls/SessionListBox.cs:46-53`：校验 `TopIndex` 边界，在恢复 `SelectedIndex` 后通过 `RestoreScrollAnchor` 夹紧恢复视口锚点；选中群聊向下浏览后重建夹具复跑通过 |
| `LC-FR-009` | P2 | **RESOLVED** | `src/linux/desktop/desktop.css:75` 显式声明 `font-weight: 700`，`src/linux/desktop/renderer.js:98` 同步置 `onlineHeading = true`，`tests/linux_desktop_ui_regression.js` 已补齐入口位置、字重、分组、过滤、键盘跳标题与刷新昵称保留断言 |
| `LC-FR-ENV-001` | — | **DEFERRED** | 保留在 `CRDocs/DEFERRED.md`，待真实多机 LAN、切网与多网卡/TUN 路由验收 |
| `LC-FR-ENV-002` | — | **DEFERRED** | 保留在 `CRDocs/DEFERRED.md`，待 Linux/QEMU 网络命名空间实跑最新 ELF 与真实 Linux Electron 桌面验收 |

---

# LC-FRIEND-REFRESH-001 — Round 1 独立审核

- **任务编号**：`LC-FRIEND-REFRESH-001`
- **任务主题**：好友刷新延迟、网卡切换与在线/离线分组
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-24
- **审核状态**：**CHANGES_REQUESTED**
- **依据**：`.claude/Rules.md`、`CRDocs/README.md`、`CRDocs/RULES.md`、`Docs/GrokBot_UI_Design_System.md`、`CRDocs/IMPLEMENTATION.md` 顶部范围与工作区全部 14 个源码文件及完整 diff。
- **验证边界**：
  - 审核者独立复跑并确认通过：Windows 原生 `csc.exe` 零错编译（临时输出至 `%TEMP%` 后清理，未改动 `release/`）、Linux 共享后端宿主机构建 `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`（0 警告 0 错误）、`node --check src/linux/desktop/renderer.js` 与 `node tests/linux_desktop_ui_regression.js`（退出码 0，输出 `linux desktop ui regression: ok`）、全部 14 个修改文件行数统计与 `git diff --check`；并通过只读反射脚本对当前编译产物 `bin/friend-refresh/LanChat.exe` 中的 `SessionListBox` 实测复现了 `LC-FR-003` 与 `LC-FR-004`。
  - **未在真实环境验收**：未在真实多机 LAN、Wi-Fi/有线切网、物理多网卡/TUN 路由环境验收，未在 Linux/QEMU 运行最新 ELF（`tests/linux_desktop_regression.py`），亦未在 Linux Electron 桌面实机验收。宿主机编译、Windows 回环夹具与 Node DOM 模拟均不构成真实 LAN 或 Linux 实机验收。

## 结论

**CHANGES_REQUESTED（打回整改）**。LANC 封包与 TCP `0x01` 确认字节保持向后兼容，UDP 多网卡快照与锁序（`_discoveryLock -> _peerLock`）无死锁，修改文件行数全部低于 1000 行软门禁，新增注释与文档满足中文规范。但存在 **4 项 P1 阻断放行问题**：

1. 对端无 `BYE` 重启（同 IP 同端口新 `LocalId`）后刷新时，TCP 握手 `0x01` 误为已失效旧节点续命，导致幽灵节点滞留、人数翻倍、群聊重复发送且昵称/状态回退；
2. 手动刷新 `RefreshPeers()` 未触发 `RebindInterfaces()`，在换网延迟/DHCP 慢于防抖窗口、瞬时绑定失败或 `NetworkChange` 异常/漏事件后，点击刷新按钮无法恢复网卡绑定；
3. `SessionListBox.OnSelectedIndexChanged` 使用未随列表重置同步的 `_previousSelectedIndex`，搜索筛选缩小列表后按键盘方向键会直接抛出未处理 `ArgumentOutOfRangeException` 崩溃，或卡死在分组标题选中态（已实测复现）；
4. `SessionListBox.WndProc` 拦截 `WM_LBUTTONDOWN` 标题行点击时未排除右侧 12px 浮动滑块拖拽热区，导致在「在线」「离线」标题高度上无法拖拽自绘滚动条（违反 Zero White Scrollbar 交互契约，已实测复现）。

## 七维核对摘要

| 维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. UDP ACK 与 TCP 握手并发归并/昵称/在线态 | FAIL | 存在 `LC-FR-001`（P1，重启对端幽灵节点误续命）；另有 `LC-FR-005`（P2，Linux 端未阻止 TCP 地址占位名覆盖已知/归档昵称） |
| 2. 刷新计时器/重复刷新/切网重绑/线程竞态与泄漏 | FAIL | 锁序一致、无死锁、旧快照探针有隔离，但存在 `LC-FR-002`（P1，手动刷新不重绑网卡）；另有 `LC-FR-006`（P2，`Dispose` 反订阅未保护、`SweepInterfaces` 睡眠后未先短路、`AsyncWaitHandle` 未及时释放） |
| 3. 非默认 TCP 端口与多网卡/TUN 路由刷新 | PARTIAL | 在线非默认端口复核与同子网物理网卡优先成立；跨子网 + TUN 时 `GetDiscoverySocket` 回落 `0.0.0.0` 且 Linux/换网未保留运行期无历史离线地址（见 `LC-FR-007`，P2） |
| 4. WinForms 分组标题鼠标/键盘/右键/滚动/选择 | FAIL | 右键过滤与 `GetScrollMetrics` 动态行高计算正确，但存在 `LC-FR-003`（P1，搜索缩表后键盘选行崩溃/卡死标题）与 `LC-FR-004`（P1，标题高度阻断滑块拖拽）；另有 `LC-FR-008`（P2，`top <= index` 导致选中群聊向下浏览时刷新跳顶） |
| 5. Linux Electron 刷新入口位置与分组渲染 | PARTIAL | `#refresh` 移至底栏最右（36px 圆形）与 `.session-heading`（32px）符合结构与设计规范；但缺少针对新入口/分组标题的 UI 回归断言，且 `.session-heading` 字重隐式依赖弹窗 `h2` 规则（见 `LC-FR-009`，P2） |
| 6. LANC 帧、TCP 握手与旧客户端兼容 | PASS | LANC 魔数、文本帧字段、`[系统自动握手]`、`0x01` 确认字节与 UDP 6 段竖线格式完全兼容；TCP 握手后补发既有 UDP `ACK` 对旧客户端无害且可向上兼容（旧客户端仅 TCP 回包时 Linux 占位昵称覆盖见 `LC-FR-005`） |
| 7. 行数门禁与中文注释/文档要求 | PASS | 14 个修改文件全部 `< 1000` 行（最高 `src/linux/desktop/renderer.js` 945 行，距软门禁余 54 行需留意）；新增注释与 XML 文档均为中文（仅 `NetworkService.Discovery.cs` 首行带入 UTF-8 BOM，非阻断） |

## P1（本轮必须修复）

### LC-FR-001 [P1 / OPEN] 对端无 BYE 重启（同 IP 同端口新 LocalId）时，刷新 TCP 握手误为旧幽灵节点续命

- **位置**：`src/NetworkService.Discovery.cs:221-259` (`UpdatePeer`)、`350-375` (`CheckRefreshedPeers`)、`495-512` (`TryTcpHandshake`)。
- **复现条件与现象**：
  1. 对端节点 `old-guid`（如 `10.0.0.5:38889`，昵称 `OldNick`）处于在线列表；
  2. 对端崩溃或无 `BYE` 重启，以相同 IP 和默认 TCP 端口 `38889` 启动新实例 `new-guid`（昵称 `NewNick`）；
  3. 本机点击手动刷新：首轮 UDP `HELLO` 收到 `new-guid` 的 UDP `ACK`，`UpdatePeer` 只移除 `addressId`（`"10.0.0.5"`）而不清理同 `(IP, TcpPort)` 的 `old-guid`，此时 `_peers` 同时存在 `old-guid`（旧 `LastSeen`）和 `new-guid`（新 `LastSeen`）；
  4. 300ms 后 `CheckRefreshedPeers` 筛选 `LastSeen < started` 命中 `old-guid`，随即对其执行 `TryTcpHandshake(10.0.0.5, 38889, interfaces, expected: old-guid)`；
  5. 新实例 `new-guid` 在 `38889` 接受连接并返回 `0x01`（TCP 确认帧不含对端 ID），`TryTcpHandshake` 第 507-509 行校验 `expected.Id`/`IP`/`TcpPort` 通过后直接写入 `old-guid.LastSeen = DateTime.UtcNow`；
  6. `RemoveExpiredPeers(peers, started)` 见 `old-guid.LastSeen >= started` 放弃移除。
- **影响**：已死亡的 `old-guid` 被错误续命，每次刷新都会重复续命；`GetActivePeers().Count` 虚增为 2（群聊人数翻倍、群聊消息向同一好友重复发送两次）；只要 `old-guid.LastSeen` 晚于 `new-guid` 的 UDP `ACK` 时间戳（或 UDP 晚到/丢包），WinForms（`SessionPresenter.cs:158`）和 Linux（`DesktopService.cs:315` + `renderer.js:59`）会优先展示已失效的 `old-guid` 及其旧昵称 `OldNick`。
- **修复要求**：同一 IPv4 + 同一 TCP 监听端口不能同时存在两个活跃 LanChat 实例。在 `UpdatePeer` 收到带 `announcedPort.HasValue` 的真实身份（`peerId != addressId`）时，应归并/剔除同 `(IP, TcpPort)` 的其他旧 `peerId`（迁移未读并在锁外通知 `OnPeerLeft`）；同时 `TryTcpHandshake(..., expected)` 在同 `(IP, port)` 已被其他活跃 `peerId` 占用时不得为 `expected` 续命。

### LC-FR-002 [P1 / OPEN] 手动刷新 `RefreshPeers()` 未触发网卡重绑 `RebindInterfaces()`

- **位置**：`src/NetworkService.Discovery.cs:287-311` (`RefreshPeers`)、`src/NetworkService.Interface.cs:67-116` (`OnNetworkAddressChanged` / `RebindInterfaces`)、`src/NetworkService.cs:177-185`。
- **复现条件与现象**：
  - `RebindInterfaces()` 仅在 `Start()` 和 `NetworkChange.NetworkAddressChanged` 的 250ms 防抖回调中被调用，`RefreshPeers()` 直接使用缓存的 `_activeInterfaces`。
  - 当真实切网/休眠唤醒时 DHCP 获取 IPv4 晚于 250ms 防抖点、切网瞬间 `BindUdpListener` 瞬时失败导致 `adapter.UdpSocket == null`、用户在 250ms 防抖窗口内提前点击手动刷新，或 `NetworkChange` 订阅在特定环境下抛异常降级时，`_activeInterfaces` 会长期停留在失效快照上，用户反复点击“刷新好友列表”按钮也无法重新绑定新网卡套接字。
- **修复要求**：在 `RefreshPeers()` 获取 `interfaces` 前调用 `RebindInterfaces()`（该函数在网卡无变化时通过 `if (!changed) return;` 零重建直接返回，且切网通知回调可直接复用，确保手动刷新始终能兜底恢复新网卡与失败的 UDP 绑定）。

### LC-FR-003 [P1 / OPEN] `SessionListBox.OnSelectedIndexChanged` 残留越界 `_previousSelectedIndex`，搜索缩表后键盘选行直接崩溃或卡死在分组标题

- **位置**：`src/UI/Controls/SessionListBox.cs:289-299`、`src/MainForm.Chat.cs:18-39`。
- **实测复现证据**（已在 `bin/friend-refresh/LanChat.exe` 的 `LanChat.SessionListBox` 上复现）：
  1. **崩溃路径**：列表含 5 项 `[群聊(0), 在线(1), PeerA(2), 离线(3), PeerB(4)]`，选中 `PeerB`（`_previousSelectedIndex = 4`）。用户输入搜索词仅命中 `PeerA`，`RenderSessionList()` 调用 `Items.Clear()` 并重建为 2 项 `[在线(0), PeerA(1)]`；因当前会话 `PeerB` 不在过滤结果中，`index = -1`，`SelectedIndex` 为 `-1`，而 `Items.Clear()` 不会触发 `OnSelectedIndexChanged`，`_previousSelectedIndex` 仍为 `4`。焦点进入列表后按 `Down`/`Home` 落到索引 `0`（分组标题「在线」），`direction = (0 > 4) ? 1 : -1` 得 `-1`，`current` 减至 `-1`，随后执行 `SelectedIndex = _previousSelectedIndex`（即 `4`），抛出未处理异常 **`ArgumentOutOfRangeException: InvalidArgument=Value of '4' is not valid for 'SelectedIndex'`**。
  2. **标题卡死路径**：原选中 `群聊`（`_previousSelectedIndex = 0`），搜索仅命中 `PeerB` 重建为 `[离线(0), PeerB(1)]` 后按 `Down`/`Home`，`direction = (0 > 0) ? 1 : -1` 得 `-1`，回退 `SelectedIndex = 0`，结果分组标题 `GroupHeading.Offline` 保持选中态（实测 `SelectedIndex=0 Item=HEAD:Offline`），无法跳至 `PeerB`。
  3. **尾部边界**：无离线好友时列表末尾为「离线」标题，按 `End`/`PageDown` 落到末尾标题后 `direction = +1` 越界，回跳旧项而非停在最后一个有效会话。
- **修复要求**：不得将未经合法性校验的 `_previousSelectedIndex` 直接写回 `SelectedIndex`。当 `IsHeading(current)` 时：先校验 `_previousSelectedIndex` 是否处于 `[0, Items.Count)` 且非标题；主方向（若 `_previousSelectedIndex` 无效则优先向下 `+1`）找不到有效会话时，反向扫描寻找最近非标题会话；仅当整个列表无任何非标题项时设为 `-1`。列表清空重建后也应同步重置/夹紧 `_previousSelectedIndex`。

### LC-FR-004 [P1 / OPEN] `SessionListBox.WndProc` 拦截标题行左键点击时吞掉右侧浮动滑块拖拽热区

- **位置**：`src/UI/Controls/SessionListBox.cs:71-76, 214-229`。
- **实测复现证据**（已在 `bin/friend-refresh/LanChat.exe` 上向溢出列表的滑块热区 `(x=295, y=80)`（宽 300、位于第 1 项标题行 `y=72..104` 高度）发送 `WM_LBUTTONDOWN`，实测 `_isThumbDragging == False`）：
  - `WndProc` 在 `base.WndProc`（触发 `OnMouseDown`）之前拦截 `WM_LBUTTONDOWN`，并仅依据 `IsHeading(IndexFromPoint(x, y))` 直接 `return`。由于 `IndexFromPoint` 覆盖控件全宽（含最右 12px 浮动滑块区域），鼠标按住滑块与「在线」「离线」标题重合的纵坐标区段时，点击被直接吞掉，无法启动滚动条拖拽，违反 `.claude/Rules.md` 第 9 条 Zero White Scrollbar 的拖拽交互契约。
  - 另第 74 行 `m.LParam.ToInt32()` 在 64 位进程遇到负 Y 坐标时会溢出，宜改为 `unchecked((int)m.LParam.ToInt64())`。
- **修复要求**：在 `WndProc` 的 `WM_LBUTTONDOWN` 标题行拦截条件中，若列表存在溢出（`maxTop > 0`）且点击 X 落在右侧滑块热区（`x >= ClientSize.Width - 12`），应放行给 `base.WndProc` -> `OnMouseDown` 处理滑块拖拽；仅对非滑块热区的标题行点击做拦截。

## P2（建议返修一并处理或登记延期）

### LC-FR-005 [P2 / OPEN] Linux 端未阻止 TCP 地址占位记录覆盖已知/归档好友昵称

- **位置**：`src/linux/LanChat.Linux/DesktopService.cs:315-320`、`src/linux/desktop/renderer.js:57-62`。
- **说明**：`IMPLEMENTATION.md` 声明“地址占位记录不覆盖已知昵称”，Windows 端已在 `SessionPresenter` 保护，但 Linux 端 `PeerList()` 原样输出占位名 `"好友 (<ip>)"`，`renderer.js` 的 `setPeers` 会直接覆盖前端已有的归档/运行期真实昵称。在 UDP 丢包或与旧客户端（TCP 握手不回 UDP `ACK`）互通时会表现为昵称被回退成 `"好友 (<ip>)"`。建议在 `DesktopService.PeerList()` 或 `renderer.js` 中对 `peer.id === peer.ip` 的占位记录保留已知/归档昵称。

### LC-FR-006 [P2 / OPEN] `Dispose()` 反订阅 `NetworkAddressChanged` 缺异常保护，且 `SweepInterfaces` / `TryTcpHandshake` 存在轻微资源清理瑕疵

- **位置**：`src/NetworkService.cs:177-185, 235-245`、`src/NetworkService.Discovery.cs:418-425, 474-476`。
- **说明**：
  1. `Start()` 中先赋值 `_networkChangeTimer = new Timer(...)` 再订阅 `NetworkChange.NetworkAddressChanged +=`；若订阅抛异常，`_networkChangeTimer` 非空，后续 `Dispose()` 未包裹 `try/catch` 的 `-=` 若也抛异常会中断 `_presenceCheckTimer`、`BYE` 与全部套接字释放。建议仅在订阅成功后保留状态或在 `Dispose()` 中做隔离保护。
  2. `SweepInterfaces` 在 `Thread.Sleep(300)` 后应先检查 `if (!_isRunning || interfaces != _activeInterfaces) return;`，避免停机/切网后仍启动最多 32 个空转线程。
  3. `TryTcpHandshake` 建议用 `using (ar.AsyncWaitHandle)` 及时释放等待句柄。

### LC-FR-007 [P2 / OPEN] 跨子网 + TUN 场景下 UDP 探测回落 `0.0.0.0`，且运行期无历史离线好友未被纳入刷新单播

- **位置**：`src/NetworkService.Discovery.cs:138-153, 298-300`、`src/linux/LanChat.Linux/DesktopService.cs:312-313`。
- **说明**：
  1. `GetDiscoverySocket` 在目标不在任一本地子网时直接回落 `_udpAnySocket`（`0.0.0.0`），与 TCP 侧 `GetLocalEndpointForTarget`（优先回落首选物理网卡以穿透 Metric 0 TUN）不一致，跨网段路由局域网开启全局 TUN 时 UDP 单播可能被 TUN 劫持。
  2. Linux `DesktopService.KnownAddresses()` 仅读取有聊天记录的归档会话键，不包含运行期见过但未发消息的离线好友（与 Windows `SessionPresenter.GetKnownAddresses()` 不对称）；且 `NetworkService` 在切网 `RefreshPeers(null)` 时也不包含上次显式刷新之后才发现的新好友 IP。

### LC-FR-008 [P2 / OPEN] `RenderSessionList()` 的 `top <= index` 限制导致选中群聊向下浏览离线好友时刷新跳回顶部

- **位置**：`src/MainForm.Chat.cs:18, 37`。
- **说明**：选中顶部会话（如索引 `0` 的群聊）并向下滚动浏览离线好友时，若点击刷新或收到后台 `OnPeerUpdated`，重设 `SelectedIndex = 0` 会将列表滚回顶部，而第 37 行 `top <= index` 阻止恢复原 `TopIndex`，造成视口跳回顶部。另第 18 行读取 `_listSessions.Items[_listSessions.TopIndex]` 建议补齐 `TopIndex >= 0 && TopIndex < Items.Count` 边界保护。

### LC-FR-009 [P2 / OPEN] Linux 分组标题与刷新按钮缺少自动化回归用例，且 `.session-heading` 字重隐式耦合弹窗 `h2`

- **位置**：`src/linux/desktop/desktop.css:75, 167`、`src/linux/desktop/renderer.js:70, 91`、`tests/linux_desktop_ui_regression.js`。
- **说明**：
  1. `desktop.css:75` 的 `.session-heading` 应显式声明 `font-weight: 700`，避免隐式依赖文件末尾第 167 行面向弹窗的 `h2` 样式；
  2. `renderer.js:91` 追加「在线」标题后建议同步置 `onlineHeading = true` 保持状态完整；
  3. `tests/linux_desktop_ui_regression.js` 建议补充对 `#refresh` 位于 `.sidebar-bottom`、无过滤/有过滤时「在线」「离线」分组标题渲染、以及键盘上下键跳过标题的断言；
  4. `src/linux/desktop/renderer.js` 当前 945 行，距 1000 行软门禁仅余 54 行，后续再向该文件追加新功能前须按 `.claude/Rules.md` 第 10 条先拆分。

## 真实环境验收项（已迁入 `CRDocs/DEFERRED.md`）

| 问题 ID | 状态 | 说明 |
| --- | :---: | --- |
| `LC-FR-ENV-001` | DEFERRED | 真实多机 LAN、Wi-Fi/有线切网、非默认端口与物理多网卡/TUN 路由下的刷新延迟、重绑与旧版互通实测 |
| `LC-FR-ENV-002` | DEFERRED | Linux/QEMU 网络命名空间实跑最新 ELF（`tests/linux_desktop_regression.py`）及 Linux Electron 实机侧栏刷新按钮与分组渲染验收 |

---

# LC-LINUX-002-STEP-3-SLICE-2 — Round 2 复审归档

> 审核者：Grok（经用户确认审核结论已通过，由执行者按规范将状态与延期项分离归档）
> 日期：2026-11-23
> 结论：**APPROVED**。本轮 P1 已全部闭环，可延期项与跨平台实机验收项已迁入 `CRDocs/DEFERRED.md`。

| 问题 ID | 严重度 | 状态 | 结论 |
| --- | --- | --- | --- |
| LC-S3-001 | P1 | RESOLVED | 外观四项已按 WinForms `AppSettings` 键名落盘，即时作用于主窗与设置窗并重启保持；通用页与外观页字号联动；10 个展示项继续只展示、无新 RPC 且后台驻留保持空回调 |
| LC-S3-002 | P1 | RESOLVED | `histories-cleared` 已作废进行中的 `historyLoads`，晚到的成功或失败 `history` 响应不再回写已清空气泡或覆盖状态 |
| LC-S3-003 | P2 | DEFERRED | `ChatHistoryService.ClearAllHistories()` 删文件异常被吃掉（与 WinForms 一致），暂不阻断本切片 |
| LC-LINUX-002-ENV-001 | — | DEFERRED | Linux 网络命名空间对最新 ELF 实跑 `tests/linux_desktop_regression.py` 待在 Linux/QEMU 执行 |
| LC-LINUX-002-GUI-001 | — | DEFERRED | Xorg / Wayland / XWayland 实机 Electron 联调、输入法与缩放叠图待在 Linux 桌面环境执行 |

---

# LC-LINUX-002-STEP-3-SLICE-2 — Round 1 独立审核（历史保留）

- **任务编号**：`LC-LINUX-002-STEP-3-SLICE-2`
- **任务主题**：Linux Electron 桌面设置八页对齐
- **审核方**：Grok 独立审核方
- **审核日期**：2026-11-23
- **审核状态**：**CHANGES_REQUESTED**（原记录为 **REJECTED**）
- **依据**：`.claude/Rules.md` 第 1、4、6、7、8、9、10 条；`PlanDocs/LC-LINUX-002/BASELINE.md`、`INTEGRATION.md`；`Docs/GrokBot_UI_Design_System.md`；`src/UI/Controls/SettingsPanels/` 下 8 个面板。
- **验证边界**：审核对照的是工作区源码。Node UI 回归、Python 语法检查、双架构 ELF 编译和 Edge Headless 1260×820 测量按送审材料采信为开发者自测，本次审核没有重跑。这些证据不能代替 X11/Wayland，也不能代替 Linux 网络命名空间里的 `delete`、`history`、`clear-all-histories`。

## 结论

不放行。设置窗拆分、IPC 白名单、昵称/自动接收/目录/只读备用确认，以及未跑 Linux 的证据边界，这几项成立。外观页被写成 Windows 未接线，与 WinForms 源码和基线不符。清空记录与在途历史加载有竞态。

## P1

### LC-S3-001 [P1 / OPEN -> Round 2 RESOLVED] 外观四项在 Windows 已接线，Linux 只改本窗内存

`AppearanceSettingsPanel.ApplySettings()` 会 `AppSettings.Save()` 并调用 `OnAppearanceChanged()`。主题、强调色、界面/聊天字体族、两套字号会立即生效，重启仍在。通用页字号滑块同样写入 `AppSettings.UiFontSize`。`BASELINE.md` 对 `settings-3` 的验收是「即时变化与重启保持」。

Linux 的 `display.themeIndex`、`accentIndex`、字体下标和 `uiSize`/`chatSize` 只活在这次设置窗进程里。滑块和下拉能拨动，不写配置，不改主窗，关掉再开回到写死默认值。`settings.js` 与 `INTEGRATION.md` 把这些和语言、链接打开方式放在一类，与 `AppearanceSettingsPanel.cs` 不符。

语言、链接打开方式、协议通道、快捷键徽章、托盘预览、后台驻留、分块校验、自动加载、TUN 过滤、单播探测，在 Windows 是空回调或只改按钮文字。这些继续保持展示态，不要新造 RPC。

### LC-S3-002 [P1 / OPEN -> Round 2 RESOLVED] 清空之后，在途历史响应能把气泡填回去

`DesktopService` 的 `clear-all-histories` 会 `_sessions.ClearAllHistories()` 并广播 `histories-cleared`，与服务端一致。主窗处理只做 `messages.clear()`，没有作废 `historyLoads`。`loadHistory()` 在 `await api.history()` 返回后，只要操作令牌还在，就会 `messages.set` 并重画。清空发生在这次往返中间时，界面会先空再出现旧记录。

## P2（Round 1 登记，已于 Round 2 修复或转入 DEFERRED）

- 反馈框 `<textarea>` 和「附带诊断」原生复选框没有接 `scrollbar.js`，也没有关掉系统滚动条。长文本会露出原生滑块。
- `computeSettingsBounds` 最小可到 480×400，窗口 `minWidth/minHeight` 是 720×500，且使用 `useContentSize`。小工作区上，夹紧结果会被最小尺寸和标题栏顶破。
- `tests/linux_desktop_ui_regression.js` 对只读备用确认只检查源码是否包含 `overridePath` 和「使用备用位置」，没有执行确认/取消分支。
- 送审稿与代码不一致：昵称上限是 1～80，与 `LinuxProfile` 一致，不是 1～32；检查更新文案是「将在后续维护切片接入」，不是「当前已是最新版本」。
- 快捷键页只写 `Shift + Enter` 换行。主窗实际是 Enter 发送，Ctrl+Enter 和 Shift+Enter 都能换行。
- 选了与当前相同的历史目录时，配置层会跳过复制，界面仍提示「聊天记录已迁移并保存设置」。`ChatHistoryService.ClearAllHistories()` 删除失败会被吃掉，界面仍显示成功；这点和 WinForms 相同。
