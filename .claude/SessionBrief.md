# SessionBrief.md — 当前接手摘要

> 2026-11-25 · 工作目录 `D:\TEmp\LanChat` · `v2.0.25` 发布候选打包中，`LC-UI-INPUT-008` 已获 Gemini Round 2 独立审核通过

## 当前状态

`LC-UI-INPUT-008` 的 4 项 P1 与 P2-1 已由 Gemini Round 2 复核为 RESOLVED。底部输入区使用 `BottomSendPanelBaseHeight = 154`，切会话和附件草稿刷新不会压缩 128px 多行胶囊；`LayoutInputPanel()` 由 Resize 与外观刷新共用，输入框不会下移覆盖快捷工具栏。输入区圆角为 18px，占位符对齐 `_txtInput.Top + EM_SETRECTNP.Top = 16`；`ChatInputBox` 默认多行换行，表情绘制裁剪到格式矩形并处理自动折行行尾的跨行坐标。

回归夹具覆盖多行 `EM_SETRECTNP` 的创建、Resize、FontChanged、RecreateHandle 生命周期、多行滚动、行尾表情像素绘制、选区、Shift+Enter，以及切会话/附件/外观后的 128px 布局不重叠断言。Windows 原生编译、聊天和好友回归、Node 语法/UI 模拟回归、`git diff --check` 均通过。未改变 LANC 魔数、Type 1/Type 2 字段顺序、确认字节或 JSONL 历史格式；相关源码与测试文件均低于 1000 行。

`LC-CC-007` 与 `LC-FILE-TRANSFER-STABILITY-001` 保持已审核通过状态。内置微信表情资源、RichEdit 坐标叠绘、PrintWindow 截图回归和文件传输并发/竞态修复已进入 v2.0.24 基线。本轮 v2.0.25 收录 UI-INPUT-008 整改与审核记录，候选包包含 Windows EXE/MSI、Linux amd64/arm64 Electron deb、便携包、CLI 与校验摘要。

## 兼容性与验证边界

项目保持纯局域网边界。真实 Windows 100%/125%/150% DPI 目测和鼠标命中、中文输入法预编辑、Linux Xorg/Wayland/XWayland Electron 实机、Linux/ELF 实跑、真实双机 LAN 与新旧版本互通仍未验证，全部保留在 `CRDocs/DEFERRED.md`，不得由宿主机回归替代。

## 提交与下一步

版本元数据与 `CHANGELOG.md` 已更新；下一步完成 `v2.0.25` 打包、提交、push 和 GitHub Release。之后仅保留延期现场验收：真实多 DPI/输入法、Linux 三类桌面会话、Linux ELF、双机 LAN、截图工具/Excel/WPS 粘贴及长时间稳定性。

入口：`Plan.md`、`contexts/Windows.md`、`contexts/Linux.md`、`contexts/Protocol.md`、`CRDocs/STATUS.md`、`CRDocs/REVIEW.md`、`CRDocs/DEFERRED.md`。
