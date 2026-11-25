# SessionBrief.md — 当前接手摘要

> 2026-11-25 · 工作目录 `D:\TEmp\LanChat` · Gemini 已批准文件传输加固，当前准备发布 v2.0.22

## 当前状态

LC-CC-003、LC-CC-004、LC-CC-005、LC-CC-006 的既有范围已通过独立审核或用户放行。Windows 组合 Emoji、图文剪贴板优先级、隐藏输入框首次右键菜单、选区/草稿隔离以及双端应用截图失败与退出清理均有宿主机回归证据。LANC 帧、TCP 文件确认字节、UDP 字段和 JSONL 历史格式保持兼容。

本轮新增四项：主窗体左上角 Hero 头像面板改用 `DoubleBufferedPanel`，降低拖拽缩放时背景擦除造成的闪烁；WinForms 与 Linux Electron 外观强调色统一扩展为附件图中的 11 个 RGB 预设；文件传输进度模型已有的 `SpeedMBps` 现在显示在 Windows/Linux 文件气泡中，并由 Linux `DesktopService` 透传 `speed`；群聊文件选择器支持 `MultiSimple` 多选在线成员，后续收件人使用独立截图副本，防止并发清理竞态。

本轮已运行 `friend_refresh_regression.ps1`、`chat_composer_regression.ps1 -ExpectLocalPack`、Node IPC/UI 回归与桌面语法检查、Linux Release `dotnet build`、`git diff --check`，全部通过；新增颜色、速率格式化、头像双缓冲、多选收件人及 `LC-RV-001`（0 在线好友/空快照）断言。相关手写源码及测试均低于 1000 行软门禁。

本轮新增文件传输稳定性加固：共享 NetworkService 使用 Stopwatch 单调计时并拒绝非有限速率；发送流先锁定后读取长度，严格按报价长度发送，BinaryReader/Writer 留住底层流；握手、发送、收尾确认分别采用 10 秒、30 秒、至少 60 秒超时，并为文件 Socket 设置 256KB 缓冲。出站文件改为按目标轮询的有界队列，单目标最多 2 路、全局最多 6 路；Windows 入口限制单批 32 文件和群聊 16 人，UI 进度 50ms 合批，托管截图改为引用计数共享清理。Linux IPC 对非有限速率和单帧序列化异常有兜底，托管粘贴发送参数已与 Windows 对齐。

新增 `tests/file_transfer_stability_regression.ps1` 与 C# 夹具，覆盖 0 字节、64KB、约 1MB 文件、32/16 入口边界及 6/2 并发观测；Windows 原生编译、好友刷新、聊天编辑器、并发接收和该夹具均通过。宿主仅安装 .NET SDK 9.0.304，无法直接构建目标 net10.0；使用 net9 临时目标的共享后端编译通过，正式 net10 构建仍待补装 SDK。

Gemini 已于 2026-11-25 对 `LC-FILE-TRANSFER-STABILITY-001` 独立审核并标记 `APPROVED`，确认无 P0/P1/P2 阻断；审核同时明确 Linux net10 正式构建、Linux/ELF 实机、真实双机 LAN 和桌面剪贴板仍属于延期边界。当前收口动作是同步版本与更新日志、提交并 push、执行 v2.0.22 Windows MSI/Linux 双架构打包及 Release 资产核对。

## 下一步

本轮记录已写入 `CRDocs/IMPLEMENTATION.md`、`CRDocs/REVIEW.md`、`CRDocs/STATUS.md`，并将已完成计划归档至 `Plan-archive.md`。v2.0.21 的既有发布提交为 `f09226b`；本轮 v2.0.22 尚待打包收口。真实 Windows 截图工具/Excel/WPS、长时间拖拽缩放录屏、Linux Xorg/Wayland/XWayland 与最新 ELF、双机局域网联测，以及安装 .NET 10 SDK 后的正式 Linux 构建仍未验证，按 `CRDocs/DEFERRED.md` 保留。

## 入口

- [执行计划](Plan.md) · [审核状态](../CRDocs/STATUS.md) · [实施与复审](../CRDocs/IMPLEMENTATION.md)
- [Windows 上下文](contexts/Windows.md) · [Linux 上下文](contexts/Linux.md) · [共享协议](contexts/Protocol.md)
- [Linux 打包器](../Tools/Scripts/BuildLinuxDeb.py) · [发布脚本](../Tools/Scripts/Publish.bat)
