# Windows.md — Windows 客户端模块上下文

## 模块定位与职责
Windows 桌面客户端，采用原生 C# WinForms 开发，定位为**零安装、无外部依赖、支持 MSI 标准向导安装与绿色单文件执行**的现代化桌面应用。界面风格深度对标 Grok Bot 桌面端现代惊艳清爽暗黑主题（100% 高保真还原），彻底告别传统沉闷布局与边缘紧贴问题。

## 核心源码与构建物清单 (规范目录)
- `src/Program.cs`：应用程序入口，具名 Mutex（`Global\LanChat_SingleInstance_Mutex_9F24FEA2`）实现单实例互斥，广播消息唤醒已运行实例；开启高 DPI 感知（`SetProcessDPIAware`），注册全局未捕获异常兜底处理。
- `src/MainForm.cs`：主窗体核心界面控制与自适应布局协调，包含两栏控件树初始化、无边框窗体与 DWM 阴影绘制、自适应响应式布局引擎（`UpdateResponsiveLayout`）、底部居中全宽输入胶囊组装与 Win32 单实例唤醒。
- `src/MainForm.Chat.cs`：会话管理与聊天交互，包含好友状态监听、自适应会话卡片自绘（`ListSessions_DrawItem`，支持 48px/56px 弹性高度与有机吉祥物）、文本消息收发、历史记录流转、Emoji 表情选择器、手动添加 IP 直连。
- `src/MainForm.Files.cs`：文件收发与分块跟踪，包含文件选择、拖拽监听、流式进度更新、文件接收落盘与打开文件/目录调用。
- `src/MainForm.Tray.cs`：系统托盘与生命周期，包含底层网络启动绑定、自适应 Hero 个人头像自绘（`BtnAvatar_Paint`）、昵称修改向导、托盘图标常驻、关闭拦截防误退、暗黑托盘菜单与在线更新代理。
- `src/NetworkService.cs`：网络通信门面，主类定义与生命周期管理。
- `src/NetworkService.Interface.cs`：物理网卡检测与 TUN 虚拟网卡穿透过滤。
- `src/NetworkService.Discovery.cs`：多物理网卡 UDP 广播与并发子网单播探测。
- `src/NetworkService.Transfer.cs`：LANC 二进制协议解包与 64KB 流式传输引擎。
- `src/UI/Renderers/WeChatIcon.cs`：纯 GDI+ 矢量图标与 Grok 吉祥物绘制引擎（`DrawMascotAvatar` 支持倾斜双椭圆胶囊眼、有机轮廓、在线蓝点；包含 SoundWave、UpArrow、Mic、WinMin、WinMax、WinClose 等矢量图）。
- `src/UI/Renderers/WeChatDarkMenuRenderer.cs`：暗黑风格右键弹出菜单渲染器与暗黑悬浮气泡组件。
- `src/UI/Controls/WeChatButtons.cs`：无边框图标按钮（`WeChatIconButton`），扩展支持圆形按钮（`IsCircle`）、自定义填充色与窗口关闭高亮模式。
- `src/UI/Controls/WeChatChatView.cs`：Grok 风格自适应居中流式聊天视图控件（840px 约束舒适阅读流、右侧自发 `#7946D7` 魅紫气泡、左侧 `#1E1E21` 深灰卡片、反引号行内代码胶囊高亮、居中时间戳对齐与平滑滚动条）。
- `src/Services/UpdateService.cs`：GitHub Releases 在线自动更新服务（REST API 优先探测 + Web 302 重定向降级更新向导弹窗与 MSI 自动下载安装）。
- `src/Services/SessionManager.cs`：会话与消息历史纯领域解耦服务，统一提供线程安全的会话管理与摘要提取。
- `Tools/Compile.bat`：原生纯编译脚本，纯 ASCII 0-BOM，秒级生成 `release\LanChat.exe` 与 `release\uninstall.exe`。
- `Tools/CompileAndLink.bat`：一键编译链接并热替换覆盖至 `D:\soft\LanChat\`。
- `Tools/Publish.bat`：一键调用 WiX 5 打包 `release\LanChat.msi` 并自动上传发布至 GitHub Releases。
- `release/LanChat.exe`：原生独立免安装可执行程序 (~148 KB)。
- `release/LanChat.msi`：生成的 Windows Installer v2.0 安装包。

## 关键设计点
1. **Grok 极简现代清爽暗黑界面**：
   - 侧边栏纯黑底色 `#0E0E0E`，主聊天区 `#121212`，输入胶囊 `#202024`，高饱和魅紫主色 `#7946D7`，深灰卡片 `#1E1E21`。
2. **两栏自适应弹性布局与呼吸感**：
   - 侧边栏宽度根据窗体总宽度动态伸缩于 220px ~ 300px 之间；
   - 阅读流与底部输入胶囊在大屏及最大化时严格锁定最大宽度 840px，并在主区域水平居中，两端留出对称呼吸空间。
3. **自适应输入胶囊**：
   - 包含左侧 `+` 号多功能圆形按钮、中间无边框单行文本框与占位提示语、右侧麦克风按钮与标志性高饱和魅紫声波圆钮。
4. **纯矢量自绘图标与吉祥物 (`WeChatIcon`)**：
   - 全面抛弃位图资源与 Emoji，纯 GDI+ 抗锯齿自绘 Grok 吉祥物与各种状态矢量图形。
5. **系统托盘后台驻留与安全退出闭环**：
   - 拦截窗体关闭，最小化至托盘后台常驻，支持多网卡即时通信与后台气泡提醒。
