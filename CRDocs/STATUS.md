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
