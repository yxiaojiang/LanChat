# Linux Electron 桌面重构与 Windows 功能对齐

**LC-LINUX-002 · 第 2 轮 · 提案方 Codex · 2026-11-20**

用户指定 Linux 先改用 Electron，退出 Python/Tk 桌面路线，按现有 LanChat Windows 版重做外观与功能，Linux 验收后再考虑 Windows 迁移。本轮交 Grok 独立审核，实施顺序、技术取舍和验收边界待用户采纳。

## 要决定的问题

| 编号 | 审核议题 | 提案 |
| --- | --- | --- |
| D1 | Electron 与现有业务如何衔接 | Electron 负责 Linux 桌面。由现有 Linux 自包含 C# ELF 增加桌面服务模式，继续复用 Windows 的 `NetworkService` 和 `Models`。保留独立 ELF CLI。 |
| D2 | 如何判定外观和功能一致 | 以当前 Windows LanChat 的运行截图、真实操作和 GrokBot 规范为基线。应用内界面逐项对齐，桌面环境限制逐项实测，等价操作由用户采纳。 |
| D3 | Python 如何退出，用户如何升级 | 新版桌面和新版命令行运行时均退出 Python。沿用安装入口和用户数据，提供两架构安装包和便携包。旧产品留作迁移及互通对照，构建脚本用 Python 与否属于构建环境选择。 |

采纳时将本案与 [LC-LINUX-001](../LC-LINUX-001/STATUS.md) 衔接。旧案的 Tk 选型、Tk 双栏／设置和 `.pyz` 新版分发由本案接替，既有网络、数据和打包证据继续用于回归。第 2 轮请重点审核下文的桌面恢复路径、升级与回退、架构放行矩阵，以及第 4、9、10 条的替换文字。规则、采纳记录和执行计划在用户采纳后同步。

## 建议决定

### 1. 桌面、通信和数据的职责

- **界面。** Electron 加本地 HTML、CSS 和 JavaScript 模块，优先用标准 DOM、原生文本编辑和 CSS 完成组件。抽出颜色、字号、尺寸、间距、状态色和圆角令牌。复用 LanChat 自有图标和吉祥物的几何及颜色，首选现有 GDI+ 渲染器构建期导出资源，覆盖各状态和高 DPI。静态资源和逻辑在包内。UI 组件使用明确的业务边界，便于以后评估共用前端。
- **桌面集成。** Electron 主进程管理单实例、窗口、托盘、通知、文件／目录选择、系统打开和更新入口。沙盒 preload 只暴露具名操作，主进程核验发送方、参数与文件来源。拖放用 `webUtils.getPathForFile`，将有效的本地选中文件交给传输服务。对端昵称、消息、文件名和更新说明一律按数据处理。应用页面使用本地资源，启用沙盒、`contextIsolation`、CSP 和明确的外链策略。 [Electron 安全指南](https://www.electronjs.org/docs/latest/tutorial/security) · [文件拖放 API](https://www.electronjs.org/docs/latest/api/web-utils)
- **网络。** 在 `src/linux/LanChat.Linux` 的入口按模式分派，独立 CLI 与 Electron 启动的桌面服务复用同一份共享 C# 核心。桌面主进程以打包内的绝对路径启动 ELF 子进程，通过继承的 stdin/stdout 传递 UTF-8 结构化请求、响应和事件，诊断写 stderr。首段验证服务就绪、协议版本和端口，查询初始状态，之后按请求 ID 和操作 ID 关联结果。单一输出队列串行写入，进度事件合并，消息、终态和连接错误保序交付。只有 ELF 打开局域网 UDP/TCP 套接字，文件直接在 ELF 内以 64 KiB 流式收发。
- **生命周期。** Electron 单实例负责恢复窗口。新版 GUI 服务与独立 CLI 在取得实际数据路径的共同锁后才监听和写入，同一配置已有持有者时给出明确提示。旧 Python 入口需要在升级、回退或打开同一便携配置前退出。界面重载从现有子进程取状态。子进程异常时显示断开，已有发送标记结果未确认，恢复后重新取节点和历史快照。桌面服务在父进程管道关闭时退出，正常退出发送离线公告并释放端口。重发由用户发起。
- **数据。** C# 服务负责设置、会话和历史的写入。审核 `SessionManager`、`ChatHistoryService` 可复用部分，Linux 路径和持久化适配按第 3 节接入。保留已有 JSONL 文件名、字段、时间戳与已验证的 Windows 转义兼容规则。历史归档按确认 IPv4，当前路由按节点 ID 和确认的监听端口，证据不足的会话保留可查。文件卡片区分原始路径、本机可打开状态和丢失状态。

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
- **更新。** 对齐版本、变更说明和下载安装入口，按本机架构选择对应 Linux 资产，通过系统包管理器安装。

#### Linux 桌面等效操作

记录桌面环境、会话类型及 Electron 实际 Ozone 后端。原生 Wayland 用方式可复核的运行证据确认，单看 `XDG_SESSION_TYPE` 只能确认登录会话。分别记录 Xorg、原生 Wayland 与 XWayland 的结果。窗口焦点和全局坐标由合成器控制，托盘是否可见及通知是否支持点击分别实测。 [Electron 38 发布说明](https://www.electronjs.org/blog/electron-38-0) · [窗口 API](https://www.electronjs.org/docs/latest/api/browser-window) · [屏幕 API](https://www.electronjs.org/docs/latest/api/screen) · [托盘 API](https://www.electronjs.org/docs/latest/api/tray)

| 用户动作 | 建议实现与验收 |
| --- | --- |
| 关闭窗口 | 当前会话明确验证可通过托盘恢复时，可以隐藏窗口，ELF 保持监听。托盘不可用或无法确认时，保留可见窗口或最小化到可恢复的桌面入口。测试无托盘、通知禁用及实际可验证的托盘能力变化，确认窗口始终有恢复途径。 |
| 恢复窗口 | 桌面启动器再次启动应请求现有实例恢复，同一个 ELF 进程继续服务。逐项测试可用的托盘、Dock／任务切换器和再次启动路径。若合成器仅提示而未给焦点，验证用户从桌面入口可回到已恢复的窗口。 |
| 真正退出 | 主窗口提供明确的退出入口，有托盘时也提供系统托盘菜单退出。正常退出发送离线公告，服务子进程和监听端口均释放。 |
| 未读与预览 | 应用内未读和打开对应会话为基本行为；系统通知可用时显示通知，支持点击时进入对应会话。原生 Wayland 的等效行为采用应用内未读与可用通知。Xorg 的光标旁预览以实测的指针定位和桌面行为为准。 |
| 最小化、最大化、还原、贴靠与跨屏 | 由合成器提供窗口操作。验证自绘标题栏的拖动、缩放、最小化、最大化、还原和关闭。自绘窗框无法达到可操作条件时，提请用户批准使用系统边框，客户区继续与 Windows 对齐。 |

通知是附加提醒，应用内入口和恢复路径必须独立可用。第 0 步确认每个支持环境采用的等效操作，第 3 步以实机截图和操作记录验收。 [桌面通知规范](https://specifications.freedesktop.org/notification/latest/protocol.html)

### 3. 构建、安装和数据换代

- Electron 锁定实施时受支持的主版本和补丁，锁定构建依赖并记录运行时摘要；ELF 首选将现有 `net9.0` 自包含发布配置升级至受支持的 .NET 10 LTS。构建产物显式区分 `amd64` 与 `arm64`，各自打包 Electron、相匹配的 ELF、图标、桌面入口、系统依赖声明。先用干净构建目录和现有打包链评估最小接入方式，验证 ELF 架构、依赖、SHA-256 和沙盒。 [Electron 版本政策](https://www.electronjs.org/docs/latest/tutorial/electron-timelines) · [.NET 支持政策](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- 安装保留 `lanchat` 命令和桌面入口，有图形会话时进入 GUI；`--cli`、`-c` 和无图形环境在启动 Electron 前分流到 ELF。核对旧 Python CLI 常用的 `/chat all`、`/send`、`/add`、`/refresh`、`/nick`、退出以及非交互 SSH 行为；现有 C# 终端功能只是起点。两个架构均交付可独立运行的 CLI 和带桌面的便携发行包，安装与便携配置归属和相对路径各自验证。
- 保留包名 `lanchat` 和 `Exec=lanchat`，候选版本按 Debian 规则高于 `2.0.16`。新版桌面安装包按架构命名，各带本架构 Electron 和 ELF，新版依赖按实际系统库声明。用两个架构明确标识的文件交付，更新入口按仓库资产名和架构选择。现有 `LanChat.deb` 是 amd64 副本，须核对其兼容入口的处理。
- **系统包演练。** 用公开版和已找到的 LC-LINUX-001 候选 deb 分别验证旧 `amd64` 到新 `amd64`、旧 `all` 到新 `arm64`，以及旧 `all` 到新 `amd64`。先试 `apt install ./新包.deb`，再试 `apt install --allow-downgrades ./原包.deb`；记录 apt 的实际处理和恢复旧依赖的结果。若同名包的架构切换无法直接完成，隔离环境中验证普通卸载后安装原目标包的替代路径，要求用户数据完整且旧入口仍可用，之后才定为交付步骤。核对 `dpkg -l` 只剩预期架构的一个 `lanchat`、旧启动载荷与包依赖的清理、桌面项与命令仍工作、卸载重装留存用户资料。旧 `python3` 是依赖，`python3-tk` 是推荐项，回退验收须显式确认 Tk 可用并启动旧 GUI。 [Debian 依赖规范](https://www.debian.org/doc/debian-policy/ch-relationships.html#binary-dependencies-depends-recommends-suggests-enhances-pre-depends)

#### 首次启动的数据选择与后续回退

| 形态 | 识别及沿用位置 |
| --- | --- |
| 已安装 | 先识别旧入口实际使用的 settings 文件。旧脚本同级若已有 `settings.json`，它优先于用户目录，升级替换载荷前须保全该文件。普通安装沿用 `~/.config/lanchat/settings.json`，旧候选使用的 `~/.local/share/lanchat/settings.json` 也纳入识别。两处均有数据而无法确认原入口时，明确提示选择。默认历史位于 `~/.local/share/lanchat/ChatHistory`。接收目录以已有配置和旧路径证据为准，普通默认是 `~/Downloads/LanChat_Received`。 |
| 便携 | 数据根由便携启动入口或用户选择的旧便携根标识，与 Electron 或 ELF 所在子目录独立。沿用根目录的 `settings.json`、`ChatHistory` 和 `ReceivedFiles`，按实际记录识别已用过的用户目录回退。公开 Python 版当时无历史文件属于正常状态，新历史使用选定数据根。 |
| 路径冲突或不可写 | 小写 `lanchat` 的旧路径作为迁移证据，Windows C# 的 `ApplicationData/LanChat` 不能直接用作 Linux 默认。对路径别名、已存在但只读的数据和不同位置的有效数据，报告读写位置并提示处理。接收目录和历史原文件随用户保留。 |

首次升级读取旧配置中的 `NickName`、`AutoAcceptFiles` 等键，保留未知键。首次改写前备份到同目录可区分版本的文件，成功后原子提交；旧路径不可写时保留原数据，经确认才在用户选定可写位置存储。既有历史优先就地读取，保留原附件路径，只对必要的格式转换做备份和可重复执行。平时用户主动通过设置更改历史目录，按 Windows 基线提供迁移与失败反馈。回退前验证旧版本能识别的数据位置和格式，必要时按备份恢复配置读取，另保留新版历史及附件。公开版和候选版的历史可见性分别验证。 [旧路径逻辑](../../src/linux/lanchat_app/storage.py) · [旧历史存储](../../src/linux/lanchat_app/history.py) · [Windows 历史设置](../../src/UI/Controls/SettingsPanels/ChatHistorySettingsPanel.cs)

### 4. 双架构实机放行

| 门槛 | 最低证据 |
| --- | --- |
| 首包集成 | 一个架构的 Linux 实机或虚拟机，以安装入口启动 Electron 和 ELF，完成待测聊天及一次文件传输。生成两架构包并核对 Electron、ELF 机器类型及包元数据。此步是集成验证。 |
| 候选支持组合 | 第 0 步冻结首发发行版、架构、桌面和图形后端矩阵。Ubuntu 22.04／24.04 为拟验版本，凡列为支持的发行版与架构均验安装、原版本升级与回退和 GUI／CLI 基础。虚拟机的包管理和启动记录保留环境标签。 |
| 两架构实机 | 至少一台物理 x86_64 和一台物理 ARM64，各以承诺支持的 Linux 桌面启动对应 `.deb`，核对 `dpkg --print-architecture` 和包内两种二进制的 ELF 机器类型（x86-64 62、AArch64 183）。每台按实际前代包升级并回退，验证用户数据、桌面启动、无头 CLI、退出与端口释放。 |
| 图形与中文输入 | 物理桌面矩阵至少覆盖 x86_64 原生 GNOME Wayland 与 Xorg，ARM64 在它实际承诺的桌面和后端上验收，若承诺 ARM64 原生 Wayland，则需要独立补齐该格。每台验一次中文预编辑、候选确认、发送与换行。整体矩阵覆盖 fcitx5 和 ibus，核对原生 Wayland 和 Xorg 各自的输入行为。 |
| 局域网与兼容 | 每台 Linux 实机与物理 Windows `v2.0.16` 互验发现、私聊、群聊、双向文件 SHA-256、退出和端口释放。至少一个代表环境完成真实 TUN、多物理网卡与无默认路由场景，并对 `v2.0.15` 和公开旧 ELF 验证已承诺的互通范围，保留旧版本已知限制。逐项留存真实网卡、子网、路由、端口和失败证据。 |

上述实机门槛缺证据时，记录对应组合尚待验证。KDE、额外分数缩放与更多发行版组合随实际支持声明纳入。已有虚拟机、回环与工具静态检查供定位和回归。

### 5. 采纳时替换的项目规则文字

| 位置 | 拟替换文字 |
| --- | --- |
| 第 4 条 Linux 交付要求 | Linux 提供 x86_64 与 ARM64 的 Electron 桌面安装包和便携包，以及每种架构对应的自包含 ELF CLI。桌面使用同架构 ELF 作为通信服务，独立命令行可直接运行 ELF。新版安装及便携入口无需 Python 运行时。既有 Python 版作为升级、回退和互通验证的来源。 |
| 第 9 条 Linux 落地规则 | Linux 应用内界面对齐 GrokBot 视觉规格和 LanChat Windows 功能基线。沿用 LanChat 图标及吉祥物几何，允许构建期用 GDI+ 导出资源交由 Electron 加载，圆角由 CSS 或等效绘制按同一尺寸验收。文件和目录选择使用系统对话框，应用内菜单、提示和选人使用暗色叠加层，提供键盘操作与焦点恢复。Linux 系统托盘、通知与窗口管理按采纳的桌面等效表验收。 |
| 第 10 条 Linux Python 分发段 | Linux 手写桌面 JavaScript、CSS 与 C# 业务源码按职责拆分，执行 1000 行预警和 1500 行上限。Electron 本体、第三方依赖与生成产物从手写行数统计中排除。Linux 桌面交付按第 4 条执行。 |

## 证据

| 核对项 | 本轮证据与边界 |
| --- | --- |
| 仓库基线 | `main` 的 HEAD 为 `2957dcf`，公开版为 `a0e49c1`。本地已有 LC-LINUX-001 候选的未提交修改，包括共享网络 C# 小修、拆分后的 Python 和打包脚本。WinForms 主窗口与当前 Windows 客户端实机视觉快照仍待第 0 步固定。 |
| 网络可复用 | 现有 [Linux C# 项目](../../src/linux/LanChat.Linux/LanChat.Linux.csproj) 以源码链接方式构建 `Models` 及全部 `NetworkService` 分部，已经用作自包含 ELF CLI。现有 [发现](../../src/NetworkService.Discovery.cs)、[网卡](../../src/NetworkService.Interface.cs)、[传输](../../src/NetworkService.Transfer.cs) 仍决定 Linux 的多网卡、LANC 和 ACK 行为。桌面服务 IPC 是拟新增部分。 |
| 候选轮次已有证据 | [实施验证](../LC-LINUX-001/VALIDATION.md) 记录 Windows 宿主双向非默认端口、中文、文件 SHA-256、Windows JSONL 互读，以及两个 Ubuntu 架构的 QEMU 安装与无头检查。QEMU 使用用户态 NAT，ARM64 安装的是 `all` 并另行执行 ELF。此处沿用记录，待验实机桌面与物理双机。 |
| 已核验迁移样本 | 公开 `release/` 中的 `amd64`／`all` deb 包含 `lanchat.py`，`amd64` 另含 ELF。被 Git 忽略的 `bin/lanchat-step2/` 已找到对应候选 deb，含 `lanchat.pyz`，两个 SHA-256 与 [实施验证](../LC-LINUX-001/VALIDATION.md) 一致，前缀分别为 `87b1bbb912993ed8` 与 `3ff0b098d78ab1ba`。`bin/lanchat-handoff/LanChat-LC-LINUX-001-candidate.zip` 与同级 SHA-256 清单相符，包内确认含两份候选 deb 和便携 `.pyz`。四份 deb 的 `Depends` 均含 `python3`，`python3-tk` 列为 `Recommends`。取样以路径、内容与摘要区分同版本文件。 |
| 旧入口、路径与写入 | [现打包入口](../../packaging/build-deb.py) 与 [Python 启动器](../../src/linux/lanchat_app/app.py) 提供桌面、显式 CLI 和无头分流。候选 [设置](../../src/linux/lanchat_app/storage.py) 优先读已存在的同级配置，[历史](../../src/linux/lanchat_app/history.py) 对不可写便携目录有回退。复用 Windows C# 服务时需注入实际路径，.NET 的 `ApplicationData` 在 Linux 也可能随 `XDG_CONFIG_HOME` 变化。 [Windows 历史服务](../../src/Services/ChatHistoryService.cs) · [.NET 运行时源码](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Environment.GetFolderPathCore.Unix.cs) |
| 并发收件风险 | [共享网络服务](../../src/NetworkService.cs#L171) 先检查目标名是否存在，[接收与落盘](../../src/NetworkService.Transfer.cs#L137) 再从该名字派生 `.downloading` 路径。同名并发可能选到同一路径。这是静态检查，第 2 步取实测；如复现，修复共享接收路径并验证 Windows 原生构建与文件兼容性。 |
| 桌面规范和功能边界 | 视觉依据 [GrokBot 规范](../../Docs/GrokBot_UI_Design_System.md)、[GrokTheme](../../src/UI/Controls/GrokTheme.cs)、当前 WinForms 业务接线。源码中的 [常规页](../../src/UI/Controls/SettingsPanels/GeneralSettingsPanel.cs) 有部分空回调和仅改变按钮文案的条目，[网络设备页](../../src/UI/Controls/SettingsPanels/NetworkDeviceSettingsPanel.cs) 有展示性开关。Windows [更新服务](../../src/Services/UpdateService.cs) 选择 MSI，Linux 需按平台映射。 |
| 框架依据 | [Electron 进程模型](https://www.electronjs.org/docs/latest/tutorial/process-model) 支持本地 Web 渲染与桌面主进程分工。WorkBuddy 用于说明 Electron 跨平台的可行性。样式、资产和用例全部来自 LanChat 自身。 |

## 未核实

| 风险 | 最迟核实与通过条件 |
| --- | --- |
| Windows 当前实机的真实功能状态和视觉冲突 | 第 0 步冻结截图和操作清单。每项有菜单入口、可观察结果和可复核源码。对只展示、禁用或功能未接通的条目提供证据。 |
| 两种架构的 Electron、.NET、图形依赖、沙盒和系统托盘 | 第 1 步记录首包架构、桌面环境、实际图形后端、包体和进程资源。第 3 步取证 Linux 桌面等效操作，第 4 步完成两架构发布所需实机格。 |
| 旧数据和安装换代 | 第 2 步实际演练 apt 架构替换与降级、旧 GUI 启动，核对公开版、候选版、安装与便携的格式及路径优先级。覆盖旧记录、UTF-8/BOM、尾部坏行、未完成操作、同 IP 重连、目录不可写、重复迁移、回退和旧绝对路径。 |
| 共享网络并发与恢复 | 第 2 步从两个独立发送端同时向同一接收端发送同名、不同内容的文件，两份均保留并分别核对 SHA-256、操作终态及最终 ACK。覆盖发送期间子进程退出、重连以及另一入口尝试打开同一数据目录。 |
| 中文输入与长历史 | 第 3 步在 Xorg 和 GNOME Wayland 分别用 fcitx5、ibus 测预编辑、候选确认、Enter 发送、Ctrl+Enter 换行和焦点恢复。用同一份至少 2000 条历史测切换、复制、追加、滚动锚点和重排。根据第 0/1 步测量登记包大小、冷启动、空闲内存、历史切换及滚动指标和验收阈值，阈值在正式实现前由审核确认。 |
| 物理网络与平台等效行为 | 第 4 步实测真实 Windows／Linux 双机、两架构、旧 Windows `v2.0.15`／`v2.0.16` 及旧 ELF。记录防火墙、物理网卡、子网、TUN、默认路由和发现耗时。窗口、托盘、通知、更新和字体差异逐项确认。 |

## 建议顺序

| 步骤 | 实施内容 | 放行条件 |
| --- | --- | --- |
| **0 · 固定基线和验收清单** | 冻结 Windows 实际构建、截图、功能与设置状态。以摘要保全旧公开包、候选包和便携样本，准备含设置、历史及接收文件的回退样本。 | Windows 对照清单、桌面等效表、旧数据识别和回退口径、两架构支持矩阵与规则替换文字均有明确落点。 |
| **1 · 打通 Electron 首包** | 在一个架构的实机或虚拟机上，从安装后的桌面入口启动 Electron 和真实 ELF，验证启动、刷新、聊天、文件传输、退出及无头 CLI。生成两架构试验包并核对架构。 | 一条 GUI → ELF → 对端路径完成收发和文件 SHA-256 核对，退出无遗留进程，沙盒正常，启动器及依赖无需 Python。该结果进入集成记录。 |
| **2 · 接通会话、文件和升级** | 实现数据读取、离线归档和重连归并，接通草稿、并发进度和文件终态，补齐 CLI。按第 3 节在支持的发行版与架构组合演练旧包升级、便携、卸载重装和回退。 | 旧 GUI 能实际恢复，数据路径和原文件可追溯。两端非默认端口、短读、拒收、中断、最终 ACK 缺失、并发同名异内容、重启历史与进程异常都有证据。按流处理大文件，保留两份接收哈希及发送终态。 |
| **3 · 对齐全部界面与桌面操作** | 依次完成主窗聊天、节点与文件、八个设置页、托盘窗口和维护入口。逐项核对视觉、真实行为、设置持久化和桌面等效表。 | 对照清单逐项附截图、录屏或可复跑操作。覆盖窗口可恢复性、通知不可用、输入法、浮动滚动条、焦点、多屏／缩放、长历史及性能。 |
| **4 · 双架构正式候选验收** | 按第 4 节在两个架构的物理设备与物理 Windows 联测，核对包体、版本、架构、哈希、旧版互通、TUN 和多网卡，复跑 Windows 原生构建。 | 两个实机格及承诺的核心矩阵全部具备通过证据。留存实际安装与回退步骤、平台等效决定和构建摘要。业务代码进入 [CRDocs](../../CRDocs/STATUS.md) 独立审核，公开发布等用户指令。 |

Grok 第 2 轮优先复核桌面恢复路径及通知回退、旧包与实际数据位置、双架构和核心回归门槛，以及第 4、9、10 条的拟替换文本。若需调整，给出影响步骤、最小修订和完整顺序。

## 明确不做

- Windows Electron 迁移作为后续独立议题，以 Linux 候选验收结果作为输入。
- 互联网会话、新业务功能、协议换代和额外发行版支持沿用各自的立项流程。本案的通信兼容目标是现有 LAN 协议与公开版本。
- 正式发布属于用户另行授权的交付步骤。本轮交付 PlanDocs 提案与审核交接。

## 对上一轮审核的处理

[第 1 轮提案](PROPOSAL-R1.md) · [Grok 第 1 轮审核](REVIEW-R1.md)

| 意见 | 处理 | 依据与改动 |
| --- | --- | --- |
| R1 · Wayland 等效操作 | **部分采纳** | 采纳按实际后端取证、托盘不可用时保留恢复入口、合成器处理窗口边界。合成器可拒绝焦点，通知服务也可忽略点击动作。第 2 节按可观察结果验收，将焦点和通知点击作为支持条件。 |
| R2 · 升级与回退 | **部分采纳** | 采纳包和数据分别演练、保留包名和用户文件、版本递增与降级验收。第 3 节改为识别实际同级路径和不可写回退，保留用户主动更改历史目录的功能。旧包中 Tk 为推荐项，回退验收增加实际启动。证据节记录已找到的候选包和交接 ZIP。 |
| R3 · 实机放行 | **部分采纳** | 采纳单架构首包集成、双架构分别实机放行。第 4 节保留中文输入、TUN、多网卡及约定旧版本互通的核心回归。ARM64 的具体桌面和后端依据首发承诺设门槛。 |
| R4 · 规则替换文字 | **部分采纳** | 第 5 节给出三处可直接替换的文本。第 9 条允许按规格用 CSS 实现圆角，第 10 条替换旧 Python 分发段，手写源码的拆分约束继续生效。 |

另补共享核心并发同名收件的实测条件，见「证据」和第 2 步放行。该项与 Linux 接入共享 C# 核心直接相关。
