# Plan-archive.md — 历史归档计划

> 最近更新：2026-11-23。

## v2.0.18 文件传输体验与跨平台包准备（2026-11-23）

- Windows 与 Linux Electron 的发送/接收进度均显示在聊天气泡中，多文件逐文件更新百分比；粘贴系统剪贴板文件可暂存为附件，用户点击发送或按 Enter 后才开始传输。普通文本粘贴保留。
- 修复 ARM64 Electron deb 内文件与目录同路径冲突，打包器在归档阶段拒绝文件/目录冲突。Linux Electron 设置拆为独立窗口及八个页面。
- Windows 原生构建、同名文件并发回环、UI 草稿/粘贴格式回归和 MSI 内容校验通过；Linux UI 回归、双架构 ELF 构建和 Electron 包结构核验通过。未运行虚拟机；ARM64 Linux 与 Windows 安装/真实剪贴板仍由用户验收。系统图形后端、输入法、托盘及长历史测试仍在 LC-LINUX-002 计划中。
- v2.0.18 正式 Release 已发布：https://github.com/yxiaojiang/LanChat/releases/tag/v2.0.18；发布 commit `6a11ea04b2c71ba89e9042e25b80955a1741506e`，分支 `main` 已 push。Release 的 9 个附件均核对远端上传状态、大小与 SHA-256。Windows MSI SHA-256：`12472e5050bd4ef05fda47711fe986942efa9733613b0cfbb22d4f83d6f07e43`；ARM64 deb：`92874c84da1032f78160f87015c312c65aedb21faefb8df736ac71fecaaba8cd`。Linux 双架构其余摘要见 Release 附件 `SHA256SUMS.json`。
- 发布期间发现 Python urllib 对大包上传等待过久；完整附件最终以 curl 流式方式补齐。发布器已更新为流式传输并在超时后核验远端资产，修复将在后续 commit push；该脚本修复不改变本次已发布产物。

## LC-LINUX-002 第 2 步（已完成）

- **完成日期**：2026-11-23。用户确认银河麒麟 V10 国防版（020）ARM64 上的 `2.0.17~experiment.5` 安装顺利，桌面运行可刷新并收发文字，据此验收通过第 2 步。
- **开发与隔离回归**：共享会话、离线历史、草稿、文件进度与终态、设置及 CLI 已接通。双架构 ELF 回归通过只读设置备份／确认、进程互斥、历史主动迁移、非默认端口、短读、中断、拒收、最终 ACK 异常、64 MiB 流传输、并发同名双哈希、重连及崩溃恢复。Windows 原生共享接收核心并发回归也通过。
- **升级和回退**：Ubuntu 24.04.5 两架构 QEMU 及 Ubuntu 22.04.5 两架构 Ubuntu Base 完成旧公开包、候选包和 `Architecture: all` 样本的升级、回退、卸载重装，观察旧 GUI、CLI、历史及原接收文件留存。Ubuntu 20.04.5 ARM64 Ubuntu Base 确认 glibc 2.31／OpenSSL 1.1 分支可安装、CLI 可退出。
- **试验交接**：第五轮 `2.0.17~experiment.5` amd64、arm64 deb、便携包、独立 CLI 和回退样本在 `bin/lc-linux-002/handoff-experiment.5/`；第 2、3、4 轮交接原样保留。实施、命令、日志路径及限制见[集成记录](../PlanDocs/LC-LINUX-002/INTEGRATION.md)。
- **证据边界**：麒麟本次回报未包含 `dpkg -l`、独立 CLI、回退和库映射输出，不声称这些项目已由我观察到。此前旧包的 `iU` 和 OpenSSL 1.1 映射有实机日志；新包的安装与 GUI 基本功能由用户明确确认。物理双架构升级／回退、无头 CLI 和 Windows 局域网互通仍属第 4 步未验项目，必须逐项取得证据。

## LC-LINUX-002 第 0、1 步

- 第 0 步完成。固定当前 WinForms 原生构建、提交和工作树差异，以及 125% 下的 18 张运行截图和操作清单。按 SHA-256 保全旧公开包、LC-LINUX-001 候选、交接 ZIP 和便携回退样本。冻结 Ubuntu 22.04／24.04 的架构及桌面后端矩阵、桌面等效操作和资源测量口径。 [Windows 基线](../PlanDocs/LC-LINUX-002/BASELINE.md)
- 第 1 步完成首包集成。Electron 通过继承管道连接共享 LANC 核心的 .NET 10 ELF，启动器支持显式和自动无头分流。Ubuntu 24.04.5 amd64 QEMU 的 Xorg／Openbox 从安装入口通过 ELF 就绪、刷新、双向私聊与群聊、双向文件 SHA-256、渲染沙盒、CLI、退出及端口释放。两架构 `2.0.17~experiment.1` 试验 deb、便携包与独立 ELF 已生成，包内机器码分别为 62 和 183，运行无需 Python。ARM64 在本阶段仅有静态核验证据。 [集成记录](../PlanDocs/LC-LINUX-002/INTEGRATION.md)
- LC-LINUX-002 仍在第 2 步。物理双架构、升级回退与公开 Windows 的联测状态按 [当前计划](Plan.md) 更新。

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

---

## 归档事项 7：架构解耦重构阶段一 —— UI 控件、绘图引擎与更新服务物理分离（已完成）

- **完成日期**：2026-10-24
- **成果**：
  1. 将 3206 行的巨型 `MainForm.cs` 进行物理模块化剥离，瘦身至 1838 行（净减 1368 行代码）：
     - 提取 `src/UI/Renderers/WeChatIcon.cs`：13 类纯 GDI+ 矢量图标绘制引擎；
     - 提取 `src/UI/Renderers/WeChatDarkMenuRenderer.cs`：微信暗黑菜单调色板与全局暗黑 Tooltip；
     - 提取 `src/UI/Controls/WeChatButtons.cs`：微信侧边栏导航按钮与暗黑工具栏图标按钮；
     - 提取 `src/UI/Controls/WeChatChatView.cs`：流式自绘聊天视图与文件卡片交互；
     - 提取 `src/Services/UpdateService.cs`：GitHub 双轨在线更新检测服务与更新向导弹窗。
  2. 更新并验证 `Tools/Compile.bat`、`Tools/CompileAndLink.bat`、`Tools/Publish.bat`，原生系统自带 `csc.exe` 耗时 < 1 秒，产物体积保持在 139 KB（~142,336 字节）。
  3. 修复 `LanChat.Linux.csproj` 源码软链接路径并完成 .NET 9 与 Framework 4.0 双轨编译验证，达成 0 警告、0 错误。

---

## 归档事项 8：架构解耦重构阶段二 —— 主窗体业务逻辑 Partial 模块化（已完成）

- **完成日期**：2026-10-24
- **成果**：
  1. 将 1838 行的 `MainForm.cs` 按业务领域彻底解耦为 4 个职责分明的 partial 类文件，所有单一文件规模均控制在 650 行以内：
     - `src/MainForm.cs`（645 行）：主窗体核心构造、控件树构建与布局计算、DWM 暗黑标题栏、Win32 窗口消息拦截（单实例广播唤醒）与 GDI+ 圆角矩形自绘辅助函数；
     - `src/MainForm.Chat.cs`（468 行）：好友上线/离线/信息更新、会话列表绘制与关键字筛选、文本消息收发流转、历史记录维护、Emoji 表情弹窗与手动添加 IP 直连握手；
     - `src/MainForm.Files.cs`（268 行）：文件选择与拖拽监听、分块传输进度条更新计算、接收文件保存分发、双击打开文件及定位系统资源管理器；
     - `src/MainForm.Tray.cs`（272 行）：网络底层初始化与网卡探测、个人昵称修改弹窗、系统托盘图标常驻、拦截关闭按钮后台静默运行、托盘微信暗黑右键菜单与在线更新委托。
  2. 更新工具链 `Tools/Compile.bat`、`CompileAndLink.bat`、`Publish.bat`，适配 `src/MainForm*.cs` 批编译。
  3. 原生系统 `csc.exe` 纯编译与 .NET 9 双轨验证耗时 < 1 秒，产物体积恒定在 139 KB，逻辑与界面零退化。


---

## 归档事项 9：架构解耦重构阶段三 —— 网络通信层内部模块化与 Partial 解耦（已完成）

- **完成日期**：2026-10-24
- **成果**：
  1. 将 1152 行的 `NetworkService.cs` 内部复杂的多职责解耦拆分为 4 个职责单一、高内聚的 partial 类文件（单文件均控制在 400 行以内）：
     - `src/NetworkService.cs`（154 行）：网络门面（Facade），公开属性、事件代理、构造函数与生命周期（`Start` / `Dispose`）；
     - `src/NetworkService.Interface.cs`（153 行）：网卡硬件探测与过滤、TUN/VPN/WSL 虚拟网卡隔离、子网掩码计算与本地终结点匹配；
     - `src/NetworkService.Discovery.cs`（396 行）：多物理网卡绑定 UDP 广播监听、254 子网高并发单播探测扫描、心跳保活、离线定时清理与节点生命周期管理；
     - `src/NetworkService.Transfer.cs`（341 行）：TCP 监听器初始化、`LANC` 二进制协议拆包封包、文本消息双向分发与 64KB 流式分块传输引擎。
  2. 更新构建工具链 `Tools/Compile.bat`、`CompileAndLink.bat`、`Publish.bat`，引入 `src\NetworkService*.cs` 模式匹配，杜绝单文件硬编码遗漏。
  3. 更新 `src/linux/LanChat.Linux/LanChat.Linux.csproj`，同步添加所有 partial 类文件软链接映射，保持 Windows 与 Linux 跨平台源码级共享。
  4. 原生系统 `csc.exe`（.NET Framework 4.0 / C# 5.0）纯编译与现代 .NET 9 SDK（Windows + Linux）双轨构建全面通过，达成 **0 警告、0 错误**，体积稳定在 142,336 字节（~139 KB）。


---

## 归档事项 10：架构解耦重构阶段四 —— 应用服务层提炼与 SessionManager 消息解耦（已完成）

- **完成日期**：2026-10-24
- **成果**：
  1. 新增应用服务模块 `src/Services/SessionManager.cs`（142 行）：
     - 纯领域服务，解耦 UI 与会话数据，统一封装广播与私聊历史消息字典 `_histories` 并实现线程安全访问控制；
     - 提供 `AddMessage`、`GetMessages`、`GetLastMessage`、`GetLastMessageSnippet`、`ClearHistory` 等高内聚操作接口；
     - 抽象活跃会话标识 `ActiveSessionKey` 与 `IsBroadcastActive` 判断，预留事件驱动模型。
  2. 精简主窗体 `src/MainForm.cs` 与 `src/MainForm.Chat.cs`：
     - 从 `MainForm.cs` 彻底剔除原生 `Dictionary<string, List<ChatMessage>> _chatHistories` 字段；
     - 重构 `SwitchActiveChatView`、`ListSessions_DrawItem`、`AddMessageToHistory` 与 `ClearCurrentChat`，直接委托 `_sessionManager` 处理。
  3. 双轨编译验证：
     - 原生系统 `csc.exe`（.NET Framework 4.0 / C# 5.0）秒级编译，产物体积仅为 143,872 字节（~140 KB）；
     - 现代 .NET 9 SDK（Windows + Linux）构建达成 **0 警告、0 错误**。

---

## 归档事项 11：GrokBot UI 全面设计规范与视觉对齐交付 (v2.1.0)

- **完成日期**：2026-10-25
- **成果**：
  1. **编写设计系统规范**：建立 `Docs/GrokBot_UI_Design_System.md` 与 `.claude/contexts/GrokUI.md`，制定 5 级暗黑纵深、排版矩阵、边框法则与圆角比例体系。
  2. **废除系统原生直角菜单**：通用圆角浮动菜单 `GrokPopupMenu`（16px圆角、44px舒适行高、8px悬停圆角卡片、纯 GDI+ 矢量图标）全盘替换会话加号、附件、右键与托盘菜单。
  3. **主输入胶囊重塑**：64px 高、32px 满圆角 `#202024` 胶囊（1px 微光边框 `#2C2C32`），11.0pt `#F2F2F6` 文本垂直居中，36x36 附件圆钮，44x44 魅紫向上箭头 `↑` 发送圆钮。
  4. **设置窗口（GrokSettingsDialog）重塑**：920x640 宽屏比例，移除侧边栏分割线，依靠 `#0E0E0E` 与 `#121212` 纯底色阶差自然分离，18.0pt 加粗明亮大标题，10.5pt 加粗中性灰分组标签，54px 卡片行，22px 内边距，32px 药丸下拉胶囊。
  5. **举一反三全窗体统一**：`GrokConnectNodeForm`、`GrokCreateNodeForm`、`GrokFeedbackForm`、`GrokChangeNickForm`、`GrokUpdateForm` 窗口大标题统一升级为 18.0pt 加粗明亮纯白，外边框微光与阴影统一；称谓统一为“连接节点”；刷新任务栏紫吉祥物图标。
  6. **稳定基线打 Tag**：在 `main` 分支提交 `3bf788d` 打上稳定发布 Tag `v2.1.0` 与 `LanChat-v2.1.0` 并推送至 GitHub 远端。

---

## 归档事项 12：主界面布局精简化与全链路即时通讯消息提醒体系 (v2.0.7)

- **完成日期**：2026-11-03
- **成果**：
  1. **移除更新安装横幅**：精简 `GrokSettingsMenuForm`，彻底去掉深蓝「发现新版本」与「安装」横幅行，菜单高度自适应优化为 332px，在底部 mi 图标上方紧凑弹出。
  2. **个人头像不可点击**：左侧 Hero 头像区域光标设为 `Cursors.Default`，解除点击弹窗事件，防止操作误触。
  3. **聊天顶栏对端信息放大 2 倍**：`_chatHeader` 高度提升至 96px，居中浮动胶囊放大为 76px 高度、38px 圆角、20.0pt Bold 粗体标题、48×48 吉祥物头像，右上角窗口控制按钮同步平滑适配。
  4. **全链路现代即时通讯提醒**：
     - 左侧会话卡片自绘 `DrawUnreadBadge` 微信红徽标（支持未读数量与 99+）；
     - Win32 `FlashWindowEx`（`FLASHW_ALL | FLASHW_TIMERNOFG`）驱动 Windows 任务栏黄色高亮闪烁；
     - 托盘图标 450ms 定时交替翻转实现经典闪烁提醒；
     - 用户点击激活窗口、点击托盘还原、点击会话列表项或聊天区域时，提示即刻自动消除。



---

## 归档事项：工作目录迁移与气泡四方向内边距（DIR-008）

- 工作目录迁至 `D:\TEmp\LanChat`；本人及对方气泡支持上下左右独立内边距，绘制起点与设计令牌一致，定制手册同步更新。
- Windows 端原生 C# 编译通过；该事项的未提交代码与本轮多屏修复一起进入发布流程。

---

## 归档事项：多屏窗口修复与 v2.0.15 发布（2026-11-16）

- 主窗体支持系统拖动贴靠，修复跨屏最大化、标题栏叠加和窗口遮挡任务栏；隔离实机已核对两屏客户区、窗口区域与还原。
- 补齐气泡四方向内边距改动和发布说明；代码与记忆提交 `64729f6`，版本和构建提交 `ad1ebe1`，均已推送 `feature/wechat-ui`。
- GitHub Release `v2.0.15` 已发布，Windows EXE/MSI 及 Linux 两个 deb 资产已上传并核对摘要；latest 指向该版本。自动隐藏任务栏和真实鼠标拖动仍列为待回归事项。

---

## 归档事项：局域网历史归并与外观设置（2026-11-18）

- 在 `main` 实现 IPv4 私聊历史归并、旧记录读取兼容、离线昵称恢复与实际发送者文件归档。上线通知随本地群聊保存。
- 外观页提供三种暗色主题、五种强调色和独立的主界面与聊天字体配置。原生 Windows 与 Linux 源码构建通过，旧历史、LANC 回环收发和模态设置交互已在隔离环境验证。
- 相关历史与外观改动进入 [v2.0.16 公开发布](https://github.com/yxiaojiang/LanChat/releases/tag/v2.0.16)，发布提交为 `a0e49c1`。Windows 独立实机回归见下方移出活跃计划的事项。

---

## 归档事项：Linux 桌面审计与重构方案评审（2026-11-18）

- 完成 24 项 Windows／Linux 桌面功能对照及同机回环、Tk 逻辑验证。Claude 第 2 轮审核收敛，用户确认采纳 Linux 绘制、文件选择器及弹层、Python 模块与单文件分发三项决定。
- 两轮提案和审核已归档到 [LC-LINUX-001](../PlanDocs/LC-LINUX-001/STATUS.md)。

## 移出活跃计划的事项（2026-11-20）

- **LC-LINUX-001**。步骤 1、2 完成候选构建及 Windows／Ubuntu 虚拟机验证，步骤 3a 完成 Tk 原型与 Windows 宿主基准。Tk 实机选型与 3b 界面改造由 LC-LINUX-002 接替。原步骤 1.6 和 2.5 的网络、数据和升级实机用例转为新版回归输入。旧版结论、源码差异及样本依据 [实施证据](../PlanDocs/LC-LINUX-001/VALIDATION.md) 保留。
- **Windows 独立实机回归**。旧历史和双机换实例的归并、离线续聊、文件归档、窄窗与分数缩放、拖顶贴靠、跨屏最大化、任务栏自动隐藏。待 Linux 收口后再排期。
- **互联网方向**。历史方案和工作树按 `codex/non-lan-network` 保留。

唯一活跃事项见 [LC-LINUX-002 执行计划](Plan.md)。
# v2.0.18 文件传输体验与跨平台包准备（2026-11-23）

- Windows 与 Linux Electron 的发送/接收进度均显示在聊天气泡中，多文件逐文件更新百分比；粘贴系统剪贴板文件可暂存为附件，用户点击发送或按 Enter 后才开始传输。普通文本粘贴保留。
- 修复 ARM64 Electron deb 内文件与目录同路径冲突，打包器在归档阶段拒绝文件/目录冲突。Linux Electron 设置拆为独立窗口及八个页面。
- Windows 原生构建、同名文件并发回环、UI 草稿/粘贴格式回归和 MSI 内容校验通过；Linux UI 回归、双架构 ELF 构建和 Electron 包结构核验通过。未运行虚拟机；ARM64 Linux 与 Windows 安装/真实剪贴板仍由用户验收。系统图形后端、输入法、托盘及长历史测试仍在 LC-LINUX-002 计划中。
- 本节记录实现与验证边界；版本、发布资产、commit/tag URL 与摘要在发布成功后补录。

