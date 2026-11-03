# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-11-03。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线 Tag `v2.1.0`，当前开发版本 `v2.0.7`）。
> 核心里程碑：**GrokBot 1:1 与现代即时通讯体验**；**本步已完成：1) 移除设置弹出菜单顶部「发现新版本」与「安装」横幅，菜单高度由 400px 精简至 332px；2) 左侧 Hero 个人头像区域改为静态不可点击（光标重置为默认箭头，移除点击呼出菜单事件）；3) 聊天顶栏对方名字与吉祥物头像整体放大 2 倍（胶囊高度 76px、圆角 38px、字体 20.0pt Bold、头像 48×48，顶栏自适应增至 96px）；4) 传统即时通讯全链路消息提醒系统（左侧会话红底白字未读徽标、Windows 任务栏 FlashWindowEx 持续黄色高亮提醒、系统托盘图标交替闪烁，用户点击窗口/托盘/会话后提示即刻自动消除）**。

---

## 一、本轮工作态（主界面视觉精细化与即时通讯提醒体验闭环）

### 1. 用户反馈与需求拆解
1. **移除设置菜单更新横幅**：左下角 mi 图标呼出的弹出菜单中，去除顶部的深蓝「发现新版本」及「安装」选项行，整体高度平滑收缩。
2. **个人头像不可点击**：左上角主 Hero 头像区域改为不可点击，恢复系统默认箭头光标，避免与底部功能入口冗余。
3. **聊天顶栏对方信息放大 2 倍**：聊天窗口顶部对方昵称胶囊与有机吉祥物头像整体视觉放大 2 倍，强化当前聊天对象辨识度。
4. **全链路即时通讯消息提醒**：
   - 会话列表左侧头像显示红点未读徽标；
   - Windows 系统任务栏图标在有新消息时呈现醒目黄色高亮闪烁；
   - 系统托盘中的图标交替闪烁；
   - 用户点击窗口、托盘或会话卡片后，相关提醒即刻消除。

---

### 2. 核心架构设计与落地细节

#### (1) 设置菜单精炼 (`GrokSettingsMenuForm`)
- 移除顶栏 `BANNER_HEIGHT = 58` 相关常数与绘制逻辑，窗体尺寸从 `342×400` 精简至 `342×332`。
- 移除了横幅高亮命中判断与点击安装检测，菜单项从顶部 `TOP_PADDING = 8` 开始直接渲染 7 个主要设置项与分割线。
- 在 `MainForm.Tray.cs` 的 `BtnSettings_Click` 中同步更新弹出基准高度 `menuH = 332`，保证菜单在底部 mi 圆钮上方精准悬浮。

#### (2) 个人头像静态化 (`MainForm.cs`)
- 将 `_heroProfilePanel` 的 `Cursor` 从 `Cursors.Hand` 调整为 `Cursors.Default`。
- 移除 `_heroProfilePanel.Click += BtnAvatar_Click;` 事件挂载，彻底去除误触弹窗干扰，操作统一归流到底部 mi 按钮。

#### (3) 聊天顶栏对端信息 2 倍放大 (`MainForm.cs`)
- 顶栏控件 `_chatHeader` 高度从 `64px` 调整至 `96px`，右侧最小化/最大化/关闭按钮尺寸同步自适应至 `56×96`（内部矢量图标依然精准居中）。
- 居中悬浮胶囊尺寸与元素全面放大 2 倍：
  - 胶囊高度 `pillH` 从 `38px` 增大为 `76px`，圆角半径从 `19px` 增大为 `38px`，最小宽度设为 `240px`；
  - 对方吉祥物头像从 `24×24` 增大为 `48×48`，左内边距与垂直内边距按 2 倍比例重排；
  - 标题文字字号由 `10.0pt Bold` 放大为 `20.0pt Bold`，字体为微软雅黑粗体，文本区域在胶囊内居中对齐。

#### (4) 全链路消息提醒与即时消除系统 (`MainForm.Chat.cs` & `MainForm.Tray.cs`)
- **未读状态追踪**：`PeerInfo` 的 `UnreadCount` 字段结合新增的 `_broadcastUnreadCount`，在收到文本或文件消息时智能判断当前会话是否正处于活动且可见状态（`isForeground && isCurrentChat`）；若非正在查看，则未读计数累加并触发外围提醒。
- **左侧会话红点徽标 (`DrawUnreadBadge`)**：在会话卡片 48px 头像右上角自绘 `#FA5151` 微信红圆角徽章，附带 `#0E0E0E` 底色隔离边框与微软雅黑 8.0pt 粗体白色数字（超过 99 条显示 `99+`）。
- **Windows 任务栏黄色高亮 (`FlashTaskbar`)**：通过 P/Invoke 调用系统原生 `FlashWindowEx`，配置标志位 `FLASHW_ALL | FLASHW_TIMERNOFG`，让 Windows 任务栏按钮持续黄色闪烁直到用户聚焦交互。
- **系统托盘闪烁 (`StartTrayFlash` / `StopTrayFlash`)**：构建 16×16 透明空白图标，启用 `450ms` 的 `_trayFlashTimer` 定时器，在原应用图标与透明图标间交替翻转，重现经典聊天软件托盘闪烁。
- **点击消除闭环 (`ClearActiveChatUnread` & `MainForm_Activated`)**：
  - 窗体被激活（点击任务栏、快捷键切入）时，调用 `MainForm_Activated` 立即重置任务栏黄色状态并停止托盘闪烁；
  - 还原托盘窗口（`RestoreFromTray`）时立即停止托盘闪烁；
  - 用户点击会话列表项、聊天记录、输入框或顶栏时，清空当前活动会话的未读计数；若全应用未读数归零，同步停止外围所有声光提示。

---

## 二、关键约束与设计原则

1. **C# 5.0 / .NET Framework 4.0**：系统原生 `csc.exe` 极速直编，体积控制在几十 KB 级别，零第三方重型依赖。
2. **视觉规范**：100% 遵守 `Docs/GrokBot_UI_Design_System.md` 与 `.claude/Rules.md` 9 大硬约束。
3. **网络契约**：UDP 38888 / TCP 38889+ / LANC 二进制协议向前向后兼容。

---

## 三、增量模块索引

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `src/UI/Controls/GrokSettingsMenuForm.cs` | 移除更新横幅，精简为 332px 高度紧凑暗黑菜单 |
| `src/MainForm.cs` | 个人头像改为 Default 光标且不可点击；顶栏放大 2 倍至 96px（76px 胶囊、20pt Bold 标题、48px 吉祥物头像） |
| `src/MainForm.Chat.cs` | 会话自绘新增 `DrawUnreadBadge` 微信红徽标；接收消息触发任务栏与托盘提醒；点击消除未读 |
| `src/MainForm.Files.cs` | 文件接收统一纳入未读状态与任务栏/托盘提醒流 |
| `src/MainForm.Tray.cs` | 新增 Win32 `FlashWindowEx` 任务栏高亮、托盘图标 450ms 定时交替闪烁与全局清除调度 |
