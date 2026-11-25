# Plan.md — 当前活跃计划

> 2026-11-25 · LC-CHAT-COMPOSER-001 Round 2 已独立复审通过，v2.0.20 已提交、推送并发布。

## 发布收口

- [x] 刷新项目记忆，记录 Round 2 ` APPROVED `、验证证据、延期边界和 deb/CLI 产物差异。
- [x] 提交当前审核通过改动并推送 ` main `。Round 2 提交为 ` 9c5c0bc `，版本同步与发布提交为 ` 1e2bf84 `。
- [x] 执行 ` Tools/Scripts/Publish.bat `，生成 Windows EXE/MSI、Linux amd64/arm64 deb、双架构便携包、双架构 CLI 和 ` SHA256SUMS.json `。
- [x] GitHub Release ` v2.0.20 ` 资产已核对为已上传，arm64 便携包在发布脚本中断后单独补传成功。

## LC-CHAT-COMPOSER-001 · 实机延期验收

- [ ] 真实双机新旧版本互通、Windows 截图工具键盘粘贴、Linux Xorg/Wayland/XWayland 剪贴板、最新 ELF 实跑仍待用户环境执行。
- [ ] ` LC-CC-003 ` 至 ` LC-CC-006 ` 按 ` CRDocs/DEFERRED.md ` 的触发阶段处理。` renderer.js ` 继续保持下次扩展前先按职责拆分。

## LC-LINUX-002 · Linux 桌面验收

- [x] 双栏主界面、会话/聊天、文件卡片、暗色菜单、自绘滑块、设置八页和双架构构建已完成宿主机验证。
- [ ] ARM64 与 x86_64 实机升级、回退、CLI、桌面收发、输入法、缩放、托盘、通知和 2000 条历史性能仍待 Linux/QEMU 或物理机取证。

入口：[审核状态](../CRDocs/STATUS.md) · [实施记录](../CRDocs/IMPLEMENTATION.md) · [Linux 集成记录](../PlanDocs/LC-LINUX-002/INTEGRATION.md) · [共享协议](contexts/Protocol.md)
