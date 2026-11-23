# SessionBrief.md — 当前接手摘要

> 2026-11-23 · 工作目录 `D:\TEmp\LanChat`，分支 `main`。公开基线 Windows `v2.0.16`（`a0e49c1`）。LC-LINUX-002 第 0 至第 2 步已归档。第 3 步切片 1 代码审核已通过，下一步做设置八页。

## 当前状态

试验版 `2.0.17~experiment.5`。桌面由 Electron 45.4.1 与本地页面组成，通信由同架构 .NET 10 自包含 ELF 承担，独立 CLI 共用该 ELF。Windows 与 Linux 共用 NetworkService、Models 和现有 LANC 封包。新版 deb 只交付 amd64、arm64，无需 Python。设置优先同级旧文件；便携同级可写时在同级创建，否则用 `~/.config/lanchat`，仅该目录创建失败才回退 `~/.local/share/lanchat`。原设置只读时，备份后须用户确认才切换。历史迁移逐个复制 JSONL，目标已有记录时拒绝覆盖。

第 2 步已由用户验收。共享会话、离线历史、草稿、文件终态和 CLI 昵称写回已接通。Ubuntu 24.04/22.04 的升级、回退与重装有记录。银河麒麟 V10 国防版用户确认第五轮可安装、刷新并收发文字；该回报没有 dpkg、CLI、回退或库映射，不补记为已观察。物理双架构、无头 CLI 和 Windows 局域网互通留在第 4 步。交接在 `bin/lc-linux-002/handoff-experiment.5/`。证据见 [集成记录](../PlanDocs/LC-LINUX-002/INTEGRATION.md)。

第 3 步切片 1 是主窗客户区。会话列表、聊天头、文件卡片、暗色菜单、确认框和浮动滑块已按 WinForms 实测尺寸接入。独立审核 Round 3 为 `APPROVED`，报告在 [CRDocs/REVIEW.md](../CRDocs/REVIEW.md)。删除好友会清历史并移出在线列表，清空只删历史。群聊「发送文件给所有人...」仍先选择一个接收人。窗口使用系统边框。托盘唤回尚无运行证据，关闭仍是退出。

界面回归命令是 `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js`，结果为 `linux desktop ui regression: ok`。`C:\Windows\System32\node` 不能执行该脚本。Chromium 1040×720 量到侧栏 420px、顶栏 96px、会话行 72px、选中色 `#7946D7`、滑块静止 4px；800px 宽时侧栏 280px。这是无 ELF 的页面测量。缓存 SDK 10.0.404 已编过 linux-x64 与 linux-arm64 ELF。本机无 QEMU，`tests/linux_desktop_regression.py` 的 `delete` 与 `history` 尚未对新 ELF 实跑，不能记成通过。`renderer.js` 现为 792 行，继续加页面会逼近 1000 行预警线。

现有设置框只有昵称、自动接收、接收目录和历史迁移，还不是八页。WinForms 八页在 `src/UI/Controls/SettingsPanels/`：通用、个人主页、外观、快捷键、文件传输、聊天记录、设备连接、关于与更新。设置窗实测基准是 1260×820。视觉以 `Docs/GrokBot_UI_Design_System.md` 和 125% 基线截图 `settings-1` 至 `settings-8` 为准。Linux 手写 JavaScript、CSS 与 C# 到 1000 行预警、1500 行硬停。注释使用中文。新页面使用暗色自绘滑块，文件和目录选择继续走系统对话框。

## 下一步

实现第 3 步切片 2：把设置对话框对齐 WinForms 八页，即通用、个人主页、外观、快捷键、文件传输、聊天记录、设备连接、关于与更新。窗口基准 1260×820，并适应工作区。已接通的昵称、自动接收、目录选择、历史迁移和只读确认必须保留。语言、链接、主题、快捷键里 Windows 只改按钮文案的项保持展示，不新造未接线行为。新界面放到独立模块，不要继续堆进 `renderer.js`。

这一片不要做托盘、通知、维护入口，也不要把关闭改成隐藏。输入法、125% 与 150% 叠图、至少 2000 条历史和三种图形后端仍未通过。实机由用户验收。Windows Electron 与正式发布另议。

## 入口

- [执行计划](Plan.md) · [阶段归档](Plan-archive.md) · [采纳定稿](../PlanDocs/LC-LINUX-002/FINAL.md)
- [Linux 模块](contexts/Linux.md) · [共享协议](contexts/Protocol.md) · [Windows 基线](contexts/Windows.md)
- [代码审核](../CRDocs/STATUS.md)
