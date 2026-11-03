# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-11-03。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（已发布 Tag `v2.0.10`，当前版本 `v2.0.10`）。
> 核心里程碑：**托盘未读悬浮预览卡片与多联系人列表、跨版本升级昵称持久化保存服务（Windows & Linux 双端对齐）**；**本阶段已全部完成并正式发布至 GitHub Releases (v2.0.10)**：
> 1) 托盘未读消息悬浮预览卡片 (`GrokTrayPreviewPopup.cs`)：对标微信/QQ桌面端，托盘图标闪烁时鼠标悬停展示无激活置顶卡片（`WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST | CS_DROPSHADOW`），多联系人列表展示（头像、昵称、未读角标、消息摘要、接收时间），条目悬停高亮，点击立即还原主界面并跳转对应会话；支持多屏工作区智能翻转与边界防溢出，鼠标移开 300ms 平滑隐藏；
> 2) 原生托盘提示保护：格式化 `_trayIcon.Text` 为 `LanChat: X条未读 (联系人1、联系人2)` 并严格做 60 字符截断，规避 WinForms 越界异常；
> 3) 昵称与偏好持久化保存体系 (`AppSettings.cs`)：解决软件升级（MSI 安装包、热覆盖、二进制更新）后用户自定义昵称被重置为操作系统系统名的问题；Windows 端优先同级便携模式，默认存储于 `%AppData%\LanChat\settings.json`；
> 4) Linux 端跨平台 100% 对齐 (`src/linux/lanchat.py`)：实现 `get_settings_file_path()`、`load_settings()`、`save_settings()`，配置漫游至 `~/.config/lanchat/settings.json`（回退 `~/.local/share/lanchat/settings.json`），GUI 与终端 `/nick` 改名均即时落盘与自动恢复；
> 5) 根目录新增工程规范更新日志 `CHANGELOG.md`，记录全量演进历史；
> 6) 执行 `Tools/Publish.bat` 成功原子同步全工程版本至 `v2.0.10`，全自动打包生成 WiX 5 `LanChat.msi`、全架构通用与 amd64 `deb` 安装包，并成功发布至 GitHub Releases (v2.0.10)。

---

## 一、本轮工作态（托盘消息预览、配置持久化与跨平台对齐）

### 1. 用户反馈与需求拆解
1. **托盘闪烁消息预览与多联系人列表**：当有人发消息托盘图标闪烁时，鼠标悬停需展示消息预览和发送者；若有多人发送需以列表展示（对标 QQ / 微信桌面端）。
2. **用户改名持久化保存**：解决用户更改昵称后更新软件昵称被重置为默认名的问题，需在升级覆盖后永久保留，并保证 Windows 与 Linux 双端一致。
3. **自动化发布与更新日志**：运行 `publish.bat` 构建发布，并写入规范更新日志。

---

### 2. 核心架构设计与落地细节

#### (1) GrokBot 风格托盘未读悬浮预览卡片 (`GrokTrayPreviewPopup.cs` & `MainForm.Tray.cs`)
- 继承 `Form` 实现无焦点弹出（`ShowWithoutActivation = true`），避免打断当前用户操作。
- 采用智能工作区几何测算与多显示器避让，自动根据系统任务栏位置（底部/顶部/边缘）自适应弹出方向与坐标夹紧。
- 多人消息列表渲染：根据未读数据实体 `TrayUnreadItem`，实时渲染群聊徽标、好友 Mascot 头像、未读计数气泡、最新一条消息摘要与时间。
- 交互事件闭环：条目点击回调自动唤醒主窗口并调用 `SwitchToSession(convKey)` 定位会话并清空未读计数；主窗口激活、会话选中、托盘还原或右键菜单弹出时自动关闭卡片。

#### (2) 全局配置持久化体系 (`AppSettings.cs` & `src/linux/lanchat.py`)
- **存储路径机制**：
  - Windows: 优先检查同级 `settings.json`（便携模式），默认漫游在 `%AppData%\LanChat\settings.json`（受 Windows 用户档案保护，MSI 安装/卸载/升级不触碰该目录）。
  - Linux: 优先检查同级 `settings.json`，默认在 `~/.config/lanchat/settings.json`（回退 `~/.local/share/lanchat/settings.json`）。
- **字段支持**：`NickName`（用户自定义昵称）、`AutoAcceptFiles`（文件自动接收偏好）。
- **触发机制**：主窗口构造函数自动加载（`AppSettings.Load()` / `load_settings()`）；设置面板、右键菜单、修改昵称弹窗与终端 `/nick` 改名时即时调用 `Save()`。

---

## 二、关键约束与设计原则

1. **C# 5.0 / .NET Framework 4.0**：系统原生 `csc.exe` 直编，零第三方依赖。属性必须在构造函数中初始化，禁用 C# 6 自动属性初始器。
2. **视觉规范**：100% 遵守 `Docs/GrokBot_UI_Design_System.md` 与 `.claude/Rules.md` 9 大硬约束。
3. **网络契约**：UDP 38888 / TCP 38889+ / LANC 二进制协议向前向后兼容。
4. **NotifyIcon 63字符限制**：WinForms 原生 `_trayIcon.Text` 赋值严格限定 `< 64` 字符，杜绝 ArgumentException。

---

## 三、增量模块索引

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `src/Services/AppSettings.cs` | 全局持久化配置服务（JSON 读写，%AppData% 漫游与便携模式） |
| `src/UI/Controls/GrokTrayPreviewPopup.cs` | GrokBot 暗黑无焦点托盘未读预览浮窗卡片控件 |
| `src/MainForm.cs` | 启动时加载 AppSettings，声明预览窗体与广播摘要缓存 |
| `src/MainForm.Tray.cs` | 托盘悬停预览触发、关闭、条目列表构建、会话跳转与安全文字刷新 |
| `src/MainForm.Chat.cs` & `src/MainForm.Files.cs` | 广播摘要记录、未读触发时托盘提示与预览同步 |
| `src/linux/lanchat.py` | Linux 端同步持久化读写配置，改名与自动接收即时落盘 |
| `CHANGELOG.md` | 根目录规范更新日志文档 |
| `packaging/publish-release.py` | 同步更新发布日志与文案 |
