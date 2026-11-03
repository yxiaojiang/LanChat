# Plan.md — 当前活跃计划

> 最近刷新：2026-11-03。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（已发布 Tag `v2.0.10`，当前版本 `v2.0.10`）。
> 核心任务：**GrokBot 1:1 对齐与即时通讯体验演进**；
> **本阶段已全部完成并正式发布至 GitHub Releases (v2.0.10)**：
> 1) 托盘未读悬浮预览卡片与多联系人列表展示 (`GrokTrayPreviewPopup.cs`)；
> 2) 托盘文字防截断与 63 字符安全截断防护；
> 3) 昵称与偏好持久化保存服务 (`AppSettings.cs`)，升级防丢失（Windows & Linux 双端对齐）；
> 4) 规范更新日志文档 `CHANGELOG.md`；
> 5) 自动化打包构建 WiX 5 MSI 与 Linux deb 包，发布至 GitHub Releases。

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
