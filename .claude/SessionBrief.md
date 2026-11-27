# 当前接手摘要（v3.0.0 发布收口，等待 Linux 实机核销）

> 2026-11-27 · 分支 `codex/v4.0-cross-platform-unification` · LC-LINUX-003 阶段 2B Round 3 已获独立审核通过，五项缺陷已闭环；当前进入 v3.0.0 打包、提交、推送与 Release 收口。

## 当前状态

Round 3 的 P0/P1/P2 已完成并通过独立复审：`renderer.js` 现在只向滚动条传入 `#message` 与 `#message-rich` 真实 DOM 节点，双轨输入按显隐切换浮动滑块，`composer-box.js` 补齐 `clientHeight`；隐藏主窗打开设置不再挂隐藏父窗或错误回焦；托盘创建异常安全降级为任务栏最小化；剪贴板图片兼容 `ArrayBufferView` 并保留 64 MiB 限制；设置 Escape 和 connecting 文案已修正。严格 `ResizeObserver`/`MutationObserver` 回归已纳入 UI 总回归。

## v3.0.0 发布状态

核心版本已从 2.0.26 统一为 3.0.0，覆盖 Windows 常量、Linux Python/ELF、Electron package、Deb/MSI 构建脚本、README、CHANGELOG 和更新回归样例。目标是通过 `Tools/Scripts/Publish.bat 3.0.0` 生成 Windows MSI/EXE、Linux amd64/arm64 deb/portable/CLI 与摘要文件，并由发布脚本提交、推送当前分支及创建或更新 GitHub Release。发布完成后须核对远端 tag、Release 资产和本地工作区清洁状态。

## 验证与边界

Round 3 宿主机门禁已通过：25 个 JS/CJS 语法、Node Linux UI/IPC/生命周期/设置/更新回归、严格 DOM Observer 专项、Python 22 项、Windows 三套回归、缓存 .NET 10 共享后端 0 警告/0 错误、行数与 `git diff --check`。这些证据不替代真实 Linux Electron、Xorg/Wayland/XWayland、托盘、IME、DPI、系统剪贴板、Linux ELF 实跑、物理 LAN 和升级回退。

## 关键约束

3.0.0 不改变 LANC/JSONL、安装目录或共享网络核心；保持全中文注释、手写源码单文件低于 1000 行预警；不把宿主机 Mock 或交叉编译写成 Linux 实机通过。发布脚本可按用户明确指令提交和推送；不要执行安装覆盖，真实 Linux 验收仍按 `CRDocs/DEFERRED.md` 阶段 5 清单取证。

## 下一步

完成 3.0.0 Release 资产构建、Git 提交/推送与 GitHub Release 核验；随后在物理 x86_64、物理 ARM64 Linux 及 Windows 主机按 CP-0、CP-1、CP-2B、S4 和真实 LAN 矩阵核销。新会话先读取本文件、`.claude/Plan.md`、`CRDocs/STATUS.md` 和 `CRDocs/DEFERRED.md`。
