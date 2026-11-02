# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-11-02。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线 Tag `v2.1.0`，当前开发版本 `v2.0.1`）。
> 核心里程碑：**GrokBot 1:1**；P0 64px 胶囊已恢复；**本步已完成：1) 发布自动版本递增（Auto Version Bump 2.0.0->2.0.1）与全工程原子同步；2) Windows 弹窗半边裁切（Region 与 OnResize 动态绑定）彻底根治；3) Linux 客户端死锁消除（queue 线程安全队列）并对标 GrokBot 暗黑系统；4) Publish.bat 失败重试与全量编译优化**；方案 B 冻结。
> 注：WinForms/GDI+ 无法与 Electron 像素级完全一致；本步用 grayscale AntiAlias + Bold/luma 逼近。

---

## 一、 本轮工作态（版本自增与全工程同步 / 弹窗裁切根治 / Linux 卡死与视觉重构）

### 1. 用户反馈
- 每次 Publish 发布后希望版本号自动递增，否则旧客户端因版本号未变无法检测到在线更新。
- 弹窗只出现一半（`GrokMessageBox` / 更新窗口右下侧被裁切）。
- Linux `lanchat_2.0.0_all.deb` 界面非常丑陋、图标缺失、布局与 Windows 不一致、按钮无反应且无法关闭（必须强杀）。
- `Publish.bat` 出现 DNS 解析异常（`Errno 11001`）导致中断。

### 2. 修复与现状
- **全工程版本号自动自增与同步机制（新增 `packaging/bump-version.py`）：**
  - 在 `Tools/Publish.bat` [0/4] 阶段自动调用，每次运行自动将 patch 号 +1（亦支持手动指定如 `Publish.bat 2.1.0`）；
  - 原子同步更新 5 处核心定义：`src/MainForm.cs`、`src/Services/UpdateService.cs`、`packaging/Package.wxs`、`src/linux/lanchat.py`、`packaging/build-deb.py`。
  - 当前全工程版本已平滑升级为 **`v2.0.1`**。
- **Windows 弹窗半边裁切根治：**
  - 在 `GrokMessageBox`、`GrokUpdateForm`、`GrokAboutForm`、`GrokChangeNickForm`、`GrokFeedbackForm`、`GrokConnectNodeForm`、`GrokCreateNodeForm` 中统一封装 `UpdateFormRegion()`，挂载 `OnResize` 与 `OnHandleCreated`，并调用 Win32 `DeleteObject(rgn)` 防止 GDI 泄漏；
  - `GrokMessageBox` 文本测量改用静态 `TextRenderer.MeasureText`，彻底解决构造函数内 `CreateGraphics()` 导致句柄提前在 300x300 下固化裁剪区的根本 Bug。实测 `Region Bounds` 与窗体 `Size` 100% 动态对齐。
- **Linux 客户端彻底重构（`src/linux/lanchat.py`）：**
  - **杜绝死锁假死：** 引入 `queue.Queue` 线程安全队列，所有网络探测与监听子线程仅向队列投递事件；主线程通过 25ms 定时器 `_poll_event_queue` 消费事件并刷新 UI，彻底根治 Linux/X11 下 Tcl/Tk 跨线程调用引发的死锁，所有按钮与窗口关闭毫秒级响应。
  - **杜绝图标缺失/方块：** 彻底剔除 Unicode 彩色 Emoji 依赖，使用专业字符与矢量绘制。
  - **视觉系统全面对标 GrokBot：** 放弃陈旧的“微信绿”，全面采用 Grok 魅紫 `#8E64DC` + 纯黑 `#121212` / `#1C1C20` 卡片体系；引入自适应跨平台字体探测链（`WenQuanYi Micro Hei` / `Noto Sans CJK SC`），杜绝生硬粗糙点阵字体。
  - **安装包已同步重建：** 重新生成 `release/lanchat_2.0.1_all.deb` 与 `release/lanchat_2.0.1_amd64.deb`。
- **发布脚本优化（`Tools/Publish.bat` & `packaging/publish-release.py`）：**
  - 强制发布前调用系统原生 `csc.exe` 完整编译最新二进制；自动注入 `%USERPROFILE%\.dotnet\tools` 环境变量；
  - 上传二进制资产增加 3 次自动退避重试，全量捕获 `URLError` 与 DNS 解析抖动，告别 Python Traceback 刷屏。

### 3. 已知剩余缺口（下一 pass）
- Create 窗 Docs `680×620`、About Docs `440×560` 需重排绝对布局后才能改尺寸。
- Settings 卡片内部分 RGB 与 54px 行已较近，仍可对照 Alis4 像素微调。
- WinForms ListBox 原生滚动条、无 CS_DROPSHADOW 的平台差异无法 1:1 GrokBot。
- `GrokTheme` 令牌尚未全面替换散落的 `FromArgb`。

---

## 二、 关键约束与设计原则

1. C# 5.0 / .NET Framework 4.0，`csc` 直编。
2. Rule 9 + `Docs/GrokBot_UI_Design_System.md`。
3. UDP 38888 / TCP 38889+ / `LANC` 不变。

---

## 三、 模块索引（增量）

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `src/UI/Controls/SessionListBox.cs` | **新增** 双缓冲 owner-draw 会话列表 |
| `src/UI/Controls/GrokTheme.cs` | SessionHover / TextSubtitle 令牌 |
| `src/MainForm.cs` / `.Chat.cs` | SessionListBox、hover、选中逻辑、DrawItem 密排 |
| `src/UI/Controls/WeChatChatView.cs` | ShowGroupSenderName 防双 layout |
| `src/UI/Controls/GrokMessageBox.cs` 等 | 弹窗 Region 与 OnResize 动态贴合 |
| `src/linux/lanchat.py` | Linux 客户端防死锁 (queue) 与 GrokBot 暗黑对齐 |
| `packaging/bump-version.py` | 全工程版本号自动递增与原子同步工具 |
| `Tools/Publish.bat` | Windows 全自动版本自增编译打包与发布脚本 |
| `Tools/CompileAndLink.bat` | Windows 热替换编译 |
