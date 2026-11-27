# Plan.md — 当前活跃计划（v3.0.0 发布与 Linux 实机收口）

> 2026-11-27 · 分支 `codex/v4.0-cross-platform-unification` · LC-LINUX-003 Round 3 已独立审核通过；当前进行 v3.0.0 发布收口。

## 当前状态

- [x] 阶段 0、阶段 1、阶段 2A、阶段 3、阶段 4、阶段 2B：编码、宿主机门禁和独立审核已完成。
- [x] Round 3：1 项 P0、2 项 P1、2 项 P2 已修复，严格 DOM Observer、生命周期、IPC、设置和全量回归通过。
- [ ] v3.0.0：版本同步、CHANGELOG/README 更新、Windows/Linux 资产构建、Git 提交推送和 GitHub Release 收口。
- [ ] 阶段 5：物理 Linux x86_64/ARM64、真实 Electron 桌面、IME/DPI、升级回退和双机 LAN 验收。

## 当前仍活跃的 1～3 项事项

1. **v3.0.0 发布**：运行 `Tools/Scripts/Publish.bat 3.0.0`，核对 MSI、EXE、双架构 deb/portable/CLI、`SHA256SUMS.json`、远端分支、tag 和 Release 资产。
2. **阶段 5 Linux 实机核销**：按 `CRDocs/DEFERRED.md` 的 CP-0、CP-1、CP-2B、S4、升级回退和真实 LAN 清单取证。
3. **工作区纪律**：保持 LANC/JSONL、安装目录和共享网络核心兼容；宿主机证据继续明确标注，不替代 Linux 实机证据。

## 已确认的全局契约

- Windows/Linux 共用 `NetworkService` 与 `Models`；Electron 主进程不创建局域网 UDP/TCP Socket；渲染层保持严格 CSP。
- v3.0.0 仅是版本与发布资产更新，不改变 LANC/JSONL、安装目录、IPC 数据边界或 Linux ELF 架构矩阵。
- Linux 新版只提供 amd64 与 arm64；应用和 CLI 不依赖 Python，便携包与安装包内含对应 Electron/ELF 资产。
- 真实 Linux、IME、DPI、托盘、升级回退、多网卡/TUN 和物理 LAN 继续以 `CRDocs/DEFERRED.md` 为准。

## 入口

- 发布与审核：`CRDocs/STATUS.md`、`CRDocs/REVIEW.md`、`CRDocs/IMPLEMENTATION.md`
- 阶段 5 清单：`CRDocs/DEFERRED.md`
- Linux 模块上下文：`.claude/contexts/Linux.md`
