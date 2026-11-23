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
