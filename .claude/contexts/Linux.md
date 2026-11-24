# Linux.md — Linux 客户端模块上下文

> 2026-11-24 · 当前源码已完成 LC-CHAT-COMPOSER-001 Round 2 审核通过，准备生成下一版 Linux amd64/arm64 deb、便携包和 CLI。桌面 deb 包含 Electron runtime、资源、同架构自包含 ELF 和启动器，体积主要由 Electron runtime 构成。`lanchat-cli_<版本>_<架构>` 仅含同架构自包含 ELF，可直接放到对应 Linux 架构运行，但只提供终端/无头入口。Windows 宿主 Node 回归和后端编译通过，Linux ELF 实跑及 Electron 实机验收仍待用户环境。旧版状态见 [LC-LINUX-001](../../PlanDocs/LC-LINUX-001/STATUS.md)。

## 当前源码和升级样本

| 用途 | 入口 |
| --- | --- |
| 自包含 ELF 和独立 CLI | [LanChat.Linux 项目](../../src/linux/LanChat.Linux/LanChat.Linux.csproj)，当前 net10.0，源码链接共享 NetworkService 和 Models |
| Electron 首包 | [桌面源码](../../src/linux/desktop)、[构建入口](../../packaging/build-electron.py)、[集成证据](../../PlanDocs/LC-LINUX-002/INTEGRATION.md) |
| 旧 Python 桌面与 CLI | `src/linux/lanchat.py`、`src/linux/lanchat_app/`。旧公开包直接运行脚本，旧候选经 zipapp |
| 旧 Linux 打包 | [打包入口](../../packaging/build-deb.py)，公开及候选 deb 包含 `amd64` 与 `all`，其中 `all` 没有 ELF |
| 已核验样本 | `release/`、`bin/lanchat-step2/`、`bin/lanchat-handoff/`。公开版和候选版都是 v2.0.16，按载荷与 SHA-256 区分 |
| 当前试验包交接 | 本地 `bin/lc-linux-002/handoff-experiment.5/`，含两架构安装、便携及独立 CLI，以及旧 Linux、旧便携和公开 Windows 联测样本 |
| 回归证据 | [旧方案实施验证](../../PlanDocs/LC-LINUX-001/VALIDATION.md)，含 Windows 宿主回环、JSONL 互读、Ubuntu 两架构 QEMU 和 SSH 结果 |

## 已采纳的实施边界

- Electron 负责本地界面及系统集成，同架构 C# ELF 处理共享网络及会话，并保留独立 CLI。主进程经继承的 stdin/stdout 传递 UTF-8 请求和事件。第 2 步已接入共享历史、离线会话和 CLI 的昵称写回。旧便携数据和只读备用配置流程已通过隔离回归。系统包矩阵仍按 Plan 取证。
- 新版 deb 只提供 `amd64` 与 `arm64`。新版应用及 CLI 无需 Python 运行时，便携根与二进制子目录独立。旧包供升级、回退和互通验证。
- 设置优先旧程序同级已有文件。便携同级可创建时先创建，其余情况只要能创建或确认 `~/.config/lanchat` 就使用其中的 `settings.json`。仅此目录创建失败才读 `~/.local/share/lanchat/settings.json`，两份同时存在按固定顺序选择。备份后写入，旧位置只读时由用户确认可写位置。升级和回退沿用原历史、接收目录和附件路径。
- UI 以当前 Windows 客户区及 GrokBot 为基准。切片 1 的双栏、会话、聊天、文件卡片、暗色菜单和浮动滑块已通过 Round 3 代码审核（[CRDocs](../../CRDocs/STATUS.md)）。第 3 步切片 2 已实现独立八页设置窗。托盘恢复没有运行证据，关闭窗口仍是退出。原生 Wayland 自绘窗框不能完成规定操作时使用系统边框，当前默认系统边框。Xorg 与 Wayland 分别取证。
- v2.0.18 源码提供 1260×820 独立设置窗和八页，保留昵称、自动收件、目录选择/迁移、只读位置确认、主题/字体及清空历史。文件收发进度在消息气泡内逐文件更新；系统复制的文件粘贴进编辑框后成为待发送附件。主窗与设置窗复用受限 preload IPC，LANC 封包不变。
- `release/linux-electron/` 由 `Tools/Scripts/BuildLinuxDeb.py` 构建，包含 amd64/arm64 deb、portable tar.gz、独立 CLI 和 `SHA256SUMS.json`；该目录已从 Git 源码提交中忽略，作为 Release 资产发布。ARM64 实机安装与剪贴板验收由用户执行，禁止以虚拟机结果替代。
- 首包可用一个架构的 Linux 实机或虚拟机集成。正式候选要求物理 x86_64 和物理 ARM64 都完成升级、回退及与公开版 Windows 物理机的局域网互通，同时通过支持矩阵。虚拟机证据单列。
- 银河麒麟 V10 国防版（020）ARM64 的第二轮试验包因 glibc 下限与 OpenSSL 包名不可满足而留在 `iU`。系统为 glibc 2.31，旧版实际映射 OpenSSL 1.1。自第三轮起要求 `libc6 (>= 2.31)`，OpenSSL 依赖优先 3 并允许 `libssl1.1 (>= 1.1.1)`。用户已确认第五轮实机安装无报错，桌面可刷新和收发文字；第 2 步据此验收通过。该次回报未含 dpkg 状态、CLI、回退或库映射输出，不补记为已测。实机双架构升级回退和 Windows 局域网互通仍是第 4 步门槛。 [实施证据](../../PlanDocs/LC-LINUX-002/INTEGRATION.md)

[当前执行计划](../Plan.md) · [采纳定稿](../../PlanDocs/LC-LINUX-002/FINAL.md) · [共享协议](Protocol.md) · [Windows 基线](Windows.md)
