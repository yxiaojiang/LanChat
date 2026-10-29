# Plan.md — 当前活跃计划

> 最近刷新：2026-10-29。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线：`main` 分支 Tag `v2.1.0` / `LanChat-v2.1.0`）。
> 核心任务：用户确认转向 **GrokBot 1:1**；P0 已恢复 64px 浮动输入胶囊；**方案 B（56px 窄导航）冻结**。

---

## 仍未完成 / 活跃计划项

1. **GrokBot 1:1 对齐（优先于微信布局）(feature/wechat-ui)**：
   - **P0 输入胶囊恢复（本步已完成代码）**：移除方案 A 的 160px 微信高输入区与 6 联工具栏；恢复 Docs §4.2 / §8 的 64px 浮动胶囊（`#202024` / 边框 `#2C2C32` / 左右 28px / 底 22px；(+) 36×36、Mic、魅紫 ↑ 44×44 发送）。待 Windows 实机 `CompileAndLink.bat` 热替换验收。
   - **方案 A（历史）**：曾落地 160px 微信高输入区 + 6 联圆钮工具栏；已由 P0 胶囊恢复覆盖输入区部分。
   - **方案 B（冻结）**：56px 微信极窄导航栏 **暂不实施**，待 GrokBot 1:1 完成后再评估。
   - **硬约束**：继续遵守 `Rules.md` 第 9 条与 `Docs/GrokBot_UI_Design_System.md`。

2. **GitHub 仓库设为 Public 并完成在线更新闭环验证**：
   - Release `v2.0.0` / `v2.1.0` 已通过 `Publish.bat` 成功更新挂载 `LanChat.msi` 与 `LanChat.deb` 资产。
   - 需用户在 GitHub 仓库页面将其可见性从 **Private** 切换为 **Public**（以允许客户端免密匿名拉取更新）。
   - 在客户端验证【🔄 检查新版本...】拉取与更新向导展示。

3. **实机多端互通与网络边界测试（可选演进项）**：
   - 验证 Windows 与 Linux（如统信 UOS / 银河麒麟 / Ubuntu）在复杂网络及多网卡环境下界面的双向文本与大文件流传输。
   - 本地轻量级离线历史消息持久化支持。

---

## 已确认仍有效的关键契约

- 端口：UDP 38888（发现与探测），TCP 38889+（通信与流传输）。
- 魔数：`LANC`（`0x4C, 0x41, 0x4E, 0x43`），小端模式，字符串为 LEB128 变长前缀 + UTF-8。
- 零第三方依赖：Windows 端由系统原生 `csc.exe` 产出绿色版 `LanChat.exe`，由 WiX 产出 `LanChat.msi`；Linux 端提供纯 Python 标准库 Tkinter 暗黑 GUI 与自包含 deb 安装包。
- 编译与语法：严格保持 C# 5.0 / .NET Framework 4.0 语法兼容，杜绝引入任何破坏轻量原生的 C# 8+ 高版本特性。
- 在线更新：支持 GitHub REST API 优先 + Web 302 重定向免限流降级双轨架构。
- 视觉规范：100% 遵守 `Docs/GrokBot_UI_Design_System.md` 与 `Rules.md` 第 9 条。
