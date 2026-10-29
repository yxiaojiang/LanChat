# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-10-29。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线 Tag `v2.1.0`）。
> 核心里程碑：**GrokBot 1:1**；P0 64px 胶囊已恢复；**本步完成侧栏图标/文字与全部菜单弹窗 YaHei UI + 色阶对齐（磁盘已改，父代理 Windows 编译，未 commit）**；方案 B 冻结。

---

## 一、 本轮工作态（P1 侧栏 + 菜单弹窗 polish）

### 1. 用户反馈
- 胶囊恢复后：左侧栏图标与文字大小仍有差别；所有菜单与弹出窗口样式也不一致（owner: 还有啊，左侧的栏…）。

### 2. 本步已改（未 commit）
- 新增 `src/UI/Controls/GrokTheme.cs`（Level0–3、文本/边框/强调色、1040×720 常量、Font 工厂）。
- 全 `src/**/*.cs` 字体统一 `Microsoft YaHei UI`。
- 侧栏：Search/Add 34 圆钮 `#202024` / 图标 `#A0A0A5` + 圆钮 inset；Hero 12pt `#F2F2F6`；会话选中 `#28282C`；标题 11 Bold / 副标题 9 `#8E8E93`；底栏用户徽标/连接胶囊 9.5 Bold / 扫描 54 蓝钮；主窗 `1040×720`。
- 聊天：对端气泡 `#1E1E21`；时间戳 9pt `#8E8E93`。
- 菜单：`GrokPopupMenu` Level1/`#2C2C32`/`#2E2E34`；`GrokSettingsMenuForm` 行高 44、图标 x=20、字 x=48。
- 弹窗：About/ChangeNick/Feedback/MessageBox/Connect/Create/Update 底色 Level1、边框与次要字色对齐；Feedback 尺寸 520×460。
- 未改网络/协议；未 commit；父代理请用 `Tools/CompileAndLink.bat` 热替换验收。

### 3. 已知剩余缺口（下一 pass）
- Create 窗 Docs `680×620`、About Docs `440×560` 需重排绝对布局后才能改尺寸。
- Settings 卡片内部分 RGB 与 54px 行已较近，仍可对照 Alis4 像素微调。
- WinForms ListBox 选中焦点矩形/原生滚动条、无 CS_DROPSHADOW 的平台差异无法 1:1 GrokBot。
- `GrokTheme` 令牌尚未全面替换散落的 `FromArgb`（本步优先可见漂移）。

---

## 二、 关键约束与设计原则

1. C# 5.0 / .NET Framework 4.0，`csc` 直编。
2. Rule 9 + `Docs/GrokBot_UI_Design_System.md`。
3. UDP 38888 / TCP 38889+ / `LANC` 不变。

---

## 三、 模块索引（增量）

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `src/UI/Controls/GrokTheme.cs` | **新增** 共享设计令牌 |
| `src/MainForm.cs` / `.Chat.cs` / `.Tray.cs` | 主窗尺寸、侧栏图标文字、会话选中、Hero 昵称、底栏 |
| `src/UI/Controls/GrokPopupMenu.cs` 等 Grok* 窗体 | 菜单/弹窗字体色阶统一 |
| `src/UI/Controls/WeChatChatView.cs` | 对端气泡 `#1E1E21` |
| `src/UI/Controls/WeChatButtons.cs` | 圆钮图标 inset |
| `Docs/GrokBot_UI_Design_System.md` | 视觉契约 |
| `Tools/CompileAndLink.bat` | Windows 热替换编译 |
