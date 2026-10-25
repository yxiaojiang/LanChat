# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-10-25。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.0.0**。
> 核心里程碑：全面超越旧版界面，**全弹窗、菜单栏与交互 100% 高保真像素级还原 Grok Bot 桌面端现代惊艳清爽暗黑风格**！
> 基于用户提供的实机参考图集（`assets/GrokBotPics`：`ConnetctApp.png`, `CreateNewBot.png`, `About.png`, `SendFK.png`, `Alis.png`~`Alis7.png`）全盘扫描重塑：
> 1. **称谓严谨更正**：全面由“连接应用”纠正为“连接节点”；
> 2. **连接节点弹窗（GrokConnectNodeForm）**：对标 `ConnetctApp.png` 顶部搜索胶囊、过滤芯片（全部/在线/直连）、局域网推荐节点卡片流与手动 IP 握手直连卡片；
> 3. **创建新节点弹窗（GrokCreateNodeForm）**：对标 `CreateNewBot.png` 动态居中吉祥物 Hero 实时预览、起名胶囊、14 种有机形态选择网格与 11 种调色盘；
> 4. **关于窗口（GrokAboutForm）**：对标 `About.png` 居中立体质感 Squircle 徽章、版本信息与“复制版本信息”圆角胶囊；
> 5. **修改昵称与发送反馈（GrokChangeNickForm, GrokFeedbackForm）**：对标 `SendFK.png` 现代暗黑表单与多行输入；
> 6. **全局设置与更新向导（GrokSettingsDialog, GrokUpdateForm）**：对标 `Alis4~7.png` 侧边栏导航与卡片流；
> 7. **统一暗黑提示弹窗（GrokMessageBox）**：彻底剔除系统原生白底 MessageBox；
> 8. **任务栏图标彻底更正**：通过 AppUserModelID + `WM_SETICON` + `SetClassLongPtr` 显式写入，解决任务栏图标缓存旧图标问题。
> 保持 C# 5.0 / .NET Framework 4.0 兼容与原生 `csc.exe` 秒级纯编译，零外部依赖，编译体积 ~217KB。

---

## 一、 本轮工作态与交付成果总结

### 1. `GrokBotPics` 参照图集像素级逆向落地
- **连接节点（GrokConnectNodeForm）**：
  - 点击底部中间“连接节点”胶囊（或顶部加号菜单“连接节点”）弹出；
  - 顶部 40px 高大圆角搜索胶囊（实时按昵称/主机名/IP 过滤），右侧刷新按钮；
  - 4 种过滤芯片胶囊（全部、局域网在线、历史节点、手动直连）；
  - 局域网节点 2 列卡片流：自绘对应有机形态吉祥物、昵称、在线状态与“连接”药丸按钮；
  - 底部“手动指定 IP 握手直连”独立卡片：输入框 + 紫色“连接握手”按钮。
- **创建新节点（GrokCreateNodeForm）**：
  - 点击列表末尾“新建 Bot”或顶部“新建节点”弹出；
  - 居中 96x96 大号吉祥物 Hero 实时预览；
  - 14 种有机形态网格（圆、云朵、圆角方块、四角星、爆米花、花生、果冻、瓜子、鹅卵石、圆角三角、楔形、六瓣花、石块、带柄南瓜）；
  - 11 种调色盘圆点（白色、棕色、红色、橙色、黄色、绿色、青色、蓝色、魅紫、粉色、烟灰），点击任意形态或颜色，顶部预览实时联动画出倾斜胶囊眼；
  - 底部“取消”与“创建”药丸按钮。
- **关于与支持（GrokAboutForm）**：
  - 84x84 立体质感 Squircle 徽章（绘制白色立体头部与倾斜双黑胶囊眼）；
  - 居中标题与版本、构建日期与版权；
  - 底部“复制版本信息”胶囊按钮（点击复制并反馈“已复制 ✓”）。
- **统一暗黑提示（GrokMessageBox）**：
  - 替代所有原生 `MessageBox.Show`，暗黑 16px 圆角窗体与矢量图标（信息、警告、错误、询问）；
  - 优雅平滑圆角药丸按钮（确定、取消、是、否）。

### 2. 任务栏图标彻底更正
- 在 `Program.cs` 启动时显式注册 `SetCurrentProcessExplicitAppUserModelID("Aritex.LanChat.Desktop.v2")`，强制 Windows 任务栏建立全新进程分组与图标索引；
- 在 `MainForm.cs` 的 `OnHandleCreated` 与 `OnShown` 中，通过 `SendMessagePtr` 显式发送 `WM_SETICON`（`ICON_SMALL` 与 `ICON_BIG`），并调用 `SetClassLongPtr` 写入 `GCLP_HICON` 与 `GCLP_HICONSM`，彻底解决无边框窗体缺少 `WS_CAPTION` 时任务栏回退显示旧缓存图标的问题。

### 3. 构建与热替换验证
- 系统原生 `csc.exe` 纯编译通过，0 错误 0 警告；
- 运行 `Tools/CompileAndLink.bat`，最新构建产物已同步替换覆盖至 `D:\soft\LanChat\LanChat.exe` 与 `D:\soft\LanChat\app.ico`。

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
