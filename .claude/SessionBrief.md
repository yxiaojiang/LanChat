# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-10-20。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.0.0**。
> 核心里程碑：全面对标微信桌面端（WeChat Desktop Dark Mode）95%+ 视觉与交互精度，彻底消除界面所有白色割裂元素；完成 Windows 托盘后台驻留与安全退出闭环；根治 Windows 最小化异常；加入全局单实例互斥与唤醒机制；集成 GitHub Releases 在线自动更新检查；为 Linux 端打造 0 依赖微信暗黑原生 GUI（Tkinter 双轨），实现双端界面交互完全统一。

---

## 一、 本轮工作态与交付成果总结

### 1. Windows 端最小化异常根治与单实例互斥唤醒
- **滚动条越界异常根治**：定位到窗口最小化时 `ClientRectangle.Height == 0` 引发 `WeChatChatView` 重排布局，导致 `_vScrollBar.Value` 赋值越界抛出 `ArgumentOutOfRangeException`。在 `RecalculateLayout` 中加入尺寸守卫（`<= 50` 立即返回）、`Math.Min/Max` 边界夹紧并包裹 `try-catch`。
- **全局单实例互斥（Mutex）**：在 `Program.Main` 中使用 `Mutex("Global\\LanChat_SingleInstance_Mutex_9F24FEA2")`。若已有实例在运行，通过系统广播消息 `RegisterWindowMessage("LANCHAT_WAKEUP_INSTANCE_MSG_V2")` 唤醒已存在的实例并前置展示，当前进程安全退出，彻底避免多开冲突。
- **WndProc 唤醒响应**：在 `MainForm.WndProc` 中监听该唤醒消息，自动调用 `RestoreFromTray()` 从系统托盘或最小化状态恢复并激活到前台。

### 2. GitHub Releases 在线检测与更新机制
- **GitHub 作为更新服务器**：配置官方标准 REST API（`https://api.github.com/repos/yxiaojiang/LanChat/releases/latest`），免自建服务器，发布 Release 即可作为全网更新源。
- **Windows 端暗黑更新体验**：在左下角设置菜单与托盘右键菜单中均增加【🔄 检查新版本...】。异步请求 GitHub API，弹出微信暗黑风格的更新向导对话框，支持查看更新日志 Changelog，一键高速下载最新 `LanChat.msi` 并拉起安装升级。
- **Linux 端版本检测同步**：在 `lanchat.py` 中同样集成 GitHub Releases 版本拉取与更新提示。

### 3. Linux 端 deb 微信暗黑原生 GUI（双端界面统一）
- **0 额外依赖 Tkinter 原生微信暗黑界面**：在 `lanchat.py` 中使用 Python 3 标准库 `tkinter` 像素级复刻 Windows 端微信暗黑 3 栏界面（导航栏、会话列表栏、聊天对话栏），支持暗黑消息气泡、头像胶囊、进度条、发送文件、群聊/私聊切换等，免去 PyQt/Electron 臃肿负担。
- **桌面快捷方式去终端化**：将 `lanchat.desktop` 改为 `Terminal=false`，桌面点击图标不再弹出黑框终端，直接以原生 GUI 呈现。
- **启动器自适应环境调度**：`/usr/bin/lanchat` 在图形桌面（`DISPLAY`/`WAYLAND_DISPLAY`）下优先启动微信暗黑 GUI；若无交互终端且缺少 `python3-tk`，自动调用 `zenity`/`notify-send` 友好提示 `sudo apt install python3-tk`；若在纯 SSH/无头服务器或传入 `--cli` 时，平滑降级至交互式 CLI 控制台。
- **deb 依赖配置优化**：在 `control` 中声明 `Depends: python3`，`Recommends: python3-tk`，兼顾 `apt` 自动补全组件与 `dpkg -i` 零阻塞安装。

### 4. 构建物全量刷新与验证
- Windows 端：绿色版 `LanChat.exe` (~46 KB) 与安装包 `LanChat.msi` / `release/LanChat.msi` (~765 KB) 重新编译通过。
- Linux 端：`release/lanchat_2.0.0_amd64.deb` (~6 MB，含自包含 ELF + Python 双核)、`release/LanChat.deb` 与 `release/lanchat_2.0.0_all.deb` (~35 KB，通用架构纯标准库版) 重新打包生成。

---

## 二、 关键约束与设计原则

1. **以最小实现、零冗余依赖为荣**：
   - Windows 端保持系统自带 `csc.exe` 秒级编译，无任何外部运行库依赖。
   - Linux 端采用 Python 标准库实现微信暗黑 GUI，兼具跨发行版与跨 CPU 架构兼容性（x86_64、ARM64、龙芯、飞腾）。
2. **纯暗黑界面无白色残留**：
   - Windows WinForms 与 Linux Tkinter 统一使用微信桌面暗黑配色（侧边栏 `#252526` / `#2e2e2e`、会话列表 `#202021` / `#252526`、激活高亮 `#07c160`、消息底色 `#1e1e1f`、气泡 `#07c160` 与 `#2c2c2e`）。
3. **网络与协议安全边界**：
   - 纯内网 P2P 通信，魔数 `0x4C, 0x41, 0x4E, 0x43`（`"LANC"`），LEB128 变长前缀 + UTF-8 字符串编码。
   - 大文件传输固定 64KB Chunk 分块流式收发，内存占用恒定 < 1MB。

---

## 三、 模块索引

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `Program.cs` | 应用程序入口，单实例互斥（Mutex）与全局消息广播唤醒，高 DPI 感知 |
| `MainForm.cs` | 主界面核心逻辑、托盘图标、暗黑渲染器、GitHub 在线更新检查对话框、消息视图防越界保护 |
| `Models.cs` | 跨平台共用数据实体（`PeerInfo`, `ChatMessage`, `FileTransferTask`） |
| `NetworkService.cs` | 跨平台共用网络引擎（多网卡探测、UDP 发现、TCP 握手、流式分块文件收发） |
| `Package.wxs` | WiX 5 安装包定义，配置目录选择向导、快捷方式与图标绑定 |
| `Uninstall.cs` | 独立卸载向导源码，支持安全脱钩与彻底清理 |
| `build.bat` / `build-msi.bat` | Windows 免安装 EXE 与 MSI 安装包一键构建脚本 |
| `build-deb.py` | Linux 多架构标准 Debian 安装包构建脚本（内嵌桌面自适应启动器与日志收集） |
| `lanchat.py` | Linux 纯标准库微信暗黑 GUI 与 CLI 双模通用版 |
| `LanChat.Linux/` | Linux 原生自包含 C# 工程（.NET 9 InvariantGlobalization 开箱即用） |

---

## 四、 下一步计划

1. **版本发布与 Release 验证**：在 GitHub 上发布 v2.0.0 Release，上传 `LanChat.msi` 与 `LanChat.deb`，验证客户端点击【🔄 检查新版本...】拉取与更新全流程。
2. **实机部署反馈追踪**：在真实物理多网卡环境及统信 UOS / 银河麒麟实机中检验 GUI 启动及文件流式互传性能。
