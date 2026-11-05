# Plan.md — 当前活跃计划

> 最近刷新：2026-11-05。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（已发布 Tag `v2.0.13`，当前版本 `v2.0.13`）。
> 核心任务：**DIR-004 全客户端 UI 统一样式系统重构（扩充 GrokTheme 设计令牌、标准化模态窗基类与自绘按钮体系）**；
> **本阶段目标**：
> 彻底解决全客户端窗口尺寸、按钮大小、按钮文字大小、菜单间距、多行文字间距、Padding、右上角关闭叉各自硬编码割裂的问题；
> 通过在 `GrokTheme.cs` 集中定义设计令牌（类似 CSS 根样式表）、抽象 `GrokModalFormBase` 模态窗基类与 `GrokButton` 自绘控件，实现“单点修改配置文件，全工程所有同类窗口与控件瞬间同步”。

---

## 仍未完成 / 活跃计划项

1. **DIR-004 全客户端 UI 统一样式系统重构 (feature/wechat-ui) [已全量实施闭环]**：
   - [x] **第 1 步 (P0-Tokens，扩充 `GrokTheme.cs`)**：已集中补齐窗口标准尺寸、按钮规格、右上角关闭叉规范、动态字体与内边距/行高 Tokens；
   - [x] **第 2 步 (P0-BaseFramework，构建 `GrokButton.cs` 与 `GrokModalFormBase.cs`)**：已建立继承自 `Button` 的多角色 GDI+ 抗锯齿自绘按钮与标准无边框圆角模态窗基类；
   - [x] **第 3 步 (P0-SmallModals，小型操作弹窗迁移)**：已完成 `GrokMessageBox`（460×260）、`GrokChangeNickForm`（440×260）、`GrokAboutForm`（440×560）继承基类，清理 Win32 样板代码，替换为 `GrokButton`；
   - [x] **第 4 步 (P0-MediumModals，中大型内容弹窗迁移)**：已完成 `GrokFeedbackForm`（520×460）、`GrokUpdateForm`（520×440）、`GrokCreateNodeForm`（680×620）、`GrokConnectNodeForm`（760×600）继承基类，替换自绘按钮与自适应对齐；
   - [x] **第 5 步 (P1-MainViews，收拢主视口、会话列表与设置窗口 Tokens)**：已完成 `SessionListBox` 行高/Margin/头像间距、`WeChatChatView` 气泡内边距/行高加量/时间戳字号及文件卡片尺寸对齐 `GrokTheme`，完成 `GrokSettingsDialog` 按钮与颜色引用对齐；通过 `Tools/Compile.bat` 原生编译全绿验证。

2. **方案 B（冻结）**：56px 微信式纯黑极窄导航栏暂不实施。

---

## 已确认仍有效的关键契约

- 端口：UDP 38888，TCP 38889+；魔数 `LANC`；零第三方依赖；C# 5.0 / .NET Framework 4.0（`Tools/Compile.bat` csc）。
- 视觉规范：100% 遵守 `Docs/GrokBot_UI_Design_System.md` 与 `Rules.md` 9 条（特别注意第 9 条：Zero White Scrollbar，全应用严禁出现系统生硬白底滚动条）。
- 打包与发布规范：WiX Package 省略硬编码 ProductCode，依靠 UpgradeCode `{7E993262-EAA5-4A37-975D-39F24FEA21D1}` 实现全自动 MajorUpgrade；发布前必须自动 bump 版本号并全量编译。
