# Plan.md — 当前活跃计划

> 最近刷新：2026-11-08。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（最新版本 `v2.0.14`，发布 Tag `v2.0.14`）。
> 核心任务：**DIR-007 全客户端 UI 定制手册针对解耦后新架构的全面迭代与更新（已全部圆满完成）**。
> **成果概述**：
> 1. 全面重构并升级 `Docs/UI_Customization_Guide.md`：建立与 DIR-005 解耦后完全对齐的「设计令牌层 $\to$ 基础通用自绘与渲染引擎 $\to$ 视图与排版层 $\to$ MVP 业务控制层」四层体系；
> 2. 补齐原文档缺失的关键组件（`GrokTrayPreviewPopup.cs` 托盘悬浮预览卡片、`WeChatDarkMenuRenderer.cs` & `WeChatDarkTooltip` 暗黑气泡与菜单、`FormResizeMessageFilter.cs` 无边框缩放、`GrokSettingsMenuForm.cs` 齿轮快捷菜单）；
> 3. 彻底纠正 8 大设置子面板的真实卡片与控件映射索引，修正主窗体 5 个分部类与 Presenter 层的职责映射；
> 4. 扩充实战 Cookbook 场景（增至 8 个典型修改范例）；
> 5. 验证原生纯编译 `Tools\Compile.bat` 0 警告、0 错误。

---

## 仍未完成 / 活跃计划项

1. **发布与部署准备 (v2.0.14)**：
   - [x] UI 定制手册 `Docs/UI_Customization_Guide.md` 针对解耦新框架全面迭代更新；
   - [x] 验证原生纯编译 `release\LanChat.exe` 与 `release\uninstall.exe`（0 警告、0 错误）；
   - [ ] 待后续需求触发 WiX MSI 安装包全量构建与 Git Tag 发布。

2. **方案 B（冻结）**：56px 微信式纯黑极窄导航栏暂不实施。

---

## 最近已完成项（归档）

- [x] **DIR-007 UI 定制手册新架构迭代与组件补齐**（2026-11-08）：针对 DIR-005 上帝类拆解后的新框架体系，全面重构 `Docs/UI_Customization_Guide.md`，精准映射所有 8 大设置面板、9 大弹窗与 3 大控制器层。
- [x] **DIR-006 注释规范落地与全客户端 UI 中文注释/文档工程**（2026-11-05）：编纂初始定制文档，集成 `Rules.md` 注释规范并完成全工程地毯式注释覆盖。
- [x] **DIR-005 全客户端代码结构解耦与上帝类重构**（2026-11-05）：彻底清除 `GrokSettingsDialog`（1914 行）与 `MainForm`（3086 行）上帝类隐患，收敛至安全区间。
- [x] **DIR-004 全客户端 UI 统一样式系统重构**（2026-11-05）：扩充 `GrokTheme.cs`，统一自绘 `GrokButton` 与 `GrokModalFormBase`。

---

## 已确认仍有效的关键契约

- 架构契约：严格执行 `.claude/Rules.md` 第 7 条（中文注释规范与 XML 文档）及第 10 条（单文件 1000 行预警、1500 行硬上限；严禁滥用 `partial class` 伪拆分；严禁新建垃圾桶类；低侵入 MVP/Controller 真解耦）。
- 端口与通信：UDP 38888，TCP 38889+；魔数 `LANC`；零第三方依赖；C# 5.0 / .NET Framework 4.0（`Tools/Compile.bat` csc）。
- 视觉规范：100% 遵守 `Docs/GrokBot_UI_Design_System.md` 与 `Rules.md` 第 9 条（Zero White Scrollbar，全应用严禁出现系统生硬白底滚动条）。
- 打包与发布规范：WiX Package 省略硬编码 ProductCode，依靠 UpgradeCode `{7E993262-EAA5-4A37-975D-39F24FEA21D1}` 实现全自动 MajorUpgrade；发布前必须自动 bump 版本号并全量编译。
