# Plan.md — 当前活跃计划

> 2026-11-24 · LC-FRIEND-REFRESH-001 Round 2 独立审核已通过。当前执行发布收口，版本和 GitHub 资产状态以本轮脚本输出为准。

## LC-FRIEND-REFRESH-001 · 发布收口与真实环境验收

- [x] Round 2 四项 P1 修复和指定 P2 修复完成，审核者独立复核通过，交接证据及隔离回归已记录至 `CRDocs/`。
- [ ] 递增发布版本，更新 `CHANGELOG.md`，运行正式构建，提交并推送工作区，发布 Windows 与 Linux 资产；核验 Release 标签、资产及 SHA-256 清单。
- [ ] 真实 Windows 多机 LAN、无 BYE 重启、Wi-Fi/有线切网、TUN 与 Linux/QEMU ELF、Electron 实机测试仍待环境执行。参见 `CRDocs/DEFERRED.md` 的 `LC-FR-ENV-001`、`LC-FR-ENV-002`、`LC-FR-007`。

## LC-LINUX-002 · Linux Electron 桌面重构

第 2 步已通过用户确认。第 3 步切片 1（主窗客户区）和切片 2（设置八页）实现及隔离回归已完成；当前构建包含两片源码。Linux 桌面实机、图形后端与系统交互验收尚未完成。证据见[集成记录](../PlanDocs/LC-LINUX-002/INTEGRATION.md)及[代码审核](../CRDocs/STATUS.md)。

### 第 3 步 · 桌面界面与系统操作验收

- [x] 实现双栏主界面、会话与聊天、文件卡片、暗色菜单、自绘滑块和独立八页设置窗；提供 Chromium/隔离回归、页面测量及双架构 ELF 构建证据。
- [ ] 用户在 ARM64 Linux 实机安装与验证收发、剪贴板文件粘贴、窗口尺寸、中文输入和设置持久化；分别记录 Xorg、原生 Wayland、XWayland。托盘/通知不可用时验证可用恢复入口。
- [ ] 完成输入法、125%/150% 同尺寸叠图、至少 2000 条历史的切换/滚动性能、托盘/通知操作验收；有系统差异时记录等效操作。缺少运行证据时本步骤保持未通过。

### 第 4 步 · 双架构候选验收

- [ ] 物理 x86_64 和物理 ARM64 各自完成旧包升级、回退、桌面/无头 CLI、端口释放；与 Windows 实机验证发现、双向私聊/群聊及双向文件 SHA-256。
- [ ] 完成核心矩阵：多网卡、TUN、无默认路由、旧公开版本互通；复核 Windows 原生构建并完成独立审核。只有承诺的实机格和核心回归都有证据时才标记通过。

## Windows 客户端后续

等待用户对 v2.0.18 MSI/EXE 的实机升级、剪贴板文件粘贴及多文件收发体验反馈；按回传证据更新状态，不代替用户声明实际安装已验证。

入口：[Linux 集成记录](../PlanDocs/LC-LINUX-002/INTEGRATION.md) · [Windows 基线](contexts/Windows.md) · [共享协议](contexts/Protocol.md)
