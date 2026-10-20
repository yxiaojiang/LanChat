# Windows.md — Windows 客户端模块上下文

## 模块定位与职责
Windows 桌面客户端，采用原生 C# WinForms 开发，定位为**零安装、无外部依赖、体积仅约 55KB 的单文件免环境程序**。

## 核心源码清单
- `Program.cs`：应用程序入口，开启高 DPI 感知（`SetProcessDPIAware`），注册全局未捕获异常兜底处理。
- `MainForm.cs`：主窗体，包含全套扁平化现代 UI、FlowLayout 工具栏、左右分栏（好友列表/聊天区域）、文件拖拽拦截（Drag & Drop）、进度条渲染、消息气泡排版。
- `build.bat`：构建脚本，直接调用系统自带的 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 进行秒级编译生成 `LanChat.exe`，无需安装 Visual Studio 或 .NET SDK。
- `LanChat.csproj`：用于 Visual Studio 或现代 `dotnet build` 的辅助工程配置。

## 关键设计点
1. **FlowLayout 自适应工具栏**：
   - 顶部工具栏右侧采用 `FlowLayoutPanel`，【🔄 刷新好友】固定宽度 120px，【➕ 添加IP】95px，【📂 接收文件夹】125px，彻底避免高 DPI 缩放（125%、150%）下的文字折行或挤压。
2. **纯代码构建界面**：所有控件通过 C# 代码声明式组装，不依赖 `.Designer.cs` 或资源文件，便于单一源文件随处直接编译。
3. **文件拖拽响应**：窗体及输入框均开启 `AllowDrop = true`，拖入文件自动触发目标选择或直接向当前选中的好友开启流式发送。
4. **安全跨线程更新**：封装 `SafeInvoke`，在后台 Socket 触发的各种网络事件中安全调度 UI 渲染，避免句柄泄漏或死锁。
