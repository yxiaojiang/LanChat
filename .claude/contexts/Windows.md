# Windows.md — Windows 客户端模块上下文

## 模块定位与职责
Windows 桌面客户端，采用原生 C# WinForms 开发，定位为**零安装、无外部依赖、支持 MSI 标准向导安装与绿色单文件执行**的现代化桌面应用。界面风格深度对标 WeChat Desktop 暗黑主题（95%+ 相似度），彻底消除所有白色残留。

## 核心源码与构建物清单
- `Program.cs`：应用程序入口，具名 Mutex（`Global\LanChat_SingleInstance_Mutex_9F24FEA2`）实现单实例互斥，广播消息唤醒已运行实例；开启高 DPI 感知（`SetProcessDPIAware`），注册全局未捕获异常兜底处理。
- `MainForm.cs`：主窗体，包含全套 WeChat 风格三栏暗黑 UI、自绘矢量图标系统（`WeChatIcon`）、暗黑提示框（`WeChatDarkTooltip`）、暗黑菜单（`WeChatDarkMenuRenderer`）、自绘消息流视图（`WeChatChatView`，防最小化越界保护）、暗黑更新检查向导（`ShowUpdateDialog`）、托盘右键退出。
- `Uninstall.cs`：独立卸载程序源码，无依赖秒级编译。
- `uninstall.exe`：安装目录自带的卸载程序 (~7 KB)，支持安全脱钩、一键彻底清理残留。
- `Package.wxs`：WiX 5 安装包定义（2.0.0 版本），定义安装路径自选、桌面/开始菜单快捷方式、统一 `app.ico` 图标绑定、ExitDialog 启动复选框。
- `app.ico`：256x256 至 16x16 全尺寸高精度多分辨率应用图标，微信绿双气泡现代扁平设计。
- `build.bat`：一键编译脚本，调用系统自带 `csc.exe` 秒级生成嵌入 `app.ico` 图标的绿色免安装版 `LanChat.exe` (~46 KB)。
- `build-msi.bat`：一键构建 Windows 标准向导安装包 `LanChat.msi`。
- `release/LanChat.msi`：生成的 Windows Installer v2.0 安装包。

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
7. **GitHub Releases 在线自动更新**：
   - 内置官方 API（`api.github.com/repos/yxiaojiang/LanChat/releases/latest`）异步更新检测。
   - 弹出暗黑更新向导展示新版本号、Release 描述与安装包大小，支持一键下载 MSI 并拉起更新。
8. **Program Files 写权限自适应**：
   - 智能探测根目录可写性：便携绿色模式使用当前目录 `ReceivedFiles`；安装在 `Program Files` 时自动将接收文件转存至用户目录 `Downloads\LanChat_Received`，杜绝访问被拒绝报错。
