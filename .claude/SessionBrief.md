# SessionBrief.md — 当前接手摘要

> 2026-11-23 · 工作目录 `D:\TEmp\LanChat`，开发分支 `main`。公开基线 Windows `v2.0.16`（`a0e49c1`）。LC-LINUX-002 第 0 至第 2 步已归档，当前从第 3 步继续。

## 当前状态

第 0 步冻结 WinForms 原生基线、125% 下的 18 张运行截图、Windows 功能清单及 Linux 支持矩阵。第 1 步在 Ubuntu 24.04.5 amd64 QEMU 的 Xorg/Openbox 中通过安装入口启动 Electron 和真实 ELF，完成刷新、双向私聊/群聊、双向文件 SHA-256、沙盒、CLI、退出和端口释放。阶段证据见 [LC-LINUX-002 集成记录](../PlanDocs/LC-LINUX-002/INTEGRATION.md)。

新版 Linux 客户端由 Electron 45.4.1、本地 HTML/CSS/JavaScript 和同架构 .NET 10 自包含 ELF 组成。桌面与独立 CLI 共用 ELF；Windows/Linux 共用 NetworkService、Models 及现有 LANC 封包。新版 deb 只交付 amd64、arm64，无需 Python。旧 Python 公开版和 LC-LINUX-001 候选作为升级、回退与互通样本。

第 2 步已接入共享会话、离线历史、草稿、文件进度/终态、设置和 CLI 昵称写回。旧版设置选择顺序与数据路径得到保留；只读配置经备份和用户确认后才切换。旧 pyz 能读新版追加的 JSONL，原收件文件逐字节一致。双架构 ELF 隔离回归覆盖配置锁、历史迁移、非默认端口、短读、中断、拒收、最终 ACK 异常、64 MiB 传输、并发同名双哈希、重连和崩溃恢复；Ubuntu 24.04/22.04 的双架构升级、回退与重装矩阵也通过。可复跑脚本为 `tests/linux_desktop_regression.py`，日志和 VM harness 位于本地忽略目录 `bin/lc-linux-002/vm/step2-current/`。

设置路径优先同级旧配置；便携目录可写时在同级创建，其余优先 `~/.config/lanchat`，仅该配置目录创建失败才回退至 `~/.local/share/lanchat`。原设置只读时，备份后须经用户确认才切换备用文件。历史目录主动迁移逐个复制 JSONL，目标含 LanChat 历史时拒绝覆盖；旧目录及附件路径继续保留供回退版读取。包回归覆盖 `amd64 → amd64 → amd64`、`all → amd64 → all` 和 `all → arm64 → all`，均有公开版与候选版样本。

当前试验版 `2.0.17~experiment.5`。银河麒麟 V10 国防版（020）ARM64 的旧包因 glibc 2.31 和 OpenSSL 1.1 环境而被依赖阻塞；新包下限为 glibc 2.31，并允许 `libssl1.1 (>= 1.1.1)`。用户已确认第五轮实机安装无报错，桌面可刷新并收发文字，并验收第 2 步通过。该回报未含 `dpkg -l`、独立 CLI、回退或库映射输出，不登记为本次已观察。物理双架构升级/回退、无头 CLI 和 Windows LAN 互通仍是第 4 步必验项。第五轮交接为 `bin/lc-linux-002/handoff-experiment.5/`，旧 `.2` 至 `.4` 目录均保留。

## 下一步

从第 3 步开始，按 Windows 清单实现双栏主界面、会话与聊天、文件卡片、弹层、八个设置页、维护入口及托盘/通知。逐项保存运行截图和操作证据，并回归缩放、窄窗、中文 IME、焦点、长历史、浮动滑块和性能。记录 Xorg、原生 Wayland、XWayland 的实际后端；托盘/通知不可用时保留窗口恢复和应用内未读，Wayland 自绘窗框无法完成规定操作时改用系统边框。

第 3 步按模块逐项交付，优先复用现有 Electron 主进程、预加载桥接和渲染器结构；动手前读取 Windows 基线及 GrokBot 设计规范，并遵守 Linux 手写源码行数上限。虚拟桌面只能作为回归证据，不能替代用户计划中的物理机验收。截图应能对应具体清单项和运行环境。

实机由用户验收，开发侧负责实现、可复现回归、打包和按证据更新 Plan。证据缺失的必验项保持未通过。Windows Electron 在 Linux 验收后另议；正式发布需另行授权。

## 入口

- [执行计划](Plan.md) · [阶段归档](Plan-archive.md) · [采纳定稿](../PlanDocs/LC-LINUX-002/FINAL.md)
- [Linux 模块](contexts/Linux.md) · [共享协议](contexts/Protocol.md) · [Windows 基线](contexts/Windows.md)
