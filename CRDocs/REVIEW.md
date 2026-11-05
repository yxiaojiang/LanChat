# 代码审核报告 (Code Review) — DIR-005 第 2 步

- **任务编号**：`DIR-005-STEP-2` (P0-MainFormBusinessDecouple)
- **任务主题**：全客户端代码结构解耦与上帝类重构工程方案（第 2 步：主窗体业务控制器解耦与 Designer 结构分离）
- **审核方**：Argon (独立审核方)
- **审核基线**：
  - `.claude/Rules.md`（特别是第 10 条 Anti-God-Class 强制契约、第 1 条独立判断与风险揭示原则、第 4 条最小实现与零冗余依赖）
  - `PlanDocs/DECISIONS.md` (`DIR-005` 第 2 步决议)
  - `src/MainForm*.cs`、`src/Controllers/*.cs` 源码与实施 diff
- **审核结论**：**`CHANGES_REQUESTED`**

---

## 一、审核范围与变动核对

本次审核对 commit `3421fe7` 涉及的所有解耦代码、新增控制器、窗体拆分及编译脚本进行了逐行审查：

| 文件 | 变更性质 | 行数 | 核心审查点 |
| :--- | :--- | :--- | :--- |
| `src/MainForm.cs` | **重构瘦身** | 457 行 | 由 **1383 行** 压降至 **457 行**，抽离业务控制器引用并剥离无边框缩放过滤器与控件图树 |
| `src/MainForm.Chat.cs` | **重构瘦身** | 606 行 | 由 **1149 行** 压降至 **606 行**，群聊/私聊/未读逻辑交接给 Presenter，保留会话列表与输入交互渲染 |
| `src/MainForm.Files.cs` | **重构瘦身** | 202 行 | 由 **386 行** 压降至 **202 行**，流式发送、分块进度、落盘校验交接给 Controller |
| `src/MainForm.Designer.cs` | **新增分离** | 685 行 | 提取主窗体全部控件树构造、分栏布局与 `UpdateResponsiveLayout` 响应式计算，对标标准 WinForms 架构 |
| `src/MainForm.Tray.cs` | 局部调整 | 671 行 | 初始化控制器并保持托盘浮窗与气泡交互逻辑 |
| `src/Controllers/SessionPresenter.cs` | **新增抽离** | 301 行 | 统一调度全员群聊与好友会话，处理节点上线/离线历史合并、关键字检索与切页编排 |
| `src/Controllers/ChatPresenter.cs` | **新增抽离** | 259 行 | 统一管理广播与私聊文本发送（后台线程并发）、接收路由、未读计数增量与系统消息注入 |
| `src/Controllers/FileTransferController.cs` | **新增抽离** | 206 行 | 统一管理异步文件发送、进度回传、接收归档与系统原生外开定位 |
| `src/UI/Controls/FormResizeMessageFilter.cs` | **新增抽离** | 82 行 | 独立封装 `IMessageFilter`，接管四周 8px 边缘原生 `WM_SYSCOMMAND (SC_SIZE)` 拖拽缩放 |
| `src/UI/Controls/WeChatChatView.cs` | 辅助扩充 | 5 行 | 新增 `ClearMessages()` 便捷清屏方法 |
| `Tools/Compile*.bat` | 配置追加 | — | `Compile.bat`、`CompileAndLink.bat`、`Publish.bat` 追加 `src\Controllers\*.cs` 通配符 |

---

## 二、逐项深度审查与实测验证

### 1. 编译管线与语言规范兼容性（通过）
- 调用系统原生 `csc.exe`（.NET Framework 4.0 / C# 5.0）纯编译验证，**0 警告、0 错误** 顺利编译 `release\LanChat.exe`；
- 执行 `dotnet build src/LanChat.csproj` 现代多目标构建双重校验，**0 警告、0 错误** 全部通过；
- 零第三方 NuGet 依赖，构建脚本 `Tools/Compile*.bat` 追加 `src\Controllers\*.cs` 均已闭环。

### 2. 单文件行数硬上限与防上帝类契约执行情况（通过）
- 严格落实 `Rules.md` 第 10 条：
  - `MainForm.cs`（457 行）、`MainForm.Chat.cs`（606 行）、`MainForm.Files.cs`（202 行）、`MainForm.Designer.cs`（685 行）、`MainForm.Tray.cs`（671 行）；
  - 全工程各源码文件全面回缩至 **700 行以内**，彻底消除了越过 1000 行警戒线和 1500 行硬上限的隐患；
  - 坚持**实质解耦**：抽离的 3 大 Controller/Presenter 类职责单一自成体系，`FormResizeMessageFilter` 独立封装，并非无意义的切分。

### 3. MVP 控制器生命周期与线程安全（基本通过）
- 控制器统一注入 `ISynchronizeInvoke`，网络线程回调均通过 `SafeInvoke` 进行安全的 `BeginInvoke` 调度；
- `SessionManager` 内部维护并发锁，多控制器共享会话状态无竞态风险。

---

## 三、阻断性缺陷与回归分析（Defects Requiring Changes）

遵循 `Rules.md` 第 1 条（独立判断第一优先级，严禁默认用户或前序实施一定正确），审核发现以下 1 处阻塞性功能缺陷与 1 处交互防抖退化：

### 1. [P0 阻塞缺陷] `FileTransferController.SendFiles` 遗漏本地界面渲染通知（文件发送后无法即时呈现在聊天视口中）
- **事实与代码核对**：
  - 查阅旧代码（`HEAD~1:src/MainForm.Files.cs`）：当用户点击发送或拖入文件时，调用 `SendFilesToPeer` 执行 `AddMessageToHistory(peer.Id, fileMsg)`，其内部显式调用了 `_chatView.AppendMessage(fileMsg)`，发送方界面立刻看到自己刚刚发送的文件气泡卡片。
  - 查阅重构后代码：
    - `FileTransferController.cs:78` 仅调用了 `_sessionManager.AddMessage(peer.Id, fileMsg)`，其后未向外派发任何事件；
    - 查阅 `FileTransferController.cs` 的事件定义（第 18-20 行），仅定义了 `ProgressChanged`、`TransferCompleted` 与 `FileReceived`，**完全遗漏了 `FileSent` 事件**；
    - 查阅 `MainForm.cs:InitializeControllers`（第 164-169 行），主窗体也未针对文件发送动作监听并追加消息。
- **缺陷现象**：本地用户点击或拖入发送文件后，聊天视口没有任何反应，发送出的文件卡片消失不见；只有当用户切换到其他联系人再切回来时，才能从持久化历史中看到该文件。
- **修复方案**：
  1. 在 `FileTransferController.cs` 中增加事件：
     ```csharp
     public event Action<ChatMessage, string> FileSent;
     ```
  2. 在 `FileTransferController.SendFiles` 第 79 行后追加触发：
     ```csharp
     if (FileSent != null) FileSent(fileMsg, peer.Id);
     ```
  3. 在 `MainForm.cs:InitializeControllers` 中绑定该事件：
     ```csharp
     _fileController.FileSent += (fileMsg, convKey) => {
         if (_activeChatKey == convKey && _chatView != null)
         {
             _chatView.AppendMessage(fileMsg);
         }
         if (_listSessions != null) _listSessions.Invalidate();
     };
     ```

### 2. [P1 体验退化] `SessionPresenter.SelectSession` 丢失相同会话点击防抖，引发视口闪烁与滚动重置
- **事实与代码核对**：
  - 查阅旧代码（`HEAD~1:src/MainForm.Chat.cs`）：在切换会话前存在明确防抖判断：
    ```csharp
    if (string.Equals(newKey, previousKey, StringComparison.Ordinal))
    {
        _selectedPeer = newPeer;
        ClearActiveChatUnread();
        return;
    }
    ```
  - 查阅重构后代码 `SessionPresenter.cs:175-217`（`SelectSession`）：删除了该防抖分支，无论点击的是否为当前已有会话，均无条件触发 `ActiveSessionChanged`；
  - 主窗体 `OnActiveSessionChanged` 响应后无条件执行 `SwitchActiveChatView()` -> `_chatView.SetMessages(...)`，导致用户重复点击当前会话项时，聊天视口无谓地清空并重建所有消息气泡，导致滚动条位置被强行重置到底部并出现画面闪烁。
- **修复方案**：
  在 `SessionPresenter.cs:SelectSession` 内，在计算完 `newKey` 后，判断若 `string.Equals(newKey, previousKey, StringComparison.Ordinal)`，则仅执行 `ClearActiveChatUnread()` 并直接 `return`，跳过 `ActiveSessionChanged` 广播。

---

## 四、代码细节核对与建议（Non-blocking Nits）

以下几点属于非阻塞性细节优化建议：
1. **清理 `MainForm.Tray.cs` 冗余空行**：`src/MainForm.Tray.cs:31-36` 抽离控制器后残留了 6 行空白，后续整理时可顺手压缩。
2. **`SafeInvoke` 增强防御**：在 3 个 Presenter/Controller 的 `SafeInvoke` 中，可加入针对 `Control.IsDisposed` 和 `IsHandleCreated` 的前置检查，避免在极端关闭窗口瞬间抛出不必要的托管异常。
3. **收拢重复的 `ClearActiveChatUnread` 逻辑**：`MainForm.Tray.cs:442` 与 `SessionPresenter.cs:272` 均声明了 `ClearActiveChatUnread()`，主窗体版本可直接委托给 Presenter 实现，进一步消除业务冗余。

---

## 五、最终结论

**`CHANGES_REQUESTED`**

重构在代码行数压降（全量 < 700 行）、上帝类解耦方向与编译脚本对齐上成效显著，但抽离过程中遗漏了本地发送文件的即时上屏呈现事件（P0 回归），并丢失了会话切页防抖保护（P1 退化）。请针对上述两点进行针对性闭环后，再次提交审核。
