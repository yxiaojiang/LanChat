# Windows.md — Windows 客户端模块上下文

## 模块定位与职责
Windows 桌面客户端，采用原生 C# WinForms 开发，定位为**零安装、无外部依赖、支持 MSI 标准向导安装与绿色单文件执行**的现代化桌面应用。
`feature/wechat-ui` 已合入 `main`，稳定标签 `Lanchat-v1.5` 对应 `69b5310`。当前在 `main` 打磨局域网客户端，互联网方向由独立分支承接。界面遵循 `.claude/Rules.md` 第 9 条及 `Docs/GrokBot_UI_Design_System.md`。

## 核心源码与构建物清单 (规范目录)
- `src/Program.cs`：应用程序入口，具名 Mutex（`Global\LanChat_SingleInstance_Mutex_9F24FEA2`）单实例互斥，广播消息唤醒已运行实例；开启高 DPI 感知（`SetProcessDPIAware`），设置 `SetCurrentProcessExplicitAppUserModelID`，注册全局未捕获异常兜底处理。
- `src/MainForm.cs`：主窗体核心界面控制与自适应布局协调。包含无边框窗体与 DWM 阴影绘制、自适应响应式布局引擎（`UpdateResponsiveLayout`）、顶部 96px 大字号/大头像对端胶囊栏、静态个人头像、底部输入区组装、Win32 单实例唤醒以及 `_sessionsPanel`、`_chatPanel`、`_chatHeader` 全链路 `DoubleBufferedPanel` 离屏防穿透。
- `src/MainForm.Chat.cs`：会话管理与聊天交互。包含好友状态监听、自适应会话卡片自绘（`ListSessions_DrawItem`，72px 卡片高度、魅紫高饱和选中态 `#7946D7`、有机吉祥物与微信红未读徽标 `DrawUnreadBadge`）、右键删除二次确认弹窗、文本消息收发、输入区工具栏 6 联快捷操作、`GrokPopupMenu` 悬浮菜单弹出。
- `src/MainForm.Files.cs`：文件收发与分块跟踪。包含文件选择、拖拽监听、流式进度更新、文件接收落盘与打开文件/目录调用。
- `src/MainForm.Tray.cs`：系统托盘与生命周期。包含底层网络启动绑定、自适应 Hero 个人头像自绘（`BtnAvatar_Paint`）、昵称修改向导、托盘图标常驻、关闭拦截防误退、`GrokPopupMenu` 暗黑托盘菜单、Win32 `FlashWindowEx` 任务栏黄色高亮、450ms 托盘图标闪烁提醒与在线更新代理。
- `src/NetworkService.cs`：网络通信门面，主类定义与生命周期管理。
- `src/NetworkService.Interface.cs`：物理网卡检测与 TUN 虚拟网卡穿透过滤。
- `src/NetworkService.Discovery.cs`：多物理网卡 UDP 广播与并发子网单播探测，开放 `RemovePeer` 节点主动移除。
- `src/NetworkService.Transfer.cs`：LANC 二进制协议解包与 64KB 流式传输引擎。
- `src/UI/Controls/GrokPopupMenu.cs`：通用圆角浮动弹出菜单（16px圆角、44px舒适行高、8px悬停卡片、纯 GDI+ 矢量图标、全自动边界检测）。
- `src/UI/Controls/GrokTrayPreviewPopup.cs`：GrokBot 风格托盘未读悬浮预览卡片（无焦点顶层窗体、多联系人列表、未读徽标、消息摘要、时间、工作区边界夹紧与跳转会话）。
- `src/UI/Controls/GrokSettingsMenuForm.cs`：Grok 原生设置弹出菜单（332px 紧凑高度、16px圆角、44px舒适行高、纯 GDI+ 矢量图标）。
- `src/UI/Controls/GrokSettingsDialog.cs`：WorkBuddy 风格高级设置中心（1260×820 宽广视界，右上角自绘 28×28 标准关闭按钮，纯自绘暗灰极窄滑块；DIR-005 重构中将 8 个 Tab 面板组件化抽离至 `src/UI/Controls/SettingsPanels/`，主对话框收敛为纯轻量导航路由容器）。
- `src/UI/Controls/DoubleBufferedPanel.cs`：公共双缓冲离屏绘制面板容器（支持 `AllPaintingInWmPaint` 与 `OptimizedDoubleBuffer`，彻底消除瀑布流刷新）。
- `src/UI/Controls/SettingsPanels/`：设置子面板集合（`SettingsPanelBase`、`GeneralSettingsPanel`、`ProfileSettingsPanel`、`AppearanceSettingsPanel`、`ShortcutsSettingsPanel`、`FileTransferSettingsPanel`、`ChatHistorySettingsPanel`、`NetworkDeviceSettingsPanel`、`AboutSettingsPanel` 与 `SettingsContext`）。
- `src/UI/Controls/GrokConnectNodeForm.cs`：连接节点窗口（顶部搜索胶囊、过滤芯片、局域网在线节点推荐卡片流与手动指定 IP 直连握手）。
- `src/UI/Controls/GrokCreateNodeForm.cs`：创建新节点窗口（居中动态吉祥物预览、起名胶囊输入框、14 种有机形态网格、11 色调色盘）。
- `src/UI/Controls/GrokAboutForm.cs`：关于窗口（居中 Squircle 吉祥物徽章、版本信息与“复制版本信息”圆角药丸按钮）。
- `src/UI/Controls/GrokChangeNickForm.cs`：暗黑昵称修改窗口（吉祥物微标、暗黑圆角输入框与紫色保存按钮）。
- `src/UI/Controls/GrokFeedbackForm.cs`：发送反馈窗口（多行反馈输入框、网络诊断复选框与发送反馈按钮）。
- `src/UI/Controls/GrokUpdateForm.cs`：软件更新窗口（版本对比横幅、更新日志与平滑进度条）。
- `src/UI/Controls/GrokMessageBox.cs`：暗黑提示框（彻底取代系统生硬白底 MessageBox），使用 TextRenderer.MeasureText 静态度量并动态绑定 Region，杜绝半边裁切。
- `src/UI/Controls/SessionListBox.cs`：自绘双缓冲 ListBox，彻底剥离 Win32 `WS_VSCROLL`，拦截 Win32 `WM_ERASEBKGND` 与 `WM_REFLECT | WM_DRAWITEM`，实现双行脏矩形精准局部刷新与零重绘切页，4px 宽（悬停 5px）优雅半透明暗灰自绘滑块。
- `src/UI/Renderers/WeChatIcon.cs`：纯 GDI+ 矢量图标与 Grok 吉祥物绘制引擎（1.8f 线宽、圆头圆角：Emoji 表情、Box 收藏抽屉盒、Folder 文件、Scissors 剪刀、Mic 麦克风、Call 电话手柄、吉祥物头像、WinMin/WinMax/WinClose 等）。
- `src/UI/Controls/WeChatButtons.cs`：无边框图标按钮（`WeChatIconButton`），扩展支持圆形按钮（`IsCircle`，34x34 悬停微光）、右上角 56x64 矩形标题栏控制按钮、自定义填充色与窗口关闭高亮模式。
- `src/UI/Controls/WeChatChatView.cs`：Grok 风格自适应居中流式聊天视图控件，彻底移除 Win32 原生 `VScrollBar`，全量内置虚拟行滚动模型与纯 GDI+ 浮动暗灰滑块；紫色气泡 Padding 缩减 50%（水平 24px/垂直 18px），单行高度自适应紧凑胶囊，文件卡片按键行距扩大至 13px 消除拥挤感。
- `src/Services/UpdateService.cs`：GitHub Releases 在线自动更新服务。
- `src/Services/AppSettings.cs`：全局配置持久化服务（%AppData%\LanChat\settings.json 漫游与便携模式）。
- `src/Services/SessionManager.cs`：会话与消息历史纯领域解耦服务，支持 `DeleteSession` 物理删除磁盘历史与内存清理。
- `packaging/bump-version.py`：全工程版本号自动自增与原子同步脚本。
- `Docs/GrokBot_UI_Design_System.md`：完整 GrokBot UI 设计规范与视觉对齐标准文档。
- `Tools/Scripts/Compile.bat`：原生纯编译脚本，纯 ASCII 0-BOM，秒级生成 `release\LanChat.exe` 与 `release\uninstall.exe`。
- `Tools/Scripts/CompileAndLink.bat`：一键编译链接并热替换覆盖至 `D:\soft\LanChat\`。
- `Tools/Scripts/Publish.bat`：全自动版本自增、强制编译、WiX 5 打包与带网络重试的 GitHub Releases 发布脚本。

## 关键设计点与演进状态
### v2.0.18 文件传输界面与剪贴板

- `FileTransferController.TransferMessageChanged` 按每个文件更新聊天记录里的进行中气泡；多文件分别显示百分比，成功后同一气泡变为文件卡片，失败/断线保留终态。`SessionManager.PersistExistingMessage` 只持久化确认完成的记录，更新现有记录不重复追加。
- `ChatFileCardRenderer` 负责聊天气泡内文件信息与进度绘制；主窗顶栏不再放传输总进度条。
- `ChatInputBox` 在 Ctrl+V、Shift+Insert 与 WM_PASTE 中识别系统 `FileDrop`；`MainForm.Files.cs` 按当前会话暂存附件，可移除，点击发送/Enter 才真正传输。普通文本仍交给 TextBox 原生粘贴。
- 原生构建和隔离同名文件并发回环、UI/附件草稿回归及 MSI 内容核验已通过。用户尚未执行实际 MSI 覆盖安装或桌面剪贴板验收。

1. **暗黑设计系统规范 (Rule 9 硬约束 - Zero White Scrollbar 契约)**：
   - 界面严格遵循暗黑纵深：主聊天区 `#121212`、工具栏底色 `#121212`、按钮与卡片底色 `#1C1C20`（悬停 `#2E2E34`）、选中高饱和魅紫 `#7946D7`、主色调 Grok 魅紫 `#8E64DC`；
   - **绝对禁止全系统原生 Win32 白底滚动条**：无论主界面会话栏、聊天流还是弹窗设置，全量使用纯 GDI+ 暗灰浮动极窄滑块，禁止出现原生白框破坏暗黑沉浸感；
   - 文本与排版矩阵：主输入文字 `11.0pt (15px)` `#F2F2F6`（95% 明度）；副文本与提示词 `9.5pt` `#8E8E93`（57% 明度）；设置项与按钮 `10.0pt ~ 10.5pt` Bold。
2. **会话切换 0ms 零闪烁与硬件离屏合成**：
   - 拦截 Win32 原生 ListBox 的无缓冲直绘与背景擦除，重写 `OnSelectedIndexChanged` 仅刷新新旧两行脏矩形，全列表其余项零重绘；
   - 主界面全链路容器升级为 `DoubleBufferedPanel` 结合 `WS_CLIPCHILDREN`，彻底消灭瀑布流扫描刷新与白闪。

## 多显示器窗口与任务栏（2026-11-16）

- 主窗体保留原生缩放和最大化样式以使用系统贴靠；标题栏拖动消息携带鼠标屏幕坐标。Windows 默认负责多屏 `MINMAXINFO` 计算，不要把外接屏尺寸直接写入以主屏为基准的最大化字段。
- `WM_NCCALCSIZE` 隐藏原生边框，最大化客户区按目标显示器工作区裁切；窗口区域也按工作区裁切，避免非客户区遮挡任务栏。还原时移除窗口区域，句柄重建时重置缓存；坐标使用相同 DPI 感知上下文。
- 两块不同缩放屏幕上，隔离测试验证了最大化客户区、窗口区域、按钮、任务栏命中及还原；自动隐藏任务栏和真实鼠标贴靠尚需手工验证。系统 DPI 感知不等于每显示器 DPI 感知。

## 局域网历史与外观（2026-11-18）

- `SessionManager` 以局域网 IPv4 键分组，并合并地址已确认的旧记录。历史文件保持原 JSONL 来源。在线列表用当前节点路由，离线列表用档案昵称，地址证据不足的记录继续单独可查。
- `AppearanceSettingsPanel` 通过设置宿主即时刷新主窗口，`GrokTheme` 定义暗色层级、强调色与主界面字号增量，`WeChatChatView` 独立使用正文设置。所有选项由 `AppSettings` 持久化。
- 历史归并与外观设置已随 [v2.0.16](https://github.com/yxiaojiang/LanChat/releases/tag/v2.0.16) 发布。LC-LINUX-002 第 0 步已固定本机 WinForms 原生构建、18 张运行截图和操作清单。第 2 步修复共享核心并发同名接收，Windows 原生构建和同名文件回环均通过。第 4 步仍要用公开 Windows 版联测两架构 Linux 实机。Windows Electron 迁移在 Linux 验收后另行评估。 [Windows 基线](../../PlanDocs/LC-LINUX-002/BASELINE.md) · [集成记录](../../PlanDocs/LC-LINUX-002/INTEGRATION.md)
