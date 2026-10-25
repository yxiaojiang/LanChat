# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-10-25。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.0.0**。
> 核心里程碑：全面超越旧版微信界面风格，**100% 高保真像素级还原 Grok Bot 桌面端现代惊艳清爽暗黑风格**！基于用户提供的实机最大化（`Alis.png` 1920x1030）与缩小（`Alis2.png` 1297x808）截图进行逐像素级逆向解构：确立「固定 420px 侧边栏 + 自适应右侧流式聊天区」的黄金架构；实现全宽呼吸感输入胶囊（68px 高、34px 圆角、28px 侧边距）、左对齐深灰圆角卡片（#222222，24px 圆角）、右对齐魅紫气泡（#7946D7，24px 圆角）；底部三联栏 Dock 与 82x82 Hero 双斜眼吉祥物纯 GDI+ 矢量自绘；严格保持 C# 5.0 / .NET Framework 4.0 兼容与系统原生 `csc.exe` 秒级纯编译，零外部依赖，产物体积恒定在 ~148KB。

---

## 一、 本轮工作态与交付成果总结

### 1. `Alis.png` 与 `Alis2.png` 像素级逆向解构与审美法则
- **双分辨率对比实测（125% DPI 1080p 实机基准）**：
  1. **侧边栏为 100% 绝对固定（Fixed Layout）**：
     - 在最大化（1920x1030）与缩小（1297x808）下，**侧边栏宽度均锁定为严格的 420px**，绝不随窗体拉伸而放大或缩小；
     - 顶部操作栏高 58px，右侧放置两个 36x36 纯黑圆形按钮（搜索与加号）；
     - 居中 Hero 头像区高 175px，居中放置 82x82 珊瑚红灵动吉祥物（右下角浅蓝状态圆点），下方 16px 处居中展示 10.5pt 用户昵称；
     - 会话列表项高 86px，内嵌 80px 高、382px 宽（左右边距各 19px）的 16px 圆角卡片（选中态底色 `#222222`），左侧 48x48 饱满吉祥物，右侧 11pt 粗体标题与 9.5pt 灰色摘要；
     - 底部 Dock 高 84px，底部下沉留白 25px，包含左侧 54x54 用户圆形徽章、中间 240x54 圆角胶囊按钮（`连接应用`，半径 27px）与右侧 54x54 高亮宝蓝圆钮（`#116DD2`）。
  2. **聊天区域完全自适应（Adaptive Pane）**：
     - **全宽底部输入胶囊**：横向铺满聊天区视口（距左 28px、距右 28px），高 68px，大圆角 34px，底部间距 24px；内嵌圆形 `+`、文本占位符、麦克风与 42x42 魅紫声波圆钮；
     - **左侧深灰卡片（对方消息）**：严格左对齐（距左 28px，与输入胶囊左边缘完全对齐），最大宽度上限 840px，圆角 24px，底色 `#222222`，边框 `#2C2C30`，内边距 `(26, 20, 26, 20)`，行距宽松透气；
     - **右侧魅紫气泡（我方消息）**：严格右对齐（距右 28px，与输入胶囊右边缘完全对齐），最大宽度上限 840px，圆角 24px，底色 `#7946D7`，内边距 `(24, 18, 24, 18)`；
     - **顶部浮动胶囊**：在聊天区顶部水平居中，高 38px（半径 19px），内含 24x24 微型吉祥物与 10.5pt 粗体会话名称；
     - **时间戳居中**：聊天记录时间戳（如 `今天 5:51`）在聊天区视口严格居中展示。

### 2. UI 矢量绘制引擎与控件全面重塑
- **`src/UI/Renderers/WeChatIcon.cs` 升级**：
  - 新增 `DrawMascotAvatar`：纯矢量高保真绘制 Grok 灵动双眼吉祥物（两颗逆时针倾斜 24° 的黑色垂直胶囊眼、右下角 `#3FA7F8` 状态蓝点、可选用机云朵波浪体与微型黑底角标）；
  - 新增 `DrawSoundWaveIcon`、`DrawUpArrowIcon`、`DrawMicIcon`、`DrawWinMinIcon`、`DrawWinMaxIcon`、`DrawWinCloseIcon`。
- **`src/UI/Controls/WeChatButtons.cs` 扩展**：
  - 增加 `IsCircle` 圆形按钮支持、自定义填充色（`FillColor`）与悬停色（`HoverFillColor`）、窗口关闭高亮模式。
- **`src/UI/Controls/WeChatChatView.cs` 深度优化**：
  - 实现左对齐 28px 与右对齐 28px 的 840px 约束气泡布局；
  - 气泡系统：自发高饱和魅紫圆角气泡（`#7946D7`，24px 大圆角），对方深灰圆角卡片（`#222222`，24px 大圆角，微光边框）；
  - 支持列表圆点前缀（`- `、`• `）与反引号行内代码胶囊（`...`）圆角高亮；
  - 居中时间戳（如“今天 5:51”）与系统提示严格对齐视口中央。
- **`src/MainForm.cs`、`MainForm.Chat.cs` 与 `MainForm.Tray.cs` 布局重塑**：
  - 固定 420px 侧边栏，各控件坐标、边距、圆角与字体精准还原；
  - 顶部浮动居中会话名称胶囊，全宽 68px 高大圆角输入胶囊，三联栏 Dock 底部沉底留白 25px。

### 3. 构建与发布闭环
- 原生系统编译器 `csc.exe` 耗时 < 1 秒，0 警告 0 错误编译通过；
- 执行 `Tools/CompileAndLink.bat` 成功将最新构建热替换至 `D:\soft\LanChat\LanChat.exe`。

---

## 二、 关键约束与设计原则

1. **以最小实现、零冗余依赖为荣**：
   - 保持 C# 5.0 / .NET Framework 4.0 语法兼容，杜绝任何 NuGet 外部依赖。
   - 编译产物稳定在 ~148KB，即拷即用，秒开运行。
2. **纯中文注释与规范**：所有新增和重构代码均保留详尽中文注释与规范排版。
3. **向后兼容与契约一致**：网络协议端口、魔数（`LANC`）、LEB128 编码与 64KB 流式传输保持 100% 兼容。

---

## 三、 模块索引 (当前项目目录架构)

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `src/Program.cs` | 应用程序入口，单实例互斥（Mutex）与全局消息广播唤醒，高 DPI 感知 |
| `src/MainForm.cs` | 主窗体核心构造、控件布局树、Grok 响应式自适应布局协调与无边框绘制辅助 |
| `src/MainForm.Chat.cs` | 会话管理与自适应列表卡片绘制（`ListSessions_DrawItem`）、文字收发与 Emoji 弹窗 |
| `src/MainForm.Files.cs` | 文件选择与拖拽监听、流式分块进度更新、文件接收与系统外壳调用 |
| `src/MainForm.Tray.cs` | 网络初始化、自适应 Hero 头像绘制（`BtnAvatar_Paint`）、托盘驻留与暗黑菜单 |
| `src/UI/Renderers/WeChatIcon.cs` | Grok 吉祥物与矢量图标绘制引擎（Mascot Avatar、SoundWave、UpArrow 等） |
| `src/UI/Renderers/WeChatDarkMenuRenderer.cs` | 暗黑风格右键弹出菜单渲染器与暗黑悬浮气泡 |
| `src/UI/Controls/WeChatButtons.cs` | 圆形图标按钮（`IsCircle`）与无边框工具栏按钮（`WeChatIconButton`） |
| `src/UI/Controls/WeChatChatView.cs` | Grok 自适应居中流式聊天视图控件（紫色气泡、深灰卡片、代码胶囊） |
| `src/Services/SessionManager.cs` | 会话与消息历史纯领域封装，彻底解耦窗体与聊天数据 |
| `src/Services/UpdateService.cs` | GitHub Releases 双轨在线检测、降级 Web 302 探测与暗黑更新向导弹窗 |
| `Tools/Compile.bat` | 原生极速纯编译脚本，纯 ASCII 0-BOM，仅生成 `release\LanChat.exe` |
| `Tools/CompileAndLink.bat` | 一键编译链接并将程序热替换覆盖至 `D:\soft\LanChat\` |
| `Tools/Publish.bat` | 一键打包 `release\LanChat.msi` 并自动上传发布至 GitHub Releases |

---

## 四、 下一步计划

1. **GitHub 仓库设为 Public 并实机验证在线更新弹窗**：
   - 需用户在 GitHub 仓库管理后台将 Visibility 设为 Public；
   - 客户端点击菜单【🔄 检查新版本...】完成闭环校验。
2. **实机多端互通与网络极端条件演练**：
   - 验证 Windows 与 Linux 在复杂多网卡与断网重连下的自适应重绘与即时消息交互。
