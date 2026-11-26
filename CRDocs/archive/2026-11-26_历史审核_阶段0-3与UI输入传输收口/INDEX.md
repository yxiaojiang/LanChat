# 历史审核归档索引

- **归档目录**：`2026-11-26_历史审核_阶段0-3与UI输入传输收口`
- **归档日期**：2026-11-26
- **归档原因**：活动区启用“最多保留最近 3 轮”后，将压缩前的完整 CRDocs 快照移出活动区。
- **完整性**：本目录中的四个 Markdown 文件是压缩前原文快照，不替代活动区当前状态；阶段 4 的最新记录也保留在快照中，便于核对迁移前后差异。

## 内容导航

| 文件 | 主要内容 | 重点检索项 |
| --- | --- | --- |
| [`STATUS.md`](STATUS.md) | 各任务状态快照 | `LC-LINUX-003`、`LC-UI-INPUT-010`、`LC-FRIEND-REFRESH-001` |
| [`REVIEW.md`](REVIEW.md) | 历次独立审核、打回、复审和问题分级 | 任务编号、`P0/P1/P2`、`RESOLVED`、`DEFERRED` |
| [`IMPLEMENTATION.md`](IMPLEMENTATION.md) | 执行者实现、命令证据和未验证边界 | 文件路径、回归命令、行数门禁 |
| [`DEFERRED.md`](DEFERRED.md) | 延期项和跨环境验收条件 | Linux/ELF、Electron、LAN、DPI/IME、升级回退 |

## 主题定位

- `LC-LINUX-003`：阶段 0/1、2A、3 的双端统一与设置页收口；阶段 4 的详细记录见活动区最近三轮，也可在本快照顶部复核。
- `LC-UI-INPUT-008`～`010`：输入区布局、光标、内联表情和富文本基线。
- `LC-CC-007`、`LC-CHAT-COMPOSER-001`：表情资源、剪贴板、截图和聊天编辑器。
- `LC-FILE-TRANSFER-STABILITY-001`、`LC-FRIEND-REFRESH-001`：文件传输并发稳定性与好友刷新。
