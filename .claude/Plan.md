# Plan.md — 当前活跃计划

> 最近刷新：2026-10-24。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.0.0**。
> 历史已完成计划项已全部归档至 `Plan-archive.md`。

---

## 仍未完成 / 活跃计划项

1. **架构解耦与工程代码重构（推进中）**：
   - **阶段一（已完成）**：UI 控件、矢量图系统与更新服务物理剥离，`MainForm.cs` 瘦身 1368 行，产出 `src/UI/` 与 `src/Services/` 独立模块，体积稳定在 139 KB，原生编译与 .NET 9 双轨 0 报错验证通过。
   - **阶段二：主窗体业务逻辑 Partial 模块化（当前待执行项）**：
     - 将精简后的 `MainForm.cs` 按职责拆解为：
       - `src/MainForm.cs`（窗体核心构造、控件布局树、系统 DPI 与整体生命周期，约 550 行）；
       - `src/MainForm.Chat.cs`（会话切换、文本发送、Emoji 选择器交互与好友状态管理，约 600 行）；
       - `src/MainForm.Files.cs`（文件/文件夹拖拽、进度条渲染与传输任务回调，约 260 行）；
       - `src/MainForm.Tray.cs`（系统托盘右键菜单、最小化后台驻留与全局广播唤醒，约 400 行）。
   - **阶段三：网络通信层内部轻量门面解耦（跨端零破坏）**：
     - 保持 `NetworkService` 对外 Facade 接口与事件不变（确保 `LanChat.Linux` 跨平台软链接 100% 编译通过）；
     - 内部划分子模块：网卡与 TUN 冲突裁决、UDP 广播与子网并发单播扫描、TCP 二进制封包编解码、64KB 流式分块传输。

2. **GitHub 仓库设为 Public 并完成在线更新闭环验证**：
   - Release `v2.0.0` 已通过 `publish-release.py` 成功创建并上传 `LanChat.msi` 与 `LanChat.deb` 资产。
   - 需用户前往 GitHub 仓库页面将其可见性从 **Private** 切换为 **Public**（以允许客户端免密匿名拉取更新）。
   - 在客户端验证【🔄 检查新版本...】拉取与更新向导展示。

3. **实机多端互通与边界测试（可选演进项）**：
   - 验证 Windows 与 Linux（如统信 UOS / 银河麒麟 / Ubuntu）在复杂网络及多网卡环境下微信暗黑界面的双向文本与大文件流传输。
   - 本地轻量级离线历史消息持久化支持。

---

## 已确认仍有效的关键契约

- 端口：UDP 38888（发现与探测），TCP 38889+（通信与流传输）。
- 魔数：`LANC`（`0x4C, 0x41, 0x4E, 0x43`），小端模式，字符串为 LEB128 变长前缀 + UTF-8。
- 零第三方依赖：Windows 端由系统原生 `csc.exe` 产出绿色版 `LanChat.exe`，由 WiX 产出 `LanChat.msi`；Linux 端提供纯 Python 标准库 Tkinter 微信暗黑 GUI 与自包含 deb 安装包。
- 编译与语法：严格保持 C# 5.0 / .NET Framework 4.0 语法兼容，杜绝引入任何破坏轻量原生的 C# 8+ 高版本特性。
- 在线更新：支持 GitHub REST API 优先 + Web 302 重定向免限流降级双轨架构。
- 视觉规范：对标 WeChat Desktop Dark Mode（侧边栏 `#252526`、会话列表 `#202021`、激活高亮 `#07c160`、主聊天区 `#1e1e1f`、气泡 `#07c160` 与 `#2c2c2e`），全界面无白色割裂元素。
