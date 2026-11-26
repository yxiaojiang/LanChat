# 审核

**LC-LINUX-003 · 第 1 轮 · 审核方 Gemini · 2026-11-26**

审核对象为 [Temps/aplan.md](../Temps/aplan.md)（对应需求输入 [Temps/a.txt](../Temps/a.txt)）。结论为 `CHANGES_REQUESTED`。

方案整体方向（继续复用共享 C# 网络核心、不破坏 `LANC` 与 `JSONL`、先补桌面生命周期与核心功能再收口实机验收、按职责拆模块守住 1000 行预警线）成立，且准确识别了多处真实缺口和 3 处隐藏代码偏差。但下列 **R1–R6** 包含现存源码事实遗漏、范围越界与高风险设计，会直接改变阶段 1～4 的实施范围与先后顺序，需提案方（Argon）修订并写入 [PROPOSAL.md](PROPOSAL.md) 后再进入第 2 轮审核。本轮未修改任何业务代码，未执行 Linux 实机或真实双机 LAN 验证。

## 本轮核对了什么

对照 [PlanDocs/README.md](README.md)、[PlanDocs/DECISIONS.md](DECISIONS.md)、[LC-LINUX-002 定稿](LC-LINUX-002/FINAL.md)、[LC-LINUX-002 基线](LC-LINUX-002/BASELINE.md)、[CRDocs/DEFERRED.md](../CRDocs/DEFERRED.md) 及 `.claude/Rules.md`，在当前 `main` 分支（`be819d1`，`v2.0.26`）逐一核对了以下源码与产物：

- **Linux 桌面主进程与桥接**：[main.cjs](../src/linux/desktop/main.cjs)（624 行）、[preload.cjs](../src/linux/desktop/preload.cjs)（96 行）、[renderer.js](../src/linux/desktop/renderer.js)（689 行）、[index.html](../src/linux/desktop/index.html)（89 行）、[overlay.js](../src/linux/desktop/overlay.js)（300 行）、[appearance.js](../src/linux/desktop/appearance.js)（236 行）、[settings-model.js](../src/linux/desktop/settings-model.js)（120 行）、[settings-pages.js](../src/linux/desktop/settings-pages.js)（501 行）、[settings.js](../src/linux/desktop/settings.js)（704 行）、[settings.html](../src/linux/desktop/settings.html)（92 行）、[package.json](../src/linux/desktop/package.json)（`version: 2.0.26`）。
- **Linux 自包含 C# 服务与 CLI**：[LanChat.Linux.csproj](../src/linux/LanChat.Linux/LanChat.Linux.csproj)、[DesktopService.cs](../src/linux/LanChat.Linux/DesktopService.cs)（502 行）、[LinuxProfile.cs](../src/linux/LanChat.Linux/LinuxProfile.cs)（401 行）、[Program.cs](../src/linux/LanChat.Linux/Program.cs)（576 行）。
- **Windows 当前基线与共享核心**：[MainForm.cs](../src/MainForm.cs)、[MainForm.Designer.cs](../src/MainForm.Designer.cs)、[MainForm.Chat.cs](../src/MainForm.Chat.cs)、[MainForm.Files.cs](../src/MainForm.Files.cs)、[MainForm.Tray.cs](../src/MainForm.Tray.cs)、[GrokConnectNodeForm.cs](../src/UI/Controls/GrokConnectNodeForm.cs)、[GrokCreateNodeForm.cs](../src/UI/Controls/GrokCreateNodeForm.cs)、[GrokSettingsMenuForm.cs](../src/UI/Controls/GrokSettingsMenuForm.cs)、[GeneralSettingsPanel.cs](../src/UI/Controls/SettingsPanels/GeneralSettingsPanel.cs)、[NetworkDeviceSettingsPanel.cs](../src/UI/Controls/SettingsPanels/NetworkDeviceSettingsPanel.cs)、[UpdateService.cs](../src/Services/UpdateService.cs)、[AppSettings.cs](../src/Services/AppSettings.cs)、[ChatHistoryService.cs](../src/Services/ChatHistoryService.cs)、[NetworkService.Interface.cs](../src/NetworkService.Interface.cs)、[NetworkService.Discovery.cs](../src/NetworkService.Discovery.cs)、[NetworkService.Transfer.cs](../src/NetworkService.Transfer.cs)。
- **构建、发布与测试门禁**：[build-electron.py](../packaging/build-electron.py)、[lanchat-launcher.sh](../packaging/lanchat-launcher.sh)、[ExportLinuxDesktopAssets.cs](../packaging/ExportLinuxDesktopAssets.cs)、[publish-release.py](../packaging/publish-release.py)、[BuildLinuxDeb.py](../Tools/Scripts/BuildLinuxDeb.py)、[release/linux-electron/SHA256SUMS.json](../release/linux-electron/SHA256SUMS.json)、[tests/linux_desktop_ui_regression.js](../tests/linux_desktop_ui_regression.js)（924 行，第 138–149 行硬断言 17 个文件 `< 1000` 行）。
- **检索验证**：在 `src/` 下全量检索 `BtnConnectNode`、`BtnCreateNode`、`BtnAddIp`、`GrokConnectNodeForm`、`GrokCreateNodeForm`、`TriggerScreenshot` 调用点；确认 `bin/lc-linux-002/cache/dotnet/dotnet.exe` 与 `release/linux-electron/SHA256SUMS.json` 均存在。

本轮为源码与构建产物静态核对，**未运行 Linux 虚拟机/实机、未启动 Xorg/Wayland 桌面会话、未运行双机局域网测试**。

## 属实

以下提案判断与当前源码完全一致：

1. **共享核心与协议边界准确**：Linux ELF 通过 `LanChat.Linux.csproj` 直接源码链接 `Models.cs`、`EmojiPack.cs`、`NetworkService*.cs`、`AppSettings.cs`、`SessionManager.cs`、`ChatHistoryService.cs`；LANC 封包与 JSONL 历史格式保持不动、不在 Node.js 重写局域网套接字的原则成立。
2. **桌面生命周期 P0 缺口属实**：`main.cjs:80-84` 关闭主窗直接调 `app.quit()`；Electron 端尚无 `Tray`、无 `Notification`、无点击通知跳转对应会话。
3. **输入区、表情面板与群聊选人代差属实**：
   - Windows `MainForm.Designer.cs:432-561` 已升级为 214px/188px 高输入区与 5 个 48px 展开工具按钮；Linux `index.html:57-63` 仍是旧版单行胶囊 + 单个 `+` 号菜单 + `<textarea>`；
   - Linux `overlay.js:250-273` 仍是单网格表情面板，缺“最近使用（12 项）”与 496×548 微信比例 8 列布局；
   - Windows `MainForm.Files.cs:295-430` 支持群聊 1～16 名在线成员多选发文件，且仅 1 人在线时免弹窗直发；Linux `renderer.js:310-334` 仍为单选且 1 人在线也弹窗。
4. **11 项“仅展示/假功能”识别属实**：`GeneralSettingsPanel.cs`、`NetworkDeviceSettingsPanel.cs` 与 `settings-pages.js`、`settings.js:420-427` 证实，10 项双端控件确为空回调/静态展示，Linux `检查更新 >` 确为占位提示框。
5. **3 处隐藏代码级偏差属实**：
   - `main.cjs:357-362` `dispatchFiles` 使用 `for...of` + `await child.request('file')` 逐个串行发送，压制了 `NetworkService.Transfer.cs:15-16` 的单目标 2 路 / 全局 6 路并发队列；
   - `DesktopService.cs:340-341` `KnownAddresses()` 仅查归档会话，遗漏运行期上线过但未聊天的节点 IP（`LC-FR-007`）；
   - `Program.cs:70-186` 独立 CLI 未绑定 `ChatHistoryService.ProfileStorageDirectory`，收发消息不落盘 JSONL。
6. **未把宿主机桩测试冒充 Linux 实机通过**：第 3 部分单列 17 项“代码已有但未验收”，符合 `LC-LINUX-002/FINAL.md` 与 `CRDocs/DEFERRED.md` 口径。
7. **行数门禁预警准确**：`tests/linux_desktop_ui_regression.js` 已 924 行且硬性断言 `< 1000` 行，新增业务必须拆独立模块与独立测试脚本。

## 不成立或说重了

1. **把“3 主题 / 11 色外观持久化”标为“代码已有但未验收”不成立——实为现存 P1 代码缺陷。**
   - 核对源码：`appearance.js:9-21` 与 `settings-model.js:34` 提供 11 种强调色（索引 `0..10`），`LinuxProfile.cs:31` 读取上限也是 `11`；但 **`main.cjs:523`** 与 **`LinuxProfile.cs:112`** 的保存校验仍写死 `accentPreset >= 5`！Linux 用户在设置页选择第 6～11 种强调色（翠绿、青、蓝、魅紫、粉、灰）会直接触发 `"强调色预设无效"` 报错。这不是“待验收”，而是现成 Bug。
2. **把“连接节点窗口 / 新建节点窗口”定性为“Windows 当前主界面明确缺失”并强塞入 Linux 输入区工具栏不成立。**
   - 全量检索 `src/`：`MainForm.Chat.cs:471-522` 中的 `BtnConnectNode_Click`、`BtnCreateNode_Click`、`BtnAddIp_Click` 在当前 Windows `v2.0.26` **没有任何可见按钮绑定**（`MainForm.Designer.cs:92,103` 的旧按钮均为 `Visible = false`，左侧底栏仅有设置圆钮和刷新按钮，`GrokSettingsMenuForm.cs` 也没有节点入口）。且 `GrokCreateNodeForm` 填非 IP 名称时只打印一条“已创建新节点”系统行，既不建对象也不保存 14 种形态/11 色。
   - 手动 IPv4 直连（`probe-ip`）确有跨网段/VPN 实用价值，但不应以“对齐 Windows 现有主界面”为由把已废弃的“新建节点（14 形态）”空壳弹窗塞进阶段 2，也不应在未经 UI 位置裁定的情况下硬加进底部输入工具栏（`aplan.md` 第 204 行）。
3. **阶段 3 将双端均为假功能的 10 项设置一次性全做成真业务，超出“同步 Windows 当前功能”范围，且部分设计不合理。**
   - `LC-LINUX-002/FINAL.md` 第 2 节口径是：“当前仅改变控件外观或文案的项登记实际状态，按基线还原展示并明确其可用性。” 把这 10 项（含三语 i18n、内置链接预览、64KB 刷盘校验、TUN/单播/历史开关）全在 Linux 侧单边做实，会立刻让 Linux 与 Windows 行为分叉，且维护成本暴增。
   - 具体硬伤：
     - `VerifyChunkFlush`（`aplan.md` 第 240 行）：`NetworkService.Transfer.cs:201-226` 是流式写临时文件后由对端收尾 ACK 确认，并没有 64KB 块级校验和；仅在关闭流前强加 `fs.Flush(true)` 既不等于“64KB 分块校验”，又会拖慢传输，与 `.claude/Rules.md` 第 6 条“最小实现、极速传输”相悖。
     - `链接打开方式` 的“内置极速预览”与当前渲染页严格 CSP（`default-src 'none'`）及纯局域网边界冲突：不能加载外网页面，若仅展示 URL 卡片则名不副实。
     - `语言（简/英/繁）` 涉及全量界面文案、弹窗、状态栏与后端错误信息的国际化，而 Windows 端本身完全无 i18n，单在 Linux 塞一份前端词典会制造半中半英的割裂体验。
4. **阶段 2 严重低估用自研 Web 富文本编辑器（`composer-box.js`）复刻 Windows RichEdit 内联表情的复杂度，且忽略了与 `preload.cjs` 的硬冲突。**
   - `preload.cjs:31-66` 的系统剪贴板文件/截图粘贴拦截强依赖原生 `<textarea id="message">` 的 `target.value`、`selectionStart`、`selectionEnd`、`maxLength` 和 `setRangeText(...)`。把 `<textarea>` 换成 `contenteditable` 会直接破坏 `preload.cjs` 的纯文本回落粘贴，且 `renderer.js` 的草稿/自动增高、`scrollbar.js` 的滚动绑定、以及 Linux `fcitx5`/`ibus` 在 `contenteditable` 下的中文预编辑选区极易出平台兼容性事故。
   - 同时，`aplan.md` 阶段 2 对 Linux 截图按钮只写了一句“调用系统截图或提示剪贴板粘贴”，未处理 Ubuntu 24.04 默认已移除 `gnome-screenshot`、Wayland 截图受限于 `xdg-desktop-portal` 的现实差异。
5. **阶段 1 对 Linux 托盘可用性的判定方式说轻了，与 `LC-LINUX-002` 已采纳的 Wayland 桌面等效表冲突。**
   - Electron 在无 AppIndicator 扩展的默认 GNOME 桌面上，`new Tray(...)` 依然成功返回对象而不抛错，但图标完全不可见！`LC-LINUX-002/REVIEW-R1.md` R1 与 `FINAL.md` 已明确：原生 Wayland 不要求图标闪烁、不做光标悬浮预览，且**未证实托盘可见并能唤回同一窗口时，关闭窗口不得隐藏**。仅靠 `trayManager.canHideToTray()`（若未定义 D-Bus 宿主探测或保守回退）会在默认 Ubuntu GNOME 上制造窗口失联的 P0 事故。

## 遗漏

以下 6 项实质意见影响阶段 0～5 的范围、设计和顺序，需在下一轮提案中逐条回应并修正：

### R1 · 补列并前置修复现存 P1 Bug：Linux 11 色强调色保存上限仍写死为 5
- **影响**：差异矩阵、阶段 0 / 阶段 2A。
- **最小修正**：将“外观 11 强调色持久化”从“代码已有但未验收”移出，登记为明确缺陷；修复 `src/linux/desktop/main.cjs:523` 与 `src/linux/LanChat.Linux/LinuxProfile.cs:112`（上限 `5` → `11`），并在 Node 与 Python 回归中补充 `accentPreset = 10` 的落盘与重启恢复用例。

### R2 · 将 11 项“仅展示/假功能”按三类分级，避免单边过度造轮子
- **影响**：阶段 3 范围与双端一致性。
- **最小修正**：提案需明确以下分级（如需扩大范围，需单列供用户拍板，且共享 C# 配置变更必须双端同步）：
  - **A 类（必接真业务）**：`检查更新`（阶段 4，Windows 已有真功能）、`后台网络驻留运行` 与 `托盘未读悬浮预览`（随阶段 1 托盘生命周期持久化并生效）、主窗真实绑定 `Ctrl+F`（聚焦搜索）与 `Ctrl+L`（清空当前会话）；
  - **B 类（低复杂度可选项，需用户确认是否双端同步接线）**：`TUN/TAP 过滤`、`多网卡并发单播探测`、`自动加载历史消息`、`发送快捷键切换`——若用户决定做，须同步明确 `AppSettings.cs` 与 `LinuxProfile.cs` 的统一键名和 Windows 端是否同步接线，避免配置漂移；
  - **C 类（本轮明确不做，改为诚实只读/说明或保持基线展示）**：`语言（三语 i18n）`、`链接打开方式（内置预览）`、`64KB 分块传输校验`（改为只读能力展示，不加降低吞吐的 `Flush(true)`）、`网络协议通道`（仅展示当前绑定网卡/端口只读信息或触发既有 `refresh`，不另造复杂通道切换）。

### R3 · 阶段 1 补齐“托盘可见性真实判定”与 Xorg/Wayland 验收分流
- **影响**：阶段 1 设计与阶段 5 桌面验收。
- **最小修正**：严格沿用 `LC-LINUX-002/FINAL.md` 桌面等效契约：
  1. 写明 `canHideToTray()` 的真实判定口径——不能以 `new Tray()` 不抛错为准，须结合桌面环境检测（例如检查 D-Bus `org.kde.StatusNotifierWatcher` 的 `IsStatusNotifierHostRegistered`）或保守默认（首次启用隐藏到托盘前须由用户确认托盘可见，失败/不可确认时关闭一律最小化到 Dock/任务切换器，确保 `second-instance` 与任务栏始终可恢复）；
  2. 明确 450ms 图标闪烁与 320px 悬浮预览卡片仅作 **Xorg 可选增强**，原生 Wayland 验收以“应用内未读 + 系统通知 + 通知点击切会话（支持时）”为准。

### R4 · 拆分过载的阶段 2，剥离 Windows 废弃节点空壳，并补齐 `preload.cjs` 与截图方案
- **影响**：阶段 2（原阶段 2 混装 7 项差异极大的任务，高风险项会拖垮低风险交付）。
- **最小修正**：
  1. **拆出阶段 2A（低风险核心功能对齐）**：修复 R1（11 强调色）、`DesktopService` 新增 `probe-ip` 与 `LC-FR-007` 运行期地址缓存、`Program.cs` 独立 CLI 绑定 `ChatHistoryService.ProfileStorageDirectory` 落盘历史、群聊文件 1～16 人多选（单在线好友免弹窗直发）+ 托管截图引用计数清理、解除 `main.cjs` `dispatchFiles` 串行 `await`、升级 496×548 表情选择器（含“最近使用”12 项）与 214/188px 工具栏布局外壳；
  2. **节点管理止损**：只保留实用的“手动输入 IPv4 直连握手”轻量入口（提案须明确放在设置“设备连接”页还是侧栏底栏，不侵占聊天工具栏），**明确不做** Windows 主窗本身都已无入口的“新建节点（14 种形态调色盘）”空壳窗；
  3. **单拆阶段 2B（高风险专项：内联表情富文本输入框）**：若用户确认 Linux 需要输入框内联 PNG 表情，须补齐 `preload.cjs` 非 `<textarea>` 剪贴板粘贴重构设计、撤销/换行/滚动/`fcitx5`/`ibus` 预编辑验证，以及回退 `<textarea>` 的开关边界；同时明确 Linux 截图按钮的真实行为（探测系统截图入口/Portal，不可用时隐藏或提示使用系统截图后 `Ctrl+V`）。

### R5 · 阶段 4 补全 `SHA256SUMS.json` 结构解析、403 降级校验与安全目录/安装退出流
- **影响**：阶段 4 更新与维护模块。
- **最小修正**：
  1. 明确按 `release/linux-electron/SHA256SUMS.json` 实际嵌套格式 `manifest[arch].artifacts[assetName]` 校验（`amd64`/`arm64`）；
  2. 明确 GitHub API 403 走 302 重定向降级时，仍尝试从 `https://github.com/yxiaojiang/LanChat/releases/download/v<ver>/SHA256SUMS.json` 拉取摘要；若摘要不可获取，必须给用户明确未校验警告或退回打开 GitHub Release 网页，禁止静默跳过校验；
  3. 禁止使用多用户共享路径 `/tmp/LanChatUpdate/`，改为用户独占目录（如 `XDG_CACHE_HOME/lanchat/updates` 或 `~/Downloads`，目录权限 `0o700`）；
  4. 明确 `.deb` 更新安装前先引导退出当前 LanChat（释放配置锁与 38888/38889 端口），再复制 `sudo apt install ./lanchat_<ver>_<arch>.deb` 命令或打开所在目录，不承诺非 root 静默覆盖安装。

### R6 · 合规写入 `PlanDocs/PROPOSAL.md`，并把 Linux ELF/桌面冒烟前置，不要全堆到阶段 5
- **影响**：提案合规性与全流程风险控制。
- **最小修正**：
  1. 下一轮修订须按 [PlanDocs/README.md](README.md) 规定的 7 个二级标题写入 `PlanDocs/PROPOSAL.md`（补齐 `## 明确不做` 与 `## 对上一轮审核的处理`），不要继续停留在 `Temps/aplan.md`；
  2. 不要把所有 Linux 实跑验证都积压到最后阶段 5：建议在阶段 0 之后、大规模改代码前，先用现存 `v2.0.26` ELF 跑通一次 Linux 网络命名空间基线回归（关闭或摸底 `LC-LINUX-002-ENV-001`），并在阶段 1（托盘/通知）与阶段 2B（内联输入法）收尾时各设一次轻量实机检查点。

## 顺序

建议按以下调整后的顺序修订提案并推进（把高风险项与低风险刚需拆开，并前置实机基线摸底）：

| 阶段 | 实施内容 | 放行与检查点 |
| --- | --- | --- |
| **阶段 0 · 基线冻结与既有 ELF 摸底** | 冻结 `v2.0.26` 差异矩阵与 IPC 契约；拆分 `tests/linux_desktop_ui_regression.js`（924 行）公共辅助函数腾出行数余量；复跑宿主机 4 套基线回归；**优先在 Linux 环境对现有 `v2.0.26` ELF 跑一次 `linux_desktop_regression.py` 摸底**。 | 4 套宿主机回归全绿；明确 R2 的 B 类设置项与 R4 的 IPv4 直连入口位置的用户决定。 |
| **阶段 1 · 桌面生命周期、托盘与通知（含 A 类托盘开关）** | 新增 `tray-manager.cjs` 与托盘图标导出；按 R3 的真实可见性判定与保守回退实现关闭隐藏/恢复/真退出、系统通知与通知点击切会话；同步接通设置页 `keepAliveInTray` 与 `trayPreview` 持久化。Xorg 悬浮预览列为可选增强。 | `linux_desktop_lifecycle_regression.js` 通过；**尽早在 1 台 Linux 桌面（GNOME Xorg/Wayland）实测托盘可见性与无托盘不失联**。 |
| **阶段 2A · 低风险核心功能同步与缺陷修复** | 修复 R1（11 色强调色保存上限）；`DesktopService` 补 `probe-ip` 与 `LC-FR-007` 运行期地址缓存；`Program.cs` CLI 补 `ChatHistoryService` 历史落盘；群聊 1～16 人多选发文件（1 人在线直发）+ 托管截图引用计数；解除 `main.cjs` `dispatchFiles` 串行 `await`；补轻量 IPv4 直连入口（不做废弃“新建节点”空壳）；升级 496×548 表情选择器（含最近 12 项）与 214/188px 工具栏外壳（暂保留原生 `<textarea>`）；主窗绑定 `Ctrl+F` / `Ctrl+L`。 | C# 零警告编译、Windows 原生编译与回归零退化、Node/Python 新增回归全绿，所有手写文件 `< 1000` 行。 |
| **阶段 3 · 设置页收口（按 R2 分级执行）** | 按用户对 R2 B 类项的拍板，仅接线确认要做的设置项（若改共享 `NetworkService`，须同步保证 Windows 零回归及配置键双端一致）；C 类项保持诚实只读/说明展示，不单边造轮子。 | 对应设置持久化、只读备用确认与重启恢复回归全绿；Windows 端编译与好友/文件回归全绿。 |
| **阶段 4 · 在线更新与维护入口** | 新增 `update-service.cjs`（GitHub API + 302 降级、`amd64`/`arm64` 与安装/便携资产匹配、按 R5 解析嵌套 `SHA256SUMS.json`、用户独占 `0o700` 缓存目录下载与退锁安装引导）+ 520×440 更新弹窗，接通通用页/关于页/托盘菜单。 | `linux_desktop_update_regression.js` Mock 回归全绿；渲染进程 CSP 无放宽。 |
| **阶段 2B（可与阶段 3/4 并行评估）· 内联表情富文本输入框专项** | 若用户确认必需：重构 `preload.cjs` 剪贴板粘贴链路以兼容 `contenteditable`，实现 `composer-box.js` 内联表情基线对齐、光标收拢与原子删表情，并保留回退 `<textarea>` 的保护边界。 | 必须在真实 Linux `fcitx5` 与 `ibus` 下实测预编辑、选词、退格、复制粘贴与发送无异常后才默认开启。 |
| **阶段 5 · 双架构构建与物理实机/真网总验** | 用 `BuildLinuxDeb.py` 构建双架构 `.deb`、便携包与独立 CLI；在物理 `x86_64`、物理 `ARM64` 与物理 Windows 主机上完成 Xorg/Wayland/XWayland、`fcitx5`/`ibus`、多 DPI、2000 条历史、旧包升级回退、真实 LAN（含 TUN/切网）与 `CRDocs/DEFERRED.md` 清零。 | 实机截图、日志、`dpkg -l`、ELF 机器码与 SHA-256 证据齐全，进入 `CRDocs` 独立代码审核。 |

## 未核实

以下项目本轮未实测，留待对应阶段现场取证：

1. Ubuntu 22.04/24.04 GNOME（Xorg、原生 Wayland、XWayland）及银河麒麟 V10 ARM64 上 `org.kde.StatusNotifierWatcher` 的实际返回值、Electron 45.4.1 `Tray` 可见性与 `Notification` 点击唤回窗口表现。
2. Linux 桌面下 `fcitx5` 与 `ibus` 在原生 `<textarea>` 及 `contenteditable` 中的真实中文预编辑、候选词上屏与按键拦截行为。
3. 最新 `v2.0.26`（及后续版本）ELF 在 Linux 非 root 仅回环网络命名空间下的 `tests/linux_desktop_regression.py` 实跑结果。
4. 物理 `x86_64`、物理 `ARM64` 与物理 Windows 主机在同一真实局域网（含 TUN 虚拟网卡、多物理网卡、Wi-Fi/有线切换、旧版 `v2.0.15`/`v2.0.16` 互通）及真实 `apt install` 升级/降级回退结果。

## 结论

**`CHANGES_REQUESTED`。**

请提案方（Argon）阅读本审核稿，按 **R1–R6** 与调整后的顺序修订方案，并使用 [PlanDocs/README.md](README.md) 规定的 7 个二级标题将完整修订版写入 [PlanDocs/PROPOSAL.md](PROPOSAL.md)，再将 [PlanDocs/STATUS.md](STATUS.md) 更新为 Round 2 `READY_FOR_REVIEW` 交回审核。用户明确采纳前不修改业务代码、不提交、不发布。
