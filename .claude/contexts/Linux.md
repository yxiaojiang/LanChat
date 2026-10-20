# Linux.md — Linux 客户端模块上下文

## 模块定位与职责
Linux 客户端支持在各种 Linux 发行版（Ubuntu、Debian、CentOS、Arch、树莓派 OS、嵌入式 Linux）上运行，兼顾无桌面的服务器/SSH 环境以及图形桌面环境。提供预编译二进制与纯 Python 单文件两种方式。

## 源码与构建物清单
1. **预编译独立二进制（ELF）**：
   - `release/linux-x64/lanchat`：针对 Intel / AMD 64位架构，单文件自包含（Self-contained）。
   - `release/linux-arm64/lanchat`：针对 aarch64 架构（树莓派 3/4/5、Jetson、RK3588、ARM 服务器）。
2. **C# 终端工程**：
   - `LanChat.Linux/LanChat.Linux.csproj`：通过链接引用直接复用根目录的 `Models.cs` 和 `NetworkService.cs`。
   - `LanChat.Linux/Program.cs`：交互式 ANSI 彩色终端 UI，支持命令行会话切换、彩色消息格式化与 ASCII 传输进度条。
   - `LanChat.Linux/build-linux.bat`：一键交叉编译脚本。
3. **通用单文件版**：
   - `lanchat.py`：纯 Python 3 标准库实现（无需 `pip install`），全架构兼容。

## 常用终端指令映射
- `/list` 或 `/peers`：打印局域网在线主机表格。
- `/chat <编号|IP|all>`：切换私聊目标（`0` 或 `all` 为群聊广播）。
- `/send <文件路径>`：流式发送二进制文件。
- `/add <IP>`：手动发起 TCP 握手添加指定 IP。
- `/refresh`：触发网段扫描。
- `/nick <新昵称>`：全网同步改名。
