# Windows.md — Windows 客户端模块上下文

## 模块定位与职责
Windows 桌面客户端，采用原生 C# WinForms 开发，定位为**零安装、无外部依赖、支持 MSI 标准向导安装与绿色单文件执行**的现代化桌面应用。
当前阶段正在基于 `feature/wechat-ui` 分支推进 WeChat 桌面端经典布局与 GrokBot 极简深邃暗黑视觉系统的深度融合（基线由 `main` 分支 Tag `v2.1.0` 锁定）。界面所有改动严格执行 `.claude/Rules.md` 第 9 条硬约束与 `Docs/GrokBot_UI_Design_System.md`。

## 核心源码与构建物清单 (规范目录)
- `src/Program.cs`：应用程序入口，具名 Mutex（`Global\LanChat_SingleInstance_Mutex_9F24FEA2`）单实例互斥，广播消息唤醒已运行实例；开启高 DPI 感知（`SetProcessDPIAware`），设置 `SetCurrentProcessExplicitAppUserModelID`，注册全局未捕获异常兜底处理。
- `src/MainForm.cs`：主窗体核心界面控制与自适应布局协调。包含无边框窗体与 DWM 阴影绘制、自适应响应式布局引擎（`UpdateResponsiveLayout`）、底部微信高输入区组装与 Win32 单实例唤醒。
- `src/MainForm.Chat.cs`：会话管理与聊天交互。包含好友状态监听、自适应会话卡片自绘（`ListSessions_DrawItem`，86px 卡片高度与有机吉祥物）、文本消息收发、输入区工具栏 6 联快捷操作、`GrokPopupMenu` 悬浮菜单弹出。
- `src/MainForm.Files.cs`：文件收发与分块跟踪。包含文件选择、拖拽监听、流式进度更新、文件接收落盘与打开文件/目录调用。
- `src/MainForm.Tray.cs`：系统托盘与生命周期。包含底层网络启动绑定、自适应 Hero 个人头像自绘（`BtnAvatar_Paint`）、昵称修改向导、托盘图标常驻、关闭拦截防误退、`GrokPopupMenu` 暗黑托盘菜单与在线更新代理。
- `src/NetworkService.cs`：网络通信门面，主类定义与生命周期管理。
- `src/NetworkService.Interface.cs`：物理网卡检测与 TUN 虚拟网卡穿透过滤。
- `src/NetworkService.Discovery.cs`：多物理网卡 UDP 广播与并发子网单播探测。
- `src/NetworkService.Transfer.cs`：LANC 二进制协议解包与 64KB 流式传输引擎。
- `src/UI/Controls/GrokPopupMenu.cs`：通用圆角浮动弹出菜单（16px圆角、44px舒适行高、8px悬停卡片、纯 GDI+ 矢量图标、全自动边界检测）。
- `src/UI/Controls/GrokSettingsDialog.cs`：Grok Bot 原生全局设置窗口（920x640，220px纯黑侧边栏无缝过渡，18pt大标题，54px卡片行，32px药丸下拉）。
- `src/UI/Controls/GrokConnectNodeForm.cs`：连接节点窗口（顶部搜索胶囊、过滤芯片、局域网在线节点推荐卡片流与手动指定 IP 直连握手）。
- `src/UI/Controls/GrokCreateNodeForm.cs`：创建新节点窗口（居中动态吉祥物预览、起名胶囊输入框、14 种有机形态网格、11 色调色盘）。
- `src/UI/Controls/GrokAboutForm.cs`：关于窗口（居中 Squircle 吉祥物徽章、版本信息与“复制版本信息”圆角药丸按钮）。
- `src/UI/Controls/GrokChangeNickForm.cs`：暗黑昵称修改窗口（吉祥物微标、暗黑圆角输入框与紫色保存按钮）。
- `src/UI/Controls/GrokFeedbackForm.cs`：发送反馈窗口（多行反馈输入框、网络诊断复选框与发送反馈按钮）。
- `src/UI/Controls/GrokUpdateForm.cs`：软件更新窗口（版本对比横幅、更新日志与平滑进度条）。
- `src/UI/Controls/GrokMessageBox.cs`：暗黑提示框（彻底取代系统生硬白底 MessageBox）。
- `src/UI/Renderers/WeChatIcon.cs`：纯 GDI+ 矢量图标与 Grok 吉祥物绘制引擎（1.8f 线宽、圆头圆角：Emoji 表情、Box 收藏抽屉盒、Folder 文件、Scissors 剪刀、Mic 麦克风、Call 电话手柄、吉祥物头像、WinMin/WinMax/WinClose 等）。
- `src/UI/Controls/WeChatButtons.cs`：无边框图标按钮（`WeChatIconButton`），扩展支持圆形按钮（`IsCircle`，34x34 悬停微光）、右上角 56x64 矩形标题栏控制按钮、自定义填充色与窗口关闭高亮模式。
- `src/UI/Controls/WeChatChatView.cs`：Grok 风格自适应居中流式聊天视图控件（840px 约束舒适阅读流、右侧自发 `#7946D7` 魅紫气泡、左侧 `#1E1E21` 深灰卡片、反引号行内代码胶囊高亮、居中时间戳对齐与平滑滚动条）。
- `src/Services/UpdateService.cs`：GitHub Releases 在线自动更新服务。
- `src/Services/SessionManager.cs`：会话与消息历史纯领域解耦服务。
- `Docs/GrokBot_UI_Design_System.md`：完整 GrokBot UI 设计规范与视觉对齐标准文档。
- `Tools/Compile.bat`：原生纯编译脚本，纯 ASCII 0-BOM，秒级生成 `release\LanChat.exe` 与 `release\uninstall.exe`。
- `Tools/CompileAndLink.bat`：一键编译链接并热替换覆盖至 `D:\soft\LanChat\`。
- `Tools/Publish.bat`：一键调用 WiX 5 打包 `release\LanChat.msi` 并自动上传发布至 GitHub Releases。

## 关键设计点与演进状态
1. **暗黑设计系统规范 (Rule 9 硬约束)**：
   - 界面严格遵循 5 级暗黑纵深：主聊天区 `#121212`、工具栏底色 `#121212`、按钮与卡片底色 `#1C1C20`（悬停 `#2E2E34`）、主色调 Grok 魅紫 `#8E64DC`；
   - 文本与排版矩阵：主输入文字 `11.0pt (15px)` `#F2F2F6`（95% 明度）；副文本与提示词 `9.5pt` `#8E8E93`（57% 明度）；设置项与按钮 `10.0pt ~ 10.5pt` Bold。严禁随意缩小字号或使用暗灰低对比度文本。
2. **方案 A：微信高输入区与 6 联圆形矢量工具栏**：
   - 底部输入区重塑为 160px 高度（顶部边框 `#232326`），内部由 42px 工具栏、多行文本输入框与底部操作条构成；
   - 工具栏配置 6 联 34x34 圆形按钮（Emoji 表情、Box 收藏、Folder 发文件、Scissors 截屏、Mic 语音、Call 通话），图标全部经由 `WeChatIcon.cs` 纯 GDI+ 1.8f 矢量自绘；
   - 彻底消除 WinForms `TextBox` 原生 Win32 白色滚动条（采用 `ScrollBars.None`），避免破坏暗黑沉浸感；
   - 右下角配置 92x32 魅紫 `#8E64DC` 发送按钮（10.0pt Bold 纯白字，圆角 6px），左侧自然排列 9.5pt `#8E8E93` 提示信息。
3. **下一步方案 B 预备架构**：
   - 引入 56px 微信式纯黑极窄左侧导航栏（Hero 头像、聊天、通讯录、收藏、发现、手机互通、底部设置菜单）；
   - 移除会话栏底部的“连接节点”与蓝扫描按钮，让 86px 会话卡片流自然纵向铺满到底部。
