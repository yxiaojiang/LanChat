# SessionBrief.md — 当前接手摘要

> 2026-11-25 · 工作目录 `D:\TEmp\LanChat` · v2.0.22 已发布，重复接收气泡修复待 Gemini 复核

## 当前状态

本轮 `LC-FILE-TRANSFER-STABILITY-001` 已完成 P0/P1/P2 实施并获 Gemini 独立审核 `APPROVED`，无 P0/P1/P2 阻断。LANC 帧、TCP 文件确认字节、UDP 字段和 JSONL 历史格式保持兼容；全中文注释与单文件小于 1000 行约束已检查。

用户实测最新版发现对端只发送一次文件时，本机偶发出现完成文件卡片和第二个“接收完成 · 100%”进度卡片。根因是 `FileTransferController` 的 50ms 进度合并队列与 `OnFileReceived` 完成回调存在先后竞态：完成回调先到时追加完整消息，延迟进度随后因找不到 `_incoming` 又新建消息。修复已在完成回调接管同一 `FileTransferId` 前移除尚未刷新的待处理进度，未改变 LANC 封包或历史格式。

共享 `NetworkService` 使用 `Stopwatch` 单调计时并拒绝非有限速率；发送流先锁定文件长度，严格按报价长度发送，`BinaryReader/Writer` 保留底层流；握手、发送、收尾确认分别使用 10 秒、30 秒、至少 60 秒动态超时，文件 Socket 缓冲为 256KB。出站文件采用按目标轮询的有界队列，单目标最多 2 路、全局最多 6 路；Windows 入口限制单批 32 文件和群聊 16 人，进度 50ms 合批，托管截图使用引用计数清理。Linux IPC 对非有限速率和单帧序列化异常兜底，并与 Windows 对齐托管粘贴参数。

新增边界夹具覆盖 0 字节、64KB、约 1MB、32/16 入口边界、6/2 并发限流和并发接收。Windows 原生编译、好友刷新、聊天编辑器、文件传输稳定性、并发接收、Node IPC/UI 回归及 `git diff --check` 均通过。发布时使用缓存的 .NET SDK 10.0.404 完成 Linux net10.0 双架构构建和打包；宿主系统默认 SDK 仍为 9.0.304。

本轮实现保持外科式修改，没有改变既有魔数、封包字段、确认字节、历史字段或跨端入口。发送端在打开文件后重新确认长度，接收端继续按报价长度写入并等待最终确认；流包装器不关闭底层网络流，避免复用连接被提前释放。超时使用单调时钟和有限范围，进度计算在异常样本下回退为零，避免界面显示无穷或非数字。队列调度按目标轮询，拒绝超出容量的新增任务，并在服务退出时释放排队项。

Windows 侧将大批量文件与群聊收件人限制在明确边界内，进度通知合并后再刷新界面，降低高吞吐传输时的跨线程调用压力。托管截图副本按引用计数管理，最后一个引用完成或失败后才删除临时文件；原始用户文件不纳入自动清理。Linux 桌面侧同步处理有限速率、序列化失败和托管粘贴参数，保证异常不会破坏 JSONL IPC 管道。

审核结论确认本次修复没有发现新的 P0、P1 或 P2 问题，延后项属于真实环境证据而非当前宿主机代码阻断。后续若补做现场验收，应优先覆盖新旧版本互传、发送中断、0 字节文件、多个目标公平性、重启后历史记录，以及 Wayland/XWayland 剪贴板权限和窗口焦点恢复。

## 发布与文档

`Publish.bat 2.0.22` 已生成 Windows EXE/MSI、Linux amd64/arm64 deb、便携包、CLI 和 `SHA256SUMS.json`，并创建 GitHub Release。发布提交为 `d029ff9`，标签为 `v2.0.22`，更新日志已写入 `CHANGELOG.md`。随后发现异常提交 `5e8ac76 (aa)` 将 24 个历史 release 归档产物写成 0 字节，已从发布提交恢复，待以独立修复提交推送。

本轮新增重复气泡回归已通过；`Compile.bat`、文件传输稳定性回归、好友刷新回归和缓存 .NET 10 SDK 的 Linux `dotnet build` 均通过。当前状态按 `CRDocs/RULES.md` 设为 `WAITING_FOR_GROK`，尚未重新打包或替换 v2.0.22 已发布资产。

## 未关闭边界与下一步

仅剩现场验收：真实双机新旧版本互通、Windows 截图工具/Excel/WPS 粘贴、Linux Xorg/Wayland/XWayland 剪贴板、Linux/ELF 实机运行与长时间桌面回归。按 `CRDocs/DEFERRED.md` 保留，不影响本次发布闭环。

入口：`Plan.md`、`ProjectMemory.md`、`contexts/Windows.md`、`contexts/Linux.md`、`contexts/Protocol.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`。
