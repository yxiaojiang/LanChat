# 当前任务 — LC-LINUX-003（阶段 2B / Linux 实机初测阻断回退整改）

- **状态**：`APPROVED`（Round 3 五项缺陷已独立复审通过，v3.0.0 发布资产已构建并上传；仅代表当前源码/宿主机与发布链范围放行，不代表真实 Linux 实机通过）
- **日期/分支**：2026-11-27 / `codex/v4.0-cross-platform-unification`
- **执行者/独立审核者**：Codex（Round 3 整改）/ Antigravity（Gemini，Round 3 独立复审通过）
- **范围**：修复 `CRDocs/REVIEW.md` Round 3 登记的 5 项缺陷（`LC-LINUX-003-S5-P0-1`、`P1-1`、`P1-2`、`P2-1`、`P2-2`）并补齐严格 DOM 监听器宿主机回归；随后将输出版本统一为 `3.0.0` 并完成 Windows/Linux 资产发布，不改 LANC/JSONL 或安装目录。
- **结论**：Round 3 全部 1 项 P0、2 项 P1 与 2 项 P2 均已独立复核 `RESOLVED`：真实 DOM `#message`/`#message-rich` 独立绑定 `scroll.bind()` 并补齐 `clientHeight` 与严格 `ResizeObserver`/`MutationObserver` 回归；隐藏主窗不再作为设置窗模态父窗且关闭时不误唤焦点；托盘构造失败安全降级为任务栏最小化且无未捕获异步拒绝；`decodeClipboardImage` 兼容 `ArrayBufferView`（保留 64 MiB 限制）；更新弹窗 `Escape` 与 `connecting` 文案已修复。全部宿主机语法、回归、行数与 `git diff --check` 门禁退出码 `0`。
- **证据边界**：Windows 宿主机 Node/DOM/IPC 桩与 Python 回归均通过，但不代表真实 Linux Electron、Xorg/Wayland/XWayland、托盘、IME、DPI、系统剪贴板、Linux/ELF、物理 LAN 或升级回退通过；这些继续按 `DEFERRED.md` 保持未验证。
- **下一步**：在物理 Linux x86_64/ARM64 主机安装并复测 v3.0.0，按 [`DEFERRED.md`](DEFERRED.md#阶段-5-linux-实机与真实-lan-总验收清单准备版) 核销 CP-0、CP-1、CP-2B、S4、升级回退和真实 LAN 清单。

## 最近 3 轮审核记忆

| 轮次 | 日期 | 结论 | 入口 |
| --- | --- | --- | --- |
| Round 3 独立复审 | 2026-11-27 | `APPROVED`；P0-1、P1-1、P1-2、P2-1、P2-2 全部闭环（Windows 宿主机范围放行，待重打包 Linux 实机验证） | [`REVIEW.md`](REVIEW.md#round-3-独立复审通过1-项-p02-项-p1-与-2-项-p2-全部闭环) |
| Round 2 独立复审 | 2026-11-26 | `APPROVED`；阶段 2B Round 1 缺陷全部闭环（仅 Windows 宿主机范围，实机初测暴露漏检） | [`REVIEW.md`](REVIEW.md#round-2-独立复审通过1-项-p02-项-p1-与-1-项-p2-全部闭环) |
| Round 1 独立审核 | 2026-11-26 | `REJECTED`；1 项 P0（IPC 粘贴越界+权限必拒）+ 2 项 P1（偏移双倍累加、启动恢复/禁用/选区脱节） | [`REVIEW.md`](REVIEW.md#round-1-独立审核结论打回1-项-p0--2-项-p1-阻断缺陷--1-项-p2-建议) |

CP-2B 及阶段 5 的 Linux 实机门禁与验收条件见 [`DEFERRED.md`](DEFERRED.md#lc-linux-003-cp-2b-open-真实-linux-fcitx5ibus-ime-与富文本输入验收)。
