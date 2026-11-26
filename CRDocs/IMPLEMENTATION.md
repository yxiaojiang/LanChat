# LC-LINUX-003 阶段 2B — 最近 3 轮实施记录

阶段 4 的完整历史已归档至 [`archive/2026-11-26_LC-LINUX-003_阶段4在线更新审核/IMPLEMENTATION.md`](archive/2026-11-26_LC-LINUX-003_阶段4在线更新审核/IMPLEMENTATION.md)。

## 阶段 5.1 候选产物构建与静态核验（2026-11-26）

- **命令**：`python Tools/Scripts/BuildLinuxDeb.py`。
- **结果**：成功使用 `bin/lc-linux-002/cache/dotnet/dotnet.exe` 提供的 .NET 10 SDK，完成 `linux-x64` 与 `linux-arm64` 自包含 ELF 构建、项目缓存 Electron 归档 SHA-256 校验，以及 2.0.26 双架构 `.deb`、portable、CLI 和 `SHA256SUMS.json` 生成。
- **摘要核验**：独立重算 `SHA256SUMS.json` 中当前 6 个 2.0.26 artifact，amd64/arm64 的 deb、portable、CLI 全部匹配。
- **ELF 核验**：从 `release/linux-electron` 的两个 CLI、两个 portable 包和两个 deb 的内嵌 `lanchat-bin` 读取 ELF 头；amd64 均为 ELF64 `e_machine=62`，arm64 均为 ELF64 `e_machine=183`。
- **边界**：构建在 Windows 宿主机完成，不代表 Linux 安装、启动或运行期验证；`release/linux-electron` 中既有 2.0.25 历史资产未删除，当前摘要只覆盖 2.0.26 产物。未执行 `Publish.bat`、安装覆盖或 `git push`。

## 阶段 5 Linux 实机与真实 LAN 总验收入口

现场执行顺序与逐项核销清单已写入 [`DEFERRED.md`](DEFERRED.md#阶段-5-linux-实机与真实-lan-总验收清单准备版)，包含 CP-0、CP-1、CP-2B、`LC-LINUX-003-S4-ENV-001`、双机局域网、切网/休眠/TUN、多 DPI/IME、升级回退及证据留存要求。

## Round 1 执行者整改交接

- **日期/状态**：2026-11-26，`WAITING_FOR_GROK`；仅交回阶段 2B 独立复审，不宣称 `APPROVED`。
- **P0-1**：`main.cjs` 新增无参 `read-clipboard-text` 与 `stage-clipboard` IPC，主进程分别直接调用 `clipboard.readText()` 和 `stagePastedFiles([], null, true)`；`preload.cjs` 移除 `stagePaste(paths, ...)` 暴露，`renderer.js` 右键粘贴/粘贴附件改走受控 IPC，渲染权限拒绝不再阻断该链路。真实受信 `paste` 事件的隔离层内部路径转交保持不变。
- **P1-1**：`composer-box.js` 与 `preload.cjs` 同步实现文本树序列化和偏移遍历：叶子文本节点只累加自身长度，`<br>` 计一个换行，块级子容器在必要边界补一个换行，不再递归结束后重复累加容器全文；`onMouseDown` 直接使用修正后的 token 偏移。
- **P1-2**：`composer-box.js` 初始化显隐遵循 `rich.hidden = !useRich; native.hidden = useRich`；统一 `isDisabled` 同步原生 disabled、富文本 inert/contentEditable/dataset，双轨同步 `dataset.draftChat`，显式 `setRangeText` 起止参数生效；`preload.cjs` 粘贴入口同步检查统一禁用状态。
- **P2-1**：补齐 `.composer-rich` 主题色 caret，并清理无用 `before` 局部变量。
- **回归补充**：`tests/linux_desktop_composer_regression.js` 增加持久化恢复、离线切换不解锁、嵌套 `<div>`/`<br>` 多行偏移、显式范围替换、无带 `paths` 的 `stagePaste` 暴露和受控剪贴板 IPC 静态断言。

## 本轮宿主机验证

- `cmd /c "Tools\\Scripts\\Compile.bat < NUL"`：通过；按要求恢复 `release/LanChat.exe` 与 `release/uninstall.exe`。
- `tests/chat_composer_regression.ps1 -ExpectLocalPack`、`tests/friend_refresh_regression.ps1`、`tests/file_transfer_stability_regression.ps1`：全部通过。
- 24 个 Linux JS/CJS 文件 `node --check`：通过；`linux_desktop_ipc_regression.js`、`linux_desktop_lifecycle_regression.js`、`linux_desktop_sync2a_regression.js`、`linux_desktop_settings_regression.js`、`linux_desktop_update_regression.js`、`linux_desktop_composer_regression.js`、`linux_desktop_ui_regression.js`：全部通过。
- `py tests/test_linux_flow.py`：22 项通过；`py -m py_compile`：14 个 Python 文件通过。
- 行数：`composer-box.js` 360、专项回归 191、`linux_desktop_ui_regression.js` 696、`preload.cjs` 234、`renderer.js` 816、`desktop.css` 201、`index.html` 99，满足本轮门禁；`git diff --check` 通过。

## 未验证边界

`tests/linux_desktop_regression.py` 需要显式 Linux ELF 路径，本次 Windows 宿主机未执行；真实 Linux Electron、Xorg/Wayland/XWayland、`fcitx5`/`ibus`、系统剪贴板、多 DPI、Linux/ELF、物理 LAN 和升级回退仍未验证。`LC-LINUX-003-CP-2B` 继续保留在 `CRDocs/DEFERRED.md` 为 `[OPEN]`，不得用本轮 Windows 桩或 Node 回归核销。

## Round 1 实施交接

- `src/linux/desktop/preload.cjs`：抽离通用粘贴适配器，统一处理 textarea/contenteditable 文本读取、DOM/原生选区、最大长度裁剪、范围插入和 `input` 事件；文件/图片仍沿用既有 `stage-paste` IPC，并接入富文本目标。
- `src/linux/desktop/composer-box.js`：新增 282 行高内聚编辑器，原生 textarea 默认安全回退；显式启用富文本后渲染 PNG 表情、对齐 1.25em 基线，点击表情将光标收拢到左右边界，Backspace/Delete 整枚删除，copy/cut 输出 `[表情名]`，composition/229 保护中文 IME，提供暗色右键粘贴/粘贴附件/启用或回退菜单回调。
- `index.html`、`desktop.css`、`renderer.js`：保留原生输入框并增加隐藏 contenteditable 双轨，接入表情资源、草稿读写、滚动条、右键菜单和附件粘贴；富文本模式仅在用户显式切换并持久化开关为启用时使用。
- `tests/linux_desktop_composer_regression.js`：133 行，覆盖令牌还原、光标收拢、原子删除、CRLF/最大长度粘贴、IME 保护、copy/cut、回退开关及 CP-2B 延期声明；已注册进 `tests/linux_desktop_ui_regression.js`。

## 宿主机验证与证据边界

- 已通过：`cmd /c "Tools\\Scripts\\Compile.bat < NUL"`；`friend_refresh_regression.ps1`、`chat_composer_regression.ps1 -ExpectLocalPack`、`file_transfer_stability_regression.ps1`；23 个 Linux JS/CJS `--check`；专项 composer 回归与完整 `linux_desktop_ui_regression.js`；`tests/test_linux_flow.py` 22 项；`tests/linux_desktop_regression.py` 与导入脚本 Python 语法；Linux 共享后端 `dotnet build`（0 警告、0 错误）；行数门禁与 `git diff --check`。
- 行数结果：`composer-box.js` 282 行、专项回归 125 行、`linux_desktop_ui_regression.js` 696 行、`settings.js` 968 行；全部满足 `<1000` 行以及 360/260/700 专项门禁。Compile 产生的 `release/*.exe` 已恢复，未执行发布、安装覆盖或推送。
- 未验证：真实 Linux Electron、Xorg/Wayland/XWayland、真实系统 PNG/JPEG 剪贴板、`fcitx5`/`ibus` 中文预编辑、多 DPI 目测和双机 LAN；这些不由 Windows DOM/IPC 桩核销。
