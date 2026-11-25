# Plan.md — 当前活跃计划

> 2026-11-25 · 文件传输稳定性加固已获 Gemini 独立审核 `APPROVED`；记忆已归档，当前准备发布 v2.0.22。正式 Linux net10 构建与实机联测继续保留延期。

## 当前活跃事项

- [ ] 在具备 .NET 10 SDK 的环境补跑 Linux `net10.0` 后端构建，并取得 Linux/ELF 实机证据。
- [ ] 完成真实双机新旧版本互通、Windows 截图工具/Excel/WPS 粘贴及 Linux Xorg/Wayland/XWayland 联测。

## 发布收口

- [x] 刷新项目记忆，记录 Round 2 ` APPROVED `、验证证据、延期边界和 deb/CLI 产物差异。
- [x] 提交当前审核通过改动并推送 ` main `。Round 2 提交为 ` 9c5c0bc `，版本同步与发布提交为 ` 1e2bf84 `。
- [x] 执行 ` Tools/Scripts/Publish.bat `，生成 Windows EXE/MSI、Linux amd64/arm64 deb、双架构便携包、双架构 CLI 和 ` SHA256SUMS.json `。
- [x] GitHub Release ` v2.0.20 ` 资产已核对为已上传，arm64 便携包在发布脚本中断后单独补传成功。

## LC-CHAT-COMPOSER-001 · 实机延期验收

- [x] `LC-CC-005` 已完成图文混合剪贴板优先级、首次右键暗色菜单和输入框复制/剪切/全选入口，宿主机回归通过，等待独立审核。
- [x] `LC-CC-006` 已完成组合表情像素命中、跨 CRLF 复制、切会话草稿隔离/恢复和关闭清理断言，宿主机回归通过，等待独立审核。
- [ ] 真实双机新旧版本互通、Windows 截图工具/Excel/WPS 键盘粘贴、Linux Xorg/Wayland/XWayland 剪贴板、最新 ELF 实跑仍待用户环境执行。

## 本轮 Windows 稳定性与文件传输

- [x] 左上角 Hero 头像面板启用双缓冲，并由 WinForms 回归断言覆盖。
- [x] 外观强调色扩展为附件图 11 色，WinForms/Linux 索引与显示保持一致。
- [x] 文件气泡显示发送/接收速率；Linux `speed` IPC 字段与 UI 回归已覆盖。
- [x] 群聊文件支持在线成员多选，托管截图按收件人复制并沿用成功引用保留/失败清理规则。
- [x] 独立审核者复跑并回写 `CRDocs/REVIEW.md`；`LC-RV-001` 空引用已修复并通过 0 在线好友断言。
- [x] 发布脚本递增到 v2.0.21，生成 Windows/Linux 产物并同步 GitHub Release（提交 `f09226b`）；`CHANGELOG.md` 已更新。
- [ ] 真实桌面、Linux 实机、ELF 与双机联测仍延期，按 `CRDocs/DEFERRED.md` 等待现场验收。

## LC-LINUX-002 · Linux 桌面验收

- [x] 双栏主界面、会话/聊天、文件卡片、暗色菜单、自绘滑块、设置八页和双架构构建已完成宿主机验证。
- [ ] ARM64 与 x86_64 实机升级、回退、CLI、桌面收发、输入法、缩放、托盘、通知和 2000 条历史性能仍待 Linux/QEMU 或物理机取证。

入口：[审核状态](../CRDocs/STATUS.md) · [实施记录](../CRDocs/IMPLEMENTATION.md) · [Linux 集成记录](../PlanDocs/LC-LINUX-002/INTEGRATION.md) · [共享协议](contexts/Protocol.md)
