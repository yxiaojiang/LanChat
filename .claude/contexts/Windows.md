# Windows.md — Windows 客户端模块上下文

## 模块定位与职责
Windows 桌面客户端，采用原生 C# WinForms 开发，定位为**零安装、无外部依赖、支持 MSI 标准向导安装与绿色单文件执行**的现代化桌面应用。界面风格深度对标 WeChat Desktop 暗黑主题（95%+ 相似度），彻底消除所有白色残留。

## 核心源码与构建物清单 (规范目录)
- `src/Program.cs`：应用程序入口，具名 Mutex（`Global\LanChat_SingleInstance_Mutex_9F24FEA2`）实现单实例互斥，广播消息唤醒已运行实例；开启高 DPI 感知（`SetProcessDPIAware`），注册全局未捕获异常兜底处理。
- `src/MainForm.cs`：主窗体核心界面控制与会话协调，包含布局容器初始化、DWM 暗黑标题栏适配、Win32 窗口消息拦截（单实例唤醒）、SafeInvoke 与 GDI+ 绘制辅助（645行）。
- `src/MainForm.Chat.cs`：会话管理与聊天交互，包含好友状态监听、会话列表自绘/筛选、文本消息收发、历史记录流转、Emoji 表情选择器、手动添加 IP 直连（468行）。
- `src/MainForm.Files.cs`：文件收发与分块跟踪，包含文件选择、拖拽监听、流式进度更新、文件接收落盘与打开文件/目录调用（268行）。
- `src/MainForm.Tray.cs`：系统托盘与生命周期，包含底层网络启动绑定、昵称修改向导、托盘图标常驻、关闭拦截防误退、暗黑托盘菜单与在线更新代理（272行）。
- `src/UI/Renderers/WeChatIcon.cs`：微信暗黑矢量图标绘制引擎（纯 GDI+ 路径计算抗锯齿自绘，涵盖气泡、通讯录、文件夹、刷新雷达、表情、剪刀截图等 13 类高保真图标）。
- `src/UI/Renderers/WeChatDarkMenuRenderer.cs`：微信暗黑风格右键弹出菜单渲染器（`WeChatDarkMenuRenderer`、`WeChatDarkMenuColorTable`）与暗黑全局浮动提示组件（`WeChatDarkTooltip`）。
- `src/UI/Controls/WeChatButtons.cs`：微信侧边栏导航按钮（`WeChatNavButton`）与无边框暗黑工具栏图标按钮（`WeChatIconButton`）。
- `src/UI/Controls/WeChatChatView.cs`：微信暗黑风格流式消息自绘视图控件（支持自发微信绿气泡、好友暗黑气泡、文件卡片点击交互、头像胶囊及防越界平滑滚动条）。
- `src/Services/UpdateService.cs`：GitHub Releases 在线自动更新服务（REST API 优先探测 + Web 302 重定向降级更新向导弹窗与 MSI 自动下载安装）。
- `src/Uninstall.cs`：独立卸载程序源码，无依赖秒级编译。
- `assets/app.ico`：256x256 至 16x16 全尺寸高精度多分辨率应用图标，微信绿双气泡现代扁平设计。
- `packaging/Package.wxs`：WiX 5 安装包定义（2.0.0 版本），定义安装路径自选、桌面/开始菜单快捷方式、统一 `app.ico` 图标绑定、ExitDialog 启动复选框。
- `packaging/publish-release.py`：GitHub Releases 自动化发布工具，一键创建 Tag 并挂载 MSI/DEB 二进制资产。
- `Tools/Compile.bat`：原生纯编译脚本，纯 ASCII 0-BOM，秒级生成 `release\LanChat.exe` 与 `release\uninstall.exe`，不改动已安装目录。
- `Tools/CompileAndLink.bat`：一键编译链接并热替换覆盖至 `D:\soft\LanChat\`。
- `Tools/Publish.bat`：一键调用 WiX 5 打包 `release\LanChat.msi` 并自动上传发布至 GitHub Releases。
- `release/LanChat.exe`：原生独立免安装可执行程序 (~139 KB)。
- `release/LanChat.msi`：生成的 Windows Installer v2.0 安装包。
- `release/uninstall.exe`：安装目录自带的卸载程序 (~7 KB)，支持安全脱钩、一键彻底清理残留。

## 关键设计点
1. **纯暗黑界面与零白边治理**：
   - `WeChatIconButton` 开启 `ControlStyles.SupportsTransparentBackColor`，背景设为透明，彻底消除搜索加号、标头及工具栏按钮默认继承的浅白底色。
   - 输入框 `_txtInput` 设置 `ScrollBars = ScrollBars.None`，消除原生 Win32 白色垂直滚动条。
   - `ContextMenuStrip` 配备定制的 `WeChatDarkMenuRenderer`，重写边距与对勾渲染，消除原生弹出菜单的左侧白边。
2. **纯自绘矢量图标系统 (`WeChatIcon`)**：
   - 彻底摒弃粗糙字符与操作系统 Emoji，纯 GDI+ 路径计算绘制：微信饱满实心气泡、专属通讯录人像卡片、3D 收藏盒子、双向刷新箭头、加号、平滑汉堡菜单、圆眼微笑顔脸、搜索放大镜等。
3. **统一精致应用图标与快捷方式**：
   - 应用程序主窗体左上角标题栏与任务栏通过 `this.Icon` 绑定 `app.ico`。
   - `LanChat.exe` 与 `uninstall.exe` 二进制文件头嵌入 `app.ico`。
   - MSI 生成的桌面快捷方式、开始菜单快捷方式、控制面板“添加/删除程序”列表统一绑定此图标。
4. **单实例互斥与呼出唤醒 (Mutex & Windows Message)**：
   - 使用系统全局 `Mutex` 限制仅能运行单个进程实例。
   - 当重复启动时，向全系统发送 `LANCHAT_WAKEUP_INSTANCE_MSG_V2` 广播，已运行实例通过 `WndProc` 捕获该消息并调用 `RestoreFromTray()` 唤醒前置激活，新进程自动静默退出。
5. **系统托盘后台驻留与安全退出闭环**：
   - 窗体初始化注册 `NotifyIcon _trayIcon` 并绑定 `app.ico`。
   - 用户点击右上角 `X`（`CloseReason.UserClosing`）时拦截关闭事件并隐藏窗体至托盘，不影响系统正常关机。
   - 左键单击/双击托盘图标前置恢复主窗口（`Show` + `WindowState.Normal` + `Activate` + `BringToFront`）。
   - 右键弹出暗黑菜单，提供【显示主界面】、【🔄 检查新版本...】与【退出 LanChat】。
   - 窗体隐藏期间收到新消息或新文件时弹出系统托盘气泡通知。
6. **窗口最小化防越界保护**：
   - 修复窗体最小化时 `Height = 0` 触发 `WeChatChatView` 重新排版导致 `_vScrollBar.Value` 越界的问题，加入尺寸守卫（`<=50` 立即跳过计算）与安全边界裁剪（Clamping）。
7. **GitHub Releases 在线自动更新（双轨架构）**：
   - 优先请求 GitHub REST API 获取详细 Changelog 与 MSI 直链；
   - 降级支持 Web 302 页面重定向解析最新 Tag，彻底免疫匿名 60次/小时 速率限制；
   - 精准识别 403（私有仓库/限流）与 404（无发布版），提供清晰引导界面。
8. **Program Files 写权限自适应**：
   - 智能探测根目录可写性：便携绿色模式使用当前目录 `ReceivedFiles`；安装在 `Program Files` 时自动将接收文件转存至用户目录 `Downloads\LanChat_Received`，杜绝访问被拒绝报错。
