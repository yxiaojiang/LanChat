# Linux.md — Linux 客户端模块上下文

> 2026-11-27 · v3.0.0 已发布：Round 3 五项缺陷已独立审核通过，Windows/Linux 双架构资产、CLI、MSI/EXE 和 GitHub Release 已完成；真实 Linux/ELF/Electron/双机 LAN 仍待阶段 5 现场核销。

> 2026-11-26 · `LC-LINUX-003` 阶段 4 已完成并通过独立审核。Windows 宿主机上的 Node/Python 回归、更新 Mock HTTP、设置控制器桩和共享 C# 编译均通过；Linux/ELF、真实 Electron 桌面、GitHub 网络、更新权限/退锁、网卡/TUN 与双机 LAN 仍未验证，不能把宿主机桩结果写成 Linux 实机证据。

## 当前阶段 4 关键状态

- `src/linux/desktop/update-service.cjs` 已接入 GitHub Release 检查、双架构资产与嵌套清单校验、缓存权限、流式校验、原子落盘、下载互斥和失败清理。
- `main.cjs`、`preload.cjs`、`settings.js` 与 `settings.html` 已接通受限更新 IPC、进度显示、官方 Release 兜底、打开缓存目录及复制 apt 命令后退出。
- `tests/linux_desktop_update_regression.js` 已覆盖慢速分块、卡死空闲超时、`.part` 清理和真实设置控制器 DOM 桩；全部证据仍是 Windows Mock/桩。
- 真实 Linux/ELF、Electron、`XDG_CACHE_HOME` 权限、apt 退锁、`.deb` 升级/回退和便携启动统一按 `CRDocs/DEFERRED.md` 的 `LC-LINUX-003-S4-ENV-001` 现场核销。

## 当前阶段 3 关键状态

- Linux 设置页已持久化 `TunFilter`、`UnicastProbe`、`AutoLoadHistory`、`SendShortcutMode`，网络状态包含只读 `interfaces` 快照。
- 设置窗 `lanchat:refresh` 已纳入 IPC 来源白名单；刷新失败在设置页 banner 提示。
- 快捷键页前两枚徽章按 `committed.sendShortcutMode` 动态同步；语言、链接、64KB 分块能力与网络协议通道保持诚实只读。
- 真实 Linux 桌面、Xorg/Wayland/XWayland、fcitx5/ibus、双架构 ELF 和物理 LAN 现场门禁继续按 `CRDocs/DEFERRED.md` 执行。

> 2026-11-26 · Windows `LC-UI-INPUT-010` 已闭环；本次完成 Windows→Linux 功能差异审计。Linux 已有主窗聊天、会话分组/未读/搜索、文件选择/拖放/剪贴板附件、逐文件进度、表情资源、历史/删除、八页设置与共享 C# 通信服务。明确未接入或未验收项：关闭窗口仍直接退出，托盘恢复/未读预览、系统通知点击、检查更新维护入口未接入；最新 ELF 的 `delete`/`history`/`clear-all-histories`、Electron 实机、Xorg/原生 Wayland/XWayland、fcitx5/ibus、多 DPI、长历史、物理 x86_64/ARM64 升级回退及真实 Windows LAN 仍未取证。同步计划应先处理桌面集成，再按 `CRDocs/DEFERRED.md` 完成现场矩阵。

> 2026-11-25 · LC-CHAT-COMPOSER-001 Round 2 已审核通过，v2.0.20 已发布。桌面 deb 包含 Electron runtime、资源、同架构自包含 ELF 和启动器，体积主要由 Electron runtime 构成。`lanchat-cli_<版本>_<架构>` 仅含同架构自包含 ELF，可直接放到对应 Linux 架构运行，但只提供终端/无头入口。Windows 宿主 Node 回归和后端编译通过，Linux ELF 实跑及 Electron 实机验收仍待用户环境。旧版状态见 [LC-LINUX-001](../../PlanDocs/LC-LINUX-001/STATUS.md)。

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
- 2026-11-25 Gemini 已批准本轮 `DesktopService` 与共享传输加固：非有限速率归零、单帧 JSON 序列化异常隔离，Linux 文件发送向共享核心传入 `allowSourceDelete: managedPaste`，与 Windows 托管截图语义对齐。宿主只有 .NET SDK 9.0.304，net10.0 正式构建待补装 SDK；同代码使用临时 net9 目标编译通过，Linux 实机与双机联测仍延期。
- `release/linux-electron/` 由 `Tools/Scripts/BuildLinuxDeb.py` 构建，包含 amd64/arm64 deb、portable tar.gz、独立 CLI 和 `SHA256SUMS.json`；该目录已从 Git 源码提交中忽略，作为 Release 资产发布。ARM64 实机安装与剪贴板验收由用户执行，禁止以虚拟机结果替代。
- 首包可用一个架构的 Linux 实机或虚拟机集成。正式候选要求物理 x86_64 和物理 ARM64 都完成升级、回退及与公开版 Windows 物理机的局域网互通，同时通过支持矩阵。虚拟机证据单列。
- 银河麒麟 V10 国防版（020）ARM64 的第二轮试验包因 glibc 下限与 OpenSSL 包名不可满足而留在 `iU`。系统为 glibc 2.31，旧版实际映射 OpenSSL 1.1。自第三轮起要求 `libc6 (>= 2.31)`，OpenSSL 依赖优先 3 并允许 `libssl1.1 (>= 1.1.1)`。用户已确认第五轮实机安装无报错，桌面可刷新和收发文字；第 2 步据此验收通过。该次回报未含 dpkg 状态、CLI、回退或库映射输出，不补记为已测。实机双架构升级回退和 Windows 局域网互通仍是第 4 步门槛。 [实施证据](../../PlanDocs/LC-LINUX-002/INTEGRATION.md)

[当前执行计划](../Plan.md) · [采纳定稿](../../PlanDocs/LC-LINUX-002/FINAL.md) · [共享协议](Protocol.md) · [Windows 基线](Windows.md)
