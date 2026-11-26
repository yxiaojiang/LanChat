# LC-LINUX-003 阶段 4 — 最近 3 轮实施记录

活动区仅保留当前任务最近 3 轮；压缩前的完整实施细节见 [`archive/2026-11-26_历史审核_阶段0-3与UI输入传输收口/IMPLEMENTATION.md`](archive/2026-11-26_历史审核_阶段0-3与UI输入传输收口/IMPLEMENTATION.md)。

## Round 1 整改实施

- `update-service.cjs`：元数据保持 8 秒硬超时；安装包收到响应头后切换 30 秒空闲读超时，数据块重置计时器，结束/错误/关闭清理；增加 `downloading` 互斥、`.part`、SHA-256、原子改名和失败清理。
- `settings.js`：检查失败、空地址和打开动作统一兜底官方 Release 白名单地址。
- 回归：新增慢速分块成功、响应头后卡死清理 `.part`、真实设置 DOM 桩、进度更新、清单缺失隐藏下载和复制命令后 `quit` 断言；补齐更新服务中文注释。

## Round 1 首次交接

- 完成 GitHub API/403→302 降级、x64/arm64 与安装/便携资产匹配、嵌套 `manifest[arch].artifacts[assetName]` 校验、`0o700` 缓存、`.part` 流式校验、原子落盘和单引号 apt 命令。
- 接通 `main.cjs`、`preload.cjs`、`settings.js`、`settings.html` 的受限 IPC、520×440 更新弹窗、进度推送、Release/缓存位置入口和复制命令后退出退锁流。

## Round 2 复审记录

- 本轮无业务代码改动；独立复核阶段 4 实际代码、调用链和 12 项宿主机命令，确认 P1/P2 全部闭环。
- 主要验证：原生编译、三套 PowerShell、IPC/生命周期/设置/更新/UI Node 回归、Python 22 项、JS/Python 语法、行数门禁和 `git diff --check` 均退出码 0。
- 仍未验证：Linux/ELF、真实 GitHub 网络、真实 Electron、`XDG_CACHE_HOME` 权限、apt 退锁、`.deb` 升级/回退和便携启动，见 [`DEFERRED.md`](DEFERRED.md)。

## 历史追溯

更早阶段的实现文件、真实命令输出和行数表见 [`archive/INDEX.md`](archive/INDEX.md) 指向的完整快照。
