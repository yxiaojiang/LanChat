# SessionBrief.md — 当前接手摘要

> 2026-11-25 · 工作目录 ` D:\TEmp\LanChat` · ` main ` 已推送并发布 v2.0.20。

## 当前状态

LC-CHAT-COMPOSER-001 Round 2 已由 Gemini 独立复审为 `APPROVED`。Windows `ChatTextLayout.PaintText` 按 `NextBoundary` 逐可见元素推进，将 BMP 肤色组合、ZWJ 序列和 `✋`、`✨`、`☕` 等回退 Emoji 整段交给 Segoe UI Emoji。Linux Electron 预加载只对 PNG/JPEG 做内存直传，WebP、BMP、GIF 通过系统剪贴板回退，空图继续尝试下一格式。Node IPC 夹具已拆分，改动文件均低于 1000 行。`LC-CC-003` 至 `LC-CC-006` 和真实双机、Windows 系统剪贴板、Linux 实机验收仍在 `CRDocs/DEFERRED.md`。

好友刷新和 Linux 设置八页 Round 2 也已通过。好友发现按 `(IPv4, TcpPort)` 归并重启身份，刷新会重绑网卡，WinForms 标题行和滑块交互已隔离回归。Linux 设置外观持久化、窗口夹紧和历史竞态已修复。LANC 帧、TCP `0x01` 确认字节、UDP 字段及 JSONL 历史格式保持兼容。

宿主机已通过 Windows 原生编译、WinForms 回环和消息夹具、组合 Emoji 逐像素回归、Node `--check` 与 UI/IPC 模拟、Python 语法检查、Linux 共享后端 Windows 构建和 `git diff --check`。这些证据不替代真实 LAN、Linux/QEMU、Xorg/Wayland/XWayland、输入法、托盘、缩放和长历史性能验收。

Linux 桌面包包含 Electron runtime、HTML/CSS/JS、同架构自包含 .NET ELF 和资源，体积主要来自 Electron runtime。`lanchat-cli_<版本>_<架构>` 仅含同架构 ELF，适合无桌面或脚本场景，不提供 Electron GUI、设置窗口和桌面入口。`deb` 是 Debian 安装包，按架构安装桌面应用、服务 ELF、资源和启动器，可用 `apt`/`dpkg` 安装。

## 下一步

Round 2 改动提交为 `9c5c0bc`，版本同步与发布提交为 `1e2bf84`，`main` 已推送。GitHub Release `v2.0.20` 的 9 个资产均已上传，包括 Windows EXE/MSI、Linux amd64/arm64 deb、双架构便携包、双架构 CLI 和 `SHA256SUMS.json`。真实多机与 Linux 实机延期项继续保留。

## 入口

- [执行计划](Plan.md) · [审核状态](../CRDocs/STATUS.md) · [实施与复审](../CRDocs/IMPLEMENTATION.md)
- [发布脚本](../Tools/Scripts/Publish.bat) · [Linux 打包器](../Tools/Scripts/BuildLinuxDeb.py) · [Linux 模块](contexts/Linux.md) · [共享协议](contexts/Protocol.md)
