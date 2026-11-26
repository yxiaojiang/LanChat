# 延期与跨环境验证清单 (DEFERRED)

> 这里只记录已经明确不属于当前 P0/P1 修复批次、或需在 Linux 实机/虚拟机环境才能验收的条目。进入对应阶段时，应迁回 `REVIEW.md` 并改为 `OPEN`。

## 阶段 5 Linux 实机与真实 LAN 总验收清单（准备版）

> 本清单只定义现场执行顺序和证据要求，不改变下方延期项状态。每项核销必须记录日期、主机型号、发行版/桌面会话、架构、命令输出、日志和必要截图；Windows Mock、Node DOM/IPC 桩和交叉编译不能替代 Linux 实机或物理 LAN 证据。

### 1. CP-0：Linux ELF 与共享后端基线

- [ ] 在 Linux 网络命名空间或批准的 Linux 实机上使用候选包内最新 `lanchat`，记录 `uname -a`、`dpkg --print-architecture`、运行库信息和 `readelf -h`；确认 x86_64 为 ELF64 `Machine: Advanced Micro Devices X86-64`/62，ARM64 为 ELF64 `Machine: AArch64`/183。
- [ ] 对两个架构分别运行 `tests/linux_desktop_regression.py <最新 lanchat ELF>` 全量用例：外观 6 项持久化重启、只读设置取消/确认、`clear-all-histories`、`history`（含冲突目标目录拒绝迁移）、`delete`、`transfer`、`crash`。
- [ ] 核对启动/退出后端进程、端口和文件句柄释放；确认安装版/便携版/独立 CLI 不依赖 Python，保存 stdout/stderr 和退出码。

### 2. CP-1：真实桌面集成、托盘与通知

- [ ] 在 GNOME Xorg、原生 GNOME Wayland、GNOME XWayland 分别启动安装版和便携版；记录桌面会话、DPI、Electron 版本和窗口截图。
- [ ] 验证 StatusNotifierItem 托盘可见、无 AppIndicator 时最小化不失联、关闭/恢复同一窗口、未读预览、通知点击激活、Xorg 闪烁/预览；不能用“进程仍在”替代托盘恢复证据。
- [ ] 验证设置窗打开/居中/关闭回焦、GTK 目录选择器、主题/强调色/字体/字号即时变化与重启保持；补测 420px 常规侧栏和 260px 窄窗底栏刷新按钮布局。

### 3. CP-2B：富文本、IME、剪贴板与 DPI

- [ ] 在 Xorg、Wayland/XWayland 下分别使用 `fcitx5` 与 `ibus`，验证中文预编辑/确认期间 Backspace、Delete、Enter 不被编辑器抢占；记录输入法版本和会话环境。
- [ ] 验证原生 textarea 默认路径与一键回退；显式启用富文本后验证 PNG 表情与 11pt 正文基线、点击表情左右收拢、Backspace/Delete 整枚删除、copy/cut 还原 `[表情名]`、嵌套多行光标/粘贴偏移。
- [ ] 使用真实系统剪贴板验证普通文本、PNG/JPEG、文件管理器路径及右键“粘贴/粘贴附件”；确认系统权限拒绝不会阻断主进程受控 IPC，且渲染层不能提交任意本地路径。
- [ ] 在 100%/125%/150% DPI 逐项复测输入基线、光标、表情尺寸、选区、滚动、焦点恢复；保存截图和复现步骤。

### 4. `LC-LINUX-003-S4-ENV-001`：在线更新、安装、退锁与回退

- [ ] 在物理 x86_64 与 ARM64 上分别核对安装版/便携版资产、`SHA256SUMS.json`、包内 ELF 机器码、`dpkg -l`、桌面入口和依赖；验证缺失/篡改摘要或资产时阻断，不得静默下载。
- [ ] 验证 GitHub API 正常、403→302 降级、网络超时、失败清理、`.part` 删除、`XDG_CACHE_HOME`/`~/.cache/lanchat/updates` 权限与并发下载互斥。
- [ ] 验证复制 `apt install` 命令后应用退出、apt/dpkg 配置锁释放；执行旧公开包/候选包 → 新 `.deb` 升级，再以 `apt install --allow-downgrades` 回退，核对设置、历史、接收目录、附件路径和普通卸载/重装。

### 5. 双机真实 LAN、切网与多网卡/TUN

- [ ] 使用物理 Windows、Linux x86_64、Linux ARM64 的至少两台端点逐项验证冷启动发现、群聊/私聊文字、时间条/气泡右键复制、表情与 PNG、文件双向 SHA-256、历史重开和退出端口释放；补测公开旧版 v2.0.18/Python 客户端互通。
- [ ] 验证改昵称实时刷新、无 `BYE` 重启/断网下线分组、重新上线、非默认 TCP 端口；核对在线/离线分组、未读、滚动位置和焦点。
- [ ] 在 Wi-Fi↔有线切换、休眠唤醒、多个物理网卡和 Clash/VPN TUN Metric 0 路由下，分别验证自动重绑与手动刷新、跨子网定向探测、无历史离线地址刷新；记录 `ip addr`、`ip route`、防火墙和实际出接口。

### 6. 交付证据与关闭规则

- [ ] 每个 CP 形成独立目录：环境清单、安装/包摘要、命令原文、退出码、Electron/ELF/服务日志、截图/录屏、网络拓扑和失败复现说明。
- [ ] 只有在对应真实环境矩阵全部通过后，才将下方条目标为 `RESOLVED` 并迁回审核记录；任何未覆盖架构、桌面会话、输入法、DPI 或网络条件继续保持 `[OPEN]`，不得用本次 Windows 阶段 5.1 构建核销。

---

## LC-LINUX-003-CP-2B [OPEN] 真实 Linux fcitx5/ibus IME 与富文本输入验收

- **来源阶段**：`LC-LINUX-003` 阶段 2B，2026-11-26；D3 = A。
- **位置**：`src/linux/desktop/preload.cjs`、`src/linux/desktop/composer-box.js`、`src/linux/desktop/index.html`、`src/linux/desktop/renderer.js`。
- **现状**：Windows 宿主机已通过 Node 语法、DOM/IPC 桩、令牌还原、光标收拢、原子删除、copy/cut、CRLF 粘贴裁剪、IME 保护和一键原生回退回归；富文本默认关闭，未把这些证据写成 Linux 实机通过。
- **触发阶段**：接入真实 Linux Electron 后，阶段 2B 独立复审通过前后均需先完成 CP-2B 现场核销。
- **验收条件**：在 Xorg、Wayland/XWayland 下分别使用 `fcitx5` 与 `ibus`，验证中文预编辑/确认期间不被 Backspace/Delete/Enter 抢占；PNG 表情基线与 11pt 正文一致；点击表情左右收拢；整枚退格/Delete；原生 textarea 一键回退；系统 PNG/JPEG/路径粘贴与暗色右键粘贴附件菜单；copy/cut 还原 `[表情名]`；100%/125%/150% DPI 与真实系统剪贴板均通过后，才允许评估默认开启富文本。

---

## LC-LINUX-003-S4-ENV-001 [OPEN] Linux 在线更新与 apt 退锁现场验收

- **来源阶段**：`LC-LINUX-003` 阶段 4，2026-11-26；对应 R5 环境边界与审核护栏 4。
- **位置**：`src/linux/desktop/update-service.cjs`、`src/linux/desktop/main.cjs`、`src/linux/desktop/settings.js`。
- **现状**：Windows 宿主机已用本地 Mock HTTP 验证 API/302、双架构资产选择、嵌套清单、流式 SHA-256、坏包删除和单引号命令；尚未在 Linux/ELF 实机访问 GitHub、验证 `XDG_CACHE_HOME` 权限、真实 Electron 下载进度、apt 配置锁释放、`.deb` 升级/回退或便携包解压启动。
- **触发阶段**：阶段 5 Linux/ELF 与真实桌面现场验收。
- **验收条件**：在 x86_64 与 ARM64 Linux 实机分别验证安装版/便携版资产、网络超时与 API 403 降级、缺失/篡改清单阻断、`~/.cache/lanchat/updates` 权限、复制命令后进程退出释放锁、`.deb` 升级与 `apt install --allow-downgrades` 回退；结果不得以 Windows Mock 证据替代。

---

## LC-UI-INPUT-010-ENV-001 [OPEN] 输入框内联表情真实 DPI/IME 现场验收

- **来源轮次**：`LC-UI-INPUT-010`，2026-11-26。
- **位置**：`src/UI/Controls/ChatInputBox.cs`、`tests/ChatComposerRegression.cs`。
- **现状**：Windows 原生编译、`PrintWindow`/RichEdit 坐标回归、表情紧凑占位断言和好友刷新联合回归均通过；尚未在真实 Windows 100%/125%/150% DPI 窗口目测表情基线、图片尺寸、鼠标命中及中文 IME 预编辑。
- **触发阶段**：`LC-UI-INPUT-010` Gemini 独立审核通过后进行 Windows 桌面现场验收。
- **验收条件**：连续表情、正文前后夹带表情、自动折行行尾表情在 100%/125%/150% DPI 下均与 11pt 正文共用视觉基线；表情无额外大段空白或原始令牌残留；普通输入、选区、复制、退格/Delete、Enter 发送、Shift+Enter 换行及微软拼音/搜狗 IME 预编辑均正常。

## LC-UI-INPUT-008-ENV-001 [OPEN] 输入区与设置页真实窗口验收

- **来源轮次**：`LC-UI-INPUT-008`，2026-11-25。
- **位置**：`src/MainForm.Designer.cs`、`src/UI/Controls/ChatInputBox.cs`、`src/UI/Controls/SettingsPanels/`、`src/linux/desktop/settings.css`。
- **现状**：Windows 原生编译、Linux 设置脚本语法检查和空白检查通过；尚未在真实窗口中完成多 DPI、鼠标命中、中文输入法预编辑、超长多行文本及 Linux Xorg/Wayland/XWayland 实机验收。
- **触发阶段**：`LC-UI-INPUT-008` 独立审核通过后进行 Windows 与 Linux 桌面现场验收。
- **验收条件**：100%/125%/150% DPI 下输入文字和内置表情保持同一基线，快捷按钮均可点击且不遮挡编辑区；普通 Enter 发送、Shift+Enter 换行；设置中心所有双行条目无裁切且间距一致；关于页显示新描述和 `2026 yangjiang`；Linux Electron 三种桌面会话下输入区、设置页和焦点恢复正常。

---

## LC-CC-ENV-001 [OPEN] 剪贴板与新旧客户端的桌面验收

- **来源** LC-CHAT-COMPOSER-001 Round 1，2026-11-24。
- **触发阶段** 本轮独立审核后，在 Windows 与 Linux 可用桌面环境验证。
- **验收** Windows 截图工具和图像应用的 Ctrl+V、Shift+Insert、右键粘贴，Linux Xorg/Wayland 截图与文件管理器的 Ctrl+V。确认只在当前会话出现缩略图，移除和取消选择接收者的行为正确。两个新版且导入同包的端点逐次发送微信表情、Unicode 与 PNG，核对显示、复制、重新打开历史和图片字节。与未导入资源或旧版对端联测可读名称、文字和图片文件卡片。运行 Linux ELF 回归，核对两端分组间距和状态圆点。
- **当前证据** Windows 原生编译和隔离回环、GDI+ 绘制以及 Linux 源码检查和 Windows 宿主机上的 Node 桩测试通过。真实多机与 Linux 桌面、实际系统剪贴板键盘操作仍待取证。
- **侧栏实机补验** LC-CC-003 已迁回 [REVIEW.md](REVIEW.md)。Linux 桌面仍需核对窄侧栏、DPI 缩放下的 Unicode 组合字形与已导入表情图片，并验证无资源时的名称回退。
- **截图清理实机补验** LC-CC-004 已迁回 [REVIEW.md](REVIEW.md)。两台端点对测截图拒收、断连与成功后历史重开，在 Linux Electron 实机触发预检和附件发送时退出。核对仅清理无历史引用的应用截图，确认历史中的 PNG 和用户原始文件均可打开。

## LC-LINUX-002-ENV-001 [OPEN] Linux 网络命名空间对最新 ELF 实跑后端回归

- **来源轮次**：`LC-LINUX-002-STEP-3-SLICE-2` Round 1 / Round 2（2026-11-23）。
- **位置**：`tests/linux_desktop_regression.py`、`src/linux/LanChat.Linux/DesktopService.cs`、`src/linux/LanChat.Linux/LinuxProfile.cs`。
- **现状**：宿主机为 Windows，本轮仅完成 `linux-x64` 与 `linux-arm64` ELF 零警告编译及 ELF64 机器码校验，尚未在 Linux 仅回环网络命名空间运行最新 ELF。
- **触发阶段**：下一次进入 QEMU / Linux 实机验证时。
- **验收条件**：在 Linux 网络命名空间对最新 ELF 跑通 `tests/linux_desktop_regression.py` 全量用例，包含外观 6 项持久化重启、只读设置取消与确认、`clear-all-histories`、`history`（含冲突目标目录拒绝迁移）、`delete`、`transfer` 与 `crash`。
- **LC-LINUX-003 CP-0 补充（2026-11-26）**：`LC-LINUX-003` 阶段 0 检查点 `CP-0`（在 Linux 网络命名空间对现存 `v2.0.26` ELF 摸底）沿用本条目保持 `[OPEN]`，Windows 宿主机编译与 Node 回归不得核销。

## LC-LINUX-002-GUI-001 [OPEN] Linux Xorg / Wayland / XWayland 实机桌面联调

- **来源轮次**：`LC-LINUX-002-STEP-3`（2026-11-23）。
- **位置**：`src/linux/desktop/`、`PlanDocs/LC-LINUX-002/BASELINE.md`。
- **现状**：主窗客户区与设置八页已通过 Node 模拟测试与 Edge Headless 静态测量，尚未在真实 Linux 桌面会话运行 Electron。
- **触发阶段**：第 3 步实机桌面联调阶段。
- **验收条件**：
  1. Xorg、原生 GNOME Wayland、GNOME XWayland 下设置窗打开居中、小工作区外框贴合、系统 GTK 目录选择器、关闭回焦主窗正常；
  2. 切换主题/强调色/字体/字号后主窗与设置窗即时变化、重启保持，清空过程中打开会话不回写旧气泡；
  3. fcitx5 与 ibus 在聊天输入、昵称修改与反馈输入中的预编辑正常；
  4. 100% / 125% / 150% DPI 同尺寸叠图对齐，以及 2000 条历史切换与滚动性能达标。

## LC-LINUX-002-SLICE-3 [OPEN] 第 3 步后续切片：托盘恢复、系统通知与检查更新维护入口

- **来源轮次**：`LC-LINUX-002-STEP-3` 切片拆分边界（2026-11-23）。
- **位置**：`src/MainForm.Tray.cs`、`src/UI/Controls/GrokUpdateForm.cs`、`src/linux/desktop/main.cjs`。
- **现状**：当前桌面关闭窗口仍直接退出，托盘恢复、未读预览、通知点击与检查更新维护入口按切片计划未在切片 1/2 接入。
- **触发阶段**：第 3 步切片 3 实施阶段。
- **验收条件**：仅在实测托盘可稳定将同一窗口唤回前台后才启用关闭隐藏，其余未读入口、通知点击与更新入口对照 `BASELINE.md` 逐项验收。
- **LC-LINUX-003 CP-1 补充（2026-11-26）**：`LC-LINUX-003` 阶段 1 检查点 `CP-1`（GNOME Xorg / 原生 Wayland / XWayland 真实 StatusNotifierItem 托盘可见性、无 AppIndicator 最小化不失联、通知点击激活与 Xorg 闪烁/预览）沿用本条目及 `LC-LINUX-002-GUI-001` 保持 `[OPEN]`，待阶段 1 整改放行并接入 Linux 实机后现场核销。

## LC-S3-003 [OPEN] `ChatHistoryService.ClearAllHistories()` 删除失败被静默忽略（P2）

- **来源轮次**：`LC-LINUX-002-STEP-3-SLICE-2` Round 1（2026-11-23，Grok 审核）。
- **位置**：`src/Services/ChatHistoryService.cs:225-243`、`src/linux/LanChat.Linux/DesktopService.cs:172-180`。
- **现状**：`ClearAllHistories()` 内部 `try { File.Delete(f); } catch { }` 会吞掉单文件删除失败，界面仍提示清空成功；该行为目前与 WinForms 一致。
- **触发阶段**：后续需要严格报告历史文件权限/占用错误时。
- **验收条件**：明确双端是否统一向调用方返回清理失败原因；若改为显式报错，同步更新 `DesktopService`、`settings.js` 与 WinForms 面板提示。

## LC-FR-ENV-001 [OPEN] 真实多机 LAN、切网与多网卡/TUN 路由下的好友刷新验收

- **来源轮次**：`LC-FRIEND-REFRESH-001` Round 1（2026-11-24，Gemini 审核）。
- **位置**：`src/NetworkService.Discovery.cs`、`src/NetworkService.Interface.cs`、`src/NetworkService.Transfer.cs`。
- **现状**：Round 1 仅完成 Windows 原生编译与单机回环隔离夹具测试；回环延迟与路由特性不能代表真实局域网广播抑制、防火墙、DHCP 延迟、Wi-Fi/有线切换及 Clash/VPN TUN Metric 0 路由环境。
- **触发阶段**：`LC-FRIEND-REFRESH-001` 审核通过后的真实双机/多机 LAN 联调与发版前验收阶段。
- **验收条件**：
  1. 真实双机/多机 Windows（及与公开旧版 v2.0.18 / Python 客户端互通）下，验证冷启动发现、改昵称实时刷新、对端无 `BYE` 重启/断网下线分组切换、非默认 TCP 端口复核及重新上线；
  2. 真实 Wi-Fi/有线切网、休眠唤醒及开启全局 TUN 代理的多网卡环境下，验证自动重绑与手动刷新按钮均能恢复发现，无旧节点误残留或误下线。

## LC-FR-ENV-002 [OPEN] Linux ELF 实跑与 Electron 实机刷新入口及分组渲染验收

- **来源轮次**：`LC-FRIEND-REFRESH-001` Round 1（2026-11-24，Gemini 审核）。
- **位置**：`src/linux/LanChat.Linux/DesktopService.cs`、`src/linux/desktop/index.html`、`src/linux/desktop/desktop.css`、`src/linux/desktop/renderer.js`。
- **现状**：宿主机为 Windows，仅完成 Linux C# 宿主机构建与 Node DOM 模拟回归，未在 Linux/QEMU 网络命名空间运行最新 ELF，也未在真实 Linux Electron 窗口检查底栏刷新按钮与「在线」「离线」分组渲染。
- **触发阶段**：下一次 Linux/QEMU 回归与第 3 步实机 Electron 桌面联调阶段。
- **验收条件**：
  1. 在 Linux 网络命名空间对最新 ELF 跑通 `tests/linux_desktop_regression.py` 及重启对端/手动刷新/改昵称广播场景；
  2. 在真实 Linux Electron 窗口（420px 常规侧栏及 260px 窄窗、100%/125%/150% DPI）确认底栏刷新按钮布局无挤压、点击刷新实时更新昵称与在线/离线分组且滚动位置和焦点保持稳定。

## LC-FR-007 [DEFERRED] 跨子网 TUN 路由与运行期离线地址刷新

- **来源轮次**：`LC-FRIEND-REFRESH-001` Round 1 审核、Round 2 返修。
- **现状**：跨子网 UDP 探测回落通配套接字。Linux 的已知地址来自归档，自动切网刷新可能缺少上次显式刷新后发现的无历史离线地址。
- **触发阶段**：`LC-FR-ENV-001` 多网卡和 TUN 路由联调，及 `LC-FR-ENV-002` 最新 Linux 后端回归。
- **验收条件**：确认跨子网 UDP 在普通路由和默认 TUN 路由下的出接口，验证同子网发现和旧版互通。验证运行期发现且未聊天的对端离线后，Linux 手动刷新及自动重绑刷新均可继续定向探测该地址。
