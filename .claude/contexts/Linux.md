# Linux.md — Linux 客户端模块上下文

## 模块定位与职责
Linux 客户端支持在各种 Linux 发行版（Ubuntu、Debian、CentOS、统信UOS、银河麒麟、Arch、树莓派 OS、嵌入式 Linux）上运行。具备标准 deb 安装包、预编译自包含独立 ELF 二进制与纯 Python 单文件三种形态，**彻底解决无需预装 .NET 运行时即可独立运行的诉求**。

## 源码与构建物清单
1. **Debian 标准安装包 (`.deb`)**：
   - `release/LanChat.deb` / `release/lanchat_2.0.0_amd64.deb`：专为 x86_64 打造，内置自包含二进制与 Python 双核，带 `.desktop` 快捷方式与最新 `lanchat.svg` / `app.png` 矢量图标。
   - `release/lanchat_2.0.0_all.deb`：全架构通用包。
   - `build-deb.py`：纯 Python 构建 deb 包工具（v2.0 规格）。
2. **预编译自包含独立二进制（ELF）**：
   - `release/linux-x64/lanchat`：针对 Intel / AMD 64位架构，单文件自带 Runtime（`--self-contained true`）。
   - `release/linux-arm64/lanchat`：针对 aarch64 架构（树莓派 3/4/5、Jetson、RK3588、麒麟 ARM 服务器）。
3. **通用单文件版**：
   - `lanchat.py`：纯 Python 3 标准库实现（无需 `pip install`），全架构兼容。
4. **图标资源**：
   - `lanchat.svg`：Linux 专用矢量双气泡图标，打包至 `/usr/share/icons/hicolor/scalable/apps/lanchat.svg`。
   - `app.png`：256x256 高清图标，打包至 `/usr/share/icons/hicolor/256x256/apps/lanchat.png`。

## 常用终端指令映射
- `/list` 或 `/peers`：打印局域网在线主机表格。
- `/chat <编号|IP|all>`：切换私聊目标（`0` 或 `all` 为群聊广播）。
- `/send <文件路径>`：流式发送二进制文件。
- `/add <IP>`：手动发起 TCP 握手添加指定 IP。
- `/refresh`：触发网段扫描。
- `/nick <新昵称>`：全网同步改名。