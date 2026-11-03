# Plan.md — 当前活跃计划

> 最近刷新：2026-11-03。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线 Tag `v2.1.0`，当前开发版本 `v2.0.7`）。
> 核心任务：**GrokBot 1:1 对齐与即时通讯体验演进**；
> **本步已完成**：
> 1) 移除设置菜单顶部的更新安装横幅选项，菜单自适应收敛至 332px；
> 2) 个人头像改为不可点击（默认箭头光标，解绑点击弹窗）；
> 3) 聊天顶栏对方名称与头像整体放大 2 倍（76px 胶囊、20pt Bold 粗体、48px 吉祥物头像、96px 顶栏）；
> 4) 全链路即时通讯消息提醒体系（左侧会话红底白字未读徽标、Windows 任务栏 FlashWindowEx 黄色高亮、系统托盘交替闪烁，用户点击后即刻消除）。

---

## 仍未完成 / 活跃计划项

1. **GrokBot 1:1 细节微调与弹窗排版演进 (feature/wechat-ui)**：
   - Create 窗口 Docs `680×620`、About Docs `440×560` 需重排绝对布局后再放大尺寸（当前暂留紧凑可用尺寸）。
   - Settings 卡片内部分色值与 54px 行高已非常接近，待后续像素级对照微调。
   - `GrokTheme` 设计令牌全面替换工程中遗留的零散 `Color.FromArgb`。

2. **方案 B（冻结）**：56px 微信式纯黑极窄导航栏暂不实施。

---

## 已确认仍有效的关键契约

- 端口：UDP 38888，TCP 38889+；魔数 `LANC`；零第三方依赖；C# 5.0 / .NET Framework 4.0（`Tools/Compile.bat` csc）。
- 视觉规范：100% 遵守 `Docs/GrokBot_UI_Design_System.md` 与 `Rules.md` 9 条。
- 打包与发布规范：WiX Package 省略硬编码 ProductCode，依靠 UpgradeCode `{7E993262-EAA5-4A37-975D-39F24FEA21D1}` 实现全自动 MajorUpgrade；发布前必须自动 bump 版本号并全量编译。
