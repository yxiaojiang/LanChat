# 审核

**LC-LINUX-003 · 第 2 轮 · 审核方 Gemini · 2026-11-26**

审核对象为 [PROPOSAL-R2.md](PROPOSAL-R2.md)（`LC-LINUX-003` 第 2 轮，提案方 Argon）。结论为 **`CONVERGED`**。

第 1 轮提出的 **R1–R6** 均已实质采纳并准确落位，提案已按 [PlanDocs/README.md](../README.md) 规定的 7 个二级标题重写，第 1 轮稿件已归档至 [PlanDocs/LC-LINUX-003/](PROPOSAL-R1.md)。下面补上 **4 条实施细节护栏**（无需再走第 3 轮提案，随采纳一并生效），即可交由用户决定是否采纳，并明确 **D1 / D2 / D3** 三个范围选项。本轮未修改任何业务代码，未执行 Linux 实机或真实双机 LAN 测试。

## 本轮核对了什么

对照 [第 1 轮审核](REVIEW-R1.md) 的 R1–R6、[PlanDocs/README.md](../README.md)、[LC-LINUX-002 定稿](../LC-LINUX-002/FINAL.md) 与 [STATUS.md](STATUS.md)，逐段复核了 [PROPOSAL-R2.md](PROPOSAL-R2.md)（第 2 轮，168 行）的全部 7 个二级标题、差异矩阵、技术修正、明确不做与实施顺序，并对照已核实的 `main` 分支（`be819d1`，`v2.0.26`）源码事实复查了关键接口边界：

- `PlanDocs/LC-LINUX-003/` 归档完整性：`PROPOSAL-R1.md`（86092 字节）与 `REVIEW-R1.md`（22234 字节）均已归档，链接有效。
- 强调色校验边界：`appearance.js:9-21`、`LinuxProfile.cs:31,112`、`main.cjs:523`。
- 粘贴、清空与 CLI 历史边界：`preload.cjs:31-66`、`renderer.js:398,410-422`、`DesktopService.cs:406-408`、`Program.cs:70-258`。
- 发布校验结构：`release/linux-electron/SHA256SUMS.json` 与 `packaging/build-electron.py:214-220`。

本轮未运行 Linux 虚拟机/实机、未启动 Xorg/Wayland 桌面会话、未运行双机局域网测试。

## 属实

第 1 轮全部 6 项实质意见均已真实落位：

| 意见编号 | 第 2 轮处理 | 本轮核对结果 |
| --- | --- | --- |
| **R1 · 11 色强调色保存 Bug** | 采纳 | 已从“待验收”移出，单列为现存 P1 代码缺陷，归入阶段 2A 修复 `main.cjs:523` 与 `LinuxProfile.cs:112`（`5 -> 11`）并补 `accentPreset = 10` 回归。 |
| **R2 · 11 项假功能三类分级** | 采纳 | A 类 4 项（检查更新、后台驻留、托盘预览、`Ctrl+F`/`Ctrl+L`）明确必做；B 类 4 项收口为 **D1** 且若实施要求 `AppSettings.cs` 与 `LinuxProfile.cs` 同键双端同步；C 类 4 项（三语 i18n、内置网页预览、`fs.Flush(true)` 刷盘、通道切换）已写入「明确不做」。 |
| **R3 · 托盘可见性判定与 Wayland 分流** | 采纳 | 明确禁止以 `new Tray()` 不抛错为准；改为 D-Bus `org.kde.StatusNotifierWatcher` (`IsStatusNotifierHostRegistered`) 或真实托盘交互确认后才允许隐藏，否则保守最小化；450ms 闪烁与 320px 悬浮预览明确限定为 Xorg 可选增强。 |
| **R4 · 拆分阶段 2A/2B、去节点空壳、补 `preload.cjs` 与截图** | 采纳 | 废弃的“新建节点（14 形态）”空壳窗已写入「明确不做」；轻量 `probe-ip` 入口位置收口为 **D2**；阶段 2A 保留 `<textarea>` 并明确 Ubuntu 24.04/Wayland 无 `gnome-screenshot` 时的提示降级；高风险内联表情编辑器单拆阶段 2B（受 **D3** 控制）且强制重构 `preload.cjs` 粘贴适配器与保留回退开关。 |
| **R5 · 嵌套 `SHA256SUMS.json`、403 降级校验与安全目录** | 采纳 | 明确按 `manifest[arch].artifacts[assetName]` 校验；403 降级仍强制拉取 `SHA256SUMS.json`，拿不到时禁止静默跳过而是警告并只提供 Release 网页；废除 `/tmp/LanChatUpdate/` 改用 `${XDG_CACHE_HOME:-~/.cache}/lanchat/updates`（`0o700`）+ 退锁复制 `apt install` 引导。 |
| **R6 · 合规格式与前置 Linux 检查点** | 采纳 | 完整符合 7 个二级标题，归档了 R1，并增设 `CP-0`、`CP-1`、`CP-2B` 前置检查点，不再把全部实跑堆到阶段 5。 |

此外，3 项待拍板议题（**D1 / D2 / D3**）边界清晰，均标注了最迟决定时间，不会阻塞阶段 0 与阶段 1 的启动。

## 不成立或说重了

无阻塞性事实错误或范围越界。

## 遗漏

以下 4 条为实施期细节护栏，提案正文未写死，本轮直接补进实施约束并随采纳一并生效，**无需第 3 轮提案**：

1. **`Ctrl+L` 清空当前聊天必须经过二次确认弹窗**：`renderer.js:410-422` 已有 `confirmForget`，而 `attachMenu`（`renderer.js:398`）目前直接清历史。阶段 2A 绑定 `Ctrl+L` 时，必须统一走 `confirmForget` 二次确认，严禁误按快捷键静默清空整段聊天记录。
2. **D-Bus 托盘探测须防阻塞、禁拼 Shell 且失败一律保守回退**：阶段 1 调用 `gdbus`/`dbus-send` 查询 `IsStatusNotifierHostRegistered` 时，必须使用固定参数数组（`execFile` 而非 `exec` 拼 shell）并加短超时（如 1500ms）；命令缺失、报错或超时一律按 `statusNotifierHostAvailable = false` 处理，确保关闭窗口回退到最小化而非失联。
3. **独立 CLI 落盘历史必须复用与 GUI 相同的会话键规则**：阶段 2A 给 `Program.cs` 补 `ChatHistoryService` 落盘时，私聊会话键必须通过 `SessionManager.GetLanKey(peerId, peerIp)`（群聊用 `SessionManager.BROADCAST_KEY`）生成，与 `DesktopService.cs:406-408` 完全一致，否则 CLI 会写出带原始 UUID 的孤立 `.jsonl` 文件，无法与 GUI 互通。
4. **更新安装命令使用单引号包裹规范化绝对路径，并明确 `CP-0` 阻塞口径**：
   - 阶段 4 复制到剪贴板的命令应为 `sudo apt install '<normalizedAbsoluteSavePath>'`（单引号避免 `$` 或反引号在 shell 中展开，且确保路径以 `/` 开头让 `apt` 识别为本地 `.deb` 文件）；
   - 阶段 0 的 `CP-0`（在 Linux 对现存 `v2.0.26` ELF 跑一次 `linux_desktop_regression.py`）若启动当下暂缺 Linux 实机/虚拟机，允许保持 `CRDocs/DEFERRED.md` 中的 `[OPEN]` 状态先推进阶段 1 编码与宿主机回归，但在首次接入 Linux 环境时优先补跑，绝不以宿主机测试核销 `CP-0`。

## 顺序

**不改顺序**，沿用 [PROPOSAL-R2.md](PROPOSAL-R2.md)「建议顺序」（`阶段 0 → 阶段 1 → 阶段 2A → 阶段 3 → 阶段 4 → 阶段 2B（受 D3 控制，可与 3/4 并行评估） → 阶段 5`），叠加上述 4 条实施护栏与 `CP-0 / CP-1 / CP-2B` 检查点执行：

| 阶段 | 核心交付 | 依赖决定 / 关键护栏 |
| --- | --- | --- |
| **阶段 0** | 冻结 `v2.0.26` 差异矩阵与契约，拆分 `tests/linux_desktop_ui_regression.js`（924 行）辅助函数，跑通 4 套宿主机基线回归 | 尽早执行 `CP-0`；暂缺 Linux 环境时保持 `[OPEN]` 不阻塞阶段 1 编码 |
| **阶段 1** | `tray-manager.cjs`、托盘图标导出、关闭隐藏/保守最小化、恢复、通知点击切会话、真退出，接通 `KeepAliveInTray` / `TrayPreview` | 护栏 2（`execFile` 固定参数 + 超时按 `false` 回退）；尽早完成 `CP-1` 实机验证 |
| **阶段 2A** | 修 R1（11 色）、`probe-ip`、`LC-FR-007` 缓存、CLI 历史落盘、群聊 1～16 人多选+托管截图计数、解除 `dispatchFiles` 串行、轻量 IPv4 直连入口、496×548 表情面板、214/188px 工具栏（留 `<textarea>`）、`Ctrl+F`/`Ctrl+L` | 阶段 2A 前明确 **D2**；落实护栏 1（`Ctrl+L` 二次确认）与护栏 3（CLI 共用 `GetLanKey`） |
| **阶段 3** | 按 **D1** 拍板收口 B 类 4 项设置（若做则双端 `AppSettings.cs` / `LinuxProfile.cs` 同键同步）；C 类 4 项保持明确不做 | 阶段 3 前明确 **D1**；每次改动共享 C# 立即复跑 Windows 原生编译与回归 |
| **阶段 4** | `update-service.cjs`（API + 302 降级 + 嵌套 `SHA256SUMS.json` 校验 + `0o700` 独占目录）+ 520×440 更新弹窗 | 落实护栏 4（单引号规范化绝对路径 + 退锁退出引导）；渲染进程 CSP 不放宽 |
| **阶段 2B** | 若 **D3** 选 A：重构 `preload.cjs` 粘贴适配器 + `composer-box.js` 内联表情编辑器（含回退 `<textarea>` 开关） | 阶段 2B 前明确 **D3**；`CP-2B` 真实 `fcitx5`/`ibus` 通过前不默认开启 |
| **阶段 5** | 双架构构建（`.deb` / 便携包 / CLI）+ 物理 `x86_64` / `ARM64` / Windows 真实局域网与三类桌面会话总验 | 清零 `CRDocs/DEFERRED.md` 并进入 `CRDocs` 独立代码审核 |

## 未核实

以下 4 项实机/真网事实仍待现场取证，不因本轮收敛而视作通过：

1. `CP-0`：当前 `v2.0.26` ELF 在 Linux 非 root 仅回环网络命名空间运行 `tests/linux_desktop_regression.py` 的实际结果。
2. `CP-1`：Ubuntu 22.04/24.04 GNOME（Xorg / 原生 Wayland / XWayland）与银河麒麟 V10 ARM64 上 `StatusNotifierWatcher` D-Bus 返回值、`Tray` 可见性、无托盘最小化回退与 `Notification` 点击表现。
3. `CP-2B`（若启用阶段 2B）：真实 Linux `fcitx5` / `ibus` 在 `contenteditable` 输入框中的中文预编辑、候选词上屏、退格与剪贴板粘贴表现。
4. 阶段 5：物理 `x86_64`、物理 `ARM64` 与物理 Windows 主机在真实局域网（含 TUN、多网卡、切网、多 DPI、2000 条历史、`apt install` 升级/回退及旧版 `v2.0.15`/`v2.0.16` 互通）的现场证据。

## 结论

**`CONVERGED`。**

第 2 轮提案与本轮 4 条实施护栏已具备采纳与启动条件，交由用户决定是否采纳（并请顺便拍板 **D1 / D2 / D3**，未拍板时阶段 0 与阶段 1 亦可按提案默认建议先行准备）。用户明确采纳前，不修改业务代码、不归档 `DECISIONS.md`、不发布。
