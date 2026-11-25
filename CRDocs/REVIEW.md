# LC-UI-INPUT-008 — Round 2 独立复审（通过：P1-1 ~ P1-4 与 P2-1 已全部闭环）

- **任务编号**：`LC-UI-INPUT-008`
- **任务主题**：微信式高输入区、表情文字纵向稳定、快捷工具展开、设置页统一间距与关于页文案
- **审核轮次**：Round 2 独立复审
- **审核基线**：工作区未提交整改（基于 `ba0dd8f` / `2b6d510`，改动 `src/MainForm.cs`、`src/MainForm.Designer.cs`、`src/MainForm.Files.cs`、`src/UI/Controls/ChatInputBox.cs`、`src/UI/Controls/GrokAboutForm.cs`、`src/UI/Controls/SettingsPanels/AppearanceSettingsPanel.cs`、`tests/ChatComposerRegression.cs`）
- **独立审核方**：Gemini
- **审核日期**：2026-11-25
- **审核结论**：**APPROVED**（当前开发与 Windows 宿主机回归范围放行；实机与跨环境验证边界继续保留于 `CRDocs/DEFERRED.md`）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`、`Docs/GrokBot_UI_Design_System.md`，以及工作区实际 diff 与宿主机独立复跑证据。

## 一、Round 1 问题逐项复核结果

| 问题 ID | 原级别 | Round 2 复核 | 复核依据与实测证据 |
| --- | :---: | :---: | --- |
| `LC-UI-INPUT-008-P1-1` | **P1** | **RESOLVED** | `MainForm.cs:126` 抽出共享常量 `BottomSendPanelBaseHeight = 154`，`MainForm.Designer.cs:437, 700` 与 `MainForm.Files.cs:208`（`RefreshAttachmentDraft`）全部统一为 `BottomSendPanelBaseHeight + height`；`tests/ChatComposerRegression.cs:VerifyMainFormInputLayout` 已覆盖初始态、广播附件草稿、切会话与私聊附件草稿，断言 `bottom.Height == 154 + 34`、`panel.Height == 128` 与 `input.Bottom <= emoji.Top` 全部通过。 |
| `LC-UI-INPUT-008-P1-2` | **P1** | **RESOLVED** | `MainForm.cs:556-580` 抽出 `LayoutInputPanel()` 供 `_inputPanel.Resize`（`MainForm.Designer.cs:585`）与 `ApplyAppearanceSettings()`（`MainForm.cs:542`）共用，已彻底移除旧的 `_txtInput.Top = (_inputPanel.Height - _txtInput.Height) / 2` 居中残留；外观刷新后 `_txtInput` 保持 `Location=(60,10)`、`Height=70`（`Bottom=80 <= 83`），回归断言通过。 |
| `LC-UI-INPUT-008-P1-3` | **P1** | **RESOLVED** | `MainForm.Designer.cs:452` 将 128px 多行输入区圆角收敛为 `18px`，几何核验左下 `+` 圆钮（`(13,80)` 直径 `38`）与右下 `↑` 发送圆钮（`(w-54,77)` 直径 `44`，距右下圆角圆心仅 `4.82px <= 18px`）均严格位于边框内；`MainForm.Designer.cs:466` 空输入占位符改为 `y=16, height=20`，与 `_txtInput.Top (10) + TextPaddingTop (6) = 16` 首行基线对齐。 |
| `LC-UI-INPUT-008-P1-4` | **P1** | **RESOLVED** | `ChatInputBox.cs:63-65` 默认统一为 `Multiline = true; WordWrap = true;`；`DrawEmojiPaint`（第 160-189 行）已将裁剪区收敛至 `EM_SETRECTNP` 格式矩形 `(0, TextPaddingTop, ClientSize.Width, ClientSize.Height - TextPaddingTop - 4)`，并在 `finish.Y != start.Y || finish.X <= start.X` 时改用 `Math.Max(lineHeight, ClientSize.Width - start.X)` 避免行尾表情负宽消失；`tests/ChatComposerRegression.cs` 已覆盖创建/滚动/Resize/FontChanged/RecreateHandle 后的 `EM_SETRECTNP` 边界、自动折行行尾表情像素绘制、多行垂直滚动与 `Shift+Enter` 换行。 |
| `LC-UI-INPUT-008-P2-1` | **P2** | **RESOLVED** | `AppearanceSettingsPanel.cs:61, 65` 已改用 `SettingTitleOffset` / `SettingDescriptionOffset`；`GrokAboutForm.cs:28, 139` 署名已同步为 `2026 yangjiang`；`MainForm.cs:529-533` 切换外观时同步刷新 5 个快捷按钮的 `HoverFillColor`。 |
| 协议/历史兼容与行数门禁 | — | **PASS** | 未修改 `Models.cs`、`NetworkService*.cs`、`ChatHistoryService.cs`、`SessionManager.cs`，LANC 魔数、Type 1/Type 2 字段顺序、确认字节与 JSONL 历史格式零改动；全部 18 个本任务涉及的手写源码与测试文件均低于 1000 行软门禁（最高：`gui.py` 951、`settings.css` 742、`MainForm.Designer.cs` 720、`ChatComposerRegression.cs` 690、`MainForm.cs` 655、`MainForm.Chat.cs` 599、`MainForm.Files.cs` 503、`settings-pages.js` 499、`SettingsPanelBase.cs` 492、`ChatInputBox.cs` 442），新增注释为中文。 |

## 二、Round 2 独立验证证据与边界区分

### 1. 已在 Windows 宿主机独立复跑通过的内容

| 验证命令 | 退出码 | 实测结果 |
| --- | :---: | --- |
| `cmd /c "Tools\Scripts\Compile.bat < NUL"`（验证后已还原 `release/LanChat.exe`、`release/uninstall.exe`） | 0 | 原生 `csc.exe` 编译成功 |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | 0 | 全部 8 组回归通过（含多行 `PrintWindow` / `EM_SETRECTNP` / 折行行尾表情 / 多行滚动 / `切会话、附件草稿、外观刷新后的输入区布局与 Shift+Enter 断言通过` 及既有剪贴板/外观/竞态/LANC/JSONL 回归） |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 0 | `Native Windows compile: ok` 及全部好友刷新/侧栏/组合表情/草稿回归通过 |
| `"C:\Program Files\nodejs\node.exe" --check src/linux/desktop/settings-pages.js` | 0 | 语法检查通过 |
| `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js` | 0 | 输出 `linux desktop ui regression: ok` |
| `git diff --check` | 0 | 无空白错误（仅有 Git LF→CRLF 提示） |

### 2. 仍未验证的实机与跨环境边界（不得视为通过，继续保留于 `CRDocs/DEFERRED.md`）

- 真实 Windows 窗口 100% / 125% / 150% DPI 下的视觉目测、表情基线对齐与鼠标点击命中；
- 真实中文输入法（微软拼音/搜狗/fcitx5/ibus 等）在多行 RichEdit 中的预编辑组合态；
- Linux Xorg / 原生 Wayland / XWayland 下的 Electron 实机运行（含 `settings.css` 58px 行高与关于页验收）；
- Linux ARM64 / x86_64 ELF 实机运行（含 `tests/linux_desktop_regression.py`）与真实双机局域网互通。

---

# LC-UI-INPUT-008 — Round 1 独立审核整改执行记录（Codex）

- **任务编号**：`LC-UI-INPUT-008`
- **整改轮次**：Round 1 独立审核打回后的整改交接
- **执行日期**：2026-11-25
- **执行者**：Codex
- **状态**：`WAITING_FOR_GROK`；以下为执行者记录，不能视为独立审核结论

## 改动与证据摘要

- 以 `BottomSendPanelBaseHeight = 154` 统一底部输入区初始化、附件草稿刷新和布局重算，避免切会话或附件操作后胶囊坍塌。
- 以 `LayoutInputPanel()` 统一 Resize 与外观刷新后的输入框、5 个快捷按钮和发送按钮布局，删除旧的 `_txtInput.Top` 居中赋值。
- 将 128px 输入区圆角改为 `18px`，占位符改为面板坐标 `y=16、height=20`；`ChatInputBox` 默认多行换行，表情绘制使用 `EM_SETRECTNP` 格式矩形裁剪，并处理自动折行行尾的跨行坐标。
- `ChatComposerRegression` 已覆盖多行格式矩形在创建/Resize/FontChanged/RecreateHandle 后的边界、多行滚动、折行行尾表情、选区、Shift+Enter，以及切会话/附件草稿/外观刷新后的 `128px` 与不重叠断言。
- 顺手完成 `AppearanceSettingsPanel` 偏移量复用、`GrokAboutForm` 的 `2026 yangjiang` 署名和 5 个快捷按钮 HoverFillColor 同步。

## 验证结果

| 命令 | 退出码 | 结果 |
| --- | :---: | --- |
| `Tools/Scripts/Compile.bat` | 0 | Windows 原生编译成功；`release` 二进制验证后已还原。 |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | 0 | 多行输入、表情绘制、布局断言、附件/LANC/JSONL 回归通过。 |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 0 | 好友刷新及联合聊天回归通过。 |
| `node --check src/linux/desktop/settings-pages.js` | 0 | 语法检查通过。 |
| `node tests/linux_desktop_ui_regression.js` | 0 | Linux UI 模拟回归通过。 |
| `git diff --check` | 0 | 无空白错误；仅有换行转换提示。 |

## 未验证边界

本轮未宣称真实 DPI 窗口目测/鼠标命中、真实中文输入法预编辑、Linux Xorg/Wayland/XWayland 实机、Linux/ELF 实跑或真实双机 LAN 通过；这些仍按 `CRDocs/DEFERRED.md` 的 `LC-UI-INPUT-008-ENV-001` 等条目保留。

## 交给 Gemini 的复审要求

请按 `CRDocs/RULES.md` 独立读取 `CRDocs/STATUS.md`、`CRDocs/REVIEW.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`，直接核对当前代码与本轮回归证据，重点审查 `LC-UI-INPUT-008-P1-1` ~ `P1-4`、`P2-1`、LANC/JSONL 兼容、行数门禁及未验证边界。审核结束后必须保留历史并回写 `CRDocs/REVIEW.md` 与 `CRDocs/STATUS.md`，再交出下一份提示词。

---

# LC-UI-INPUT-008 — 独立审核结论（打回：存在 4 项 P1 布局与多行渲染回归）

- **任务编号**：`LC-UI-INPUT-008`
- **任务主题**：微信式高输入区、表情文字纵向稳定、快捷工具展开、设置页统一间距与关于页文案
- **审核轮次**：Round 1 独立审核
- **审核基线**：提交 `2b6d510`（`feat: 优化微信式输入区与设置排版`）及交接提交 `ba0dd8f`
- **独立审核方**：Gemini
- **审核日期**：2026-11-25
- **审核结论**：**REJECTED**（无 P0，但核查出 **4 项 P1 阻断问题** `LC-UI-INPUT-008-P1-1` ~ `LC-UI-INPUT-008-P1-4` 与 2 项 P2，打回 Codex 整改）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`、`Docs/GrokBot_UI_Design_System.md`，以及 `2b6d510` 完整 diff、调用链与宿主机独立实测。

## 一、五维审核重点核对结论

| 核对维度 | 结论 | 核对依据与实测发现 |
| --- | :---: | --- |
| 1. Windows 输入区 | **FAIL (P1)** | `MainForm.Designer.cs` 已将 `_bottomSendPanel.Height` 扩为 `154`（胶囊 `128px`）、`_txtInput` 改为多行 `RichEdit`；`EM_SETRECTNP` 在 `OnHandleCreated` / `Resize` / `FontChanged` / `RecreateHandle` 探针中均保持 `0,6,W,66`；普通 `Enter` 发送、`Shift+Enter` 换行（实测 `.NET 4.x RichTextBox` 写入 `U+000A` 而非 `\v`）符合快捷键契约。但存在严重回归：**切会话/附件草稿将输入区压回 `64px`（`P1-1`）**、**切换外观重置 `_txtInput.Top=29` 压住工具栏（`P1-2`）**、**`radius=64` 致按钮突出边框且占位符与首行错位 `20px`（`P1-3`）**、**自动折行行尾表情因 `finish.X - start.X < 0` 消失且回归测试仍只测单行（`P1-4`）**。 |
| 2. 快捷工具栏 | **FAIL (P1)** | 表情（`ShowEmojiPopup`）、接收目录（`OpenReceivedFolder`）、发文件（`BtnSendFile_Click`）、截图（`TriggerScreenshot`）、清空会话（`ClearCurrentChat`）均已真实接线；`_inputPanel.Resize` 在全部按钮实例化后绑定，无空引用，最小窗宽 `880px` 下横向无挤压；隐藏 `_btnToolMic`（语音协议未实现）**判断合理**。但在 `P1-1` / `P1-2` / `P1-3` 触发时，快捷按钮会与输入框重叠或超出胶囊边界。 |
| 3. 设置页面 | **PASS (含 P2)** | `SettingsPanelBase` 新增 `SettingTitleOffset = 7`、`SettingDescriptionOffset = 32`；General、Profile、FileTransfer、ChatHistory、NetworkDevice、About 均已接入；100% DPI 探针实测 `10pt` 标题占 `7..23.93`、`8.5pt` 说明占 `32..46.39`，在 `54px` 行高内无裁切（底部留白 `7.61px`）；Linux `settings.css` 已同步 `min-height: 58px`、`margin-top: 6px`。仅 `AppearanceSettingsPanel.cs:60-63` 仍硬编码 `7`/`29` 未共用常量（`P2-1`）。 |
| 4. 关于页 | **PASS (含 P2)** | `git grep "LanChat Team" src/` 返回 0 匹配；Windows `AboutSettingsPanel.cs`、Linux Electron `settings-pages.js` 与旧版 Linux `gui.py` 均已更新为新描述及 `2026 yangjiang`。仅托盘入口的 `src/UI/Controls/GrokAboutForm.cs:28, 139` 仍残留旧署名 `© 2026 ChaseFraser`（`P2-1`）。 |
| 5. 兼容性与代码质量 | **PASS** | 未修改 LANC 魔数、Type 1/Type 2 字段顺序、确认字节或 JSONL 历史格式；全部 13 个改动源码文件低于 1000 行软门禁（最高 `gui.py` 951、`MainForm.Designer.cs` 745、`settings.css` 742、`MainForm.Chat.cs` 599、`settings-pages.js` 499、`SettingsPanelBase.cs` 492、`ChatInputBox.cs` 438）；宿主机编译与既有回归全部退出码 `0`。 |

## 二、问题分级清单（P0 / P1 / P2）

| 问题 ID | 严重度 | 位置 | 问题描述与复现证据 | 整改要求 |
| --- | :---: | --- | --- | --- |
| `LC-UI-INPUT-008-P1-1` | **P1** | `src/MainForm.Files.cs:208`（调用方：`MainForm.Chat.cs:88, 431`、`MainForm.Files.cs:112, 123, 170`） | `RefreshAttachmentDraft()` 仍硬编码 `_bottomSendPanel.Height = 90 + height;`（旧 64px 胶囊高度）。只要用户切换任意会话或粘贴/发送/移除一次附件草稿，`_inputPanel.Height` 即从 `128px` 坍塌回 `64px`；`_inputPanel.Resize` 随后将快捷按钮排布到 `y = 19..51`，而 `_txtInput` 占 `y = 10..52`，**输入框与 5 个快捷按钮完全重叠碰撞**。 | 抽出底部输入区基准高度常量（`154`），统一 `MainForm.Files.cs:208` 与 `MainForm.Designer.cs:437, 726` 为 `154 + height`，并补充切会话与附件草稿切换不坍塌的自动化断言。 |
| `LC-UI-INPUT-008-P1-2` | **P1** | `src/MainForm.cs:533`（`ApplyAppearanceSettings()`） | `ApplyAppearanceSettings()` 仍残留单行居中逻辑 `_txtInput.Top = (_inputPanel.Height - _txtInput.Height) / 2;`。用户更改主题、强调色或字体后，`_txtInput.Top` 被改写为 `29`（`Bottom = 99`），与底部快捷按钮（`y = 83..115`）重叠 **`16px`**；而 `UpdateResponsiveLayout()` 不改变 `_inputPanel.Size`，不会触发 `_inputPanel.Resize` 复原。 | 将输入区内部布局抽为独立方法供 `_inputPanel.Resize` 与 `ApplyAppearanceSettings()` 共用，移除第 533 行居中残留，并补充调用 `ApplyAppearanceSettings()` 后 `_txtInput.Bottom <= _btnToolEmoji.Top` 的断言。 |
| `LC-UI-INPUT-008-P1-3` | **P1** | `src/MainForm.Designer.cs:452, 465-466, 594-608` | 128px 高输入区仍沿用 `radius = _inputPanel.Height / 2 = 64` 半圆胶囊，几何实测左下 `+` 圆钮（`(13,80)` 直径 `38`）外沿距左半圆心 `(64,64)` 为 **`66.42px`（超出边框 `2.42px`）**，右下 `↑` 发送圆钮（`(w-54,77)` 直径 `44`）外沿距右半圆心为 **`69.42px`（超出边框 `5.42px`）**；同时空输入占位符 `DrawUiTextLeft(..., 64, 10, ..., 70)` 垂直居中在 `y ≈ 35.7`，与多行 `RichEdit` 首行 `y = 16` 错位约 `20px`。 | 将 128px 多行输入区圆角调整为适配矩形多行容器的半径（如 `16px ~ 22px`），确保所有按钮完全位于边框内；将占位符改为与多行首行顶端对齐。 |
| `LC-UI-INPUT-008-P1-4` | **P1** | `src/UI/Controls/ChatInputBox.cs:63, 65, 160-199`；`tests/ChatComposerRegression.cs` | 1) 独立 WinForms 探针实测：多行自动折行时，位于行尾的 `[敲打]` 后紧接下一行字符，导致 `start={X=96,Y=6}, end={X=0,Y=32}, tokenWidth = -96`，因 `tokenWidth <= 2f` 被静默跳过绘制、裸露原文；2) `DrawEmojiPaint` 裁剪区为 `ClientRectangle` 而非 `EM_SETRECTNP` 格式矩形 `(0, TextPaddingTop, W, H-4)`，多行滚动时边缘半行表情会侵入上下内边距；3) 构造器仍默认 `Multiline = false; WordWrap = false;`，导致既有 `ChatComposerRegression` 未覆盖本轮多行模式与 `EM_SETRECTNP`。 | 修复 `DrawEmojiPaint` 在 `finish.Y != start.Y` 时的行尾表情宽度计算，并将绘制裁剪收敛到 `EM_SETRECTNP` 格式矩形；统一默认配置并在 `ChatComposerRegression.cs` 增加多行 `EM_SETRECTNP`、折行行尾表情、多行垂直滚动与 `Shift+Enter` 回归断言。 |
| `LC-UI-INPUT-008-P2-1` | **P2** | `src/UI/Controls/SettingsPanels/AppearanceSettingsPanel.cs:60-63`、`src/UI/Controls/GrokAboutForm.cs:28, 139`、`src/MainForm.cs:525-526` | 1) `AppearanceSettingsPanel` 双行项仍硬编码 `7`/`29` 未复用共享常量；2) 托盘入口的 `GrokAboutForm` 仍保留旧署名 `© 2026 ChaseFraser`；3) `ApplyAppearanceSettings` 未同步刷新 5 个新增快捷按钮的 `HoverFillColor`。 | 返修 P1 时可顺手统一，非阻断。 |
| `LC-UI-INPUT-008-P2-2` | **P2** | `CRDocs/DEFERRED.md` (`LC-UI-INPUT-008-ENV-001`) | 100%/125%/150% DPI 实机基线对齐、鼠标命中、中文输入法预编辑、Linux Xorg/Wayland/XWayland Electron 实机与真实双机互通仍待现场验收。 | 继续保留于 `CRDocs/DEFERRED.md`，不得凭宿主机编译/探针宣称实机通过。 |

## 三、独立验证证据与边界区分

### 1. 已在 Windows 宿主机独立执行的验证

| 验证命令 / 探针 | 退出码 | 实测结果 |
| --- | :---: | --- |
| `cmd /c "Tools\Scripts\Compile.bat < NUL"`（验证后已还原跟踪二进制 `release/LanChat.exe`、`release/uninstall.exe`） | 0 | 原生 `csc.exe` 编译成功 |
| `"C:\Program Files\nodejs\node.exe" --check src/linux/desktop/settings-pages.js` | 0 | 语法检查通过 |
| `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js` | 0 | `linux desktop ui regression: ok` |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | 0 | 既有 7 组回归通过（注：未覆盖本轮多行 `ChatInputBox`） |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 0 | 原生编译及全部既有回归通过 |
| `git diff --check` | 0 | 无空白错误 |
| 独立 WinForms 探针（`InputAuditProbe` / `ShiftEnterProbe` / `MainFormAuditProbe`） | 0 | 证实 `EM_SETRECTNP` 在创建/Resize/FontChanged/RecreateHandle 保持 `0,6,W,66`、`Shift+Enter` 写入 `U+000A`、`54px` 设置行 `7..23.93 / 32..46.39` 无裁切；同时**实证复现 `P1-1`、`P1-2`、`P1-3`、`P1-4`** |

### 2. 尚未验证的实机与跨环境边界（不得视为通过）

- 真实 Windows 窗口 100% / 125% / 150% DPI 目测与鼠标点击命中；
- 真实中文输入法（微软拼音/搜狗等）在多行 RichEdit 中的预编辑组合态；
- Linux Xorg / 原生 Wayland / XWayland 下的 Electron 实机运行（含 `settings.css` 与关于页视觉验收）；
- 真实双机局域网互通。

---

# LC-UI-INPUT-008 — 执行者送审记录

- **任务编号**：`LC-UI-INPUT-008`
- **任务主题**：微信式高输入区、表情文字纵向稳定、快捷工具展开、设置页统一间距与关于页文案
- **执行日期**：2026-11-25
- **执行者**：Codex
- **当前状态**：`WAITING_FOR_GROK`；以下为执行记录，不能视为独立审核结论
- **审核依据**：`.claude/Rules.md`、`CRDocs/README.md`、`CRDocs/RULES.md`、`CRDocs/STATUS.md`、`CRDocs/DEFERRED.md`、`Docs/GrokBot_UI_Design_System.md`，以及提交 `2b6d510` 的实际代码与 diff。

## 执行摘要

Windows 输入区由 64px 单行胶囊调整为 128px 高多行编辑区；`ChatInputBox` 使用 RichEdit `EM_SETRECTNP` 固定格式区域内边距，抑制插入内置表情令牌后的文字纵向漂移。普通 Enter 仍发送，Shift+Enter 换行。加号菜单中的表情、接收目录、发文件、系统截图、清空会话展开为底部快捷按钮，语音入口因协议未支持而隐藏。设置中心的双行标题/说明采用 `SettingsPanelBase` 共享偏移量，Linux Electron 同步增加行高和说明间距；关于页三端文案移除 `LanChat Team`，以 `2026 yangjiang` 收尾。

## 已执行验证

| 验证 | 结果 |
| --- | --- |
| `Tools/Scripts/Compile.bat` | 返回 0，Windows 原生编译成功；release 二进制已还原。 |
| `node --check src/linux/desktop/settings-pages.js` | 返回 0。 |
| `git diff --check` | 返回 0。 |

## 证据边界

本轮没有执行真实窗口截图、鼠标命中探针、100%/125%/150% DPI、多行输入法预编辑、Linux Xorg/Wayland/XWayland 实机或真实双机互通。因此不能把本轮宿主机编译结果扩展为全环境验收；相关现场项已在 `CRDocs/STATUS.md` 和 `CRDocs/DEFERRED.md` 标明。

## 请 Gemini 复核

请按 `CRDocs/RULES.md` 独立读取实际代码与 diff，重点审查 `EM_SETRECTNP` 生命周期、输入区 Resize 初始化顺序、工具栏按钮命中与窄窗口边界、Enter/Shift+Enter 契约、设置页文字裁切、关于页文案和旧协议/历史兼容。审核完成后必须把完整结论回写到本文件顶部，并将 `CRDocs/STATUS.md` 改为 `APPROVED` 或 `REJECTED`；只有审核者可以使用这两个结论状态。

---

# LC-CC-007 — Round 3 独立复审（通过：`LC-CC-007-R2-P1-1` 已闭环）

- **任务编号**：`LC-CC-007`
- **任务主题**：微信比例表情选择器、输入框图片预览与内置表情资源
- **审核轮次**：Round 3 独立复审（复核 Round 2 打回项 `LC-CC-007-R2-P1-1` 与可选 P3）
- **审核基线**：工作区未提交整改（基于 `6248514`，改动 `src/MainForm.Designer.cs`、`src/Services/EmojiPack.cs`、`src/UI/Controls/ChatEmojiPicker.cs`、`src/UI/Controls/ChatInputBox.cs`、`src/linux/desktop/main.cjs`、`tests/ChatComposerRegression.cs`）
- **独立审核方**：Gemini
- **审核日期**：2026-11-25
- **审核结论**：**APPROVED**（当前开发、Windows 宿主机回归与 Linux `net10.0` 构建范围放行；实机与跨环境验证边界继续保留于 `CRDocs/DEFERRED.md`）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/STATUS.md`、`CRDocs/DEFERRED.md`、`Docs/GrokBot_UI_Design_System.md`，以及工作区实际代码、diff 与宿主机独立实测。

## 一、Round 2 问题复核结果

| 问题 ID | 原级别 | Round 3 复核 | 复核依据与实测证据 |
| --- | :---: | :---: | --- |
| `LC-CC-007-R2-P1-1` | **P1** | **RESOLVED** | 核查 `tests/ChatComposerRegression.cs:269-289`：`CapturePrintWindow` 已改用 `PrintWindow(control.Handle, hdc, 2)`（`PW_RENDERFULLCONTENT`，第 279 行）；全文已彻底移除 `Graphics.CopyFromScreen(...)`（零匹配）；仅在 `PrintWindow` 返回 `false` 时于同一离屏 `hdc` 上调用 `SendMessage(control.Handle, WmPrint, hdc, (IntPtr)0x00000004)` 回退（第 280-284 行）；不再依赖屏幕坐标、窗口遮挡或交互式桌面 DC。在上一轮抛出 `Win32Exception: 句柄无效。` 的同一非交互会话中独立复跑 `chat_composer_regression.ps1 -ExpectLocalPack` 与隔离 `LOCALAPPDATA` 回归均稳定退出码 `0`。 |
| `LC-CC-007-R2-P3-1` | **P3（可选）** | **PARTIAL（非阻断）** | 1) `src/UI/Controls/ChatInputBox.cs:135` 已将 `caretInside` 修正为 `SelectionLength == 0 && selectionStart > index && selectionStart < end`，插入符位于令牌末尾 `end` 时立即保留表情图片显示且不覆盖插入符，位于令牌内部 `(index, end)` 时仍回退原生文本便于编辑；2) `ChatEmojiPicker` 网格居中/外宽未在本轮调整，属非阻断 P3。 |
| Round 1 历史项 & 兼容性 | — | **PASS** | Round 1 全部 P0/P1/P2（`LC-CC-007-P0-1`、`P1-1`、`P1-2`、`P2-1`、`P2-2`）已在 Round 2 复核关闭且本轮未引入回退；LANC 协议与 JSONL 历史格式零改动；全量改动源码与测试文件 `< 1000` 行（`MainForm.Designer.cs` 691、`main.cjs` 610、`ChatComposerRegression.cs` 558、`ChatInputBox.cs` 398、`ChatEmojiPicker.cs` 326、`EmojiPack.cs` 115），新增注释为中文。 |

## 二、Round 3 独立验证证据与边界区分

### 1. 已在 Windows 宿主机独立复跑通过的内容

| 验证命令 | 退出码 | 实测结果 |
| --- | :---: | --- |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | 0 | 全部 7 组回归通过（含 `输入框 PrintWindow、RichEdit 坐标、鼠标命中、滚动、选区、MaxLength 与纯文本粘贴通过`、剪贴板优先级、11 色外观与速率、接收完成竞态、LANC/JSONL 回环及托管截图清理） |
| 隔离 `LOCALAPPDATA` 后执行 `tests/chat_composer_regression.ps1 -ExpectLocalPack` | 0 | 临时空 `LOCALAPPDATA` 环境下全部 7 组回归通过，确认内置 `WeChatEmoji.pack` 不依赖开发机外部目录 |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 0 | `Native Windows compile: ok` 及全部好友刷新、侧栏、组合表情与草稿回归通过 |
| `"C:\Program Files\nodejs\node.exe" --check src/linux/desktop/main.cjs` | 0 | 语法检查通过 |
| `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js` | 0 | 输出 `linux desktop ui regression: ok` |
| `"C:\Program Files\nodejs\node.exe" -e "require('./tests/linux_desktop_ipc_regression.js')().then(()=>console.log('ipc ok'))"` | 0 | 输出 `LC-CC-004` IPC 清理通过及 `ipc ok` |
| `git diff --check` | 0 | 仅有 Git LF→CRLF 提示，无空白错误 |

### 2. 仅完成 Linux `net10.0` 构建但未在 Linux 实机运行的内容

- `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`：退出码 `0`（0 警告、0 错误），生成 `src/linux/LanChat.Linux/bin/Release/net10.0/lanchat.dll`；尚未在 Linux 实机运行。

### 3. 仍未验证的跨环境与实机内容（不得视为通过，继续保留于 `CRDocs/DEFERRED.md`）

- 真实双机互通（新版对新版、新版对旧版、无资源对端的表情令牌回退、复制与历史重开）。
- Windows MSI / 绿色包安装、100%/125%/150% 多 DPI 缩放及真实桌面截图工具/Excel/WPS 粘贴。
- Linux Xorg / 原生 Wayland / XWayland 下的 Electron 实机运行与系统剪贴板。
- Linux ARM64 / x86_64 自包含 ELF 实机运行及 `tests/linux_desktop_regression.py` 回归。

---

# LC-CC-007 — Round 2 打回项整改执行记录（`LC-CC-007-R2-P1-1`）

- **任务编号**：`LC-CC-007`
- **整改轮次**：Round 2 复审打回回归夹具修复
- **执行日期**：2026-11-25
- **执行者**：Codex
- **对应审核问题**：`LC-CC-007-R2-P1-1`（`tests/ChatComposerRegression.cs:CapturePrintWindow`）
- **本轮结论**：P1 阻断项已修复；可选 P3 的 `caretInside` 边界也已顺手修正，选择器网格居中/外宽未纳入本轮改动。

## 一、本轮改动

1. `tests/ChatComposerRegression.cs:CapturePrintWindow`：
   - 将 `PrintWindow(control.Handle, hdc, 1)` 改为 `PrintWindow(control.Handle, hdc, 2)`（`PW_RENDERFULLCONTENT`）。
   - 移除无条件 `Graphics.CopyFromScreen(...)`，不再依赖屏幕坐标、窗口遮挡、交互式桌面或桌面 DC。
   - 仅当 `PrintWindow(..., 2)` 返回 `false` 时，才使用同一离屏 HDC 调用 `WM_PRINT` 回退。
2. `src/UI/Controls/ChatInputBox.cs`：将 `caretInside` 改为 `selectionStart > index && selectionStart < end`，使表情令牌末尾插入符位置立即保留图片显示，同时不覆盖插入符。

## 二、验证证据（以下命令均退出码 0）

| 验证命令 | 结果 |
| --- | --- |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | 通过；PrintWindow、RichEdit 坐标、鼠标命中、滚动、选区、MaxLength、纯文本粘贴、LANC/JSONL 与托管截图清理回归均通过。 |
| 隔离 `LOCALAPPDATA` 后再次执行 `tests/chat_composer_regression.ps1 -ExpectLocalPack` | 通过；隔离环境下同一完整回归通过。 |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 通过；原生 Windows 编译及好友/侧栏/组合表情回归通过。 |
| `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false` | 通过；`net10.0` 构建 0 警告、0 错误。 |
| `"C:\\Program Files\\nodejs\\node.exe" --check src/linux/desktop/main.cjs` | 通过。 |
| `"C:\\Program Files\\nodejs\\node.exe" tests/linux_desktop_ui_regression.js` | 通过；`linux desktop ui regression: ok`。 |
| `"C:\\Program Files\\nodejs\\node.exe" -e "require('./tests/linux_desktop_ipc_regression.js')().then(()=>console.log('ok'))"` | 通过；输出 `ok`。 |
| `git diff --check` | 通过；仅有 Git 的 LF→CRLF 提示，无空白错误。 |

代码核验确认 `tests/ChatComposerRegression.cs` 中 `CapturePrintWindow` 使用 `PrintWindow(..., 2)`，且不再包含 `CopyFromScreen`。

## 三、未验证边界

本轮没有把以下内容写成通过：真实双机新版/旧版互通、Windows MSI/绿色包安装、100%/125%/150% 多 DPI、真实截图工具/Excel/WPS 粘贴、Linux Xorg/Wayland/XWayland Electron 实机、Linux ARM64/x86_64 ELF 实机运行及 `tests/linux_desktop_regression.py`。Linux 本轮仅完成 `net10.0` 构建和 Node 宿主机回归，仍按 `CRDocs/DEFERRED.md` 保留现场验收边界。

请 Gemini 按 `CRDocs/RULES.md` 独立复核本轮 P1 修复与证据；审核结束后再回写 `CRDocs/REVIEW.md`、`CRDocs/STATUS.md`，只有独立审核者可以将状态改为 `APPROVED`。

---

# LC-CC-007 — Round 2 独立复审（打回：回归夹具 `CopyFromScreen` 导致必跑回归退出码 1）

- **任务编号**：`LC-CC-007`
- **审核轮次**：Round 2 独立复审
- **审核基线**：工作区未提交整改（基于 `6248514`，改动 `ChatInputBox.cs`、`ChatEmojiPicker.cs`、`EmojiPack.cs`、`MainForm.Designer.cs`、`main.cjs`、`tests/ChatComposerRegression.cs`）
- **独立审核方**：Gemini
- **审核日期**：2026-11-25
- **审核结论**：**REJECTED**（Round 1 全部产品代码 P0/P1/P2 已复核 **RESOLVED**，但新增回归夹具 `tests/ChatComposerRegression.cs:CapturePrintWindow` 无条件调用 `CopyFromScreen` 导致必跑回归 `tests/chat_composer_regression.ps1 -ExpectLocalPack` 抛出 `Win32Exception: 句柄无效。` 退出码 1，记为 P1 `LC-CC-007-R2-P1-1` 打回修复）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/STATUS.md`、`CRDocs/DEFERRED.md`、`Docs/GrokBot_UI_Design_System.md`，以及工作区实际 diff 与宿主机独立实测。

## 一、Round 1 问题复核结果

| 问题 ID | 原级别 | Round 2 复核 | 复核依据与实测证据 |
| --- | :---: | :---: | --- |
| `LC-CC-007-P0-1` | P0 | **RESOLVED** | `ChatInputBox.cs` 已彻底删除 `EmojiOverlay` 子控件（实测 `CONTROLS = 0`），改在 `WM_PAINT`/`WM_PRINT`/`WM_PRINTCLIENT` 后按 RichEdit 真实矩形叠绘；`PrintWindow(..., 2)`（`PW_RENDERFULLCONTENT`）实测 `total=10400, black=0, bg=9129, bright=268, emoji=267`（黑屏 0 像素）；`WM_NCHITTEST = 1` 且 `ChildWindowFromPoint == box.Handle`，无鼠标吞噬 |
| `LC-CC-007-P1-1` | P1 | **RESOLVED** | 改用 `GetPositionFromCharIndex(index)` 与 `GetPositionFromCharIndex(end)` 定位可见区内令牌；实测末尾表情 `"Hi [敲打]"` `start={20,0}, end={60,0}, width=40`，代理对前缀 `"😊👨‍👩‍👧‍👦 [敲打] ok"` `idx=14, start={54,0}, end={94,0}, width=40`；`WM_HSCROLL`/`WM_VSCROLL`/`WM_MOUSEWHEEL`/`SelectionChanged`/`TextChanged` 均同步刷新，选区与插入符命中令牌时跳过叠绘 |
| `LC-CC-007-P1-2` | P1 | **RESOLVED** | 构造器与 `MainForm.Designer.cs` 均显式设置 `Multiline = false`、`MaxLength = 32767`，`OnHandleCreated` 追加 `EM_EXLIMITTEXT`（`0x0435`），`OnTextChanged` 兜底裁剪；`ProcessCmdKey`（`Ctrl+V` / `Shift+Insert`）与 `WM_PASTE` 在非附件路径统一走 `PasteClipboardText()` 读取 `UnicodeText`/`Text` 并恢复 `Font`/`ForeColor`，禁止 RTF/OLE 粘贴 |
| `LC-CC-007-P2-1` | P2 | **RESOLVED** | `ChatEmojiPicker` 实测尺寸 `496×548`，网格预留 `GridSliderReserve = 16`，补齐 `16px` 圆角、`1px #2C2C32` 描边、`CS_DROPSHADOW` 与 `GotFocus`/`LostFocus`/`OnDeactivate` 重绘，移除无交互底栏，真实维护进程内 `RecentOrder`（上限 12 去重） |
| `LC-CC-007-P2-2` | P2 | **RESOLVED** | `main.cjs` 新增 `emojiPackCache` 缓存非空结果并在后端异常或返回 `[]` 时回退 `readEmojiPack()`；`EmojiPack.cs` 补齐 `token.Length <= 26`（括号内 `<= 24`）及 PNG IHDR `1..64` 校验 |
| LANC / JSONL 兼容 | — | **PASS** | 未改动 `Models.cs`、`NetworkService*.cs`、`ChatHistoryService.cs`、`SessionManager.cs`；LANC 魔数、Type 1/Type 2 字段顺序与 JSONL 历史格式零改动；全量改动手写文件 `< 1000` 行，全中文注释 |

## 二、Round 2 新发现问题清单

| 问题 ID | 严重度 | 位置 | 问题描述与复现证据 | 整改要求 |
| --- | :---: | --- | --- | --- |
| `LC-CC-007-R2-P1-1` | **P1** | `tests/ChatComposerRegression.cs:269-288` | `CapturePrintWindow` 对 `PrintWindow` 传入标志 `1`（`PW_CLIENTONLY`）后，第 285-286 行无条件调用 `graphics.CopyFromScreen(...)`。在非交互/锁屏桌面会话中独立执行 `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` 时直接抛出 `System.ComponentModel.Win32Exception: 句柄无效。`，返回退出码 `1`，并阻断后续所有剪贴板、外观、竞态、LANC 回环与托管截图清理回归执行；即使在交互桌面下，若 `(40,40)` 被其他窗口遮挡也会导致用例不稳定。 | 改用 `PrintWindow(control.Handle, hdc, 2)`（`PW_RENDERFULLCONTENT`，本机探针已实证在同环境返回 `ok=True, black=0, bg=9129, bright=268, emoji=267`），移除无条件 `CopyFromScreen`，确保 `chat_composer_regression.ps1 -ExpectLocalPack` 及隔离 `LOCALAPPDATA` 复跑在任意桌面会话稳定退出码 `0`。 |
| `LC-CC-007-R2-P3-1` | **P3（可选）** | `src/UI/Controls/ChatInputBox.cs:135`、`src/UI/Controls/ChatEmojiPicker.cs:69-77` | 1) `caretInside` 判断 `selectionStart <= end`，刚点击插入末尾表情时插入符恰在 `end`，需再键入字符或移动光标才转为图片显示（可评估改为 `selectionStart > index && selectionStart < end`）；2) `ChatEmojiPicker` 宽 `496` 但网格宽 `400` 且左偏 `x=12`，右侧留白 `84px`（可顺手居中或收紧外宽）。 | 非阻断项，可按需顺手优化。 |

## 三、Round 2 独立验证证据与边界区分

### 1. 已在 Windows 宿主机独立验证的内容

| 命令/专项探针 | 退出码 | 实测结果 |
| --- | :---: | --- |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | **1（FAIL）** | 在 `VerifyRichEmojiInput` -> `CapturePrintWindow` 抛出 `System.ComponentModel.Win32Exception: 句柄无效。`（`LC-CC-007-R2-P1-1`） |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 0 | `Native Windows compile: ok` 及全部既有回归通过 |
| `cmd /c "Tools\Scripts\Compile.bat < NUL"` + 真实 WinForms 探针（验证后已还原 `release/*.exe`） | 0 | `CONTROLS=0`, `MAXLENGTH=32767`, `MULTILINE=False`, `PW2: ok=True black=0 bg=9129 bright=268 emoji=267`, `TRAILING_EMOJI width=40`, `SURROGATE_PREFIX width=40`, `HITTEST ht=1 childIsBox=True`, `PICKER_SIZE=496x548` |
| `"C:\Program Files\nodejs\node.exe"`（`v24.18.0`）执行 `--check src/linux/desktop/main.cjs`、`tests/linux_desktop_ui_regression.js` 与 `tests/linux_desktop_ipc_regression.js` | 0 | 全部通过 |
| `git diff --check` | 0 | 无空白错误 |

### 2. 仅完成 Linux `net10.0` DLL 构建但未在 Linux 实机运行的内容

- `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`：退出码 `0`，0 警告、0 错误；尚未在 Linux 实机运行。

### 3. 尚未验证的跨环境与实机内容（继续保留于 `CRDocs/DEFERRED.md`）

- 真实双机新版/旧版/无资源对端互通；Windows MSI / 绿色包安装与多 DPI（100%/125%/150%）；Linux Xorg / Wayland / XWayland Electron 实机与 ARM64 / x86_64 ELF 实机运行（含 `tests/linux_desktop_regression.py`）。

---

# LC-CC-007 — Round 1 打回整改执行记录

- **任务编号**：`LC-CC-007`
- **整改轮次**：Round 1 审核打回整改
- **执行方**：Codex
- **执行日期**：2026-11-25
- **当前状态**：`WAITING_FOR_GROK`，等待 Gemini 独立复审；本记录不是独立审核结论
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/STATUS.md`、本文件原 Round 1 审核结论、`Docs/GrokBot_UI_Design_System.md` 与工作区实际源码

## 一、本轮整改内容

### P0：`LC-CC-007-P0-1`

- 删除 `ChatInputBox` 的 `Dock = Fill` `EmojiOverlay` 子控件，不再让 `OptimizedDoubleBuffer` 和空的 `OnPaintBackground` 覆盖 RichEdit 客户区。
- 在 `RichTextBox` 自身完成 `WM_PAINT`、`WM_PRINT`、`WM_PRINTCLIENT` 后，用窗口 DC 叠绘表情图片；背景只填充命中的真实令牌矩形，普通文字、输入背景和光标不再被大面积黑色遮挡。
- 叠绘使用 `GetPositionFromCharIndex` 的 RichEdit 客户区坐标，并在水平/垂直滚动、鼠标滚轮、文字变化、选区变化和重绘后刷新。没有覆盖子窗口，因此 `WM_NCHITTEST`、左键定位、右键命中和系统选区均回到 RichEdit。
- 选区与插入符落在表情令牌范围时跳过图片叠绘，保留原生 RichEdit 的高亮和光标。

### P1：`LC-CC-007-P1-1`

- 废弃逐 UTF-16 码元 `MeasureString` 累加，改为按令牌起止索引调用 `GetPositionFromCharIndex`，仅在客户区可见范围内绘制，并用起止坐标计算令牌宽度。
- 在 `TextChanged`、`SelectionChanged`、`WM_PAINT`、`WM_PRINT`、`WM_HSCROLL`、`WM_VSCROLL` 和鼠标滚轮路径同步刷新；代理对问题不再参与表情令牌坐标计算。

### P1：`LC-CC-007-P1-2`

- `ChatInputBox` 构造器和 `src/MainForm.Designer.cs` 均显式设置 `Multiline = false`、`MaxLength = 32767`；句柄创建时再次写入 RichEdit 原生长度上限，并对异常追加路径做长度裁剪。
- `WM_PASTE`、Ctrl+V、Shift+Insert 均先处理文件/图片附件；非附件路径只读取 `UnicodeText`/`Text` 并以当前 `Font`、`ForeColor` 插入，禁止调用 RichEdit RTF/OLE 粘贴。
- 主输入字体和颜色仍由设计器保持为 `11.0pt`、`#F2F2F6`。

### P2：`LC-CC-007-P2-1` 与 `LC-CC-007-P2-2`

- `ChatEmojiPicker` 改为 496×548 基准尺寸，网格宽度为 `8×48px + 16px` 滑块预留区；补齐 `#2C2C32` 1px 外框、16px 圆角、`CS_DROPSHADOW`、失焦/获焦重绘，并移除无交互底部装饰图标。
- 最近使用由进程内真实选择顺序维护，重复选择会移到首位并限制为 12 项。
- `main.cjs` 对非空 `emoji-pack` 结果做主进程缓存，后端返回空数组或异常时回退 `readEmojiPack()`；非空回退结果也会缓存。
- `EmojiPack.cs` 增加令牌总长度不超过 26 个 UTF-16 字符（方括号内最多 24 个字符）以及 PNG IHDR 存在、宽高均为 1～64 的校验。

### 回归测试

`tests/ChatComposerRegression.cs` 现已使用真实 WinForms `ChatInputBox` 宿主窗体，覆盖：

- `PrintWindow` 调用及实际可见客户区渲染：输入背景非纯黑、普通文字可见、微信 PNG 可见；嵌入式 RichEdit 在本机主题下若 `PrintWindow` 返回 false，则补发 `WM_PRINT` 并采集实际客户区，避免把平台返回值误当作渲染结果。
- 无覆盖子控件、`WM_NCHITTEST` 客户区命中、左键点击定位和右键命中。
- 水平滚动后的 RichEdit 起止坐标变化、可见令牌范围、选区长度和令牌内插入符位置。
- `MaxLength = 32767`、单行契约、WM_PASTE、Ctrl+V、Shift+Insert 的 RTF/HTML 混合剪贴板纯文本化、字体颜色保持和 OLE 缺失。

本轮没有修改 LANC 魔数、Type 1/Type 2 字段顺序、确认字节、JSONL 历史字段或表情令牌网络格式。

## 二、验证证据

以下命令均从 `D:\TEmp\LanChat` 执行，退出码均为 0：

| 验证命令 | 结果 |
| --- | --- |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | 通过；输出包含输入框 PrintWindow、RichEdit 坐标、鼠标命中、滚动、选区、MaxLength 与纯文本粘贴，以及 LANC/JSONL/PNG 回环。 |
| 上述聊天回归在隔离 `LOCALAPPDATA` 子进程中复跑 | 通过；内置 `WeChatEmoji.pack` 不依赖旧外部目录。 |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 通过；原生 Windows 编译及既有好友/侧栏/组合表情回归通过。 |
| `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false` | 通过；Linux `net10.0`，0 警告、0 错误。 |
| `"C:\\Program Files\\nodejs\\node.exe" --check src/linux/desktop/main.cjs` | 通过。 |
| `"C:\\Program Files\\nodejs\\node.exe" tests/linux_desktop_ui_regression.js` | 通过；`linux desktop ui regression: ok`。 |
| `"C:\\Program Files\\nodejs\\node.exe" -e "require('./tests/linux_desktop_ipc_regression.js')().then(()=>console.log('ok'))"` | 通过；输出 `ok`。 |
| `git diff --check` | 通过；仅有 Git 的 LF→CRLF 提示，无空白错误。 |

相关手写文件当前均低于 1000 行：`ChatInputBox.cs` 405、`ChatEmojiPicker.cs` 330、`EmojiPack.cs` 118、`main.cjs` 624、`ChatComposerRegression.cs` 564、`MainForm.Designer.cs` 691。

## 三、未验证边界

以下内容没有在本轮写成通过，仍需现场或独立环境验收：

- 真实双机新版/旧版互通、缺资源端表情令牌回退、复制与历史重开。
- Windows MSI/绿色包安装、100%/125%/150% DPI 下的浮层位置、输入区叠绘和真实截图工具/Excel/WPS 粘贴。
- Linux Xorg、原生 Wayland、XWayland Electron 实机运行与系统剪贴板；Linux ARM64/x86_64 ELF 实跑及 `tests/linux_desktop_regression.py`。
- 真实 Linux 桌面下 `emoji-pack` IPC 的启动耗时、重连缓存效果和输入法交互。

请 Gemini 独立复审本节 P0/P1 代码与证据，并在完成后按 `CRDocs/RULES.md` 回写 `CRDocs/REVIEW.md`、`CRDocs/STATUS.md`，只有独立审核者可以将状态改为 `APPROVED`。

---

# LC-CC-007 — 微信比例表情选择器、输入框图片预览与内置表情资源独立审核（Commit `6248514`）

- **任务编号**：`LC-CC-007`
- **任务主题**：微信比例表情选择器、输入框图片预览与内置表情资源
- **审核基线**：代码提交 `6248514`（`修复微信表情面板与内置表情资源`），CRDocs 送审基线 `b0cfe56`
- **独立审核方**：Gemini
- **审核日期**：2026-11-25
- **审核结论**：**REJECTED**（发现 1 项 P0 阻断、2 项 P1 阻断与 2 项 P2 问题，打回 Codex 整改）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/STATUS.md`、`CRDocs/DEFERRED.md`、`Docs/GrokBot_UI_Design_System.md`，以及提交 `6248514` 完整 diff 与宿主机独立实测。

## 一、六维重点核对结论

| 核对维度 | 结论 | 核对依据与发现 |
| --- | :---: | --- |
| 1. `ChatEmojiPicker.cs` | **WARN (P2)** | 复用 `SessionListBox` 暗色自绘滑块，符合 Zero White Scrollbar 契约，支持 Esc/失焦关闭与工作区防溢出；但实际尺寸为 `408×548`（非送审记录所称 `496×548`），「最近使用」仅静态切片前 12 项未记录使用历史，底部分类栏 5 个图标纯装饰无交互，缺少 `1px #2C2C32` 外边框、`16px` 圆角/`CS_DROPSHADOW` 与 `LostFocus` 重绘，且右侧滑块 12px 热区与第 8 列重叠（`LC-CC-007-P2-1`） |
| 2. `ChatInputBox.cs` | **FAIL (P0 + P1)** | 底层 `GetMessageText()` 未污染 `[表情名称]` 原文，但 `EmojiOverlay` 存在严重阻断缺陷：`Dock = Fill` + `OptimizedDoubleBuffer` + 空 `OnPaintBackground` 在真实 Win32 绘制中将 `88.5%` 区域涂成纯黑 `(0,0,0)` 遮挡文字与光标；仅设 `Enabled = false` 未处理 `WM_NCHITTEST -> HTTRANSPARENT` 导致鼠标点击/划选/右键被吞（`LC-CC-007-P0-1`）；逐字 `MeasureString` 偏离 RichEdit 真实坐标（短文本已差 `6.2px`）且不随水平滚动/选区/代理对更新（`LC-CC-007-P1-1`）；继承 `RichTextBox` 后 `MaxLength` 变为 `2147483647` 且 `Ctrl+V` 允许贴入富文本 RTF 样式（`LC-CC-007-P1-2`） |
| 3. 内置资源与嵌入 | **PASS (含 P2 建议)** | `assets/WeChatEmoji.pack` 实测 `990,849` 字节、魔数 `LCEMOJI1`、恰好 `109` 项有效 PNG（全数 `<= 64×64`、令牌合法、无重复、无尾字节）；Windows `LanChat.exe` 与 Linux `lanchat.dll` 均已真实嵌入 `LanChat.WeChatEmoji.pack`；隔离 `LOCALAPPDATA` 复跑通过，不再依赖开发机外部目录；仅 `EmojiPack.cs` 缺少 24 字符令牌长度与 PNG IHDR `<= 64` 校验（`LC-CC-007-P2-2`） |
| 4. Linux `emoji-pack` IPC | **PASS (含 P2 建议)** | `DesktopService.cs:172-175` 返回 `[[token, "data:image/png;base64,..."], ...]`，JSON 结构与 `presentation.js` 匹配；`renderer.js:546` 仅启动时请求一次，单行 JSONL 约 `1.32 MB`（`1,322,980` 字节），未破坏 JSONL 管道；但 `main.cjs:568` 在后端返回空数组 `[]` 时未回退 `readEmojiPack()`，且无主进程缓存（`LC-CC-007-P2-2`） |
| 5. LANC 协议与 JSONL 兼容 | **PASS** | 未改动 `Models.cs`、`NetworkService*.cs`、`ChatHistoryService.cs`、`SessionManager.cs`；LANC 魔数、Type 1 `Content`（仍传输原始 `[表情名称]`）、Type 2 文件帧与 JSONL 历史格式零改动，未将本地路径或图片字节写入网络协议 |
| 6. 行数门禁、中文注释与脚本 | **PASS** | 全部改动文件 `< 1000` 行软门禁（`EmojiPack.cs` 104、`ChatInputBox.cs` 273、`ChatEmojiPicker.cs` 283、`Tests/ChatComposerRegression.cs` 376、`DesktopService.cs` 492、`MainForm.Chat.cs` 590、`main.cjs` 600、`MainForm.Designer.cs` 690），注释为中文，`git diff --check` 返回 0 |

## 二、问题分级清单（P0 / P1 / P2 / P3）

| 问题 ID | 严重度 | 位置 | 问题描述与复现证据 | 整改要求 |
| --- | :---: | --- | --- | --- |
| `LC-CC-007-P0-1` | **P0** | `src/UI/Controls/ChatInputBox.cs:35-36, 61-117` | `EmojiOverlay` 以 `Dock = Fill` 覆盖 `RichTextBox`：1) 启用 `OptimizedDoubleBuffer` 但 `OnPaintBackground` 为空且无透明合成，`PrintWindow` 实测 `400×26`（`10400` 像素）中输入 `"Hello [敲打] World"` 后有 **`9200` 像素（88.5%）为纯黑 `(0,0,0)`**，直接遮黑普通文字、`#202024` 背景与光标；2) 仅设 `Enabled = false` 未拦截 `WM_NCHITTEST` 返回 `HTTRANSPARENT (-1)`，实测 `SendMessage(overlay.Handle, WM_NCHITTEST)` 返回 `1 (HTCLIENT)` 且 `ChildWindowFromPoint` 命中覆盖层，导致无法点击定位光标、划选文字或右键弹出 `GrokPopupMenu`。现有 `VerifyRichEmojiInput` 仅对覆盖层自身调用 `DrawToBitmap` 未测到上述失效。 | 彻底重构输入区表情渲染方案（例如取消全覆盖不透明子控件，改为在 `RichTextBox` `WM_PAINT` 后直接于自身 DC 上按实际字符位置叠绘表情，或采用严格 `WS_EX_TRANSPARENT` + `WM_NCHITTEST -> HTTRANSPARENT` + 非双缓冲局部重绘），并补充真实 `ChatInputBox` 渲染背景/文字可见性（非纯黑）与鼠标点击/右键命中回归断言。 |
| `LC-CC-007-P1-1` | **P1** | `src/UI/Controls/ChatInputBox.cs:79-116` | 表情绘制位置使用逐 UTF-16 字符 `MeasureString(..., GenericTypographic)` 从 `x = 0` 累加，不依赖 RichEdit 实际排版：1) `"Hello [敲打] World"` 实测 `RichTextBox.GetPositionFromCharIndex(6).X = 43`，覆盖层算出 `36.8`（偏差 `6.2px`）；2) 长文本水平滚动后实测 `RichEditX = 350`（视口内），覆盖层画在 `x > 650`（视口外），裸露原文 `[敲打]`；3) `text.Substring(index, 1)` 拆分代理对 Emoji；4) 未在 `HScroll`/`SelectionChanged`/`WM_PAINT` 时重绘，光标与选区不同步。 | 改用 `RichTextBox.GetPositionFromCharIndex(index)`（及结束字符坐标）定位每个表情令牌的真实客户区坐标；仅绘制位于可见 `ClientRectangle` 内的令牌；在 `TextChanged`、`HScroll`、`SelectionChanged`、滚动/重绘消息后触发更新，且不遮挡选区高亮与插入符。 |
| `LC-CC-007-P1-2` | **P1** | `src/UI/Controls/ChatInputBox.cs:15, 26-40, 123-187`；`src/MainForm.Designer.cs:501-512` | 改为继承 `RichTextBox` 后：1) 未设置 `MaxLength`，实测 `BOX_MAXLENGTH = 2147483647`（原 `TextBox` 为 `32767`，与 Linux `32768` 对齐），使 `MainForm.Chat.cs:446` 长度保护实质失效；2) 剪贴板含富文本（RTF/HTML）+ 文本时 `TryPasteAttachment()` 返回 `false` 落回 `RichTextBox` 原生粘贴，实测会贴入 `20pt` 红色等 RTF 格式，破坏 `11.0pt #F2F2F6` 纯文本输入规范；3) 构造设 `Multiline = true` 而 `MainForm.Designer.cs:503` 改回 `Multiline = false`，配置冲突。 | 显式设置 `MaxLength = 32767`（并确认插入表情与粘贴均遵守上限）；在 `ProcessCmdKey` / `WM_PASTE` 中对纯文本粘贴统一走 `PasteClipboardText()`（或 `Paste(DataFormats.GetFormat(DataFormats.UnicodeText))`）禁止贴入 RTF/OLE 格式；统一单行 `Multiline = false` 契约并补充最大长度与 RTF 纯文本化回归断言。 |
| `LC-CC-007-P2-1` | **P2** | `src/UI/Controls/ChatEmojiPicker.cs:45-47, 54, 60-61, 173-189, 204-243` | 1) 浮层宽度为 `408×548`（与送审记录“约 496×548”不符）；2) 「最近使用」仅静态复制 `_entries` 前 12 项，选中表情后不更新；3) 底部分类栏 5 个图标无点击/悬停/提示；4) 缺少 `1px #2C2C32` 描边、`16px` 圆角/`CS_DROPSHADOW` 及网格 `LostFocus` 重绘；5) 网格宽度 `384 = 8×48` 未为右侧自绘滑块预留间距，滑块 12px 热区与第 8 列重叠。 | 整改 P0/P1 时一并评估修复：记录真实最近使用列表（至少进程内更新）、为网格预留滑块右边距、补齐 `1px #2C2C32` 外框与 `LostFocus` 重绘、移除无效装饰图标或明确其交互，并使文档尺寸描述与代码一致。 |
| `LC-CC-007-P2-2` | **P2** | `src/linux/desktop/main.cjs:566-569`；`src/Services/EmojiPack.cs:55-90` | 1) `main.cjs` 仅在 `service.request('emoji-pack')` 抛错时回退 `readEmojiPack()`，返回空数组 `[]` 时未回退；2) `EmojiPack.cs` 未校验令牌长度 `<= 24` 字符及 PNG IHDR 尺寸 `<= 64×64`（与 `ChatEmojiPicker` / `main.cjs` 校验尺度不一致）；3) 单次 `emoji-pack` 响应 JSONL 约 `1.32 MB`，重连时重复全量回传。 | 建议 `main.cjs` 对非空结果做内存缓存并在空数组时回退 `readEmojiPack()`；建议 `EmojiPack.cs` 补齐 24 字符令牌长度与 PNG IHDR `<= 64×64` 校验。 |
| — | **P3** | `src/UI/Controls/ChatEmojiPicker.cs:233-240` | 末行（5 项）上 `_allColumn` 处于 `5..7` 时按 `Left` 会先夹紧到列 4 而非左移至列 3。 | 可顺手在 `Left`/`Right` 计算前先将 `column` 夹紧到 `count - 1`。 |

## 三、独立验证证据与边界区分

### 1. 已在 Windows 宿主机独立验证的内容

| 命令/专项探针 | 退出码 | 实测结果 |
| --- | :---: | --- |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | 0 | 既有 7 项回归输出全部通过 |
| 隔离 `LOCALAPPDATA` 后执行 `tests/chat_composer_regression.ps1 -ExpectLocalPack` | 0 | 确认内置资源不依赖开发机外部 `WeChatEmoji` 目录 |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 0 | `Native Windows compile: ok` 及全部既有好友刷新/编辑器/草稿回归通过 |
| `cmd /c "Tools\Scripts\Compile.bat < NUL"` + 反射核验 `release\LanChat.exe`（验证后已还原工作区跟踪二进制） | 0 | 嵌入 `LanChat.app.ico`、`LanChat.WeChatEmoji.pack`（`990,849` 字节） |
| `assets/WeChatEmoji.pack` 二进制逐项解析探针 | 0 | `size=990849`, `magic=LCEMOJI1`, `count=109`, `badPng=0`, `badDim=0`, `badTok=0`, `dup=0`, `maxPng=11315`, `trailing=0`, `estJsonlBytes=1322980` |
| 真实 WinForms `ChatInputBox` 渲染与 Win32 命中探针 | 0 | **复现 P0/P1**：`PrintWindow` 9200/10400 像素纯黑；`WM_NCHITTEST = 1 (HTCLIENT)`；`ChildWindowFromPoint` 命中覆盖层；`RichEditX=43` vs `OverlayMeasuredX=36.8`；滚动后 `RichEditX=350`；`MaxLength=2147483647`；`RichTextBox` 接受 `20pt` 红色 RTF |
| `C:\Program Files\nodejs\node.exe`（`v24.18.0`）执行 `--check src/linux/desktop/main.cjs`、`tests/linux_desktop_ui_regression.js` 及直接调用 `tests/linux_desktop_ipc_regression.js` | 0 | 全部通过（注：系统 `PATH` 中 `C:\WINDOWS\system32\node` 为空桩，需显式调用 `C:\Program Files\nodejs\node.exe`） |
| `git diff --check` | 0 | 无空白错误 |

### 2. 仅完成 Linux `net10.0` DLL 构建但未在 Linux 实机运行的内容

- `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`：退出码 `0`，0 警告、0 错误，生成 `src/linux/LanChat.Linux/bin/Release/net10.0/lanchat.dll`（`1,097,728` 字节），字节核验包含 `LanChat.WeChatEmoji.pack` 与 `LCEMOJI1`。
- 该 DLL **尚未在 Linux 实机运行**，也未实跑 `DesktopService` 的 `emoji-pack` 管道通讯。

### 3. 尚未验证的跨环境与实机内容（不得视为通过，继续保留于 `CRDocs/DEFERRED.md`）

- 真实双机的新版对新版、新版对旧版、无资源对端的表情发送、复制、历史重开与可读回退。
- Windows 真实 MSI / 绿色包安装与多 DPI（100% / 125% / 150%）下的表情浮层定位、输入框图文混排交互与光标体验。
- Linux Xorg / 原生 Wayland / XWayland 下的 Electron 实机运行（含 `emoji-pack` IPC 启动耗时与渲染）。
- Linux ARM64 / x86_64 自包含 ELF 实机运行与 `tests/linux_desktop_regression.py` 回归。

---

# LC-FILE-TRANSFER-STABILITY-001 — 重复接收气泡修复独立审核（Commit `4e8bbc4`）

- **任务编号**：`LC-FILE-TRANSFER-STABILITY-001`
- **任务主题**：快速接收文件时重复气泡修复
- **审核基线**：`HEAD` 提交 `4e8bbc4`（`fix: prevent duplicate incoming file bubbles`）
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-25
- **审核结论**：**APPROVED**（当前开发、Windows 宿主机回归与 Linux `net10.0` 构建范围放行）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、本文件既有记录，以及 `4e8bbc4` 完整 diff（`src/Controllers/FileTransferController.cs`、`tests/ChatComposerRegression.cs`、`CHANGELOG.md`）。

## 审核重点逐项核对

| 核对项 | 结论 | 核对依据 |
| --- | :---: | --- |
| 1. `HandleFileReceived` 与 50ms 延迟进度竞态修复 | PASS | `FileTransferController.cs:296-308` 新增 `RemovePendingProgress(fileMsg.FileTransferId)`，在 `HandleFileReceived` 接管完成消息前于 `lock (_progressGate)` 内从 `_pendingProgress` 移除同 `TransferId` 的未刷新进度；由于 `FlushPendingProgress` 与 `HandleFileReceived` 均序列化于 UI 线程且共用 `_progressGate`，即使 50ms 定时器已投递 `BeginInvoke`，晚到的 `FlushPendingProgress` 也不再看到该进度 |
| 2. 完成先于延迟进度时不追加第二个气泡 | PASS | `HandleFileReceived` 撤销 `_pendingProgress` 后，仅当 `_incoming` 未命中时才调用 `_sessionManager.AddMessage(convKey, fileMsg)` 并以 `added = true` 发布一次完成卡片，不会再被后续 `HandleIncomingProgress` 追加“接收完成 · 100%”气泡 |
| 3. 普通顺序（进度先到、完成后到）仍复用同一气泡 | PASS | 50ms 定时器先执行 `FlushPendingProgress` 时，`_pendingProgress` 已被清空并写入 `_incoming[Tuple(convKey, transferId)]`；随后 `HandleFileReceived` 命中 `_incoming` 并复用原 `ChatMessage` 更新 `FilePath`/`TransferProgress = null`（`added = false`），仅保留一个气泡 |
| 4. 不误删其他传输的待处理进度 | PASS | `RemovePendingProgress` 仅对非空 `fileMsg.FileTransferId` 执行 `_pendingProgress.Remove(transferId)`，其余并发传输拥有独立 `TransferId`，不受影响 |
| 5. `FileTransferId`、会话键、JSONL 与 LANC 兼容 | PASS | 未改动 `Models.cs`、`NetworkService*.cs`、`ChatHistoryService.cs` 与 `SessionManager.cs`，LANC 二进制封包、确认字节、会话键与 JSONL 历史格式完全兼容 |
| 6. `ChatComposerRegression.cs` 边界用例覆盖 | PASS | `VerifyIncomingCompletionRace`（`156-194` 行）模拟“最终进度（`IsCompleted = true`）经 `QueueProgress` 排队后、`HandleFileReceived` 先抵达”，等待 `150ms` 越过 50ms 定时器后断言会话内存、UI `TransferMessageChanged`（`added == true`）与 JSONL 历史均严格为 1 条 |
| 7. 行数门禁与中文注释 | PASS | 变更文件均 `< 1000` 行（`FileTransferController.cs` 433 行、`ChatComposerRegression.cs` 350 行），新增注释为中文 |
| 8. `v2.0.22` 已发布资产边界 | PASS | `CHANGELOG.md` 将本轮修复置于 `## [Unreleased]` 节区，未错误标记为包含在已发布的 `v2.0.22` 资产中 |

## 独立复跑验证证据（均退出码 0）

1. `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：退出码 0，输出包含 `文件接收完成先于延迟进度刷新时不会重复追加气泡` 及全部既有回归通过。
2. `cmd /c "Tools\Scripts\Compile.bat < NUL"`：退出码 0，`release\LanChat.exe` 编译成功。
3. `powershell -NoProfile -ExecutionPolicy Bypass -File tests/file_transfer_stability_regression.ps1`：退出码 0，输出 `文件传输稳定性边界回归通过：0 字节、64KB、全局 6、单目标 2`。
4. `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：退出码 0，输出包含 `Native Windows compile: ok` 与全部既有回归通过。
5. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`（SDK `10.0.404`）：退出码 0，输出 `LanChat.Linux -> ...\net10.0\lanchat.dll`，0 警告、0 错误。
6. `git diff --check`：退出码 0，无空白错误。

## 问题分级清单

- **P0 / P1 / P2 / P3**：均无。

## 未验证边界（继续保留于 `CRDocs/DEFERRED.md`）

- Linux 实机 / ARM64 / x86_64 ELF 实跑、Linux Xorg / Wayland / XWayland Electron 桌面与剪贴板、真实双机 LAN（含新旧版本互通）及 Windows 真实桌面截图工具 / Excel / WPS 粘贴仍未现场验证。

---

# LC-FILE-TRANSFER-STABILITY-001 — 本轮重复接收气泡修复执行记录

- **任务编号**：`LC-FILE-TRANSFER-STABILITY-001`
- **任务主题**：快速接收时重复文件气泡修复
- **执行方**：Codex
- **执行日期**：2026-11-25
- **审核状态**：`WAITING_FOR_GROK`
- **关联记录**：[上一轮独立审核](#lc-file-transfer-stability-001--独立审核)

## 改动

1. 在 `src/Controllers/FileTransferController.cs` 增加待处理进度撤销逻辑。`HandleFileReceived` 处理完成消息前，按 `FileTransferId` 移除 `_pendingProgress` 中尚未由 50ms 定时器刷新的一条进度，避免完成卡片先追加后又被迟到进度创建第二条气泡。
2. 保持既有 `_incoming` 会话/传输号关联、完成消息复用、历史持久化和 LANC 二进制封包不变。
3. 在 `tests/ChatComposerRegression.cs` 增加完成先于延迟进度刷新时序回归，验证内存消息数量、UI 追加事件数量和 JSONL 历史数量均为 1。

## Windows 与 Linux 验证

1. `tests/chat_composer_regression.ps1 -ExpectLocalPack`：返回 0；输出“文件接收完成先于延迟进度刷新时不会重复追加气泡”。
2. `cmd /c "Tools\\Scripts\\Compile.bat < NUL"`：返回 0，Windows 原生编译成功。
3. `tests/file_transfer_stability_regression.ps1`：返回 0，0 字节、64KB、全局 6、单目标 2 通过。
4. `tests/friend_refresh_regression.ps1`：返回 0，原有 Windows 好友刷新与入口边界通过。
5. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`：返回 0，Linux `net10.0` 构建 0 警告、0 错误。
6. `git diff --check`：待提交前执行，必须无空白错误。

## 未验证边界

真实双机新旧版本互通、Linux ARM64/x86_64 ELF 实机、Linux Xorg/Wayland/XWayland 剪贴板、Windows 截图工具/Excel/WPS 实际粘贴及长时间吞吐仍未验证，继续遵循 `CRDocs/DEFERRED.md`。本轮尚未重新打包或替换已发布的 v2.0.22 资产，待独立审核通过后再决定版本发布策略。

---

# LC-FILE-TRANSFER-STABILITY-001 — 独立审核

- **任务编号**：`LC-FILE-TRANSFER-STABILITY-001`
- **任务主题**：文件传输稳定性与并发限流加固（P0 / P1 / P2）
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-25
- **审核状态**：**APPROVED**（当前开发与 Windows 宿主机回归范围放行）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/STATUS.md`、本文件底部同名执行记录，以及工作区实际改动源码与测试（`src/NetworkService.cs`、`src/NetworkService.Transfer.cs`、`src/Controllers/FileTransferController.cs`、`src/MainForm.Files.cs`、`src/MainForm.Chat.cs`、`src/linux/LanChat.Linux/DesktopService.cs`、`tests/FileTransferStabilityRegression.cs`、`tests/file_transfer_stability_regression.ps1`、`tests/FriendRefreshRegression.cs`、`tests/ConcurrentReceive.cs`）。

## 独立复跑与验证边界

### 1. Windows 宿主机独立复跑（均退出码 0）

1. `cmd /c "Tools\Scripts\Compile.bat < NUL"`：退出码 0，Windows 原生 `release\LanChat.exe` 编译成功。
2. `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：退出码 0，包含 `Native Windows compile: ok`、32 文件 / 16 群聊收件人入口边界断言及既有 WinForms 回归通过。
3. `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：退出码 0，包含剪贴板优先级、LANC 文本/图片回环、JSONL 历史及 `LC-CC-004` 截图清理回归通过。
4. `powershell -NoProfile -ExecutionPolicy Bypass -File tests/file_transfer_stability_regression.ps1`：退出码 0，输出 `文件传输稳定性边界回归通过：0 字节、64KB、全局 6、单目标 2`。
5. 独立以 `csc.exe` 编译 `src/Models.cs`、`src/NetworkService*.cs` 与 `tests/ConcurrentReceive.cs` 并传入临时目录运行：退出码 0，输出 `Final ACK: 1, 1; Complete events: 2`，双路同名文件 SHA-256 校验通过（随后清理临时目录）。
6. `dotnet --list-sdks` 实测宿主仅安装 `9.0.304`；`dotnet build src/linux/LanChat.Linux/LanChat.Linux.csproj --no-restore` 退出码 1，报 `NETSDK1045`（当前 .NET SDK 不支持面向 `.NET 10.0`）。
7. 行数门禁与注释规范：全部改动/新增手写文件均 `< 1000` 行软门禁（`NetworkService.Transfer.cs` 615、`MainForm.Files.cs` 503、`DesktopService.cs` 488、`FriendRefreshRegression.cs` 486、`FileTransferController.cs` 424、`NetworkService.cs` 265、`FileTransferStabilityRegression.cs` 244、`file_transfer_stability_regression.ps1` 16），新增注释与 XML 文档均为中文。

### 2. 未验证边界（继续保留，不得视同已验收）

- **正式 Linux `net10.0` 构建**：宿主仅安装 `.NET SDK 9.0.304`，`net10.0` 正式编译未完成；此前执行者的 `net9.0` 临时编译仅作语法兼容参考，不得替代正式 `net10.0` 构建。
- **Linux 实机与双机联测**：Linux ARM64 / x86_64 ELF 实机、Linux Electron Xorg / Wayland / XWayland、真实双机新旧客户端互通、真实 Windows 截图工具/Excel/WPS 粘贴，以及真实 LAN 吞吐与连接数取证均尚未验证。

## 审核结论与维度核对

**APPROVED（当前开发与 Windows 宿主机回归范围放行）**。无 P0、无 P1、无 P2 阻断遗留。

| 核对维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. LANC 协议向后兼容 | PASS | `NetworkService.Transfer.cs` 未改动 `MAGIC`、`packetType 2` 字段顺序（`transferId`、`senderId`、`senderNick`、`fileName`、`fileSize`）及 `0x01`/`0x00` 确认字节 |
| 2. Stopwatch 速率有限性 | PASS | 收发双方均使用 `Stopwatch.GetTimestamp()` 单调计时；`CalculateTransferSpeed` 对 `bytes <= 0`、`elapsedSeconds <= 0.001`、`NaN`/`Infinity`/负数统一返回 `0d`；UI 格式化与 Linux IPC 同步兜底 |
| 3. 流长度 TOCTOU、截断、0 字节与 `leaveOpen` | PASS | `SendFileCore` 先打开带 `FileShare` 的 `FileStream` 再读取同一实例 `fs.Length`；按剩余字节读取并在 `read == 0` 时抛 `EndOfStreamException`；0 字节文件跳过循环并完成收尾；文件流 `BinaryReader`/`BinaryWriter` 均设 `leaveOpen: true` |
| 4. 分阶段超时与 Socket 缓冲 | PASS | 握手接收超时 10s、正文发送超时 30s、最终确认按文件大小设为 60s~300s；packetType 2 协商前后双端设置 256KB `SendBufferSize`/`ReceiveBufferSize` |
| 5. 有界出站并发与公平轮询 | PASS | `NetworkService` 维持全局最多 6 路、单目标最多 2 路；`TakeReadyOutgoingTransfersLocked` 按节点轮询出队；`RunOutgoingTransfer` 的 `finally` 保证异常时回收计数并继续出队；`Dispose()` 调用 `RejectQueuedOutgoingTransfers()` 拒绝排队任务 |
| 6. 32 文件 / 16 成员入口限制 | PASS | `MainForm.Files.cs` 在剪贴板文件/截图、文件选择器、拖拽与群发入口均强制校验单批 32 文件与群聊 16 在线成员并给出中文提示；无效/离线全失败时明确提示 |
| 7. 托管截图引用计数与清理 | PASS | `SendFilesToPeers` 仅首次认领移交预览句柄，`FileTransferController` 以 `ManagedPasteState` 跟踪 `Remaining` 与 `HasSavedReference`；多目标成功/部分失败保留文件，全部失败删除，退出时 `DiscardOutgoingPastes()` 清理未落盘引用 |
| 8. Windows UI 进度合批 | PASS | `FileTransferController` 以 50ms `Timer` 将高频接收进度合并至 `_pendingProgress` 后单次 `SafeInvoke` 刷新；发送端已在 `SendFileCore` 按 200ms 节流，并在控件销毁时释放定时器 |
| 9. Linux IPC 兜底与 `managedPaste` | PASS | `DesktopService.cs` 对 `SpeedMBps` 使用 `double.IsFinite` 归零；`WriteOutput` 对单帧构造/序列化异常做隔离记录不中断后续输出；`SendFile` 将 `managedPaste` 透传给 `allowSourceDelete` |
| 10. 边界回归与测试覆盖 | PASS | `FileTransferStabilityRegression.cs` 实测 0 字节、64KB、约 1MB 接收速率与全局 6 / 单目标 2 并发上限；`FriendRefreshRegression.cs` 覆盖 32 文件与 16 成员边界；`ConcurrentReceive.cs` 复跑通过 |

---

# 历史执行记录与审核归档

> 以下为 `LC-FILE-TRANSFER-STABILITY-001` 送审前执行记录（位于本文件底部）及历史任务审核归档。

---

# 本轮交付 — Windows 缩放稳定性、11 色外观、传输速率与群聊文件多选（独立复审）

- **日期**：2026-11-25
- **执行者**：Codex（基线提交 `e3e9a58`、返修提交 `a056460`）
- **审核方**：独立审核方
- **审核状态**：**APPROVED**（当前开发与 Windows 宿主机回归范围放行）
- **范围**：头像缩放防闪烁、附件图 11 色外观预设、Windows/Linux 文件气泡当前速率、群聊在线收件人多选与托管截图副本清理，以及 `LC-RV-001` 无在线好友空列表返修。
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`，以及 `e3e9a58`、`a056460` 实际 diff 与相关调用链。
- **发布确认**：v2.0.21 已由 `Tools/Scripts/Publish.bat` 完成打包并上传 GitHub Release，发布提交为 `f09226b`；更新日志已写入 `CHANGELOG.md`。

## 独立复跑与验证边界

### 1. Windows 宿主机独立复跑（均退出码 0）

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：退出码 0（`Native Windows compile: ok`、`LC-FR-001`~`004`/`008`、`LC-CHAT-COMPOSER-001`、`LC-CC-006`、头像 `DoubleBufferedPanel`、群聊收件人多选、0 在线好友空列表与 `null` 快照归一化断言通过）。
2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：退出码 0（包含 `外观 11 色调色盘与发送/接收速率格式化断言通过`、`LC-CC-005`、`LC-CC-004` 及 Emoji/PNG/LANC/历史回归）。
3. `C:\Program Files\nodejs\node.exe` 对 `src/linux/desktop/*.js`、`src/linux/desktop/*.cjs` 与 `tests/linux_desktop_*_regression.js` 共 15 个文件执行 `--check`，并运行 `tests/linux_desktop_ui_regression.js`：全部退出码 0，输出 `LC-CC-004: Linux IPC 发送终态、退出预检与管道晚到确认清理通过` 与 `linux desktop ui regression: ok`。
4. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`：退出码 0，0 警告、0 错误。
5. `git diff --check`：退出码 0，无空白错误；本轮改动手写源码与测试文件均 `< 1000` 行软门禁（最高为 `tests/linux_desktop_ui_regression.js` 907 行）。

### 2. 未验证的跨环境边界（继续保留于 `CRDocs/DEFERRED.md`）

- 真实 Windows 拖拽缩放长时间录屏、真实截图工具（Win+Shift+S / 微信截图）/ Excel / WPS 粘贴。
- Linux Xorg / Wayland / XWayland 实机 Electron、最新 Linux ELF 实跑（`tests/linux_desktop_regression.py`）与真实双机局域网联测。

## 审核结论与维度核对

**APPROVED（当前范围放行）**。无 P0、无 P1 遗留；`LC-RV-001` 已复核 **RESOLVED**：无在线好友时 `PickFileRecipients` 返回 `new List<PeerInfo>()`，`NormalizeRecipientList` 将 `null` 快照归一化，关闭警告框后 `PromptPeerAndSendFiles` 与 `PickFileRecipient` 访问 `peers.Count` 不再触发 `NullReferenceException`；0 在线好友与 `null` 快照自动化断言及全部宿主机回归复跑通过。

| 核对维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. 头像缩放防闪烁 | PASS | `src/MainForm.Designer.cs:210` 将 `_heroProfilePanel` 改为 `DoubleBufferedPanel`，`FriendRefreshRegression.cs:281` 已覆盖类型断言 |
| 2. 11 色配色与索引兼容 | PASS | `GrokTheme.cs`、`AppearanceSettingsPanel.cs`、`AppSettings.cs`（上限 11）、`LinuxProfile.cs`（上限 11）与 `appearance.js` 均严格对齐 11 色 RGB 与索引顺序，越界均做夹紧 |
| 3. 双向传输速率显示 | PASS | `NetworkService.Transfer.cs` 收发均写入 `SpeedMBps`；`FileTransferController.FormatTransferRate` 与 `conversation-view.js` 统一格式化 `KB/s` / `MB/s`；`ChatFileCardRenderer.cs` 与 `DesktopService.cs` 透传/渲染正常 |
| 4. 群聊多选与托管截图清理 | PASS | `PickFileRecipients` 多选与 `SendFilesToPeers` 为 `i > 0` 生成 `"群发-<guid>.png"` 托管副本及失败清理逻辑成立；`PickFileRecipients` 全路径返回非空 `List<PeerInfo>`，`NormalizeRecipientList` 归一化 `null` 快照，`PromptPeerAndSendFiles` 与 `PickFileRecipient` 不再触发 `NullReferenceException` |
| 5. LANC 协议与 JSONL 兼容 | PASS | 未改动 `Models.cs`、`NetworkService*.cs`、`ChatHistoryService.cs`、`SessionManager.cs`，LANC 封包与 JSONL 历史格式完全兼容 |
| 6. 行数门禁与注释规范 | PASS | 全部改动文件 `< 1000` 行软门禁，无空白错误，新增 XML 注释为中文 |

### 问题清单

| 问题 ID | 严重度 | 状态 | 位置与说明 |
| --- | :---: | :---: | --- |
| `LC-RV-001` | P1 | **RESOLVED** | `src/MainForm.Files.cs:264-287` 无在线好友分支已返回 `new List<PeerInfo>()`，并通过 `NormalizeRecipientList` 防御 `null` 快照；`FriendRefreshRegression.cs:283-285` 新增 0 在线好友和 `null` 归一化断言并复跑通过。 |

---

# LC-CC-005 / LC-CC-006 — 独立审核

- **任务编号**：`LC-CC-005` / `LC-CC-006`
- **任务主题**：Windows 图文混合剪贴板优先级、隐藏输入框首次右键暗色菜单（含复制/剪切/全选），以及组合表情像素命中、跨 CRLF 复制、会话草稿隔离与关闭清理回归断言
- **审核方**：独立审核方
- **审核日期**：2026-11-25
- **审核状态**：**APPROVED**（当前开发与 Windows 宿主机回归范围放行）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`，以及工作区实际 diff（`src/UI/Controls/ChatInputBox.cs`、`src/MainForm.Designer.cs`、`tests/ChatComposerRegression.cs`、`tests/FriendRefreshRegression.cs`）。

## 独立复跑与验证边界

### 1. Windows 宿主机独立复跑（均退出码 0）

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：退出码 0，输出 `LC-CC-005: Windows 图文混合剪贴板优先级断言通过` 及既有 Emoji、PNG/位图粘贴、LANC 回环、历史回读与 `LC-CC-004` 回归。
2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：退出码 0，输出包含 `Native Windows compile: ok`、`LC-CHAT-COMPOSER-001`（含隐藏输入框首次右键弹出 `GrokPopupMenu`）及 `LC-CC-006: 组合表情像素命中、跨 CRLF 复制、切会话草稿和关闭清理通过`。
3. `C:\Program Files\nodejs\node.exe` 对 `src/linux/desktop/*.js`、`src/linux/desktop/*.cjs` 与 `tests/linux_desktop_*_regression.js` 共 15 个文件执行 `--check`，并运行 `tests/linux_desktop_ui_regression.js`：全部退出码 0，输出 `LC-CC-004: Linux IPC 发送终态、退出预检与管道晚到确认清理通过` 与 `linux desktop ui regression: ok`。
4. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`：退出码 0，0 警告、0 错误。
5. `git diff --check`：退出码 0，无空白错误；变更文件行数均 `< 1000` 行软门禁（`ChatInputBox.cs` 180 / 编辑器 184 行、`MainForm.Designer.cs` 690 行、`ChatComposerRegression.cs` 291 / 295 行、`FriendRefreshRegression.cs` 466 / 472 行，工作区最高 `tests/linux_desktop_ui_regression.js` 905 / 922 行）。

### 2. 待验证的跨环境实机项（未验证，继续保留）

- **Windows 真实桌面剪贴板**：真实 Windows 截图工具（Win+Shift+S / 微信截图）与 Excel/WPS 单元格的 `Ctrl+V`、`Shift+Insert` 及右键粘贴。
- **Linux 实机、ELF 与双机联测**：Linux Xorg / Wayland / XWayland 下的 Electron 实机联调、最新 Linux ELF 实跑（`tests/linux_desktop_regression.py`）与真实双机局域网联测，继续保留于 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`、`LC-LINUX-002-ENV-001`、`LC-LINUX-002-GUI-001`）。

## 结论与六维核对

**APPROVED（当前范围放行）**。无 P0、无 P1 遗留；`LC-CC-005`、`LC-CC-006` 标记为 **RESOLVED**（Windows 代码实现与宿主机自动化回归闭环，真实桌面工具/Excel/WPS、Linux 实机/ELF 与双机联测保留于 `CRDocs/DEFERRED.md`）。

| 核对维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. 图文混合剪贴板优先级 | PASS | `ChatInputBox.TryPasteAttachment` 优先识别 `FileDrop`，随后以 `HasClipboardText` 检查非空 `UnicodeText`/`Text` 并返回 `false` 交给原生文本粘贴，仅无非空文本时才读取 `PNG`/`Bitmap`；`HasClipboardAttachment` 与 `ShowContextMenuAt` 同步遵循此优先级 |
| 2. 隐藏输入框首次右键与编辑菜单 | PASS | `MainForm.Designer.cs:471-477` 在 `_inputPanel.MouseDown` 右键显框聚焦后直接调用 `_txtInput.ShowContextMenuAt(_inputPanel.PointToScreen(e.Location))`；`ChatInputBox.WndProc` 接管 `WM_CONTEXTMENU` 统一展示暗色 `GrokPopupMenu`，按剪贴板与选区动态提供「粘贴」「粘贴附件」「复制」「剪切」「全选」 |
| 3. `LC-CC-006` 选区、复制与草稿断言 | PASS | `FriendRefreshRegression.cs` 在真实 `WeChatChatView` 上逐像素扫描 `"甲👨‍👩‍👧‍👦👍🏽🇨🇳❤️1️⃣\r\n乙"` 全部视觉行确认 `GetCharIndexAtPoint` 仅落于 `ChatTextLayout.NextBoundary` 边界，验证跨 CRLF 全选复制原文一致、切换会话草稿隔离/恢复及显式关闭清理 |
| 4. LANC 协议与 JSONL 兼容 | PASS | 未改动任何 LANC 封包结构、传输协议或 JSONL 历史格式与字段 |
| 5. 宿主机回归与构建 | PASS | 两套 Windows 回归、原生编译、Node 15 文件 `--check` 与 UI 回归、共享后端 `dotnet build` 及 `git diff --check` 全部退出码 0 |
| 6. 行数门禁与中文注释规范 | PASS | 全部变更文件 `< 1000` 行软门禁，新增注释与 XML 文档均为中文 |

| 问题 ID | 原严重度 | 结论 | 说明 |
| --- | :---: | :---: | --- |
| `LC-CC-005` | P2 | **RESOLVED** | 图文剪贴板优先级与隐藏输入框首次右键暗色菜单（含复制/剪切/全选）已落地并通过回归；真实 Excel/WPS 与截图工具实机验收保留于 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`） |
| `LC-CC-006` | P2 | **RESOLVED** | 组合表情像素命中、跨 CRLF 复制、会话草稿隔离/恢复与关闭清理自动化断言已补齐并通过 |

---

# LC-CC-004 — 独立审核

- **任务编号**：`LC-CC-004`
- **任务主题**：双端应用截图异步发送终态清理、历史引用保护与 Linux 退出并发收尾
- **审核方**：独立审核方
- **审核日期**：2026-11-25
- **审核状态**：**APPROVED**（当前开发与 Windows 宿主机回归范围放行）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`，以及以已放行的 `LC-CC-003` 工作区变更为基线的实际 diff 与完整调用链（`src/Controllers/FileTransferController.cs`、`src/MainForm.Chat.cs`、`src/MainForm.Files.cs`、`src/NetworkService.Transfer.cs`、`src/Services/ChatHistoryService.cs`、`src/Services/SessionManager.cs`、`src/linux/LanChat.Linux/DesktopService.cs`、`src/linux/desktop/{main.cjs,service.cjs}`、`tests/{ChatComposerRegression.cs,linux_desktop_ipc_regression.js}`）。

## 独立复跑与验证边界

### 1. Windows 宿主机独立复跑（均退出码 0）

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：退出码 0，输出包含 `LC-CC-004: Windows 拒收、断连、存储失败与退出清理通过，保留成功截图和用户原文件` 及原有 Emoji/剪贴板/LANC/历史回归。
2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：退出码 0（原生 `csc.exe` 编译、好友刷新/网卡重绑、侧栏交互与截图草稿暂存移除通过）。
3. 独立以 `csc.exe` 编译 `src/`（排除 `linux/`、`obj/`、`bin/`、`Uninstall.cs`）至临时目录并编译运行 `tests/ConcurrentReceive.cs`：退出码 0，输出 `Final ACK: 1, 1; Complete events: 2`，两份同名文件 SHA-256 校验通过（随后清理临时目录）。
4. `C:\Program Files\nodejs\node.exe` 对 `src/linux/desktop/*.js`、`src/linux/desktop/*.cjs` 与 `tests/linux_desktop_*_regression.js` 共 15 个文件执行 `--check`，并运行 `tests/linux_desktop_ui_regression.js`：全部退出码 0，输出 `LC-CC-004: Linux IPC 发送终态、退出预检与管道晚到确认清理通过` 与 `linux desktop ui regression: ok`。
5. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`：退出码 0，0 警告、0 错误。（注：本机未安装 PowerShell 7 `pwsh`，未直接执行 `bin/lc-cc-004-backend/verify.ps1`，`DesktopService.cs` 逻辑已通过源码与调用链静态核对。）
6. `git diff --check`：退出码 0，无空白错误；全部 18 个工作区改动/新增手写源码与测试文件均 `< 1000` 行软门禁（`LC-CC-004` 改动文件最高为 `main.cjs` 598 行 / 编辑器 611 行、`MainForm.Chat.cs` 588 行；工作区最高为 `tests/linux_desktop_ui_regression.js` 905 行 / 编辑器 923 行）。

### 2. 待验证的跨环境实机项（未验证，继续保留）

- **Linux 实机与 ELF**：Linux Xorg / Wayland / XWayland 下的真实 Electron 截图发送、预检/队列期间退出清理、最新 Linux ELF 实跑（`tests/linux_desktop_regression.py`）。
- **真实双机联测**：真实双机端到端截图拒收、断连、成功后历史重开、用户原文件保留，以及新旧客户端互通。上述实机项继续保留于 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`、`LC-LINUX-002-ENV-001`、`LC-LINUX-002-GUI-001`）。

## 结论与六维核对

**APPROVED（当前范围放行）**。无 P0、无 P1 遗留；`LC-CC-004` 标记为 **RESOLVED**（双端代码与宿主机回归闭环，实机/双机补验保留于 `CRDocs/DEFERRED.md` `LC-CC-ENV-001`）。

| 核对维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. Windows 截图所有权移交与退出/历史竞态 | PASS | `MainForm.Chat.cs:418` 经 `TakeDraftImage` 仅移交实际入队截图给 `FileTransferController._outgoingPastes`；完成回调与 `DiscardOutgoingPastes()` 共用 `lock (_outgoingPastes)`：退出先删则回调直接返回不写历史，发送先完则同步调用 `PersistExistingMessage(..., confirmStorage: true)` 写入并复核 JSONL，成功保留、失败删除；`NetworkService.Transfer.cs:384` 仅对托管截图开启 `FileShare.Delete` |
| 2. Linux `send-paste` 与 `before-quit` 交叠 | PASS | `stage-paste`、`discard-paste`、`send-paste` 与后台 `send()` 均纳入 `pasteOperations`；`before-quit` 置 `exiting = true` 后等待 `service.stop()`、`discardPastedFiles` 与全部 `pasteOperations`；预检与队列循环均检查 `exiting`，退出期间不再派发排队附件且立即清理已接管截图而非回写 `pendingPastes` |
| 3. 管道晚到确认与后端异常终止保留 | PASS | `sendingPastes` 跟踪在途托管截图，`child.on('event')` 收到 `historySaved === true` 置 `paste.saved = true`；`finally` 在断连时 `await child.stop()` 等 stdout 管道关闭排空晚到 `message` 事件后再判删；`service.cjs:27,79` 返回 `code === 0`，后端异常退出返回 `false` 时保留截图 |
| 4. 历史引用与用户原文件保护 | PASS | Windows 仅 `StageClipboardImage` 进入 `_draftImagePreviews`，Linux 仅 `stageClipboardImage` 置 `managed: true`；用户文件与普通发送沿用原路径与 `FileShare.Read`，不会被草稿/失败/退出清理误删；已写入 JSONL 的成功截图在退出与无 UI 句柄场景均保留 |
| 5. LANC 协议与 JSONL 兼容 | PASS | LANC 文件报价帧（`LANC` + `2`）、`0x01`/`0x00` 确认字节与 JSONL 记录格式零改动；`managedPaste` 与 `historySaved` 仅用于本地 Electron 与 `DesktopService` 管道 |
| 6. 行数门禁与中文注释规范 | PASS | 全部改动/新增文件 `< 1000` 行软门禁，新增注释均为中文，无空白错误 |

| 问题 ID | 原严重度 | 结论 | 说明 |
| --- | :---: | :---: | --- |
| `LC-CC-004` | P2 | **RESOLVED** | 双端异步传输终态清理、历史引用保护与退出并发收尾代码与宿主机回归通过；Linux 实机/ELF 与双机联测补验保留于 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`） |

---

# LC-CC-003 — 独立复审

- **任务编号**：`LC-CC-003`
- **任务主题**：Linux 侧栏摘要按可见元素截断、归档启动摘要透传、表情回显与缺图回退，以及 `renderer.js` 职责拆分
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-25
- **审核状态**：**APPROVED**（当前开发与宿主机回归范围放行）
- **依据**：`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`、`.claude/Rules.md`，以及工作区实际 diff（`src/linux/LanChat.Linux/DesktopService.cs`、`src/linux/desktop/{desktop.css,index.html,presentation.js,renderer.js,session-sidebar.js,conversation-view.js}`、`tests/linux_desktop_ui_regression.js`）与 `packaging/build-electron.py`。

## 独立复跑与验证边界

### 1. 宿主机回归与专项复跑（本次复审在 Windows 宿主机独立执行，均退出码 0）

1. `C:\Program Files\nodejs\node.exe`（`v24.18.0`）：对 `src/linux/desktop/` 下 13 个 JS/CJS 与 `tests/` 下 2 个回归脚本共 15 个文件逐一执行 `--check`，全部退出码 0；运行 `node tests/linux_desktop_ui_regression.js`，退出码 0，输出 `linux desktop ui regression: ok`。
2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：退出码 0（Windows 组合表情整段测量与 GDI+ 像素对照、本地表情加载、剪贴板数据、LANC 回环与历史回读通过）。
3. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：退出码 0（原生编译、好友刷新与网卡重绑、会话列表键盘/滑块、分组尺寸与截图草稿回归通过）。
4. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`：退出码 0，0 警告、0 错误。
5. 独立专项复跑（隔离临时夹具与 Edge Headless CDP）：
   - **侧栏摘要边界**：复测私聊 16 / 群聊 20 阈值下 `👨‍👩‍👧‍👦`、`👍🏽`、`🇨🇳`、`[敲打]`、`[未知令牌]` 的恰好达限、保留边界与舍弃边界，连续多令牌边界，`displayName` 13 字素边界（`keepTokensTogether = false`），`[文件]` 摘要与空会话兜底，全部通过。
   - **归档启动路径**：加载编译产物 `lanchat.dll` 对隔离 JSONL 历史目录执行 `SessionManager.LoadPersistedHistories()` 与 `DesktopService.ProcessLine("{\"id\":\"req-1\",\"method\":\"state\"}")`，确认私聊（39 字符）、文件（28 字符）及群聊（32 字符）归档 `summary` 均完整未截断返回；并在渲染端确认 `setPeers` 合并在线态时保留归档 `summary`。
   - **表情回显与回退**：在 Edge Headless（加载真实 `index.html`、`desktop.css` 与全部生产 JS，仅以 `/mock-bridge.js` 注入 `window.lanchat`）验证 420px 与 280px 窄侧栏：有效 PNG（`[敲打]`）真实解码（`naturalWidth === 1`）；坏图（`[坏图]`）触发 `error` 事件后移除 `.message-emoji`、保留 `.summary-token`（`display: inline-block`）并恢复非透明文字；缺图（`[未知]`）保留 `.summary-token`；单行 `nowrap` / `text-overflow: ellipsis` / `9.5pt`（`12.7px`）且不折行。
6. `git diff --check` 退出码 0；全量 8 个改动/新增源码与测试文件编辑器行数复核与 `IMPLEMENTATION.md` 一致且均 `< 1000` 行软门禁（`tests/linux_desktop_ui_regression.js` 923、`renderer.js` 690、`DesktopService.cs` 471、`presentation.js` 201、`conversation-view.js` 194、`desktop.css` 192、`session-sidebar.js` 146、`index.html` 90）；`packaging/build-electron.py:200-202` 自动收录 `src/linux/desktop/` 全目录文件，新拆模块打包路径完整。

### 2. Linux 实机与跨机验证边界（未验证，继续保留）

- 宿主机为 Windows，本轮**尚未验证** Linux Xorg / Wayland / XWayland 下的真实 Electron 45 窄侧栏缩略效果、Linux 系统 Emoji 字形与 DPI 缩放表现、真实系统剪贴板、Linux/QEMU 网络命名空间最新 ELF 实跑（`tests/linux_desktop_regression.py`），以及真实双机新旧客户端联测。上述跨环境验收继续保留在 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`、`LC-LINUX-002-ENV-001`、`LC-LINUX-002-GUI-001`）。

## 结论与六维核对

**APPROVED（当前范围放行）**。无 P0、无 P1 遗留；`LC-CC-003` 标记为 **RESOLVED**（宿主机代码与回归闭环，Linux 实机视觉补验保留于 `CRDocs/DEFERRED.md` `LC-CC-ENV-001`）；`LC-CC-006` 中 `renderer.js` 拆分部分已落地，剩余 Windows 选区与草稿用例保留于 `CRDocs/DEFERRED.md`。

| 核对维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. `renderer.js` 职责拆分与打包收录 | PASS | `renderer.js` 由 961 行降至 690 行，拆出 `session-sidebar.js`（146 行）与 `conversation-view.js`（194 行），`index.html` 按序加载，`build-electron.py` 自动收录全目录 |
| 2. 侧栏摘要字素与表情令牌边界 | PASS | `presentation.js` 以 `Intl.Segmenter`（`grapheme`）+ `EMOJI_TOKEN`（`/\[[^\[\]\s]{1,24}\]/g`）计数截断，私聊 16 / 群聊 20 阈值与 `displayName` 13 字素边界成立 |
| 3. 启动归档 `summary` 透传链路 | PASS | `DesktopService.cs:127` 传 `int.MaxValue` 输出完整归档摘要，`renderer.js` `setPeers` 合并在线状态时保留 `previous.summary`，由渲染端统一按可见元素截断 |
| 4. 侧栏表情回显与缺图/坏图回退 | PASS | `drawMessageText(..., true)` 复用气泡渲染逻辑；有图显 PNG，无包或单图缺失保留 `.summary-token` 可读文字，`img` `error` 清理 `.message-emoji` 并恢复文本与自然宽度 |
| 5. LANC 协议、历史格式与 Windows 兼容性 | PASS | 未改动 `Models.cs`、`NetworkService*.cs`、`ChatHistoryService.cs` 及 `SessionManager.GetLastMessageSnippet` 默认 20 上限，LANC 帧与 JSONL 历史完全向后兼容 |
| 6. 行数门禁与中文注释规范 | PASS | 全部改动/新增文件 `< 1000` 行软门禁，新增注释均为中文，无空白错误 |

| 问题 ID | 原严重度 | 结论 | 说明 |
| --- | :---: | :---: | --- |
| `LC-CC-003` | P2 | **RESOLVED** | 代码实现、回归测试与宿主机隔离夹具/Edge Headless 复验通过；Linux Xorg/Wayland/XWayland 实机窄侧栏与 DPI 视觉补验保留于 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`） |

---

# LC-CHAT-COMPOSER-001 — Round 2 独立复审

- **任务编号**：`LC-CHAT-COMPOSER-001`
- **任务主题**：微信表情导入与无资源回退、Unicode 组合表情排版与划选复制、在线/离线分组 8px 间距与圆点、截图粘贴草稿与发送（Round 2 返修复审）
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-24
- **审核状态**：**APPROVED**（当前开发与宿主机回归范围放行）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`（Round 2）、`CRDocs/REVIEW.md`（Round 1）、`CRDocs/DEFERRED.md`，以及工作区实际 diff、完整调用链与 20 个改动/新增源码及测试文件。

## 独立复跑与验证边界

### 1. 宿主机回归（本次复审在 Windows 宿主机独立复跑，均退出码 0）

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：通过。原生 `csc.exe` 生产编译通过；`LC-FR-001` ~ `LC-FR-004` / `LC-FR-008` 与 `LC-CHAT-COMPOSER-001`（真实 WinForms 列表 32/40px 分组间距、单组 32px、翠绿 `#1FD67E` / 珊瑚红 `#FC4455` 圆点、截图草稿暂存/缩略图比例与移除清理）全部通过。
2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：通过。输出 `BMP 肤色、ZWJ 和独立表情的 Segoe UI Emoji 整段测量及 GDI+ 像素对照通过` 与 `聊天表情布局、PNG 与位图粘贴数据、LANC 文本和图片回环及历史回读通过`，验证了 `✋🏽`、`✌🏽`、`❤‍🔥`、`✋`、`✨`、`☕` 的单段测量宽度、混合文本绘制宽度及 240×60 逐像素一致，以及 `[敲打]` 本地 64×64 加载、LANC 回环与历史回读。
3. 使用 `C:\Program Files\nodejs\node.exe` 对全部 7 个改动/新增 JS/CJS 文件（`src/linux/desktop/{main.cjs,overlay.js,preload.cjs,presentation.js,renderer.js}`、`tests/{linux_desktop_ui_regression.js,linux_desktop_ipc_regression.js}`）执行 `--check`，并运行 `tests/linux_desktop_ui_regression.js`（内联调用拆出的 `tests/linux_desktop_ipc_regression.js`）：全部退出码 0，输出 `linux desktop ui regression: ok`。
4. `python -m py_compile Tools/Scripts/import-wechat-emoji.py` 退出码 0；`bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false` 退出码 0（0 警告、0 错误）。
5. `git diff --check` 退出码 0；全量 20 个改动/新增手写源码与测试文件行数复核全部 `< 1000` 行软门禁：
   - 本轮变更文件：`src/UI/Renderers/ChatTextLayout.cs`（209）、`tests/ChatComposerRegression.cs`（188）、`src/linux/desktop/preload.cjs`（96）、`src/linux/desktop/main.cjs`（575）、`tests/linux_desktop_ui_regression.js`（869）、`tests/linux_desktop_ipc_regression.js`（220）；
   - 全工作区最高为 `src/linux/desktop/renderer.js`（961 行，本轮未触碰，已登记 `LC-CC-006` 要求下次扩展前先拆分）。

### 2. 真实双机验证（未验证）

- 尚未在两台物理/虚拟机间实测：双端同包微信表情逐条收发、复制与历史重开；新版与未导入资源端或公开旧版跨机互通（可读 `[名称]`、Unicode 组合表情、截图 PNG 文件卡片打开）；以及真实 Windows 桌面截图工具（Win+Shift+S / 微信截图 / 图像应用）的 `Ctrl+V`、`Shift+Insert`、右键粘贴链路。继续保留在 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`）。

### 3. Linux 实机验收（未验证）

- 宿主机为 Windows，本轮仅完成 Node 桩 IPC/模拟 DOM 回归与共享后端宿主机构建；**尚未验证** Linux/QEMU 最新 ELF 实跑（`tests/linux_desktop_regression.py`）、Linux Electron 45 在 Xorg/Wayland/XWayland 下的真实 `clipboard.read()`、截图工具/文件管理器/浏览器复制图像时的 PNG/JPEG 兼容回退、表情网格暗色滑块与实机分组视觉对齐。继续保留在 `CRDocs/DEFERRED.md`（`LC-CC-ENV-001`、`LC-LINUX-002-ENV-001`、`LC-LINUX-002-GUI-001`）。

## 结论

**APPROVED（当前范围放行）**。无 P0、无 P1 遗留。Round 1 阻断放行的全部 2 项 P1（`LC-CC-001`、`LC-CC-002`）均已按要求落地且经源码调用链审查与独立复跑验证闭环；Node IPC 专项用例已按职责拆至 `tests/linux_desktop_ipc_regression.js`（220 行），原入口 `tests/linux_desktop_ui_regression.js` 回落至 869 行；4 项 P2（`LC-CC-003` ~ `LC-CC-006`）与跨环境验收项（`LC-CC-ENV-001`）均已带触发阶段与验收条件登记至 `CRDocs/DEFERRED.md`。LANC 协议封包、网络层、历史存储与旧客户端兼容零破坏；全量 20 个改动/新增文件 `< 1000` 行，新增注释均为中文。

## Round 1 问题逐项复审结论

| 问题 ID | 严重度 | 结论 | 复核依据 |
| --- | :---: | :---: | --- |
| `LC-CC-001` | P1 | **RESOLVED** | `src/UI/Renderers/ChatTextLayout.cs:150-200`：`PaintText` 改为 `for (int i = 0, end; i < text.Length; i = end)` 按 `NextBoundary(text, i)` 推进，结合 `IsEmojiElement(text, i, end)` 覆盖 `0x1F000..0x1FAFF`、`0x2600..0x27BF`、常用 BMP 计时/星标符号及 `\uFE0F`/`\u20E3`/`\u200D`，整段交给 `Segoe UI Emoji` 一次测量与绘制，普通文字保持连续排版；`tests/ChatComposerRegression.cs:76-118` 新增 `VerifyEmojiPaint` 对 `✋🏽`、`✌🏽`、`❤‍🔥`、`✋`、`✨`、`☕` 完成整段宽度、混排宽度与 240×60 逐像素对照并独立复跑通过 |
| `LC-CC-002` | P1 | **RESOLVED** | `src/linux/desktop/preload.cjs:38-52` 仅对 `image/png`、`image/jpeg` 内存图像走 `ArrayBuffer` 直传，非直传格式（WebP/BMP/GIF）有 `plain` 时走原生文本粘贴、无 `plain` 时交由主进程读系统剪贴板；`src/linux/desktop/main.cjs:229-272` 仅解码 `['image/png', 'image/jpeg']`，`decodeClipboardImage` 返回空图时继续尝试下一候选（内存空图亦回退 `clipboard.read()`），全部尝试失败才抛 `无法读取剪贴板图片`，仅有非兼容格式时静默返回空草稿且不建文件；`tests/linux_desktop_ipc_regression.js:9-165` 通过 `vm.runInNewContext` 实测 `preload.cjs` 与 `main.cjs` 全部路径并独立复跑通过 |
| `LC-CC-003` | P2 | **DEFERRED** | 已登记至 `CRDocs/DEFERRED.md`，随 `renderer.js` 拆分与 Linux 侧栏摘要专项处理 |
| `LC-CC-004` | P2 | **DEFERRED** | 已登记至 `CRDocs/DEFERRED.md`，归入双端附件生命周期专项处理异步失败清理与退出竞态 |
| `LC-CC-005` | P2 | **DEFERRED** | 已登记至 `CRDocs/DEFERRED.md`，归入 Windows 输入区交互整改与真实剪贴板验收 |
| `LC-CC-006` | P2 | **DEFERRED** | Node IPC 测试已完成职责拆分（`linux_desktop_ui_regression.js` 降至 869 行）；剩余 `renderer.js` 拆分与 Windows 选区/草稿补齐用例已登记至 `CRDocs/DEFERRED.md` |
| `LC-CC-ENV-001` | — | **DEFERRED** | 保留在 `CRDocs/DEFERRED.md`，待真实双机 LAN、Windows 真实截图工具与 Linux ELF / Electron 实机验收 |

---

# LC-CHAT-COMPOSER-001 — Round 1 独立审核

- **任务编号**：`LC-CHAT-COMPOSER-001`
- **任务主题**：微信表情导入与无资源回退、Unicode 组合表情排版与划选复制、在线/离线分组 8px 间距与圆点、截图粘贴草稿与发送
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-24
- **审核状态**：**REJECTED**（按 `CRDocs/RULES.md` 打回整改；对应 `CRDocs/README.md` 的 `CHANGES_REQUESTED`）
- **依据**：`.claude/Rules.md`、`CRDocs/RULES.md`、`CRDocs/README.md`、`CRDocs/STATUS.md`、`CRDocs/IMPLEMENTATION.md`、`CRDocs/DEFERRED.md`、`Docs/GrokBot_UI_Design_System.md`，以及工作区全部 18 个已修改文件与 5 个未跟踪新文件（`Tools/Scripts/import-wechat-emoji.py`、`src/UI/Controls/ChatEmojiPicker.cs`、`src/UI/Renderers/ChatTextLayout.cs`、`tests/ChatComposerRegression.cs`、`tests/chat_composer_regression.ps1`）。

## 独立复跑与验证边界

### 1. 宿主机回归（本次审核在 Windows 宿主机独立复跑，均退出码 0）

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`：通过。原生 `csc.exe` 生产编译成功；`LC-FR-001` ~ `LC-FR-004` / `LC-FR-008` 及 `LC-CHAT-COMPOSER-001`（真实 WinForms 列表 32/40px 标题行高、单组筛选 32px、`#1FD67E`/`#FC4455` 6px 状态圆点、200×100 截图保存 PNG 及 24×12 缩略图与移除清理）通过。
2. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack`：通过（已确认本机 `%LOCALAPPDATA%\LanChat\WeChatEmoji` 存在 109 行 `manifest.tsv` 与 109 个 `<= 64×64` PNG）。包含 `ChatTextLayout` 窄行折行与 CRLF 索引、`[敲打]` 64×64 加载与彩色绘制、PNG 透明度/Bitmap/FileDrop/文本区分、LANC 隔离回环文本与 PNG 字节一致及 JSONL 历史回读。
3. `"C:\Program Files\nodejs\node.exe"` 逐一 `--check` 检查 `src/linux/desktop/{main.cjs,preload.cjs,presentation.js,overlay.js,renderer.js}` 与 `tests/linux_desktop_ui_regression.js`，并运行 `tests/linux_desktop_ui_regression.js`：全部退出码 0，输出 `linux desktop ui regression: ok`（注：本机 `C:\WINDOWS\system32\node` 为无效占位，须使用 `C:\Program Files\nodejs\node.exe`）。
4. `python -m py_compile Tools/Scripts/import-wechat-emoji.py` 通过；`bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false` 在 Windows 宿主机通过（0 警告、0 错误）。
5. 审核者只读反射验证：对 `WeChatChatView.GetCharIndexAtPoint` 全行逐像素扫描确认 `"甲👨‍👩‍👧‍👦👍🏽🇨🇳❤️1️⃣\r\n[敲打]乙"` 命中 0 次可见元素内拆点；同时在编译产物上**实测复现**了 `LC-CC-001`（`✋🏽` 与 `❤‍🔥` 被 `ChatTextLayout.PaintText` 拆为两段字体绘制与测量）。
6. `git diff --check` 通过；全部 19 个改动/新增源码与测试文件编辑器行数均 `< 1000` 行软门禁（最高为 `tests/linux_desktop_ui_regression.js` 964 行与 `src/linux/desktop/renderer.js` 961 行）。

### 2. 真实双机验证（未验证）

- 尚未在两台物理/虚拟机之间执行：双方导入同包微信表情逐条收发、复制与历史重开；新版与未导入资源端或公开旧版（v2.0.19 及更早 Windows/Linux/Python 客户端）跨机互通验收 `[敲打]` 可读文本、Unicode 组合表情与截图 PNG 文件卡片打开；以及真实 Windows 桌面截图工具（Win+Shift+S / 微信截图 / 图像应用）的 `Ctrl+V`、`Shift+Insert`、右键粘贴链路。见 `CRDocs/DEFERRED.md` 的 `LC-CC-ENV-001`。

### 3. Linux 实机验收（未验证）

- 宿主机为 Windows，本轮仅完成 Node 桩 IPC/模拟 DOM 回归与 C# 后端 Windows 宿主机构建；**尚未验证** Linux/QEMU 最新 ELF 实跑（`tests/linux_desktop_regression.py`）、Linux Electron 45 真实 Xorg/Wayland/XWayland 系统剪贴板（`clipboard.read()`、截图工具、文件管理器、浏览器图片复制）、表情网格暗色滑块交互及实机分组视觉对齐。见 `CRDocs/DEFERRED.md` 的 `LC-CC-ENV-001`、`LC-LINUX-002-ENV-001`、`LC-LINUX-002-GUI-001`。

## 结论

**REJECTED（打回整改）**。无 P0。微信 4.2.0.13 本地导入脚本哈希校验与 Qt 资源解包逻辑清晰，无资源时双端均安全回退为 Unicode 面板与可读 `[名称]` 文本；LANC 封包魔数、文本帧、文件帧与 JSONL 历史格式零破坏，完全兼容旧客户端；双端在线/离线分组 8px 间距与 6px `#1FD67E`/`#FC4455` 状态圆点度量一致；Windows 截图预览、切会话隔离、移除删图、确认发送保图与退出清理闭环基本成立。

但存在 **2 项 P1 阻断放行问题**（另登记 **4 项 P2**）：

1. `LC-CC-001`：Windows `ChatTextLayout.PaintText` 未按可见元素步进，实测会将 BMP 基底组合表情（如 `✋🏽`、`✊🏽`、`✌🏽`、`❤‍🔥`）拆成 UI 字体 + Emoji 字体两段分别绘制与测量，且自带回退面板中的 `✋`/`✨`/`☕` 未路由至 Emoji 字体；
2. `LC-CC-002`：Linux `preload.cjs` 与 `main.cjs` 将 `image/webp`、`image/bmp`、`image/gif` 等非 PNG/JPEG 原始字节直接交给仅支持 PNG/JPEG 解码的 `nativeImage.createFromBuffer`，必然抛出 `无法读取剪贴板图片`，且 `preload.cjs` 抢占 `data.files` 中的非 PNG/JPEG 图像后会跳过系统剪贴板原本可用的 `image/png`。

## 六维核对摘要

| 维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. 微信表情导入与无资源可读回退 | PASS | `Tools/Scripts/import-wechat-emoji.py` 强校验微信 4.2.0.13 `Weixin.dll` SHA-256、Qt 目录结构与 PNG 头，原子写入 `manifest.tsv`；Windows `%LOCALAPPDATA%\LanChat\WeChatEmoji` 与 Linux `app.getPath('userData')/WeChatEmoji`（`package.json` `productName: "LanChat"`）路径一致；无包时双端回退 50 个 Unicode 表情且对端保留 `[名称]` 原文 |
| 2. Unicode 组合表情换行/划选/复制/历史回读 | FAIL | `BreakLines` 保留 CRLF 索引、`WeChatChatView.GetCharIndexAtPoint` 逐像素验证不拆元素、`CopySelectedText` 与 `ChatHistoryService` 完整保留原文；但存在 `LC-CC-001`（P1，`PaintText` 拆分 BMP 基底组合表情）与 `LC-CC-003`（P2，Linux 侧栏 `clip()` 按 UTF-16 截断） |
| 3. 在线/离线分组 8px 间距与 6px 圆点 | PASS | Windows `MeasureItem` 与 `ListSessions_DrawItem` 共用 `SessionHeadingGap`（双组同显离线 40px、单组 32px），`AccentGreen` (`#1FD67E`) / `TextDestructive` (`#FC4455`) 6px 圆点距标题右侧 6px 垂直居中；Linux `desktop.css:75-78` 以 `[data-online="true"] ~ [data-online="false"]` 加 `margin-top: 8px` 及同色同尺寸 `::after` 圆点对齐 |
| 4. 截图粘贴预览/切会话/移除/确认发送/清理 | FAIL | Windows 预览按 24px 等比缩放、按会话隔离、移除/删好友/退出清理未提交 PNG、确认发送保留本地副本且不误删用户文件；但 Linux 端存在 `LC-CC-002`（P1，非 PNG/JPEG 图像必败且屏蔽 `image/png` 回退）；另有 `LC-CC-004`（P2，异步失败传图残留与退出竞态）和 `LC-CC-005`（P2，Windows 文本+位图优先级与首次右键） |
| 5. LANC 协议与旧客户端兼容性 | PASS | `src/Models.cs`、`src/NetworkService*.cs`、`src/Services/ChatHistoryService.cs` 与 `src/linux/LanChat.Linux/` 零改动；微信表情按普通 UTF-8 文本 `[名称]` 传输，截图复用既有 PNG 文件流，旧客户端零兼容破坏 |
| 6. 行数门禁与中文注释规范 | PASS | 全部 19 个改动/新增源码与测试文件 `< 1000` 行软门禁，新增类与公开/内部成员具备全中文注释与 XML 文档；`renderer.js`（961 行）与 `linux_desktop_ui_regression.js`（964 行）逼近 1000 行预警线（见 `LC-CC-006`） |

## P0（阻断级）

无。

## P1（本轮必须修复）

### LC-CC-001 [P1 / OPEN] Windows `ChatTextLayout.PaintText` 按 UTF-16 单码元扫描，拆分 BMP 基底组合表情并遗漏自带回退 BMP 表情

- **位置**：`src/UI/Renderers/ChatTextLayout.cs:150-182`、`src/UI/Controls/ChatEmojiPicker.cs:22-23`
- **复现步骤与实测证据**：
  1. 构造含 BMP 基底肤色或无 `\uFE0F` ZWJ 组合表情的消息：`"✋🏽"`（`U+270B + U+1F3FD`，UTF-16 长度 3）或 `"❤‍🔥"`（`U+2764 + U+200D + U+1F525`，UTF-16 长度 4）；
  2. `ChatTextLayout.NextBoundary(text, 0)` 正确返回 `3` 与 `4`，但在 `PaintText` 中循环为 `for (int i = 0; i < text.Length; i++)`，第 153-154 行仅检查 `(code >= 0x1F000 && code <= 0x1FAFF) || (i + 1 < text.Length && (text[i + 1] == '\uFE0F' || text[i + 1] == '\u20E3'))`；
  3. `i = 0` 时 `0x270B` / `0x2764` 不命中，循环推进至 `i = 1` 命中肤色修饰符 `0x1F3FD` 或后段 `0x1F525`，导致 `PaintText` 先用主 UI 字体绘制 `"✋"` / `"❤"`，再用 `Segoe UI Emoji` 单独绘制 `"🏽"` / `"\u200D🔥"`；
  4. 在本轮编译产物 `ChatComposerRegression.exe` 上反射调用 `ChatTextLayout.Measure` 实测结果：
     - `✋🏽`：`NextBoundary(0) == 3`，`ChatTextLayout.Measure == 33.781` == `Measure(YaHei, "✋") + Measure(SegoeEmoji, "🏽") == 33.781`（整段 `Segoe UI Emoji` 应为 `35.650`）；
     - `❤‍🔥`：`NextBoundary(0) == 4`，`ChatTextLayout.Measure == 32.155` == `Measure(YaHei, "❤") + Measure(SegoeEmoji, "\u200D🔥") == 32.155`（整段 `Segoe UI Emoji` 应为 `36.158`）；
  5. 此外，`ChatEmojiPicker.UnicodeEntries` 自带的无资源回退表情 `✋`（`U+270B`）、`✨`（`U+2728`）、`☕`（`U+2615`）均不在 `0x1F000..0x1FAFF` 且无 `\uFE0F`，同样不会切入 `Segoe UI Emoji` 绘制。
- **影响**：标准 Unicode 肤色组合手势（`✋🏽`、`✊🏽`、`✌🏽`、`☝🏽`、`✍🏽`）及无 `\uFE0F` 的 ZWJ 表情在气泡与侧栏预览中被拆成两段不同字体，出现手势与肤色色块分离且测量宽度失真。
- **修复要求**：
  1. `PaintText` 改为按 `int end = NextBoundary(text, i)` 逐可见元素步进（避免拆进元素内部或落到低代理码元）；
  2. 对整个元素 `[i, end)` 判定本地微信表情或 Unicode 表情（覆盖 `0x1F000..0x1FAFF`、BMP 常用 Emoji 区段如 `0x2600..0x27BF`/`0x2300..0x23FF`/`0x2B50`，或元素内含 `\uFE0F`/`\u20E3`/`\u200D`/肤色修饰符 `0x1F3FB..0x1F3FF`），将整个元素一次性交给 `Segoe UI Emoji` 测量与绘制；
  3. 在 `tests/ChatComposerRegression.cs` 补充 `✋🏽`、`❤‍🔥`、`✋`/`✨`/`☕` 的整段测量与绘制回归断言。

### LC-CC-002 [P1 / OPEN] Linux 剪贴板图像将 WebP/BMP/GIF 直接交由 `nativeImage.createFromBuffer` 导致必然解码失败，且抢占系统剪贴板的 `image/png`

- **位置**：`src/linux/desktop/preload.cjs:37, 45-49`、`src/linux/desktop/main.cjs:230-265`
- **复现链路与依据**：
  1. Electron 官方文档明确 `nativeImage.createFromBuffer(buffer)` 仅原生解码 **PNG** 与 **JPEG**，不支持 WebP、BMP 或 GIF（传入这些编码的原始字节时 `nativeImage.createFromBuffer` 返回空图像，`image.isEmpty() === true`）；
  2. `main.cjs:249` 的 `readClipboardImage` 将 `['image/png', 'image/jpeg', 'image/webp', 'image/bmp']` 直接交由 `stageClipboardImage` -> `nativeImage.createFromBuffer(Buffer.from(bytes))`。当系统剪贴板仅含 `image/bmp` 或 `image/webp` 时必然抛出 `无法读取剪贴板图片`；
  3. 更严重的是 `preload.cjs:37` 使用 `file.type.startsWith('image/')` 匹配 `event.clipboardData.files` 中的首个内存图像，并在第 48 行直接传递 `await image.arrayBuffer()` 给 `lanchat:stage-paste`，致使 `main.cjs:230` 跳过 `clipboard.read()`。当用户在 Linux 浏览器或桌面应用中复制 WebP/GIF 图片时，`clipboardData.files` 往往暴露原始 `image/webp` / `image/gif`，即便系统剪贴板同时提供了转换后的 `image/png`，该路径也会直接把 WebP/GIF 原始字节送进 `nativeImage.createFromBuffer` 导致粘贴报错；
  4. `tests/linux_desktop_ui_regression.js` 中 `nativeImage.createFromBuffer` 为固定返回非空对象的桩实现，未暴露此格式不匹配问题。
- **修复要求**：
  1. `preload.cjs` 仅对 `image/png` 与 `image/jpeg` 直接走内存 `ArrayBuffer` 快路径；遇到其他 `image/*`（如 `image/webp`、`image/gif`、`image/bmp`）时，优先在渲染进程通过 `createImageBitmap` / Canvas 转码为 PNG 字节，或回退交由主进程读取 `clipboard.read()` 中可用的 `image/png` / `image/jpeg`（并注意避免 `plain` 非空时误拦截纯文本）；
  2. `main.cjs` 的 `readClipboardImage` 与 `stageClipboardImage` 对齐 `nativeImage.createFromBuffer` 实际支持的 PNG/JPEG 格式（若保留多 MIME 遍历，应在 `createFromBuffer` 返回空图时继续尝试下一可用 MIME，而非首次命中非支持格式即整体抛错）；
  3. 在 `tests/linux_desktop_ui_regression.js` 补充非 PNG/JPEG 剪贴板图像回退与空图跳过断言。

## P2（可延期或返修顺手处理）

### LC-CC-003 [P2 / OPEN] Linux 侧栏摘要 `clip()` 按 UTF-16 码元截断且未复用表情渲染

- **位置**：`src/linux/desktop/presentation.js:55-58, 91`、`src/linux/desktop/renderer.js:138-139`
- **说明**：Windows 侧栏摘要已改用 `ChatTextLayout.DrawSnippet` 按可见元素截断并显示表情图；Linux 侧栏仍通过 `value.slice(0, limit - 1)` 按 UTF-16 码元硬截断，遇到多码元组合表情（如 `👨‍👩‍👧‍👦`、`👍🏽`、`🇨🇳`）或 `[敲打]` 时可能截断代理对产生孤立替代符或半截令牌（如 `[敲...`）。建议改为按可见字符/表情令牌边界截断。

### LC-CC-004 [P2 / OPEN] 双端异步发送失败的截图 PNG 不会自动清理，且 Linux `send-paste` 与 `before-quit` 存在并发退出残留窗口

- **位置**：`src/MainForm.Chat.cs:419`、`src/Controllers/FileTransferController.cs:127-136`、`src/linux/desktop/main.cjs:399, 520-526`
- **说明**：
  1. 双端在点击确认发送启动异步传输时即解除草稿托管，若后续 TCP 发送失败（如对端拒收或断连），失败消息不写入历史，而 `PastedImages/截图-*.png` 仍留在用户目录；
  2. Linux `send-paste` 在 `await dispatchFiles` 前先从 `pendingPastes` 删除 `id`，若期间触发 `before-quit` 执行 `discardPastedFiles([...pendingPastes.keys()])`，随后 `dispatchFiles` 抛错在 `catch` 中又将 `id` 写回 `pendingPastes`，导致退出时漏删未提交截图。建议在 `catch` 中判断 `if (exiting)` 立即 `unlink` 临时图片。

### LC-CC-005 [P2 / OPEN] Windows 剪贴板同时含文本与位图时的优先级与 Linux 不一致，且空输入栏首次右键未弹出粘贴菜单

- **位置**：`src/UI/Controls/ChatInputBox.cs:50-79`、`src/MainForm.Designer.cs:471-475`
- **说明**：
  1. Linux `preload.cjs:46` 在系统剪贴板路径传入 `allowImage = !plain`，避免同时含文本与位图的富文本/表格复制被误当图片；Windows `ChatInputBox` 未检查 `DataFormats.UnicodeText`，从 Excel/WPS 复制单元格时会按截图粘贴，且剪贴板残留截图时右键输入框会以单一“粘贴附件”菜单替代原生复制/剪切菜单；
  2. Windows `_txtInput` 空态隐藏时首次右键 `_inputPanel` 仅触发 `MouseDown` 显示输入框，需二次右键才弹出暗色粘贴菜单。

### LC-CC-006 [P2 / OPEN] `renderer.js` 与 `linux_desktop_ui_regression.js` 逼近 1000 行软门禁，且 Windows 回归可补齐选区与会话切换用例

- **位置**：`src/linux/desktop/renderer.js`（961 行）、`tests/linux_desktop_ui_regression.js`（964 行）、`tests/ChatComposerRegression.cs`、`tests/FriendRefreshRegression.cs`
- **说明**：两份 JS 文件距 `.claude/Rules.md` 第 10 条 1000 行预警线分别仅余 39 行与 36 行，后续继续加功能/测试前须先按职责拆分；另建议在返修回归中补充 Windows `WeChatChatView` 像素级划选不拆表情、跨 CRLF 复制及截图草稿切换会话保留/退出清理的自动化断言。

## 跨环境验收项（保留在 `CRDocs/DEFERRED.md`）

| 问题 ID | 状态 | 说明 |
| --- | :---: | --- |
| `LC-CC-ENV-001` | OPEN | 真实 Windows 桌面截图粘贴键盘链路、真实双机新旧客户端联测、Linux Xorg/Wayland Electron 剪贴板与最新 ELF 实跑 |

---

# LC-FRIEND-REFRESH-001 — Round 2 独立复审

- **任务编号**：`LC-FRIEND-REFRESH-001`
- **任务主题**：好友刷新延迟、网卡切换与在线/离线分组（Round 2 返修复审）
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-24
- **审核状态**：**APPROVED**
- **依据**：`.claude/Rules.md`、`CRDocs/README.md`、`CRDocs/RULES.md`、`CRDocs/STATUS.md`、`CRDocs/REVIEW.md`（Round 1）、`CRDocs/DEFERRED.md`、`CRDocs/IMPLEMENTATION.md`（Round 2）、工作区完整 diff 与实际调用链。
- **独立复跑与验证边界**：
  - 审核者独立复跑并确认通过：
    1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1`（退出码 0；原生 `csc.exe` 生产编译通过；隔离回环与 Win32 消息夹具 `LC-FR-001`、`LC-FR-002`、`LC-FR-003 / LC-FR-008`、`LC-FR-004` 四组检查全部通过）；
    2. 对本轮生成的 `LanChat.exe` 编译并运行 `tests/ConcurrentReceive.cs`（退出码 0，`Final ACK: 1, 1; Complete events: 2`，双同名文件 SHA-256 校验通过）；
    3. `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`（退出码 0，0 警告 0 错误）；
    4. `C:\Program Files\nodejs\node.exe` 执行 `--check src/linux/desktop/renderer.js`、`--check tests/linux_desktop_ui_regression.js` 及 `tests/linux_desktop_ui_regression.js`（均退出码 0，输出 `linux desktop ui regression: ok`）；
    5. `git diff --check` 通过，全量 17 个改动/新增源码与测试文件编辑器行数复核通过。
  - **未验证边界（不构成实机验收）**：本轮所有验证均在 Windows 宿主机上完成（生产编译、回环隔离夹具、Win32 原生消息测试、Linux 后端宿主机构建、Node 模拟 DOM 与桩 IPC）。**尚未验证**真实多机 LAN、Wi-Fi/有线实际切网、DHCP 延迟、休眠唤醒、物理多网卡/TUN 路由及公开旧版（v2.0.18 / Python）跨机互通（`LC-FR-ENV-001`），**尚未验证** Linux/QEMU 网络命名空间实跑最新 ELF（`tests/linux_desktop_regression.py`）与真实 Linux Electron 桌面侧栏交互（`LC-FR-ENV-002`）。

## 结论

**APPROVED（当前范围放行）**。无 P0、无 P1 遗留。Round 1 阻断放行的全部 4 项 P1（`LC-FR-001` ~ `LC-FR-004`）均已按要求落地且经源码调用链与独立复跑回归确认闭环；4 项 P2（`LC-FR-005`、`LC-FR-006`、`LC-FR-008`、`LC-FR-009`）已同步闭环；`LC-FR-007` 已按规则明确写入 `CRDocs/DEFERRED.md` 并具备清晰触发阶段与验收条件。LANC 封包魔数、文本帧字段、`[系统自动握手]`、TCP `0x01` 确认字节与 UDP 六段竖线格式保持向后兼容；全部工作区改动文件均低于 1000 行软门禁，新增注释符合全中文规范。

## 七维复核摘要

| 维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. UDP ACK 与 TCP 握手并发归并/昵称/在线态 | PASS | `LC-FR-001` 与 `LC-FR-005` 闭环：`UpdatePeer` 在 `_peerLock` 内按 `(IPv4, TcpPort)` 归并同端点旧身份、累加未读并在锁外派发 `OnPeerLeft`；`TryTcpHandshake` 持 `_discoveryLock -> _peerLock` 校验快照、对象引用与端点独占性，拒绝为已移除或冲突端点续期；匿名 `expected == null` 探测不刷新已知真实身份；Linux `setPeers` 保留运行期与归档昵称 |
| 2. 刷新计时器/重复刷新/切网重绑/线程竞态与泄漏 | PASS | `LC-FR-002` 与 `LC-FR-006` 闭环：启动、改昵称、切网回调与手动刷新统一经 `RefreshPeers()` 先执行 `RebindInterfaces()`，可重试空 `_udpAnySocket` / 失败监听并复用健康快照；锁序严格为 `_discoveryLock -> _peerLock` 无死锁；`Dispose()` 已隔离反订阅异常；`SweepInterfaces` 睡眠后已先短路；`AsyncWaitHandle` 已用 `using` 释放 |
| 3. 非默认 TCP 端口与多网卡/TUN 路由刷新 | PARTIAL (DEFERRED) | 在线非默认端口复核、同 IP 异端口保留与同子网物理网卡优先成立；跨子网 TUN 回落通配与 Linux 运行期无历史离线地址属 P2，已准确登记至 `CRDocs/DEFERRED.md`（`LC-FR-007`），随 `LC-FR-ENV-001` / `LC-FR-ENV-002` 一并验收 |
| 4. WinForms 分组标题鼠标/键盘/右键/滚动/选择 | PASS | `LC-FR-003`、`LC-FR-004`、`LC-FR-008` 闭环：`ClearSessionItems()` 同步清空旧索引，`OnSelectedIndexChanged` 校验旧索引并双向查找非标题会话（全标题落 `-1`）；`WndProc` 低 32 位解包有符号坐标并在溢出时将右缘 12px 滑块热区直交 `OnMouseDown`/`OnMouseUp`（保留当前选择、拦截标题正文与无溢出热区、兼容 x64 负 Y）；`RenderSessionList` 校验 `TopIndex` 边界并在恢复选中后夹紧恢复视口锚点 |
| 5. Linux Electron 刷新入口位置与分组渲染 | PASS | `LC-FR-009` 闭环：`#refresh` 位于 `.sidebar-bottom` 最右（36px），`.session-heading` 显式声明 `font-weight: 700`，`onlineHeading` 状态完整；`tests/linux_desktop_ui_regression.js` 已覆盖入口位置、字重、空/非空分组渲染、过滤、键盘越过标题与刷新占位昵称保护 |
| 6. LANC 帧、TCP 握手与旧客户端兼容 | PASS | LANC 魔数、文本帧结构、`[系统自动握手]`、TCP `0x01` 与 UDP 六段格式未破坏；对端 TCP 握手后补发既有 UDP `ACK`；旧客户端仅回 `0x01` 时单一未冲突端点仍可正常复核，且已明确记录单字节确认在无新身份公告/冲突证据时无法区分同端点换 ID 的协议边界 |
| 7. 行数门禁与中文注释/文档要求 | PASS | 全部 17 个修改/新增源码与测试文件均 `< 1000` 行软门禁；最高为 `src/linux/desktop/renderer.js`（953 行，距 1000 行预警仅余 47 行，后续新增功能前须先按 SRP 拆分）与 `tests/linux_desktop_ui_regression.js`（935 行）；新增与调整注释均为中文 |

## Round 1 问题逐项复审结论

| 问题 ID | 严重度 | 结论 | 复核依据 |
| --- | :---: | :---: | --- |
| `LC-FR-001` | P1 | **RESOLVED** | `src/NetworkService.Discovery.cs:193-275, 505-529`：带 `announcedPort` 的身份公告按 `(IP, TcpPort)` 归并同端点旧节点并迁移未读，锁外派发 `OnPeerLeft`；同 IP 异端口保留；迟到 TCP `0x01` 复核快照、`current == expected` 与端点无其他身份冲突后才续期；`tests/friend_refresh_regression.ps1` 独立复跑通过 |
| `LC-FR-002` | P1 | **RESOLVED** | `src/NetworkService.Discovery.cs:293-319`、`src/NetworkService.Interface.cs:76-113`、`src/NetworkService.cs:168-190`：所有刷新入口统一走 `RefreshPeers()` -> `RebindInterfaces()`，覆盖漏事件、延迟获取与瞬时绑定失败，不变网卡零重建复用快照；夹具独立复跑通过 |
| `LC-FR-003` | P1 | **RESOLVED** | `src/UI/Controls/SessionListBox.cs:39-44, 327-361`、`src/MainForm.Chat.cs:24`：重建清空 `_previousSelectedIndex`，跨标题前校验旧索引范围与非标题性，主方向无有效行时反向查找，仅标题时落 `-1`；缩表 `Home`/`Down`、末尾标题 `End` 与纯标题场景夹具独立复跑通过 |
| `LC-FR-004` | P1 | **RESOLVED** | `src/UI/Controls/SessionListBox.cs:87-108, 226-233, 255-311`：`unchecked((int)m.LParam.ToInt64())` 解析有符号 16 位 `x`/`y`，溢出时右缘 12px 滑块热区直交 `OnMouseDown`/`OnMouseUp` 不篡改当前选择，无溢出与正文区标题点击继续拦截；原生 Win32 消息与 x64 负 Y 坐标夹具独立复跑通过 |
| `LC-FR-005` | P2 | **RESOLVED** | `src/linux/desktop/renderer.js:57-67`：`peer.id === peer.ip && previous?.nick` 时保留运行期及归档昵称，不回退为 `"好友 (<ip>)"`；Node UI 回归独立复跑通过 |
| `LC-FR-006` | P2 | **RESOLVED** | `src/NetworkService.cs:233-239`（反订阅 `try/catch` 保护）、`src/NetworkService.Discovery.cs:426-427`（睡眠后先检停机与快照切换）、`484-488`（`using (var connectWait = ar.AsyncWaitHandle)`） |
| `LC-FR-007` | P2 | **DEFERRED** | 已在 `CRDocs/DEFERRED.md` 准确登记跨子网 TUN 回落与 Linux/切网无历史离线地址现状、触发阶段（`LC-FR-ENV-001` / `LC-FR-ENV-002`）及验收条件，不阻断当前切片 |
| `LC-FR-008` | P2 | **RESOLVED** | `src/MainForm.Chat.cs:18-40` 与 `src/UI/Controls/SessionListBox.cs:46-53`：校验 `TopIndex` 边界，在恢复 `SelectedIndex` 后通过 `RestoreScrollAnchor` 夹紧恢复视口锚点；选中群聊向下浏览后重建夹具复跑通过 |
| `LC-FR-009` | P2 | **RESOLVED** | `src/linux/desktop/desktop.css:75` 显式声明 `font-weight: 700`，`src/linux/desktop/renderer.js:98` 同步置 `onlineHeading = true`，`tests/linux_desktop_ui_regression.js` 已补齐入口位置、字重、分组、过滤、键盘跳标题与刷新昵称保留断言 |
| `LC-FR-ENV-001` | — | **DEFERRED** | 保留在 `CRDocs/DEFERRED.md`，待真实多机 LAN、切网与多网卡/TUN 路由验收 |
| `LC-FR-ENV-002` | — | **DEFERRED** | 保留在 `CRDocs/DEFERRED.md`，待 Linux/QEMU 网络命名空间实跑最新 ELF 与真实 Linux Electron 桌面验收 |

---

# LC-FRIEND-REFRESH-001 — Round 1 独立审核

- **任务编号**：`LC-FRIEND-REFRESH-001`
- **任务主题**：好友刷新延迟、网卡切换与在线/离线分组
- **审核方**：Gemini 独立审核方
- **审核日期**：2026-11-24
- **审核状态**：**CHANGES_REQUESTED**
- **依据**：`.claude/Rules.md`、`CRDocs/README.md`、`CRDocs/RULES.md`、`Docs/GrokBot_UI_Design_System.md`、`CRDocs/IMPLEMENTATION.md` 顶部范围与工作区全部 14 个源码文件及完整 diff。
- **验证边界**：
  - 审核者独立复跑并确认通过：Windows 原生 `csc.exe` 零错编译（临时输出至 `%TEMP%` 后清理，未改动 `release/`）、Linux 共享后端宿主机构建 `bin/lc-linux-002/cache/dotnet/dotnet.exe build src/linux/LanChat.Linux/LanChat.Linux.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=false -p:PublishTrimmed=false`（0 警告 0 错误）、`node --check src/linux/desktop/renderer.js` 与 `node tests/linux_desktop_ui_regression.js`（退出码 0，输出 `linux desktop ui regression: ok`）、全部 14 个修改文件行数统计与 `git diff --check`；并通过只读反射脚本对当前编译产物 `bin/friend-refresh/LanChat.exe` 中的 `SessionListBox` 实测复现了 `LC-FR-003` 与 `LC-FR-004`。
  - **未在真实环境验收**：未在真实多机 LAN、Wi-Fi/有线切网、物理多网卡/TUN 路由环境验收，未在 Linux/QEMU 运行最新 ELF（`tests/linux_desktop_regression.py`），亦未在 Linux Electron 桌面实机验收。宿主机编译、Windows 回环夹具与 Node DOM 模拟均不构成真实 LAN 或 Linux 实机验收。

## 结论

**CHANGES_REQUESTED（打回整改）**。LANC 封包与 TCP `0x01` 确认字节保持向后兼容，UDP 多网卡快照与锁序（`_discoveryLock -> _peerLock`）无死锁，修改文件行数全部低于 1000 行软门禁，新增注释与文档满足中文规范。但存在 **4 项 P1 阻断放行问题**：

1. 对端无 `BYE` 重启（同 IP 同端口新 `LocalId`）后刷新时，TCP 握手 `0x01` 误为已失效旧节点续命，导致幽灵节点滞留、人数翻倍、群聊重复发送且昵称/状态回退；
2. 手动刷新 `RefreshPeers()` 未触发 `RebindInterfaces()`，在换网延迟/DHCP 慢于防抖窗口、瞬时绑定失败或 `NetworkChange` 异常/漏事件后，点击刷新按钮无法恢复网卡绑定；
3. `SessionListBox.OnSelectedIndexChanged` 使用未随列表重置同步的 `_previousSelectedIndex`，搜索筛选缩小列表后按键盘方向键会直接抛出未处理 `ArgumentOutOfRangeException` 崩溃，或卡死在分组标题选中态（已实测复现）；
4. `SessionListBox.WndProc` 拦截 `WM_LBUTTONDOWN` 标题行点击时未排除右侧 12px 浮动滑块拖拽热区，导致在「在线」「离线」标题高度上无法拖拽自绘滚动条（违反 Zero White Scrollbar 交互契约，已实测复现）。

## 七维核对摘要

| 维度 | 结论 | 说明 |
| --- | :---: | --- |
| 1. UDP ACK 与 TCP 握手并发归并/昵称/在线态 | FAIL | 存在 `LC-FR-001`（P1，重启对端幽灵节点误续命）；另有 `LC-FR-005`（P2，Linux 端未阻止 TCP 地址占位名覆盖已知/归档昵称） |
| 2. 刷新计时器/重复刷新/切网重绑/线程竞态与泄漏 | FAIL | 锁序一致、无死锁、旧快照探针有隔离，但存在 `LC-FR-002`（P1，手动刷新不重绑网卡）；另有 `LC-FR-006`（P2，`Dispose` 反订阅未保护、`SweepInterfaces` 睡眠后未先短路、`AsyncWaitHandle` 未及时释放） |
| 3. 非默认 TCP 端口与多网卡/TUN 路由刷新 | PARTIAL | 在线非默认端口复核与同子网物理网卡优先成立；跨子网 + TUN 时 `GetDiscoverySocket` 回落 `0.0.0.0` 且 Linux/换网未保留运行期无历史离线地址（见 `LC-FR-007`，P2） |
| 4. WinForms 分组标题鼠标/键盘/右键/滚动/选择 | FAIL | 右键过滤与 `GetScrollMetrics` 动态行高计算正确，但存在 `LC-FR-003`（P1，搜索缩表后键盘选行崩溃/卡死标题）与 `LC-FR-004`（P1，标题高度阻断滑块拖拽）；另有 `LC-FR-008`（P2，`top <= index` 导致选中群聊向下浏览时刷新跳顶） |
| 5. Linux Electron 刷新入口位置与分组渲染 | PARTIAL | `#refresh` 移至底栏最右（36px 圆形）与 `.session-heading`（32px）符合结构与设计规范；但缺少针对新入口/分组标题的 UI 回归断言，且 `.session-heading` 字重隐式依赖弹窗 `h2` 规则（见 `LC-FR-009`，P2） |
| 6. LANC 帧、TCP 握手与旧客户端兼容 | PASS | LANC 魔数、文本帧字段、`[系统自动握手]`、`0x01` 确认字节与 UDP 6 段竖线格式完全兼容；TCP 握手后补发既有 UDP `ACK` 对旧客户端无害且可向上兼容（旧客户端仅 TCP 回包时 Linux 占位昵称覆盖见 `LC-FR-005`） |
| 7. 行数门禁与中文注释/文档要求 | PASS | 14 个修改文件全部 `< 1000` 行（最高 `src/linux/desktop/renderer.js` 945 行，距软门禁余 54 行需留意）；新增注释与 XML 文档均为中文（仅 `NetworkService.Discovery.cs` 首行带入 UTF-8 BOM，非阻断） |

## P1（本轮必须修复）

### LC-FR-001 [P1 / OPEN] 对端无 BYE 重启（同 IP 同端口新 LocalId）时，刷新 TCP 握手误为旧幽灵节点续命

- **位置**：`src/NetworkService.Discovery.cs:221-259` (`UpdatePeer`)、`350-375` (`CheckRefreshedPeers`)、`495-512` (`TryTcpHandshake`)。
- **复现条件与现象**：
  1. 对端节点 `old-guid`（如 `10.0.0.5:38889`，昵称 `OldNick`）处于在线列表；
  2. 对端崩溃或无 `BYE` 重启，以相同 IP 和默认 TCP 端口 `38889` 启动新实例 `new-guid`（昵称 `NewNick`）；
  3. 本机点击手动刷新：首轮 UDP `HELLO` 收到 `new-guid` 的 UDP `ACK`，`UpdatePeer` 只移除 `addressId`（`"10.0.0.5"`）而不清理同 `(IP, TcpPort)` 的 `old-guid`，此时 `_peers` 同时存在 `old-guid`（旧 `LastSeen`）和 `new-guid`（新 `LastSeen`）；
  4. 300ms 后 `CheckRefreshedPeers` 筛选 `LastSeen < started` 命中 `old-guid`，随即对其执行 `TryTcpHandshake(10.0.0.5, 38889, interfaces, expected: old-guid)`；
  5. 新实例 `new-guid` 在 `38889` 接受连接并返回 `0x01`（TCP 确认帧不含对端 ID），`TryTcpHandshake` 第 507-509 行校验 `expected.Id`/`IP`/`TcpPort` 通过后直接写入 `old-guid.LastSeen = DateTime.UtcNow`；
  6. `RemoveExpiredPeers(peers, started)` 见 `old-guid.LastSeen >= started` 放弃移除。
- **影响**：已死亡的 `old-guid` 被错误续命，每次刷新都会重复续命；`GetActivePeers().Count` 虚增为 2（群聊人数翻倍、群聊消息向同一好友重复发送两次）；只要 `old-guid.LastSeen` 晚于 `new-guid` 的 UDP `ACK` 时间戳（或 UDP 晚到/丢包），WinForms（`SessionPresenter.cs:158`）和 Linux（`DesktopService.cs:315` + `renderer.js:59`）会优先展示已失效的 `old-guid` 及其旧昵称 `OldNick`。
- **修复要求**：同一 IPv4 + 同一 TCP 监听端口不能同时存在两个活跃 LanChat 实例。在 `UpdatePeer` 收到带 `announcedPort.HasValue` 的真实身份（`peerId != addressId`）时，应归并/剔除同 `(IP, TcpPort)` 的其他旧 `peerId`（迁移未读并在锁外通知 `OnPeerLeft`）；同时 `TryTcpHandshake(..., expected)` 在同 `(IP, port)` 已被其他活跃 `peerId` 占用时不得为 `expected` 续命。

### LC-FR-002 [P1 / OPEN] 手动刷新 `RefreshPeers()` 未触发网卡重绑 `RebindInterfaces()`

- **位置**：`src/NetworkService.Discovery.cs:287-311` (`RefreshPeers`)、`src/NetworkService.Interface.cs:67-116` (`OnNetworkAddressChanged` / `RebindInterfaces`)、`src/NetworkService.cs:177-185`。
- **复现条件与现象**：
  - `RebindInterfaces()` 仅在 `Start()` 和 `NetworkChange.NetworkAddressChanged` 的 250ms 防抖回调中被调用，`RefreshPeers()` 直接使用缓存的 `_activeInterfaces`。
  - 当真实切网/休眠唤醒时 DHCP 获取 IPv4 晚于 250ms 防抖点、切网瞬间 `BindUdpListener` 瞬时失败导致 `adapter.UdpSocket == null`、用户在 250ms 防抖窗口内提前点击手动刷新，或 `NetworkChange` 订阅在特定环境下抛异常降级时，`_activeInterfaces` 会长期停留在失效快照上，用户反复点击“刷新好友列表”按钮也无法重新绑定新网卡套接字。
- **修复要求**：在 `RefreshPeers()` 获取 `interfaces` 前调用 `RebindInterfaces()`（该函数在网卡无变化时通过 `if (!changed) return;` 零重建直接返回，且切网通知回调可直接复用，确保手动刷新始终能兜底恢复新网卡与失败的 UDP 绑定）。

### LC-FR-003 [P1 / OPEN] `SessionListBox.OnSelectedIndexChanged` 残留越界 `_previousSelectedIndex`，搜索缩表后键盘选行直接崩溃或卡死在分组标题

- **位置**：`src/UI/Controls/SessionListBox.cs:289-299`、`src/MainForm.Chat.cs:18-39`。
- **实测复现证据**（已在 `bin/friend-refresh/LanChat.exe` 的 `LanChat.SessionListBox` 上复现）：
  1. **崩溃路径**：列表含 5 项 `[群聊(0), 在线(1), PeerA(2), 离线(3), PeerB(4)]`，选中 `PeerB`（`_previousSelectedIndex = 4`）。用户输入搜索词仅命中 `PeerA`，`RenderSessionList()` 调用 `Items.Clear()` 并重建为 2 项 `[在线(0), PeerA(1)]`；因当前会话 `PeerB` 不在过滤结果中，`index = -1`，`SelectedIndex` 为 `-1`，而 `Items.Clear()` 不会触发 `OnSelectedIndexChanged`，`_previousSelectedIndex` 仍为 `4`。焦点进入列表后按 `Down`/`Home` 落到索引 `0`（分组标题「在线」），`direction = (0 > 4) ? 1 : -1` 得 `-1`，`current` 减至 `-1`，随后执行 `SelectedIndex = _previousSelectedIndex`（即 `4`），抛出未处理异常 **`ArgumentOutOfRangeException: InvalidArgument=Value of '4' is not valid for 'SelectedIndex'`**。
  2. **标题卡死路径**：原选中 `群聊`（`_previousSelectedIndex = 0`），搜索仅命中 `PeerB` 重建为 `[离线(0), PeerB(1)]` 后按 `Down`/`Home`，`direction = (0 > 0) ? 1 : -1` 得 `-1`，回退 `SelectedIndex = 0`，结果分组标题 `GroupHeading.Offline` 保持选中态（实测 `SelectedIndex=0 Item=HEAD:Offline`），无法跳至 `PeerB`。
  3. **尾部边界**：无离线好友时列表末尾为「离线」标题，按 `End`/`PageDown` 落到末尾标题后 `direction = +1` 越界，回跳旧项而非停在最后一个有效会话。
- **修复要求**：不得将未经合法性校验的 `_previousSelectedIndex` 直接写回 `SelectedIndex`。当 `IsHeading(current)` 时：先校验 `_previousSelectedIndex` 是否处于 `[0, Items.Count)` 且非标题；主方向（若 `_previousSelectedIndex` 无效则优先向下 `+1`）找不到有效会话时，反向扫描寻找最近非标题会话；仅当整个列表无任何非标题项时设为 `-1`。列表清空重建后也应同步重置/夹紧 `_previousSelectedIndex`。

### LC-FR-004 [P1 / OPEN] `SessionListBox.WndProc` 拦截标题行左键点击时吞掉右侧浮动滑块拖拽热区

- **位置**：`src/UI/Controls/SessionListBox.cs:71-76, 214-229`。
- **实测复现证据**（已在 `bin/friend-refresh/LanChat.exe` 上向溢出列表的滑块热区 `(x=295, y=80)`（宽 300、位于第 1 项标题行 `y=72..104` 高度）发送 `WM_LBUTTONDOWN`，实测 `_isThumbDragging == False`）：
  - `WndProc` 在 `base.WndProc`（触发 `OnMouseDown`）之前拦截 `WM_LBUTTONDOWN`，并仅依据 `IsHeading(IndexFromPoint(x, y))` 直接 `return`。由于 `IndexFromPoint` 覆盖控件全宽（含最右 12px 浮动滑块区域），鼠标按住滑块与「在线」「离线」标题重合的纵坐标区段时，点击被直接吞掉，无法启动滚动条拖拽，违反 `.claude/Rules.md` 第 9 条 Zero White Scrollbar 的拖拽交互契约。
  - 另第 74 行 `m.LParam.ToInt32()` 在 64 位进程遇到负 Y 坐标时会溢出，宜改为 `unchecked((int)m.LParam.ToInt64())`。
- **修复要求**：在 `WndProc` 的 `WM_LBUTTONDOWN` 标题行拦截条件中，若列表存在溢出（`maxTop > 0`）且点击 X 落在右侧滑块热区（`x >= ClientSize.Width - 12`），应放行给 `base.WndProc` -> `OnMouseDown` 处理滑块拖拽；仅对非滑块热区的标题行点击做拦截。

## P2（建议返修一并处理或登记延期）

### LC-FR-005 [P2 / OPEN] Linux 端未阻止 TCP 地址占位记录覆盖已知/归档好友昵称

- **位置**：`src/linux/LanChat.Linux/DesktopService.cs:315-320`、`src/linux/desktop/renderer.js:57-62`。
- **说明**：`IMPLEMENTATION.md` 声明“地址占位记录不覆盖已知昵称”，Windows 端已在 `SessionPresenter` 保护，但 Linux 端 `PeerList()` 原样输出占位名 `"好友 (<ip>)"`，`renderer.js` 的 `setPeers` 会直接覆盖前端已有的归档/运行期真实昵称。在 UDP 丢包或与旧客户端（TCP 握手不回 UDP `ACK`）互通时会表现为昵称被回退成 `"好友 (<ip>)"`。建议在 `DesktopService.PeerList()` 或 `renderer.js` 中对 `peer.id === peer.ip` 的占位记录保留已知/归档昵称。

### LC-FR-006 [P2 / OPEN] `Dispose()` 反订阅 `NetworkAddressChanged` 缺异常保护，且 `SweepInterfaces` / `TryTcpHandshake` 存在轻微资源清理瑕疵

- **位置**：`src/NetworkService.cs:177-185, 235-245`、`src/NetworkService.Discovery.cs:418-425, 474-476`。
- **说明**：
  1. `Start()` 中先赋值 `_networkChangeTimer = new Timer(...)` 再订阅 `NetworkChange.NetworkAddressChanged +=`；若订阅抛异常，`_networkChangeTimer` 非空，后续 `Dispose()` 未包裹 `try/catch` 的 `-=` 若也抛异常会中断 `_presenceCheckTimer`、`BYE` 与全部套接字释放。建议仅在订阅成功后保留状态或在 `Dispose()` 中做隔离保护。
  2. `SweepInterfaces` 在 `Thread.Sleep(300)` 后应先检查 `if (!_isRunning || interfaces != _activeInterfaces) return;`，避免停机/切网后仍启动最多 32 个空转线程。
  3. `TryTcpHandshake` 建议用 `using (ar.AsyncWaitHandle)` 及时释放等待句柄。

### LC-FR-007 [P2 / OPEN] 跨子网 + TUN 场景下 UDP 探测回落 `0.0.0.0`，且运行期无历史离线好友未被纳入刷新单播

- **位置**：`src/NetworkService.Discovery.cs:138-153, 298-300`、`src/linux/LanChat.Linux/DesktopService.cs:312-313`。
- **说明**：
  1. `GetDiscoverySocket` 在目标不在任一本地子网时直接回落 `_udpAnySocket`（`0.0.0.0`），与 TCP 侧 `GetLocalEndpointForTarget`（优先回落首选物理网卡以穿透 Metric 0 TUN）不一致，跨网段路由局域网开启全局 TUN 时 UDP 单播可能被 TUN 劫持。
  2. Linux `DesktopService.KnownAddresses()` 仅读取有聊天记录的归档会话键，不包含运行期见过但未发消息的离线好友（与 Windows `SessionPresenter.GetKnownAddresses()` 不对称）；且 `NetworkService` 在切网 `RefreshPeers(null)` 时也不包含上次显式刷新之后才发现的新好友 IP。

### LC-FR-008 [P2 / OPEN] `RenderSessionList()` 的 `top <= index` 限制导致选中群聊向下浏览离线好友时刷新跳回顶部

- **位置**：`src/MainForm.Chat.cs:18, 37`。
- **说明**：选中顶部会话（如索引 `0` 的群聊）并向下滚动浏览离线好友时，若点击刷新或收到后台 `OnPeerUpdated`，重设 `SelectedIndex = 0` 会将列表滚回顶部，而第 37 行 `top <= index` 阻止恢复原 `TopIndex`，造成视口跳回顶部。另第 18 行读取 `_listSessions.Items[_listSessions.TopIndex]` 建议补齐 `TopIndex >= 0 && TopIndex < Items.Count` 边界保护。

### LC-FR-009 [P2 / OPEN] Linux 分组标题与刷新按钮缺少自动化回归用例，且 `.session-heading` 字重隐式耦合弹窗 `h2`

- **位置**：`src/linux/desktop/desktop.css:75, 167`、`src/linux/desktop/renderer.js:70, 91`、`tests/linux_desktop_ui_regression.js`。
- **说明**：
  1. `desktop.css:75` 的 `.session-heading` 应显式声明 `font-weight: 700`，避免隐式依赖文件末尾第 167 行面向弹窗的 `h2` 样式；
  2. `renderer.js:91` 追加「在线」标题后建议同步置 `onlineHeading = true` 保持状态完整；
  3. `tests/linux_desktop_ui_regression.js` 建议补充对 `#refresh` 位于 `.sidebar-bottom`、无过滤/有过滤时「在线」「离线」分组标题渲染、以及键盘上下键跳过标题的断言；
  4. `src/linux/desktop/renderer.js` 当前 945 行，距 1000 行软门禁仅余 54 行，后续再向该文件追加新功能前须按 `.claude/Rules.md` 第 10 条先拆分。

## 真实环境验收项（已迁入 `CRDocs/DEFERRED.md`）

| 问题 ID | 状态 | 说明 |
| --- | :---: | --- |
| `LC-FR-ENV-001` | DEFERRED | 真实多机 LAN、Wi-Fi/有线切网、非默认端口与物理多网卡/TUN 路由下的刷新延迟、重绑与旧版互通实测 |
| `LC-FR-ENV-002` | DEFERRED | Linux/QEMU 网络命名空间实跑最新 ELF（`tests/linux_desktop_regression.py`）及 Linux Electron 实机侧栏刷新按钮与分组渲染验收 |

---

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
# LC-FILE-TRANSFER-STABILITY-001 — 文件传输稳定性与并发限流加固执行记录

> 2026-11-25 · 执行者 Codex · 状态 `WAITING_FOR_GROK` · 等待 Gemini 独立审核
> 工程：`D:/TEmp/LanChat`

## 本轮范围

按 `.claude/Plan.md` 顶部「文件传输稳定性与并发限流加固」方案实施 P0、P1、P2，保持既有 LANC 魔数、packetType 2 文件报价字段顺序、确认字节及 JSONL 历史字段兼容；遵守全中文注释、零外部依赖和单文件低于 1000 行约束。

## 实际改动

### P0：传输稳定性与 Linux IPC

- `src/NetworkService.Transfer.cs` 的发送端和接收端统一使用 `Stopwatch.GetTimestamp()` 单调计时；当时间窗口不超过 1ms、字节增量为零或计算结果非有限/负数时输出 0，杜绝 `NaN`、`Infinity` 和负速率。
- 保持 accept 握手 `ReceiveTimeout = 10000ms`；进入文件流后设置 `SendTimeout = 30000ms`；读取最终 `doneConfirm` 前按文件大小设置 `ReceiveTimeout`，范围为 60000~300000ms。
- `src/linux/LanChat.Linux/DesktopService.cs` 对 `SpeedMBps` 使用 `double.IsFinite` 归零；JSONL 写出对单帧构造/序列化异常隔离并记录错误，不因单帧失败终止后续输出。

### P1：文件一致性、入口和底层并发

- 发送端先打开带正确 `FileShare` 的 `FileStream`，再从同一流读取 `Length` 写入协议头；发送循环按剩余长度计算 `toRead`，提前读到 0 时主动失败；文件读写器均以 `leaveOpen: true` 构造。
- 文件 TCP 双端在确认 packetType 2 后使用 256KB Socket 缓冲。
- `NetworkService` 增加按目标节点轮询的有界出站队列：单目标最多 2 路，全局最多 6 路，前序完成后继续出队。
- `src/MainForm.Files.cs` 统一单批 32 个文件和群聊最多 16 名在线成员，剪贴板、文件选择器、拖拽和群发入口均有超限提示；群发全部无效/离线时明确提示。
- `src/Controllers/FileTransferController.cs` 以 50ms 定时合并进度更新，降低高频 `BeginInvoke`；托管截图改为按发送目标引用计数共享清理，不再同步复制 N-1 份。

### P2：跨端细节

- Linux `SendFile` 向共享核心传入 `allowSourceDelete: managedPaste`，与 Windows 托管截图语义一致。
- 新增 `tests/FileTransferStabilityRegression.cs` 与 `tests/file_transfer_stability_regression.ps1`，覆盖 0 字节、64KB、约 1MB 文件、32 文件/16 收件人入口边界及全局 6/单目标 2 并发上限。

## LANC 兼容性判断

本轮未修改 `MAGIC`、packetType 2 后的 `transferId`、发送者 ID、昵称、文件名、Int64 文件大小字段顺序，也未修改 accept/doneConfirm 确认字节。修改只涉及流生命周期、计时、Socket 参数、发送调度和桌面层状态管理，旧版对端仍可按原协议收发。

## 实际验证

| 命令 | 结果 |
| --- | --- |
| `cmd /c "Tools\\Scripts\\Compile.bat < NUL"` | 返回 0；Windows 原生 `LanChat.exe` 与 `uninstall.exe` 编译完成。 |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/friend_refresh_regression.ps1` | 返回 0；好友刷新、入口 32/16 边界断言及既有 WinForms 回归通过。 |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/chat_composer_regression.ps1 -ExpectLocalPack` | 返回 0；剪贴板、LANC 文本/PNG、历史及截图清理回归通过。 |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/file_transfer_stability_regression.ps1` | 返回 0；0 字节、64KB、约 1MB、速率有限性、全局最多 6 路和单目标最多 2 路通过。 |
| `tests/ConcurrentReceive.cs` 回环编译并运行 | 返回 0；同名双文件最终 ACK、SHA-256 和完成事件通过。 |
| `dotnet build src/linux/LanChat.Linux/LanChat.Linux.csproj --no-restore` | 未通过环境门槛：宿主只有 SDK 9.0.304，项目目标为 net10.0，报 `NETSDK1045`。 |
| `dotnet restore/build ... -p:TargetFramework=net9.0 -p:RuntimeFrameworkVersion=9.0.8` | 返回 0；作为临时兼容语法证据，0 警告、0 错误，不替代正式 net10 构建。 |
| `git diff --check` | 返回 0；无空白错误。 |

## 行数与未验证边界

本轮相关手写文件行数：`NetworkService.Transfer.cs` 615、`FileTransferController.cs` 424、`MainForm.Files.cs` 503、`DesktopService.cs` 498、`FileTransferStabilityRegression.cs` 245，均低于 1000 行。

尚未验证：安装 .NET 10 SDK 后的正式 Linux net10 构建、Linux/ARM64/x86_64 ELF 实机、Linux Electron Xorg/Wayland/XWayland、真实双机新旧版本互通、真实 Windows 截图工具/Excel/WPS 粘贴，以及真实 LAN 下的吞吐和连接数取证。Windows 回环和 net9 临时目标证据不得替代上述实机验收。

## 交接审核重点

请 Gemini 独立核对：

1. `NetworkService` 出站队列在并发完成、拒绝、异常和 `Dispose` 时的计数回收及公平轮询；确认不存在超过 6/2 的窗口。
2. 发送端先开流取长度、动态截断失败、0 字节文件和最终确认超时逻辑；确认没有改变 LANC 字段顺序和确认字节。
3. 托管截图引用计数在多目标成功、部分失败、全部失败和窗体退出时的保留/删除语义。
4. Linux IPC 单帧序列化异常、非有限速率归零和 `managedPaste` 参数传递。
5. 现有回归输出与未验证边界是否被准确区分；若发现 P0/P1 问题，请在本文件写明复现条件、分级和修复要求，并将 `CRDocs/STATUS.md` 保持为打回状态。

---

