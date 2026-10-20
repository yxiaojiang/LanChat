# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-10-20。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.0**。
> 核心里程碑：全面对标微信桌面端（WeChat Desktop Dark Mode）95%+ 视觉与交互精度，彻底消除界面所有白色割裂元素；完成 Windows 托盘后台驻留与安全退出闭环；修复 Linux deb 图标点击闪退问题，加入全链路诊断日志与终端防闪退保护。

---

## 一、 当前工作态与交付成果总结

### 1. Windows 端系统托盘驻留与安全退出闭环
- **托盘最小化拦截**：在 `MainForm_FormClosing` 中精准识别 `e.CloseReason == CloseReason.UserClosing`。点击窗口右上角关闭按钮（`X`）时自动拦截并隐藏到系统托盘（`this.Hide()`），首次最小化弹出系统气泡提示，不阻断 Windows 关机与重启。
- **左键单击/双击恢复**：绑定 `RestoreFromTray()`，执行 `Show()` + `WindowState = Normal` + `Activate()` + `BringToFront()` 组合，确保在 Win10/Win11 下稳定前置激活。
- **暗黑托盘右键菜单**：复用 `WeChatDarkMenuRenderer` 绘制纯暗黑菜单，提供【显示主界面】与【退出 LanChat】选项；左下角设置菜单亦同步加入【退出 LanChat】。
- **后台消息气泡提醒**：窗体隐藏期间收到新文本或文件时，自动通过托盘图标弹出轻量气泡提醒，点击直接唤醒主窗口。
- **构建产物刷新**：重新编译生成免安装 `LanChat.exe` 与安装包 `LanChat.msi`（含 `release\LanChat.msi`）。

### 2. Linux 端 deb 闪退根治与全链路诊断日志体系
- **根治写权限拒绝崩溃**：彻底消除 `lanchat.py` 中写死 `/usr/lib/lanchat/ReceivedFiles` 导致普通用户启动抛出 `PermissionError` 的致命缺陷；重构为智能路径探测，自动降级至用户目录 `~/Downloads/LanChat_Received` 或 `~/.local/share/lanchat/ReceivedFiles`。
- **全链路多级持久化日志**：
  - 启动器日志：`~/.local/share/lanchat/launcher.log` 及 `/tmp/lanchat_${USER}_launch.log`，记录 OS 版本、内核、架构、Glibc、Python 版本与各阶段退出码。
  - 运行日志：`~/.local/share/lanchat/lanchat_python.log` 与 `lanchat_dotnet.log`，接入全局 `sys.excepthook` 与 `UnhandledException`。
- **防闪退与终端自适应保护**：
  - 若在无 TTY 桌面环境下启动，自动探测系统安装的终端模拟器（GNOME Terminal、UKUI、Mate、Konsole、XFCE、xterm 等）并拉起交互窗口；
  - 若程序发生异常退出（非 0），终端不再瞬间关闭，而是高亮打印错误摘要与日志路径，并暂停等待回车确认，彻底解决“黑框一闪而过无法排查”的问题。
- **消除 .NET 9 ICU 依赖**：为 `LanChat.Linux` 开启 `<InvariantGlobalization>true</InvariantGlobalization>`，避免无 `libicu` 的精简 Linux 环境启动 abort。
- **构建产物刷新**：重新构建生成 `release/lanchat_2.0.0_amd64.deb`、`release/LanChat.deb` 与 `release/lanchat_2.0.0_all.deb`。

---

## 二、 关键约束与设计原则

1. **零外部运行环境依赖**：
   - Windows 端始终保持利用系统自带 `csc.exe` 秒级构建独立 EXE；MSI 安装包内置必要组件，绝不要求用户预先安装运行时。
   - Linux 端同时维护自包含原生 ELF 与纯 Python 3 标准库双轨引擎，支持各发行版及龙芯/飞腾/树莓派全架构。
2. **纯暗黑界面无白色残留**：
   - 界面中所有自绘控件及托盘弹出层均继承 `WeChatDarkMenuRenderer`，严禁使用原生刺眼白底控件。
3. **网络与协议安全边界**：
   - 纯内网 P2P 通信，魔数 `0x4C, 0x41, 0x4E, 0x43`（`"LANC"`），LEB128 变长前缀 + UTF-8 字符串编码。
   - 大文件传输固定 64KB Chunk 分块流式收发，内存占用恒定 < 1MB。

---

## 三、 模块索引

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `MainForm.cs` | 主界面核心逻辑、暗黑托盘图标管理（`_trayIcon`）、微信矢量图标系统（`WeChatIcon`）、暗黑菜单渲染器（`WeChatDarkMenuRenderer`） |
| `Models.cs` | 跨平台共用数据实体（`PeerInfo`, `ChatMessage`, `FileTransferTask`） |
| `NetworkService.cs` | 跨平台共用网络引擎（多网卡探测、UDP 发现、TCP 握手、流式分块文件收发） |
| `Package.wxs` | WiX 5 安装包定义，配置目录选择向导、快捷方式与图标绑定 |
| `Uninstall.cs` | 独立卸载向导源码，支持安全脱钩与彻底清理 |
| `build.bat` / `build-msi.bat` | Windows 免安装 EXE 与 MSI 安装包一键构建脚本 |
| `build-deb.py` | Linux 多架构标准 Debian 安装包构建脚本（内嵌防闪退启动器与日志收集） |
| `lanchat.py` | Linux 纯标准库单文件通用版（含全局日志与动态存储目录） |
| `LanChat.Linux/` | Linux 原生自包含 C# 工程（开启 InvariantGlobalization 与全局未捕获异常日志） |

---

## 四、 下一步计划

1. **实机部署反馈追踪**：在真实物理局域网环境及主流 Linux 发行版（统信 UOS / 银河麒麟 / Ubuntu）中检验托盘最小化及 deb 日志定位情况。
2. **历史记录轻量持久化（可选）**：离线历史消息本地 SQLite/JSON 存储。