# Plan.md — 当前活跃计划

> 最近刷新：2026-11-04。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（已发布 Tag `v2.0.12`，当前版本 `v2.0.12`）。
> 核心任务：**会话切换零重绘零闪烁、魅紫选中背景态、紫色气泡内边距减半、文件卡片呼吸感优化、右键删除会话与历史消息**；
> **本阶段已全部完成并正式发布至 GitHub Releases (v2.0.12)**：
> 1) 会话切换零重绘零闪烁：拦截 Win32 `WM_ERASEBKGND` 与 `WM_REFLECT | WM_DRAWITEM`，重写 `OnSelectedIndexChanged` 仅刷新新旧两项脏矩形，全链路容器升级 `DoubleBufferedPanel` 并挂载 `WS_CLIPCHILDREN`；
> 2) 魅紫选中态与色彩规范：`SessionSelected` 升级为 `#7946D7`，1px `#915FF0` 精细微光边框，副文本智能切换浅紫白 `#E4DAFA`，头像外框镂空无缝融合；
> 3) 气泡与文件卡片排版：紫色气泡 Padding 缩减 50%（水平 24px/垂直 18px），单行高度降至 34px，文件卡片操作行距离扩大至 13px 消除拥挤；
> 4) 会话右键删除与历史清理：右键“删除”带危险红标，居中暗黑确认弹窗二次确认，`SessionManager.DeleteSession` 物理删除磁盘历史并同步内存/网络清理。

---

## 仍未完成 / 活跃计划项

1. **GrokBot / WorkBuddy 细节微调与弹窗排版演进 (feature/wechat-ui)**：
   - Create 窗口 Docs `680×620`、About Docs `440×560` 需重排绝对布局后再放大尺寸（当前暂留紧凑可用尺寸）。
   - `GrokTheme` 设计令牌全面替换工程中遗留的零散 `Color.FromArgb`。
   - 聊天记录跨平台备份与自动同步服务后续接入。

2. **方案 B（冻结）**：56px 微信式纯黑极窄导航栏暂不实施。

---

## 已确认仍有效的关键契约

- 端口：UDP 38888，TCP 38889+；魔数 `LANC`；零第三方依赖；C# 5.0 / .NET Framework 4.0（`Tools/Compile.bat` csc）。
- 视觉规范：100% 遵守 `Docs/GrokBot_UI_Design_System.md` 与 `Rules.md` 9 条（特别注意第 9 条：Zero White Scrollbar，全应用严禁出现系统生硬白底滚动条）。
- 打包与发布规范：WiX Package 省略硬编码 ProductCode，依靠 UpgradeCode `{7E993262-EAA5-4A37-975D-39F24FEA21D1}` 实现全自动 MajorUpgrade；发布前必须自动 bump 版本号并全量编译。
