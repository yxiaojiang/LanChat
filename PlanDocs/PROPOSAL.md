# 提案 (DIR-005)

## 对上一轮审核的处理

针对 DIR-005 Round 1 审核意见（详见 `PlanDocs/REVIEW.md`），逐条回应如下：

1. **Tab 面板清单与实际代码脱节（事实错误）**：
   - 【采纳】核实 `src/UI/Controls/GrokSettingsDialog.cs:187-200`（`InitializeNavItems`）与第 428-1146 行的 8 个 `Build*Tab` 实现。
   - 原提案中错误推定的 `NotificationSettingsPanel`（通知）予以彻底废除，全面修正为与源码 100% 对齐的 8 个真实子面板映射：
     - Tab 0: `GeneralSettingsPanel.cs`（通用，对应 `BuildGeneralTab` 428 行）
     - Tab 1: `ProfileSettingsPanel.cs`（个人主页，对应 `BuildProfileTab` 654 行）
     - Tab 2: `AppearanceSettingsPanel.cs`（外观，对应 `BuildAppearanceTab` 748 行）
     - Tab 3: `ShortcutsSettingsPanel.cs`（快捷键，对应 `BuildShortcutsTab` 864 行）
     - Tab 4: `FileTransferSettingsPanel.cs`（文件传输，对应 `BuildFileTransferTab` 916 行）
     - Tab 5: `ChatHistorySettingsPanel.cs`（聊天记录，对应 `BuildChatHistoryTab` 1002 行）
     - Tab 6: `NetworkDeviceSettingsPanel.cs`（设备连接，对应 `BuildNetworkDeviceTab` 1087 行）
     - Tab 7: `AboutSettingsPanel.cs`（关于与更新，对应 `BuildAboutTab` 1146 行）

2. **重构范围泛化，混入了业务新功能（偏离治理主题）**：
   - 【采纳】完全剥离原第三阶段中关于“ChatMessage 增加 GUID、发送状态机枚举（Draft/Sending/Sent/Failed）及失败重发队列”的所有内容。
   - 本提案严格聚焦于 Rules.md 第 10 条规定的“治理超限大文件与解耦上帝类”重构目标，坚决不在纯架构重构中夹带业务功能开发。消息可靠性需求将在后续视网络状况另立独立 Feature 提案推进。

3. **补充遗漏一：`DoubleBufferedPanel` 的抽取与共享**：
   - 【采纳】核实 `src/UI/Controls/GrokSettingsDialog.cs:23-34` 局部类 `DoubleBufferedPanel` 为关键容器控件。在第 1 步中将其抽出至独立的公共控件文件 `src/UI/Controls/DoubleBufferedPanel.cs`，供 `GrokSettingsDialog`、`SettingsPanelBase` 及各子面板统一继承与复用，彻底避免子面板与宿主 Dialog 产生循环依赖与嵌套定义碎片化。

4. **补充遗漏二：面板依赖注入与 `SettingsContext` 上下文契约设计**：
   - 【采纳】核实 `src/UI/Controls/GrokSettingsDialog.cs:149-162` 构造函数的外部依赖。抽象出轻量级上下文载体类 `SettingsContext`（置于 `src/UI/Controls/SettingsPanels/SettingsContext.cs`），封装 `NetworkService`、`Action OnCheckUpdate`、`Action OnShowChangeNick`、`Action OnOpenFolder`、`Action OnFontChanged` 及宿主通信委托。子面板初始化或生命周期挂载时一次性注入 `SettingsContext`，使子面板彻底解耦宿主 Dialog 实例。

5. **补充遗漏三：`MainForm` 控制器抽离中的 UI 线程同步契约（`ISynchronizeInvoke`）**：
   - 【采纳】核实 `NetworkService` 底层网络回调均为后台线程。在第 2 步抽离 `ChatPresenter`、`FileTransferController` 与 `SessionPresenter` 时，在控制器构造函数中显式注入 `ISynchronizeInvoke`（即主窗体实例自身），所有跨线程网络事件回调统一通过 `_syncInvoke.BeginInvoke` 安全调度回 UI 主线程，杜绝 `InvalidOperationException` 风险。

6. **重构实施顺序调整**：
   - 【采纳】完全采纳审核方制定的清晰两步法顺序：第 1 步治理首要超限上帝类 `GrokSettingsDialog`，第 2 步解耦主窗体业务控制器并瘦身分部类，新特性剥离出本案。

---

## 要决定的问题

LanChat 客户端目前存在明显的“上帝类（God Class）”聚集与高耦合现象，亟需依据 `.claude/Rules.md` 第 10 条（单文件 1000-1500 行上限与杜绝上帝类契约）进行彻底的结构解耦：

1. **超限首恶治理问题**：`src/UI/Controls/GrokSettingsDialog.cs`（1914 行）严重突破 1500 行绝对硬上限。单文件内混杂了 8 个业务面板的控件树构建、导航路由、几十个设置项的读写绑定与绘制，每次迭代修改都存在巨大的心智负担与副作用风险；
2. **主窗体假拆分与高耦合问题**：`src/MainForm.cs`（1167 行）突破 1000 行警戒线，虽然通过 C# `partial class` 分割了 `MainForm.Chat.cs`（952 行）、`MainForm.Tray.cs`（671 行）、`MainForm.Files.cs`（296 行），但各文件仍共享整个窗体的私有字段与状态，属于“切碎了上帝类但上帝类仍然存在”。主窗体直接操控底层 Socket 封包、文件传输 I/O、历史读写与消息排版，属于典型的 WinForms Smart UI 紧耦合；
3. **分阶段实施边界与轻量约束**：在严守 C# 5.0 / .NET Framework 4.0 语法、零第三方外部 NuGet 依赖、免 IoC 容器的前提下，如何以最低侵入性、保持向后兼容和原生编译的方式，将 UI 表现层、业务控制层（Presenter/Controller）、数据模型层稳妥解耦。

---

## 建议决定

1. **确定重构基线原则**：严格执行 Rules.md 第 10 条，以“真解耦、基于生命周期与单一职责（SRP）”为准则，杜绝滥用 `partial class` 搞机械分片，杜绝创建垃圾桶静态类；
2. **提取共享基础控件与上下文契约**：
   - 将 `GrokSettingsDialog.cs:23-34` 的 `DoubleBufferedPanel` 提取为独立公共控件 `src/UI/Controls/DoubleBufferedPanel.cs`，消除子面板与 Dialog 之间的内部类依赖；
   - 在 `src/UI/Controls/SettingsPanels/` 建立 `SettingsPanelBase.cs` 抽象基类，统一继承 `DoubleBufferedPanel`，规范面板生命周期（`Initialize(SettingsContext context)`、`ApplySettings()`、`Dispose()`）；
   - 定义轻量级 `SettingsContext` DTO 上下文契约（`src/UI/Controls/SettingsPanels/SettingsContext.cs`），集中封装 `NetworkService`、更新检查委托、修改昵称委托、打开接收目录委托及字体刷新回调，实现子面板与宿主 Dialog 的解耦。
3. **第一阶段 (P0-SettingsDialogDecouple)：设置窗口 8 大面板组件化拆解**：
   - 将 `GrokSettingsDialog.cs` 降维为纯轻量导航容器与路由调度器，总代码量压缩至 300~400 行；
   - 在 `src/UI/Controls/SettingsPanels/` 依据源码真实 Tab 严格抽离 8 个独立面板组件：
     - `GeneralSettingsPanel.cs`（通用：语言/打开方式/开机自启/字体缩放，对应原 `BuildGeneralTab` 428 行）
     - `ProfileSettingsPanel.cs`（个人主页：昵称/设备ID/头像徽章/签名展示，对应原 `BuildProfileTab` 654 行）
     - `AppearanceSettingsPanel.cs`（外观：暗黑主题色阶/字体家族切换/强调色偏好，对应原 `BuildAppearanceTab` 748 行）
     - `ShortcutsSettingsPanel.cs`（快捷键：发送消息快捷键/截图/老板键设置，对应原 `BuildShortcutsTab` 864 行）
     - `FileTransferSettingsPanel.cs`（文件传输：存储路径配置/自动接收阈值/传输并发数，对应原 `BuildFileTransferTab` 916 行）
     - `ChatHistorySettingsPanel.cs`（聊天记录：数据存储目录/清理缓存/记录导出，对应原 `BuildChatHistoryTab` 1002 行）
     - `NetworkDeviceSettingsPanel.cs`（设备连接：广播端口/网卡适配器绑定/发现节点列表，对应原 `BuildNetworkDeviceTab` 1087 行）
     - `AboutSettingsPanel.cs`（关于与更新：客户端版本/检查更新/版权与诊断信息，对应原 `BuildAboutTab` 1146 行）
   - 每个子面板代码量均收敛在 120~220 行安全区间，职责清晰内聚，具备独立生命周期；
4. **第二阶段 (P0-MainFormBusinessDecouple)：主窗体业务控制器解耦**：
   - 引入轻量级 MVP/Controller 模式，在 `src/Controllers/` 构建无直接 UI 控件依赖的纯业务控制器，构造时统一注入 `ISynchronizeInvoke`（窗体句柄），所有底层网络异步回调通过其安全切回 UI 主线程：
     - `FileTransferController.cs`：接管文件发送队列、对方接收/拒绝确认与分块传输进度流，抽离 `MainForm.Files.cs` 中的业务流；
     - `SessionPresenter.cs`：管理 Peer 好友在线状态更新、会话列表动态增删、全员广播节点维护及未读消息计数统计；
     - `ChatPresenter.cs`：接管消息投递协调、文字/文件/图片会话编排与剪贴板转发逻辑；
   - `MainForm` 及其分部类只保留原生 Win32 消息路由、无边框窗体交互、各 UI 视图组件组装与事件转接，主文件与分部类全面降至 300~600 行安全区间。
5. **构建通道同步更新**：
   - 在 `Tools/Compile.bat`、`Tools/CompileAndLink.bat` 与 `Tools/Publish.bat` 编译脚本中显式追加 `src\UI\Controls\SettingsPanels\*.cs` 与 `src\Controllers\*.cs` 通配路径，确保原生 csc 批处理脚本零编译链接错误。

---

## 证据

1. **实际文件行数与超限现状核对证据**（2026-11-05 实测）：
   - `src/UI/Controls/GrokSettingsDialog.cs`：**1914 行**，全工程单文件超限第一位（超限 414 行）；
   - `src/MainForm.cs`（1167 行）、`MainForm.Chat.cs`（952 行）、`MainForm.Tray.cs`（671 行）、`MainForm.Files.cs`（296 行），主窗体总计 3086 行；
   - `src/UI/Controls/WeChatChatView.cs`（934 行）：在 DIR-004 中收口，内部结构稳定，暂不超限；
   - `src/UI/Renderers/WeChatIcon.cs`（859 行）：纯 GDI+ 静态矢量图标集，内聚度极高，无需强拆；
2. **源码实现映射证据**：
   - `GrokSettingsDialog.cs:23-34` 声明了内部类 `DoubleBufferedPanel`；
   - `GrokSettingsDialog.cs:187-200`（`InitializeNavItems`）精确定义了 8 个真实 Tab 导航项目（0: 通用、1: 个人主页、2: 外观、3: 快捷键、4: 文件传输、5: 聊天记录、6: 设备连接、7: 关于与更新）；
   - `GrokSettingsDialog.cs:428-1146` 包含了对应的 8 个 `Build*Tab` 私有方法，代码量达 1200+ 行；
   - `GrokSettingsDialog.cs:149-162` 明确了宿主注入的 `NetworkService` 及 4 个 Action 回调；
3. **编译通道与路径缺陷证据**：
   - 核对 `Tools/Compile.bat:43`、`CompileAndLink.bat:45`、`Publish.bat:64`，其编译参数仅包含 `src\UI\Controls\*.cs`，不具备跨子目录递归能力。新增 `src\UI\Controls\SettingsPanels\` 或 `src\Controllers\` 必须显式在 3 个脚本中追加通配符，否则将导致本地脚本编译失败。

---

## 未核实

1. **多层嵌套容器在懒加载切换时的首帧重绘与输入框焦点**：
   - 子面板使用懒加载挂载到右侧主内容区时，在开启 `WS_EX_COMPOSITED` 与 `WS_CLIPCHILDREN` 的环境下，输入框（`TextBox`）的默认焦点与高 DPI 缩放下首次绘制是否存在视觉微跳，需在第 1 步实施时实测校验。
2. **控制器析构与后台网络事件解绑顺序**：
   - 主窗体关闭（`FormClosing` / `FormClosed`）时，`FileTransferController` 与 `ChatPresenter` 等控制器解绑 `NetworkService` 事件的先后顺序，需实测确认无悬挂后台线程或在窗体销毁后触发跨线程委托。

---

## 建议顺序

1. **第 1 步 (P0-SettingsDialogDecouple，治理超限首恶)**：
   - 更新 `Tools/Compile.bat`、`Tools/CompileAndLink.bat`、`Tools/Publish.bat`，追加 `src\UI\Controls\SettingsPanels\*.cs` 编译通配符；
   - 提取公共 `DoubleBufferedPanel` 至 `src/UI/Controls/DoubleBufferedPanel.cs`；
   - 建立 `SettingsPanelBase.cs` 面板抽象基类与 `SettingsContext.cs` 上下文 DTO；
   - 按源码真实 8 个 Tab 分别抽取子面板：`GeneralSettingsPanel.cs`、`ProfileSettingsPanel.cs`、`AppearanceSettingsPanel.cs`、`ShortcutsSettingsPanel.cs`、`FileTransferSettingsPanel.cs`、`ChatHistorySettingsPanel.cs`、`NetworkDeviceSettingsPanel.cs`、`AboutSettingsPanel.cs`；
   - 重构 `GrokSettingsDialog.cs` 为纯导航容器，代码行数收敛至 400 行以内；
   - 执行 `echo. | cmd.exe /c Tools\Compile.bat` 验证原生纯编译无报错。
2. **第 2 步 (P0-MainFormBusinessDecouple，主窗体业务控制器解耦)**：
   - 更新 `Tools/Compile*.bat` 脚本，追加 `src\Controllers\*.cs` 编译通配符；
   - 抽离 `FileTransferController.cs`（注入 `ISynchronizeInvoke`，接管文件传输队列与进度调度）；
   - 抽离 `SessionPresenter.cs` 与 `ChatPresenter.cs`（注入 `ISynchronizeInvoke`，接管节点发现状态与消息编排）；
   - 瘦身 `MainForm` 及其分部类，只保留原生 WinForms 视图组装与事件转接，各文件行数全面降至 300~600 行安全区间；
   - 执行 `echo. | cmd.exe /c Tools\Compile.bat` 验证原生纯编译无报错。

---

## 明确不做

1. **不引入任何外部框架与 IoC 容器**：不引入 Autofac、Prism、MVVMLight 等第三方 NuGet 包，坚持使用原生纯 C# 5.0 构造器依赖注入与事件订阅模式；
2. **不搞过度抽象与空壳接口**：不为单一实现的类强行套用 `IFoo` 空壳接口，只在存在真正解耦需求（如面板生命周期契约、线程同步上下文）处建立必要基类和契约；
3. **不夹带业务新特性**：消息 GUID、消息流转状态枚举（Draft/Sending/Sent/Failed）及失败重发队列明确属于 Feature 范围，坚决不夹带进本次大文件治理重构案，后续视网络可靠性需求另立独立 Feature 提案；
4. **不变更底层网络协议魔数与传输包结构**：保持现有 `LANC` 魔数与分块传输协议向前兼容，不影响跨端 Linux 终端与 Python 端 peer 互通。
