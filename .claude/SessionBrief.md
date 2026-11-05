# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-11-05。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（已发布 Tag `v2.0.13`，当前版本 `v2.0.13`）。
> 核心任务：**DIR-004 全客户端 UI 统一样式系统重构（扩充 GrokTheme 设计令牌、标准化模态窗基类与自绘按钮体系）已收敛采纳**。
> 本轮通过 PlanDocs 双模型独立评审（Argon 审核），确认了“以 `GrokTheme.cs` 为单点样式配置中心（类似 CSS Custom Properties）、抽象 `GrokModalFormBase` 模态窗基类、构建 `GrokButton` 统一样式自绘按钮”的重构路径，彻底消除散落的 114 处 `new Font`、383 处 `Color.FromArgb` 与各窗口尺寸、间距、按钮各立山头的问题。

---

## 一、本轮工作态（DIR-004 方案评审收敛与采纳）

### 1. 核心需求与背景
用户提出全客户端 UI 统一样式需求：多类型窗口及其内部元素（窗口大小、菜单大小与间距、字体大小、多行文字间距、Padding、按钮尺寸与文字大小、右上角关闭叉）必须使用统一配置方式管理（类似 CSS 样式表），单点修改配置文件即可全盘同步。

### 2. 现状排查结论
1. 全工程存在 114 处 `new Font(...)` 与 383 处 `Color.FromArgb(...)`；
2. 现有 7 个模态弹窗全部在各自构造函数中硬编码尺寸，且与 `Docs/GrokBot_UI_Design_System.md` 规范存在脱节（如 AboutForm 现 360×370 vs 规范 440×560）；
3. 界面中缺乏统一样式的 Button 控件，各弹窗使用原生 Button 并挂载 20~30 行匿名 `Paint` 事件以 GDI+ 绘制圆角；
4. 右上角关闭按钮尺寸（22px、24px、26px、36px）及命中坐标各异；
5. 多行行间距（如 `+ 5f`）和内边距属于魔法数字。

### 3. 讨论收敛与采纳决定 (DIR-004)
- **不新建类**：全量设计令牌直接收口扩充至 `src/UI/Controls/GrokTheme.cs`；
- **拒绝一刀切**：严格按设计规范第九节为各窗口定义基准尺寸（`SizeAbout` 440×560、`SizeMessageBox` 460×260 等），避免按钮裁切；
- **原生接口保障**：自绘 `GrokButton` 继承自 `Button`，天然具备 `IButtonControl`（Enter 确定 / ESC 取消 / DialogResult）；
- **基类抽象**：构建 `GrokModalFormBase` 接管无边框圆角、DWM 阴影、拖拽与右上角关闭叉；
- **安全隔离**：`GrokSettingsDialog`（1952 行）仅对齐 Token 和自绘按钮，不重写其基类。

---

## 二、实施闭环与交付进展 (DIR-004 已收口完成)

1. **第 1 步 (P0-Tokens，扩充 `GrokTheme.cs`)**：
   - 在 `GrokTheme.cs` 补齐各窗体标准基准尺寸常量（`SizeMainWindow` 1040×720、`SizeSettings` 920×640/1260×820、`SizeConnectNode` 760×600、`SizeCreateNode` 680×620、`SizeAbout` 440×560、`SizeFeedback` 520×460、`SizeUpdate` 520×440、`SizeMessageBox` 460×260、`SizeChangeNick` 440×260）；
   - 补齐按钮规格（标准 36px、药丸 32px、水平内距 18px、圆角 8px/胶囊）、关闭叉规格（28×28px、距边 16px）、动态字体属性与内边距/行高 Tokens；
2. **第 2 步 (P0-BaseFramework，构建基础构件)**：
   - 构建 `src/UI/Controls/GrokButton.cs`：继承 `Button`，原生实现 `IButtonControl`（Enter/ESC/DialogResult），提供 Primary、Secondary、Destructive、Pill 四种角色纯 GDI+ 抗锯齿自绘；
   - 构建 `src/UI/Controls/GrokModalFormBase.cs`：继承 `Form`，标准化接管 16px 圆角 Region、DWM 阴影、鼠标拖拽、1px 微光边框与标准右上角自绘关闭叉；
3. **第 3 步 (P0-SmallModals，小型操作弹窗迁移)**：
   - `GrokMessageBox`（460×260）、`GrokChangeNickForm`（440×260）、`GrokAboutForm`（440×560）全量迁移继承 `GrokModalFormBase`，剥离 Win32 API 样板代码，按钮全量替换为 `GrokButton`；
4. **第 4 步 (P0-MediumModals，中大型内容弹窗迁移)**：
   - `GrokFeedbackForm`（520×460）、`GrokUpdateForm`（520×440）、`GrokCreateNodeForm`（680×620）、`GrokConnectNodeForm`（760×600）全量迁移继承 `GrokModalFormBase`，替换自绘按钮并重构自适应布局；
5. **第 5 步 (P1-MainViews，主视图与设置窗口 Tokens 收拢)**：
   - `SessionListBox` 会话项高度（72px）、卡片 Margin、头像间距对齐 `GrokTheme`；
   - `WeChatChatView` 气泡内边距、行高加量（`+ 5f`）、时间戳字号及文件卡片尺寸对齐 `GrokTheme`；
   - `GrokSettingsDialog` 颜色令牌与按钮引用全量收口对齐 `GrokTheme`。
6. **验证结果**：
   - 纯原生 C# 5.0 / .NET Framework 4.0 `csc.exe` 极速纯编译验证通过，零错误零警告。

---

## 三、增量模块索引

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `PlanDocs/DECISIONS.md` | 记录 DIR-004 已采纳计划决定 |
| `PlanDocs/STATUS.md` | 状态标定为 IDLE |
| `.claude/Plan.md` | 标记 DIR-004 实施 5 步全量完成 |
| `src/UI/Controls/GrokTheme.cs` | 已扩充：全客户端集中设计令牌、窗口尺寸与自绘几何工具 |
| `src/UI/Controls/GrokButton.cs` | 已新建：继承 Button，多角色自绘抗锯齿按钮控件 |
| `src/UI/Controls/GrokModalFormBase.cs` | 已新建：通用无边框圆角模态窗基类 |
| `src/UI/Controls/GrokMessageBox.cs` | 已重构：继承基类，标准尺寸 460×260，GrokButton |
| `src/UI/Controls/GrokChangeNickForm.cs` | 已重构：继承基类，标准尺寸 440×260，GrokButton |
| `src/UI/Controls/GrokAboutForm.cs` | 已重构：继承基类，标准尺寸 440×560，GrokButton |
| `src/UI/Controls/GrokFeedbackForm.cs` | 已重构：继承基类，标准尺寸 520×460，GrokButton |
| `src/UI/Controls/GrokUpdateForm.cs` | 已重构：继承基类，标准尺寸 520×440，GrokButton |
| `src/UI/Controls/GrokCreateNodeForm.cs` | 已重构：继承基类，标准尺寸 680×620，GrokButton |
| `src/UI/Controls/GrokConnectNodeForm.cs` | 已重构：继承基类，标准尺寸 760×600，GrokButton |
| `src/UI/Controls/GrokSettingsDialog.cs` | 已对齐：颜色令牌、关闭叉及操作胶囊对齐 GrokTheme |
| `src/UI/Controls/WeChatChatView.cs` | 已对齐：行高加量、气泡内边距与文件卡片对齐 GrokTheme |
| `src/MainForm.Chat.cs` | 已对齐：SessionListBox 绘制间距与头像规格对齐 GrokTheme |
