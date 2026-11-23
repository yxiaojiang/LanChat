# 代码审核报告 (Code Review) — LC-LINUX-002 第 3 步切片 1 (Round 3 复核)

- **任务编号**：`LC-LINUX-002-STEP-3-SLICE-1`
- **任务主题**：Linux Electron 桌面重构 — 第 3 步切片 1（主窗客户区对齐：双栏、会话、聊天、文件卡片、暗色菜单与浮动滑块）
- **审核方**：独立审核方
- **审核日期**：2026-11-23
- **审核基线**：
  - `.claude/Rules.md`（第 1、4、6、7、8、9、10 条硬约束）
  - `.claude/SessionBrief.md`、`.claude/Plan.md`、`PlanDocs/LC-LINUX-002/INTEGRATION.md`
  - Round 2 审核意见（原 `CRDocs/REVIEW.md`）及本轮整改代码
  - `Docs/GrokBot_UI_Design_System.md` 与 WinForms 基线（`MainForm.Chat.cs`、`WeChatChatView.cs`、`SessionPresenter.cs`、`GrokPopupMenu.cs`）
- **审核结论**：**`APPROVED`**
- **验证边界**：本轮代码审核放行。宿主机无 QEMU，新 ELF 的 `delete` / `history` 仍未实跑；该缺口保留在集成记录中，不构成新的代码打回项。第 3 步产品验收（图形后端、输入法、缩放叠图、长历史、设置八页、托盘通知）仍按 `INTEGRATION.md` 保持未通过。

---

## 一、Round 2 问题闭环

| Round 2 编号 | 问题概要 | Round 3 复核状态 | 说明 |
| :--- | :--- | :--- | :--- |
| **R2-P0-1** | 输入框键盘被滑块劫持，满高后增删不刷新滑块 | **已闭环** | 可编辑控件不再注册 `keydown`；非输入视图跳过 `shiftKey`、`isComposing`、`keyCode === 229` 以及 `alt`/`ctrl`/`meta`。`input` 与 `bind` 返回的 `refresh` 都会调度重绘。`.compose textarea` 为 `padding: 0 10px 0 0`，字号、行高与 `#F2F2F6` 继承未改，原生滚动条仍由 `.scroll-view` 隐藏 |
| **R2-P0-2** | `delete_session_regression` 吞帧，文档仍写 SDK 9 | **代码与口径已闭环；ELF 实跑仍未执行** | `call_collect()` 在同一次读取中保留响应前的事件帧。`DeleteChat` 在 `_gate` 内同步 `RemovePeer` → `OnPeerLeft` → `Publish("peers")`，然后才 `Reply`。文档已改为 SDK `10.0.404`，并写明未在 Linux 命名空间实跑 |
| **R2-P1-1** | `suppressAttachToggle` 在 `pointerdown` 后 0ms 被清掉 | **已闭环** | 按下时只置位并监听 `pointerup` / `pointercancel` / `blur`；抬起后再用 `setTimeout(0)` 清标志。`openMenu` / `openEmoji` 入口会先清零并解除监听 |
| **R2-P2** | 末尾才滚底、单气泡 `Ctrl+A`、右内边距、滑块与 `+` 按钮单测 | **已闭环** | `Ctrl+Enter` 仅在选区终点等于全文长度时滚底。`.bubble` 为 `tabIndex = -1`，`#messages` 在焦点或选区位于气泡内时执行单气泡全选。UI 回归已覆盖 textarea 放行、`input` 刷新、非输入视图 `Shift`/IME 放行，以及 70ms 按压后 `pointerup` 仍可切换关闭、拖离松手后标志复位 |

未发现新的安全边界回退。`main.cjs` 仍只打开已登记绝对路径，缺失文件仅回退到直接父目录；群聊拖放仍使用一次性 token。`delete-session` 的 `removePeer` 仍要求布尔值。本轮没有新增依赖，也没有改 LANC 封包布局。

---

## 二、关键核对

### 1. 输入框与浮动滑块

`scrollbar.js` 对 `TEXTAREA`、`INPUT`、`isContentEditable` 不绑定滚动快捷键；`options.keyboard === false` 同样不绑定。非输入视图在修饰键或输入法组合期间直接返回，避免 `preventDefault` 吃掉选区扩展和 `Ctrl+A`。

满高后高度不再变化，`MutationObserver` 看不到 textarea 的 value 变化。现在 `input` 会 `schedule()`，`syncComposeHeight()` 也会调用返回的 `refresh`。同一帧内重复调度会被 `requestAnimationFrame` 合并。

输入胶囊仍是 11pt、行高 23px、继承 `#F2F2F6`。右侧 10px 内边距让 4px/5px 滑块留在字外。`.scroll-view` 继续 `scrollbar-width: none`，WebKit 滚动条宽度为 0。

### 2. 删除会话事件计数

`Service.call()` 仍会丢弃响应前的帧，计数路径已经改走 `call_collect()`。`DesktopService` 的删除分支本身不再额外 `Publish("peers")`；在线好友只经由 `OnPeerLeft` 入队一次，且该写入发生在带 `id` 的响应之前。通道是单读者 FIFO，因此这次响应之前的 `peers` 帧会被收进 `events`。

断言与源码一致：

- 删除全员群聊返回「不能删除全员群聊」。
- `removePeer=false` 清空群聊：删历史，不调用 `RemovePeer`，`peers` 为 0。
- 删除 `LAN-127.0.0.2` 的在线节点：回复列表中不再有 `online-peer`，测量窗口内 `peers` 为 1。
- 删除不在线列表中的 `LAN-127.0.0.99`：`DeleteSession` 对缺席键只是移除空记录，不抛错，也不离场，`peers` 为 0。

`ACK|id|nick|host|port|ip` 与 `ProcessUdpPacket` 的五段以上解析一致，地址键为 `LAN-127.0.0.2`。

### 3. `+` 按钮二次点击

人工点击的顺序是 `pointerdown` →（按住）→ `pointerup` → `click`，`click` 与 `pointerup` 在同一次输入任务里同步派发，`setTimeout(0)` 要等这次任务结束。因此 50–150ms 的按住时间里标志保持为真，`renderer.js` 里 `consumeAttachToggle()` 能吃掉随后的 `click`。拖离按钮松手不会产生 `click`，抬起后的 0ms 定时器会把标志清掉，下一次点击仍能打开菜单。`blur` 会立即清标志，避免窗口失焦后吞掉以后的点击。

### 4. 气泡 `Ctrl+A` 与 WinForms

WinForms `WeChatChatView.OnKeyDown` 在聊天视图上处理 `Ctrl+A`，菜单文案同样是「全选 / Ctrl+A」，图标类型为 User。Linux 侧保留该 hint 和 `User` 图标，并把全选范围限制为当前 `.bubble`。焦点或选区不在气泡内时不拦截，输入框不在 `#messages` 内，组合键不会被聊天区抢走。`Ctrl+Enter` 仍先判断终点再插入换行，中间位置不再强制滚到底。

### 5. 行数与依赖

手写桌面源码均低于 1000 行预警线。当前最长文件是 `src/linux/desktop/renderer.js`，**792 行**（上一轮 775 行）。`overlay.js` 284 行，`scrollbar.js` 87 行，`desktop.css` 154 行，`DesktopService.cs` 407 行，`tests/linux_desktop_regression.py` 410 行。没有新的第三方包。

---

## 三、本轮实测

- `"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js` → `linux desktop ui regression: ok`。
- `python -m py_compile packaging/build-electron.py packaging/build_linux.py tests/linux_desktop_regression.py` → 通过。
- 本轮未改 `DesktopService` 的删除语义，也未改封包。Round 2 已用 `bin/lc-linux-002/cache/dotnet`（SDK 10.0.404）对当前服务工程做过 `linux-x64` 与 `linux-arm64` 零警告编译。本轮不再重复发布 ELF。
- 未运行 `tests/linux_desktop_regression.py`。当前 Windows 宿主机没有 QEMU，不能在 Linux 网络命名空间加载这些 ELF。

---

## 四、为何不因缺 ELF 实跑而挂起

Round 2 把该项列为 P0，是因为当时的测试**在任何成功实现上都会失败**：`call()` 先读到并丢掉 `peers`，后面的队列计数必然为 0。该必败点已经消失。

现有证据足够放行本轮代码审核：

1. 删除路径的 `peers` 与响应在同一把锁内按固定顺序入队，不依赖实机时序才能判断「恰好一次」。
2. 文档把「双架构已编译」和「delete/history 未实跑」分开写了，没有把未执行用例记成通过。
3. 规则第 8 条仍要求以后在 Linux 上实跑；它现在约束的是集成证据，不是一段已知写错的断言。`Plan.md` 与 `INTEGRATION.md` 里的第 3 步产品验收本来就还没关闭。

因此 Round 3 **不挂起、不要求为了本轮审核再等 VM**。下次有 Linux 或 QEMU 时，应补跑：

`python tests/linux_desktop_regression.py <elf> delete history`

并把结果补进 `INTEGRATION.md`。若实跑失败，再单开缺陷。

---

## 五、不阻塞放行的剩余说明

1. **发现线程与计数窗口**。`history` 屏障只能排空「读到该次响应为止」已经在队列里的帧。`OnPeerJoined` / `OnPeerUpdated` 在后台线程拿 `_gate`，若在屏障响应入队之后才 `Publish("peers")`，下一轮 `call_collect` 会把这次发现刷新算进去。本用例的 ACK 和文件接收都在测量窗口之前，隔离回环里随后没有新的对端流量，心跳淘汰要 25 秒。这是实跑时的低概率波动，不是删除分支会发两次 `peers`。
2. **中间换行的视口**。`syncComposeHeight()` 仍先把高度压到 23px。光标不在末尾时不再强制滚底，收缩可能把 `scrollTop` 夹到顶部。末尾换行仍会滚底。这是 P2 建议落地后的剩余体验，不影响放行。
3. **空白处 `Ctrl+A`**。焦点和选区都不在 `.bubble` 上时，快捷键继续交给浏览器。单气泡路径已经接上。
4. **规则第 7 条**。`scrollbar.js` 跳过可编辑控件、`overlay.js` 把清标志推迟到 `pointerup` 之后，这两处时序没有内联中文注释。行为已由单测钉住，不要求为注释再开一轮。

---

## 六、审核结论

**`APPROVED`**

Round 2 的两项 P0 和一项 P1 均已按最小改动闭环：输入框不再被滑块抢走光标键，满高后会刷新滑块，`+` 按钮在正常按压时长下可以切换关闭，删除会话测试不再丢弃响应前的 `peers` 帧。P2 项已落入实现和 UI 回归。安全白名单、拖放 token 和单文件行数没有回退。

本轮代码审核放行。ELF 上的 `delete` / `history` 实跑仍是后续集成证据，不阻塞这次放行；第 3 步整体界面验收也不因本结论改为通过。
