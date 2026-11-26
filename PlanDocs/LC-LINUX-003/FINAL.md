# Windows 当前功能同步到 Linux（V4.0 双端功能统一）

**LC-LINUX-003 · 已采纳定稿 · 2026-11-26**

开发分支：`codex/v4.0-cross-platform-unification`（基线：`V3.0` / 程序输出版本 `2.0.26`）。用户明确采纳 [第 2 轮提案](PROPOSAL-R2.md) 与 [第 2 轮审核](REVIEW-R2.md) 的 4 条实施护栏，并拍板 **D1 = A、D2 = A、D3 = A**。实施从 [详细执行计划](../../.claude/Plan.md) 阶段 0 启动。

[第 2 轮提案](PROPOSAL-R2.md) · [第 2 轮审核](REVIEW-R2.md) · [收口状态](STATUS.md) · [详细执行计划](../../.claude/Plan.md)

## 采纳的决定

| 编号 | 议题 | 决定 |
| --- | --- | --- |
| **D1** | R2 B 类 4 项设置（`TunFilter`、`UnicastProbe`、`AutoLoadHistory`、`SendShortcutMode`）接线范围 | **选 A**：4 项在阶段 3 于 Windows `AppSettings.cs` 与 Linux `LinuxProfile.cs` 使用统一 JSON 键名、保持与现行行为一致的默认值，并在双端设置面板同步接线持久化。 |
| **D2** | 轻量“手动输入 IPv4 直连握手（`probe-ip`）”GUI 入口位置 | **选 A**：放入设置中心 **“设备连接”页** 卡片顶部，不改动主窗左侧栏现有视觉布局。 |
| **D3** | 阶段 2B（输入框内联 PNG 表情富文本编辑器）是否纳入交付 | **选 A**：阶段 2A 交付 214/188px 微信式高输入区外壳、展开工具栏、496×548 表情选择器与群聊 1～16 人多选并保留原生 `<textarea>`；阶段 2B 作为独立高风险专项重构 `preload.cjs` 粘贴适配器与 `composer-box.js`，必须通过真实 Linux `fcitx5`/`ibus` 实测（`CP-2B`）后才默认开启，并保留一键回退开关。 |

### 随采纳一并生效的第 2 轮审核护栏（4 条）

1. **`Ctrl+L` 二次确认**：阶段 2A 绑定 `Ctrl+L` 清空当前聊天时，必须经现有 `confirmForget` 二次确认弹窗，严禁一键静默清空整段历史。
2. **D-Bus 托盘探测安全与保守回退**：阶段 1 查询 `org.kde.StatusNotifierWatcher` (`IsStatusNotifierHostRegistered`) 必须使用 `execFile` 固定参数数组（禁止拼 Shell）及短超时（1500ms）；命令缺失、报错或超时一律按 `false` 处理，回退为最小化到 Dock/任务切换器，确保窗口永不失联。
3. **独立 CLI 历史会话键一致**：阶段 2A 为独立 CLI（`Program.cs`）接入 `ChatHistoryService` 落盘时，私聊键必须经 `SessionManager.GetLanKey(peerId, peerIp)`（群聊用 `SessionManager.BROADCAST_KEY`）生成，与 `DesktopService.cs:406-408` 严格一致。
4. **单引号更新命令与 `CP-0` 阻塞口径**：阶段 4 复制到剪贴板的安装命令采用单引号包裹规范化绝对路径 `sudo apt install '<normalizedAbsoluteSavePath>'`；阶段 0 的 `CP-0`（Linux 命名空间跑 `v2.0.26` ELF）在当下暂缺 Linux 环境时允许保持 `CRDocs/DEFERRED.md` `[OPEN]` 不阻塞阶段 1 编码，但首次接入 Linux 环境须优先补跑且绝不以宿主机测试核销。

## 硬约束与明确不做

- **硬性守恒**：保持 `LANC` 魔数/报文结构、`JSONL` 历史格式、Windows 行为不回归；Electron 主进程绝不新建局域网 UDP/TCP Socket（仅阶段 4 更新可用 HTTPS），渲染进程保持严格 `default-src 'none'` CSP；GUI 与 CLI 共用 `LinuxProfile` 路径与文件锁；全部手写源码/测试文件 `< 1000` 行预警（1500 行硬上限），全中文注释遵守 `.claude/Rules.md` 第 7 条。
- **明确不做**：
  1. 不做 Windows 主窗已无可见入口的“新建节点（14 种形态调色盘）”空壳窗，不在聊天输入工具栏强加旧版节点弹窗按钮；
  2. 不做 R2 C 类 4 项假功能造轮子：不做单边三语国际化词典（`语言`）、不做破坏纯内网与 CSP 的“内置极速网页预览”（`链接打开方式`）、不在 `NetworkService.Transfer.cs` 添加降低吞吐的 `fs.Flush(true)`（`64KB 分块传输校验` 改为只读能力展示）、不另造复杂协议通道切换逻辑（`网络协议通道` 仅展示绑定网卡/端口或触发 `refresh`）；
  3. 不承诺非 root 静默覆盖安装 `.deb`（统一退锁后复制安装命令引导）；
  4. 未收到用户发布指令前，不修改发布版本号、不热替换安装、不发布 Release。

## 实施顺序与未核实门槛

`阶段 0（基线冻结与测试拆分 + CP-0）→ 阶段 1（托盘/通知/关闭恢复 + A 类开关 + CP-1）→ 阶段 2A（11 色 Bug 修复、probe-ip、LC-FR-007、CLI 历史、群聊 1～16 人多选、解除串行发文件、496×548 表情面板、214/188px 工具栏外壳、Ctrl+F/Ctrl+L）→ 阶段 3（B 类 4 项双端同键接线）→ 阶段 4（Linux 更新检查、嵌套 SHA256SUMS.json 校验与 0o700 独占下载）→ 阶段 2B（受 CP-2B 门禁的内联表情富文本专项）→ 阶段 5（双架构构建与物理实机/真网总验）`。

未核实清单（`CP-0`、`CP-1`、`CP-2B`、阶段 5 物理双架构及真实局域网联测）均留待实际 Linux 与双机环境取证核销，严禁以宿主机静态/桩回归替代。
