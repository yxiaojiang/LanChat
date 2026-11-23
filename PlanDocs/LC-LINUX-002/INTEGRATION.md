# LC-LINUX-002 · Linux 首包集成

试验版本 `2.0.17~experiment.1`。构建使用 .NET SDK 10.0.404、运行时 10.0.15 与 Electron 45.4.1。两份 Electron 官方 ZIP 按各自 SHA-256 锁定，应用的本地 HTML、CSS、JavaScript 无需 npm 依赖。试验构建独立于旧 Python 包和正式发布脚本。 [Electron 版本](https://releases.electronjs.org/release/v45.4.1) · [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

本地输出为 `bin/lc-linux-002/experiment/`。每种架构包含一个 deb、一个桌面便携 tar.gz 及一个可独立运行的 ELF。文件名及 SHA-256 见同目录 `SHA256SUMS.json`。从 deb 的实际 data tar 回读二进制，Electron 和通信 ELF 在 amd64 包均为 ELF64、机器码 62，在 arm64 包均为 ELF64、机器码 183。两份 deb 的实际控制字段与载荷均无 Python 运行时。

## 构建入口

1. 通过 .NET 10 SDK 运行 `python packaging/build_linux.py bin/lc-linux-002/data-elf`。构建结果分别放在 `linux-x64/lanchat` 和 `linux-arm64/lanchat`。当前工作树的第 2 步构建使用新试验版本目录，保留本次集成的 `experiment/` 原始产物。
2. 在 Windows 用系统 `csc.exe` 编译 [图标导出器](../../packaging/ExportLinuxDesktopAssets.cs)，引用当前原生编译的 `LanChat.exe` 和 `System.Drawing.dll`，将生成的导出器置于该 `LanChat.exe` 同级运行并传入 PNG 输出目录。渲染器几何来自 WinForms。
3. 将官方 `electron-v45.4.1-linux-x64.zip` 和 `electron-v45.4.1-linux-arm64.zip` 放入同一缓存路径。运行下列本地打包命令。

```powershell
python packaging/build-electron.py --output bin/lc-linux-002/data-experiment --archives bin/lc-linux-002/cache --elf bin/lc-linux-002/data-elf --icons bin/lc-linux-002/icons
```

系统安装保留包名 `lanchat` 和 `Exec=lanchat`。图形环境默认打开桌面，`--cli`、`-c` 和无图形会话直接执行 ELF。便携包解压后运行根目录 `./lanchat`，根目录用于沿用旧同级设置。桌面运行需要系统图形库和可用的 Chromium 沙盒。系统 deb 的 `chrome-sandbox` 为 root:root、4755。

## 集成环境

QEMU 10.2.0 TCG，Ubuntu Minimal 24.04.5 amd64；基于旧实验 VM 的独立写时复制盘。GUI 使用 Xorg、Openbox，屏幕 1280×800。虚拟机的安装启动只计首包集成。与公开 Windows 的物理互通、正式升级／回退、GNOME 图形后端矩阵，以及中文输入法仍按计划逐项验收。

- APT 在虚拟机中把现有 LC-LINUX-001 候选 amd64 2.0.16 变更为试验 2.0.17，核对新包版本和沙盒辅助程序权限。记录保存在来宾 `~/lc002/evidence/`。
- ELF 从直接入口输出 `ready` 协议 1、UDP 38888、TCP 38889，随后 `state` 和 `quit` 均返回有效 JSON。
- SSH 无 TTY 运行安装命令，输入 `/chat all`、`/refresh`、`/quit` 成功。附带图形环境变量时，安装命令的 `--cli` 和 `-c` 仍直接进入 CLI，退出后监听端口释放。
- 原工作树的 22 项旧 Python／共享网络回归通过。
- 从 `gio launch /usr/share/applications/lanchat.desktop` 打开真实 Electron 窗口，客户区 1040×720。应用服务和对端 ELF 分别在来宾的两个 veth 命名空间里（`10.77.2.1/29`、`10.77.2.2/29`）。验证自动发现、点击刷新、双向中文群聊与私聊。普通点击系统 GTK 文件选择器可发送。
- 双向文件均为 311071 字节，源件与接收件 SHA-256 均为 `0e8316c345e411129ae83a0f1ef404d23fd7a34fabf4d001aed93e45317b16e6`。收件结束后对端收到最终 ACK，桌面和对端均显示接收文件。点击「退出 LanChat」后对端移除该节点，退出后的 `ss` 结果中无 UDP 38888 或 TCP 38889。
- 渲染器 `/proc/.../status` 实测 `NoNewPrivs: 1`、`Seccomp: 2`，程序未附带关闭沙盒的参数。双方传输完成后单次 PSS 采样中，桌面 ELF 为 28444 KiB，已采样的 Electron 进程及该 ELF 共 315076 KiB，未计对端；这组数值来自 TCG 来宾热态。
- 本地原始记录见 `bin/lc-linux-002/vm/evidence/`，包括 `integration.log`、`peer.jsonl`、`file-transfer.png`、`sockets-before-quit.txt`、`sockets-after-quit.txt` 和渲染器状态。ARM64 只完成构建及包内机器码与摘要核验。

## 第 2 步已开始

- 旧 WinForms 基线的共享接收核心在两路交错发送同名异内容文件时只保留一份，最终 ACK 为 `1, -1`，完成事件数为 1。修复为每路独立临时文件，串行提交时选取可用的文件名。原生 Windows 构建回归中，最终 ACK 为 `1, 1`，完成事件数为 2，两个落盘文件分别匹配源件 SHA-256 `58bb119c35513a451d24dc20ef0e9031ec85b35bfc919d263e7e5d9868909cb5` 和 `fc686c314491e1f68bf1899fc54b2327353c44dd1ab4ed56538ef623edd1e866`。证据在 `bin/lc-linux-002/baseline/windows/concurrent-receive-before.txt` 和 `bin/lc-linux-002/regression/windows/concurrent-receive-after.txt`。
- 当前源码已接入共享 `SessionManager` 和 `ChatHistoryService`，历史就地读取及追加，按确认 IPv4 归档、按现行节点 ID 和监听端口发包。Linux CLI `/nick` 在原位置备份并写回设置，保留未知键。第 1 步试验包已冻结，第 2 步构建另存。
- 用保全的便携样本副本做隔离兼容回归。旧候选 pyz 通过 LANC 发来带引号、反斜线、换行的中文和第二条群聊，重启新版后继续读到共三条历史。旧 pyz 也读到全部三条、更新后的昵称和未知字段。原接收文件及写前设置备份逐字节一致。记录在 `bin/lc-linux-002/vm/evidence/data-regression.log`。系统包升级／回退、旧 GUI 与双架构实机仍按 Plan 验收。
- `2.0.17~experiment.2` 的两架构 deb、便携包和独立 ELF 位于 `bin/lc-linux-002/data-experiment/`。复读 deb 核对两类二进制的架构与摘要。更新后的 amd64 桌面在同一 QEMU 环境复跑了首包链路，双向聊天、双向 SHA-256、沙盒和退出仍通过。该复跑用于验证试验包集成。
- 最新 ELF 的隔离配置回归确认同级已有文件优先、配置和数据两个目录共存时使用配置目录、仅配置目录创建失败时回退、只读原文件保持原字节。当前只读时报告写入错误，选择可写位置的确认流程待接入。复跑原始记录在 `bin/lc-linux-002/vm/experiment.2/evidence/`。

## 第 2 步第三轮试验包

用户回传 `dpkg -i`、`/etc/os-release`、APT policy、`dpkg -l` 与运行中通信服务的动态库映射。实机为银河麒麟桌面 V10 国防版（020）ARM64。第二轮包解包后状态为 `iU`，配置同时被两项依赖阻塞。

| 失败依赖 | 现场证据 | 第三轮调整 |
| --- | --- | --- |
| `libc6 (>= 2.35)` | 系统安装 `2.31-0kylin9.1k20.8`。两种架构的 Electron 主程序最高引用 `GLIBC_2.25`，自包含 ELF 宿主最高引用 `GLIBC_2.27`。 | 改为 `libc6 (>= 2.31)`。 |
| `libssl3t64 \| libssl3` | 二者均无已安装版本和候选。已装 `libssl1.1` 版本为 `1.1.1f-1ubuntu2.24`，`lanchat-bin` 映射了 `/usr/lib/aarch64-linux-gnu/libssl.so.1.1` 与 `libcrypto.so.1.1`。 | 保留 OpenSSL 3 优先，追加 `libssl1.1 (>= 1.1.1)`。 |

运行时在加载 OpenSSL 3 失败后尝试 1.1。物理现场仅有第二轮包的桌面启动、文字收发和刷新证据。第三轮包的实机配置状态待用户核验。 [固定版本 .NET OpenSSL 加载代码](https://github.com/dotnet/runtime/blob/v10.0.15/src/native/libs/System.Security.Cryptography.Native/opensslshim.c#L89-L98)

第三轮构建使用此前锁定的 Electron 与 .NET。安装包、桌面便携包和独立 ELF 的构建摘要在 `bin/lc-linux-002/experiment.3/SHA256SUMS.json`。以 tar 读回两种架构的 Electron 与通信 ELF，核对二进制机器码、控制字段和系统包沙盒权限。完整试验交接在 `bin/lc-linux-002/handoff-experiment.3/`，上一轮交接按原摘要保全。

设置只读时先备份原文件并展示备用路径，经用户确认才写入备用位置。备份和备用设置放在 `~/.local/share/lanchat/settings-overrides/<原路径 SHA-256>/`，保留原配置与数据目录。旧版回退读取原配置，备用文件用于取回新版修改。桌面入口为「设置」，CLI 在 `/nick` 写入受阻后提示确认。应用重启读取已获确认的备用设置。

### 第 2 步 2026-11-23 续测

| 环境与版本 | 已有证据 |
| --- | --- |
| Ubuntu 24.04.5 amd64 QEMU，第三轮 | APT 从第二轮升级后已配置。隔离回环确认只读设置备份、取消、确认、配置锁与重启。网络用例覆盖非默认端口、定长短读、中断清理、拒收、最终 ACK 缺失、接收落盘失败 ACK、同名并发和同 IP 换节点恢复。并发两份分别匹配 `47eba4d8fe95b8c6da29c6aad402470c09116737fade574d39b23348193d3e0a` 和 `dbfaca2662cb70b69dfefd5ac95d1f54a73663092d46cefdc9609dc695a12c98`，64 MiB 传输与两路最终 ACK 均通过。 |
| Ubuntu 22.04.5 amd64 Ubuntu Base，第三轮 | 在 Ubuntu 24.04.5 QEMU 内用官方 Ubuntu Base 根文件系统及该发行版的 APT 源安装。包状态为 `ii`，服务输出 ready 和 quit，进程映射 `libssl.so.3` 与 `libcrypto.so.3`，无头 CLI 退出正常。该记录为包管理和 ELF 证据。 |
| Ubuntu 20.04.5 arm64 Ubuntu Base，第二、三、五轮 | 模拟安装时复现第二轮两项依赖无法满足。第三轮及第五轮经 APT 先后配置成功，`lanchat` 和 `libssl1.1` 均为 `ii`，第五轮的无头 CLI 退出码为零。第五轮独立 ELF 在同一根文件系统的回环命名空间启动和退出，实测 glibc `2.31-0ubuntu9.9`，映射 `libssl.so.1.1` 与 `libcrypto.so.1.1`。此环境用来核对旧 ABI 分支，银河麒麟实机仍按用户回传结果验收。 |
| Ubuntu 24.04.5 amd64 和 arm64 QEMU，第四轮 ELF | 独立用例验证主动迁移。复制后的 JSONL 与原文件逐字节一致，已占用目标目录原数据保持，迁移后的新收件只追加至新目录，重启恢复两条文件记录及两个原始附件路径。 |
| Ubuntu 24.04.5 amd64 QEMU，第三轮系统包 | 公开版与 LC-LINUX-001 候选各自覆盖 `amd64 → amd64 → amd64`、`all → amd64 → all`。APT 实际升级、直接回退及卸载重装通过。回退后的包状态是 `ii`、旧 Python GUI 可见、CLI 读回新版写入的昵称。候选版读取原历史和新增兼容历史。原接收文件及路径保留。 |
| Ubuntu 24.04.5 arm64 QEMU，第三轮系统包 | APT 配置成功，`dpkg -l` 观察到第三轮 `ii`。Xvfb 下首次桌面运行时 GPU 子进程退出，页面装载失败。后续软件渲染取证单列。 |
| Ubuntu 24.04.5 arm64 QEMU，第三轮旧包回退 | 公开版、候选版分别完成 `all → arm64 → all`。保留设置、历史、附件原路径，恢复旧 CLI 和旧 Tk 窗口，通过普通卸载重装。候选版回退遇到来宾自动更新占用 APT 锁，释放后从同一升级样本继续。见 `arm64-noble-matrix.log` 与 `arm64-noble-candidate-complete.log`。 |
| Ubuntu 24.04.5 arm64 QEMU，第四轮包内桌面 | 使用从 deb 提取的原样载荷，Xvfb 测试以 `--disable-gpu` 启动 Electron，保留渲染器沙盒。通信进程强退后手动重连，PID 由 7831 变为 8446，重读旧历史、保留草稿、退出释放监听。截图中的中文缺字保留为最小来宾见证，中文输入和显示由后续矩阵验收。 |
| Ubuntu 24.04.5 amd64 QEMU，第四轮包内桌面 | 使用从 deb 提取的原样载荷，在 X11/Xvfb 与 Openbox 中启动。在草稿仍未发送时强退通信进程，用户操作重连后 PID 由 7280 变为 7852。截图显示重读旧历史和保留原草稿。渲染器的 `NoNewPrivs` 和 `Seccomp` 均符合沙盒要求，最后从窗口退出并释放监听。 |
| Ubuntu 24.04.5 amd64 和 arm64 QEMU，第五轮 ELF | 设置、迁移和传输脚本全部通过。管道输入 `/nick`、`/quit` 在原设置只读时退出，原配置和备份逐字节一致。旧便携 pyz 读回未知键、新昵称和三条兼容历史，原收件不变。 |
| Ubuntu 24.04.5 amd64 和 arm64 QEMU，第五轮传输中断 | 对端收到全部文件正文后保留连接及最终回执，强退发送方通信进程，进程未返回成功。重启后监听和配置锁可用，历史无成功文件卡片，测试对端未收到重发。 |
| Ubuntu 22.04.5 amd64 Ubuntu Base，第五轮系统包 | 从第三轮直接 APT 升级，`dpkg -l` 显示第五轮 `ii`。CLI 验群聊切换、刷新和退出，退出码为零。 |
| Ubuntu 22.04.5 arm64 Ubuntu Base，第三轮系统包 | APT 安装后 `ii`，通信服务映射 OpenSSL 3 的两个库，服务与无头 CLI 退出成功。 |
| Ubuntu 22.04.5 两架构 Ubuntu Base，第五轮系统包 | 公开版与 LC-LINUX-001 候选分别覆盖 `amd64 → amd64 → amd64`、`all → amd64 → all`、`all → arm64 → all`。APT 实际升级、直接回退和卸载重装均完成，旧 Tk 窗口经 Xvfb 观察到。新版写回昵称由旧 CLI 读回，原历史和附件在原路径。记录见 `amd64-jammy-rollback.log` 与 `arm64-jammy-rollback.log`。这是 Ubuntu Base 根文件系统和 X11 虚拟显示证据。 |
| Ubuntu 24.04.5 amd64 QEMU，第五轮系统包同级设置 | 旧候选的 `/usr/lib/lanchat/settings.json` 在升级、普通卸载及恢复旧包时保留原路径和原字节。新版及恢复后的旧版 CLI 均读取其中的昵称。见 `amd64-experiment.5-adjacent.log`。 |

第四轮允许在桌面设置中选择聊天记录目录，迁移完成后才保存新路径。旧目录的 JSONL 保留给旧程序，旧 Python 不识别 `ChatHistoryDirectory`。选择已有 LanChat 历史的目标会报告冲突。附件仍在原收件目录。

第五轮修改只读配置的 CLI 确认流程。交互终端可以确认备用路径，输入重定向时保留后续命令。构建产物在 `bin/lc-linux-002/experiment.5/`，完整交接含两架构安装包、便携包、独立 ELF 与旧版样本，位置为 `bin/lc-linux-002/handoff-experiment.5/`。26 件文件写入交接摘要，并复核两个 deb 和便携归档中 Electron、通信 ELF 的架构和摘要。以前各轮目录保留。既有 22 项 Python／共享核心测试复跑通过，桌面 JavaScript 语法与 Git 差异检查通过。

可复跑的 Linux ELF 用例在 [回归脚本](../../tests/linux_desktop_regression.py)，在非 root 的仅回环网络命名空间中运行，参数依次为 ELF 路径和可选用例名 `settings`、`history`、`transfer`、`crash`。软件模拟环境可设置 `LANCHAT_TEST_TIMEOUT_SCALE`。原始记录位于 `bin/lc-linux-002/vm/step2-current/`。两架构的 `*-experiment.5-regression.log`、`*-experiment.5-crash.log` 和 `*-experiment.5-portable.log` 是当前 ELF 回归。桌面重连记录见 `amd64-gui-reconnect-warm.log`、`arm64-gui-renderer-retry.log` 及两张 `*-gui-reconnected.png`。AMD64 的旧包升级回退证据见 `amd64-noble-matrix-retry.log` 和 `amd64-noble-candidate-matrix.log`。ARM64 glibc 2.31 的映射见 `arm64-focal-openssl11-elf-retry.log`，第五轮 APT 与 CLI 见 `arm64-focal-experiment.5.log`。

## 第 2 步实机验收（2026-11-23）

用户确认 `2.0.17~experiment.5` 已在银河麒麟 V10 国防版（020）ARM64 安装，过程顺利且无报错；桌面实际运行可刷新好友并收发文字。用户据此确认第 2 步通过。该回报没有附 `dpkg -l`、独立 CLI、回退或运行时库映射结果，因此这些输出不登记为本次实机观察。第 2 步开发侧和隔离回归的版本、命令、哈希与日志路径见上文；物理 x86_64／ARM64 的旧包升级回退、无头 CLI 和 Windows 局域网互通仍按第 4 步逐项验收。

## 第 3 步切片 · 主窗客户区（2026-11-23）

这一片只改主窗客户区，不改八个设置页、托盘、通知和维护入口，也没有生成新的试验包。`handoff-experiment.2` 至 `handoff-experiment.5` 均未改动。

已接入的行为：

- 侧栏固定 420px，宽度小于 850px 时改为 35% 且不窄于 260px。会话行高 72px，选中 `#7946D7`，群聊显示在线人数和「置顶」，未读用头像角标。
- 右键菜单文案为「发送文件...」「删除」「发送文件给所有人...」「清空群聊记录」。删除会清历史并调用 `RemovePeer`；清空只删历史。群聊文件仍然先选择一个在线接收人，不向所有节点广播文件。
- 附件菜单含选择文件、50 个表情、打开接收目录，以及不经二次确认直接清空当前会话。后者与 WinForms `ClearCurrentChat` 一致。
- 文件消息显示 340px 卡片，提供「打开文件」「打开目录」。路径只在文件消息里下发，主进程用绝对路径和 `realpath` 核验后再交给系统打开。
- 会话列表、聊天记录和传输列表使用自绘滑块：静止 4px、`#34343C`，悬停或拖拽 5px、`#646470`，无溢出时隐藏。
- Enter 发送，Ctrl+Enter 或 Shift+Enter 换行，组合输入期间不发送。WinForms 单行框会吞掉 Ctrl+Enter；这里按第 3 步已采纳的换行契约实现。
- 窗口仍使用系统边框。关闭目前会退出，因为还没有证明托盘能把同一窗口唤回前台。

回归与测量：

| 检查 | 结果 | 边界 |
| --- | --- | --- |
| `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js` | 输出 `linux desktop ui regression: ok` | 覆盖展示规则、菜单原文、样式令牌和手写文件行数。`C:\Windows\System32\node` 不执行脚本，不能用来判断通过。 |
| Chromium 视口 1040×720 | 侧栏 420×720，顶栏 96，身份胶囊 76，会话行 72，底栏 84，用户圆钮 54×54，输入胶囊高 64，选中色 `rgb(121, 70, 215)` | 页面没有预加载桥和 ELF，状态文本是「桌面桥接不可用」。这不是 Linux 桌面截图。 |
| 同一页面滑块 | 溢出后滑块可见；静止宽 4px、右边距 2px、`rgb(52, 52, 60)`；加上拖拽类后宽 5px | 只量了计算样式，没有录鼠标拖拽。 |
| Chromium 视口 800×560 | 侧栏 280px，即 `800 × 0.35` | 对齐 WinForms 小于 850px 的公式。850px 时仍为 420px。 |
| `build_linux.py`（`.NET SDK 10.0.404` 缓存） | `linux-x64` 与 `linux-arm64` 自包含 ELF 均以 0 警告、0 错误编过并校验 ELF64 机器码（62 / 183） | 使用 `bin/lc-linux-002/cache/dotnet` 构建到临时目录验证后清理。宿主机无 QEMU，`tests/linux_desktop_regression.py` 的 ELF 删除会话与历史实跑仍需在 Linux 环境或虚拟机执行。 |

独立代码审核 Round 3（2026-11-23）结论为 `APPROVED`，报告在 `CRDocs/REVIEW.md`。放行范围是主窗客户区源码。ELF 的 `delete`/`history` 仍未实跑，不记为通过。

仍未通过：Xorg、原生 Wayland、XWayland、fcitx5、ibus、125% 与 150% 同尺寸叠图、至少 2000 条历史的切换和滚动性能、托盘恢复、通知点击、维护入口，以及设置八页在 Linux 实机／桌面环境中的实跑验收。缺这些运行证据时，第 3 步保持未通过。

## 第 3 步切片 2 · 设置八页（2026-11-23）

这一片只做独立设置对话框及其八个页面，不做托盘、通知、维护入口，也不改关闭窗口退出语义；`handoff-experiment.2` 至 `handoff-experiment.5` 均未改动，LANC 封包结构未改。

已实现的结构与行为：

- 设置窗口拆为独立 Electron 模态子窗体（`settings.html`、`settings.css`、`settings-model.js`、`settings-pages.js`、`settings.js`，共用 `appearance.js`），`renderer.js` 移除旧内联 `<dialog id="settings">` 逻辑并保持主窗监听与同步；`histories-cleared` 事件会作废进行中的 `historyLoads`，防止晚到的 `history` 响应把已清空的气泡写回。基准客户区 `1260×820`，经 `computeSettingsBounds`、`computeSettingsMinSize` 与 `ready-to-show` 外框夹紧适配小工作区并居中限位；使用系统边框与 `Esc` 关闭，关闭后焦点回到主窗设置触发按钮。
- 左侧 `270px` 侧栏（`#0E0E0E`，无竖向分割线）含实时关键字搜索框及四组八页导航：`通用`、`个人主页`、`外观`、`快捷键`、`文件传输`、`聊天记录`、`设备连接`、`关于与更新`；侧栏、右侧视口与反馈文本框均使用现有 `scrollbar.js` 暗色浮动滑块并关闭原生滚动条。
- 保留已接通的昵称修改（440×260 暗色模态与 `1~80` 字符/无竖线换行校验）、自动接收文件开关（通用与文件传输页双向同步）、接收目录选择与打开文件夹、聊天记录目录迁移（选择相同目录不提示已迁移）与打开文件夹、只读配置备份及「使用备用位置」二次确认（`overridePath` / `confirmOverride`）；同时在 `DesktopService.cs` 接入 `clear-all-histories` 以响应聊天记录页「立即清空 >」暗色确认。目录选择继续走系统对话框。
- 外观四项（主题模式 `ThemeMode`、强调色 `AccentPreset`、主界面/聊天框字体族 `UiFontFamily` / `ChatFontFamily`、两套字号 `UiFontSize` / `ChatFontSize`）按 WinForms `AppSettings` 键名经 `save-settings` 落盘，并立即作用到主窗与设置窗，重启后从配置恢复；通用页「字体大小」与外观页「主界面字体大小」共享并双向同步 `UiFontSize`。快捷键页与主窗实际输入按键一致（`Enter` 发送，`Ctrl + Enter / Shift + Enter` 换行）。
- Windows 中仅更新按钮文案或静态展示的 10 项（语言、链接打开方式、网络协议通道、快捷键徽章、托盘未读悬浮预览、后台网络驻留运行、64KB 分块校验、自动加载历史消息、TUN/TAP 过滤、多网卡并发单播探测）继续只做展示与本地状态切换，不新增 RPC（其中「后台网络驻留运行」保持空回调）；`检查更新 >` 仅提示将由后续维护切片接入，`发送反馈 >` 为本地暗色反馈表单。
- `ExportLinuxDesktopAssets.cs` 增补 `Settings`、`Menu`、`Contacts`（常态与 `#8E64DC` 激活态）、`MascotProfile`、`MascotWhite` 的 GDI+ 导出；主进程和 preload 对设置窗口放行限定的设置/状态/目录 IPC，其余主窗发送、删会话与打开文件白名单仍只限主页面。

本地回归、构建与页面测量（非 Linux 桌面实跑）：

| 检查 | 结果 | 边界 |
| --- | --- | --- |
| `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js` | 输出 `linux desktop ui regression: ok` | 覆盖 `settings-model.js`、`computeSettingsBounds` 各种工作区夹紧、八页结构/令牌/白名单和全部手写文件行数（均 `< 1000` 行）。`C:\Windows\System32\node` 不能用来判断通过。 |
| `python -m py_compile packaging/build-electron.py packaging/build_linux.py tests/linux_desktop_regression.py` | 退出码 0 | 仅校验打包与测试脚本语法。 |
| `ExportLinuxDesktopAssets.cs`（Framework64 `csc.exe` 临时编译并导出至 `bin/lc-linux-002/icons/`） | 成功生成新增 PNG 图标 | 使用 `bin/lc-linux-002/baseline/windows/LanChat.exe` 作为 GDI+ 引用，临时构建目录已清理。 |
| Edge Headless（无预加载桥、无 ELF）`settings.html` `1260×820` 视口测量与八页截图 | 侧栏 `270×820` `rgb(14, 14, 14)`、搜索框 `238×34`、8 项导航（行高 `38px`）、卡片容器宽 `900px` 底色 `rgb(28, 28, 31)`、通用页 10 行均为 `54px`、开关 `40×22` `rgb(31, 214, 126)`、刻度滑块 `280×34`、各页卡片高度对齐 WinForms、无溢出时滑块隐藏 | 仅无桥接静态页面渲染与计算样式测量（状态展示为默认/未连接回退值），不是 Linux 桌面实机截图。 |
| 同一页面 `1240×600` 与 `720×500` 视口 | `1240×600` 触发溢出（`scrollHeight 726 > 600`），滑块可见且静止 `4px` `rgb(52, 52, 60)`、拖拽态 `5px`；`720×500` 侧栏缩至 `210px`、内容区 `466px`、无水平溢出 | 仅页面测量，未录制实际鼠标拖拽与系统边框高度叠加。 |
| `build_linux.py`（`.NET SDK 10.0.404` 缓存） | `linux-x64` 与 `linux-arm64` 自包含 ELF 均以 0 警告、0 错误编过并校验 ELF64 机器码（62 / 183） | 仅验证 `DesktopService.cs` 新增 `clear-all-histories` 后的双架构编译，构建到临时目录后已清理。 |

尚无 Linux 桌面／ELF 运行证据（仍未通过）：

## v2.0.18 发布（2026-11-23）

Windows WinForms 与 Linux Electron 的每文件传输气泡、系统剪贴板文件粘贴及 Linux 设置八页纳入 `v2.0.18`。Linux amd64/arm64 ELF、deb、portable tar.gz、独立 CLI 和摘要由 `Tools/Scripts/BuildLinuxDeb.py` 生成；Windows `LanChat.exe` 与 MSI 由 `Tools/Scripts/Publish.bat` 原生编译并打包。正式 Release 包含 Windows EXE/MSI 与 Linux 双架构 deb、便携包、CLI、`SHA256SUMS.json`，共 9 个资产；逐项核对 GitHub 状态、文件大小和 SHA-256。Release 与代码提交：[`v2.0.18`](https://github.com/yxiaojiang/LanChat/releases/tag/v2.0.18)，`6a11ea04b2c71ba89e9042e25b80955a1741506e`。

`lanchat_2.0.18_arm64.deb` SHA-256 为 `92874c84da1032f78160f87015c312c65aedb21faefb8df736ac71fecaaba8cd`。本轮未启动虚拟机；ARM64 实机安装、剪贴板和 Windows 覆盖安装由用户验收。此前记录的 Xorg/Wayland、输入法、托盘与通知、长历史性能、物理双架构升级回退及互通验收仍未通过。

- Linux Electron 实机运行下点击左下角芯片拉起独立设置 `BrowserWindow`、模态居中与工作区适配、关闭回焦主窗，以及与 `DesktopService` 联动的真实保存、只读备用确认、历史迁移冲突提示、`clear-all-histories`、系统 GTK 目录选择器和打开目录动作。
- Xorg、原生 Wayland、XWayland 三种后端表现，fcitx5 / ibus 输入法在昵称与反馈输入中的预编辑行为，以及 100% / 125% / 150% 同尺寸叠图核对。
- `tests/linux_desktop_regression.py` 的 `delete`、`history` 及本切片新增 `clear-all-histories` 在 Linux 网络命名空间对最新 ELF 的实跑（宿主机无 QEMU）。
- 本切片代码审核尚未进行（`CRDocs/STATUS.md` 仍记录切片 1 状态）。
