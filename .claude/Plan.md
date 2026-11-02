# Plan.md — 当前活跃计划

> 最近刷新：2026-11-02。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线 Tag `v2.1.0`，当前开发版本 `v2.0.6`）。
> 核心任务：**GrokBot 1:1 对齐**；
> **已完成**：
> 1) Windows 搜索栏整行自适应布局（移除加号，搜索按钮放大 2 倍，整行比例适中）；
> 2) 左下角移除蓝色向上箭头扫描图标；
> 3) 偏好与配置功能集成至左下角 mi 图标菜单首项，支持右键直接呼出配置；
> 4) 主界面无边框窗口四周与四角支持鼠标拖拽缩放（FormResizeMessageFilter 原生拦截）；
> 5) 聊天流自绘消息支持鼠标划选高亮、IBeam 光标、双击全选、Ctrl+C 复制与右键深色菜单复制；
> 6) WiX MSI 与 Linux deb 全自动构建与 GitHub Releases (v2.0.6) 自动化发布。

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
