# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-10-25。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.1.0**。
> 核心里程碑：全面对标实机对比图与 GrokBot 桌面端，**全盘重构输入胶囊、浮动菜单栏与设置界面，建立完整 UI 设计系统规范，举一反三无死角对齐**！
> 基于用户提供的 3 组实测比对图与实机参考图集（`assets/GrokBotPics`）深度重塑：
> 1. **建立权威设计系统文档**：完成 `Docs/GrokBot_UI_Design_System.md` 与 `.claude/contexts/GrokUI.md`，详尽记录字体大小/字重/颜色明度阶梯、边框距离法则、圆角系统、图标规范与窗口比例；
> 2. **彻底废除系统原生直角菜单**：新增 `GrokPopupMenu`（16px 大圆角、CS_DROPSHADOW 柔和阴影、44px 舒适行高、8px 悬停圆角卡片、纯 GDI+ 矢量细线图标），全盘替换主界面新建会话菜单、输入区附件菜单、会话右键菜单及托盘右键菜单；
> 3. **主输入胶囊深度对标重构**：消除刺眼蓝色发光边框，更新为 64px 高、32px 满圆角 `#202024` 深炭灰胶囊（1px 微光边框 `#2C2C32`）；文本采用 11.0pt `#F2F2F6`（95% 明度）绝对数学垂直居中；左侧 36x36 附件圆钮（`#2A2A2E`）；右侧细线矢量麦克风（`#8E8E93`）；右侧发送圆钮更新为 44x44 魅紫正圆（`#8E64DC`），内部绘制加粗纯白居中向上矢量细箭头 `↑`；
> 4. **全局设置窗口（GrokSettingsDialog）全面重塑**：尺寸扩充至 920x640 黄金宽屏比例；移除侧边栏与主视口间的竖直单线，依靠 `#0E0E0E` 与 `#121212` 纯底色阶差自然分离；顶部强化 18.0pt 加粗明亮大标题；新增 10.5pt 加粗中性灰分组标签；卡片内边距严格 22px、行高严格 54px；右侧控件统一为 32px 药丸下拉胶囊；
> 5. **“举一反三”全窗体审计对齐**：全盘核实所有模态窗口（`GrokConnectNodeForm`、`GrokCreateNodeForm`、`GrokFeedbackForm`、`GrokChangeNickForm`、`GrokUpdateForm`），统一大标题为 18.0pt 加粗明亮纯白、统一边框微光、统一圆角与按钮状态；
> 6. **称谓与图标严谨闭环**：全代码库 100% 杜绝“连接应用”，一律为“连接节点”；通过 `SetCurrentProcessExplicitAppUserModelID`、`WM_SETICON` 双向发送及 `SetClassLongPtr` 彻底刷新任务栏紫吉祥物图标；
> 保持 C# 5.0 / .NET Framework 4.0 兼容与原生 `csc.exe` 秒级纯编译，零外部依赖，编译体积 ~224KB。

---

## 一、 本轮工作态与交付成果总结

### 1. 核心问题定位与设计系统确立
- 用户针对实测截图提出三处核心不协调：
  - 图1：主输入胶囊字号缩水、未垂直居中、存在蓝色发光边框外圈、发送钮图标未对齐；
  - 图2：新建连接菜单为原生 90 度直角生硬 ContextMenuStrip，伴随低分辨率系统 Emoji；
  - 图3：设置窗体比例紧凑、存在多余侧边栏竖线、文字明度与行高逼仄。
- 提取并确立了 GrokBot UI 核心规范（已沉淀至 `Docs/GrokBot_UI_Design_System.md`）：
  - 5 级暗黑纵深（`#0E0E0E` -> `#121212` -> `#1C1C1F` -> `#202024` -> `#28282C`）；
  - 排版规格：标题 18.0pt Bold、标签 10.5pt Bold、输入 11.0pt Regular、辅助 9.0pt Regular；
  - 呼吸间距：卡片内边距 22px、卡片单行 54px、菜单项 44px；
  - 矢量无死角：消除所有系统原生 Emoji，纯 GDI+ 绘制 1.2px~2.2px 几何细线图标。

### 2. Universal 浮动菜单组件 `GrokPopupMenu` 落地
- 新增 `src/UI/Controls/GrokPopupMenu.cs`：
  - 继承自无边框 Form，配置 `CS_DROPSHADOW` 与 `WS_EX_TOOLWINDOW`；
  - 16px 圆角剪裁、`#1C1C20` 现代深灰背景与 `#2C2C34` 细边框；
  - 44px 舒适行高，鼠标悬停平滑呈现 8px 圆角反馈底块（`#2E2E34`，危险项为深红暗底）；
  - 纯 GDI+ 绘制 18x18 矢量图标，文本绝对垂直居中；
  - 支持副文本/快捷提示与带间距细分割线；
  - 具备屏幕边界防溢出自动纠偏与失焦自动销毁。
- **全盘替换**：
  - `MainForm.Chat.cs` 的 `BtnAddSession_Click`；
  - `MainForm.cs` 的 `ShowInputPlusContextMenu`；
  - `MainForm.Chat.cs` 的 `ListSessions_MouseUp`（好友右键与群聊右键）；
  - `MainForm.Tray.cs` 的托盘右键菜单。全项目消灭原生直角菜单。

### 3. 主输入胶囊与发送键重塑
- 胶囊高度精准设定为 64px（底部 Dock 高度 92px，上下间距平衡），圆角半径 32px；
- 底色采用纯正 `#202024`，1px 平滑暗黑边框 `#2C2C32`，彻底消除蓝色发光线；
- 输入框字号升级为 `11.0pt (15px)`，高饱和明亮白 `#F2F2F6`，占位符 `#5E5E66`，数学垂直居中；
- 左侧附件圆钮升级为 36x36 `#2A2A2E`（悬停 `#38383E`），细线矢量 `+`；
- 右侧麦克风图标调整为 32x32 点击区，`#8E8E93` 矢量麦克风；
- 右侧发送按钮升级为 44x44 正圆，Grok 魅紫 `#8E64DC`，内部绘制 2.2px 线宽、90 度对称纯白向上矢量细箭头 `↑`。

### 4. 设置窗口（GrokSettingsDialog）全面重塑
- 尺寸扩展为 920x640，横向空间大幅拓展；
- 移除竖直分割线，侧边栏 220px `#0E0E0E` 与内容区 `#121212` 纯依靠色阶差自然分离；
- 侧边栏项高度 46px，圆角 10px，激活底色 `#242428`，文字 10.5pt 加粗 `#F5F5F7`；
- 内容区标题升级为 18.0pt 加粗明亮纯白 `#FFFFFF`，位于 (252, 28)；
- 分组标签新增“账户”、“外观与偏好”、“支持与反馈”、“网络设备”、“文件传输偏好”、“更新状态”、“关于产品”，均为 10.5pt 加粗 `#8E8E93`；
- 卡片宽度 636px，底色 `#1C1C1F`，圆角 16px，行高 54px，左右内边距 22px；
- 右侧控制项统一为 32px 高度圆角药丸下拉胶囊（`#242428`，`#E0E0E5` + ` ∨`）。

### 5. 举一反三全窗体统一
- `GrokConnectNodeForm`、`GrokCreateNodeForm`、`GrokFeedbackForm`、`GrokChangeNickForm`、`GrokUpdateForm` 窗口大标题统一升级为 18.0pt 加粗明亮纯白，外边框微光与阴影统一。

### 6. 构建与热替换验证
- 原生系统编译器 `csc.exe` 耗时 < 1 秒，0 警告 0 错误编译通过；
- 执行 `Tools/CompileAndLink.bat` 成功将最新构建热替换至 `D:\soft\LanChat\LanChat.exe` 与 `D:\soft\LanChat\app.ico`。

---

## 二、 关键约束与设计原则

1. **以最小实现、零冗余依赖为荣**：
   - 保持 C# 5.0 / .NET Framework 4.0 语法兼容，杜绝任何 NuGet 外部依赖。
   - 编译产物稳定在 ~224KB，即拷即用，秒开运行。
2. **纯中文注释与规范**：所有新增和重构代码均保留详尽中文注释与规范排版。
3. **向后兼容与契约一致**：网络协议端口、魔数（`LANC`）、LEB128 编码与 64KB 流式传输保持 100% 兼容。

---

## 三、 模块索引 (当前项目目录架构)

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `src/Program.cs` | 应用程序入口，单实例互斥（Mutex）与全局消息广播唤醒，高 DPI 感知，AppUserModelID 注册 |
| `src/MainForm.cs` | 主窗体核心构造、控件布局树、Grok 响应式自适应布局协调、输入胶囊、无边框绘制辅助 |
| `src/MainForm.Chat.cs` | 会话管理与自适应列表卡片绘制（`ListSessions_DrawItem`）、文字收发与 GrokPopupMenu 弹出 |
| `src/MainForm.Files.cs` | 文件选择与拖拽监听、流式分块进度更新、文件接收与系统外壳调用 |
| `src/MainForm.Tray.cs` | 网络初始化、自适应 Hero 头像绘制（`BtnAvatar_Paint`）、托盘驻留与 GrokPopupMenu 托盘菜单 |
| `src/UI/Controls/GrokPopupMenu.cs` | 统一圆角浮动弹出菜单（16px圆角、44px舒适行高、8px悬停卡片、矢量图标、全自动边界检测） |
| `src/UI/Controls/GrokSettingsDialog.cs` | 全局设置窗口（920x640，220px纯黑侧边栏无缝过渡，18pt大标题，54px卡片行，32px药丸下拉） |
| `src/UI/Controls/GrokConnectNodeForm.cs` | 连接节点窗口（顶部搜索胶囊、过滤芯片、局域网在线节点推荐卡片流与手动指定 IP 直连握手） |
| `src/UI/Controls/GrokCreateNodeForm.cs` | 创建新节点窗口（居中动态吉祥物预览、起名胶囊输入框、14 种有机形态网格、11 色调色盘） |
| `src/UI/Controls/GrokAboutForm.cs` | 关于窗口（居中 Squircle 吉祥物徽章、版本信息与“复制版本信息”圆角药丸按钮） |
| `src/UI/Controls/GrokChangeNickForm.cs` | 暗黑昵称修改窗口（吉祥物微标、暗黑圆角输入框与紫色保存按钮） |
| `src/UI/Controls/GrokFeedbackForm.cs` | 发送反馈窗口（多行反馈输入框、网络诊断复选框与发送反馈按钮） |
| `src/UI/Controls/GrokUpdateForm.cs` | 软件更新窗口（版本对比横幅、更新日志与平滑进度条） |
| `src/UI/Controls/GrokMessageBox.cs` | 统一样式暗黑提示框（彻底取代系统生硬白底 MessageBox） |
| `src/UI/Renderers/WeChatIcon.cs` | Grok 吉祥物与矢量图标绘制引擎（Mascot Avatar、UpArrow、SoundWave、Mic 等） |
| `src/UI/Controls/WeChatChatView.cs` | Grok 自适应居中流式聊天视图控件（紫色气泡、深灰卡片、代码胶囊） |
| `Docs/GrokBot_UI_Design_System.md` | 完整 GrokBot UI 设计规范与视觉对齐标准文档 |
| `Tools/Compile.bat` | 原生极速纯编译脚本，纯 ASCII 0-BOM，仅生成 `release\LanChat.exe` |
| `Tools/CompileAndLink.bat` | 一键编译链接并将程序热替换覆盖至 `D:\soft\LanChat\` |
| `Tools/Publish.bat` | 一键打包 `release\LanChat.msi` 并自动上传发布至 GitHub Releases |
