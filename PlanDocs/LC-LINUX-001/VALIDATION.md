# LC-LINUX-001 实施验证

## 1.1 旧版回归基线（2026-11-19）

基线为 `main` 的 `2957dcf`。Windows 宿主使用 Python 3.13 和 Tk 8.6，以临时配置、接收目录、回环地址和分段连接替身运行 `python -m unittest discover -s tests -p test_linux_flow.py -v`。8 个用例均复现故障，其中 1 个异常、7 个断言失败。

| 用例 | 旧版行为 | 修复后的期望 |
| --- | --- | --- |
| 群聊有首条系统消息 | 重绘抛出 `KeyError: is_self` | 正常显示系统消息 |
| 注入渲染异常 | 历史控件留在可编辑状态 | 恢复只读 |
| 离线私聊 | 删除输入内容 | 保留草稿并在原会话提示 |
| 学习文字发送者 | 将 UDP 确认的 `41907` 和 `workstation` 改成 `38889` 与 `LAN-Host` | 保留确认的端口和主机名 |
| TCP 魔数分段 | 收端提前返回 | 读满并确认正常帧，截断帧不确认 |
| 文件长度分段 | 收端未发送接受或完成回执 | 长度读满，文件落盘后确认 |
| 同名文件发送者 | 归到列表中第一个同名节点 | 用发送者 ID 与连接地址归档 |
| 文件接收方省略最终回执 | 无失败通知，并触发完成进度 | 发起任务收到缺最终确认终态 |

当前机器的 WSL 子系统不可用，Linux 图形会话、Windows／Ubuntu 双机和 ARM64 实机仍待验证。

## 步骤 1 本机检查点（2026-11-19）

- `src/linux/lanchat.py` 修复草稿、只读恢复、操作终态、选人、节点路由与 TCP 文件收发。共享 `NetworkService.Discovery.cs`、`NetworkService.Transfer.cs` 保留文字发送者已知的端口和主机名。
- `tests/test_linux_flow.py` 的 14 项均通过。使用 `tests/LinuxLoopback.cs` 编译共享核心，Python 和 C# 在不同的非默认回环端口双向收发中文。将同一夹具改为编译 `2957dcf` 的共享核心，复现 `38889 LAN-Host`，回归按预期失败。
- 在隔离构建副本运行 `Tools/Compile.bat` 通过。`build-linux.bat` 从临时中间目录构建 x86_64、ARM64 ELF，`build-deb.py` 生成 amd64 和 all 候选包。输入缺失时退出 1，未生成包。
- 候选包位于忽略目录 `bin/lanchat-step1/`。amd64 包内 ELF 与当轮 x64 构建同字节，SHA-256 为 `76f65daa872e976da1ef7db3105885981dad57192b89b99c77e579a491ab6710`。ARM64 ELF 的 SHA-256 为 `885f0cabfb7b7e641ac2244845cb3dd44f9c8c4d0ee0170e71f418d718dd2e07`。all 包的桌面项和启动器指向包内 Python 源码，包内容按字节核对。
- 1.6 仍待 Ubuntu/Windows 双机和 ARM64 实机，覆盖新旧版、旧 ELF、真实发现与文件 SHA-256、拒收与中断、桌面/SSH/缺 Tk、路由与 TUN。独立代码审核待对应矩阵完成。当前 ELF 证据为跨架构构建和格式核对。

## 步骤 2 本机检查点（2026-11-19）

- `src/linux/lanchat_app/` 按职责拆分入口、CLI、网络、Tk、会话和存储。最长手写 Python 源文件 `gui.py` 为 960 行。包装器 `src/linux/lanchat.py` 保留源码启动。
- `packaging/build-deb.py` 生成便携 `.pyz` 和两个 Debian 包。两包的桌面项、启动器和包内归档已核对，启动脚本通过 Git Bash 语法检查。原生编译脚本与两架构 ELF 构建通过。模拟缺少 .NET、缺少 ELF 或架构错误均返回非零，且没有候选输出。
- 22 项回归通过。含真回环 C# 双向中文与 311071 字节文件，文件 SHA-256 一致；组内选人、短读、拒收、缺 ACK、超时、并发同名、UDP 子网与直连公告覆盖也通过。no-route 和 TUN 使用接口及 UDP 模拟样例。
- 用 `v2.0.15` 标签和 `a0e49c1`（v2.0.16）源代码原生编译共享核心，与候选 Python 在 Windows 回环双向传输。Python 使用 38889 时，两版均收发中文及文件；Python 使用非默认端口时，两版均复现旧核心覆盖端口和主机名。公开安装版双机验收仍待办。
- `tests/LinuxHistoryInterop.cs` 使用 Windows 实际历史服务双向核验中文、引号、反斜杠、换行、其他 C0 控制字符、文件路径、毫秒时间戳及缺字段旧行。另验损坏尾行、重载无重复、未确认传输、同名节点、直连归并、昵称和不可写目录回退。
- 隔离便携目录的 `.pyz` 能读既有昵称和自动接收配置，Windows Tk 装载归档内图标及历史。显式 CLI 与模拟 Linux 无头均未导入 Tk，模拟图形环境缺 Tk 时提示 `python3-tk` 并退出 1。
- 2.5 保持待验。当前宿主缺少可用 Linux 环境，尚未在 `/usr/lib/lanchat` 实际安装及升级、经 SSH 启动、从桌面图标验证缺 Tk 提示，也未运行两架构 ELF。双机、公开旧安装版、默认路由与 TUN 的现场矩阵同样待验。

最终本地候选位于 `bin/lanchat-step2/`。版本字段沿用测试基线 2.0.16。两架构 ELF 在本轮重新生成，amd64 包中的 ELF 与本轮 x64 文件同字节。

| 产物 | SHA-256 |
| --- | --- |
| x64 ELF（含 amd64 包内 ELF） | `97fa913c81826db5e89606facf833b494ad766ae13cbeded6363a8f0011b5d53` |
| ARM64 ELF | `79ad6af5d99b201881707970142941882d95ef5788128a803b0991aeaf681873` |
| lanchat.pyz | `f962dffba0a7e2f660be62a72702a809c5c00927b0e1963f1e53b9b768260dca` |
| amd64 .deb | `87b1bbb912993ed8e6d737888dca79239b6282dd235e32412a03a537a6ee366c` |
| all .deb | `3ff0b098d78ab1ba6a3c4d785bd45f0d39ab1d0b248036e01cf83a4520199d99` |

## 步骤 3a 原型检查点（2026-11-19）

`tests/linux_ui_prototype/` 含两种消息视图和 GDI+ 资源生成器。Windows Tk 8.6.15 跑通 2000 条消息测量、两组资源和无资源回退。实际 Linux Xorg／Wayland、两种中文输入法、桌面分数缩放、截图和最终选型待补。步骤 3b 等 2.5 和 3a 通过。 [原型数据与验证步骤](TK-PROTOTYPE.md)

## Ubuntu 虚拟机补测（2026-11-19）

使用 QEMU 10.2.0 的 TCG 在当前 Windows 宿主启动 Ubuntu Minimal 24.04.5 LTS x86_64 与 aarch64。Debian 包及 ELF 使用上表候选产物。网络为 QEMU 用户态 NAT，SSH 端口仅转发到 Windows 回环。实体设备及双机局域网验收保留待办。 [QEMU 分发](https://www.qemu.org/download/) · [Ubuntu 镜像](https://cloud-images.ubuntu.com/minimal/releases/noble/release/)

| 虚拟机 | 已验证 |
| --- | --- |
| x86_64 | v2.0.15 至 v2.0.16 再覆盖候选 amd64 包，两个旧包与候选的 Python CLI 均可启动。两个旧 x64 ELF 和候选 x64 ELF 均可启动。安装的候选 x64 ELF 摘要与构建输出一致。 |
| aarch64 | 安装候选 all 包，执行新 ARM64 ELF 并核对摘要。真实 Linux 内核中创建 TUN、删除默认 IPv4 路由后，通过 UDP 实包验证同子网优先采用源地址，跨网段 TUN 源按合法公告回退。检查后恢复来宾网络。 |
| 两种架构 | 通过非交互 SSH 启动无头 CLI、显式 `--cli` 和可执行便携 `.pyz`。图形环境变量已设置但未安装 Tk 时提示 `python3-tk` 并退出 1。安装及便携路径读取既有配置，安装历史读取旧 JSONL 时间戳，便携模式创建同级接收目录。 |

- x86_64 的同一虚拟机中，公开 v2.0.16 旧 ELF 与候选 ELF 在一对独立 Linux 网络命名空间里通过 UDP 发现。两方向私聊、群聊与 311071 字节文件 SHA-256 均通过。首次发现并选中对方约 7.64 秒。
- Windows 宿主与设备模拟器通过 NAT 和 SSH 端口转发，验证了候选 C# 与已安装 Python 的双向文字和 Python 至 C# 的文件确认。反向测试使用关闭自动接收的 Ubuntu 账号，接收成功仍待验证。

## 实机验收交接（2026-11-19）

按用户安排，实机验收由用户执行。`bin/lanchat-handoff/LanChat-LC-LINUX-001-candidate.zip` 汇集两种候选 Debian 包、便携版、两架构 ELF、原生编译的 Windows 程序、两个对照版本、Tk 原型及资源、测试文件和验收说明。ZIP 通过解压校验，内部 75 个交付文件均列入 SHA-256 清单，便携版和 ELF 保留可执行权限。整包摘要放在同级 `.zip.sha256`。
