# Plan.md — 当前活跃计划（V4.0 双端功能统一）

> 2026-11-26 · 分支 `codex/v4.0-cross-platform-unification` · `LC-LINUX-003` 阶段 2B 已通过 Round 2 独立复审（Windows 宿主机范围），阶段 5.1 候选产物已完成宿主机构建核验。

## 当前状态

- [x] 阶段 0、阶段 1、阶段 2A、阶段 3、阶段 4：实现、宿主机回归、文档交接与独立审核已完成。
- [x] 阶段 2B：内联表情富文本输入框 2B.1～2B.3 已通过 Round 2 独立复审；Windows 宿主机范围闭环，CP-2B 仍待真实 Linux `fcitx5`/`ibus`。
- [ ] 阶段 5：阶段 5.1 双架构候选构建已在 Windows 宿主机完成并核验；Linux/ELF 实机、真实桌面与双机 LAN 总验收等待现场环境。

## 当前仍活跃的 1～3 项事项

1. **阶段 5 Linux 实机核销**：按 `CRDocs/DEFERRED.md` 的 CP-0、CP-1、CP-2B、`LC-LINUX-003-S4-ENV-001` 与真实 LAN 清单逐项取证。
2. **候选产物交接**：保留 2.0.26 双架构 `.deb`、便携包、CLI 与 `SHA256SUMS.json` 的 Windows 静态核验结果；到 Linux 实机复核安装、启动、升级、回退和运行期 ELF。
3. **工作区纪律**：继续保持当前分支和协议兼容，不改版本号、LANC/JSONL、安装目录；代码注释中文、手写文件 `<1000` 行；所有宿主机桩证据必须明确标注，不能写成 Linux 实机通过。

## 已确认的全局契约

- Windows/Linux 共享 `NetworkService` 与 `Models`；Electron 主进程不创建局域网 UDP/TCP Socket；渲染层保持严格 CSP。
- `TunFilter`、`UnicastProbe`、`AutoLoadHistory`、`SendShortcutMode` 使用统一 JSON 键名和默认值；Linux 设置窗通过受限 preload IPC 访问共享服务。
- 本轮允许提交本地收口 commit，但禁止执行 `Publish.bat`、安装覆盖或 `git push`；阶段 5.1 使用 `python Tools/Scripts/BuildLinuxDeb.py`，不改变版本号。

## 阶段 5.1 当前证据

- `python Tools/Scripts/BuildLinuxDeb.py` 已成功使用项目缓存 `.NET SDK 10` 构建 `linux-x64`/`linux-arm64` ELF，并生成 2.0.26 双架构 deb、portable、CLI 与 `SHA256SUMS.json`。
- 独立重算摘要：当前 6 个 2.0.26 artifact 全部与 `SHA256SUMS.json` 匹配；portable/deb 内嵌 `lanchat-bin` 分别核验 ELF64 `e_machine=62`（amd64）与 `183`（arm64）。
- 以上仍是 Windows 宿主机交叉构建/静态证据；真实 Linux 安装、Electron 启动、输入法、系统托盘、更新退锁和 LAN 仍不得标记通过。

## 入口

- 审核与延期：`CRDocs/STATUS.md`、`CRDocs/REVIEW.md`、`CRDocs/DEFERRED.md`
- 阶段 3 证据：`CRDocs/IMPLEMENTATION.md`
- 历史计划：`Plan-archive.md`
- 模块上下文：`contexts/Windows.md`、`contexts/Linux.md`、`contexts/Protocol.md`
