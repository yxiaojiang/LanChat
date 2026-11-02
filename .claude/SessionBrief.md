# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-11-02。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线 Tag `v2.1.0`，当前开发版本 `v2.0.6`）。
> 核心里程碑：**GrokBot 1:1**；**本步已完成：1) Windows 左上角搜索栏与搜索按钮优化（移除加号按钮，搜索按钮放大 2 倍至 46px，整行按 7:1 比例自适应撑满）；2) 左下角移除蓝色向上箭头按钮；3) 偏好与配置集成至左下角 mi 图标（菜单首项与右键直达）；4) 主界面无边框窗口支持四周与四角鼠标平滑拖拽缩放（FormResizeMessageFilter 全局消息捕获 + 原生 WM_SYSCOMMAND SC_SIZE）；5) 聊天记录支持鼠标选中文本高亮、IBeam 光标、双击全选、Ctrl+C 与右键深色圆角菜单复制；6) 全自动构建发布 v2.0.6 MSI 与 Linux deb 包并推送至远程**。

---

## 一、本轮工作态（UI 深度精简、窗口缩放与聊天划选复制升级）

### 1. 用户反馈与需求拆解
1. **左上角搜索栏与增加按钮**：去除增加按钮（`_btnAddSession`），搜索按钮放大 2 倍，搜索栏和搜索按钮占据整行并保持和谐比例。
2. **左下角蓝色向上箭头**：彻底去除 `_btnBottomScan` 按钮。
3. **偏好与配置功能**：加到左下角的 `mi` 图标中。
4. **主界面窗口鼠标缩放**：解决无边框窗口无法通过鼠标在边缘拖拉缩放大小的问题。
5. **聊天内容鼠标划选复制**：聊天内容支持鼠标拖拽选中文字并高亮，支持复制（Ctrl+C 及右键菜单）。

---

### 2. 核心架构设计与落地细节

#### (1) 搜索栏与按钮横向整行排版
- 彻底移除 `_btnAddSession` 相关控件与事件绑定。
- 搜索输入框容器 `_searchBoxContainer` 设为常驻可见，圆角半径 21px 胶囊形态。
- 搜索按钮 `_btnSearchToggle` 尺寸从 34px 增大至 46px（内部矢量放大镜同步加粗并放大 2 倍）。
- 在自适应响应式布局中，按 `Width - 16(左边距) - 16(右边距) - 46(按钮) - 10(间距)` 计算搜索框宽度（约 332px），二者按 ~7:1 比例撑满整行，垂直居中。

#### (2) 底部控制栏极致精简
- 彻底移除 `_btnBottomScan` 声明、初始化与事件。
- 底部控制栏保留左侧 54px 圆形 `mi` 用户头像按钮，视线简洁通透。

#### (3) 偏好与配置集成至左下角 mi 图标
- `GrokSettingsMenuForm` 构造函数新增 `Action onShowSettings` 参数，菜单项增至 7 项。
- 第 0 项增加「偏好与配置」（图标 `WeChatIconType.Settings`，副文本 `打开  ›`），点击直接打开 `GrokSettingsDialog(0)`。
- 为 `_btnBottomUser` 绑定右键事件 `MouseUp`，右键点击直接呼出全局设置窗口，操作直达。

#### (4) 全局无边框边缘拖拉缩放架构 (`FormResizeMessageFilter`)
- **根因分析**：由于 `MainForm` 内部被各种子 Panel 填满（Dock=Left, Fill, Bottom），鼠标位于窗口边缘时事件直接被子控件捕获并返回 `HTCLIENT`，导致 `Form.WndProc` 无法捕获边缘 `WM_NCHITTEST`。
- **解决方案**：实现 `IMessageFilter`，通过 `Application.AddMessageFilter` 在应用消息泵层拦截 `WM_MOUSEMOVE`、`WM_LBUTTONDOWN` 和 `WM_SETCURSOR`：
  - 动态计算光标距离窗体四周 8px 边缘的相对位置；
  - 根据边缘方位（左、右、上、下、四角）切换 `Cursors.SizeWE/SizeNS/SizeNWSE/SizeNESW`；
  - 鼠标左键按下时调用 Win32 原生 `ReleaseCapture()` + `SendMessage(hWnd, WM_SYSCOMMAND, 0xF000 + dir, 0)`，进入系统原生高效平滑缩放循环。

#### (5) 自绘聊天流鼠标划选高亮与复制机制 (`WeChatChatView`)
- **视觉分行引擎 (`BreakTextIntoVisualLines`)**：将消息内容按换行符与可用宽度拆分为带全局字符偏移量与精确包围盒的 `VisualLine` 列表。
- **字符级命中测试 (`GetCharIndexAtPoint`)**：根据鼠标坐标计算所在行并度量子字符串精确映射至消息文本的字符索引。
- **交互与高亮**：
  - 鼠标悬停文字气泡显示 `IBeam` 光标；
  - 鼠标左键按住拖拽划选，底层绘制柔和半透明选区背景色（`#AA4673EB`），文字无错位叠加；
  - 双击选中文中全部文字；
  - 拦截 `Ctrl+C` 与 `Ctrl+A` 快捷键，复制选中文本或全选；
  - 右键点击弹出 `GrokPopupMenu`，提供「复制 (Ctrl+C)」、「复制全文」、「全选 (Ctrl+A)」，点击即刻复制到剪贴板。

---

## 二、关键约束与设计原则

1. C# 5.0 / .NET Framework 4.0，系统原生 `csc.exe` 极速直编，零第三方重型依赖。
2. 视觉规范：100% 遵守 `Docs/GrokBot_UI_Design_System.md` 与 `.claude/Rules.md` 9 大硬约束。
3. 网络契约：UDP 38888 / TCP 38889+ / 魔数 `LANC` 向后兼容。

---

## 三、增量模块索引

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `src/MainForm.cs` | 搜索栏整行排版、移除加号与蓝色箭头、挂载全局边缘调整大小过滤器 `FormResizeMessageFilter` |
| `src/MainForm.Chat.cs` | 移除 `BtnAddSession_Click`，群聊专属置顶卡片与置顶角标保持 |
| `src/MainForm.Tray.cs` | `mi` 头像菜单绑定 `ShowSettingsDialog(0)` |
| `src/UI/Controls/GrokSettingsMenuForm.cs` | 新增「偏好与配置」菜单项，扩展为 7 项 |
| `src/UI/Controls/WeChatChatView.cs` | 重构自绘流式视图：支持分行度量、鼠标划选高亮、双击全选、快捷键复制与右键弹出菜单复制 |
| `src/UI/Renderers/WeChatIcon.cs` | 新增 `WeChatIconType.Copy` 矢量双矩形复制图标，优化放大版搜索图标 |
| `release/` | 自动生成 `LanChat.exe`、`LanChat.msi`、`LanChat.deb` (v2.0.6) |
