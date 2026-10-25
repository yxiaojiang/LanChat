# Plan.md — 当前活跃计划

> 最近刷新：2026-10-25。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线：`main` 分支 Tag `v2.1.0` / `LanChat-v2.1.0`）。
> 核心任务：在严格遵循 GrokBot 暗黑设计系统（`Docs/GrokBot_UI_Design_System.md` 及 `Rules.md` 第 9 条硬约束）的前提下，将界面布局结构逐步对齐微信桌面端。

---

## 仍未完成 / 活跃计划项

1. **WeChat 布局结构与 Grok 暗黑设计系统深度融合 (feature/wechat-ui)**：
   - **方案 A（已闭环落地并热替换验证）**：
     - **左侧会话栏保持稳定**：保留 420px 会话栏完整结构（Hero 头像、86px 会话卡片、底部三联 Dock），避免界面大改失控；
     - **右侧输入区微信化重塑**：重塑为 160px 微信高输入区，配置 `#232326` 顶分割线与 `#121212` 纯黑背景；
     - **6 联工具栏按钮**：Emoji、Box（收藏）、Folder（文件）、Scissors（截图）、Mic（语音）、Call（通话），全部采用 34x34 圆形按钮容器（`#1C1C20`，悬停 `#2E2E34`），纯 GDI+ 1.8f 矢量绘制（`WeChatIcon.cs`），端点圆滑，视觉张力与左侧成熟圆钮 100% 对齐；
     - **消灭系统白底滚动条**：设置 `ScrollBars = ScrollBars.None` 消除暗黑界面中刺眼的 Win32 白色滚动条，同时保留滚轮与光标顺畅翻滚；
     - **排版矩阵严格达标**：输入字号升级为 `11.0pt (15px)` `#F2F2F6`（95% 明度），提示文字设定为 `9.5pt` `#8E8E93`（57% 明度），发送按钮升级为 92x32 魅紫 `#8E64DC`（10.0pt Bold 纯白字，6px 平滑圆角）；
     - **硬约束确立**：在 `.claude/Rules.md` 写入第 9 条 UI 硬约束，严禁随意手绘粗糙线条或非标字号。
   - **方案 B（下阶段即将执行）**：
     - **引入 56px 微信式纯黑极窄左侧导航栏**：配置顶部吉祥物头像、聊天、通讯录、收藏、发现、手机互通及底部设置菜单按钮；
     - **移除会话栏底部旧 Dock 按钮**：移除“连接节点”与蓝扫描按钮，让 86px 会话卡片流自然纵向铺满到底部；
     - **每步实机验证**：严格以 `Tools/CompileAndLink.bat` 进行 0 警告 0 错误编译覆盖并验证。

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
