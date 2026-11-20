# LC-LINUX-002 · Linux 首包集成

试验版本 `2.0.17~experiment.1`。构建使用 .NET SDK 10.0.404、运行时 10.0.15 与 Electron 45.4.1。两份 Electron 官方 ZIP 按各自 SHA-256 锁定，应用的本地 HTML、CSS、JavaScript 无需 npm 依赖。试验构建独立于旧 Python 包和正式发布脚本。 [Electron 版本](https://releases.electronjs.org/release/v45.4.1) · [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

本地输出为 `bin/lc-linux-002/experiment/`。每种架构包含一个 deb、一个桌面便携 tar.gz 及一个可独立运行的 ELF。文件名及 SHA-256 见同目录 `SHA256SUMS.json`。从 deb 的实际 data tar 回读二进制，Electron 和通信 ELF 在 amd64 包均为 ELF64、机器码 62，在 arm64 包均为 ELF64、机器码 183。两份 deb 的实际控制字段与载荷均无 Python 运行时。

## 构建入口

1. 通过 .NET 10 SDK 运行 `python packaging/build_linux.py bin/lc-linux-002/data-elf`。构建结果分别放在 `linux-x64/lanchat` 和 `linux-arm64/lanchat`。当前工作树的第 2 步构建使用新试验版本目录，保留本次集成的 `experiment/` 原始产物。
2. 在 Windows 用系统 `csc.exe` 编译 [图标导出器](../../packaging/ExportLinuxDesktopAssets.cs)，引用当前原生编译的 `LanChat.exe` 和 `System.Drawing.dll`，将生成的导出器置于该 `LanChat.exe` 同级运行并传入 PNG 输出目录。渲染器几何来自 WinForms。
3. 将官方 `electron-v45.4.1-linux-x64.zip` 和 `electron-v45.4.1-linux-arm64.zip` 放入同一缓存路径。运行下列本地打包命令。

```powershell
python packaging/build-electron.py --output bin/lc-linux-002/data-experiment --archives bin/lc-linux-002/cache --elf bin/lc-linux-002/data-elf --icons bin/lc-linux-002/icons
```

系统安装保留包名 `lanchat` 和 `Exec=lanchat`。图形环境默认打开桌面，`--cli`、`-c` 和无图形会话直接执行 ELF。便携包解压后运行根目录 `./lanchat`，根目录用于沿用旧同级设置。桌面运行需要系统图形库和可用的 Chromium 沙盒。系统 deb 的 `chrome-sandbox` 为 root:root、4755。

## 集成环境

QEMU 10.2.0 TCG，Ubuntu Minimal 24.04.5 amd64；基于旧实验 VM 的独立写时复制盘。GUI 使用 Xorg、Openbox，屏幕 1280×800。虚拟机的安装启动只计首包集成。与公开 Windows 的物理互通、正式升级／回退、GNOME 图形后端矩阵，以及中文输入法仍按计划逐项验收。

- APT 在虚拟机中把现有 LC-LINUX-001 候选 amd64 2.0.16 变更为试验 2.0.17，核对新包版本和沙盒辅助程序权限。记录保存在来宾 `~/lc002/evidence/`。
- ELF 从直接入口输出 `ready` 协议 1、UDP 38888、TCP 38889，随后 `state` 和 `quit` 均返回有效 JSON。
- SSH 无 TTY 运行安装命令，输入 `/chat all`、`/refresh`、`/quit` 成功。附带图形环境变量时，安装命令的 `--cli` 和 `-c` 仍直接进入 CLI，退出后监听端口释放。
- 原工作树的 22 项旧 Python／共享网络回归通过。
- 从 `gio launch /usr/share/applications/lanchat.desktop` 打开真实 Electron 窗口，客户区 1040×720。应用服务和对端 ELF 分别在来宾的两个 veth 命名空间里（`10.77.2.1/29`、`10.77.2.2/29`）。验证自动发现、点击刷新、双向中文群聊与私聊。普通点击系统 GTK 文件选择器可发送。
- 双向文件均为 311071 字节，源件与接收件 SHA-256 均为 `0e8316c345e411129ae83a0f1ef404d23fd7a34fabf4d001aed93e45317b16e6`。收件结束后对端收到最终 ACK，桌面和对端均显示接收文件。点击「退出 LanChat」后对端移除该节点，退出后的 `ss` 结果中无 UDP 38888 或 TCP 38889。
- 渲染器 `/proc/.../status` 实测 `NoNewPrivs: 1`、`Seccomp: 2`，程序未附带关闭沙盒的参数。双方传输完成后单次 PSS 采样中，桌面 ELF 为 28444 KiB，已采样的 Electron 进程及该 ELF 共 315076 KiB，未计对端；这组数值来自 TCG 来宾热态。
- 本地原始记录见 `bin/lc-linux-002/vm/evidence/`，包括 `integration.log`、`peer.jsonl`、`file-transfer.png`、`sockets-before-quit.txt`、`sockets-after-quit.txt` 和渲染器状态。ARM64 只完成构建及包内机器码与摘要核验。

## 第 2 步已开始

- 旧 WinForms 基线的共享接收核心在两路交错发送同名异内容文件时只保留一份，最终 ACK 为 `1, -1`，完成事件数为 1。修复为每路独立临时文件，串行提交时选取可用的文件名。原生 Windows 构建回归中，最终 ACK 为 `1, 1`，完成事件数为 2，两个落盘文件分别匹配源件 SHA-256 `58bb119c35513a451d24dc20ef0e9031ec85b35bfc919d263e7e5d9868909cb5` 和 `fc686c314491e1f68bf1899fc54b2327353c44dd1ab4ed56538ef623edd1e866`。证据在 `bin/lc-linux-002/baseline/windows/concurrent-receive-before.txt` 和 `bin/lc-linux-002/regression/windows/concurrent-receive-after.txt`。
- 当前源码已接入共享 `SessionManager` 和 `ChatHistoryService`，历史就地读取及追加，按确认 IPv4 归档、按现行节点 ID 和监听端口发包。Linux CLI `/nick` 在原位置备份并写回设置，保留未知键。第 1 步试验包已冻结，第 2 步构建另存。
- 用保全的便携样本副本做隔离兼容回归。旧候选 pyz 通过 LANC 发来带引号、反斜线、换行的中文和第二条群聊，重启新版后继续读到共三条历史。旧 pyz 也读到全部三条、更新后的昵称和未知字段。原接收文件及写前设置备份逐字节一致。记录在 `bin/lc-linux-002/vm/evidence/data-regression.log`。系统包升级／回退、旧 GUI 与双架构实机仍按 Plan 验收。
- `2.0.17~experiment.2` 的两架构 deb、便携包和独立 ELF 位于 `bin/lc-linux-002/data-experiment/`。复读 deb 核对两类二进制的架构与摘要。更新后的 amd64 桌面在同一 QEMU 环境复跑了首包链路，双向聊天、双向 SHA-256、沙盒和退出仍通过。该复跑用于验证试验包集成。
- 最新 ELF 的隔离配置回归确认同级已有文件优先、配置和数据两个目录共存时使用配置目录、仅配置目录创建失败时回退、只读原文件保持原字节。当前只读时报告写入错误，选择可写位置的确认流程待接入。复跑原始记录在 `bin/lc-linux-002/vm/experiment.2/evidence/`。
