# 延期与跨环境验证清单 (DEFERRED)

> 这里只记录已经明确不属于当前 P0/P1 修复批次、或需在 Linux 实机/虚拟机环境才能验收的条目。进入对应阶段时，应迁回 `REVIEW.md` 并改为 `OPEN`。

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
