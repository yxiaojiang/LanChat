# 延期与跨环境验证清单 (DEFERRED)

> 这里只记录已经明确不属于当前 P0/P1 修复批次、或需在 Linux 实机/虚拟机环境才能验收的条目。进入对应阶段时，应迁回 `REVIEW.md` 并改为 `OPEN`。

---

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
