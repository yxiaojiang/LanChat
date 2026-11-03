# Plan.md — 当前活跃计划

> 最近刷新：2026-11-03。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（已发布 Tag `v2.0.9`，当前版本 `v2.0.9`）。
> 核心任务：**GrokBot 1:1 对齐与即时通讯体验演进**；
> **本阶段已全部完成并正式发布至 GitHub Releases (v2.0.9)**：
> 1) 魅紫发送按钮默认深灰禁用（#2C2C32），仅当输入框包含非空文本时即时点亮为明亮魅紫（#8E64DC，Hover #9D75EC，光标变为小手），消息发出后自动回退；
> 2) 全局 ToolTip 遵循 Windows 标准无焦点层级（WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST），引入多显示器工作区边界检测与自适应避让（Clamping & Auto-Flip），底栏按钮提示默认置于正上方居中，彻底根除超出屏幕被截断的问题；
> 3) 搜索框内新增自适应抗锯齿圆头 GDI+ 矢量小叉按钮（Dismiss，1.6px 圆头笔触），输入查找内容时浮现，点击一键清空搜索词并恢复所有会话；
> 4) 左侧会话卡片右键菜单移除冗余的“发起会话”与“复制节点 IP”，保持清爽高效的“发送文件...”与“清空聊天记录”；
> 5) Windows 与 Linux 双端功能全面对齐：将发送按钮未输入置灰点亮联动、搜索框小叉一键清空、会话卡片右键上下文菜单（发送文件/清空记录）、全员群聊未读徽标与 ChaseFraser 版权标示 100% 同步至 Linux 端（src/linux/lanchat.py），并重新打包全架构通用与 amd64 自包含 deb 安装包；
> 6) 运行 `packaging/publish-release.py` 成功推送 Git 提交并更新发布至 GitHub Releases。

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
