# LC-UI-INPUT-008 — 微信式输入区与设置页排版执行记录

> 2026-11-25 · 执行者 Codex · 状态 `WAITING_FOR_GROK` · 本记录是执行者交接，不是独立审核结论

## 本轮范围

根据用户提供的四张界面参考图，处理 Windows 主聊天输入区、设置中心各页面的双行排版，以及关于页产品描述与署名。未修改 LANC 魔数、Type 1/Type 2 封包字段、确认字节或 JSONL 历史格式。

## 实际改动

1. **微信式输入区与表情纵向稳定**
   - `src/MainForm.Designer.cs` 将底部输入胶囊调整为 128px 高，编辑区改为多行 RichEdit，上方用于文本编辑、下方保留快捷工具栏。
   - `src/UI/Controls/ChatInputBox.cs` 通过 RichEdit `EM_SETRECTNP` 固定文字格式区域的上/下内边距，避免插入 `[表情名]` 令牌后原生文字视觉上移；保留原始令牌文本与既有图片叠绘路径。
   - `src/MainForm.Chat.cs` 保持普通 Enter 发送，增加 Shift+Enter 换行。

2. **加号功能展开**
   - 将表情、打开接收文件夹、选择并发送文件、系统截图、清空当前会话作为输入区底部的 32px GDI+ 快捷按钮展开。
   - 加号菜单入口仍保留；语音按钮因当前协议没有语音消息能力而继续隐藏，避免展示不可用功能。

3. **设置页统一增加纵向留白**
   - `SettingsPanelBase` 增加共享 `SettingTitleOffset=7` 与 `SettingDescriptionOffset=32`。
   - 通用、个人主页、文件传输、聊天记录、设备连接、关于等页面的双行条目统一采用共享偏移量；Linux Electron `settings.css` 同步将条目最小高度调为 58px、说明文字上间距调为 6px。

4. **关于页文案**
   - Windows、Linux Electron 与旧版 Linux GUI 同步改为“专注局域网内的即时通讯与文件协作”“轻量、快速、无云端依赖 · 2026 yangjiang”，移除 `LanChat Team` 署名。

## 宿主机验证

| 实际命令 | 结果 |
| --- | --- |
| `Tools/Scripts/Compile.bat` | 返回 0；Windows 原生 `LanChat.exe` 与 `uninstall.exe` 编译成功。验证后已还原 release 二进制，避免把构建产物混入源码提交。 |
| `node --check src/linux/desktop/settings-pages.js` | 返回 0；Linux 设置页面脚本语法通过。 |
| `git diff --check` | 返回 0；无空白错误，仅有 Git 的换行转换提示。 |
| `git status --short` | 提交后工作区干净；源码与当前态记忆已提交并推送，提交 `2b6d510`。 |

## 文件行数门禁

本轮修改的手写源码均低于 1000 行软门禁：`MainForm.Designer.cs` 745、`ChatInputBox.cs` 447、`MainForm.Chat.cs` 599、`SettingsPanelBase.cs` 492、`GeneralSettingsPanel.cs` 269、`AboutSettingsPanel.cs` 122、Linux `settings-pages.js` 501、`settings.css` 742、旧版 Linux `gui.py` 960。

## 未验证边界

- 尚未在真实 Windows 窗口中对 100%/125%/150% DPI 做截图或鼠标命中验收。
- 尚未通过自动化用例证明多行 RichEdit 在超长文本、滚动、中文输入法预编辑和表情插入混合场景下的最终视觉表现。
- 尚未在 Linux Xorg、Wayland、XWayland 实机检查 Electron 输入区与设置页；当前仅同步源码并完成 Node 语法检查。

## 请审核者重点核对

1. `ChatInputBox` 的 `EM_SETRECTNP` 是否会在句柄重建、字体变化、Resize 和多行滚动时造成文本/表情坐标漂移或光标命中异常。
2. `_inputPanel.Resize` 的快捷按钮初始化、布局边界与发送按钮位置，确认不存在空引用、遮挡、不可点击或超出窄窗口的问题。
3. Shift+Enter 与普通 Enter 的按键语义是否符合现有快捷键契约；确认附件、剪贴板、历史和 LANC/JSONL 文本格式没有回归。
4. 设置页共享偏移量是否覆盖所有双行条目，并确认 54px 卡片行高下底部文字没有裁切。
5. 将真实 DPI、实际窗口目测和 Linux 实机验证区分为未验证证据，不要仅凭宿主机编译将本轮直接判为全环境通过。

---

# LC-FILE-TRANSFER-STABILITY-001 — 执行摘要

> 2026-11-25 · 执行者 Codex · 状态 `APPROVED` · Gemini 已批准重复接收气泡修复；详细记录见 `CRDocs/REVIEW.md` 同名记录

P0/P1/P2 已完成；本轮补修快速接收时的重复气泡竞态，Windows 回归、新增边界夹具、Linux net10.0 构建均通过；真实 Linux/ELF、双机 LAN 和桌面剪贴板仍未验证。

## v2.0.23 发布结果

- 用户明确要求将 `4e8bbc4` 修复与版本 `v2.0.23` 一起发布；`v2.0.22` 已发布资产保持不变，不包含本轮修复。
- `Tools/Scripts/Publish.bat 2.0.23` 已完成 Windows EXE/MSI、Linux amd64/arm64 ELF、deb、便携包、CLI 和 `SHA256SUMS.json` 构建。
- GitHub Release：[v2.0.23](https://github.com/yxiaojiang/LanChat/releases/tag/v2.0.23)；发布提交为 `7bd84e5`，标签为 `v2.0.23`，全部资产上传成功。

## 重复接收气泡修复

- **现象**：对端只发送一次文件时，接收端偶发同时出现一个完成文件卡片和一个“接收完成 · 100%”进度卡片。
- **根因**：`FileTransferController` 将进度合并延迟 50ms；快速传输的 `OnFileReceived` 可能先于延迟进度执行，完成回调先追加完整卡片，随后延迟进度因找不到 `_incoming` 又追加一条卡片。
- **修复**：`HandleFileReceived` 在接管完成消息前，按 `FileTransferId` 从 `_pendingProgress` 移除尚未刷新的进度，保留既有完成消息复用逻辑；未修改 LANC 封包、文件确认字节和历史格式。
- **新增回归**：`ChatComposerRegression` 模拟“最终进度已排队、完成回调先到”的时序，断言会话内存、UI 追加事件和 JSONL 历史均只保留一条文件消息。

## 本轮验证

| 实际命令 | 结果 |
| --- | --- |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | 返回 0；新增重复接收气泡回归与既有聊天、剪贴板、LANC 回环、截图清理用例通过。 |
| `cmd /c "Tools\\Scripts\\Compile.bat < NUL"` | 返回 0；Windows 原生 `LanChat.exe` 编译成功。 |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/file_transfer_stability_regression.ps1` | 返回 0；0 字节、64KB、全局 6、单目标 2 边界通过。 |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 返回 0；好友刷新及既有 Windows 入口边界通过。 |
| `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false` | 返回 0；Linux net10.0，0 警告、0 错误。 |

## v2.0.22 发布收口

- `Tools/Scripts/Publish.bat 2.0.22` 已完成 Windows EXE/MSI、Linux amd64/arm64 deb、双架构便携包、双架构 CLI 和 `SHA256SUMS.json` 构建。
- GitHub Release：[v2.0.22](https://github.com/yxiaojiang/LanChat/releases/tag/v2.0.22)；发布提交为 `d029ff9`，标签为 `v2.0.22`。
- 发布后发现异常提交 `5e8ac76 (aa)` 将 24 个历史 release 归档产物写成 0 字节，已从 `d029ff9` 恢复，后续以独立修复提交推送。
- 用户已明确要求将本轮修复作为 `v2.0.23` 发布；`v2.0.22` 资产保持原样，不宣称包含 `4e8bbc4`。

---

# 本轮交付 — Windows 缩放稳定性、11 色外观、传输速率与群聊文件多选

> 2026-11-25 · 执行者 Codex · 状态 `APPROVED` · v2.0.21 已发布

## v2.0.21 发布结果

- `Tools/Scripts/Publish.bat` 已完成版本递增、Windows 原生编译、WiX MSI、Linux x86_64/ARM64 自包含 ELF、Electron Debian/便携包与 CLI 构建。
- GitHub Release：[`v2.0.21`](https://github.com/yxiaojiang/LanChat/releases/tag/v2.0.21)，全部资产上传成功；发布提交为 `f09226b`，此前审核与返修记录分别为 `41a9be5`、`a056460`。
- `CHANGELOG.md` 已新增 v2.0.21 更新说明，包含头像缩放、11 色配色、传输速率、群聊文件多选及 `LC-RV-001` 修复。

## LC-RV-001 返修

- `PickFileRecipients` 在无在线好友时统一返回 `new List<PeerInfo>()`；新增 `NormalizeRecipientList`，将可能的 `null` 在线快照归一化为非空列表，保持 `PromptPeerAndSendFiles` 与 `PickFileRecipient` 的 `Count` 判空调用不变。
- `FriendRefreshRegression` 新增空列表与 `null` 快照断言，覆盖关闭“无在线好友”警告后的安全返回路径。
- 返修后 `friend_refresh_regression.ps1`、`chat_composer_regression.ps1 -ExpectLocalPack`、Node IPC/UI 回归、Node `--check`、Linux Release `dotnet build` 与 `git diff --check` 均通过。

## 实际改动

- 将主窗体左上角 Hero 头像所在面板改为 `DoubleBufferedPanel`，避免拖拽缩放期间普通 `Panel` 背景擦除与头像重绘闪烁；新增 WinForms 回归断言确认该面板启用双缓冲。
- 外观强调色扩展为附件图中的 11 个标准 RGB：`#FFFFFF`、`#A16B39`、`#FC2C3E`、`#F9670E`、`#F69412`、`#14C370`、`#13B8A3`、`#1884FC`、`#905AFB`、`#FB3499`、`#777777`。WinForms 与 Linux Electron 共用同一索引顺序，settings.json 的整数索引和已有 LANC/JSONL 格式不变。
- `FileTransferController.FormatTransferRate` 统一格式化 KB/s、MB/s；Windows 文件气泡在发送/接收进度有速率样本时追加当前网速，Linux `DesktopService` 将 `speed` 透传到 Electron，桌面文件卡片同步显示。
- 群聊附件发送使用在线成员多选弹框（`MultiSimple`），点击“确定发送”后逐个投递到所选成员。剪贴板截图在多收件人场景为后续收件人生成独立托管副本，成功历史引用保留，失败或未入队副本按既有托管清理策略删除；用户原始文件不接管清理。
- 未修改 LANC 封包、文件报价/确认字节或 JSONL 历史字段。

## Windows 宿主机与共享后端验证

| 实际命令 | 结果 |
| --- | --- |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 返回 0；原生编译、头像双缓冲断言、群聊多选收件人断言、好友刷新与 LC-CC-006 全部通过。 |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | 返回 0；新增 11 色 RGB 与 KB/s/MB/s 格式化断言，既有 Emoji、PNG、LANC、历史及 LC-CC-004/005 回归通过。 |
| `C:/Program Files/nodejs/node.exe --check <桌面与测试 JS/CJS>` | 返回 0；相关桌面脚本与回归脚本语法通过。 |
| `C:/Program Files/nodejs/node.exe tests/linux_desktop_ui_regression.js` | 返回 0；Linux 外观 11 色、双向进度速率显示及既有 UI/IPC 用例通过。 |
| `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false` | 返回 0，0 警告、0 错误。 |
| `git diff --check` | 返回 0，无空白错误。 |

## 行数与未验证边界

本轮变更手写源码与测试文件均低于 1000 行软门禁，最高为 `tests/linux_desktop_ui_regression.js` 924 行。当前仅完成 Windows 宿主机和脚本/共享后端验证；真实 Windows 拖拽缩放长时间录屏、真实截图工具/Excel/WPS 粘贴、Linux Xorg/Wayland/XWayland 实机、最新 Linux ELF 实跑及双机局域网联测未验证，继续保留于 [DEFERRED.md](DEFERRED.md)。

---

# LC-CC-005 / LC-CC-006 — Windows 输入区与选区/草稿用例

> 2026-11-25 · 执行者 Codex · 状态 `WAITING_FOR_GROK` · 工作区交付

## 实际改动

- `ChatInputBox` 在图文混合剪贴板中优先读取非空 Unicode 文本。文本存在时不再把位图当作截图附件，普通文本粘贴继续交给原生输入流程。输入框右键统一使用暗色菜单，保留粘贴、粘贴附件、复制、剪切和全选入口。输入框隐藏时，外层输入胶囊首次右键直接转交同一菜单。
- `ChatComposerRegression` 增加 Excel/WPS 类图文并存 `DataObject` 的优先级断言，并确认纯位图仍保留附件入口。
- `FriendRefreshRegression` 增加真实 `WeChatChatView` 视觉行像素命中扫描，所有命中点都必须位于 `ChatTextLayout.NextBoundary` 边界。新增跨 CRLF 原文复制、切换好友会话后草稿隔离、切回后恢复和关闭窗体清理未发送截图断言，并验证隐藏输入框第一次右键能弹出 `GrokPopupMenu`。
- 未修改 LANC 封包、历史 JSONL 字段、传输协议或用户原始文件清理策略。

## Windows 宿主机验证

| 实际命令 | 结果 |
| --- | --- |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | 返回 0。输出 `LC-CC-005: Windows 图文混合剪贴板优先级断言通过`，并通过既有 Emoji、PNG、LANC、历史回读及 LC-CC-004 回归。 |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 返回 0。好友刷新、网卡重绑、侧栏交互、LC-CC-005 首次右键暗色菜单、LC-CC-006 像素命中/跨 CRLF/会话草稿/关闭清理全部通过。 |
| `cmd.exe /d /c "Tools\Scripts\Compile.bat < nul"` | 在 Windows 隔离编译目录返回 0，生成 `LanChat.exe` 与 `uninstall.exe`。 |
| `C:/Program Files/nodejs/node.exe --check <桌面与测试 JS/CJS>` | 15 个桌面和 Node 回归文件均返回 0。 |
| `C:/Program Files/nodejs/node.exe tests/linux_desktop_ui_regression.js` | 返回 0，输出 `linux desktop ui regression: ok`。 |
| `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false` | 返回 0，0 警告、0 错误。 |
| `git diff --check` | 返回 0，无空白错误。 |

## 行数与未验证边界

本轮变更的手写源码和测试文件均低于 1000 行软门禁：`ChatInputBox.cs` 184 行、`MainForm.Designer.cs` 690 行、`ChatComposerRegression.cs` 295 行、`FriendRefreshRegression.cs` 472 行。工作区最高仍为 `tests/linux_desktop_ui_regression.js` 922 行。

Windows 真实截图工具与 Excel/WPS 桌面剪贴板仍未在用户实际桌面取证。Linux Xorg、Wayland、XWayland、最新 ELF 实跑及真实双机联测仍待环境验收，保留于 [DEFERRED.md](DEFERRED.md)。

---

# LC-CC-004 — 截图发送失败与退出清理

> 2026-11-25 · 执行者 Codex · 状态 `WAITING_FOR_GROK` · 工作区交付

## 实际改动

- Windows 从草稿预览登记中移交实际入队的应用截图。普通文件继续沿用原发送入口。控制器在 TCP 完成回调中同步确认截图历史写入，并与窗体退出清理使用同一把锁。拒收、断连、写入失败且无有效引用时删除应用截图。发送成功且历史已写入的截图即使在 UI 句柄失效时也保留。退出允许删除仍由发送线程打开的托管截图，普通文件保持原读取共享模式。
- Linux 主进程跟踪截图创建、丢弃、发送预检及后台队列。发送预检失败时可重试，退出已开始时直接清理预检持有的应用截图。退出停止后端并等待清理收尾，排队附件停止派发。文件响应和消息事件返回托管截图的历史写入结果，主进程在后端管道关闭前仍接收晚到的已落盘消息。后端异常退出且在途结果无法确认时保留该截图，避免丢失可能已写入的历史附件。用户文件按创建来源与应用截图区分。
- 两端继续使用原 LANC 文件报价帧和确认字节，JSONL 继续使用原字段与序列化。Linux 的 `managedPaste` 和 `historySaved` 仅用于本地 Electron 与后端管道。

## Windows 宿主机验证

| 实际命令 | 结果 |
| --- | --- |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | 返回 0。新增对端拒收、读完 PNG 后断连、历史写入失败、退出与最终 ACK 交叠、无 UI 句柄时写入成功、保留同目录用户同名模式文件的回环断言。原 Emoji、剪贴板数据、LANC 与历史回读回归通过。 |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 返回 0。原生编译、好友刷新、网卡重绑、WinForms 侧栏交互与截图草稿暂存及移除通过。 |
| `C:/Program Files/nodejs/node.exe --check <文件>` | 桌面目录 13 个 JS/CJS 和 2 个 Node 回归脚本均返回 0。 |
| `C:/Program Files/nodejs/node.exe tests/linux_desktop_ui_regression.js` | 返回 0，输出 `linux desktop ui regression: ok`。真实主进程处理器配合 IPC 桩与临时目录，覆盖预检失败后重试、混合批次拒收与断连、写入结果、退出期间预检成功与失败、队列停止、管道晚到确认、正常与异常后端退出、原文件保留。 |
| `cmd.exe /d /c 'Tools\Scripts\Compile.bat < nul'` | 在隔离的 `bin/lc-cc-004-compile/` 工作目录调用原脚本，返回 0。生成 `LanChat.exe` 与 `uninstall.exe`。 |
| `csc.exe /nologo /codepage:65001 /target:exe /r:<隔离目录>/LanChat.exe /out:<隔离目录>/ConcurrentReceive.exe tests/ConcurrentReceive.cs`，随后运行 `ConcurrentReceive.exe <隔离接收目录>` | 编译、运行均返回 0。输出 `Final ACK: 1, 1; Complete events: 2`，两份同名文件的 SHA-256 校验通过。 |
| `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false` | 返回 0，0 警告、0 错误。 |
| `pwsh -NoProfile -File bin/lc-cc-004-backend/verify.ps1` | 返回 0。忽略目录的临时夹具加载上述 `lanchat.dll`，使用隔离 TCP 回环与历史目录验证后端实际响应和事件。输出 `stored OK`、`blocked OK`、`rejected OK`、`stopping OK`。 |
| `git diff --check` | 返回 0，无空白错误。 |

## 行数与待验证环境

按编辑器行号，LC-CC-004 改动的手写源码和测试文件均低于 1000 行。`FileTransferController.cs` 331 行、`MainForm.Chat.cs` 589 行、`MainForm.Files.cs` 386 行、`NetworkService.Transfer.cs` 434 行、`ChatHistoryService.cs` 476 行、`SessionManager.cs` 390 行、`DesktopService.cs` 476 行、`main.cjs` 611 行、`service.cjs` 117 行、`ChatComposerRegression.cs` 274 行、`linux_desktop_ipc_regression.js` 393 行。含已放行的 LC-CC-003 工作区变更，手写源码与测试最高为 923 行。

Linux Xorg、Wayland、XWayland 上的 Electron 实机截图和退出、最新 Linux ELF 实跑、真实双机拒收与断连及新旧版联测仍待取证。 [跨环境验收](DEFERRED.md)

---

# LC-CC-003 — Linux 侧栏摘要与表情回显

> 2026-11-25 · 执行者 Codex · 状态 `WAITING_FOR_GROK`

## 实际改动

- 先拆分原 961 行的 `renderer.js`。`session-sidebar.js` 负责会话行、分组、搜索、方向键与侧栏菜单锚点。`conversation-view.js` 负责气泡、文件卡片、进度呈现、聊天滚动与复制。主控制器持有会话、历史加载、发送和传输状态。`index.html` 在主控制器前加载两个视图，打包器按桌面目录收录这两个文件。
- `presentation.js` 使用 `Intl.Segmenter` 的字素边界，将导入清单语法匹配的 `[名称]` 当作一个元素。群聊 20、私聊 16 的阈值及超出时追加 `...` 的行为继续适用。启动时 `DesktopService` 传递最后一条归档消息的完整摘要来源文本，归档与实时摘要都在渲染端截断。`state.sessions[].summary` 仍是字符串，保留既有群聊昵称前缀和文件标签。LANC 帧和历史 JSONL 格式沿用原结构。
- 侧栏与聊天气泡共用行内表情渲染。已导入的令牌显示图片，资源缺失或图片解码失败时显示原 `[名称]`。侧栏令牌使用行内盒，沿用原单行省略样式、9.5pt 基准字号和表情图尺寸。

## 回归与证据

| 命令或夹具 | Windows 宿主机实测结果 |
| --- | --- |
| `node --check`，桌面目录 13 个 JS/CJS 与 2 个 Node 回归文件 | 15 个文件均返回 0。使用 `C:/Program Files/nodejs/node.exe` v24.18.0。 |
| `node tests/linux_desktop_ui_regression.js` | 返回 0，输出 `linux desktop ui regression: ok`。断言 👨‍👩‍👧‍👦、👍🏽、🇨🇳、`[敲打]` 在恰好达限、保留边界和舍弃边界均完整。覆盖归档私聊和实时群聊的表情图片、无表情包、单项资源缺失和图片错误回退。原 IPC、好友分组、键盘导航、历史竞态、文件进度和截图草稿用例一并通过。 |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | 返回 0。Windows Emoji 整段测量和像素对照、本地表情、剪贴板数据、LANC 回环与历史回读通过。 |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 返回 0。原生编译、好友刷新和网卡重绑、会话列表键盘/滑块、分组尺寸与截图草稿回归通过。 |
| `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false` | 返回 0，0 警告、0 错误。 |
| Windows PowerShell 7 临时归档夹具 | 加载上述构建的程序集，读取临时 JSONL，直接调用 `DesktopService` 的 `state` 处理路径。返回的群聊和私聊 `summary` 与完整原文一致。夹具未启动网络服务。 |
| Windows Edge Headless 临时页面夹具 | 使用应用页面、样式和生产脚本，只替换桌面桥接。实际解码侧栏和气泡中的 PNG，并核对无资源和坏图时令牌文字的 DOM 与样式。 |
| `git diff --check` | 返回 0，无空白错误。 |

## 行数与未验证边界

按编辑器行号计，新增及修改的手写文件均低于 1000 行。 `renderer.js` 690 行，`session-sidebar.js` 146 行，`conversation-view.js` 194 行，`presentation.js` 201 行，`desktop.css` 192 行，`index.html` 90 行，`DesktopService.cs` 471 行，`linux_desktop_ui_regression.js` 923 行。

Linux Xorg、Wayland、XWayland 的实际 Electron 窄侧栏摘录、Emoji 字形、系统剪贴板和 DPI 暂无实测。最新 ELF 的 Linux/QEMU 网络命名空间回归及双机新旧端联测仍待取证。 [跨环境验收](DEFERRED.md)

---

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
# LC-FILE-TRANSFER-STABILITY-001 — 执行摘要

> 2026-11-25 · 执行者 Codex · 状态 `WAITING_FOR_GROK` · 详细记录见 `CRDocs/REVIEW.md` 顶部

- 已完成 P0/P1/P2：单调速率、文件流 TOCTOU、超时、256KB Socket 缓冲、32/16 入口限制、6/2 公平出站队列、UI 进度合批、托管截图引用计数清理和 Linux IPC 兜底。
- 已新增 `tests/FileTransferStabilityRegression.cs` 与 `tests/file_transfer_stability_regression.ps1`，覆盖 0 字节、64KB、约 1MB、速率有限性和并发上限。
- Windows 原生编译、好友刷新、聊天编辑器、并发接收及新增夹具均通过；Linux 共享代码使用 net9 临时目标编译通过。
- 正式 `dotnet build` net10.0 受宿主仅安装 SDK 9.0.304 阻断；真实 Linux/ELF、双机 LAN 和桌面剪贴板仍未验证。

---

