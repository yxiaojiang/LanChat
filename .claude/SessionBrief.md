# 当前接手摘要（LC-LINUX-003 阶段 3 已审核通过）

> 2026-11-26 · 分支 `codex/v4.0-cross-platform-unification` · 本轮阶段 3 已完成整改、独立审核通过，代码与文档随本次记忆保存提交并推送。

## 当前状态

LC-LINUX-003 阶段 3.1～3.4 已闭环。共享 `NetworkService` 保持 LANC/JSONL 与旧协议兼容；Windows/Linux 已统一持久化 `TunFilter`、`UnicastProbe`、`AutoLoadHistory`、`SendShortcutMode`。设置页的语言、链接、64KB 分块能力与网络协议通道均按真实能力诚实展示。Round 1 打回的 4 项 P1 已修复：设置窗刷新 IPC 放行并捕获错误、Windows 快捷键第五行不再与开关重叠、私聊 `Ctrl+L` 只清当前历史不删好友、Linux 前两枚快捷键徽章随模式保存同步。两项 P2 也已收口，设置回归新增真实控制器/DOM 桩和 IPC 来源校验。

## 验证与边界

Windows 原生 `Compile.bat`、三套 PowerShell 回归、全部 Node 回归、Python `tests/test_linux_flow.py` 22 项、Python/JS 语法检查、行数门禁和 `git diff --check` 均退出码 0。`tests/linux_desktop_ui_regression.js` 为 691 行，所有本轮手写代码文件低于 1000 行。以上 Linux 命名测试均为 Windows 宿主机上的模拟 DOM、桩 IPC 或纯逻辑验证，不代表 Linux/ELF 实机通过。Windows 宿主机没有可用 Linux/WSL 与 .NET 10 环境，Linux ELF、真实 Electron 桌面、Xorg/Wayland、fcitx5/ibus、真实多网卡/TUN、双机 LAN、升级回退和多 DPI/IME 仍按 `CRDocs/DEFERRED.md` 延期。

## 关键约束

继续保持 `codex/v4.0-cross-platform-unification` 分支；不得擅自改版本号、LANC/JSONL、发布链或安装目录。除非用户明确要求，不执行 `Publish.bat`、覆盖安装或推送到其他分支。所有新增注释使用中文，代码文件遵守 1000 行门禁；不能把 Windows 宿主机桩结果写成 Linux 实机通过。

## 下一步

阶段 3 已归档，下一步由用户决定进入阶段 4 在线更新、安排 Linux/ELF 与真实桌面现场验收，或先核销 `CRDocs/DEFERRED.md` 中的环境项。新会话先读取本文件、`.claude/Plan.md` 和 `CRDocs/STATUS.md`；当前审核状态记录在 `CRDocs/STATUS.md`，阶段 3 证据记录在 `CRDocs/IMPLEMENTATION.md` 与 `CRDocs/REVIEW.md`。
