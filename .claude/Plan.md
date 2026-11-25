# Plan.md — 当前活跃计划

> 2026-11-25 · `LC-CC-007` Round 3 已获 Gemini 独立审核 `APPROVED`；`v2.0.24` 已完成打包、发布与远程推送。历史完成项见 `Plan-archive.md`。

## 当前活跃事项

- [ ] 完成真实双机新旧版本互通、Windows 截图工具/Excel/WPS 粘贴及 Linux Xorg/Wayland/XWayland 联测。
- [ ] 在目标 Linux 环境补跑最新 ELF 实机运行、升级/回退、CLI、桌面收发及长时间稳定性证据。
- [ ] 对 `LC-CC-007` 做现场验收：新旧客户端表情令牌互通、双端安装包内置资源加载、实际 DPI 下微信比例浮层与输入框图片预览。

## 当前发布基线

- 版本：`v2.0.24`
- 准备提交：`15dd985`
- 发布提交：`debd9ef`
- 发布标签：`v2.0.24`
- Release：[GitHub v2.0.24](https://github.com/yxiaojiang/LanChat/releases/tag/v2.0.24)
- 说明：LC-CC-007 Round 3 审核通过；本轮 P1 修复与可选 P3 修复已包含在 `v2.0.24`。
- 更新日志：`CHANGELOG.md`
- 审核记录：`CRDocs/STATUS.md`、`CRDocs/REVIEW.md`、`CRDocs/IMPLEMENTATION.md`
- 延期边界：`CRDocs/DEFERRED.md`

入口：[审核状态](../CRDocs/STATUS.md) · [实施记录](../CRDocs/IMPLEMENTATION.md) · [Linux 集成记录](../PlanDocs/LC-LINUX-002/INTEGRATION.md) · [共享协议](contexts/Protocol.md)
