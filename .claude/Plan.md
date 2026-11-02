# Plan.md — 当前活跃计划

> 最近刷新：2026-11-02。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线 Tag `v2.1.0`，当前开发版本 `v2.0.4`）。
> 核心任务：**GrokBot 1:1 对齐**；**已完成 Windows 句柄/弹窗裁切修复、Linux 客户端防死锁与暗黑重构、自动版本递增发布、MSI 大版本无缝升级根治 (动态 ProductCode)**；方案 B 冻结。

---

## 仍未完成 / 活跃计划项

1. **GrokBot 1:1 细节微调与弹窗排版演进 (feature/wechat-ui)**：
   - Create 窗 Docs `680×620`、About Docs `440×560` 需重排绝对布局后再放大尺寸（当前暂留紧凑可用尺寸）。
   - Settings 卡片内部分色值与 54px 行高已非常接近，待后续像素级对照微调。
   - `GrokTheme` 设计令牌全面替换工程中遗留的零散 `Color.FromArgb`。

2. **多端发布与在线更新闭环验证 (v2.0.4)**：
   - Windows 端已彻底消除 1638 安装拦截，旧版本（2.0.0/2.0.1/2.0.3）点击【检查更新】可平滑 MajorUpgrade 自动替换升级至 `v2.0.4`；
   - 验证 Linux deb 包在实体 Linux 机上的无死锁运行与流式文件传输。

3. **方案 B（冻结）**：56px 微信式纯黑极窄导航栏暂不实施。

---

## 已确认仍有效的关键契约

- 端口：UDP 38888，TCP 38889+；魔数 `LANC`；零第三方依赖；C# 5.0 / .NET Framework 4.0（`Tools/Compile.bat` csc）。
- 视觉规范：100% 遵守 `Docs/GrokBot_UI_Design_System.md` 与 `Rules.md` 第 9 条。
- 打包与发布规范：WiX Package 省略硬编码 ProductCode，依靠 UpgradeCode `{7E993262-EAA5-4A37-975D-39F24FEA21D1}` 实现全自动 MajorUpgrade；发布前必须自动 bump 版本号并全量编译。
