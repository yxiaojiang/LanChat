# Plan.md — 当前活跃计划

> 最近刷新：2026-11-04。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（已发布 Tag `v2.0.11`，当前版本 `v2.0.11`）。
> 核心任务：**WorkBuddy 风格设置中心重构、0ms 瞬时切页、全应用消灭生硬白底滚动条 (Zero White Scrollbar)**；
> **本阶段已全部完成并正式发布至 GitHub Releases (v2.0.11)**：
> 1) WorkBuddy 风格设置中心 (`GrokSettingsDialog.cs`)：扩大 1.5 倍（1260×820）、右上角自绘关闭按钮、药丸下拉单行不折行、字体样式字号定制；
> 2) 0ms 瞬间无感切页与消除瀑布流刷新：Tab 页面全量预构建内存缓存（`_tabPages`），窗口及容器注入 `WS_CLIPCHILDREN` 与 `WS_EX_COMPOSITED` 硬件离屏双缓冲；
> 3) 全局消灭生硬白底滚动条契约：`Rules.md` 写入第 9 条硬约束；`SessionListBox`、`WeChatChatView`、`GrokSettingsDialog` 全量纯自绘暗灰极窄滑块；
> 4) 规范更新日志 `CHANGELOG.md` 与自动化发布工具 `packaging/publish-release.py`。

---

## 仍未完成 / 活跃计划项

1. **GrokBot / WorkBuddy 细节微调与弹窗排版演进 (feature/wechat-ui)**：
   - Create 窗口 Docs `680×620`、About Docs `440×560` 需重排绝对布局后再放大尺寸（当前暂留紧凑可用尺寸）。
   - `GrokTheme` 设计令牌全面替换工程中遗留的零散 `Color.FromArgb`。
   - 聊天记录自动保存与跨平台备份管理服务后续接入。

2. **方案 B（冻结）**：56px 微信式纯黑极窄导航栏暂不实施。

---

## 已确认仍有效的关键契约

- 端口：UDP 38888，TCP 38889+；魔数 `LANC`；零第三方依赖；C# 5.0 / .NET Framework 4.0（`Tools/Compile.bat` csc）。
- 视觉规范：100% 遵守 `Docs/GrokBot_UI_Design_System.md` 与 `Rules.md` 9 条（特别注意第 9 条：Zero White Scrollbar，全应用严禁出现系统生硬白底滚动条）。
- 打包与发布规范：WiX Package 省略硬编码 ProductCode，依靠 UpgradeCode `{7E993262-EAA5-4A37-975D-39F24FEA21D1}` 实现全自动 MajorUpgrade；发布前必须自动 bump 版本号并全量编译。
