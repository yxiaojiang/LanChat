# Linux.md — Linux 客户端模块上下文

## 模块定位与职责
Linux 客户端支持在各种 Linux 发行版（Ubuntu、Debian、CentOS、统信UOS、银河麒麟、Arch、树莓派 OS、嵌入式 Linux）上运行。具备标准 deb 安装包、预编译自包含独立 ELF 二进制与纯 Python 单文件三种形态。**全面实现与 Windows 端高度一致的微信暗黑桌面 GUI，彻底解决“桌面点击图标只能启动黑框终端”的问题，且保持 0 外部重型依赖**。

## 源码与构建物清单
1. **Debian 标准安装包 (`.deb`)**：
   - `release/LanChat.deb` / `release/lanchat_2.0.0_amd64.deb`：专为 x86_64 打造，内置自包含二进制与 Python WeChat GUI 双核，带 `.desktop` 快捷方式与最新 `lanchat.svg` / `app.png` 矢量图标。
   - `release/lanchat_2.0.0_all.deb`：全架构通用包（纯 Python 标准库）。
   - `build-deb.py`：纯 Python 构建 deb 包工具（v2.0 规格）。
2. **纯标准库微信暗黑原生 GUI (`lanchat.py`)**：
   - 基于 Python 3 标准库 `tkinter` 像素级自绘，复刻 Windows 微信暗黑三栏布局（侧边导航栏、会话列表、聊天对话流、消息气泡、发送文件、流式进度等），免除 PyQt/Electron 等数百 MB 臃肿依赖。
   - 包含 GitHub Releases 在线新版本检测。
   - 自动检测运行环境：若处于图形桌面环境且 `tkinter` 可用，优先启动 GUI；若在 SSH/无头服务器或指定 `--cli`，平滑回退至命令行交互模式。
3. **预编译自包含独立二进制（ELF）**：
   - `release/linux-x64/lanchat`：针对 Intel / AMD 64位架构，单文件自带 Runtime（`--self-contained true`）。
   - `release/linux-arm64/lanchat`：针对 aarch64 架构（树莓派 3/4/5、Jetson、RK3588、麒麟 ARM 服务器）。
4. **图标与桌面项规范**：
   - `lanchat.svg` / `app.png`：Linux 专用矢量双气泡图标。
   - `lanchat.desktop`：配置 `Terminal=false`，桌面双击图标直接启动无黑框图形桌面客户端。
5. **智能启动器与日志保障机制 (`/usr/bin/lanchat`)**：
   - **环境探测调度**：检测 `$DISPLAY` 或 `$WAYLAND_DISPLAY`。在桌面环境下优先运行 `python3 lanchat.py`；若系统缺少 `python3-tk`，自动唤起 `zenity` 或 `notify-send` 弹窗友好提示 `sudo apt install python3-tk`。
   - **终端自适应**：仅在终端交互模式且缺失 TTY 时，才自适应拉起系统终端模拟器（GNOME Terminal/UKUI/Mate/Konsole/XFCE 等）。
   - **全链路诊断日志**：启动器日志写入 `~/.local/share/lanchat/launcher.log` 及 `/tmp/lanchat_${USER}_launch.log`；Python 与 .NET 异常写入 `lanchat_python.log` 与 `lanchat_dotnet.log`。

## 常用终端指令映射 (CLI 降级模式)
- `/list` 或 `/peers`：打印局域网在线主机表格。
- `/chat <编号|IP|all>`：切换私聊目标（`0` 或 `all` 为群聊广播）。
- `/send <文件路径>`：流式发送二进制文件。
- `/add <IP>`：手动发起 TCP 握手添加指定 IP。
- `/refresh`：触发网段扫描。
- `/nick <新昵称>`：全网同步改名。
