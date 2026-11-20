# 审核

**LC-LINUX-002 · 第 1 轮 · 审核方 Grok · 2026-11-20**

审核对象为 [第 1 轮提案](PROPOSAL-R1.md)。结论为 `CHANGES_REQUESTED`。Electron 负责 Linux 桌面、网络仍走自包含 ELF、Windows Electron 留到 Linux 验收之后，这三件事可以保留。下面 R1–R3 会改变第 0、1、2、4 步的放行条件，需要写进下一轮提案。本轮没有 Linux 桌面，也没有执行安装或回退。

## 本轮核对了什么

对照提案 D1–D3、构建与换代、未核实表和建议顺序，核了这些源码和已有安装包：

- [packaging/build-deb.py](../../packaging/build-deb.py)：`VERSION = "2.0.16"`，只产出 `amd64` 与 `all`，没有 `arm64` 包。控制字段没有 `Replaces`、`Conflicts`、`Breaks`。启动脚本在有 `python3` 时执行 `lanchat.pyz`。
- `release/` 里现有的三份包。`lanchat_2.0.16_amd64.deb` 与 `LanChat.deb` 都是 6097690 字节，控制字段同为 `Package: lanchat`、`Version: 2.0.16`、`Architecture: amd64`、`Depends: python3`。包内是 `/usr/bin/lanchat`、`/usr/lib/lanchat/lanchat.py`、`lanchat-bin` 和 `lanchat.desktop`。`lanchat-bin` 为 ELF64，机器码 62（x86-64），14967951 字节。`lanchat_2.0.16_all.deb` 同一版本和依赖，没有 ELF。桌面项都是 `Exec=lanchat`。维护脚本只有 `postinst`。
- 公开包内的 `lanchat.py`：安装路径在 `/usr`、`/etc`、`/opt` 下时，设置写 `~/.config/lanchat/settings.json`，接收目录用 `~/Downloads/LanChat_Received`。便携时用脚本同级的 `settings.json` 和 `ReceivedFiles`。键为 `NickName`、`AutoAcceptFiles`。这份公开脚本没有聊天记录文件。
- 候选源码 [storage.py](../../src/linux/lanchat_app/storage.py)、[history.py](../../src/linux/lanchat_app/history.py)：安装形态的历史在 `~/.local/share/lanchat/ChatHistory`，便携形态在归档同级 `ChatHistory`。
- [AppSettings.cs](../../src/Services/AppSettings.cs) 第 68–80 行、[ChatHistoryService.cs](../../src/Services/ChatHistoryService.cs) 第 48–58 行：无同级 `settings.json` 时使用 `ApplicationData/LanChat`。在 Linux 上的 .NET 里，这个目录是 `~/.config/LanChat`，历史默认落到 `~/.config/LanChat/ChatHistory`。
- [MainForm.Tray.cs](../../src/MainForm.Tray.cs) 第 226–306、498–530 行：关闭即隐藏到托盘；左键或双击还原；右键菜单含退出；未读时在光标处弹出预览。
- [LC-LINUX-001 验证记录](../LC-LINUX-001/VALIDATION.md) 的虚拟机一节：x86_64 覆盖的是 amd64 包；aarch64 安装的是 `all` 包，ELF 另行执行。没有 arm64 包，没有桌面会话。
- Electron 文档与问题单：Linux 托盘走 StatusNotifierItem，`Tray.getBounds()` 只在 macOS 和 Windows 提供，点击坐标在 Linux 上为 `(0, 0)`。[托盘 API](https://www.electronjs.org/docs/latest/api/tray)。原生 Wayland 下 `setPosition` / `setBounds` 不能移动窗口，`getPosition()` 返回 `[0, 0]`；Electron 38 起 Wayland 会话默认走原生 Wayland。[electron#40886](https://github.com/electron/electron/issues/40886)、[electron#48833](https://github.com/electron/electron/issues/48833)。

仓库里没有找到 LC-LINUX-001 的候选 deb、便携包或交接压缩包。

## 属实

- 桌面改用 Electron、局域网套接字仍只由自包含 ELF 打开、保留独立 CLI，和现有 [LanChat.Linux.csproj](../../src/linux/LanChat.Linux/LanChat.Linux.csproj) 的链接方式相符。桌面服务的进程协议确实还没有实现。
- Windows Electron 放在「明确不做」，等 Linux 候选验收后再单独立项。本轮不加入这条迁移，也不把它写进顺序。
- 公开包与候选包的版本号都是 `2.0.16`。ARM64 今天没有对应架构的 deb，能装上的是 `Architecture: all`。提案把 `all` → `arm64`／`amd64`、便携目录和回退列为必须演练，这个范围是对的。
- 原生 Wayland 下窗口坐标和激活由合成器决定，托盘可能不存在。这两句判断成立。Windows 关闭到托盘、光标处预览、系统贴靠，不能原样搬过去。
- [VALIDATION.md](../LC-LINUX-001/VALIDATION.md) 里的 Ubuntu 结果是 QEMU 用户态网络和 SSH，不是物理局域网，也不是桌面。提案没有把它们写成已经通过，这点准确。

## 不成立或说重了

**「至少一条真实链路，其余架构与桌面实机分别登记」不能作为架构放行。** 第 1 步可以靠一条链路继续做集成。第 4 步的「关键组合通过」没有列出哪一格缺失就停止。按现有文字，x64 走通之后，ARM64 只登记也可以往下称为候选。QEMU 里的安装同样不能填进实机格。

**「延续旧安装与便携选定的位置」和直接复用 C# 路径不是同一件事。** `AppSettings` 与 `ChatHistoryService` 在 Linux 上会读写 `~/.config/LanChat/`。已安装的 Python 使用 `~/.config/lanchat/settings.json` 和 `~/.local/share/lanchat/ChatHistory`。公开版 `lanchat.py` 甚至不写历史文件。不先定路径表，第 2 步会新建另一套目录，回退时旧程序看不到新写入的内容。

**「建议用系统通知代替托盘预览」还不是等效决定。** 对齐表仍把系统贴靠、任务栏区域和托盘预览列为应对齐行为。原生 Wayland 上这些坐标不会随 Electron 版本补齐。XWayland 下能定位，也不能记成原生 Wayland 通过。

## 遗漏

### R1 · Wayland 等效操作要先定表

影响第 0 步和第 3 步。最小修正是把下表写进决定，第 3 步只验证这张表。

原生 Wayland 行的条件：`XDG_SESSION_TYPE=wayland`，且 Electron 的 Ozone 平台是 `wayland`。会话是 Wayland、进程却是 X11 时，结果记入 Xorg／XWayland，不能填原生 Wayland 行。托盘可用的含义是：用户看得见图标，并且一次激活能把同一窗口带到前台。`Tray` 构造成功不算。

| 用户动作 | 托盘可见 | 托盘不可见 |
| --- | --- | --- |
| 关闭窗口 | 隐藏窗口，ELF 与监听保持。发一条系统通知，说明仍在运行、如何打开、如何退出。 | 不隐藏。最小化，Dock 或任务栏保留入口。 |
| 再次打开 | 托盘激活、Dock、再次执行 `lanchat` 三种途径都打开同一窗口，网络不重启。以窗口到前台为准。 | Dock，或再次执行 `lanchat`。 |
| 真正退出 | 托盘菜单和窗口内都有退出。发送离线并释放端口。托盘菜单用系统 StatusNotifier 菜单。 | 窗口内退出，要求相同。 |
| 未读 | 系统通知的默认动作打开对应会话。不做跟随光标的预览，也不要求图标闪烁。 | 同一条通知。窗口留在 Dock。 |
| 最小化、最大化、还原、贴靠、跨屏坐标 | 由合成器处理。程序不调用 `setPosition` 或 `setBounds` 去模仿。 | 同左。 |

本次会话还没有证明托盘可见且能唤回窗口时，关闭按「托盘不可见」处理。Xorg 可以另行尝试光标预览，那不是 Wayland 行的通过条件。自绘标题栏只在原生 Wayland 上拖动、缩放、最小化、最大化和关闭都可用时保留；否则使用合成器边框，客户区仍按 GrokBot。

### R2 · 升级与回退要分成包和数据两件事

影响第 0 步和第 2 步。

数据就地使用，不搬目录：

| 形态 | 设置 | 历史 | 接收文件 |
| --- | --- | --- | --- |
| 安装在 `/usr`、`/etc`、`/opt` | `~/.config/lanchat/settings.json`。仅当该文件不存在、而 `~/.local/share/lanchat/settings.json` 存在时读后者。 | `~/.local/share/lanchat/ChatHistory`，就地追加。公开版没有这个目录，表示本来就没有历史文件。 | 设置中没有接收目录时，用 `~/Downloads/LanChat_Received`。 |
| 便携 | 用户保留的目录中的 `settings.json` | 同一目录中的 `ChatHistory`。公开版没有该目录时不另建第二套。 | 同一目录中的 `ReceivedFiles`。 |

便携根目录是含这些数据的目录。ELF 或 Electron 位于子目录时，不用子目录做数据根。保留 `NickName` 和 `AutoAcceptFiles`；可以增加键。第一次改写 `settings.json` 之前，在同目录复制 `settings.json.bak-<新版本>`。不移动已有 jsonl 和已接收文件，不改写其中的绝对路径，不把历史目录改到别处。旧进程与新进程不同时写同一目录。

包的规则：

- 包名保持 `lanchat`。Debian 版本必须大于 `2.0.16`。新版本只发布 `amd64` 与 `arm64`，不再发布 `Architecture: all`。一个文件名不能同时代表两种架构；现有 `LanChat.deb` 就是 amd64 包的副本。
- `/usr/bin/lanchat` 替换后不调用 `python3`、`lanchat.py` 或 `lanchat.pyz`。去掉 `Depends: python3` 和 `Recommends: python3-tk`，改为 Electron 实际需要的库。安装用 `apt install ./包.deb`。
- 维护脚本不删除 `~/.config/lanchat`、`~/.local/share/lanchat` 和 `~/Downloads/LanChat_Received`。
- 升级样本至少包括：`release/lanchat_2.0.16_amd64.deb`（拥有 `lanchat.py` 与 `lanchat-bin`）、`release/lanchat_2.0.16_all.deb`（无 ELF），以及 LC-LINUX-001 候选包（当前脚本拥有 `lanchat.pyz`，与公开包的文件清单不同）。候选包现在不在仓库里，第 0 步先保全。
- 通过时，`dpkg -l` 只剩新架构的一个 `lanchat`，桌面项仍是 `Exec=lanchat`。错误架构的包被 dpkg 拒绝。
- 回退先退出新进程，再执行 `apt install --allow-downgrades ./旧包`。x64 回到旧 amd64 包，arm64 回到旧 `all` 包。旧依赖 `python3` 与 `python3-tk` 由这条命令装回。旧入口能读原设置；候选版历史仍在原来的 jsonl。若旧程序读不了改写后的设置，先恢复 bak 再启动。包回退不会把被搬走的数据搬回来，所以数据本就不搬。

虚拟机可以演练 dpkg，结果标明虚拟机。它不代替第 4 步的实机。

### R3 · 两架构的实机放行

影响第 1 步的出口和第 4 步。

第 1 步允许用一台机器或虚拟机打开窗口、接上 ELF、完成一次传输，并核对两份包内 Electron 与 ELF 的机器码。这一步不放行 amd64，也不放行 arm64。

第 4 步缺任一架构的实机，候选就不通过。每台实机都不是 QEMU 或 WSL，并且同时满足：

1. `dpkg --print-architecture` 与包一致。包内 Electron 和 ELF 都是该架构：x86-64 机器码 62，aarch64 机器码 183。另一架构的二进制不在包内。
2. 按 R2，从该架构真正的前代包装升级。arm64 的前代是 `all` 包，不是 amd64 包。
3. 有本地显示器。x64 的原生 GNOME Wayland 按 R1 通过。arm64 没有 Wayland 会话时，不能用 x64 的结果填格，arm64 图形放行不成立。
4. 与一台运行公开版的物理 Windows 在真实局域网完成发现、双向文字、一个文件的 SHA-256，退出后端口释放。虚拟机 NAT 不算。
5. 在这台实机上完成一次 R2 回退。

TUN、多网卡、`v2.0.15`、KDE、arm64 分数缩放、两种输入法以及两台 Linux 互聊，缺了就保持未覆盖，不因此把已通过的实机格改判失败，也不能省略不写。

### R4 · 规则替换文字

提案要求本轮确认 [Rules.md](../../.claude/Rules.md) 第 4、9、10 条的替换文字，但正文里还没有可粘贴的句子。采纳时用这三句：

- 第 4 条的 Linux 要求改为：Linux 交付 x86_64 与 ARM64 两套自包含 ELF；桌面和新的命令行入口使用对应 ELF。新安装包和便携包不依赖 Python 运行时。已安装的 Python 包只作升级来源和回退对照。
- 第 9 条的 Linux 落地改为：应用内界面遵循同一视觉规格。图标和圆角在构建期生成，由 Electron 加载。文件和目录选择用系统对话框。应用内菜单、提示和接收者选择用暗色叠加层。原生 Wayland 的托盘菜单、窗口位置、贴靠和任务栏按 R1 的等效操作验收。
- 第 10 条补充：手写 JavaScript、CSS，以及 Linux 桌面服务继续修改的 C# 业务文件，适用 1000 行预警和 1500 行上限。Electron 本体、依赖目录和打包产物不计入。

## 顺序

按此顺序修订后再启动。Windows Electron 不进入本顺序。

| 步骤 | 交付和放行条件 |
| --- | --- |
| 0. 固定基线与三份契约 | 保留提案中的 Windows 截图和操作清单，并保全公开包、候选包和一份带设置、历史、接收文件的便携目录。同时写入 R1 等效表、R2 路径与回退、R3 实机矩阵、R4 规则文字。 |
| 1. 打通首包 | 一个架构上窗口、ELF、一次传输和无头 CLI 跑通，可用虚拟机。两份架构包都生成，包内 Electron 与 ELF 机器码正确，启动器不调用 Python。不放行架构。 |
| 2. 数据、升级与回退 | 按 R2 就地读写。在 Ubuntu 22.04 与 24.04 上演练公开包、候选包和便携目录的升级与 `apt install --allow-downgrades` 回退。虚拟机结果单独标注。 |
| 3. 界面与桌面等效 | 应用内界面按第 0 步清单交付。原生 Wayland 与 Xorg 按 R1 取证，记录 Ozone 平台。标题栏做不到契约中的操作时使用合成器边框。 |
| 4. 双架构实机验收 | R3 的五条在 x64 与 ARM64 实机上都有结果。缺一架构则候选不通过。其后才进入代码审核；公开发布仍等用户指令。 |

## 未核实

本轮没有安装这些 deb，没有做架构切换或降级，没有 Wayland 或 Xorg 会话，也没有第二台电脑。Electron 的坐标和托盘限制来自上述文档与问题单，不是本机实测。LC-LINUX-001 候选安装包不在当前工作区。`all` 升到更高版本的 `arm64` 时，Ubuntu 22.04／24.04 的 apt 是否直接替换同一包名，要等第 2 步的演练；演练失败就停在第 2 步，不把该格写成已通过。

## 结论

**`CHANGES_REQUESTED`。** D1–D3 的方向保留，Windows Electron 不在本案实施。下一轮提案写入 R1–R4 和上面的顺序后再交接。用户采纳前不改业务代码，也不发布。
