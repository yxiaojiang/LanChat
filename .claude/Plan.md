# Plan.md — 当前活跃计划

> 最近刷新：2026-11-05。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（最新版本 `v2.0.14`，发布 Tag `v2.0.14`）。
> 核心任务：**DIR-006 UI 元素定制手册编纂、协作注释规范集成与全客户端代码中文注释工程（已全部圆满完成）**。
> **成果概述**：
> 1. 成功编纂完整详尽的 UI 定制指南：`Docs/UI_Customization_Guide.md`（50KB+），涵盖三层架构、`GrokTheme.cs` 色彩与尺寸设计令牌、自绘控件、模态弹窗、矢量图标及常见修改场景的 Cookbook；
> 2. 读取并吸收 `Agreement-archive.md` 注释规范，更新 `.claude/Rules.md` 第 7 条，明确全中文、全公共元素 XML 总结说明、逻辑分块线 `// ====`、以及 UI 布局与 GDI+ 流程注释要求；
> 3. 对本项目全量代码（尤其是全部 UI 控件、设置面板、自定义渲染器、业务控制器、服务层与模型）进行了地毯式高质量中文注释补齐；
> 4. 严格遵守单文件 1000 行预警（最高 959 行）与 C# 5.0 / .NET Framework 4.0 语法约束，经系统原生 `csc.exe` 纯编译 0 警告、0 错误闭环验证。

---

## 仍未完成 / 活跃计划项

1. **发布与部署准备 (v2.0.14)**：
   - [x] UI 定制手册 `Docs/UI_Customization_Guide.md` 编纂与归档；
   - [x] 协作注释规则融入 `Rules.md` 并实施全项目中文注释注入；
   - [x] 验证原生纯编译 `release\LanChat.exe` 与 `release\uninstall.exe`（0 警告、0 错误）；
   - [ ] 待后续需求触发 WiX MSI 安装包全量构建与 Git Tag 发布。

2. **方案 B（冻结）**：56px 微信式纯黑极窄导航栏暂不实施。

---

## 最近已完成项（归档）

- [x] **DIR-006 注释规范落地与全客户端 UI 中文注释/文档工程**（2026-11-05）：
  - 编纂 `Docs/UI_Customization_Guide.md`，提供开箱即用的 UI 元素修改手册；
  - 集成 `Rules.md` 第 7 条协作注释规则；
  - 完成全部 50+ 个源文件的中文注释补强，涵盖 `WeChatChatView`、`WeChatIcon`、`GrokTheme`、`SessionListBox`、`GrokSettingsDialog`、8 大设置子面板、`SessionPresenter`、`ChatPresenter`、`FileTransferController`、`NetworkService*.cs` 等；
  - 验证单文件安全行数与 0 警告 0 错误编译通过。
- [x] **DIR-005 全客户端代码结构解耦与上帝类重构**（2026-11-05）：彻底清除全工程中 `GrokSettingsDialog`（1914 行）与 `MainForm`（3086 行紧耦合）的超限与上帝类隐患，全面落实 Rules.md 第 10 条。
- [x] **DIR-004 全客户端 UI 统一样式系统重构**（2026-11-05）：扩充 `GrokTheme.cs` 设计令牌，实现自绘 `GrokButton` 与 `GrokModalFormBase` 模态窗基类，完成全量 7 个模态窗与视口视图样式收拢，CRDocs 审核通过 (`APPROVED`)。

---

## 已确认仍有效的关键契约

- 架构契约：严格执行 `.claude/Rules.md` 第 7 条（中文注释规范与 XML 文档）及第 10 条（单文件 1000 行预警、1500 行硬上限；严禁滥用 `partial class` 伪拆分；严禁新建垃圾桶类；低侵入 MVP/Controller 真解耦）。
- 端口与通信：UDP 38888，TCP 38889+；魔数 `LANC`；零第三方依赖；C# 5.0 / .NET Framework 4.0（`Tools/Compile.bat` csc）。
- 视觉规范：100% 遵守 `Docs/GrokBot_UI_Design_System.md` 与 `Rules.md` 第 9 条（Zero White Scrollbar，全应用严禁出现系统生硬白底滚动条）。
- 打包与发布规范：WiX Package 省略硬编码 ProductCode，依靠 UpgradeCode `{7E993262-EAA5-4A37-975D-39F24FEA21D1}` 实现全自动 MajorUpgrade；发布前必须自动 bump 版本号并全量编译。
