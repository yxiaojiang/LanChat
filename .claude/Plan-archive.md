# Plan-archive.md — 历史归档计划

> 归档时间：2026-10-20。

---

## 归档事项 1：LanChat v1.0 架构升级与全平台安装包（已完成）

- **完成日期**：2026-10-20
- **成果**：
  1. 打上 Git Tag：`LanChat-v1.0` 与 `v1.0`。
  2. Windows 端完成 WiX Toolset v5 打包，产出 `LanChat.msi`。
  3. Linux 端完成自包含二进制打包与 `build-deb.py`，产出 `LanChat.deb` 与 `lanchat_1.0.0_amd64.deb`、`lanchat_1.0.0_all.deb`。
  4. 完成初版 WeChat 桌面端深色主题三栏布局。

---

## 归档事项 2：MSI 安装向导交互增强与卸载器（已完成）

- **完成日期**：2026-10-20
- **成果**：
  1. 增加标准安装向导界面（`WixUI_InstallDir`）：支持用户自定义安装路径，点击【浏览】任意更换盘符目录。
  2. 安装完成界面增加【立即运行 LanChat】复选框，点击【完成】直接拉起程序。
  3. 编写并编译 `uninstall.exe`，支持脱钩复制至临时目录静默调用 MSI 卸载并安全清理安装目录与快捷方式。
  4. 解决安装至 `Program Files` 时的文件接收权限问题，智能切回用户 `Downloads` 目录。

---

## 归档事项 3：界面细节微距重构达到 95%+ WeChat 桌面端相似度（已完成）

- **完成日期**：2026-10-20
- **成果**：
  1. 重写 12 类矢量纯代码自绘制图标引擎（`WeChatIcon`），彻底替换粗糙字符与原生 Emoji 图标。
  2. 严格对标微信原生：实心微信绿聊天气泡、带 3 条卡片线的专属通讯录人像、3D 文件盒子、平滑汉堡菜单等。
  3. 实现 `WeChatDarkTooltip`，精准复刻微信桌面端暗黑微距悬浮提示框。
  4. 导航按钮增加平滑圆角矩形背景悬停与按压反馈。

---

## 归档事项 4：消除白色残留元素与统一应用图标发布 v2.0（已完成）

- **完成日期**：2026-10-20
- **成果**：
  1. 彻底清除界面中残留的白色元素：
     - `WeChatIconButton` 开启透明背景支持，消除搜索加号、标头与工具栏按钮的白底。
     - 输入框移除原生 Win32 垂直白色滚动条。
     - 自定义 `WeChatDarkMenuRenderer` 与 `WeChatDarkMenuColorTable`，消除菜单左侧白边与边距。
  2. 设计现代化双气泡聊天应用图标，生成 `app.ico`、`app.png`、`lanchat.svg`。
  3. 窗体标题栏、Windows EXE、MSI 快捷方式、卸载快捷方式、Linux 图标全链路统一绑定该图标。
  4. 打包发布 Windows `LanChat.msi` (2.0.0)、Linux `lanchat_2.0.0_amd64.deb` 与 `lanchat_2.0.0_all.deb`。
  5. 打上 Git Tag：`LanChat-v2.0` 与 `v2.0`。


---

## 归档事项 5：Windows 最小化异常修复、单实例互斥唤醒与 GitHub 在线更新（已完成）

- **完成日期**：2026-10-20
- **成果**：
  1. 修复窗体最小化时 `WeChatChatView` 滚动条 Value 越界抛出 `ArgumentOutOfRangeException` 异常，加入边界夹紧与尺寸守卫。
  2. 实现基于 `Mutex` 与 Windows 广播消息 `RegisterWindowMessage` 的单实例互斥机制，重复启动时自动唤醒已运行实例并前置展示。
  3. 实现基于 GitHub Releases API 的在线新版本检测与微信暗黑更新向导，支持一键拉取最新 Release 并下载 MSI 升级。

---

## 归档事项 6：Linux deb 微信暗黑原生 GUI 与双端界面完全统一（已完成）

- **完成日期**：2026-10-20
- **成果**：
  1. 在 `lanchat.py` 中采用 Python 标准库 `tkinter` 纯自绘复刻与 Windows 完全一致的微信暗黑 3 栏界面（导航栏、会话列表、聊天区域、暗黑消息气泡、发送文件、流式进度等），0 额外依赖。
  2. 将 `lanchat.desktop` 修改为 `Terminal=false`，消除桌面点击图标时的黑框终端闪现。
  3. 优化启动器 `/usr/bin/lanchat`：图形环境优先启动 GUI，无头/SSH 环境平滑回退至 CLI 控制台；桌面缺少 `python3-tk` 时自动弹出系统提示窗。
  4. 重新编译生成 Windows `LanChat.msi`、`LanChat.exe` 与 Linux `release/lanchat_2.0.0_amd64.deb`、`release/LanChat.deb`、`release/lanchat_2.0.0_all.deb`。
