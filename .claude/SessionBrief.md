# SessionBrief.md — 当前接手摘要

> 2026-11-26 · 工作目录 `D:\TEmp\LanChat` · `V3.0` 封版准备中，程序输出版本保持 `2.0.26`

## 当前状态

`LC-UI-INPUT-008` 的 4 项 P1 与 P2-1 已由 Gemini Round 2 复核为 RESOLVED。当前输入区继续使用 188px 多行胶囊和 214px 底部基准高度，切会话、附件草稿和外观刷新不会压缩输入区。`ChatInputBox` 使用系统 RichEdit 4.1，将原生文字、内置表情和主题色光标合成到同一离屏帧。表情令牌不再设置 RichEdit 字符上移量，避免插入或删除表情时正文基线跳动；图片按 PNG 非透明内容放大到两倍高度，并按实际可见内容底边贴合正文。原生白色光标通过绘制前后隐藏，鼠标点击表情内部时插入点收拢到令牌两侧，令牌原文仍用于发送和历史。

回归夹具覆盖多行 `EM_SETRECTNP` 的创建、Resize、FontChanged、RecreateHandle 生命周期、多行滚动、行尾表情像素绘制、选区、鼠标点击令牌、插入删除后的正文基线、Shift+Enter，以及切会话/附件/外观后的输入区布局断言。Windows 原生编译、聊天和好友回归、Node 语法/UI 模拟回归、`git diff --check` 均通过。未改变 LANC 魔数、Type 1/Type 2 字段顺序、确认字节或 JSONL 历史格式；`ChatInputBox.cs` 964 行、`ChatComposerRegression.cs` 958 行，已接近 1000 行预警线，下一轮继续增加行为前应先评估拆分。

`LC-CC-007` 与 `LC-FILE-TRANSFER-STABILITY-001` 保持已审核通过状态。内置微信表情资源、RichEdit 坐标叠绘、PrintWindow 截图回归和文件传输并发/竞态修复已进入 v2.0.24 基线。本轮 v2.0.26 收录 UI-INPUT-010 的 RichEdit 基线、表情实际像素对齐与光标收敛修复，候选包包含 Windows EXE/MSI、Linux amd64/arm64 Electron deb、便携包、CLI 与校验摘要。

## 兼容性与验证边界

项目保持纯局域网边界。真实 Windows 100%/125%/150% DPI 目测和鼠标命中、中文输入法预编辑、Linux Xorg/Wayland/XWayland Electron 实机、Linux/ELF 实跑、真实双机 LAN 与新旧版本互通仍未验证，全部保留在 `CRDocs/DEFERRED.md`，不得由宿主机回归替代。

## Linux 同步审计

Linux 已具备主窗双栏、会话/未读/搜索、聊天气泡、Unicode/静态表情回显、文件选择/拖放/剪贴板附件、逐文件进度、历史与删除、八页设置、主题/字体持久化及共享 C# 通信服务。

已确认的功能缺口是：关闭窗口仍直接退出，托盘恢复、未读托盘预览、系统通知点击和检查更新维护入口尚未接入；Linux 的 ELF `delete`/`history`/`clear-all-histories`、Electron 实机设置与焦点、Xorg/原生 Wayland/XWayland、fcitx5/ibus、多 DPI、2000 条历史、物理 x86_64/ARM64 升级回退及真实 Windows LAN 联测尚未验收。具体矩阵见 `PlanDocs/LC-LINUX-002/STATUS.md`、`BASELINE.md`、`CRDocs/DEFERRED.md`。

## 提交与下一步

版本元数据与 `CHANGELOG.md` 已更新；`v2.0.26` 已完成 Windows EXE/MSI、Linux amd64/arm64 deb/便携包/CLI 构建、提交、push 和 GitHub Release。`V3.0` 封版标签已推送，程序输出版本保持不变；当前分支 `codex/v4.0-cross-platform-unification` 已从主线创建并推送，作为 Windows/Linux 功能统一的后续开发分支。

V4.0 计划基线已写入 `PlanDocs/V4.0-UNIFICATION.md`：补齐 Linux 托盘/通知/关闭恢复、连接节点 GUI、设置页展示性开关的真实业务接线、更新维护入口，并完成 Linux ELF、Xorg/Wayland/XWayland、fcitx5/ibus、多 DPI、双架构升级回退和真实双机 LAN 验收。之后仅保留延期现场验收：截图工具/Excel/WPS 粘贴及长时间稳定性。

入口：`Plan.md`、`contexts/Windows.md`、`contexts/Linux.md`、`contexts/Protocol.md`、`CRDocs/STATUS.md`、`CRDocs/REVIEW.md`、`CRDocs/DEFERRED.md`。本轮新增的 `RichEditPaint.cs` 负责 TOM 基线查询和离屏缓冲，待发布脚本完成版本同步后提交并推送。
