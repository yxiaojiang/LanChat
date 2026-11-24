# Plan.md — 当前活跃计划

> 2026-11-24 · LC-CHAT-COMPOSER-001 Round 2 已独立复审通过，进入提交、双架构打包和 GitHub Release 收口。

## 本轮发布收口

- [x] 刷新项目记忆，记录 Round 2 ` APPROVED `、验证证据、延期边界和 deb/CLI 产物差异。
- [ ] 提交当前审核通过改动并推送 ` main `。
- [ ] 执行 ` Tools/Scripts/Publish.bat `，生成 Windows EXE/MSI、Linux amd64/arm64 deb、便携包、CLI 和 ` SHA256SUMS.json `。
- [ ] 核对 GitHub Release 版本、资产大小、架构命名和 SHA-256，回写记忆中的实际提交与发布结果。

## LC-CHAT-COMPOSER-001 · 实机延期验收

- [ ] 真实双机新旧版本互通、Windows 截图工具键盘粘贴、Linux Xorg/Wayland/XWayland 剪贴板、最新 ELF 实跑仍待用户环境执行。
- [ ] ` LC-CC-003 ` 至 ` LC-CC-006 ` 按 ` CRDocs/DEFERRED.md ` 的触发阶段处理。` renderer.js ` 继续保持下次扩展前先按职责拆分。

## LC-LINUX-002 · Linux 桌面验收

- [x] 双栏主界面、会话/聊天、文件卡片、暗色菜单、自绘滑块、设置八页和双架构构建已完成宿主机验证。
- [ ] ARM64 与 x86_64 实机升级、回退、CLI、桌面收发、输入法、缩放、托盘、通知和 2000 条历史性能仍待 Linux/QEMU 或物理机取证。

入口：[审核状态](../CRDocs/STATUS.md) · [实施记录](../CRDocs/IMPLEMENTATION.md) · [Linux 集成记录](../PlanDocs/LC-LINUX-002/INTEGRATION.md) · [共享协议](contexts/Protocol.md)
