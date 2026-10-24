# Plan.md — 当前活跃计划

> 最近刷新：2026-10-24。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.0.0**。
> 阶段一至阶段四重构已全部归档至 `Plan-archive.md`。

---

## 仍未完成 / 活跃计划项

1. **架构解耦与工程代码重构（推进成果）**：
   - **阶段一（已完成）**：UI 控件、矢量图系统与更新服务物理剥离，产出 `src/UI/` 与 `src/Services/` 独立模块，体积稳定在 139 KB。
   - **阶段二（已完成）**：主窗体业务逻辑 Partial 模块化，拆分为 `MainForm.cs`（569行）、`MainForm.Chat.cs`（510行）、`MainForm.Files.cs`（242行）、`MainForm.Tray.cs`（296行），单文件规模大幅降低。
   - **阶段三（已完成）**：网络通信层内部轻量门面解耦，拆分为 `NetworkService.cs`（154行）、`NetworkService.Interface.cs`（153行）、`NetworkService.Discovery.cs`（396行）、`NetworkService.Transfer.cs`（341行），跨端零破坏，双轨 0 警告 0 错误编译通过。
   - **阶段四（已完成）**：提炼应用服务层 `src/Services/SessionManager.cs`（142行），主窗体完全剥离 `_chatHistories` 数据字典，实现会话上下文与历史消息纯领域封装，原生体积恒定保持 ~140KB。

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
- 视觉规范：对标 WeChat Desktop Dark Mode（侧边栏 `#252526`、会话列表 `#202021`、激活绿 `#07c160`、主聊天区 `#1e1e1f`、气泡 `#07c160` 与 `#2c2c2e`），全界面无白色割裂元素。
