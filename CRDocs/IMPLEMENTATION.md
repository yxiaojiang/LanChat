# LC-CHAT-COMPOSER-001 — Round 2 整改交接

> 2026-11-24 · 执行者 Codex · 状态 `WAITING_FOR_GROK` · 交付为未提交工作区

## 整改内容

| 问题 | 实现与断言 |
| --- | --- |
| `LC-CC-001` | `ChatTextLayout.PaintText` 用 `NextBoundary(text, i)` 推进到每个可见元素末尾，对整个元素识别导入表情及 Unicode Emoji。组合表情与独立 BMP Emoji 通过 Segoe UI Emoji 单段测量和绘制。常规文字连续排版。 `ChatComposerRegression.cs` 对 `✋🏽`、`✌🏽`、`❤‍🔥`、`✋`、`✨`、`☕` 比较整段 GDI+ 测量宽度、混排宽度和逐像素绘制。 |
| `LC-CC-002` | `preload.cjs` 的内存文件直传限 PNG/JPEG。其他图片从主进程 `clipboard.read()` 获取系统提供的 PNG/JPEG，普通文本走原生粘贴。主进程先试 PNG，再试 JPEG，候选解码为空时继续，内存图解码为空也读取系统剪贴板。剪贴板只有非兼容格式时没有图片草稿。测试覆盖 WebP/BMP/GIF、PNG/JPEG 直传、普通文本、文件路径优先、空 PNG 继续尝试 JPEG、内存空图继续尝试系统 JPEG，以及所有可解码候选均为空时的错误提示。图像读取与解码接口参照 [Electron Clipboard](https://www.electronjs.org/docs/latest/api/clipboard) 和 [NativeImage](https://www.electronjs.org/docs/latest/api/native-image)。 |

## 职责与行数

先将主进程路径、窗口边界和剪贴板夹具抽到 `tests/linux_desktop_ipc_regression.js`，原 Node 入口继续执行该模块，再在模块里补充预加载和主进程的剪贴板用例。现有工作区 20 个改动或新增源码与测试文件均低于 1000 行。

| 本轮变更的源码或测试 | 行数 |
| --- | ---: |
| `src/UI/Renderers/ChatTextLayout.cs` | 209 |
| `tests/ChatComposerRegression.cs` | 188 |
| `src/linux/desktop/preload.cjs` | 96 |
| `src/linux/desktop/main.cjs` | 575 |
| `tests/linux_desktop_ui_regression.js` | 869 |
| `tests/linux_desktop_ipc_regression.js` | 220 |

`renderer.js` 为 961 行，本轮整改落在预加载与主进程。LC-CC-003 的摘要分段涉及 Unicode 边界、表情令牌与侧栏渲染，随 renderer 拆分处理。LC-CC-004 一并评估后留给附件生命周期专项，异步失败收尾须同时覆盖双端历史引用和 Linux 退出竞态。LC-CC-005 及 LC-CC-006 剩余用例已登记到 [DEFERRED.md](DEFERRED.md)。

## 验证命令与实际结果

以下命令均在 Windows 宿主机从仓库根目录重跑。

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`，退出码 0。

   ```text
   Native Windows compile: ok
   LC-FR-001: 无 BYE 同端口换 ID、锁外通知、未读迁移和 TCP 确认隔离通过
   LC-FR-002: 无网卡事件时手动重绑、重试失败监听及复用正常快照通过
   LC-FR-003 / LC-FR-008: 缩表方向键、空分组和滚动锚点通过
   LC-FR-004: 标题行热区拖拽、无溢出拦截和 x64 负坐标通过
   LC-CHAT-COMPOSER-001: 联合标题间距、圆点颜色、图像草稿暂存与移除通过
   ```

2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`，退出码 0。

   ```text
   BMP 肤色、ZWJ 和独立表情的 Segoe UI Emoji 整段测量及 GDI+ 像素对照通过
   聊天表情布局、PNG 与位图粘贴数据、LANC 文本和图片回环及历史回读通过
   ```

3. 在 PowerShell 中使用 `& 'C:\Program Files\nodejs\node.exe' --check <文件>` 逐一检查下列文件，各命令退出码均为 0，Node 无输出。

   - `src/linux/desktop/main.cjs`
   - `src/linux/desktop/overlay.js`
   - `src/linux/desktop/preload.cjs`
   - `src/linux/desktop/presentation.js`
   - `src/linux/desktop/renderer.js`
   - `tests/linux_desktop_ui_regression.js`
   - `tests/linux_desktop_ipc_regression.js`

   `& 'C:\Program Files\nodejs\node.exe' tests/linux_desktop_ui_regression.js` 退出码 0，输出 `linux desktop ui regression: ok`。

4. `python -m py_compile Tools/Scripts/import-wechat-emoji.py`，退出码 0，无输出。
5. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`，退出码 0。输出 `LanChat.Linux -> D:\TEmp\LanChat\src\linux\LanChat.Linux\bin\Release\net10.0\lanchat.dll`、`已成功生成。`、`0 个警告`、`0 个错误`，用时 `00:00:01.71`。
6. `git diff --check`，退出码 0，无空白问题。Git 输出当前工作区配置下的 LF 转 CRLF 提示。

## 未验证边界

- 真实双机 LAN 联测尚未执行。待验收同包表情收发、复制和历史重开，以及与未导入资源端和公开旧版的文字、表情及 PNG 互通。
- Windows 截图工具和图像应用的实际 Ctrl+V、Shift+Insert、右键粘贴链路尚未验收。
- Linux Xorg、Wayland、XWayland 的 Electron 实机剪贴板尚未验收，包括截图工具、文件管理器及浏览器复制图像时是否提供 PNG/JPEG。Linux/QEMU 上的最新 ELF 与 `tests/linux_desktop_regression.py` 尚未执行。详见 [LC-CC-ENV-001](DEFERRED.md#lc-cc-env-001-open-剪贴板与新旧客户端的桌面验收)。

---

# LC-CHAT-COMPOSER-001 — Round 1 实现交接

> 2026-11-24 · 执行者 Codex · 基于 Git main 的 1c3302e · 等待 Gemini 独立审核。

## 入口和数据流

| 范围 | 实现 |
| --- | --- |
| 本机表情来源 | `Tools/Scripts/import-wechat-emoji.py` 校验本机 `C:\Program Files\Tencent\Weixin\4.2.0.13\Weixin.dll` 的完整哈希，读取 Qt 资源的默认静态表情图、面板顺序与现代名称映射。用人工核对的经典表情映射补齐名称，按顺序导出 109 个 PNG 和 manifest.tsv。PE 资源表的 REA 记录 `ev_442562ff91eefa595a83f258f84c44320d026ebe03272409fbce3130e1cbb16d` 只覆盖 PE 资源表。PNG 与面板 XML 的证据来自 DLL 内的 Qt 资源和导入结果。 |
| 表情导入与渲染 | Windows 从 `%LOCALAPPDATA%\LanChat\WeChatEmoji` 读取。Linux Electron 从 `app.getPath('userData')/WeChatEmoji` 读取，由主进程只返回清单名称及对应 PNG。导入脚本支持 --dll 与 --output，导入后重启客户端。发送、历史、复制都保留原始 Unicode 或 `[名称]`，聊天气泡匹配本地资源。无资源时保留 Unicode 选择器和名称文本。Windows 的 ChatTextLayout 从聊天控件抽出，统一折行、划选、绘制和侧栏预览。 |
| 联系人分组 | WinForms 的 MeasureItem 与绘制共用 8 像素间距条件，Linux 通过在线标题与离线标题的 CSS 相邻顺序添加间距。状态点直径 6 像素，色值为 #1FD67E 和 #FC4455。 |
| Windows 粘贴 | ChatInputBox 优先识别文件列表，再识别 PNG 或系统 Bitmap。Ctrl+V、Shift+Insert、WM_PASTE 以及现有暗色右键菜单进入同一流程。MainForm.Files 保存 PNG 并为当前会话提供预览。移除、删除会话或关闭时清理未提交截图，提交后保留本地历史引用。 |
| Linux 粘贴 | preload 捕获可信粘贴事件，读取 Chromium 提供的内存图像，或通过 Electron 45 的 clipboard.read() 读取当次系统剪贴板快照。主进程生成 PNG、缩略图和草稿 ID。提交后沿用现有文件队列，若开始发送失败则恢复草稿 ID。原始文件和发送后图片各自保留历史可打开路径。 |

LANC 文字和文件封包、网络服务以及 JSONL 历史结构均沿用现有入口。图片附件继续受文件接收设置约束。已提交的剪贴板 PNG 会保留在本机 LanChat 的 PastedImages 目录。

## 验证与行数

验证命令与退出结果见 REVIEW.md。本轮改动文件中 Linux 模拟测试 980 行、renderer.js 961 行、WeChatChatView.cs 862 行，其余手写源码和测试均低于 1000 行。Windows 原生试用产物位于忽略目录 `bin/chat-composer/LanChat.exe`。当前移交的是工作区代码和独立编译产物，现场验收范围见 LC-CC-ENV-001。

---

# LC-FRIEND-REFRESH-001 — Round 2 返修

> Codex 执行 · 2026-11-24 · `READY_FOR_REVIEW` · 工作区交付

## 本轮修改

| 编号 | 实现 |
| --- | --- |
| 发布版本联动 | 发布核验发现 `packaging/build-electron.py` 和 `src/linux/desktop/package.json` 未被原版本脚本同步，首次生成的 Electron 资产仍标记 2.0.18。已扩展 `packaging/bump-version.py` 同步新版 Electron 打包器和桌面元数据，重建 v2.0.19 双架构包并替换同一 Release 资产。 |
| `LC-FR-001` | 带监听端口的身份公告在 `_peerLock` 内按 `(IPv4, TcpPort)` 归并旧节点并累加到当前节点的未读，完成归并后在锁外派发 `OnPeerLeft`。收到 TCP `0x01` 时复核网卡快照、候选对象及端点归属。同端点存在其他身份或候选已被移除时拒绝续期。无指定身份的握手只用于连通性和地址占位。 |
| `LC-FR-002` | 手动刷新、启动和网卡事件回调统一走 `RefreshPeers()`。刷新先重绑，再取快照发包，重试缺失的网卡或通配 UDP 监听。正常套接字和未变化的接口快照可复用。 |
| `LC-FR-003` / `LC-FR-008` | 重建时同步清空旧索引，键盘跳过标题时校验旧索引并在主方向无会话时反向查找。选中会话恢复后，以会话键或标题恢复视口锚点，再按当前行高和客户区夹紧。 |
| `LC-FR-004` | 用 `LParam` 低 32 位解析两个有符号坐标。右侧 12px 有溢出时进入 `OnMouseDown` 和 `OnMouseUp`，绕过原生 ListBox 会改变当前选择的左键流程。普通标题点击继续被拦截。 |
| `LC-FR-005` / `LC-FR-009` | Linux renderer 合并地址占位时保留运行期或归档昵称。分组标题声明 700 字重并同步记录在线标题状态。Node 模拟覆盖底栏刷新入口、标题、筛选、键盘导航和刷新后的昵称。 |
| `LC-FR-006` | 保护网卡事件反订阅，扫描睡眠后检查服务及快照，及时释放 TCP 异步连接等待句柄。 |

LANC 握手帧、TCP `0x01` 及 UDP 六段公告兼容。旧客户端只回 `0x01` 时，仍可复核唯一且端点未发生可观察变更的已知身份。单个确认字节无法区分同端点新旧实例，识别换 ID 依赖收到新身份公告或已有端点冲突证据。`LC-FR-007` 已登记延期，涉及跨子网 TUN 路由及无历史离线地址保留。

## 验证命令与结果

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`，退出码 0。系统 `csc.exe` 先编译生产 Windows 应用，再编译隔离夹具。夹具用本机回环和临时端口，注入可控网卡枚举并关闭实际网卡事件和外部广播。下列检查全部通过。
   - `LC-FR-001` 的对端不发 `BYE`，关闭并以同 IP 同端口换 ID 重开。刷新复核已经连上新实例时收到 UDP 公告，验证旧未读迁移、锁外通知、新昵称和同 IP 异端口保留。之后放行迟到的 TCP `0x01`，验证候选旧记录和新记录的时间戳均未被误写。额外验证同端点冲突、无指定身份的探测及旧版单一端点复核。
   - `LC-FR-002` 在无网卡事件时点击刷新，验证 UDP 实际绑定新接口、关闭旧监听并使用新 IPv4 发六段公告。模拟上次绑定留下空套接字，验证下一次刷新重绑，而继续刷新复用健康快照。
   - `LC-FR-003` 和 `LC-FR-008` 在主窗缩表后发送 `Home` 和 `Down`，覆盖旧索引越界与被标题占用。另测末尾标题 `End`、仅有标题、缩表夹紧，以及选中群聊时向下浏览后重建仍保留视口锚点。
   - `LC-FR-004` 发送原生 `WM_LBUTTONDOWN`、`WM_MOUSEMOVE`、`WM_LBUTTONUP`，验证标题高度上的滑块热区可拖动视口并保留当前会话。无溢出或标题正文区按下时保持拦截，同时覆盖 x64 负 Y 坐标。
2. 令 `$out` 指向该轮夹具在 `bin/friend-refresh-round2/` 下的输出目录，执行 `$fixture = (Resolve-Path tests/ConcurrentReceive.cs).Path`，随后执行 `& "$env:windir\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:exe "/r:$out\LanChat.exe" "/out:$out\ConcurrentReceive.exe" $fixture` 和 `& "$out\ConcurrentReceive.exe" "$out\ConcurrentReceive"`。退出码 0，`Final ACK: 1, 1; Complete events: 2`，两个同名文件均通过 SHA-256 校验。
3. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`，退出码 0，0 警告、0 错误。此命令在 Windows 构建共享后端。
4. 以 `C:/Program Files/nodejs/node.exe` 执行 `--check src/linux/desktop/renderer.js`、`--check tests/linux_desktop_ui_regression.js` 和 `tests/linux_desktop_ui_regression.js`，均退出码 0，回归输出 `linux desktop ui regression: ok`。测试使用模拟 DOM 与桩 IPC。
5. `git diff --check` 通过，复核 `CRDocs/REVIEW.md` 文件散列与本轮改动前相同。

## 行数门禁

| 本轮源码或测试文件 | 行数 |
| --- | ---: |
| `src/NetworkService.Discovery.cs` | 639 |
| `src/NetworkService.Interface.cs` | 247 |
| `src/NetworkService.cs` | 254 |
| `src/MainForm.Chat.cs` | 645 |
| `src/UI/Controls/SessionListBox.cs` | 363 |
| `src/linux/desktop/renderer.js` | 953 |
| `src/linux/desktop/desktop.css` | 182 |
| `tests/FriendRefreshRegression.cs` | 351 |
| `tests/friend_refresh_regression.ps1` | 61 |
| `tests/linux_desktop_ui_regression.js` | 935 |

按编辑器行号口径，Round 1 与 Round 2 工作区内全部改动文件低于 1000 行。本轮新增和调整的代码注释均使用中文。

## 未验证边界

- `LC-FR-ENV-001` 的真实多机 LAN、Wi-Fi/有线切网、DHCP、休眠唤醒、物理多网卡/TUN 及公开旧版跨机互通仍待验收。本轮在 Windows 上完成编译、回环和原生 WinForms 消息级验证。
- `LC-FR-ENV-002` 的最新 Linux ELF 网络命名空间回归（`tests/linux_desktop_regression.py`）以及 Linux Electron 实机侧栏与交互仍待验收。

---

# LC-FRIEND-REFRESH-001 — Round 1：好友刷新延迟、网卡切换与在线/离线分组

> 执行者：Codex；审核者：Gemini（待审核）
> 日期：2026-11-24；状态：READY_FOR_REVIEW
> 工程：D:/TEmp/LanChat
> 交付形态：工作区未提交；未改写审核者维护的 `CRDocs/REVIEW.md`；编译产物与 GUI 测试夹具放在忽略的 `bin/` 中。

## 本轮范围

修复 Windows 启动、改昵称和换网后的好友刷新延迟，在截图左下栏头像右侧增加手动刷新按钮，将联系人明确分为「在线」「离线」，且在刷新时同步昵称和分组。同步维护当前共享 NetworkService 的 Linux Electron 启动、改昵称和刷新入口，避免共享核心改动后 Linux 继续调用旧入口。LANC 封包、TCP 握手字节和现有 UDP 命令字段均保持原样。

## 执行记录

1. `NetworkService.Discovery.cs` 不再为每个地址创建一个线程，也不再对每个地址串行等待连接超时。手动刷新优先单播询问存档和运行期已知地址，随后并发复核在线好友。全网段 UDP 扫描在后台发出，缺少 UDP 身份回应的地址由有界工作线程补充 TCP 握手；已公告的好友复核使用其公布的 TCP 端口。
2. TCP 握手仍使用既有 LANC 帧和确认字节。对端收到握手后通过既有 UDP ACK 返回完整节点 ID、昵称、监听端口与地址，供刷新方更新头像列表。旧客户端仍以 TCP 确认兼容，地址占位记录不覆盖已知昵称。
3. `NetworkService.Interface.cs` 监听本机 IPv4 地址变化，对短时间连续通知合并处理并重绑 UDP 网卡套接字；发送时选择与目标同子网的物理网卡。刷新探测期间不依赖旧地址快照。
4. `SessionPresenter.cs` 保存已见好友快照，按在线状态与离线历史会话建立两个标题分组，继续用既有历史存储恢复离线好友和昵称。列表重绘时恢复滚动锚点、当前会话和过滤条件。标题不可选，鼠标与键盘切换会话时跳过标题。
5. `MainForm.Designer.cs` 在左栏底部用户头像右侧加入 36px GDI+ 圆形刷新按钮。Linux Electron 将既有刷新按钮移到侧栏底部头像右侧，列表增添相同的在线、离线标题。
6. 修改文件共 14 个：`src/Controllers/SessionPresenter.cs`、`src/MainForm.Chat.cs`、`src/MainForm.Designer.cs`、`src/MainForm.Tray.cs`、`src/MainForm.cs`、`src/NetworkService.Discovery.cs`、`src/NetworkService.Interface.cs`、`src/NetworkService.Transfer.cs`、`src/NetworkService.cs`、`src/UI/Controls/SessionListBox.cs`、`src/linux/LanChat.Linux/DesktopService.cs`、`src/linux/desktop/desktop.css`、`src/linux/desktop/index.html`、`src/linux/desktop/renderer.js`。

## 验证记录

1. Windows 原生编译：`cmd.exe /d /c 'Tools\Scripts\Compile.bat < nul'`，退出码 0，生成 `release/LanChat.exe`。编译产物复制到忽略目录 `bin/friend-refresh/LanChat.exe` 供审核试用，跟踪中的 `release/` 文件已还原。
2. Windows 隔离 GUI 和网络回环夹具：使用隔离 UDP/TCP 端口、回环地址和夹具历史目录，对启动识别昵称、手动刷新昵称、昵称广播、网卡地址变更重绑、已知非默认 TCP 端口复核、无 BYE 下线分组、昵称保留、重新上线、标题行鼠标/键盘不可选、当前会话/搜索保留及按钮位置进行实测。记录耗时分别为启动 455ms、按钮刷新 4ms、昵称广播 0ms、模拟切网 269ms、已知端口复核 321ms、无 BYE 下线确认 1673ms、重新上线 0ms。GUI 截图位于忽略目录 `bin/friend-refresh-check/friend-refresh.png`。此回环夹具只在隔离源码副本改用 48888/48889 端口与可控回环接口，不改项目源码端口。
3. Windows 同名并发文件接收回归：通过独立夹具程序集和临时目录运行 `tests/ConcurrentReceive.cs`，两个 ACK 均为 1，两个接收完成事件均出现，两个最终文件 SHA-256 校验通过。
4. Linux 共享后端宿主机构建：`bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`，0 警告、0 错误。
5. Linux Electron 界面：`node --check src/linux/desktop/renderer.js` 与 `node tests/linux_desktop_ui_regression.js` 均退出码 0。
6. `git diff --check` 通过。所有修改源码均少于 1000 行；本轮最高的 `src/linux/desktop/renderer.js` 为 945 行，低于 CRDocs 的 1000 行软门禁和 1500 行硬门禁。

## 未验证边界

- 没有 Linux 系统可执行文件运行时，未运行 `tests/linux_desktop_regression.py`，未在 Linux 网络命名空间运行 Linux ELF。
- 未在真实双机、多台 Windows 客户端和用户实际 Wi-Fi/有线换网环境验收延迟、广播限制、多网卡及防火墙影响。回环测得时间不能作为实际局域网性能保证。
- 未在 Linux Electron 桌面实机验收侧栏视觉或点击刷新时昵称分组实时更新。
- 本轮没有编译 MSI、覆盖安装目录、提交或推送。

## 审核关注

请重点独立核对 UDP ACK 与 TCP 握手并发时的好友身份归并和存活时间、刷新版本与计时器并发、网卡重绑时正在运行的工作线程、动态行高列表滚动/键盘/右键选择，以及 Linux 前端改动与现有布局规则。按 `CRDocs/README.md` 将分级结论写入 `CRDocs/REVIEW.md` 并更新 `CRDocs/STATUS.md`；有 P0/P1 时记录具体复现条件并转为 `CHANGES_REQUESTED`。不要将宿主机回环和构建证据描述成 Linux 或真实 LAN 实机验收。

---

# LC-LINUX-002-STEP-3-SLICE-2 — Round 2：P1 与 P2 整改交付

> 执行者：Gemini；审核者：Grok（本文件仅记录执行者实施与验证）
> 日期：2026-11-23；状态：已交接并经用户确认审核通过（APPROVED）
> 工程：D:/TEmp/LanChat
> 交付形态：未提交工作区，未修改 `CRDocs/REVIEW.md` 审核历史，未修改 `handoff-experiment.2` 至 `handoff-experiment.5`

## 本轮范围

按 Round 1 审核意见修复全部 2 条 P1 与可一并处理的 P2，不扩大到托盘恢复、系统通知或检查更新维护入口，不改 LANC 封包结构。

## P1 整改

### LC-S3-001 外观四项按 WinForms `AppSettings` 落盘，即时作用主窗与设置窗并重启保持

1. **配置读取与落盘（`LinuxProfile.cs` / `DesktopService.cs`）**：
   - 按 `src/Services/AppSettings.cs` 同一键名与校验范围读写 `ThemeMode`（`0..2`，默认 `0`）、`AccentPreset`（`0..4`，默认 `0`）、`UiFontFamily`（默认 `"Microsoft YaHei UI"`）、`UiFontSize`（`8.0..24.0`，默认 `10.0`）、`ChatFontFamily`（默认 `"Microsoft YaHei UI"`）、`ChatFontSize`（`8.0..26.0`，默认 `10.5`）。
   - `SaveSettings(...)` 保留既有未知键并支持只读配置 `overridePath` 二次确认与重启恢复；外观字段设为可选参数，不破坏已有 CLI/回归调用。
2. **统一设计令牌与即时渲染（`appearance.js` / `desktop.css` / `settings.css`）**：
   - `appearance.js` 严格复现 `GrokTheme.Surface(r, g, b, mode)` 三套深色主题、五套强调色、七种字体栈映射、`--ui-font-delta`（`uiSize - 10.0` pt）与 `--chat-font-size`，供主窗与设置窗共用。
   - 主窗 `renderer.js` 在 `initialize()` 并行拉取 `api.settings()` 恢复外观，收到 `settings-updated` 后立即刷新外观变量与输入框高度；设置窗 `settings.js` 启动时及 `updateAppearance()` 时立即应用外观变量（支持快速拖拽合并持久化，只读确认取消时回退到 `committed`）。
   - 通用页「字体大小」滑块与外观页「主界面字体大小」滑块绑定同一 `committed.uiFontSize` 并双向同步。
3. **展示项边界与文档修正**：
   - 语言、链接打开方式、协议通道、快捷键徽章、托盘预览、后台网络驻留运行（保持空回调 `() => {}`）、分块校验、自动加载、TUN 过滤、单播探测继续仅做本地展示，未新增 RPC。
   - 修正 `settings.js`、`settings-model.js` 与 `PlanDocs/LC-LINUX-002/INTEGRATION.md` 中关于外观项「Windows 未接线」的过时说明。

### LC-S3-002 `histories-cleared` 作废进行中的 `historyLoads`

- `src/linux/desktop/renderer.js` 在 `handleEvent` 收到 `histories-cleared` 时先执行 `historyLoads.clear()`，再清空 `messages`、`unread` 与摘要并重画；在途 `loadHistory(chat)` 于 `await api.history(chat)` 返回或异常后检测到 `historyLoads.get(chat) !== operation` 即放弃写回气泡与错误状态。

## P2 整改

1. **反馈框浮动滑块**：`settings.html` 为 `#feedback-input` 套用 `.scroll-host.feedback-scroll` 与 `.scroll-view`；`settings.css` 隐藏原生滚动条并设置复选框 `accent-color: var(--accent)`；`settings.js` 绑定 `LanChatScroll` 并在打开反馈框时刷新滑块。
2. **小工作区尺寸夹紧**：`main.cjs` 修正 `computeSettingsBounds` 小工作区下限、新增 `computeSettingsMinSize(bounds)` 并在 `ready-to-show` 按外框 `getBounds()` 二次夹紧至 `display.workArea`；`settings.css` 增加 `@media (max-width: 600px)`。
3. **真实确认/取消及竞态自动化测试**：`settings.js` 与 `renderer.js` 支持 `LANCHAT_TEST_MODE=1` 注入；`tests/linux_desktop_ui_regression.js` 新增 `testSettingsController()` 与 `testMainRendererRaceAndAppearance()`。
4. **快捷键页对齐**：`settings-model.js` 将「多行换行输入」更新为 `Ctrl + Enter / Shift + Enter`。
5. **相同历史目录提示**：`settings.js` 对比归一化后的目标目录与原目录，相同目录提示「所选目录与当前目录相同，无需迁移」。
6. **Linux ELF 用例补充**：`tests/linux_desktop_regression.py` 补充外观 6 项重启持久化与 `clear-all-histories` 断言（待 Linux 环境运行）。

## 实际文件与行数门禁（均 < 1000 行）

| 文件 | 本轮作用 | 行数 | 门禁 |
| --- | --- | :---: | :---: |
| `src/linux/LanChat.Linux/LinuxProfile.cs` | 读写与校验 6 项外观配置，兼容只读备份与未知键 | 397 | PASS |
| `src/linux/LanChat.Linux/DesktopService.cs` | `settings` / `save-settings` 透传可选外观字段 | 453 | PASS |
| `src/linux/desktop/appearance.js` | 主窗与设置窗共用的主题/强调色/字体 CSS 变量计算 | 228 | PASS |
| `src/linux/desktop/main.cjs` | 外观 IPC 校验、`settings-updated` 广播、小工作区窗口夹紧 | 418 | PASS |
| `src/linux/desktop/renderer.js` | 启动/热更新外观应用、`histories-cleared` 作废 `historyLoads` | 765 | PASS |
| `src/linux/desktop/settings-model.js` | 外观预设接入与快捷键 `Ctrl + Enter / Shift + Enter` 对齐 | 111 | PASS |
| `src/linux/desktop/settings-pages.js` | 外观控件接通、通用字号联动、展示项保持无 RPC/空回调 | 499 | PASS |
| `src/linux/desktop/settings.js` | 外观持久化、同目录判定、反馈滑块绑定、可测注入 | 700 | PASS |
| `src/linux/desktop/desktop.css` | 主题/强调色/界面与聊天字体 CSS 变量化 | 170 | PASS |
| `src/linux/desktop/settings.css` | 主题/字号变量化、反馈框隐藏原生滚动条、窄屏适配 | 742 | PASS |
| `src/linux/desktop/index.html` | 引入 `appearance.js` | 85 | PASS |
| `src/linux/desktop/settings.html` | 引入 `appearance.js`，反馈框接入 `.scroll-host` | 91 | PASS |
| `tests/linux_desktop_ui_regression.js` | 新增外观、只读确认/取消、同目录、反馈滑块、竞态测试 | 817 | PASS |
| `tests/linux_desktop_regression.py` | 补充外观重启持久化与 `clear-all-histories` 用例 | 421 | PASS |
| `PlanDocs/LC-LINUX-002/INTEGRATION.md` | 修正切片 2 接线说明与测试边界 | 142 | PASS |

## 验证与未验证边界

1. `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js`：退出码 0，输出 `linux desktop ui regression: ok`（模拟 DOM 与桩 IPC，非 Electron 实机）。
2. `python -m py_compile packaging/build-electron.py packaging/build_linux.py tests/linux_desktop_regression.py`：退出码 0（仅语法检查）。
3. `python packaging/build_linux.py <temp>`（使用 `bin/lc-linux-002/cache/dotnet`）：`linux-x64` 与 `linux-arm64` 均 0 警告 0 错误编出并校验 ELF64 机器码（62 / 183），临时输出已清理（未在 Linux 上执行该 ELF）。
4. Edge Headless `--dump-dom`（`settings.html` `1260×820`、`index.html` `1040×720`）：静态装载与 `.feedback-scroll .float-thumb` 挂载正常（无预加载桥与 ELF）。
5. **未在 Linux / ELF 上验证**：`tests/linux_desktop_regression.py` 在 Linux 网络命名空间对最新 ELF 实跑、Xorg/Wayland/XWayland 实机 Electron 联调、GTK 目录框、fcitx5/ibus 输入法与 2000 条历史性能，均已转入 `CRDocs/DEFERRED.md` 跟踪。

## 异议与理由

无异议。
