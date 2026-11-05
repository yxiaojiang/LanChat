# 提案 (DIR-005)

## 要决定的问题

LanChat 客户端目前存在明显的“上帝类（God Class）”聚集与高耦合现象，亟需依据 `.claude/Rules.md` 第 10 条（单文件 1000-1500 行上限与杜绝上帝类契约）进行彻底的结构解耦：

1. **超限首恶治理问题**：`src/UI/Controls/GrokSettingsDialog.cs`（1914 行）严重突破 1500 行绝对硬上限。单文件内混杂了 8 个业务面板的控件树构建、导航路由、几十个设置项的读写绑定与绘制，每次迭代修改都存在巨大的心智负担与副作用风险；
2. **主窗体假拆分与高耦合问题**：`src/MainForm.cs`（1167 行）突破 1000 行警戒线，虽然通过 C# `partial class` 分割了 `MainForm.Chat.cs`（952 行）、`MainForm.Tray.cs`（671 行）、`MainForm.Files.cs`（296 行），但各文件仍共享整个窗体的私有字段与状态，典型“切碎了上帝类但上帝类仍然存在”。主窗体直接操控底层 Socket 封包、文件传输 I/O、历史读写与消息排版，属于典型的 WinForms Smart UI 紧耦合；
3. **分阶段实施边界与轻量约束**：在严守 C# 5.0 / .NET Framework 4.0 语法、零第三方外部 NuGet 依赖、免 IoC 容器的前提下，如何以最低侵入性、保持向后兼容和原生编译的方式，将 UI 表现层、业务控制层（Presenter/Controller）、数据模型层稳妥解耦。

---

## 建议决定

1. **确定重构基线原则**：严格执行 Rules.md 第 10 条，以“真解耦、基于生命周期与单一职责（SRP）”为准则，杜绝滥用 `partial class` 搞机械分片，杜绝创建垃圾桶静态类；
2. **第一阶段 (P0-SettingsDialogDecouple)：设置窗口组件化拆解**：
   - 将 `GrokSettingsDialog.cs` 降维为纯轻量导航与面板切换容器，总代码量压缩至 300~400 行；
   - 在 `src/UI/Controls/SettingsPanels/` 建立 `SettingsPanelBase` 基础面板（继承自 UserControl 或 DoubleBufferedPanel）；
   - 将 8 个 Tab 分别抽离为独立的面板组件：
     - `GeneralSettingsPanel.cs`（常规设置：语言/打开方式/开机自启）
     - `AppearanceSettingsPanel.cs`（外观与个性化：主题/字体样式/字号）
     - `AccountSettingsPanel.cs`（账户与身份：昵称/设备ID/吉祥物头像）
     - `NetworkSettingsPanel.cs`（网络与连接：协议/端口/网卡绑定/状态）
     - `FilesSettingsPanel.cs`（文件传输：默认保存路径/自动接收）
     - `HistorySettingsPanel.cs`（聊天记录：数据存储/历史清理/导出）
     - `NotificationSettingsPanel.cs`（通知与提醒：声音/托盘弹窗/浮窗预览）
     - `AboutSettingsPanel.cs`（关于与更新：版本检测/诊断信息/版权）
   - 每个子面板代码量严格控制在 150~250 行之间，职责清晰内聚，具备独立生命周期；
3. **第二阶段 (P0-MainFormBusinessDecouple)：主窗体业务控制器解耦**：
   - 引入轻量级 MVP/Controller 架构，在 `src/Controllers/` 构建无 UI 绑定的业务控制器：
     - `ChatPresenter.cs`：管理当前选中的会话上下文、消息投递、发送失败处理、划选复制协调；
     - `FileTransferController.cs`：接管文件发送队列、对方接收确认与分块传输进度流；
     - `SessionPresenter.cs`：管理 Peer 好友列表动态增删、全员群聊置顶、未读计数统计；
   - `MainForm` 只保留 Win32 消息循环、顶层无边框控制、子组件组装与事件转接，主文件与分部类全面降至 300~600 行以内；
4. **第三阶段 (P1-MessageStateModel)：数据模型与状态机规范化**：
   - 在 `Models.cs` 中完善 `ChatMessage` 的唯一标识（GUID）、消息流转状态枚举（`Draft`、`Sending`、`Sent`、`Failed`）；
   - 建立消息失败重发队列，与 `WeChatChatView` 自绘重试交互打通。

---

## 证据

1. **实际文件行数测量证据**（2026-11-05 实测）：
   - `src/UI/Controls/GrokSettingsDialog.cs`：**1914 行**（全工程最大单文件，严重超限 414 行）；
   - `src/MainForm.cs`：**1167 行**（超 1000 行警戒线）；
   - `src/MainForm.Chat.cs`：**952 行**（逼近警戒线）；
   - `src/UI/Controls/WeChatChatView.cs`：**934 行**（在 DIR-004 中收口，内部结构稳定，暂不超限）；
   - `src/UI/Renderers/WeChatIcon.cs`：**859 行**（纯 GDI+ 静态矢量图标集，内聚度极高，无需强拆）；
2. **编译通道与路径证据**：
   - `Tools/Compile.bat` 第 43 行：
     ```cmd
     "%CSC_PATH%" ... src\Models.cs src\NetworkService*.cs src\MainForm*.cs src\Program.cs src\UI\Controls\*.cs src\UI\Renderers\*.cs src\Services\*.cs
     ```
     当前编译命令已包含 `src\UI\Controls\*.cs` 与 `src\Services\*.cs`；为支持 `SettingsPanels` 与 `Controllers`，只需在 `Compile.bat`、`CompileAndLink.bat` 与 `Publish.bat` 的 csc 命令行中补充 `src\UI\Controls\SettingsPanels\*.cs` 与 `src\Controllers\*.cs` 通配符即可，零第三方侵入；
3. **架构现状与耦合证据**：
   - 在 `GrokSettingsDialog.cs` 中，`BuildControls()` 内部连续调用 `BuildGeneralTab()`、`BuildAppearanceTab()`、`BuildAccountTab()`、`BuildNetworkTab()`... 将近 1200 行全部为匿名委托和嵌套控件定义，与外层 Dialog 滚动容器紧密死锁；
   - 在 `MainForm.Files.cs` 中，拖拽文件事件 `MainForm_DragDrop` 直接依赖私有字段 `_netService` 发起网络传输，无法独立编写业务测试。

---

## 未核实

1. **多级子控件双缓冲与切页闪烁风险**：
   - 当 8 个设置面板转变为独立 `UserControl` 后，父级为 `DoubleBufferedPanel` 视口。需在实测中验证 WinForms 的 `WS_CLIPCHILDREN` 是否会在多层嵌套控件切页时出现短暂重绘空白；
   - 应对对策：子面板基类 `SettingsPanelBase` 统一继承 `DoubleBufferedPanel` 或在构造时开启 `OptimizedDoubleBuffer` 与 `AllPaintingInWmPaint`。
2. **控制器跨线程 UI 刷新机制**：
   - `NetworkService` 运行在独立工作线程中，当解耦为 `ChatPresenter` 等控制器后，事件回调需要在主线程同步。
   - 应对对策：控制器构造时传入窗体的 `ISynchronizeInvoke`（即 Form 自身），或在 UI 订阅层显式通过 `BeginInvoke` 转回 UI 线程。

---

## 建议顺序

1. **第 1 步 (P0-SettingsDialogDecouple，治理超限第一大件)**：
   - 在 `Tools/Compile*.bat` 中追加 `src\UI\Controls\SettingsPanels\*.cs` 编译通配符；
   - 建立 `src/UI/Controls/SettingsPanels/SettingsPanelBase.cs` 基类；
   - 依次拆解抽离 8 个子面板（`GeneralSettingsPanel.cs`、`AppearanceSettingsPanel.cs` 等）；
   - 将 `GrokSettingsDialog.cs` 缩减重构成纯导航切换容器（降至 400 行以内）；
   - 执行 `Tools/Compile.bat` 验证纯编译无报错。
2. **第 2 步 (P0-MainFormBusinessDecouple，主窗体控制器解耦)**：
   - 在 `Tools/Compile*.bat` 中追加 `src\Controllers\*.cs` 编译通配符；
   - 提取 `FileTransferController.cs`，将 `MainForm.Files.cs` 的文件流业务迁入；
   - 提取 `SessionPresenter.cs` 与 `ChatPresenter.cs`，接管好友状态维护与聊天消息编排；
   - 将 `MainForm.cs` 各文件瘦身至 300~600 行安全区间；
   - 执行 `Tools/Compile.bat` 验证。
3. **第 3 步 (P1-MessageStateModel，消息模型与重试状态机闭环)**：
   - 在 `Models.cs` 补齐 `MessageStatus` 状态枚举与唯一标识；
   - 优化 `ChatPresenter` 离线缓存重试逻辑与 `WeChatChatView` 渲染联动。

---

## 明确不做

1. **不引入庞大的外部框架**：绝不引入 Prism、MVVMLight、Autofac 等第三方依赖，坚持使用原生纯 C# 5.0 的构造器依赖注入与事件模式；
2. **不过度接口化**：不为了解耦而搞上百个一对一的无用 `IFoo` 空壳接口，只在存在跨层、多实现或高变更点（如面板基类、控制器接口）做必要抽象；
3. **不触碰跨端底层协议魔数**：不修改网络封包 `LANC` 魔数与分块协议定义，保持与现有 Linux 终端/Python 端的向前兼容性。

---

## 对上一轮审核的处理

无（本轮为 DIR-005 首轮提案）。
