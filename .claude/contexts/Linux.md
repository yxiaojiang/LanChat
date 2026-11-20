# Linux.md — Linux 客户端模块上下文

> 2026-11-20 · 公开版 v2.0.16（a0e49c1）。LC-LINUX-002 已完成首包集成，当前试验版本为 `2.0.17~experiment.2`。旧版状态与证据见 [LC-LINUX-001](../../PlanDocs/LC-LINUX-001/STATUS.md)。

## 当前源码和升级样本

| 用途 | 入口 |
| --- | --- |
| 自包含 ELF 和独立 CLI | [LanChat.Linux 项目](../../src/linux/LanChat.Linux/LanChat.Linux.csproj)，当前 net10.0，源码链接共享 NetworkService 和 Models |
| Electron 首包 | [桌面源码](../../src/linux/desktop)、[构建入口](../../packaging/build-electron.py)、[集成证据](../../PlanDocs/LC-LINUX-002/INTEGRATION.md) |
| 旧 Python 桌面与 CLI | `src/linux/lanchat.py`、`src/linux/lanchat_app/`。旧公开包直接运行脚本，旧候选经 zipapp |
| 旧 Linux 打包 | [打包入口](../../packaging/build-deb.py)，公开及候选 deb 包含 `amd64` 与 `all`，其中 `all` 没有 ELF |
| 已核验样本 | `release/`、`bin/lanchat-step2/`、`bin/lanchat-handoff/`。公开版和候选版都是 v2.0.16，按载荷与 SHA-256 区分 |
| 当前试验包交接 | 本地 `bin/lc-linux-002/handoff-experiment.2/`，含两架构安装、便携及独立 CLI，以及旧 Linux、旧便携和公开 Windows 联测样本 |
| 回归证据 | [旧方案实施验证](../../PlanDocs/LC-LINUX-001/VALIDATION.md)，含 Windows 宿主回环、JSONL 互读、Ubuntu 两架构 QEMU 和 SSH 结果 |

## 已采纳的实施边界

- Electron 负责本地界面及系统集成，同架构 C# ELF 处理共享网络及会话，并保留独立 CLI。主进程经继承的 stdin/stdout 传递 UTF-8 请求和事件。第 2 步已接入共享历史、离线会话和 CLI 的昵称写回，旧便携数据已通过隔离兼容回归。系统包矩阵与只读配置的确认流程待验。
- 新版 deb 只提供 `amd64` 与 `arm64`。新版应用及 CLI 无需 Python 运行时，便携根与二进制子目录独立。旧包供升级、回退和互通验证。
- 设置优先旧程序同级已有文件。便携同级可创建时先创建，其余情况只要能创建或确认 `~/.config/lanchat` 就使用其中的 `settings.json`。仅此目录创建失败才读 `~/.local/share/lanchat/settings.json`，两份同时存在按固定顺序选择。备份后写入，旧位置只读时由用户确认可写位置。升级和回退沿用原历史、接收目录和附件路径。
- UI 以当前 Windows 客户区及 GrokBot 为基准。托盘或通知不可用时仍能恢复窗口。原生 Wayland 自绘窗框不能完成拖动、缩放等规定操作时使用系统边框。Xorg 和 Wayland 后端分别取证。
- 首包可用一个架构的 Linux 实机或虚拟机集成。正式候选要求物理 x86_64 和物理 ARM64 都完成升级、回退及与公开版 Windows 物理机的局域网互通，同时通过支持矩阵。虚拟机证据单列。
- 用户报告 ARM64 试验包解包后可启动、收发文字与刷新，但 `dpkg` 因依赖问题没有配置该包。试验 deb 声明 `libssl3t64 | libssl3`。现场发行版、全部缺失依赖和 .NET 加载的实际 OpenSSL 库待核实。用户自行完成实机验收，开发侧准备试验包。

[当前执行计划](../Plan.md) · [采纳定稿](../../PlanDocs/LC-LINUX-002/FINAL.md) · [共享协议](Protocol.md) · [Windows 基线](Windows.md)
