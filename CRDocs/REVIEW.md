# LC-FRIEND-REFRESH-001 — Round 2 独立复审

- **任务编号**：`LC-FRIEND-REFRESH-001`
- **任务主题**：好友刷新延迟、网卡切换与在线/离线分组（Round 2 返修复审）
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-24
- **审核状态**：**APPROVED**
- **依据**：`.claude/Rules.md`、`CRDocs/README.md`、`CRDocs/RULES.md`、`CRDocs/STATUS.md`、`CRDocs/REVIEW.md`（Round 1）、`CRDocs/DEFERRED.md`、`CRDocs/IMPLEMENTATION.md`（Round 2）、工作区完整 diff 与实际调用链。
- **独立复跑与验证边界**：
  - 审核者独立复跑并确认通过：
    1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`（退出码 0；原生 `csc.exe` 生产编译通过；隔离回环与 Win32 消息夹具 `LC-FR-001`、`LC-FR-002`、`LC-FR-003 / LC-FR-008`、`LC-FR-004` 四组检查全部通过）；
    2. 对本轮生成的 `LanChat.exe` 编译并运行 `tests/ConcurrentReceive.cs`（退出码 0，`Final ACK: 1, 1; Complete events: 2`，双同名文件 SHA-256 校验通过）；
    3. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`（退出码 0，0 警告 0 错误）；
    4. `C:\Program Files\nodejs\node.exe` 执行 `--check src/linux/desktop/renderer.js`、`--check tests/linux_desktop_ui_regression.js` 及 `tests/linux_desktop_ui_regression.js`（均退出码 0，输出 `linux desktop ui regression: ok`）；
    5. `git diff --check` 通过，全量 17 个改动/新增源码与测试文件编辑器行数复核通过。
  - **未验证边界（不构成实机验收）**：本轮所有验证均在 Windows 宿主机上完成（生产编译、回环隔离夹具、Win32 原生消息测试、Linux 后端宿主机构建、Node 模拟 DOM 与桩 IPC）。**尚未验证**真实多机 LAN、Wi-Fi/有线实际切网、DHCP 延迟、休眠唤醒、物理多网卡/TUN 路由及公开旧版（v2.0.18 / Python）跨机互通（`LC-FR-ENV-001`），**尚未验证** Linux/QEMU 网络命名空间实跑最新 ELF（`tests/linux_desktop_regression.py`）与真实 Linux Electron 桌面侧栏交互（`LC-FR-ENV-002`）。

## 结论

**APPROVED（当前范围放行）**。无 P0、无 P1 遗留。Round 1 阻断放行的全部 4 项 P1（`LC-FR-001` ~ `LC-FR-004`）均已按要求落地且经源码调用链与独立复跑回归确认闭环；4 项 P2（`LC-FR-005`、`LC-FR-006`、`LC-FR-008`、`LC-FR-009`）已同步闭环；`LC-FR-007` 已按规则明确写入 `CRDocs/DEFERRED.md` 并具备清晰触发阶段与验收条件。LANC 封包魔数、文本帧字段、`[系统自动握手]`、TCP `0x01` 确认字节与 UDP 六段竖线格式保持向后兼容；全部工作区改动文件均低于 1000 行软门禁，新增注释符合全中文规范。

## 七维复核摘要

| 维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. UDP ACK 与 TCP 握手并发归并/昵称/在线态 | PASS | `LC-FR-001` 与 `LC-FR-005` 闭环：`UpdatePeer` 在 `_peerLock` 内按 `(IPv4, TcpPort)` 归并同端点旧身份、累加未读并在锁外派发 `OnPeerLeft`；`TryTcpHandshake` 持 `_discoveryLock -> _peerLock` 校验快照、对象引用与端点独占性，拒绝为已移除或冲突端点续期；匿名 `expected == null` 探测不刷新已知真实身份；Linux `setPeers` 保留运行期与归档昵称 |
| 2. 刷新计时器/重复刷新/切网重绑/线程竞态与泄漏 | PASS | `LC-FR-002` 与 `LC-FR-006` 闭环：启动、改昵称、切网回调与手动刷新统一经 `RefreshPeers()` 先执行 `RebindInterfaces()`，可重试空 `_udpAnySocket` / 失败监听并复用健康快照；锁序严格为 `_discoveryLock -> _peerLock` 无死锁；`Dispose()` 已隔离反订阅异常；`SweepInterfaces` 睡眠后已先短路；`AsyncWaitHandle` 已用 `using` 释放 |
| 3. 非默认 TCP 端口与多网卡/TUN 路由刷新 | PARTIAL (DEFERRED) | 在线非默认端口复核、同 IP 异端口保留与同子网物理网卡优先成立；跨子网 TUN 回落通配与 Linux 运行期无历史离线地址属 P2，已准确登记至 `CRDocs/DEFERRED.md`（`LC-FR-007`），随 `LC-FR-ENV-001` / `LC-FR-ENV-002` 一并验收 |
| 4. WinForms 分组标题鼠标/键盘/右键/滚动/选择 | PASS | `LC-FR-003`、`LC-FR-004`、`LC-FR-008` 闭环：`ClearSessionItems()` 同步清空旧索引，`OnSelectedIndexChanged` 校验旧索引并双向查找非标题会话（全标题落 `-1`）；`WndProc` 低 32 位解包有符号坐标并在溢出时将右缘 12px 滑块热区直交 `OnMouseDown`/`OnMouseUp`（保留当前选择、拦截标题正文与无溢出热区、兼容 x64 负 Y）；`RenderSessionList` 校验 `TopIndex` 边界并在恢复选中后夹紧恢复视口锚点 |
| 5. Linux Electron 刷新入口位置与分组渲染 | PASS | `LC-FR-009` 闭环：`#refresh` 位于 `.sidebar-bottom` 最右（36px），`.session-heading` 显式声明 `font-weight: 700`，`onlineHeading` 状态完整；`tests/linux_desktop_ui_regression.js` 已覆盖入口位置、字重、空/非空分组渲染、过滤、键盘越过标题与刷新占位昵称保护 |
| 6. LANC 帧、TCP 握手与旧客户端兼容 | PASS | LANC 魔数、文本帧结构、`[系统自动握手]`、TCP `0x01` 与 UDP 六段格式未破坏；对端 TCP 握手后补发既有 UDP `ACK`；旧客户端仅回 `0x01` 时单一未冲突端点仍可正常复核，且已明确记录单字节确认在无新身份公告/冲突证据时无法区分同端点换 ID 的协议边界 |
| 7. 行数门禁与中文注释/文档要求 | PASS | 全部 17 个修改/新增源码与测试文件均 `< 1000` 行软门禁；最高为 `src/linux/desktop/renderer.js`（953 行，距 1000 行预警仅余 47 行，后续新增功能前须先按 SRP 拆分）与 `tests/linux_desktop_ui_regression.js`（935 行）；新增与调整注释均为中文 |

## Round 1 问题逐项复审结论

| 问题 ID | 严重度 | 结论 | 复核依据 |
| --- | :---: | :---: | --- |
| `LC-FR-001` | P1 | **RESOLVED** | `src/NetworkService.Discovery.cs:193-275, 505-529`：带 `announcedPort` 的身份公告按 `(IP, TcpPort)` 归并同端点旧节点并迁移未读，锁外派发 `OnPeerLeft`；同 IP 异端口保留；迟到 TCP `0x01` 复核快照、`current == expected` 与端点无其他身份冲突后才续期；`tests/friend_refresh_regression.ps1` 独立复跑通过 |
| `LC-FR-002` | P1 | **RESOLVED** | `src/NetworkService.Discovery.cs:293-319`、`src/NetworkService.Interface.cs:76-113`、`src/NetworkService.cs:168-190`：所有刷新入口统一走 `RefreshPeers()` -> `RebindInterfaces()`，覆盖漏事件、延迟获取与瞬时绑定失败，不变网卡零重建复用快照；夹具独立复跑通过 |
| `LC-FR-003` | P1 | **RESOLVED** | `src/UI/Controls/SessionListBox.cs:39-44, 327-361`、`src/MainForm.Chat.cs:24`：重建清空 `_previousSelectedIndex`，跨标题前校验旧索引范围与非标题性，主方向无有效行时反向查找，仅标题时落 `-1`；缩表 `Home`/`Down`、末尾标题 `End` 与纯标题场景夹具独立复跑通过 |
| `LC-FR-004` | P1 | **RESOLVED** | `src/UI/Controls/SessionListBox.cs:87-108, 226-233, 255-311`：`unchecked((int)m.LParam.ToInt64())` 解析有符号 16 位 `x`/`y`，溢出时右缘 12px 滑块热区直交 `OnMouseDown`/`OnMouseUp` 不篡改当前选择，无溢出与正文区标题点击继续拦截；原生 Win32 消息与 x64 负 Y 坐标夹具独立复跑通过 |
| `LC-FR-005` | P2 | **RESOLVED** | `src/linux/desktop/renderer.js:57-67`：`peer.id === peer.ip && previous?.nick` 时保留运行期及归档昵称，不回退为 `"好友 (<ip>)"`；Node UI 回归独立复跑通过 |
| `LC-FR-006` | P2 | **RESOLVED** | `src/NetworkService.cs:233-239`（反订阅 `try/catch` 保护）、`src/NetworkService.Discovery.cs:426-427`（睡眠后先检停机与快照切换）、`484-488`（`using (var connectWait = ar.AsyncWaitHandle)`） |
| `LC-FR-007` | P2 | **DEFERRED** | 已在 `CRDocs/DEFERRED.md` 准确登记跨子网 TUN 回落与 Linux/切网无历史离线地址现状、触发阶段（`LC-FR-ENV-001` / `LC-FR-ENV-002`）及验收条件，不阻断当前切片 |
| `LC-FR-008` | P2 | **RESOLVED** | `src/MainForm.Chat.cs:18-40` 与 `src/UI/Controls/SessionListBox.cs:46-53`：校验 `TopIndex` 边界，在恢复 `SelectedIndex` 后通过 `RestoreScrollAnchor` 夹紧恢复视口锚点；选中群聊向下浏览后重建夹具复跑通过 |
| `LC-FR-009` | P2 | **RESOLVED** | `src/linux/desktop/desktop.css:75` 显式声明 `font-weight: 700`，`src/linux/desktop/renderer.js:98` 同步置 `onlineHeading = true`，`tests/linux_desktop_ui_regression.js` 已补齐入口位置、字重、分组、过滤、键盘跳标题与刷新昵称保留断言 |
| `LC-FR-ENV-001` | — | **DEFERRED** | 保留在 `CRDocs/DEFERRED.md`，待真实多机 LAN、切网与多网卡/TUN 路由验收 |
| `LC-FR-ENV-002` | — | **DEFERRED** | 保留在 `CRDocs/DEFERRED.md`，待 Linux/QEMU 网络命名空间实跑最新 ELF 与真实 Linux Electron 桌面验收 |

---

# LC-FRIEND-REFRESH-001 — Round 1 独立审核

- **任务编号**：`LC-FRIEND-REFRESH-001`
- **任务主题**：好友刷新延迟、网卡切换与在线/离线分组
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-24
- **审核状态**：**CHANGES_REQUESTED**
- **依据**：`.claude/Rules.md`、`CRDocs/README.md`、`CRDocs/RULES.md`、`Docs/GrokBot_UI_Design_System.md`、`CRDocs/IMPLEMENTATION.md` 顶部范围与工作区全部 14 个源码文件及完整 diff。
- **验证边界**：
  - 审核者独立复跑并确认通过：Windows 原生 `csc.exe` 零错编译（临时输出至 `%TEMP%` 后清理，未改动 `release/`）、Linux 共享后端宿主机构建 `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`（0 警告 0 错误）、`node --check src/linux/desktop/renderer.js` 与 `node tests/linux_desktop_ui_regression.js`（退出码 0，输出 `linux desktop ui regression: ok`）、全部 14 个修改文件行数统计与 `git diff --check`；并通过只读反射脚本对当前编译产物 `bin/friend-refresh/LanChat.exe` 中的 `SessionListBox` 实测复现了 `LC-FR-003` 与 `LC-FR-004`。
  - **未在真实环境验收**：未在真实多机 LAN、Wi-Fi/有线切网、物理多网卡/TUN 路由环境验收，未在 Linux/QEMU 运行最新 ELF（`tests/linux_desktop_regression.py`），亦未在 Linux Electron 桌面实机验收。宿主机编译、Windows 回环夹具与 Node DOM 模拟均不构成真实 LAN 或 Linux 实机验收。

## 结论

**CHANGES_REQUESTED（打回整改）**。LANC 封包与 TCP `0x01` 确认字节保持向后兼容，UDP 多网卡快照与锁序（`_discoveryLock -> _peerLock`）无死锁，修改文件行数全部低于 1000 行软门禁，新增注释与文档满足中文规范。但存在 **4 项 P1 阻断放行问题**：

1. 对端无 `BYE` 重启（同 IP 同端口新 `LocalId`）后刷新时，TCP 握手 `0x01` 误为已失效旧节点续命，导致幽灵节点滞留、人数翻倍、群聊重复发送且昵称/状态回退；
2. 手动刷新 `RefreshPeers()` 未触发 `RebindInterfaces()`，在换网延迟/DHCP 慢于防抖窗口、瞬时绑定失败或 `NetworkChange` 异常/漏事件后，点击刷新按钮无法恢复网卡绑定；
3. `SessionListBox.OnSelectedIndexChanged` 使用未随列表重置同步的 `_previousSelectedIndex`，搜索筛选缩小列表后按键盘方向键会直接抛出未处理 `ArgumentOutOfRangeException` 崩溃，或卡死在分组标题选中态（已实测复现）；
4. `SessionListBox.WndProc` 拦截 `WM_LBUTTONDOWN` 标题行点击时未排除右侧 12px 浮动滑块拖拽热区，导致在「在线」「离线」标题高度上无法拖拽自绘滚动条（违反 Zero White Scrollbar 交互契约，已实测复现）。

## 七维核对摘要

| 维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. UDP ACK 与 TCP 握手并发归并/昵称/在线态 | FAIL | 存在 `LC-FR-001`（P1，重启对端幽灵节点误续命）；另有 `LC-FR-005`（P2，Linux 端未阻止 TCP 地址占位名覆盖已知/归档昵称） |
| 2. 刷新计时器/重复刷新/切网重绑/线程竞态与泄漏 | FAIL | 锁序一致、无死锁、旧快照探针有隔离，但存在 `LC-FR-002`（P1，手动刷新不重绑网卡）；另有 `LC-FR-006`（P2，`Dispose` 反订阅未保护、`SweepInterfaces` 睡眠后未先短路、`AsyncWaitHandle` 未及时释放） |
| 3. 非默认 TCP 端口与多网卡/TUN 路由刷新 | PARTIAL | 在线非默认端口复核与同子网物理网卡优先成立；跨子网 + TUN 时 `GetDiscoverySocket` 回落 `0.0.0.0` 且 Linux/换网未保留运行期无历史离线地址（见 `LC-FR-007`，P2） |
| 4. WinForms 分组标题鼠标/键盘/右键/滚动/选择 | FAIL | 右键过滤与 `GetScrollMetrics` 动态行高计算正确，但存在 `LC-FR-003`（P1，搜索缩表后键盘选行崩溃/卡死标题）与 `LC-FR-004`（P1，标题高度阻断滑块拖拽）；另有 `LC-FR-008`（P2，`top <= index` 导致选中群聊向下浏览时刷新跳顶） |
| 5. Linux Electron 刷新入口位置与分组渲染 | PARTIAL | `#refresh` 移至底栏最右（36px 圆形）与 `.session-heading`（32px）符合结构与设计规范；但缺少针对新入口/分组标题的 UI 回归断言，且 `.session-heading` 字重隐式依赖弹窗 `h2` 规则（见 `LC-FR-009`，P2） |
| 6. LANC 帧、TCP 握手与旧客户端兼容 | PASS | LANC 魔数、文本帧字段、`[系统自动握手]`、`0x01` 确认字节与 UDP 6 段竖线格式完全兼容；TCP 握手后补发既有 UDP `ACK` 对旧客户端无害且可向上兼容（旧客户端仅 TCP 回包时 Linux 占位昵称覆盖见 `LC-FR-005`） |
| 7. 行数门禁与中文注释/文档要求 | PASS | 14 个修改文件全部 `< 1000` 行（最高 `src/linux/desktop/renderer.js` 945 行，距软门禁余 54 行需留意）；新增注释与 XML 文档均为中文（仅 `NetworkService.Discovery.cs` 首行带入 UTF-8 BOM，非阻断） |

## P1（本轮必须修复）

### LC-FR-001 [P1 / OPEN] 对端无 BYE 重启（同 IP 同端口新 LocalId）时，刷新 TCP 握手误为旧幽灵节点续命

- **位置**：`src/NetworkService.Discovery.cs:221-259` (`UpdatePeer`)、`350-375` (`CheckRefreshedPeers`)、`495-512` (`TryTcpHandshake`)。
- **复现条件与现象**：
  1. 对端节点 `old-guid`（如 `10.0.0.5:38889`，昵称 `OldNick`）处于在线列表；
  2. 对端崩溃或无 `BYE` 重启，以相同 IP 和默认 TCP 端口 `38889` 启动新实例 `new-guid`（昵称 `NewNick`）；
  3. 本机点击手动刷新：首轮 UDP `HELLO` 收到 `new-guid` 的 UDP `ACK`，`UpdatePeer` 只移除 `addressId`（`"10.0.0.5"`）而不清理同 `(IP, TcpPort)` 的 `old-guid`，此时 `_peers` 同时存在 `old-guid`（旧 `LastSeen`）和 `new-guid`（新 `LastSeen`）；
  4. 300ms 后 `CheckRefreshedPeers` 筛选 `LastSeen < started` 命中 `old-guid`，随即对其执行 `TryTcpHandshake(10.0.0.5, 38889, interfaces, expected: old-guid)`；
  5. 新实例 `new-guid` 在 `38889` 接受连接并返回 `0x01`（TCP 确认帧不含对端 ID），`TryTcpHandshake` 第 507-509 行校验 `expected.Id`/`IP`/`TcpPort` 通过后直接写入 `old-guid.LastSeen = DateTime.UtcNow`；
  6. `RemoveExpiredPeers(peers, started)` 见 `old-guid.LastSeen >= started` 放弃移除。
- **影响**：已死亡的 `old-guid` 被错误续命，每次刷新都会重复续命；`GetActivePeers().Count` 虚增为 2（群聊人数翻倍、群聊消息向同一好友重复发送两次）；只要 `old-guid.LastSeen` 晚于 `new-guid` 的 UDP `ACK` 时间戳（或 UDP 晚到/丢包），WinForms（`SessionPresenter.cs:158`）和 Linux（`DesktopService.cs:315` + `renderer.js:59`）会优先展示已失效的 `old-guid` 及其旧昵称 `OldNick`。
- **修复要求**：同一 IPv4 + 同一 TCP 监听端口不能同时存在两个活跃 LanChat 实例。在 `UpdatePeer` 收到带 `announcedPort.HasValue` 的真实身份（`peerId != addressId`）时，应归并/剔除同 `(IP, TcpPort)` 的其他旧 `peerId`（迁移未读并在锁外通知 `OnPeerLeft`）；同时 `TryTcpHandshake(..., expected)` 在同 `(IP, port)` 已被其他活跃 `peerId` 占用时不得为 `expected` 续命。

### LC-FR-002 [P1 / OPEN] 手动刷新 `RefreshPeers()` 未触发网卡重绑 `RebindInterfaces()`

- **位置**：`src/NetworkService.Discovery.cs:287-311` (`RefreshPeers`)、`src/NetworkService.Interface.cs:67-116` (`OnNetworkAddressChanged` / `RebindInterfaces`)、`src/NetworkService.cs:177-185`。
- **复现条件与现象**：
  - `RebindInterfaces()` 仅在 `Start()` 和 `NetworkChange.NetworkAddressChanged` 的 250ms 防抖回调中被调用，`RefreshPeers()` 直接使用缓存的 `_activeInterfaces`。
  - 当真实切网/休眠唤醒时 DHCP 获取 IPv4 晚于 250ms 防抖点、切网瞬间 `BindUdpListener` 瞬时失败导致 `adapter.UdpSocket == null`、用户在 250ms 防抖窗口内提前点击手动刷新，或 `NetworkChange` 订阅在特定环境下抛异常降级时，`_activeInterfaces` 会长期停留在失效快照上，用户反复点击“刷新好友列表”按钮也无法重新绑定新网卡套接字。
- **修复要求**：在 `RefreshPeers()` 获取 `interfaces` 前调用 `RebindInterfaces()`（该函数在网卡无变化时通过 `if (!changed) return;` 零重建直接返回，且切网通知回调可直接复用，确保手动刷新始终能兜底恢复新网卡与失败的 UDP 绑定）。

### LC-FR-003 [P1 / OPEN] `SessionListBox.OnSelectedIndexChanged` 残留越界 `_previousSelectedIndex`，搜索缩表后键盘选行直接崩溃或卡死在分组标题

- **位置**：`src/UI/Controls/SessionListBox.cs:289-299`、`src/MainForm.Chat.cs:18-39`。
- **实测复现证据**（已在 `bin/friend-refresh/LanChat.exe` 的 `LanChat.SessionListBox` 上复现）：
  1. **崩溃路径**：列表含 5 项 `[群聊(0), 在线(1), PeerA(2), 离线(3), PeerB(4)]`，选中 `PeerB`（`_previousSelectedIndex = 4`）。用户输入搜索词仅命中 `PeerA`，`RenderSessionList()` 调用 `Items.Clear()` 并重建为 2 项 `[在线(0), PeerA(1)]`；因当前会话 `PeerB` 不在过滤结果中，`index = -1`，`SelectedIndex` 为 `-1`，而 `Items.Clear()` 不会触发 `OnSelectedIndexChanged`，`_previousSelectedIndex` 仍为 `4`。焦点进入列表后按 `Down`/`Home` 落到索引 `0`（分组标题「在线」），`direction = (0 > 4) ? 1 : -1` 得 `-1`，`current` 减至 `-1`，随后执行 `SelectedIndex = _previousSelectedIndex`（即 `4`），抛出未处理异常 **`ArgumentOutOfRangeException: InvalidArgument=Value of '4' is not valid for 'SelectedIndex'`**。
  2. **标题卡死路径**：原选中 `群聊`（`_previousSelectedIndex = 0`），搜索仅命中 `PeerB` 重建为 `[离线(0), PeerB(1)]` 后按 `Down`/`Home`，`direction = (0 > 0) ? 1 : -1` 得 `-1`，回退 `SelectedIndex = 0`，结果分组标题 `GroupHeading.Offline` 保持选中态（实测 `SelectedIndex=0 Item=HEAD:Offline`），无法跳至 `PeerB`。
  3. **尾部边界**：无离线好友时列表末尾为「离线」标题，按 `End`/`PageDown` 落到末尾标题后 `direction = +1` 越界，回跳旧项而非停在最后一个有效会话。
- **修复要求**：不得将未经合法性校验的 `_previousSelectedIndex` 直接写回 `SelectedIndex`。当 `IsHeading(current)` 时：先校验 `_previousSelectedIndex` 是否处于 `[0, Items.Count)` 且非标题；主方向（若 `_previousSelectedIndex` 无效则优先向下 `+1`）找不到有效会话时，反向扫描寻找最近非标题会话；仅当整个列表无任何非标题项时设为 `-1`。列表清空重建后也应同步重置/夹紧 `_previousSelectedIndex`。

### LC-FR-004 [P1 / OPEN] `SessionListBox.WndProc` 拦截标题行左键点击时吞掉右侧浮动滑块拖拽热区

- **位置**：`src/UI/Controls/SessionListBox.cs:71-76, 214-229`。
- **实测复现证据**（已在 `bin/friend-refresh/LanChat.exe` 上向溢出列表的滑块热区 `(x=295, y=80)`（宽 300、位于第 1 项标题行 `y=72..104` 高度）发送 `WM_LBUTTONDOWN`，实测 `_isThumbDragging == False`）：
  - `WndProc` 在 `base.WndProc`（触发 `OnMouseDown`）之前拦截 `WM_LBUTTONDOWN`，并仅依据 `IsHeading(IndexFromPoint(x, y))` 直接 `return`。由于 `IndexFromPoint` 覆盖控件全宽（含最右 12px 浮动滑块区域），鼠标按住滑块与「在线」「离线」标题重合的纵坐标区段时，点击被直接吞掉，无法启动滚动条拖拽，违反 `.claude/Rules.md` 第 9 条 Zero White Scrollbar 的拖拽交互契约。
  - 另第 74 行 `m.LParam.ToInt32()` 在 64 位进程遇到负 Y 坐标时会溢出，宜改为 `unchecked((int)m.LParam.ToInt64())`。
- **修复要求**：在 `WndProc` 的 `WM_LBUTTONDOWN` 标题行拦截条件中，若列表存在溢出（`maxTop > 0`）且点击 X 落在右侧滑块热区（`x >= ClientSize.Width - 12`），应放行给 `base.WndProc` -> `OnMouseDown` 处理滑块拖拽；仅对非滑块热区的标题行点击做拦截。

## P2（建议返修一并处理或登记延期）

### LC-FR-005 [P2 / OPEN] Linux 端未阻止 TCP 地址占位记录覆盖已知/归档好友昵称

- **位置**：`src/linux/LanChat.Linux/DesktopService.cs:315-320`、`src/linux/desktop/renderer.js:57-62`。
- **说明**：`IMPLEMENTATION.md` 声明“地址占位记录不覆盖已知昵称”，Windows 端已在 `SessionPresenter` 保护，但 Linux 端 `PeerList()` 原样输出占位名 `"好友 (<ip>)"`，`renderer.js` 的 `setPeers` 会直接覆盖前端已有的归档/运行期真实昵称。在 UDP 丢包或与旧客户端（TCP 握手不回 UDP `ACK`）互通时会表现为昵称被回退成 `"好友 (<ip>)"`。建议在 `DesktopService.PeerList()` 或 `renderer.js` 中对 `peer.id === peer.ip` 的占位记录保留已知/归档昵称。

### LC-FR-006 [P2 / OPEN] `Dispose()` 反订阅 `NetworkAddressChanged` 缺异常保护，且 `SweepInterfaces` / `TryTcpHandshake` 存在轻微资源清理瑕疵

- **位置**：`src/NetworkService.cs:177-185, 235-245`、`src/NetworkService.Discovery.cs:418-425, 474-476`。
- **说明**：
  1. `Start()` 中先赋值 `_networkChangeTimer = new Timer(...)` 再订阅 `NetworkChange.NetworkAddressChanged +=`；若订阅抛异常，`_networkChangeTimer` 非空，后续 `Dispose()` 未包裹 `try/catch` 的 `-=` 若也抛异常会中断 `_presenceCheckTimer`、`BYE` 与全部套接字释放。建议仅在订阅成功后保留状态或在 `Dispose()` 中做隔离保护。
  2. `SweepInterfaces` 在 `Thread.Sleep(300)` 后应先检查 `if (!_isRunning || interfaces != _activeInterfaces) return;`，避免停机/切网后仍启动最多 32 个空转线程。
  3. `TryTcpHandshake` 建议用 `using (ar.AsyncWaitHandle)` 及时释放等待句柄。

### LC-FR-007 [P2 / OPEN] 跨子网 + TUN 场景下 UDP 探测回落 `0.0.0.0`，且运行期无历史离线好友未被纳入刷新单播

- **位置**：`src/NetworkService.Discovery.cs:138-153, 298-300`、`src/linux/LanChat.Linux/DesktopService.cs:312-313`。
- **说明**：
  1. `GetDiscoverySocket` 在目标不在任一本地子网时直接回落 `_udpAnySocket`（`0.0.0.0`），与 TCP 侧 `GetLocalEndpointForTarget`（优先回落首选物理网卡以穿透 Metric 0 TUN）不一致，跨网段路由局域网开启全局 TUN 时 UDP 单播可能被 TUN 劫持。
  2. Linux `DesktopService.KnownAddresses()` 仅读取有聊天记录的归档会话键，不包含运行期见过但未发消息的离线好友（与 Windows `SessionPresenter.GetKnownAddresses()` 不对称）；且 `NetworkService` 在切网 `RefreshPeers(null)` 时也不包含上次显式刷新之后才发现的新好友 IP。

### LC-FR-008 [P2 / OPEN] `RenderSessionList()` 的 `top <= index` 限制导致选中群聊向下浏览离线好友时刷新跳回顶部

- **位置**：`src/MainForm.Chat.cs:18, 37`。
- **说明**：选中顶部会话（如索引 `0` 的群聊）并向下滚动浏览离线好友时，若点击刷新或收到后台 `OnPeerUpdated`，重设 `SelectedIndex = 0` 会将列表滚回顶部，而第 37 行 `top <= index` 阻止恢复原 `TopIndex`，造成视口跳回顶部。另第 18 行读取 `_listSessions.Items[_listSessions.TopIndex]` 建议补齐 `TopIndex >= 0 && TopIndex < Items.Count` 边界保护。

### LC-FR-009 [P2 / OPEN] Linux 分组标题与刷新按钮缺少自动化回归用例，且 `.session-heading` 字重隐式耦合弹窗 `h2`

- **位置**：`src/linux/desktop/desktop.css:75, 167`、`src/linux/desktop/renderer.js:70, 91`、`tests/linux_desktop_ui_regression.js`。
- **说明**：
  1. `desktop.css:75` 的 `.session-heading` 应显式声明 `font-weight: 700`，避免隐式依赖文件末尾第 167 行面向弹窗的 `h2` 样式；
  2. `renderer.js:91` 追加「在线」标题后建议同步置 `onlineHeading = true` 保持状态完整；
  3. `tests/linux_desktop_ui_regression.js` 建议补充对 `#refresh` 位于 `.sidebar-bottom`、无过滤/有过滤时「在线」「离线」分组标题渲染、以及键盘上下键跳过标题的断言；
  4. `src/linux/desktop/renderer.js` 当前 945 行，距 1000 行软门禁仅余 54 行，后续再向该文件追加新功能前须按 `.claude/Rules.md` 第 10 条先拆分。

## 真实环境验收项（已迁入 `CRDocs/DEFERRED.md`）

| 问题 ID | 状态 | 说明 |
| --- | :---: | --- |
| `LC-FR-ENV-001` | DEFERRED | 真实多机 LAN、Wi-Fi/有线切网、非默认端口与物理多网卡/TUN 路由下的刷新延迟、重绑与旧版互通实测 |
| `LC-FR-ENV-002` | DEFERRED | Linux/QEMU 网络命名空间实跑最新 ELF（`tests/linux_desktop_regression.py`）及 Linux Electron 实机侧栏刷新按钮与分组渲染验收 |

---

# LC-LINUX-002-STEP-3-SLICE-2 — Round 2 复审归档

> 审核者：Grok（经用户确认审核结论已通过，由执行者按规范将状态与延期项分离归档）
> 日期：2026-11-23
> 结论：**APPROVED**。本轮 P1 已全部闭环，可延期项与跨平台实机验收项已迁入 `CRDocs/DEFERRED.md`。

| 问题 ID | 严重度 | 状态 | 结论 |
| --- | --- | --- | --- |
| LC-S3-001 | P1 | RESOLVED | 外观四项已按 WinForms `AppSettings` 键名落盘，即时作用于主窗与设置窗并重启保持；通用页与外观页字号联动；10 个展示项继续只展示、无新 RPC 且后台驻留保持空回调 |
| LC-S3-002 | P1 | RESOLVED | `histories-cleared` 已作废进行中的 `historyLoads`，晚到的成功或失败 `history` 响应不再回写已清空气泡或覆盖状态 |
| LC-S3-003 | P2 | DEFERRED | `ChatHistoryService.ClearAllHistories()` 删文件异常被吃掉（与 WinForms 一致），暂不阻断本切片 |
| LC-LINUX-002-ENV-001 | — | DEFERRED | Linux 网络命名空间对最新 ELF 实跑 `tests/linux_desktop_regression.py` 待在 Linux/QEMU 执行 |
| LC-LINUX-002-GUI-001 | — | DEFERRED | Xorg / Wayland / XWayland 实机 Electron 联调、输入法与缩放叠图待在 Linux 桌面环境执行 |

---

# LC-LINUX-002-STEP-3-SLICE-2 — Round 1 独立审核（历史保留）

- **任务编号**：`LC-LINUX-002-STEP-3-SLICE-2`
- **任务主题**：Linux Electron 桌面设置八页对齐
- **审核方**：Grok 独立审核方
- **审核日期**：2026-11-23
- **审核状态**：**CHANGES_REQUESTED**（原记录为 **REJECTED**）
- **依据**：`.claude/Rules.md` 第 1、4、6、7、8、9、10 条；`PlanDocs/LC-LINUX-002/BASELINE.md`、`INTEGRATION.md`；`Docs/GrokBot_UI_Design_System.md`；`src/UI/Controls/SettingsPanels/` 下 8 个面板。
- **验证边界**：审核对照的是工作区源码。Node UI 回归、Python 语法检查、双架构 ELF 编译和 Edge Headless 1260×820 测量按送审材料采信为开发者自测，本次审核没有重跑。这些证据不能代替 X11/Wayland，也不能代替 Linux 网络命名空间里的 `delete`、`history`、`clear-all-histories`。

## 结论

不放行。设置窗拆分、IPC 白名单、昵称/自动接收/目录/只读备用确认，以及未跑 Linux 的证据边界，这几项成立。外观页被写成 Windows 未接线，与 WinForms 源码和基线不符。清空记录与在途历史加载有竞态。

## P1

### LC-S3-001 [P1 / OPEN -> Round 2 RESOLVED] 外观四项在 Windows 已接线，Linux 只改本窗内存

`AppearanceSettingsPanel.ApplySettings()` 会 `AppSettings.Save()` 并调用 `OnAppearanceChanged()`。主题、强调色、界面/聊天字体族、两套字号会立即生效，重启仍在。通用页字号滑块同样写入 `AppSettings.UiFontSize`。`BASELINE.md` 对 `settings-3` 的验收是「即时变化与重启保持」。

Linux 的 `display.themeIndex`、`accentIndex`、字体下标和 `uiSize`/`chatSize` 只活在这次设置窗进程里。滑块和下拉能拨动，不写配置，不改主窗，关掉再开回到写死默认值。`settings.js` 与 `INTEGRATION.md` 把这些和语言、链接打开方式放在一类，与 `AppearanceSettingsPanel.cs` 不符。

语言、链接打开方式、协议通道、快捷键徽章、托盘预览、后台驻留、分块校验、自动加载、TUN 过滤、单播探测，在 Windows 是空回调或只改按钮文字。这些继续保持展示态，不要新造 RPC。

### LC-S3-002 [P1 / OPEN -> Round 2 RESOLVED] 清空之后，在途历史响应能把气泡填回去

`DesktopService` 的 `clear-all-histories` 会 `_sessions.ClearAllHistories()` 并广播 `histories-cleared`，与服务端一致。主窗处理只做 `messages.clear()`，没有作废 `historyLoads`。`loadHistory()` 在 `await api.history()` 返回后，只要操作令牌还在，就会 `messages.set` 并重画。清空发生在这次往返中间时，界面会先空再出现旧记录。

## P2（Round 1 登记，已于 Round 2 修复或转入 DEFERRED）

- 反馈框 `<textarea>` 和「附带诊断」原生复选框没有接 `scrollbar.js`，也没有关掉系统滚动条。长文本会露出原生滑块。
- `computeSettingsBounds` 最小可到 480×400，窗口 `minWidth/minHeight` 是 720×500，且使用 `useContentSize`。小工作区上，夹紧结果会被最小尺寸和标题栏顶破。
- `tests/linux_desktop_ui_regression.js` 对只读备用确认只检查源码是否包含 `overridePath` 和「使用备用位置」，没有执行确认/取消分支。
- 送审稿与代码不一致：昵称上限是 1～80，与 `LinuxProfile` 一致，不是 1～32；检查更新文案是「将在后续维护切片接入」，不是「当前已是最新版本」。
- 快捷键页只写 `Shift + Enter` 换行。主窗实际是 Enter 发送，Ctrl+Enter 和 Shift+Enter 都能换行。
- 选了与当前相同的历史目录时，配置层会跳过复制，界面仍提示「聊天记录已迁移并保存设置」。`ChatHistoryService.ClearAllHistories()` 删除失败会被吃掉，界面仍显示成功；这点和 WinForms 相同。
