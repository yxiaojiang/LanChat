# Plan.md — 当前活跃计划（V4.0 双端功能统一）

> 2026-11-26 · 分支 `codex/v4.0-cross-platform-unification` · `LC-LINUX-003` 阶段 4 已完成并通过独立审核；阶段 4 已归档至 `Plan-archive.md`。

## 当前状态

- [x] 阶段 0、阶段 1、阶段 2A、阶段 3、阶段 4：实现、宿主机回归、文档交接与独立审核已完成。
- [ ] 阶段 2B：内联表情富文本输入框专项，等待用户明确开启。
- [ ] 阶段 5：双架构构建、Linux/ELF 实机、真实桌面与双机 LAN 总验收，等待现场环境与用户安排。

## 当前仍活跃的 1～3 项事项

1. **阶段 2B 评估**：只有用户明确开启后，才设计内联表情富文本输入框专项；遵守 `CP-2B` 门禁、回退开关和文件行数预警。
2. **现场验收准备**：保留 `CRDocs/DEFERRED.md` 中的 Linux/ELF、Electron Xorg/Wayland、fcitx5/ibus、多 DPI/IME、多网卡/TUN、双机 LAN 与更新升级回退边界；有真实环境时优先执行对应 CP 门禁。
3. **工作区纪律**：继续保持当前分支和协议兼容，不改版本号、LANC/JSONL、安装目录；代码注释中文、手写文件 `<1000` 行；所有宿主机桩证据必须明确标注，不能写成 Linux 实机通过。

## 已确认的全局契约

- Windows/Linux 共享 `NetworkService` 与 `Models`；Electron 主进程不创建局域网 UDP/TCP Socket；渲染层保持严格 CSP。
- `TunFilter`、`UnicastProbe`、`AutoLoadHistory`、`SendShortcutMode` 使用统一 JSON 键名和默认值；Linux 设置窗通过受限 preload IPC 访问共享服务。
- 禁止执行 `Publish.bat`、覆盖安装或向其他分支推送，除非用户明确要求；纯编译使用 `Tools/Scripts/Compile.bat`。本轮用户已明确要求向当前分支提交并 push。

## 入口

- 审核与延期：`CRDocs/STATUS.md`、`CRDocs/REVIEW.md`、`CRDocs/DEFERRED.md`
- 阶段 3 证据：`CRDocs/IMPLEMENTATION.md`
- 历史计划：`Plan-archive.md`
- 模块上下文：`contexts/Windows.md`、`contexts/Linux.md`、`contexts/Protocol.md`
