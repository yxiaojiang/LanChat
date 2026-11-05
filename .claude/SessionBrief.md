# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-11-05。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（已发布 Tag `v2.0.13`，当前版本 `v2.0.13`）。
> 核心任务：**DIR-005 全客户端代码结构解耦与上帝类重构工程方案（实施 Rules.md 第 10 条：治理超限大文件、拆解 GrokSettingsDialog 与 MainForm，实现 UI 与核心/数据层分离）已收敛采纳**。
> 本轮通过 PlanDocs 双模型独立评审（Argon 审核），确认了“提取公共 `DoubleBufferedPanel`、构建 `SettingsPanelBase` 与 `SettingsContext` DTO，按源码真实 8 个 Tab 将 1914 行的 `GrokSettingsDialog` 降维重构成 400 行以内的纯导航容器；随后在 `src/Controllers/` 构建注入 `ISynchronizeInvoke` 的轻量业务控制器，将 `MainForm` 核心业务与分部类瘦身至 300~600 行安全区间”的重构路径，彻底消除大文件超限与上帝类紧耦合隐患。

---

## 一、本轮工作态（DIR-005 方案评审收敛与采纳）

### 1. 核心需求与背景
项目在 `.claude/Rules.md` 第 10 条中确立了“单文件 1000 行预警、1500 行硬上限，杜绝上帝类，严禁滥用 `partial class` 搞机械切片”的强制契约。经代码全量排查，全工程存在严重的代码聚集与结构耦合问题，亟需系统化治理。

### 2. 现状排查结论
1. **超限首恶**：`src/UI/Controls/GrokSettingsDialog.cs` 高达 **1914 行**，超出 1500 行绝对硬上限 414 行。单文件内堆叠了 8 个业务面板的控件树构建（占 1200+ 行）、导航路由、设置项读写绑定与自绘逻辑；
2. **主窗体假拆分**：`src/MainForm.cs`（1167 行）突破 1000 行警戒线，其分部类 `MainForm.Chat.cs`（952 行）、`MainForm.Tray.cs`（671 行）、`MainForm.Files.cs`（296 行）总计 3086 行，共享私有字段与状态，直接操控底层 Socket 封包、文件分块传输 I/O 与历史存储，属于典型的 WinForms Smart UI 紧耦合；
3. **编译脚本路径缺陷**：`Tools/Compile.bat`、`CompileAndLink.bat`、`Publish.bat` 均只指定了 `src\UI\Controls\*.cs`，新增子目录必须同步更新 3 处批处理脚本。

### 3. 讨论收敛与采纳决定 (DIR-005)
- **真解耦原则**：严格执行 Rules.md 第 10 条，按生命周期与单一职责（SRP）组件化拆解，严禁滥用 `partial class`，严禁建立垃圾桶静态类；
- **基础构件抽取**：将 Dialog 内部类 `DoubleBufferedPanel` 提取为独立公共控件 `src/UI/Controls/DoubleBufferedPanel.cs`；建立 `SettingsPanelBase.cs` 基类与轻量级 `SettingsContext.cs` DTO 上下文契约，统一解耦宿主依赖；
- **8 大真实面板组件化**：严密对齐源码真实 8 个 Tab，抽离 `GeneralSettingsPanel`、`ProfileSettingsPanel`、`AppearanceSettingsPanel`、`ShortcutsSettingsPanel`、`FileTransferSettingsPanel`、`ChatHistorySettingsPanel`、`NetworkDeviceSettingsPanel`、`AboutSettingsPanel`，各面板控制在 120~220 行；`GrokSettingsDialog.cs` 缩减至 400 行以内；
- **主窗体业务控制器抽离**：在 `src/Controllers/` 构建注入 `ISynchronizeInvoke` 的 `FileTransferController`、`SessionPresenter`、`ChatPresenter`，所有底层异步网络回调安全调度回 UI 线程，各分部类降至 300~600 行；
- **边界纯粹**：消息 GUID、状态枚举与失败重发队列属于业务 Feature，坚决不夹带进本次架构重构案。

---

## 二、当前活跃实施目标（DIR-005 执行顺序）

1. **第 1 步 (P0-SettingsDialogDecouple，治理超限首恶) [已完成，2026-11-05]**：
   - 已更新 `Tools/Compile.bat`、`Tools/CompileAndLink.bat`、`Tools/Publish.bat` 追加 `src\UI\Controls\SettingsPanels\*.cs` 编译通配符；
   - 已提取公共 `DoubleBufferedPanel` 至 `src/UI/Controls/DoubleBufferedPanel.cs`；
   - 已建立 `src/UI/Controls/SettingsPanels/SettingsPanelBase.cs` 基类与 `SettingsContext.cs` 上下文 DTO；
   - 已依真实 8 个 Tab 逐一抽取独立面板，重构 `GrokSettingsDialog.cs` 为纯导航容器（代码由 1914 行压降至 384 行，严格收敛至 400 行以内）；
   - 执行 `Tools/Compile.bat` 验证原生纯编译无报错全绿通过。
2. **第 2 步 (P0-MainFormBusinessDecouple，主窗体业务控制器解耦) [当前下一步]**：
   - 更新 `Tools/Compile*.bat` 脚本追加 `src\Controllers\*.cs` 编译通配符；
   - 抽离 `FileTransferController`、`SessionPresenter`、`ChatPresenter`（注入 `ISynchronizeInvoke`）；
   - 瘦身 `MainForm` 及其分部类至 300~600 行安全区间；
   - 执行 `Tools/Compile.bat` 验证原生纯编译无报错。

---

## 三、增量模块与架构索引

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `PlanDocs/DECISIONS.md` | 记录 DIR-005 已采纳计划决定与执行顺序 |
| `PlanDocs/STATUS.md` | 状态标定为 IDLE，第 1 步已完成，等待第 2 步实施 |
| `.claude/Plan.md` | 标记 DIR-005 第 1 步已闭环，第 2 步为主窗体控制器解耦 |
| `src/UI/Controls/DoubleBufferedPanel.cs` | 已提取：公共抗闪烁双缓冲面板容器控件 (43 行) |
| `src/UI/Controls/SettingsPanels/SettingsPanelBase.cs` | 已建立：设置子面板抽象基类及通用开关/滑块控件 (378 行) |
| `src/UI/Controls/SettingsPanels/SettingsContext.cs` | 已建立：面板依赖注入轻量上下文 DTO (33 行) |
| `src/UI/Controls/SettingsPanels/*SettingsPanel.cs` | 已建立：8 个独立设置业务面板组件 (各 65~257 行) |
| `src/UI/Controls/GrokSettingsDialog.cs` | 已重构：纯导航与视口容器 (384 行，瘦身至 400 行以内) |
| `src/Controllers/` | 待建立：主窗体 MVP 业务控制器层 (第 2 步) |
