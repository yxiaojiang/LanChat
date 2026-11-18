# 审核

**LC-LINUX-001 · 第 1 轮 · 审核方 Claude · 2026-11-18**

审核对象为 [第 1 轮提案](PROPOSAL-R1.md)。结论为 `CHANGES_REQUESTED`。首批修复的方向成立，提案列出的 8 项故障均已在本机独立复现。需要修订的原因有三类：一是步骤 1 漏了 3 处会改变修复内容的事实；二是 CLI 产物链已经断开；三是步骤 3 的 Tk 可行性和规则相容性范围比提案写的更大，其中有几项需要用户决定。

## 本轮核对了什么

**基线与范围。** 工作区 `main` 位于 `a0e49c1`（`release: 发布 v2.0.16`），未提交的改动只在 `PlanDocs/`。按 [项目规则](../../.claude/Rules.md) 读取了 `SessionBrief.md`、`Plan.md`、`contexts/Linux.md`、`contexts/Protocol.md`，并读取了 [GrokBot 设计规范](../../Docs/GrokBot_UI_Design_System.md) 中涉及颜色、字号、输入胶囊和圆角的条目。

**源码。** 通读了 [`src/linux/lanchat.py`](../../src/linux/lanchat.py)，共 1696 行。对照了 [`NetworkService.Transfer.cs`](../../src/NetworkService.Transfer.cs) 的帧读写和超时，以及 [`NetworkService.Discovery.cs`](../../src/NetworkService.Discovery.cs) 中 `AddOrUpdatePeer` 与 UDP 地址的选择逻辑。还核对了 [`SessionManager.cs`](../../src/Services/SessionManager.cs) 中 `GetLanKey` 的写法，[`ChatHistoryService.cs`](../../src/Services/ChatHistoryService.cs) 的 JSONL 字段和文件命名，[`MainForm.Files.cs`](../../src/MainForm.Files.cs) 的群聊选人入口，以及 [`WeChatIcon.cs`](../../src/UI/Renderers/WeChatIcon.cs) 的接口和画笔线宽。

**产物与脚本。** 用 Python 解析 `release/LanChat.deb`、`lanchat_2.0.16_amd64.deb` 和 `lanchat_2.0.16_all.deb` 的 ar 与 tar 结构，比对内含 `lanchat.py` 的 SHA-256，读取 control 字段和文件清单。核对了 `release/linux-x64`、`release/linux-arm64` 两份 ELF 的时间戳，共享网络源码的提交历史，以及 `packaging/build-all.bat`、`packaging/build-deb.py`、`src/linux/LanChat.Linux/build-linux.bat` 和 `Tools/Publish.bat` 的构建路径。

**实测环境。** Windows 10 19045，Python 3.13.14，Tk 8.6.15，`tk scaling` 为 1.33。把 `lanchat.py` 复制到临时目录后加载，并把 `HOME`/`USERPROFILE` 重定向到临时目录，避免往仓库写入便携配置。测试不绑定 38888/38889，只在 `127.0.0.1` 的临时端口上做 TCP 探测，并驱动真实的 `WeChatGui` 对象。**本轮没有 Linux 桌面，也没有第二台物理设备。** 下文所有 Tk 结论都来自 Windows 上的 Tk，Linux 上的界面表现一律列为待核实。

## 属实

以下 8 项故障与提案一致，均已复现：

| 提案所述 | 源码位置 | 本机复现结果 |
| --- | --- | --- |
| 系统消息触发 `KeyError: 'is_self'` | `_render_message_item` 先取 `item["is_self"]`，之后才判断 `is_sys`（第 1142–1148 行） | 窗口首次 `<Configure>` 即抛错两次，直接调用 `_redraw_chat_history` 也抛错。**补充**：抛错后聊天记录控件停在 `state="normal"`，用户可以直接往历史区里打字。因此群聊在**启动时**就是空白的，不只是切回群聊后才出问题。 |
| 「会话」按钮无回调 | 第 705–710 行没有 `command` | `cget("command")` 为空 |
| 群聊文件入口中断 | 群聊右键菜单调用 `_on_send_file`，它在 `BROADCAST` 下直接弹出提示后返回（第 1293–1295 行） | 读码确认 |
| 同名联系人的文件归错 | `on_file_received` 只传昵称，`_handle_file_recv` 按昵称取第一个匹配（第 1262–1271 行） | 两位同名联系人 p1、p2 时，文件固定归入 p1；没有匹配时归入**当前打开的会话** |
| 失败只写日志 | GUI 丢弃了 `send_text_message` 的返回值；`send_file` 拒收时直接 `return`，最终确认 `s.recv(1)` 的结果没有检查（第 559–581 行） | 拒收时发送端**没有任何进度或结束事件**。接收端读完数据后不回确认就断开，发送端仍发出 `(…, 200000, 200000, 0, False)`，即被当作成功。 |
| 长会话列表不可达 | `sessions_container` 是没有滚动能力的 `tk.Frame` | 确认 |
| 自动接收显示相反 | 按钮文字写死为「开」（第 864–869 行） | 设置为 `false` 时显示「自动接收: 开」。准确说法是**初值写死**，切换一次后显示正确。 |
| 重启后没有私聊历史 | 历史只存在内存的 `histories` 里，键为每次启动重新生成的 `uuid4` | 确认，群聊历史同样不落盘 |

**TCP 短读。** 把魔数分成 `LA` 和 `NC` 两段、间隔 0.3 秒发送，Python 端读到 2 字节后直接关闭连接，没有 ACK，也没有投递消息；同一帧整包发送则正常。Python 的 `recv(1)[0]` 和 `struct.unpack("<q", recv(8))` 也有同样的短读问题。C# 端用 `BinaryReader.ReadBytes`/`ReadInt64` 循环读满，不受影响。

**安装产物。** 三个 `.deb` 内的 `/usr/lib/lanchat/lanchat.py` 与源码 SHA-256 一致（`78d1c4e8…`），control 中为 `Depends: python3`、`Recommends: python3-tk`。amd64 包另带 `/usr/lib/lanchat/lanchat-bin`，all 包不带 ELF。

**C# CLI。** `LanChat.Linux.csproj` 为 `Exe`，目标框架 `net9.0`，通过 `Compile Include` 链接了 `Models.cs` 与四个 `NetworkService*.cs`。

**Windows 基线。** `GetLanKey` 以 `LAN-<IPv4>` 为私聊键，取不到地址时才退回节点 ID。历史文件为 `history_BROADCAST.jsonl` 和 `history_peer_<键>.jsonl`。群聊文件经 `PromptPeerAndSendFiles` 选人。注意 Windows 选人框本身是原生 `Form`+`ListBox`，只能作为**行为**基线，不能作为视觉基线。

**设计取值。** 提案引用的 `#0E0E0E`、`#121212`、`#202024`、输入胶囊 64 px / 圆角 32 px、附件钮 36 px、发送钮 44 px、正文 11 pt，均与设计规范第 50–53、139–153 行一致。

## 不成立或说重了

1. **「Windows 宿主 Tk 交互测试」不能支撑任何视觉或输入法结论。** Windows 上的 Tk 由 GDI 绘制，使用 Windows 的输入法框架和字体，这与 Linux 的 X11/XIM/fontconfig 是两套实现。这些测试可以证明逻辑故障，但步骤 3 关于圆角、抗锯齿、中文输入的可行性不能引用它们。提案正文没有这样引用，但「证据」表把它和逻辑故障并列，修订时应写明适用边界。
2. **「X11/Wayland」的说法需要收窄。** Tk 8.6 没有原生 Wayland 后端，在 Wayland 会话下一律通过 XWayland 运行。验收时应记录 `XDG_SESSION_TYPE` 并确认是否经由 XWayland，同时关注分数缩放下的模糊问题。这里不存在需要单独验证的「Tk 的 Wayland 后端」。
3. **群聊选人「文案与 Windows 一致」可以，但不应照搬 Windows 的实现。** Windows 的选人框是原生 `ListBox`，按 [规则第 9 条](../../.claude/Rules.md) 本身就会带出白底滚动条。Linux 端应对齐选人行为（无人在线时提示，单人时直发，多人时选择），控件按规则自绘。

## 遗漏

**R1 · 收到文字后，对端的端口和主机名被改写（影响步骤 1、2）。** `_handle_tcp_client` 每收到一条文字，就调用 `add_or_update_peer(sender_id, nick, "LAN-Host", addr[0], TCP_BASE_PORT)`（第 326 行），并且无条件覆盖已有的 `port` 和 `host`。实测把 peer 的端口设为 38895，收到一条文字后变成 `LAN-Host`/`38889`，此后的回复会发往错误端口。C# 第 90 行同样传入 `TCP_BASE_PORT`，`AddOrUpdatePeer` 在端口不同时也会覆盖，所以这是双端共有的问题。只要 38889 被占用，或同一主机运行两个实例，就会触发；提案计划的「同机回环」测试恰好处在这种条件下。**最小修正**：Python 端对已知 peer 保留发现阶段的端口和主机名，只在新 peer 时使用默认端口。这不改变帧格式。C# 侧是否同步修改另行决定，但需要在步骤 1 的验收中加入「非默认端口」用例。

**R2 · 会话归档用的 IPv4 来源与 Windows 不一致（影响步骤 2 的键是否稳定）。** Python 收到 UDP 公告后，只要 `parts[5]` 不是 `127.0.0.1`，就用对方自报的 IP（第 423–425 行）。Windows 默认用报文源地址，只有源地址是回环或不在本机任何子网内时，才改用自报 IP（Discovery 第 105–113 行）。另外，Python 的 `detect_primary_ip` 借路由到 `8.8.8.8` 的出口地址来确定本机 IP，开启 TUN 时自报的会是虚拟网卡地址。提案把「多网卡对齐」列为不做，可以接受；但步骤 2 要用「已确认的局域网 IPv4」作为会话键，选址规则就必须在步骤 1 或 2 中与 Windows 对齐，否则同一台对端可能落到两个键下。**最小修正**：把这条选址规则写进步骤 1 的范围，物理网卡绑定继续不做。

**R3 · 私聊目标离线时，消息被静默丢弃（影响步骤 1）。** `_send_message` 先清空输入框，然后才查找 peer（第 1234–1252 行）。peer 被心跳超时移除后，用户输入的文字既没有发出，也没有进入记录。实测 `input_left=""`、`history=None`。提案的「私聊结果回到发起会话」覆盖不到这条路径，因为请求根本没有发出。**最小修正**：发送前判断在线状态，离线时保留输入并给出提示；步骤 2 接通离线会话后，再决定是否允许离线时写入本地记录。

**R4 · CLI 产物链已经断开，步骤 3「验证两类 CLI」无法基于当前源码执行（影响全部步骤的验收）。** `release/linux-x64/lanchat` 与 `linux-arm64/lanchat` 构建于 2026-10-20。此后共享网络源码已有 5 次提交，包括 `1eb8b76`（会话归档与 `RemoteAddress`）。`v2.0.16` 的 amd64 包内 `lanchat-bin` 为 14967951 字节，与 10-20 的旧 ELF 大小完全相同。`Tools/Publish.bat` 只调用 `build-deb.py`，不重建 ELF。`packaging/build-all.bat` 中的 `cd LanChat.Linux` 在目录重构后指向不存在的目录。`build-linux.bat` 输出到 `../release`，即 `src/linux/release`，而 `build-deb.py` 读取的是根目录下的 `release/linux-x64`。**最小修正**：在步骤 1 中修通 ELF 的构建与打包路径，并在交付状态中注明当前公开包里的 ELF 版本。这一项是否重建、何时发布，由用户按规则中的「发布」指令决定。

**R5 · 步骤 3 的可行性风险不只在图标（影响步骤 3 的拆分和用户决策范围）。** 以下判断来自 Tk 的实现常识，**尚未在 Linux 上实测**，但足以改变步骤 3 的形态：

- Tk 8.6 在 X11 上的 Canvas 图形没有抗锯齿。圆形按钮（36/44 px）、输入胶囊两端、浮动滑块端头和气泡圆角，如果用 Canvas 直接画，边缘会是锯齿，达不到规则第 9 条「杜绝生硬」的要求。因此「后端」问题覆盖所有圆形和圆角，不只是图标。
- `tk.Text` 的 tag 背景只能是矩形，现有「气泡」实际是逐行文字底色。要做真正的圆角气泡，只有两条路：一是改用 Canvas 消息流，需要自己换行和处理选择/复制；二是在 Text 中嵌入图片或窗口，长历史下的性能待测。这是步骤 3 的架构选择，提案只写了「由 Tk 绘制层承担」。
- 规则第 9 条要求所有弹出窗口、二级菜单为暗黑圆角组件。Tk 无法给 X11 顶层窗口做不规则形状或透明；`-transparentcolor` 仅限 Windows，`-alpha` 依赖合成器。因此 `tk.Menu`、`messagebox`、`simpledialog` 以及计划中的选人窗口，只能改为**主窗口内的叠加层**。Tk 自带的 `filedialog` 在 Linux 上是 Tk 风格的浅色窗口，带原生滚动条，在纯标准库内无法改造。

**最小修正**：把步骤 3 拆为「3a 实机技术验证」和「3b 正式改造」，3a 可以与步骤 1、2 并行；同时把文件选择窗口列为需要用户决定的规则例外。

**R6 · 图标后端：审核方意见（影响步骤 3 的正式接入）。** 用构建期 GDI+ 预渲染 `WeChatIcon.cs` 的方案，**符合规则第 9 条的本意**：几何、线宽、圆端点出自同一份 GDI+ 代码。它也**不违反规则第 4 条**：产物以 base64 嵌入 `lanchat.py`，运行期仍只依赖标准库。但规则第 9 条的字面语境是 WinForms，没有提到 Linux。是否适用于 Linux、是否接受「构建期 GDI+ 加运行期位图」这种等效形式，应由用户确认，并记入 DECISIONS 或 Rules，审核方不能替用户扩展规则的解释。如果用户采纳，需要满足以下条件：

1. 生成器直接调用 `WeChatIcon.Draw` 等现有方法，不另写一份几何。生成脚本放进仓库，由 `Publish.bat` 或打包脚本调用，产物可以重复生成。
2. 产物必须**嵌入单文件**，否则 Python 单文件形态拿不到图标。PNG 需要 Tk 8.6 支持；Linux 上下文列出的 CentOS 一类系统可能只有 Tk 8.5（不支持 PNG），需要 GIF 或纯文字回退，具体版本待核实。
3. 抗锯齿边缘依赖 PNG 的 alpha 通道。Tk 的 `PhotoImage.put` 无法写入半透明像素，因此强调色不能在运行期着色，只能按颜色和倍率分别预渲染，资源体积需要实测。
4. 现有图标中，`Add` 和第 742 行的图标线宽为 2.2，第 764–895 行一批为 1.2 或 1.4，都不在规则要求的 1.6–2.0 范围内。预渲染时只能选用合规的图标，或先修正这些线宽，不能整批照搬。
5. 结合 R5，同一后端应覆盖圆形按钮、胶囊两端等需要抗锯齿的形状，否则图标合规了，承载图标的圆钮仍是锯齿。

**R7 · 历史格式没有说明是否与 Windows 共用（影响步骤 2）。** 提案只写了「轻量 JSONL」。[规则第 5 条](../../.claude/Rules.md) 要求避免双端分歧。建议直接采用 Windows 的字段名，即 `ChatHistoryService.SerializeMessage` 中的 `SessionKey`、`SenderId`、`SenderNick`、`TargetId`、`PeerNick`、`RemoteAddress`、`Content`、`Timestamp`、`IsSelf`、`IsBroadcast`、`IsSystem`、`IsFile`、`FileName`、`FilePath`、`FileSize`，以及 `LAN-<IPv4>` 键和 `history_BROADCAST.jsonl` / `history_peer_*.jsonl` 文件名。这样双端的工具和排障口径一致。如果故意不共用，应写明理由。

**R8 · `lanchat.py` 的结构决定来得太晚（影响步骤 2 的开始条件）。** 规则第 10 条字面上只约束 `.cs` 文件，但 `lanchat.py` 已有 1696 行，GUI、网络、CLI、更新逻辑都在同一个文件里。步骤 2 将加入持久化、身份归并和传输状态，步骤 3 还要重写 UI，文件很可能超过 3000 行。提案把拆分评估放在步骤 3。建议在步骤 2 开始前，请用户决定两件事：一是规则第 10 条是否同样适用于 Python；二是继续维护单个源码文件，还是改为多模块源码加标准库 `zipapp` 生成单文件 `.pyz`（这会影响启动器、`.deb` 内容和用户的直接运行方式）。步骤 1 的外科式修复可以直接在原文件中进行。

**R9 · 接收目录同时出现在步骤 2 和步骤 3（文字矛盾）。** 「建议决定」第 3 节把「修改接收目录」放在第一批分类设置，即步骤 3；「建议顺序」又把「接收目录」列入步骤 2 的交付。应只保留一处。

**R10 · 步骤 1 新增的文字反馈就应遵守字号规范。** 当前 Linux 端普遍使用 7–8 pt 字号，规则第 9 条要求辅助文字为 9.5 pt `#8E8E93`。步骤 1 新增的失败提示、选人窗口等不应继续沿用 8 pt，否则步骤 3 还要返工一次。代码层面，Python 的类和公共方法也应按规则第 7 条写中文文档字符串，列入步骤 1 后的代码审核项。

**R11 · 项目记忆与事实不符（不阻塞，但应在修订稿中注明）。** `SessionBrief.md` 和 `Plan.md` 仍写着「当前公开版本 `v2.0.15`」，而 `HEAD` 已是 `v2.0.16` 的发布提交。`contexts/Linux.md` 写的是「全面实现与 Windows 端高度一致的…GUI」，与本话题的审计结论不符，产物文件名也停在 2.0.10。按规则，这些应在话题采纳或收口时刷新。

## 顺序

调整后的完整顺序如下。D1–D3 是需要用户做的决定，可以与步骤 1 并行，不阻塞步骤 1。

| 步骤 | 交付 | 开始条件与验收调整 |
| --- | --- | --- |
| D1 用户决定 | 规则第 9 条是否适用于 Linux；是否接受构建期 GDI+ 预渲染位图，覆盖图标和圆形、圆角形状（R6） | 步骤 3b 开始前必须有结论 |
| D2 用户决定 | Tk 无法满足的规则例外：文件选择窗口；顶层弹窗改为窗口内叠加层（R5） | 步骤 1 的选人窗口如果采用叠加层，可以先按叠加层实现 |
| D3 用户决定 | Python 是否适用行数上限，以及选择单文件还是 `zipapp`（R8） | 步骤 2 开始前必须有结论 |
| 1. 修复主流程 | 提案原有内容，加上 R1 端口保留、R2 选址规则、R3 离线发送、记录控件在异常后保持只读、R4 修通 ELF 构建与打包路径 | 原验收标准不变，补充以下用例：非默认 TCP 端口、拒收、接收端断开时不回确认、离线私聊、同名两机发文件。**至少一台 x86_64 Linux 安装真实 `.deb`，与一台 Windows 物理机互通**，Windows 侧覆盖 v2.0.16 和 v2.0.15。完成后做独立代码审核。 |
| 2. 接通会话历史 | 提案原有内容，JSONL 按 R7 与 Windows 对齐；接收目录只保留在步骤 2 或 3 之一（R9） | 在**安装路径**（`/usr/lib/lanchat` 走 `~/.config` 分支）和便携路径各验一遍。直连后再被发现，不应产生重复会话。 |
| 3a. 实机技术验证（可与 1、2 并行） | 在 X11 和 XWayland 会话下截图验证：Canvas 抗锯齿、PNG alpha、Text 与 Canvas 两种消息流各 2000 条时的切换和滚动耗时、叠加层菜单、Tk Text 在 fcitx5/ibus 下的候选窗与提交 | 产出一页实测记录，决定 3b 的绘制方式。不进入正式代码。 |
| 3b. 主窗与设置改造 | 提案原有内容，按 D1、D2 和 3a 的结论实施 | 按下方「未核实」中的实机矩阵全量回归 |

## 未核实

本轮没有 Linux 桌面，也没有第二台设备。以下各项仍需在实机上取证。修订稿应把它们写成交付门槛，而不是备注。

| 待核实项 | 最低取证要求 | 影响 |
| --- | --- | --- |
| 真实局域网双向互通 | Linux（安装的 `.deb`）与 Windows 物理机互相发现，记录从启动到出现在列表的时间；私聊、群聊、双向文件做 SHA-256 比对；Windows 关闭自动接收时的拒收路径；传输中途结束任一端进程。记录防火墙状态（ufw、firewalld 或发行版自带防火墙）、有无默认路由，以及 Linux 侧是否开启 TUN。 | 步骤 1 的结论 |
| 发行版、会话类型、架构 | 最低组合：x86_64 Ubuntu 22.04 或 24.04（GNOME Wayland 走 XWayland，另测一次 Xorg 会话），外加一台 ARM64 设备验证 all 包和 ARM64 ELF。记录 Tk 版本号、`tk scaling` 和桌面缩放比例。缺少 ARM64 或国产发行版环境时，交付状态须注明「未验证」。 | 全部步骤 |
| 中文输入 | 用 fcitx5 和 ibus 各测一次 Tk Text：候选窗位置、预编辑显示、Enter 提交与发送之间是否冲突、Ctrl+Enter 换行 | 步骤 3a；也影响 Enter 键绑定是否需要调整 |
| 抗锯齿和 alpha 表现 | 在 X11 和 XWayland 下，截图对比 Canvas 圆形和 PNG 圆钮，按 100% 放大检查边缘 | 决定 D1 和 3b 的绘制方式 |
| 长历史性能 | 2000 条消息时，会话切换、滚动和窗口缩放重绘的耗时 | 决定步骤 3a 的消息流方案 |
| 安装与启动器 | 从桌面图标启动，确认不出现终端；缺少 `python3-tk` 时 zenity 或 notify-send 的提示；SSH 无头环境下回退到 `--cli`；重建后的 ELF CLI 能与 Python 及 Windows 双向互通 | 步骤 1 和步骤 3b |
| 旧 ELF 的互通情况 | 当前公开 amd64 包中的旧 ELF 与 v2.0.16 Windows 能否互通（帧格式声明未变，但没有实测） | 决定 R4 是否需要单独发布修复 |

## 结论

**`CHANGES_REQUESTED`**。提案方向正确，8 项故障描述属实，步骤 1 的主体可以保留。请提案方在第 2 轮中处理以下内容：

1. 把 R1–R3 并入步骤 1 的范围和验收，R4 并入步骤 1 的交付。
2. 按 R5 把步骤 3 拆分为 3a 和 3b，3a 与步骤 1、2 并行。
3. 采纳或回应 R7 的历史格式对齐、R9 的文字矛盾和 R10 的字号与注释要求。
4. 把 D1–D3 作为待用户决定的问题写入「要决定的问题」。R6 中的审核意见可以作为 D1 的推荐选项。
5. 把「未核实」中的实机矩阵写成交付门槛。

本轮用于复现的探针脚本只放在本机临时目录，没有加入仓库，也没有修改任何业务代码。
