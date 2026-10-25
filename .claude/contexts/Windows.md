# Windows.md — Windows 客户端模块上下文

## 模块定位与职责
Windows 桌面客户端，采用原生 C# WinForms 开发，定位为**零安装、无外部依赖、支持 MSI 标准向导安装与绿色单文件执行**的现代化桌面应用。界面风格深度对标 Grok Bot 桌面端现代惊艳清爽暗黑主题（100% 高保真还原），彻底告别传统沉闷布局与边缘紧贴问题。

## 核心源码与构建物清单 (规范目录)
- `src/Program.cs`：应用程序入口，具名 Mutex（`Global\LanChat_SingleInstance_Mutex_9F24FEA2`）实现单实例互斥，广播消息唤醒已运行实例；开启高 DPI 感知（`SetProcessDPIAware`），设置 `SetCurrentProcessExplicitAppUserModelID`，注册全局未捕获异常兜底处理。
- `src/MainForm.cs`：主窗体核心界面控制与自适应布局协调，包含两栏控件树初始化、无边框窗体与 DWM 阴影绘制、自适应响应式布局引擎（`UpdateResponsiveLayout`）、底部 64px 居中全宽输入胶囊组装与 Win32 单实例唤醒。
- `src/MainForm.Chat.cs`：会话管理与聊天交互，包含好友状态监听、自适应会话卡片自绘（`ListSessions_DrawItem`，支持 48px/56px 弹性高度与有机吉祥物）、文本消息收发、历史记录流转、`GrokPopupMenu` 悬浮菜单弹出。
- `src/MainForm.Files.cs`：文件收发与分块跟踪，包含文件选择、拖拽监听、流式进度更新、文件接收落盘与打开文件/目录调用。
- `src/MainForm.Tray.cs`：系统托盘与生命周期，包含底层网络启动绑定、自适应 Hero 个人头像自绘（`BtnAvatar_Paint`）、昵称修改向导、托盘图标常驻、关闭拦截防误退、`GrokPopupMenu` 暗黑托盘菜单与在线更新代理。
- `src/NetworkService.cs`：网络通信门面，主类定义与生命周期管理。
- `src/NetworkService.Interface.cs`：物理网卡检测与 TUN 虚拟网卡穿透过滤。
- `src/NetworkService.Discovery.cs`：多物理网卡 UDP 广播与并发子网单播探测。
- `src/NetworkService.Transfer.cs`：LANC 二进制协议解包与 64KB 流式传输引擎。
- `src/UI/Controls/GrokPopupMenu.cs`：通用圆角浮动弹出菜单（16px圆角、44px舒适行高、8px悬停卡片、矢量图标、全自动边界检测）。
- `src/UI/Controls/GrokSettingsDialog.cs`：Grok Bot 原生全局设置窗口（920x640，220px纯黑侧边栏无缝过渡，18pt大标题，54px卡片行，32px药丸下拉）。
- `src/UI/Controls/GrokConnectNodeForm.cs`：Grok Bot 原生连接节点窗口（100% 对标 ConnetctApp.png：顶部搜索胶囊、过滤芯片、局域网在线节点推荐卡片流与手动指定 IP 直连握手）。
- `src/UI/Controls/GrokCreateNodeForm.cs`：Grok Bot 原生创建新节点/新Bot窗口（100% 对标 CreateNewBot.png：居中动态吉祥物预览、起名胶囊输入框、14 种有机形态网格、11 色调色盘）。
- `src/UI/Controls/GrokAboutForm.cs`：Grok Bot 原生关于窗口（100% 对标 About.png：居中 Squircle 吉祥物徽章、版本信息与“复制版本信息”圆角药丸按钮）。
- `src/UI/Controls/GrokChangeNickForm.cs`：Grok Bot 原生暗黑昵称修改窗口（吉祥物微标、暗黑圆角输入框与紫色保存按钮）。
- `src/UI/Controls/GrokFeedbackForm.cs`：Grok Bot 原生发送反馈窗口（100% 对标 SendFK.png：多行反馈输入框、网络诊断复选框与发送反馈按钮）。
- `src/UI/Controls/GrokUpdateForm.cs`：Grok Bot 原生软件更新窗口（100% 对标 Alis7.png：版本对比横幅、更新日志与平滑进度条）。
- `src/UI/Controls/GrokMessageBox.cs`：Grok Bot 原生统一样式暗黑提示框（彻底取代系统生硬白底 MessageBox）。
- `src/UI/Renderers/WeChatIcon.cs`：纯 GDI+ 矢量图标与 Grok 吉祥物绘制引擎（`DrawMascotAvatar` 支持倾斜双椭圆胶囊眼、有机轮廓、在线蓝点；包含 UpArrow、Mic、SoundWave、WinMin、WinMax、WinClose 等矢量图）。
- `src/UI/Controls/WeChatButtons.cs`：无边框图标按钮（`WeChatIconButton`），扩展支持圆形按钮（`IsCircle`）、右上角 56x64 矩形标题栏控制按钮、自定义填充色与窗口关闭高亮模式。
- `src/UI/Controls/WeChatChatView.cs`：Grok 风格自适应居中流式聊天视图控件（840px 约束舒适阅读流、右侧自发 `#7946D7` 魅紫气泡、左侧 `#1E1E21` 深灰卡片、反引号行内代码胶囊高亮、居中时间戳对齐与平滑滚动条）。
- `src/Services/UpdateService.cs`：GitHub Releases 在线自动更新服务（REST API 优先探测 + Web 302 重定向降级更新向导弹窗与 MSI 自动下载安装）。
- `src/Services/SessionManager.cs`：会话与消息历史纯领域解耦服务，统一提供线程安全的会话管理与摘要提取。
- `Docs/GrokBot_UI_Design_System.md`：完整 GrokBot UI 设计规范与视觉对齐标准文档。
- `Tools/Compile.bat`：原生纯编译脚本，纯 ASCII 0-BOM，秒级生成 `release\LanChat.exe` 与 `release\uninstall.exe`。
- `Tools/CompileAndLink.bat`：一键编译链接并热替换覆盖至 `D:\soft\LanChat\`。
- `Tools/Publish.bat`：一键调用 WiX 5 打包 `release\LanChat.msi` 并自动上传发布至 GitHub Releases。
- `release/LanChat.exe`：原生独立免安装可执行程序 (~224 KB)。
- `release/LanChat.msi`：生成的 Windows Installer v2.0 安装包。

## 关键设计点
1. **Grok 极简现代清爽暗黑界面**：
   - 侧边栏纯黑底色 `#0E0E0E`，主聊天区 `#121212`，设置大卡片 `#1C1C1F`，输入胶囊 `#202024`，高饱和魅紫主色 `#8E64DC`，消除所有亮蓝外描边。
2. **固定侧边栏与全宽自适应聊天区**：
   - 侧边栏宽度固定为 420px，包含 82x82 Hero 吉祥物、86px 会话卡片与底部 84px 三联栏 Dock；
   - 聊天区完全自适应：底部输入胶囊横向铺满视口（高 64px、圆角 32px、左右边距 28px、底部间距 24px，字号 11.0pt 高饱和明亮白，44x44 魅紫白细向上箭头发送圆钮）；
   - 右上角贴顶无缝窗口控制按钮：单键 56x64 矩形，13px 极简矢量线框（最小化、最大化、还原、关闭）。
3. **原生弹出悬浮菜单 (`GrokPopupMenu`)**：
   - 彻底废除 Windows 原生直角 `ContextMenuStrip`；16px 圆角无边框窗体、44px 舒适行高、8px 悬停圆角卡片、纯 GDI+ 矢量图标、防溢出屏幕边缘保护。
4. **全局设置窗口 (`GrokSettingsDialog`)**：
   - 920x640 宽屏比例，220px 纯黑侧边栏自然过渡，18.0pt 加粗明亮大标题，10.5pt 加粗中性灰分组标签，54px 卡片行，22px 内部留白，32px 药丸下拉胶囊。
5. **纯矢量自绘图标与吉祥物应用图标**：
   - 全面抛弃位图资源与旧版微信图标，应用主图标 `app.ico` 全面重绘为 Grok 魅紫吉祥物正圆，包含 16~256 全尺寸 Windows 图标族。
6. **全弹窗与菜单 Grok Bot 风格 100% 对齐与任务栏图标修复**：
   - 全盘消除生硬白底 OS 弹窗，全套落地独立暗黑组件；
   - 彻底更正为“连接节点”；
   - 通过 AppUserModelID 与句柄层 `WM_SETICON` + `SetClassLongPtr` 解决任务栏图标缓存旧图标问题。
