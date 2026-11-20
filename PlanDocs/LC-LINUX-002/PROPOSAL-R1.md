# Linux Electron 桌面重构与 Windows 功能对齐

**LC-LINUX-002 · 第 1 轮 · 提案方 Codex · 2026-11-20**

用户指定 Linux 先改用 Electron，退出 Python/Tk 桌面路线，按现有 LanChat Windows 版重做外观与功能，Linux 验收后再考虑 Windows 迁移。本轮交 Grok 独立审核，实施顺序、技术取舍和验收边界待用户采纳。

## 要决定的问题

| 编号 | 审核议题 | 提案 |
| --- | --- | --- |
| D1 | Electron 与现有业务如何衔接 | Electron 负责 Linux 桌面。由现有 Linux 自包含 C# ELF 增加桌面服务模式，继续复用 Windows 的 `NetworkService` 和 `Models`。保留独立 ELF CLI。 |
| D2 | 如何判定外观和功能一致 | 以当前 Windows LanChat 的运行截图、真实操作和 GrokBot 规范为基线。应用内界面逐项对齐，桌面环境限制逐项实测，等价操作由用户采纳。 |
| D3 | Python 如何退出，用户如何升级 | 新版桌面和新版命令行运行时均退出 Python。沿用安装入口和用户数据，提供两架构安装包和便携包。旧产品留作迁移及互通对照，构建脚本用 Python 与否属于构建环境选择。 |

采纳时将本案与 [LC-LINUX-001](../LC-LINUX-001/STATUS.md) 衔接。旧案的 Tk 选型、Tk 双栏／设置和 `.pyz` 新版分发由本案接替，旧案已取得的网络、数据、打包证据用于回归。审核须确认新案对 [Rules.md](../../.claude/Rules.md) 第 4、9、10 条的具体替代文字。建议保留双架构自包含 ELF 要求，将桌面交付从 Tk／`.pyz` 改为 Electron，保留 GrokBot 视觉和系统文件选择器约定，将手写 JS／CSS 和业务层沿用的 C# 纳入 1000 行预警、1500 行上限。规则、采纳记录和执行计划在用户采纳后同步。

## 建议决定

### 1. 桌面、通信和数据的职责

- **界面。** Electron 加本地 HTML、CSS 和 JavaScript 模块，优先用标准 DOM、原生文本编辑和 CSS 完成组件。抽出颜色、字号、尺寸、间距、状态色和圆角令牌。复用 LanChat 自有图标和吉祥物的几何及颜色，首选现有 GDI+ 渲染器构建期导出资源，覆盖各状态和高 DPI。静态资源和逻辑在包内。UI 组件使用明确的业务边界，便于以后评估共用前端。
- **桌面集成。** Electron 主进程管理单实例、窗口、托盘、通知、文件／目录选择、系统打开和更新入口。沙盒 preload 只暴露具名操作，主进程核验发送方、参数与文件来源。拖放用 `webUtils.getPathForFile`，将有效的本地选中文件交给传输服务。对端昵称、消息、文件名和更新说明一律按数据处理。应用页面使用本地资源，启用沙盒、`contextIsolation`、CSP 和明确的外链策略。 [Electron 安全指南](https://www.electronjs.org/docs/latest/tutorial/security) · [文件拖放 API](https://www.electronjs.org/docs/latest/api/web-utils)
- **网络。** 在 `src/linux/LanChat.Linux` 的入口按模式分派，独立 CLI 与 Electron 启动的桌面服务复用同一份共享 C# 核心。桌面主进程以打包内的绝对路径启动 ELF 子进程，通过继承的 stdin/stdout 传递 UTF-8 结构化请求、响应和事件，诊断写 stderr。首段验证服务就绪、协议版本和端口，查询初始状态，之后按请求 ID 和操作 ID 关联结果。单一输出队列串行写入，进度事件合并，消息、终态和连接错误保序交付。只有 ELF 打开局域网 UDP/TCP 套接字，文件直接在 ELF 内以 64 KiB 流式收发。
- **生命周期。** GUI 与 CLI 的启动、监听和退出各自验证。同一安装／便携配置的新版入口使用共同的写入互斥，避免重复占用和历史并发写。关闭到托盘时保持网络，明确退出时收尾、发送离线公告并释放端口。界面重载从现有子进程取状态。子进程异常时显示断开，已有发送标记结果未确认，恢复后重新取节点和历史快照。父进程退出或管道关闭后，子进程退出并释放资源。重新发送由用户发起。
- **数据。** 以一个 C# 桌面服务为设置、会话和历史写入者。先审核 `SessionManager`、`ChatHistoryService` 的可复用部分和 Windows 路径依赖，按需增加 Linux 路径和持久化适配。复用现有 JSONL 文件名、字段、时间戳与 Windows 已验证的转义兼容规则。数据存放优先延续旧安装与便携选定的位置，迁移前识别真实路径与键名，保护既有数据。仅对不能兼容读取的项做可重复的转换，按需创建备份并原子提交。历史归档按确认 IPv4，当前路由继续按节点 ID 和确认的监听端口，证据不足的会话保留可查。文件卡片区分原始路径、本机可打开状态和丢失状态。

这条路线让 Linux 一次替换桌面和运行时，延用现有 LANC 互通实现。若在 Node.js 重写网络，发现、多网卡、TUN、端口学习、二进制短读和完工 ACK 都要独立回归，且会形成第二份生产协议实现。首包要计量 Electron 与 ELF 的体积和资源开销，Windows 当前的轻量交付不适合作为 Electron 的体积指标。

### 2. Windows 功能对齐清单

第 0 步把每一行展开到可操作入口和状态，记录 Windows 菜单原文、截图、源码位置、Linux 实现入口、用例和结果。清单须覆盖类目中的全部已接通入口，功能是否存在以实际动作和结果为准。字段、排序和键盘行为随基线记录。

| 范围 | 应对齐的用户行为与表现 | Windows 核对入口 |
| --- | --- | --- |
| 主窗、导航与会话 | 自绘标题栏、左会话栏和右聊天栏，首屏及窄窗布局。头像和搜索胶囊、刷新/连接、群聊置顶、私聊、会话预览、时间与未读、选中/悬停/离线态；右键菜单及清理、删除确认。 | [MainForm](../../src/MainForm.cs) · [Designer](../../src/MainForm.Designer.cs) · [会话与输入](../../src/MainForm.Chat.cs) |
| 节点和个人资料 | 自动发现、在线列表与手动 IP 直连、等待与失败提示、新建节点和修改昵称、吉祥物及强调色、设备标识查看和复制。同一 IP 换节点、离线归档与重连回到正确会话。 | [连接节点](../../src/UI/Controls/GrokConnectNodeForm.cs) · [新建节点](../../src/UI/Controls/GrokCreateNodeForm.cs) · [设置面板](../../src/UI/Controls/SettingsPanels) |
| 聊天与输入 | 群聊与私聊、系统消息、时间、气泡宽度和换行、文字选择与复制、表情面板、输入胶囊与附件菜单。核对发送/换行快捷键、草稿、离线/失败反馈和消息锚点。非当前会话的事件归到正确发起会话。 | [聊天控件](../../src/UI/Controls/WeChatChatView.cs) · [会话与输入](../../src/MainForm.Chat.cs) |
| 文件 | 文件选择、多选及拖放、群聊条件下选接收人、进度及终态、接收目录和自动接收、卡片打开文件／所在目录、同名和并发。关闭自动接收时对照 Windows 当前拒收语义。最终成功以对方落盘 ACK 为准。 | [文件界面](../../src/MainForm.Files.cs) · [共享传输](../../src/NetworkService.Transfer.cs) |
| 设置与维护 | 常规、个人资料、外观、快捷键、文件传输、聊天记录、网络设备、关于八页，核对主题／强调色／界面与正文独立字号、即时生效和重启持久化、历史目录迁移与清理、反馈、诊断复制和检查更新。当前仅改变控件外观或文案的项登记实际状态，按基线还原展示并明确其可用性。 | [设置容器](../../src/UI/Controls/GrokSettingsDialog.cs) · [设置面板](../../src/UI/Controls/SettingsPanels) · [更新服务](../../src/Services/UpdateService.cs) |
| 桌面集成 | 单实例唤醒、关闭到托盘、恢复和真正退出、未读提醒和预览、最小化/最大化/还原、系统贴靠、跨屏与任务栏区域。桌面文件及目录选择保留系统样式，应用内菜单和提示保持统一。 | [托盘与窗口](../../src/MainForm.Tray.cs) · [主窗](../../src/MainForm.cs) · [托盘预览](../../src/UI/Controls/GrokTrayPreviewPopup.cs) |

- **视觉基线。** 基准从 Windows `v2.0.16`（`a0e49c1`）开始，核对当前 WinForms 客户端实际构建与运行状态后记录准确提交、构建摘要和引用的工作树差异。固定窗口客户区、数据、字体、缩放、主题和交互状态。截图覆盖主窗、群聊/私聊、空态/未读、文件卡片、附件菜单、节点与确认弹层、八个设置页和托盘预览。Windows 运行表现与设计规范有冲突时，截图和代码一起提交裁定。
- **截图与交互。** 同尺寸并排及叠图检查布局边界、留白、色值、图标、圆角、气泡与文件卡片，尺寸以 100% 缩放下的逻辑坐标对照。检查 100%、125%、150% 和窄窗，记录字体替代与系统装饰的可见差异，应用内字体用可用且许可合规的映射。会话、聊天、设置及弹层使用规范中的浮动滑块，静止 4 px、悬停／拖拽 5 px，验证无溢出隐藏、鼠标拖动、滚轮和键盘；按实际输入法核对组合键与回车。
- **Linux 桌面等效操作。** 原生 Wayland 下窗口位置和激活由合成器控制。先测托盘能力，有托盘时提供恢复与退出。没有可用托盘时保留可从桌面恢复的窗口或最小化入口。无法定位托盘悬浮预览时，建议用系统通知和点击后进入未读会话完成同一任务。必须提交 Xorg、GNOME Wayland 的实际结果及等效操作供审核，标出哪些属于系统界面。更新流程对齐版本、变更说明和下载安装入口，按本机架构选择正确的 Linux 资产，通过系统包管理器安装。 [Electron 窗口 API](https://www.electronjs.org/docs/latest/api/browser-window) · [Electron 托盘 API](https://www.electronjs.org/docs/latest/api/tray)

### 3. 构建和换代

- Electron 锁定实施时受支持的主版本和补丁，锁定构建依赖并记录运行时摘要；ELF 首选将现有 `net9.0` 自包含发布配置升级至受支持的 .NET 10 LTS。构建产物显式区分 `amd64` 与 `arm64`，各自打包 Electron、相匹配的 ELF、图标、桌面入口、系统依赖声明。先用干净构建目录和现有打包链评估最小接入方式，验证 ELF 架构、依赖、SHA-256 和沙盒。 [Electron 版本政策](https://www.electronjs.org/docs/latest/tutorial/electron-timelines) · [.NET 支持政策](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- 安装保留 `lanchat` 命令和桌面入口，有图形会话时进入 GUI；`--cli`、`-c` 和无图形环境在启动 Electron 前分流到 ELF。核对旧 Python CLI 常用的 `/chat all`、`/send`、`/add`、`/refresh`、`/nick`、退出以及非交互 SSH 行为；现有 C# 终端功能只是起点。两个架构均交付可独立运行的 CLI 和带桌面的便携发行包，安装与便携配置归属和相对路径各自验证。
- 验证从公开版及 LC-LINUX-001 候选安装升级，特别是旧 `Architecture: all` 到 `arm64`／`amd64` 的包替换、旧桌面快捷方式、包依赖和版本排序。使用真实旧安装与便携样本演练重启、回退、卸载与重装。保留旧版包供测试／回退，按实际数据兼容范围和备份说明回退步骤。旧运行时不会参与新版启动；历史文件、聊天附件和设置随用户保留。

## 证据

| 核对项 | 本轮证据与边界 |
| --- | --- |
| 仓库基线 | `main` 的 HEAD 为 `2957dcf`，公开版为 `a0e49c1`。本地已有 LC-LINUX-001 候选的未提交修改，包括共享网络 C# 小修、拆分后的 Python 和打包脚本。WinForms 主窗口与当前 Windows 客户端实机视觉快照仍待第 0 步固定。 |
| 网络可复用 | 现有 [Linux C# 项目](../../src/linux/LanChat.Linux/LanChat.Linux.csproj) 以源码链接方式构建 `Models` 及全部 `NetworkService` 分部，已经用作自包含 ELF CLI。现有 [发现](../../src/NetworkService.Discovery.cs)、[网卡](../../src/NetworkService.Interface.cs)、[传输](../../src/NetworkService.Transfer.cs) 仍决定 Linux 的多网卡、LANC 和 ACK 行为。桌面服务 IPC 是拟新增部分。 |
| 候选轮次已有证据 | [实施验证](../LC-LINUX-001/VALIDATION.md) 已记录 Windows 宿主双向非默认端口、中文和文件 SHA-256、Windows JSONL 互读，另有 x86_64／ARM64 Ubuntu 虚拟机安装和无头检查。实机桌面与物理双机互通需另验。此处沿用既有记录，本轮未重跑。 |
| 迁移输入 | [现打包入口](../../packaging/build-deb.py) 的 `amd64` 与 `all` 安装包均优先启动 `.pyz`；后者不带 ELF。[Python 启动器](../../src/linux/lanchat_app/app.py) 处理显式 CLI 和无头。设置与路径见 [storage.py](../../src/linux/lanchat_app/storage.py)、历史见 [history.py](../../src/linux/lanchat_app/history.py) 及 Windows 的 [ChatHistoryService](../../src/Services/ChatHistoryService.cs)。 |
| 桌面规范和功能边界 | 视觉依据 [GrokBot 规范](../../Docs/GrokBot_UI_Design_System.md)、[GrokTheme](../../src/UI/Controls/GrokTheme.cs)、当前 WinForms 业务接线。源码中的 [常规页](../../src/UI/Controls/SettingsPanels/GeneralSettingsPanel.cs) 有部分空回调和仅改变按钮文案的条目，[网络设备页](../../src/UI/Controls/SettingsPanels/NetworkDeviceSettingsPanel.cs) 有展示性开关。Windows [更新服务](../../src/Services/UpdateService.cs) 选择 MSI，Linux 需按平台映射。 |
| 框架依据 | [Electron 进程模型](https://www.electronjs.org/docs/latest/tutorial/process-model) 支持本地 Web 渲染与桌面主进程分工。WorkBuddy 用于说明 Electron 跨平台的可行性。样式、资产和用例全部来自 LanChat 自身。 |

## 未核实

| 风险 | 最迟核实与通过条件 |
| --- | --- |
| Windows 当前实机的真实功能状态和视觉冲突 | 第 0 步冻结截图和操作清单。每项有菜单入口、可观察结果和可复核源码。对只展示、禁用或功能未接通的条目提供证据。 |
| 两种架构的 Electron、.NET、图形依赖、沙盒和系统托盘 | 第 1 步测 Ubuntu 22.04／24.04 的候选支持组合，记录架构、桌面环境、实际 Wayland/X11/XWayland 后端、包体和进程资源。实测进程树、权限、启动和退出，缺环境的组合留作待验。 |
| 旧数据和安装换代 | 第 2 步核对公开版、候选版、安装与便携的格式及路径优先级。覆盖旧记录、UTF-8/BOM、尾部坏行、未完成操作、同 IP 重连、目录不可写、重复迁移、回退及旧绝对路径。 |
| 中文输入与长历史 | 第 3 步在 Xorg 和 GNOME Wayland 分别用 fcitx5、ibus 测预编辑、候选确认、Enter 发送、Ctrl+Enter 换行和焦点恢复。用同一份至少 2000 条历史测切换、复制、追加、滚动锚点和重排。根据第 0/1 步测量登记包大小、冷启动、空闲内存、历史切换及滚动指标和验收阈值，阈值在正式实现前由审核确认。 |
| 物理网络与平台等效行为 | 第 4 步实测真实 Windows／Linux 双机、两架构、旧 Windows `v2.0.15`／`v2.0.16` 及旧 ELF。记录防火墙、物理网卡、子网、TUN、默认路由和发现耗时。窗口、托盘、通知、更新和字体差异逐项确认。 |

## 建议顺序

| 步骤 | 实施内容 | 放行条件 |
| --- | --- | --- |
| **0 · 固定基线和验收清单** | 冻结 Windows 实际构建、截图、功能与设置状态，保全 LC-LINUX-001 的候选源码差异、安装样本和数据。确认 D1–D3、首发系统组合、系统差异处理和测量口径。 | Windows 界面与操作都有对照来源，保全旧候选和回滚样本，规则修订进入采纳后的执行计划。 |
| **1 · 打通 Electron 首包** | 从安装后的桌面入口显示实际窗口。Electron 与真实 ELF 打通启动、重启、退出、节点事件、刷新、私聊和群聊，拿到发送终态；传一个真实文件并验证落盘哈希。生成两架构试验包，验证无头命令直接进入 ELF。 | 两架构安装实测，GUI ↔ ELF ↔ Windows 走通至少一条真实链路；其余架构与桌面实机结果分别登记。证明无 Python 运行时依赖、正常沙盒、可诊断的启动失败及无遗留进程。 |
| **2 · 接通会话、文件和升级** | 实现数据读取、离线归档和重连归并，接通会话草稿、并发文件进度、文件终态和应用设置。补齐 ELF CLI 与旧调用习惯。验证交叉读写和真正的旧包升级、便携、回退。 | 同名不同 IP、同 IP 换节点、非默认端口双向、短读、拒收、中断、最终 ACK 缺失、并发同名文件和重启找回历史都有证据。大文件按流处理，进度事件不阻塞 UI，文件完整性用 SHA-256 验证。 |
| **3 · 对齐全部界面与桌面操作** | 按主窗聊天、节点与文件、八个设置页、托盘窗口与维护入口依次交付。逐一核对真实行为、应用内视觉、即时设置、桌面等效操作。 | 第 0 步清单每项标明实测通过或经用户采纳的等效操作，附截图／录屏或可复跑用例。覆盖输入法、浮动滚动条、焦点、多屏／分数缩放、长历史及性能。 |
| **4 · 双机和正式候选验收** | 在真实桌面和物理局域网走完整安装、安装升级、便携、CLI、Windows 与 Linux 双向功能。新旧两架构按支持矩阵取证。复跑 Windows 原生构建与共享网络行为，核对候选包内 ELF 和 Electron 的构建版本、架构与 SHA-256。 | 关键组合通过，系统差异已裁定，构建失败可见，真实升级与回退可复跑。业务代码进入 [CRDocs](../../CRDocs/STATUS.md) 独立审核，公开发布等用户指令。 |

Grok 本轮优先复核四件事：共享 C# 的真实复用边界和进程生命周期，Windows 入口与设置清单是否漏项，旧 `all` 包与便携数据的换代可行性，以及 Wayland 和 x64／ARM64 的放行口径。若需调整，给出影响步骤、最小修订和完整顺序。

## 明确不做

- Windows Electron 迁移作为后续独立议题，以 Linux 候选验收结果作为输入。
- 互联网会话、新业务功能、协议换代和额外发行版支持沿用各自的立项流程。本案的通信兼容目标是现有 LAN 协议与公开版本。
- 正式发布属于用户另行授权的交付步骤。本轮交付 PlanDocs 提案与审核交接。

## 对上一轮审核的处理

无。
