# 当前接手摘要（LC-LINUX-003 阶段 4 已审核通过）

> 2026-11-26 · 分支 `codex/v4.0-cross-platform-unification` · 阶段 4 已完成整改并经独立复审通过；当前代码、CRDocs 与记忆待本轮提交推送。

## 当前状态

LC-LINUX-003 阶段 4.1～4.3 已闭环，范围是 Linux 在线更新与维护入口。更新服务已完成 GitHub API/403→302 降级、x64/arm64 资产匹配、嵌套清单强校验、缓存目录和权限、`.part` 流式 SHA-256、原子改名、坏包清理、单引号 apt 命令及下载互斥锁；Electron 设置窗通过受限 IPC 接入检查、下载、打开目录、打开 Release 和复制命令后退出。Round 1 的 2 项 P1 与 2 项 P2 已在 Round 2 复审中标记 `RESOLVED`，`CRDocs/STATUS.md` 当前为 `APPROVED`。

## 验证与边界

Windows 原生 `Compile.bat`、三套 PowerShell 回归、IPC/生命周期/设置/更新/UI Node 回归、Python `tests/test_linux_flow.py` 22 项、JS/Python 语法检查、行数门禁和 `git diff --check` 均退出码 0。更新回归覆盖慢速分块下载、响应头后卡死超时清理 `.part`、真实 `settings.js` DOM 桩和发布页兜底。所有手写文件低于 1000 行。Linux 命名测试仍是 Windows 宿主机上的 Mock HTTP、DOM/IPC 桩或纯逻辑验证，不代表 Linux/ELF 实机通过；真实 GitHub 网络、Electron 桌面、XDG 权限、apt 退锁、升级回退、多网卡/TUN、双机 LAN 与多 DPI/IME 继续按 `CRDocs/DEFERRED.md` 延期。

## 关键约束

继续保持 `codex/v4.0-cross-platform-unification` 分支；不改版本号、LANC/JSONL、发布链或安装目录。所有新增注释使用中文，手写代码遵守 1000 行门禁；不能把宿主机桩结果写成 Linux 实机通过。用户已明确要求本轮提交并 push，因此可推送当前分支；仍不执行 `Publish.bat` 或安装覆盖。

## 下一步

阶段 4 已归档，下一步由用户决定进入阶段 2B（内联表情富文本输入框专项）或接入 Linux 环境核销阶段 5 现场验收。新会话先读取本文件、`.claude/Plan.md` 和 `CRDocs/STATUS.md`；阶段 4 证据在 `CRDocs/IMPLEMENTATION.md`、`CRDocs/REVIEW.md`，延期边界在 `CRDocs/DEFERRED.md`。
