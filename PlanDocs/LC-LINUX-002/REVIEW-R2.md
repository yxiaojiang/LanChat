# 审核

**LC-LINUX-002 · 第 2 轮 · 审核方 Grok · 2026-11-20**

审核对象为 [第 2 轮提案](PROPOSAL-R2.md)。结论为 `CONVERGED`。R1–R4 都有落点。与提案原文相比，启动前按本轮「顺序」补上三条：设置文件按旧程序的固定顺序选定，第 4 步缺证据不算通过，升级演练本身不搬动历史目录。

## 本轮核对了什么

对照 [第 1 轮审核](REVIEW-R1.md) 的 R1–R4，核对第 2 轮的处理、桌面等效表、数据表、实机矩阵和规则替换文字。重读了这些源码：

- [storage.py](../../src/linux/lanchat_app/storage.py) 第 92–115 行的设置路径，以及 [history.py](../../src/linux/lanchat_app/history.py) 第 107–119 行的历史目录。
- [AppSettings.cs](../../src/Services/AppSettings.cs) 第 68–80 行、[ChatHistoryService.cs](../../src/Services/ChatHistoryService.cs) 第 48–58 行。
- [NetworkService.cs](../../src/NetworkService.cs) 第 171–186 行的 `GetUniqueFilePath`，以及 [NetworkService.Transfer.cs](../../src/NetworkService.Transfer.cs) 第 137–158 行的接收落盘。
- [MainForm.Tray.cs](../../src/MainForm.Tray.cs) 的关闭隐藏、托盘还原和光标预览。

重新计算了候选产物的 SHA-256：

| 文件 | SHA-256 |
| --- | --- |
| `bin/lanchat-step2/lanchat_2.0.16_amd64.deb` | `87b1bbb912993ed8e6d737888dca79239b6282dd235e32412a03a537a6ee366c` |
| `bin/lanchat-step2/lanchat_2.0.16_all.deb` | `3ff0b098d78ab1ba6a3c4d785bd45f0d39ab1d0b248036e01cf83a4520199d99` |
| `bin/lanchat-step2/lanchat.pyz` | `f962dffba0a7e2f660be62a72702a809c5c00927b0e1963f1e53b9b768260dca` |
| `bin/lanchat-handoff/LanChat-LC-LINUX-001-candidate.zip` | `a14a885fc858b8b6eb0f22e7cbe881b8aa5ba5c6885090f9031ed07eacd998fd` |

前两份与 [实施验证](../LC-LINUX-001/VALIDATION.md) 的记录一致，压缩包与同级 `.sha256` 一致。候选 deb 内是 `lanchat.pyz`；`all` 包没有 ELF，`amd64` 包另有 `lanchat-bin`。两份的 `Depends` 都是 `python3`，`python3-tk` 是 `Recommends`。交接包内还有 `linux/legacy/` 的 `2.0.15` 与 `2.0.16` amd64 包。

本轮没有安装这些包，没有 Wayland 会话，也没有第二台电脑。

## 属实

| 意见 | 第 2 轮处理 | 本轮核对 |
| --- | --- | --- |
| R1 | 部分采纳：按实际后端验收，托盘不能确认时保留恢复入口 | 等效表要求隐藏前先证实托盘能唤回窗口；恢复看窗口是否可回到前台，不把通知点击当成唯一路径。原生 Wayland 用运行证据确认 Ozone，不只看 `XDG_SESSION_TYPE`。Xorg 光标预览单独取证。 |
| R2 | 部分采纳：包和数据分开演练，版本高于 `2.0.16`，回退要启动旧 GUI | 公开包与候选包的载荷不同，前者是 `lanchat.py`，后者是 `lanchat.pyz`。同级 `settings.json` 优先，安装默认 `~/.config/lanchat/settings.json`，历史默认 `~/.local/share/lanchat/ChatHistory`。`~/.config/LanChat` 被排除出 Linux 默认路径。回退命令是 `apt install --allow-downgrades`，并要求 Tk 真正把旧界面拉起来。 |
| R3 | 部分采纳：首包集成与双架构实机分开 | 第 1 步明确写入集成记录，可用虚拟机。第 4 步要求物理 x86_64 和物理 ARM64 各自升级、回退，并与物理 Windows `v2.0.16` 做局域网。ARM64 的 Wayland 不由 x64 结果填上。 |
| R4 | 部分采纳：第 5 节给出三处替换文字 | 三句都可直接替换。第 9 条把圆角交给 CSS 或等效绘制，并按同一尺寸验收。第 10 条替换的是 Linux 分发段，手写源码的拆分上限仍在。 |

Windows Electron 仍在「明确不做」。共享收件的静态判断成立：`GetUniqueFilePath` 先看目标名是否存在，接收端随后才创建同名 `.downloading` 文件，中间没有原子占位。两路同名接收可能选中同一路径。提案把它列为第 2 步实测，没有写成已经复现。

## 不成立或说重了

**「两处设置都有数据就提示选择」说宽了。** 旧程序的顺序是固定的。同级 `settings.json` 已经存在时用它；便携目录可写时也在同级创建。否则只要能创建 `~/.config/lanchat`，就用那里的 `settings.json`，即使 `~/.local/share/lanchat/settings.json` 同时存在。后者只在配置目录创建失败时使用。两处都有文件，并不等于无法确认旧入口。

**「实机门槛缺证据时记录为尚待验证」不能使候选通过。** 第 4 步已经要求两个实机格和承诺的核心矩阵都有通过证据。矩阵里的中文输入、TUN、多网卡、无默认路由，以及已承诺的 `v2.0.15` 和旧 ELF 互通，缺一格就停在第 4 步。KDE、额外分数缩放和未列入支持声明的发行版可以保持未覆盖，并写明未测。

## 遗漏

下面三条提案没有单独写死。本轮在「顺序」里补上，不再要求第 3 轮提案。

1. **新版不发布 `Architecture: all`。** 安装包只有 `amd64` 与 `arm64`，各含本架构的 Electron 和 ELF。若保留 `LanChat.deb` 这个文件名，它只作为 amd64 包的副本，arm64 使用另一个文件名。
2. **采纳本提案即批准自绘窗框的回退。** 第 3 步若在原生 Wayland 上不能完成拖动、缩放、最小化、最大化和关闭，就使用系统边框，客户区继续对齐 Windows。该步不另等一轮方案。
3. **升级演练不搬动历史目录和已接收文件。** 用户日后在设置中迁移历史，按 Windows 基线另测，并记录旧 Python 不读取 `ChatHistoryDirectory`。第 2 步的回退样本保持原目录，旧 GUI 要看到原记录以及新版追加的记录。

## 顺序

按下列顺序启动。桌面集成表里的托盘、预览、贴靠和任务栏，以第 2 轮等效表和本轮补充为准。

| 步骤 | 交付和放行条件 |
| --- | --- |
| 0. 固定基线与契约 | 维持提案。冻结 Windows 清单、等效表、支持矩阵和规则文字，保全公开包、候选包、交接包和一份含设置、历史、接收文件的便携样本。设置识别使用旧程序的固定顺序。 |
| 1. 打通首包 | 维持提案。一个架构的实机或虚拟机完成 GUI、ELF、一次文件校验、退出和无头 CLI，并生成两架构包、核对机器码。结果只记为集成，不放行架构。 |
| 2. 数据、升级与回退 | 维持提案的演练范围和并发同名收件。设置写入落在旧程序回退后会读取的那个文件；旧路径只读时才改到用户确认的可写位置，并先备份。先试 `apt install ./新包.deb`。只有在隔离环境证明卸载再安装不丢失用户数据、旧入口和旧 GUI 仍可用之后，才把该路径写成交付步骤。回退使用 `apt install --allow-downgrades`，并实际启动旧 GUI。 |
| 3. 界面与桌面等效 | 维持提案。原生 Wayland 与 Xorg 分开记录 Ozone。通知不可用时，应用内未读和窗口恢复仍然可用。自绘窗框按本轮遗漏第 2 条处理。 |
| 4. 双架构正式候选验收 | 维持提案的两台物理机、物理 Windows `v2.0.16`、升级回退和承诺矩阵。缺证据的承诺格保持未通过。未列入支持声明的组合只记录为未测。其后进入代码审核；公开发布仍等用户指令。 |

Windows Electron 不进入本顺序。

## 未核实

本轮没有运行 apt、没有桌面会话，也没有物理双机。Wayland 下托盘是否可见、通知能否点击、同名架构切换能否直接完成，仍按第 2、3、4 步取证。并发同名收件只做了源码阅读，没有发送两路文件。

## 结论

**`CONVERGED`。** 可以按上面的顺序启动第 0 步。用户采纳前不改业务代码，也不发布。采纳时把第 5 节的三处文字写入项目规则，并把本轮三条补充写入执行计划。
