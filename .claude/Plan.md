# Plan.md — 当前活跃计划

> 最近刷新：2026-11-05。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（已发布 Tag `v2.0.13`，当前版本 `v2.0.13`）。
> 核心任务：**DIR-005 全客户端代码结构解耦与上帝类重构工程方案（实施 Rules.md 第 10 条：治理超限大文件、拆解 GrokSettingsDialog 与 MainForm，实现 UI 与核心/数据层分离）已全部完成**。
> **成果概述**：
> 严格落实 Rules.md 第 10 条（单文件 1000/1500 行硬上限与防上帝类契约），原超限 1914 行的 `GrokSettingsDialog.cs` 拆解为 8 大独立子面板并瘦身为 384 行导航容器；原 3086 行紧耦合的 `MainForm*.cs` 上帝类通过在 `src/Controllers/` 抽离轻量 MVP 业务控制器层（`SessionPresenter`、`ChatPresenter`、`FileTransferController`）与分离 `MainForm.Designer.cs`，全部收敛至 200~685 行安全区间。经 `Tools/Compile.bat`（C# 5.0 / .NET Framework 4.0）与 `dotnet build` 原生编译双校验全绿通过（0 警告、0 错误）。

---

## 仍未完成 / 活跃计划项

1. **DIR-005 全客户端代码结构解耦与上帝类重构 (feature/wechat-ui)**：
   - [x] **第 1 步 (P0-SettingsDialogDecouple，设置中心组件化拆解)**（已完成，2026-11-05）：
     - 更新 `Tools/Compile.bat`、`Tools/CompileAndLink.bat`、`Tools/Publish.bat` 追加 `src\UI\Controls\SettingsPanels\*.cs` 编译通配符；
     - 提取公共 `DoubleBufferedPanel` 至 `src/UI/Controls/DoubleBufferedPanel.cs`；
     - 建立 `src/UI/Controls/SettingsPanels/SettingsPanelBase.cs` 基类与 `SettingsContext.cs` 上下文 DTO；
     - 依源码真实 8 个 Tab 抽离独立面板：`GeneralSettingsPanel.cs`、`ProfileSettingsPanel.cs`、`AppearanceSettingsPanel.cs`、`ShortcutsSettingsPanel.cs`、`FileTransferSettingsPanel.cs`、`ChatHistorySettingsPanel.cs`、`NetworkDeviceSettingsPanel.cs`、`AboutSettingsPanel.cs`；
     - 重构 `GrokSettingsDialog.cs` 为纯导航容器（代码由 1914 行压降至 384 行，严格收敛至 400 行以内）；
     - 执行 `Tools/Compile.bat` 原生纯编译验证全绿通过。
   - [x] **第 2 步 (P0-MainFormBusinessDecouple，主窗体业务控制器解耦)**（已完成，2026-11-05）：
     - 更新 `Tools/Compile*.bat` 脚本追加 `src\Controllers\*.cs` 编译通配符；
     - 抽离 `FileTransferController.cs`（注入 `ISynchronizeInvoke`，接管文件传输业务流、分块进度、落盘校验与系统外开，206 行）；
     - 抽离 `SessionPresenter.cs` 与 `ChatPresenter.cs`（注入 `ISynchronizeInvoke`，接管全员群聊、节点搜索过滤、在线状态维护及文本消息收发路由，分别为 300 行与 259 行）；
     - 提取 `FormResizeMessageFilter.cs`（无边框窗口原生拖拽缩放，82 行）；
     - 分离 `MainForm.Designer.cs`（控件树构建与响应式布局，685 行）；
     - 瘦身 `MainForm.cs`（457 行）、`MainForm.Chat.cs`（606 行）、`MainForm.Files.cs`（202 行）、`MainForm.Tray.cs`（671 行），全部分部类严格受控在安全区间；
     - 执行 `Tools/Compile.bat` 原生纯编译验证全绿（0 警告、0 错误）。

2. **方案 B（冻结）**：56px 微信式纯黑极窄导航栏暂不实施。

---

## 最近已完成项（归档）

- [x] **DIR-005 全客户端代码结构解耦与上帝类重构**（2026-11-05）：彻底清除全工程中 `GrokSettingsDialog`（1914 行）与 `MainForm`（3086 行紧耦合）的超限与上帝类隐患，全面落实 Rules.md 第 10 条。
- [x] **DIR-004 全客户端 UI 统一样式系统重构**（2026-11-05）：扩充 `GrokTheme.cs` 设计令牌，实现自绘 `GrokButton` 与 `GrokModalFormBase` 模态窗基类，完成全量 7 个模态窗与视口视图样式收拢，CRDocs 审核通过 (`APPROVED`)。

---

## 已确认仍有效的关键契约

- 架构契约：严格执行 `.claude/Rules.md` 第 10 条（单文件 1000 行预警、1500 行硬上限；严禁滥用 `partial class` 伪拆分；严禁新建垃圾桶类；低侵入 MVP/Controller 真解耦）。
- 端口与通信：UDP 38888，TCP 38889+；魔数 `LANC`；零第三方依赖；C# 5.0 / .NET Framework 4.0（`Tools/Compile.bat` csc）。
- 视觉规范：100% 遵守 `Docs/GrokBot_UI_Design_System.md` 与 `Rules.md` 第 9 条（Zero White Scrollbar，全应用严禁出现系统生硬白底滚动条）。
- 打包与发布规范：WiX Package 省略硬编码 ProductCode，依靠 UpgradeCode `{7E993262-EAA5-4A37-975D-39F24FEA21D1}` 实现全自动 MajorUpgrade；发布前必须自动 bump 版本号并全量编译。
