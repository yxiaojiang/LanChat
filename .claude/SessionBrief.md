# SessionBrief.md — 当前接手摘要

> 2026-11-20 · 工作目录 D:\TEmp\LanChat，开发分支 main。公开版基线 v2.0.16 对应 a0e49c1。

## 当前状态

唯一活跃事项是 LC-LINUX-002 Linux Electron 桌面重构。Grok 第 2 轮审核收敛，用户采纳定稿及启动补充。实施尚未开始。Linux 首先重做当前 Windows 版的应用内样式及真实功能，涵盖双栏、会话和聊天、节点与个人资料、文件传输、分类设置、维护入口和可恢复的窗口托盘操作。Windows Electron 等 Linux 验收后另行评估。正式发布按项目指定入口和用户授权执行。

Electron 用本地 HTML、CSS、JavaScript 负责桌面，现有 Linux C# 自包含 ELF 增加桌面服务模式。两端继续共享 NetworkService 和 Models，保留现有局域网 LANC 帧、端口语义、文件流和完成 ACK。Electron 与 ELF 经继承的标准输入输出传递结构化控制和事件，文件由 ELF 流式处理。显式 CLI 和无头入口直接进入独立 ELF。同一数据配置由一个新版进程写入，旧版本与新版同一配置的升级演练先退出正在运行的进程。

新版安装和便携运行时无需 Python，deb 只交付 amd64 与 arm64。旧公开包、候选 pyz 和 C# ELF 用于升级、回退和协议互通。设置沿旧入口固定优先级：同级已有就使用，便携同级可写时先创建，其余情况优先 ~/.config/lanchat/settings.json。只有配置目录创建失败才回退到 ~/.local/share/lanchat/settings.json，两处共存自动按优先级选定。首次改写备份，实际不可写由用户确认新位置。升级、回退就地保留原历史目录、接收文件和路径。旧候选应能读原有及新版追加的兼容历史；更改历史目录是用户另行操作。

旧 LC-LINUX-001 完成了候选构建、宿主回环及 Ubuntu QEMU 检查，证据没有覆盖物理双机或 Linux 桌面。保留当前工作树已有改动。公开包在 release/，本机候选在 bin/lanchat-step2，交接 ZIP 在 bin/lanchat-handoff。新版仍需实测 apt 架构替换、回退 GUI、并发同名收件，以及在 Wayland 上恢复窗口和进行中文输入。原生 Wayland 自绘边框不能完成拖动、缩放等规定操作时直接采用系统边框，客户区继续按 Windows 和 GrokBot 对齐。

验收要分别记录源件摘要、包内架构、桌面实际后端和物理局域网环境。共享网络的文字端口保留和文件完工语义在迁移时复跑，若并发同名收件复现冲突，修复共享落盘路径并回归 Windows 原生构建。

## 下一步

从第 0 步固定 Windows 实际构建、截图和操作清单，保全旧样本，并冻结支持矩阵与测量口径。然后打通第 1 步的 Linux 安装入口、Electron、真实 ELF、聊天、文件、退出和无头 CLI。单架构实机或虚拟机可完成首包集成，同时生成两个架构的试验包。第 2 步做数据、文件与包升级回退，第 3 步完整对齐界面和桌面行为。第 4 步必须有物理 x86_64 和物理 ARM64 各自的升级回退、以及分别与物理 Windows v2.0.16 的局域网互通。承诺的核心回归与实机格缺通过证据即候选未通过。用真实证据更新完成状态。

## 入口

- [执行计划](Plan.md) · [采纳定稿](../PlanDocs/LC-LINUX-002/FINAL.md) · [审核补充](../PlanDocs/LC-LINUX-002/REVIEW-R2.md)
- [Linux 模块](contexts/Linux.md) · [Windows 基线](contexts/Windows.md) · [共享协议](contexts/Protocol.md)
