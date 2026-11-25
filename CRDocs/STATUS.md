# 当前任务 — LC-FILE-TRANSFER-STABILITY-001

- **状态**：`APPROVED`（`4e8bbc4` 重复接收气泡修复在 Windows 宿主机回归与 Linux `net10.0` 构建范围放行）
- **主题**：文件传输稳定性、并发限流与接收气泡去重
- **日期**：2026-11-25；**执行者**：Codex（`4e8bbc4`）；**独立审核者**：Gemini
- **报告**：[本轮独立审核](REVIEW.md#lc-file-transfer-stability-001--重复接收气泡修复独立审核commit-4e8bbc4) · [本轮执行记录](REVIEW.md#lc-file-transfer-stability-001--本轮重复接收气泡修复执行记录)
- **一句话结论**：`FileTransferController.RemovePendingProgress` 在 `HandleFileReceived` 接管前按 `FileTransferId` 撤销未刷新的 50ms 延迟进度，彻底消除快速接收时的重复气泡竞态；普通顺序仍复用同一气泡且不误删其他传输，LANC/JSONL 零破坏，全部 6 项宿主机/构建/空白检查独立复跑通过；`v2.0.22` 已发布资产未包含本轮修复。
- **下一步**：按用户指示执行 `v2.0.23` 双端构建与 GitHub Release；明确 `v2.0.22` 资产不包含 `4e8bbc4`。Linux 实机/ELF、Linux Xorg/Wayland/XWayland、真实双机 LAN 与 Windows 真实桌面截图工具/Excel/WPS 粘贴仍按 [DEFERRED.md](DEFERRED.md) 保留现场验收。

---

## 当前任务 — Windows 稳定性、外观与群聊文件传输收口

- **状态** `APPROVED`（当前开发与 Windows 宿主机回归范围放行）。
- **日期** 2026-11-25；**执行者** Codex（`e3e9a58`、`a056460`）；**独立审核方** 独立复审。
- **本轮范围**：左上角头像缩放防闪烁、附件图 11 色外观、文件气泡发送/接收网速、群聊在线成员多选发送与托管截图副本清理，以及 `LC-RV-001` 无在线好友空列表返修。
- **一句话结论**：`LC-RV-001` 已复核关闭（`PickFileRecipients` 全路径返回非空 `List<PeerInfo>`，`NormalizeRecipientList` 归一化 `null` 快照，0 在线好友与 `null` 快照断言通过）；五项宿主机/脚本/后端回归命令全部通过，头像双缓冲、11 色配色、双向速率与 LANC/JSONL 兼容均成立，全量改动文件 `< 1000` 行。
- **未验证**：真实 Windows 桌面拖拽缩放录屏/截图工具/Excel/WPS、Linux Xorg/Wayland/XWayland 与最新 ELF、真实双机局域网联测，继续见 [DEFERRED.md](DEFERRED.md)。
- **发布结果**：v2.0.21 已完成 Windows/Linux 打包并上传 GitHub Release（`f09226b`）。更新日志已写入 `CHANGELOG.md`。
- **下一步**：当前开发、审核与发布范围已闭环；真实桌面、Linux 实机/ELF 与双机联测仍按 [DEFERRED.md](DEFERRED.md) 等待现场验收。 [审核结论](REVIEW.md) · [实施与验证](IMPLEMENTATION.md) · [实机边界](DEFERRED.md)

---

## 当前任务 — LC-CC-005 / LC-CC-006（Windows 输入区与选区/草稿用例）

- **状态** `APPROVED`（当前开发与 Windows 宿主机回归范围放行）。
- **日期** 2026-11-25。
- **执行者** Codex；**独立审核方** 独立复审。
- **结论** `ChatInputBox` 图文混合剪贴板优先保留非空文本，隐藏输入框首次右键弹出暗色 `GrokPopupMenu` 并保留粘贴/粘贴附件/复制/剪切/全选；`LC-CC-006` 组合表情逐像素命中边界、跨 CRLF 复制、切会话草稿隔离/恢复与关闭清理断言通过；LANC/JSONL 零破坏，全量文件 `< 1000` 行。
- **验证边界** 独立复跑 `chat_composer_regression.ps1 -ExpectLocalPack`、`friend_refresh_regression.ps1`（含原生编译）、Node 15 文件 `--check` 与 `tests/linux_desktop_ui_regression.js`、`dotnet build` 共享后端及 `git diff --check` 均通过。真实 Windows 截图工具/Excel/WPS 桌面、Linux Xorg/Wayland/XWayland、最新 ELF 与真实双机联测仍待验证，保留于 `CRDocs/DEFERRED.md`。
- **下一步** `LC-CC-005` / `LC-CC-006` 当前范围已闭环。由用户决定进入提交/打包发布，或安排 `CRDocs/DEFERRED.md` 中的真实桌面、Linux 实机/ELF 与双机联测验收（`LC-CC-ENV-001`、`LC-LINUX-002-ENV-001`、`LC-LINUX-002-GUI-001`）。 [审核结论](REVIEW.md) · [实施与验证](IMPLEMENTATION.md) · [实机边界](DEFERRED.md)

---

## 当前任务 — LC-CC-004（截图发送失败与退出清理）

- **状态** `APPROVED`（当前开发与 Windows 宿主机回归范围放行）。
- **日期** 2026-11-25。
- **执行者** Codex；**独立审核方** 独立复审。
- **结论** Windows 截图所有权移交、`lock (_outgoingPastes)` 退出/历史同步结算与 `FileShare.Delete` 成立；Linux `send-paste`/`before-quit` 交叠、管道晚到确认及后端异常终止保留逻辑成立；用户原文件与历史引用受保护，LANC/JSONL 零破坏，全量改动文件 `< 1000` 行。
- **验证边界** 独立复跑 Windows 两套回归、`ConcurrentReceive.cs`、Node 15 文件 `--check` 与 `tests/linux_desktop_ui_regression.js`、`dotnet build` 共享后端及 `git diff --check` 均通过（宿主机无 `pwsh`，未直接执行 `bin/lc-cc-004-backend/verify.ps1`）。Linux Electron 实机、最新 ELF（`tests/linux_desktop_regression.py`）与真实双机联测仍待验证，保留于 `CRDocs/DEFERRED.md`。
- **下一步** `LC-CC-004` 当前范围已闭环。由用户决定进入提交/打包发布，或安排 `CRDocs/DEFERRED.md` 中的 `LC-CC-005`/`LC-CC-006` 剩余用例及 Linux/双机实机验收（`LC-CC-ENV-001`、`LC-LINUX-002-ENV-001`、`LC-LINUX-002-GUI-001`）。 [审核结论](REVIEW.md) · [实施与验证](IMPLEMENTATION.md) · [实机边界](DEFERRED.md)

---

## 历史状态 — LC-CC-003（Linux 侧栏摘要与表情回显）

- **状态** `APPROVED`（当前开发与宿主机回归范围放行）。
- **日期** 2026-11-25。
- **执行者** Codex；**独立审核方** Gemini（独立复审）。
- **结论** `renderer.js` 已拆分为主控制器（690 行）、`session-sidebar.js`（146 行）与 `conversation-view.js`（194 行），全量修改文件 `< 1000` 行；`presentation.js` 按 `Intl.Segmenter` 字素与表情令牌截断；`DesktopService.cs` 启动 `state` 完整透传归档摘要；侧栏已导入表情可显图，缺图与坏图均回退可读 `[名称]`；LANC 协议与 Windows 行为零破坏。
- **验证边界** 独立复跑 Node 15 文件 `--check` 与 `tests/linux_desktop_ui_regression.js`、两套 Windows 回归、`dotnet build` 共享后端、`git diff --check`，并独立以编译产物 `lanchat.dll` 验证隔离 JSONL 归档 `state` 响应及 Edge Headless 验证真实 PNG 解码、`error` 回退与 420px/280px 窄侧栏单行样式，全部通过。**未验证** Linux Xorg/Wayland/XWayland Electron 实机窄侧栏与系统剪贴板、Linux/QEMU 最新 ELF 实跑（`tests/linux_desktop_regression.py`）及真实双机新旧端联测，继续保留于 `CRDocs/DEFERRED.md`。
- **下一步** `LC-CC-003` 当前范围已闭环。由用户决定进入提交/打包发布，或安排下一项专项（如 `CRDocs/DEFERRED.md` 中的 `LC-CC-004`、`LC-CC-005`、`LC-CC-006` 剩余用例，或 Linux/双机实机验收 `LC-CC-ENV-001` / `LC-LINUX-002-ENV-001` / `LC-LINUX-002-GUI-001`）。 [复审结论](REVIEW.md) · [实施证据](IMPLEMENTATION.md) · [实机边界](DEFERRED.md)

---

## 历史状态 — LC-CHAT-COMPOSER-001（Round 2：独立复审通过）

- **状态**：**APPROVED**（当前开发与宿主机回归范围放行）。
- **日期**：2026-11-24。
- **执行者**：Codex（Round 2 整改）；**独立审核方**：Gemini（Round 2 独立复审）。
- **范围**：Windows WinForms 与 Linux Electron 的微信静态表情导入与回显、Unicode 组合表情排版与划选复制（含 `LC-CC-001` 整段 BMP Emoji 排版）、在线/离线分组 8px 间距与 6px 圆点、截图粘贴草稿与发送（含 `LC-CC-002` Linux PNG/JPEG 直传与空图回退）。
- **一句话结论**：Round 1 全部 2 项 P1（`LC-CC-001`、`LC-CC-002`）已复核 `RESOLVED`，Node IPC 回归已拆至 `tests/linux_desktop_ipc_regression.js`（220 行），全量 20 个改动/新增文件 `< 1000` 行，LANC 旧版兼容零破坏，其余 4 项 P2（`LC-CC-003` ~ `LC-CC-006`）与跨环境项（`LC-CC-ENV-001`）已登记至 `CRDocs/DEFERRED.md`。
- **验证边界**：独立复跑 `tests/friend_refresh_regression.ps1`、`tests/chat_composer_regression.ps1 -ExpectLocalPack`、`C:\Program Files\nodejs\node.exe`（7 文件 `--check` + `tests/linux_desktop_ui_regression.js`）、`py_compile`、`dotnet build` 共享后端及 `git diff --check` 全部退出码 0。**未验证**真实双机新旧版互通、Windows 真实截图工具键盘链路、Linux/QEMU 最新 ELF 实跑（`tests/linux_desktop_regression.py`）与 Linux Xorg/Wayland/XWayland Electron 实机剪贴板及界面（见 `CRDocs/DEFERRED.md` `LC-CC-ENV-001`）。
- **记录**：[Round 2 独立复审](REVIEW.md) · [Round 2 实施详情](IMPLEMENTATION.md) · [延期及跨环境验收](DEFERRED.md#lc-cc-env-001-open-剪贴板与新旧客户端的桌面验收)。
- **下一步**：`LC-CHAT-COMPOSER-001` 当前开发与宿主机回归范围已闭环；由用户决定进入提交/打包发布，或安排 `CRDocs/DEFERRED.md` 中的真实双机与 Linux 实机验收（`LC-CC-ENV-001`）及后续 P2 专项（`LC-CC-003` ~ `LC-CC-006`）。

---

## 历史状态 — LC-CHAT-COMPOSER-001（Round 1：独立审核打回）

- **状态**：**REJECTED**（按 `CRDocs/RULES.md` 打回整改；对应 `CRDocs/README.md` 中的 `CHANGES_REQUESTED`）。
- **日期**：2026-11-24。
- **执行者**：Codex；**独立审核方**：Gemini。
- **范围**：Windows WinForms 与 Linux Electron 的微信静态表情导入与回显、Unicode 组合表情排版与划选复制、在线/离线分组 8px 间距与 6px 圆点、截图粘贴草稿与发送。
- **一句话结论**：微信 4.2.0.13 本机 109 表情导入、无资源可读回退、LANC 旧版兼容与双端分组间距圆点均成立，但存在 2 项 P1 阻断（`LC-CC-001` Windows `ChatTextLayout.PaintText` 拆分 BMP 基底组合表情、`LC-CC-002` Linux 剪贴板非 PNG/JPEG 图像直接交 `nativeImage.createFromBuffer` 必败并屏蔽 `image/png` 回退）及 4 项 P2，打回整改。
- **验证边界**：Windows 宿主机回归（`friend_refresh_regression.ps1`、`chat_composer_regression.ps1 -ExpectLocalPack`、`node --check` + `linux_desktop_ui_regression.js`、`py_compile`、`dotnet build` 共享后端及只读反射检查）全部独立复跑并取得实证；**未验证**真实双机新旧版互通、真实 Windows 桌面截图工具键盘链路、Linux ELF 实跑与 Linux Xorg/Wayland Electron 桌面实机（见 `CRDocs/DEFERRED.md` `LC-CC-ENV-001`）。
- **记录**：[Round 1 独立审核](REVIEW.md) · [Round 1 实现细节](IMPLEMENTATION.md) · [跨环境验收](DEFERRED.md#lc-cc-env-001-open-剪贴板与新旧客户端的桌面验收)。
- **下一步**：Codex 按 `CRDocs/REVIEW.md` 修复 `LC-CC-001` 与 `LC-CC-002`（建议评估顺手关闭 `LC-CC-003`、`LC-CC-004` 并注意 `renderer.js` / `linux_desktop_ui_regression.js` 1000 行预警线），补充针对性自动化断言并复跑三套回归后，更新 `CRDocs/IMPLEMENTATION.md`（追加 Round 2）、按 `CRDocs/RULES.md` 更新 `CRDocs/REVIEW.md` 与 `CRDocs/STATUS.md` 为 `WAITING_FOR_GROK` 交回复审。

---

## 当前任务 — LC-FRIEND-REFRESH-001（Round 2：独立复审通过）

- **Task ID**：`LC-FRIEND-REFRESH-001`
- **Round**：2
- **Status**：**APPROVED**
- **Executor**：Codex（Round 2 返修）
- **Reviewer / Last updated by**：Gemini（独立复审）；2026-11-24
- **Current owner**：用户（进入真实 LAN / Linux 实机验收或提交收口）
- **Scope**：Round 1 四项 P1（`LC-FR-001` ~ `LC-FR-004`）与四项 P2（`LC-FR-005`、`LC-FR-006`、`LC-FR-008`、`LC-FR-009`）已全部复核 `RESOLVED`；`LC-FR-007` 与运行环境验收项（`LC-FR-ENV-001`、`LC-FR-ENV-002`）已登记至 `CRDocs/DEFERRED.md`。
- **审核入口**：Round 2 独立复审结论见 `CRDocs/REVIEW.md` 顶部；实施记录见 `CRDocs/IMPLEMENTATION.md` 顶部。
- **验证边界**：独立复跑 `tests/friend_refresh_regression.ps1`、`tests/ConcurrentReceive.cs`、Linux 后端 Windows 宿主机构建、`node --check` 与 `tests/linux_desktop_ui_regression.js` 均退出码 0；全量 17 个改动/新增源码与测试文件 `< 1000` 行（`src/linux/desktop/renderer.js` 953 行，下次新增功能前须先拆分）。**未验证**真实多机 LAN、Wi-Fi/有线切网、物理多网卡/TUN、公开旧版跨机互通、Linux/QEMU 最新 ELF 实跑（`tests/linux_desktop_regression.py`）与 Linux Electron 实机界面。
- **下一步**：`LC-FRIEND-REFRESH-001` 当前开发与宿主机回归范围已闭环；由用户决定提交收口，或安排 `CRDocs/DEFERRED.md` 中的真实 LAN / Linux 实机验收（`LC-FR-ENV-001`、`LC-FR-ENV-002`、`LC-FR-ENV-007`）。

---

## Round 1 审核状态（历史） — LC-FRIEND-REFRESH-001

- **Task ID**：`LC-FRIEND-REFRESH-001`
- **Round**：1
- **Status**：**CHANGES_REQUESTED**
- **Executor**：Codex
- **Reviewer / Last updated by**：Gemini（独立审核）；2026-11-24
- **Current owner**：Codex（返修）
- **Scope**：共享好友发现与网卡重绑、Windows 好友列表刷新按钮及在线/离线分组、Linux Electron 刷新入口位置和在线/离线分组。LANC 帧与字段兼容。
- **审核依据**：`.claude/Rules.md`、`CRDocs/README.md`、`CRDocs/RULES.md`、`Docs/GrokBot_UI_Design_System.md`、工作区实际代码与完整 diff。
- **审核入口**：Round 1 独立审核结论见 `CRDocs/REVIEW.md` 顶部；实施记录见 `CRDocs/IMPLEMENTATION.md` 顶部；真实 LAN / Linux 实机验收项已登记至 `CRDocs/DEFERRED.md`。
- **阻塞结论（4 项 P1 OPEN）**：
  1. `LC-FR-001`：对端无 `BYE` 重启（同 IP 同端口新 `LocalId`）后刷新，TCP 握手 `0x01` 误给旧幽灵节点续命，导致重复节点、群聊重发与昵称回退；
  2. `LC-FR-002`：手动刷新 `RefreshPeers()` 未调用 `RebindInterfaces()`，网卡事件延迟/遗漏或瞬时绑定失败后无法靠手动刷新恢复；
  3. `LC-FR-003`：`SessionListBox.OnSelectedIndexChanged` 残留未重置的 `_previousSelectedIndex`，搜索缩表后按方向键触发 `ArgumentOutOfRangeException` 崩溃或选中分组标题卡死（已实测复现）；
  4. `LC-FR-004`：`SessionListBox.WndProc` 拦截 `WM_LBUTTONDOWN` 标题行时未排除右侧 12px 浮动滑块热区，标题高度上无法拖拽滚动条（已实测复现）。
  另登记 5 项 P2（`LC-FR-005` ~ `LC-FR-009`）。
- **验证边界**：独立复跑 Windows `csc.exe` 编译、Linux `dotnet build`、Node 语法与 UI 模拟回归均通过，并用只读反射脚本复现 `LC-FR-003`/`LC-FR-004`；真实多机 LAN、Wi-Fi/有线切网、TUN 路由、Linux ELF 实跑及 Electron 桌面实机未验证。
- **下一步**：Codex 按 `CRDocs/REVIEW.md` 修复全部 4 项 P1（建议一并处理顺手可修的 P2 如 `LC-FR-005`/`LC-FR-006`/`LC-FR-008`/`LC-FR-009`），补充针对性自动化/隔离回归验证，将 Round 2 记录追加到 `CRDocs/IMPLEMENTATION.md` 顶部并把本状态改回 `READY_FOR_REVIEW` 后交回审核。

---

## 当前任务 — LC-LINUX-002-STEP-3-SLICE-2（Round 2：设置八页整改复审）

- **Task ID**：`LC-LINUX-002-STEP-3-SLICE-2`
- **Round**：2
- **Status**：**APPROVED**
- **Executor**：Gemini
- **Reviewer / Last updated by**：Grok（独立审核方已确认通过，由执行者按用户指示同步归档并升级 CRDocs）；2026-11-23
- **Current owner**：用户
- **Scope**：独立设置模态窗八页（`settings.html`、`settings.css`、`settings-model.js`、`settings-pages.js`、`settings.js`、`appearance.js`、`main.cjs`、`renderer.js`、`LinuxProfile.cs`、`DesktopService.cs`）。不含托盘恢复、系统通知、维护入口，不改 LANC 封包。
- **需求来源**：`PlanDocs/LC-LINUX-002/BASELINE.md`、`INTEGRATION.md`、`src/Services/AppSettings.cs`、`src/UI/Controls/GrokTheme.cs`、`src/UI/Controls/SettingsPanels/` 与 Round 1 `REVIEW.md`。
- **代码基线**：基于 Round 1 返修后的未提交工作区。
- **审核入口**：`CRDocs/REVIEW.md` 顶部 Round 2；交付记录见 `CRDocs/IMPLEMENTATION.md` 顶部 Round 2。

`LC-S3-001`（外观四项持久化与即时/重启生效）、`LC-S3-002`（`histories-cleared` 作废在途 `historyLoads`）均已闭环。P2 项中，反馈框浮动滑块、小工作区窗口尺寸夹紧、只读备用确认真实分支回归、快捷键页 `Ctrl + Enter / Shift + Enter` 对齐与同目录不提示已迁移已在本轮处理；`LC-S3-003`（`ClearAllHistories` 吞异常，与 WinForms 一致）及 Linux/ELF 实跑、Xorg/Wayland 桌面联调、后续切片 3 已写入 `CRDocs/DEFERRED.md`。

宿主机 Node UI 回归、Python 语法检查、Edge Headless 静态 DOM 与双架构 ELF 编译均通过；未在 Linux/QEMU 上运行最新 ELF 与真实桌面环境，不宣称实机已通过。

---

## Round 1 首轮审核状态（历史） — LC-LINUX-002-STEP-3-SLICE-2

- **Task ID**：`LC-LINUX-002-STEP-3-SLICE-2`
- **Round**：1（独立审核）
- **Status**：**CHANGES_REQUESTED**（原记录为 `REJECTED`）
- **Executor**：前序执行者
- **Reviewer / Last updated by**：Grok；2026-11-23
- **Current owner**：执行者（返修）
- **Scope**：Linux Electron 桌面设置八页初版拆分、IPC 白名单、昵称/自动接收/目录迁移/只读备用确认与 `clear-all-histories`。

2 项 P1 OPEN：`LC-S3-001` 外观四项只在设置窗内存生效且被写成 Windows 未接线；`LC-S3-002` 清空后在途 `historyLoads` 响应可能把旧气泡填回。P2 含反馈框原生滚动条、小工作区 `minWidth/minHeight` 顶破 `computeSettingsBounds`、只读备用确认缺少真实分支执行测试、快捷键页缺少 `Ctrl + Enter`、相同历史目录仍提示已迁移。


