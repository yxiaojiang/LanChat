# Plan.md — 当前活跃计划

> 最近刷新：2026-10-29。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线：`main` 分支 Tag `v2.1.0` / `LanChat-v2.1.0`）。
> 核心任务：用户确认转向 **GrokBot 1:1**；P0 64px 胶囊已恢复；**本步为侧栏/菜单弹窗样式对齐**；**方案 B（56px 窄导航）冻结**。

---

## 仍未完成 / 活跃计划项

1. **GrokBot 1:1 对齐（优先于微信布局）(feature/wechat-ui)**：
   - **P0 输入胶囊恢复**：代码已完成（64px / `#202024` / `#2C2C32`）；待 Windows `CompileAndLink.bat` 热替换验收。
   - **P1 侧栏 + 菜单弹窗样式对齐（本步代码已完成，未 commit）**：
     - 全盘 `"Microsoft YaHei"` → `"Microsoft YaHei UI"`；新增 `src/UI/Controls/GrokTheme.cs` 设计令牌。
     - 左侧栏：搜索/加号 34 圆钮 Level2、Hero 昵称 12pt `#F2F2F6`、会话标题 11pt / 副标题 9pt `#8E8E93`、选中 `#28282C`、底栏用户徽标/连接胶囊 9.5 Bold / 扫描钮。
     - 菜单与弹窗：`GrokPopupMenu` / `GrokSettingsMenuForm`（行高 44）及 Settings/Connect/Create/About/ChangeNick/Feedback/Update/MessageBox 统一 Level1 `#1C1C1F`、边框 `#2C2C32`、文本阶梯；对端气泡 `#1E1E21`；主窗默认 `1040×720`。
     - **未做**：Create 窗 Docs `680×620` 需重排绝对坐标（暂留 `450×580`）；About Docs `440×560` 暂留紧凑尺寸；未触网络/协议；未再引入 160px 输入或方案 B。
   - **方案 B（冻结）**：56px 微信极窄导航栏 **暂不实施**。
   - **硬约束**：`Rules.md` 第 9 条与 `Docs/GrokBot_UI_Design_System.md`。

2. **GitHub 仓库设为 Public 并完成在线更新闭环验证**：
   - Release `v2.0.0` / `v2.1.0` 已挂载；需用户切 Public 后客户端验证【检查新版本】。

3. **实机多端互通与网络边界测试（可选演进项）**。

---

## 已确认仍有效的关键契约

- 端口：UDP 38888，TCP 38889+；魔数 `LANC`；零第三方依赖；C# 5.0 / .NET Framework 4.0（`Tools/Compile.bat` csc）。
- 视觉规范：100% 遵守 `Docs/GrokBot_UI_Design_System.md` 与 `Rules.md` 第 9 条。
