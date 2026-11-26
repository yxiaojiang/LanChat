# SessionBrief.md — 当前接手摘要

> 2026-11-26 · 分支 `codex/v4.0-cross-platform-unification` · `LC-LINUX-003` 阶段 0、阶段 1（1.1～1.5）已完成，未提交未 push

## 当前状态

基线仍为 `V3.0`（程序输出版本 `2.0.26`）。本轮完成阶段 0：四套宿主机基线全绿，将 `tests/linux_desktop_ui_regression.js` 从 924 行拆至 700 行，并抽出 `tests/linux_desktop_test_helpers.js`；核对基线和延期清单后确认 Windows 宿主机没有可用 Linux/WSL 发行版，`LC-LINUX-002-ENV-001` 继续 `[OPEN]`，没有用宿主机结果核销 CP-0。

阶段 1 已完成 1.1～1.5：导出 `tray.png`、`tray-unread.png`、`tray-blank.png`；LinuxProfile/DesktopService、Electron 设置桥接和设置页已持久化 `KeepAliveInTray` / `TrayPreview`；新增 `tray-manager.cjs`，用固定参数 `execFile(busctl, ..., timeout=1500)` 探测 D-Bus，失败或超时保守最小化；主进程接入托盘隐藏、通知、未读 Tooltip、Xorg 闪烁/预览、恢复和显式退出，渲染层接入未读同步与通知点击切会话；新增生命周期回归并纳入 UI 行数门禁。

## 兼容性与红线

未改 `LANC` 魔数、Type 1/Type 2 字段、`JSONL` 历史格式、版本号、安装目录、发布脚本或 Release；Electron 主进程没有新增局域网 UDP/TCP Socket。所有本轮新增/修改的手写源码与测试文件均低于 1000 行，新增注释使用中文。明确不做的节点空壳、三语切换、内置网页预览、`fs.Flush(true)`、复杂协议通道和非 root 静默安装仍未实现。系统编译会生成 `release` 二进制，但本轮已恢复到任务开始前状态。

## 验证与下一步

`Compile.bat`、`linux_desktop_ui_regression.js`（含生命周期回归）、`chat_composer_regression.ps1 -ExpectLocalPack`、`friend_refresh_regression.ps1` 均退出码 0；Linux C# 工程用项目缓存的 .NET 10.0.404 在 Windows 宿主机构建为 0 警告/0 错误，但不是 Linux 实跑。当前下一步是按 `.claude/Plan.md` 进入阶段 2A；CP-0、CP-1、CP-2B 及真实 Linux 桌面、多 DPI/IME、双架构和物理 LAN 仍待现场验证。当前工作树仍有用户原有的 `.claude`、`PlanDocs/LC-LINUX-003` 与 `Temps` 改动，未覆盖。

入口：`Plan.md`、`PlanDocs/LC-LINUX-003/FINAL.md`、`CRDocs/STATUS.md`、`CRDocs/DEFERRED.md`。当前工作树缺少 `contexts/Linux.md`，不可据此声称已读取 Linux 上下文。
