# Plan.md — 当前活跃计划

> 最近刷新：2026-11-03。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线 Tag `v2.1.0`，当前开发版本 `v2.0.7`）。
> 核心任务：**GrokBot 1:1 对齐与即时通讯体验演进**；
> **本步已完成**：
> 1) 全局版权所有者由 Aritex 修改为 ChaseFraser（关于窗口、设置页、AppUserModelID、MSI 制造商）；
> 2) 右上角窗口三联控制按钮尺寸精准还原为 56×64 标准长宽（紧贴右上角，消除纵向过度拉伸）；
> 3) 外机部署图标完全一致性保障（/res 程序集嵌入资源流 + 动态 Mascot 兜底自绘 + MSI 打包物理释放 app.ico，彻底清除红黄蓝原生图标）；
> 4) 底部输入胶囊小十字图标放大 1.5 倍、去除语音麦克风图标，输入框宽度自适应延伸至发送按钮。

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
