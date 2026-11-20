# SessionBrief.md — 当前接手摘要

> 2026-11-20 · 工作目录 `D:\TEmp\LanChat`，开发分支 `main`。公开联测基线 Windows `v2.0.16`（`a0e49c1`）。

## 当前状态

唯一活跃事项为 LC-LINUX-002。第 0 步已固定当前 WinForms 原生构建、采用 125% 缩放的 18 张运行截图和功能清单，并冻结 Ubuntu 22.04／24.04 的架构与桌面后端矩阵。第 1 步在 Ubuntu 24.04.5 amd64 QEMU 的 Xorg／Openbox 中通过首包集成，验证从已安装的桌面入口启动、刷新、双向私聊与群聊、双向文件 SHA-256、渲染器沙盒、无头入口、退出和端口释放。第 0、1 步已归档。

新客户端由 Electron 45.4.1 加本地 HTML、CSS、JavaScript 构成，同架构 .NET 10 自包含 ELF 通过 stdin/stdout 提供桌面服务，也可作为独立 CLI。Windows 与 Linux 继续复用 NetworkService 和 Models，沿用 LANC、64 KiB 流式传输和最终 ACK。新版只打包 amd64 和 arm64，运行无需 Python。旧 Python 发布版及 LC-LINUX-001 候选用于升级、回退和兼容性对照。

第 2 步已接入共享会话、离线历史和 CLI 昵称写回。设置沿用旧顺序，已有同级设置优先。可写便携目录优先新建同级设置，其余使用 `~/.config/lanchat`，仅配置目录创建失败时回退到 `~/.local/share/lanchat`。首次写入保留备份和未知键。旧 pyz 已读回原有及新版追加的 JSONL，原接收文件逐字节一致。只读位置目前报告写入错误，选择可写位置的确认流程仍待实现。共享接收核心已修复并发同名覆盖，Windows 原生回环的两份异内容文件分别匹配源 SHA-256，最终 ACK 和完成事件各两个。

当前试验版本为 `2.0.17~experiment.2`。两种架构的 deb、便携包、独立 ELF、公开旧版和回退样本在 `bin/lc-linux-002/handoff-experiment.2/`，25 件文件通过摘要检查。第 1 步原产物在 `bin/lc-linux-002/experiment/`，旧样本在 `bin/lc-linux-002/baseline/`，构建缓存和原始日志也在本地 `bin/`。这些产物属于本地忽略目录，仓库内的构建和验证文档记录了来源与路径。

## 下一步

先定位 ARM64 安装失败。用户报告第二轮 ARM64 桌面可启动、收发文字及刷新好友，截图显示 `dpkg` 缺依赖并保留未配置状态，画面可见 `libssl3`。包内控制字段为 `libssl3t64 | libssl3`，.NET 10 可使用 OpenSSL 1.1.1 及以上，现场实际加载版本尚不明确。先核对发行版、完整依赖报错、包状态、仓库可用的库和 `lanchat-bin` 的实际动态库。结合已冻结的发行版矩阵和复现结果处理打包依赖，再产出递增版本的试验包。

继续完成第 2 步剩余文件与配置流程、实际旧包升级和回退、旧 GUI 恢复。第 2 步通过后进入第 3 步。用户自行执行实机验收，开发侧负责准备可核验的安装包并按证据更新 Plan。物理 x86_64 和 ARM64 各自完成升级与回退，并各自和物理 Windows 公开版完成真实 LAN 互通。已确认的安装失败及缺证据的必验项保持未通过。Windows Electron 留到 Linux 验收后另议，正式发布另等用户授权。

剩余文件回归覆盖非默认端口、定长短读、拒收、中断、缺最终回执及大文件流式传输。旧包升级回退就地保留历史目录、附件路径和接收件，用户主动切换历史目录作为单独用例。原生 Wayland 仍需验证窗口恢复、系统托盘和中文输入。

## 入口

- [执行计划](Plan.md) · [阶段归档](Plan-archive.md) · [采纳定稿](../PlanDocs/LC-LINUX-002/FINAL.md) · [首包集成](../PlanDocs/LC-LINUX-002/INTEGRATION.md)
- [Linux 模块](contexts/Linux.md) · [Windows 基线](contexts/Windows.md) · [共享协议](contexts/Protocol.md)
