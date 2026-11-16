# LanChat 全客户端 UI 元素样式与界面修改指南 (UI Customization Guide)

> **适用版本**：LanChat v2.0.14+  
> **文档位置**：`Docs/UI_Customization_Guide.md`  
> **制定基准**：严格对标 `.claude/Rules.md`（DIR-004 统一自绘样式、DIR-005 代码架构解耦、DIR-006 注释规范及 Zero White Scrollbar 契约）与 `Docs/GrokBot_UI_Design_System.md`。  
> **目标读者**：开发者与 UI 定制人员。本文档详尽解析全应用每一个窗体、容器、面板、按钮、字体、颜色、圆角、间距与矢量的修改位置与代码示例，确保开发者无需猜测即可精准、安全地调整界面视觉。

---

## 目录

1. [样式体系架构总览与“分层修改法则”](#一-样式体系架构总览与分层修改法则)
2. [第一层：全局设计令牌中枢修改 (`GrokTheme.cs`)](#二-第一层全局设计令牌中枢修改-grokthemecs)
   - [2.1 背景层级与纵深色彩 (Elevation)](#21-背景层级与纵深色彩-elevation)
   - [2.2 边框、分割线与微光线条 (Borders)](#22-边框分割线与微光线条-borders)
   - [2.3 文本明度阶梯与颜色 (Text Hierarchy)](#23-文本明度阶梯与颜色-text-hierarchy)
   - [2.4 品牌强调色与聊天状态 (Accent & Brands)](#24-品牌强调色与聊天状态-accent--brands)
   - [2.5 按钮状态色彩定义 (Button Roles)](#25-按钮状态色彩定义-button-roles)
   - [2.6 窗口基准尺寸矩阵 (Window Sizes)](#26-窗口基准尺寸矩阵-window-sizes)
   - [2.7 控件间距、内边距与度量 (Metrics)](#27-控件间距内边距与度量-metrics)
   - [2.8 动态排版与字体系统 (Typography)](#28-动态排版与字体系统-typography)
   - [2.9 核心自绘渲染工具函数](#29-核心自绘渲染工具函数)
3. [第二层：通用自绘基础控件与渲染引擎](#三-第二层通用自绘基础控件与渲染引擎)
   - [3.1 统一自绘按钮控件 (`GrokButton.cs`)](#31-统一自绘按钮控件-grokbuttoncs)
   - [3.2 无边框模态窗基类 (`GrokModalFormBase.cs`)](#32-无边框模态窗基类-grokmodalformbasecs)
   - [3.3 浮动暗黑右键菜单 (`GrokPopupMenu.cs`)](#33-浮动暗黑右键菜单-grokpopupmenucs)
   - [3.4 图标与控制按钮 (`WeChatButtons.cs`)](#34-图标与控制按钮-wechatbuttonscs)
   - [3.5 双缓冲防闪烁容器 (`DoubleBufferedPanel.cs`)](#35-双缓冲防闪烁容器-doublebufferedpanelcs)
   - [3.6 无边框窗口缩放消息过滤器 (`FormResizeMessageFilter.cs`)](#36-无边框窗口缩放消息过滤器-formresizemessagefiltercs)
   - [3.7 暗黑菜单渲染器与浮动 Tooltip (`WeChatDarkMenuRenderer.cs`)](#37-暗黑菜单渲染器与浮动-tooltip-wechatdarkmenurenderercs)
   - [3.8 纯矢量 GDI+ 图标引擎 (`WeChatIcon.cs`)](#38-纯矢量-gdi-图标引擎-wechaticoncs)
4. [第三层：主窗口拆分架构与视觉定制 (`MainForm*.cs`)](#四-第三层主窗口拆分架构与视觉定制-mainformcs)
   - [4.1 控件树与响应式布局 (`MainForm.Designer.cs`)](#41-控件树与响应式布局-mainformdesignercs)
   - [4.2 左侧会话导航栏 (`_sessionsPanel`)](#42-左侧会话导航栏-_sessionspanel)
     - [4.2.1 顶部搜索框与搜索圆纽](#421-顶部搜索框与搜索圆纽)
     - [4.2.2 头部 Hero Profile 卡片与大头像 (`MainForm.Tray.cs`)](#422-头部-hero-profile-卡片与大头像-mainformtraycs)
     - [4.2.3 会话列表自绘 (`SessionListBox.cs` & `MainForm.Chat.cs`)](#423-会话列表自绘-sessionlistboxcs--mainformchatcs)
     - [4.2.4 自绘极简暗灰浮动滑块 (Zero White Scrollbar)](#424-自绘极简暗灰浮动滑块-zero-white-scrollbar)
     - [4.2.5 底部用户圆形徽标与快捷菜单 (`GrokSettingsMenuForm.cs`)](#425-底部用户圆形徽标与快捷菜单-groksettingsmenuformcs)
   - [4.3 右侧主聊天视口区 (`_chatPanel`)](#43-右侧主聊天视口区-_chatpanel)
     - [4.3.1 顶部标题药丸栏与无边框窗体控制钮](#431-顶部标题药丸栏与无边框窗体控制钮)
     - [4.3.2 文件传输进度横幅 (`MainForm.Files.cs`)](#432-文件传输进度横幅-mainformfilescs)
     - [4.3.3 核心聊天流自绘 (`WeChatChatView.cs`)](#433-核心聊天流自绘-wechatchatviewcs)
     - [4.3.4 底部浮动输入胶囊与发送圆纽](#434-底部浮动输入胶囊与发送圆纽)
   - [4.4 系统托盘、未读多会话悬浮预览 (`GrokTrayPreviewPopup.cs`)](#44-系统托盘未读多会话悬浮预览-groktraypreviewpopupcs)
5. [第四层：全局设置中心与 8 大子面板 (`GrokSettingsDialog` & `SettingsPanels`)](#五-第四层全局设置中心与-8-大致面板-groksettingsdialog--settingspanels)
   - [5.1 设置窗体外壳与左侧导航栏 (`GrokSettingsDialog.cs`)](#51-设置窗体外壳与左侧导航栏-groksettingsdialogcs)
   - [5.2 设置子面板抽象基类 (`SettingsPanelBase.cs`)](#52-设置子面板抽象基类-settingspanelbasecs)
   - [5.3 真实 8 大子面板具体修改索引](#53-真实-8-大致面板具体修改索引)
6. [第五层：9 大独立模态与悬浮弹窗修改指南](#六-第五层9-大独立模态与悬浮弹窗修改指南)
   - [6.1 统一消息确认框 (`GrokMessageBox.cs`)](#61-统一消息确认框-grokmessageboxcs)
   - [6.2 修改昵称窗口 (`GrokChangeNickForm.cs`)](#62-修改昵称窗口-grokchangenickformcs)
   - [6.3 关于软件窗口 (`GrokAboutForm.cs`)](#63-关于软件窗口-grokaboutformcs)
   - [6.4 意见反馈窗口 (`GrokFeedbackForm.cs`)](#64-意见反馈窗口-grokfeedbackformcs)
   - [6.5 软件更新窗口 (`GrokUpdateForm.cs`)](#65-软件更新窗口-grokupdateformcs)
   - [6.6 新建节点窗口 (`GrokCreateNodeForm.cs`)](#66-新建节点窗口-grokcreatenodeformcs)
   - [6.7 连接节点窗口 (`GrokConnectNodeForm.cs`)](#67-连接节点窗口-grokconnectnodeformcs)
   - [6.8 齿轮设置快捷弹出菜单 (`GrokSettingsMenuForm.cs`)](#68-齿轮设置快捷弹出菜单-groksettingsmenuformcs)
   - [6.9 托盘未读多会话悬浮预览卡片 (`GrokTrayPreviewPopup.cs`)](#69-托盘未读多会话悬浮预览卡片-groktraypreviewpopupcs)
7. [第六层：业务控制层与数据联动 (Controllers & Presenters)](#七-第六层业务控制层与数据联动-controllers--presenters)
   - [7.1 会话业务编排 (`SessionPresenter.cs`)](#71-会话业务编排-sessionpresentercs)
   - [7.2 聊天收发编排 (`ChatPresenter.cs`)](#72-聊天收发编排-chatpresentercs)
   - [7.3 文件传输调度 (`FileTransferController.cs`)](#73-文件传输调度-filetransfercontrollercs)
8. [高频修改场景实战 Cookbook (常见需求秒改)](#八-高频修改场景实战-cookbook-常见需求秒改)
   - [场景 1：全局一键更换主品牌色 (魅紫 → 极客蓝 / 翡翠绿)](#场景-1全局一键更换主品牌色-魅紫--极客蓝--翡翠绿)
   - [场景 2：调整全局默认字体与字号阶梯](#场景-2调整全局默认字体与字号阶梯)
   - [场景 3：调整聊天气泡内边距与最大宽度](#场景-3调整聊天气泡内边距与最大宽度)
   - [场景 4：调整主窗体默认大小或左侧栏宽度](#场景-4调整主窗体默认大小或左侧栏宽度)
   - [场景 5：修改按钮默认圆角为直角或小圆角](#场景-5修改按钮默认圆角为直角或小圆角)
   - [场景 6：在全局设置中心中新增一个设置项或卡片行](#场景-6在全局设置中心中新增一个设置项或卡片行)
   - [场景 7：新增一个全新的自绘矢量图标并在界面中使用](#场景-7新增一个全新的自绘矢量图标并在界面中使用)
   - [场景 8：修改无边框窗体拖拽缩放热区边距 (`FormResizeMessageFilter.cs`)](#场景-8修改无边框窗体拖拽缩放热区边距-formresizemessagefiltercs)
9. [语法约束、单文件限制与编译验证契约](#九-语法约束单文件限制与编译验证契约)

---

## 一、 样式体系架构总览与“分层修改法则”

项目历经 **DIR-004（UI 统一样式系统重构）** 与 **DIR-005（全客户端架构解耦与防上帝类工程）**，已彻底消除所有硬编码样式与散乱耦合逻辑，形成清晰规范的 **分层治理架构**：

```
+----------------------------------------------------------------------------------------------------+
| 第一层：全局设计令牌中枢 (Single Source of Truth)                                                    |
|   文件：src/UI/Controls/GrokTheme.cs                                                                |
|   管理：背景色阶(Level0~3)、文字阶梯、品牌强调色、基准窗体尺寸、全局字体、间距度量与 GDI+ 绘图辅助函数     |
+----------------------------------------------------------------------------------------------------+
                                                  │
                                                  ▼
+----------------------------------------------------------------------------------------------------+
| 第二层：通用自绘基础控件与渲染引擎 (Base Controls & Renderers)                                       |
|   文件：GrokButton, GrokModalFormBase, GrokPopupMenu, DoubleBufferedPanel,                         |
|         FormResizeMessageFilter, WeChatButtons, WeChatIcon, WeChatDarkMenuRenderer                 |
|   管理：按钮 4 大角色、模态窗 16px 圆角+阴影+拖拽外壳、暗黑右键菜单、无边框缩放、40+ 纯矢量图标与 Tooltip |
+----------------------------------------------------------------------------------------------------+
                                                  │
                                                  ▼
+----------------------------------------------------------------------------------------------------+
| 第三层：视图层与界面排版 (Views, Dialogs & Panels)                                                   |
|   主窗口组件：MainForm.Designer (布局与树), MainForm.Chat (聊天与会话自绘), MainForm.Files, Tray    |
|   自绘核心：WeChatChatView (气泡流), SessionListBox (联系人卡片), GrokTrayPreviewPopup (托盘卡片)    |
|   设置中心：GrokSettingsDialog (导航外壳) + 8 大独立子面板 (src/UI/Controls/SettingsPanels/*.cs)    |
|   独立模态弹窗：GrokMessageBox, GrokAboutForm, GrokChangeNickForm 等 9 大窗口                        |
+----------------------------------------------------------------------------------------------------+
                                                  │
                                                  ▼
+----------------------------------------------------------------------------------------------------+
| 第四层：业务控制与数据联动层 (MVP Presenters & Controllers)                                          |
|   文件：src/Controllers/SessionPresenter.cs, ChatPresenter.cs, FileTransferController.cs          |
|   管理：会话状态过滤、消息收发与提示路由、文件流式分块进度、跨线程 SafeInvoke 调度                      |
+----------------------------------------------------------------------------------------------------+
```

### 修改定位口诀：
1. **想改“全局通用属性”**（如主色调、字体、背景深浅、弹窗默认大小）：**只改 `GrokTheme.cs`**，全客户端自动联动。
2. **想改“某一类控件的外观动效”**（如所有按钮的悬停亮度、所有弹窗的右上角关闭叉、菜单悬停底色）：**修改对应的公共控件**（如 `GrokButton.cs`、`GrokModalFormBase.cs`、`GrokPopupMenu.cs`）。
3. **想改“特定界面的专属排版与布局”**（如主窗口搜索框位置、聊天气泡特定细节、某个弹窗独有的卡片）：**进入具体的窗体分部类或设置子面板文件修改**。
4. **想改“界面交互逻辑与状态反馈”**（如点击某项后的切换事件、防抖延迟、未读红点清除）：**定位到 `src/Controllers/` 对应的 Presenter**。

---

## 二、 第一层：全局设计令牌中枢修改 (`GrokTheme.cs`)

**文件路径**：`src/UI/Controls/GrokTheme.cs`  
这是整个客户端样式的“心脏”。所有常量均严格兼容 C# 5.0 / .NET Framework 4.0 语法。

### 2.1 背景层级与纵深色彩 (Elevation)

客户端采用 5 级暗黑纵深分层矩阵，依靠微妙的明暗色阶呈现立体感，严禁在暗黑底色上画生硬的白色反光边框：

```csharp
// GrokTheme.cs (行 24~30)
public static readonly Color Level0 = Color.FromArgb(14, 14, 14);       // #0E0E0E 最底层基板（左侧会话栏、设置侧边栏）
public static readonly Color Level05 = Color.FromArgb(18, 18, 18);      // #121212 主视口底色（主聊天视口、设置内容区）
public static readonly Color Level1 = Color.FromArgb(28, 28, 31);       // #1C1C1F 卡片容器（大卡片、模态弹窗底色）
public static readonly Color Level2 = Color.FromArgb(32, 32, 36);       // #202024 浮动胶囊（底部输入胶囊、搜索框容器）
public static readonly Color Level3 = Color.FromArgb(40, 40, 44);       // #28282C 高亮与次要按钮正常底色
public static readonly Color Level3Hot = Color.FromArgb(46, 46, 52);    // #2E2E34 悬停高亮色（菜单项、侧边栏 Tab 悬停）
```

* **修改效果**：
  * 若要调亮整体暗色调，把 `Level0` 改为 `(22, 22, 24)`，`Level05` 改为 `(28, 28, 30)` 等；
  * 若要更纯粹的 OLED 纯黑，可将 `Level0` 改为 `(0, 0, 0)`。

---

### 2.2 边框、分割线与微光线条 (Borders)

```csharp
// GrokTheme.cs (行 32~34)
public static readonly Color Border = Color.FromArgb(44, 44, 50);       // #2C2C32 模态窗外沿、输入胶囊外框、卡片边框
public static readonly Color BorderInner = Color.FromArgb(38, 38, 43);  // #26262B 卡片内部分割线、行间分割线
public static readonly Color Separator = Color.FromArgb(44, 44, 52);    // #2C2C34 浮动菜单分割线
```

* **修改规范**：线宽统一保持 `1.0px`。坚决杜绝亮蓝色或高对比亮白边框。

---

### 2.3 文本明度阶梯与颜色 (Text Hierarchy)

```csharp
// GrokTheme.cs (行 37~48)
public static readonly Color TextPrimary = Color.FromArgb(242, 242, 246);   // #F2F2F6 (95%明度) 主内容、输入文字、正文
public static readonly Color TextTitle = Color.FromArgb(255, 255, 255);     // #FFFFFF (100%明度) 大标题、模态窗窗名
public static readonly Color TextSecondary = Color.FromArgb(142, 142, 147); // #8E8E93 (57%明度) 辅助说明、时间戳、次级文字
public static readonly Color TextSubtitle = Color.FromArgb(161, 161, 166);   // #A1A1A6 会话列表副文本、卡片分组标签
public static readonly Color TextPlaceholder = Color.FromArgb(94, 94, 102); // #5E5E66 (38%明度) 输入框占位符
public static readonly Color TextDisabled = Color.FromArgb(74, 74, 80);     // #4A4A50 (30%明度) 禁用文字、灰色状态
public static readonly Color TextMenu = Color.FromArgb(228, 228, 232);      // #E4E4E8 浮动菜单主要文字
public static readonly Color TextPill = Color.FromArgb(245, 245, 247);      // #F5F5F7 顶部药丸标题、头像文字
public static readonly Color TextCard = Color.FromArgb(240, 240, 244);      // #F0F0F4 卡片主要设置项标题
public static readonly Color TextDestructive = Color.FromArgb(252, 68, 85); // #FC4455 危险操作、退出/删除红色
public static readonly Color TextDestructiveHot = Color.FromArgb(255, 92, 108); // 危险操作悬停提亮红
```

---

### 2.4 品牌强调色与聊天状态 (Accent & Brands)

```csharp
// GrokTheme.cs (行 51~66)
public static readonly Color AccentPurple = Color.FromArgb(142, 100, 220);  // #8E64DC Grok 魅紫（悬停与发送键）
public static readonly Color AccentPurpleHot = Color.FromArgb(157, 117, 236);
public static readonly Color AccentPurpleDeep = Color.FromArgb(121, 70, 215); // #7946D7 主品牌色、发送气泡底色、选中项
public static readonly Color AccentBlue = Color.FromArgb(77, 155, 255);     // #4D9BFF 智能天蓝（链接、提示、更新文本）
public static readonly Color AccentBlueBtn = Color.FromArgb(17, 109, 210);  // #116DD2 蓝色按钮底色
public static readonly Color AccentCoral = Color.FromArgb(252, 44, 62);     // #FC2C3E 珊瑚红（Hero 头像底色、警报）
public static readonly Color AccentGreen = Color.FromArgb(31, 214, 126);    // #1FD67E 在线绿色指示点、开关开启色

// 聊天与交互表面色
public static readonly Color SessionSelected = Color.FromArgb(121, 70, 215); // #7946D7 会话列表选中底色 (与本人气泡一致)
public static readonly Color SessionHover = Color.FromArgb(34, 34, 38);     // #222226 会话项悬停卡片底色
public static readonly Color PeerBubble = Color.FromArgb(30, 30, 33);       // #1E1E21 对方发送的消息气泡底色
public static readonly Color InputPlus = Color.FromArgb(42, 42, 46);        // #2A2A2E 输入框左侧 (+) 圆纽底色
public static readonly Color IconMuted = Color.FromArgb(160, 160, 165);     // #A0A0A5 静止态图标灰
public static readonly Color MenuHoverDanger = Color.FromArgb(60, 24, 28);  // #3C181C 危险项悬停暗红底色
```

---

### 2.5 按钮状态色彩定义 (Button Roles)

```csharp
// GrokTheme.cs (行 68~76)
// Primary (主要确认按钮：魅紫)
public static readonly Color ButtonPrimaryNormal = Color.FromArgb(121, 70, 215); // #7946D7
public static readonly Color ButtonPrimaryHover = Color.FromArgb(142, 100, 220);  // #8E64DC
public static readonly Color ButtonPrimaryDown = Color.FromArgb(105, 55, 195);

// Secondary (次要取消/功能按钮：暗灰 Level3)
public static readonly Color ButtonSecondaryNormal = Color.FromArgb(40, 40, 44);  // #28282C
public static readonly Color ButtonSecondaryHover = Color.FromArgb(52, 52, 58);
public static readonly Color ButtonSecondaryDown = Color.FromArgb(32, 32, 36);

// Destructive (危险操作按钮：暗酒红)
public static readonly Color ButtonDestructiveNormal = Color.FromArgb(60, 24, 28); // #3C181C
public static readonly Color ButtonDestructiveHover = Color.FromArgb(80, 30, 36);
public static readonly Color ButtonDestructiveDown = Color.FromArgb(48, 18, 22);
```

---

### 2.6 窗口基准尺寸矩阵 (Window Sizes)

修改这里的尺寸常量，所有对应的窗体在初始化时都会自动采用新基准尺寸：

```csharp
// GrokTheme.cs (行 79~88)
public static readonly Size SizeMainWindow = new Size(1040, 720);   // 主窗口 (1.44:1 比例)
public static readonly Size SizeSettings = new Size(920, 640);       // 设置窗口标准尺寸
public static readonly Size SizeSettingsLarge = new Size(1260, 820); // 设置窗口大屏舒适尺寸
public static readonly Size SizeConnectNode = new Size(760, 600);    // 连接节点窗口
public static readonly Size SizeCreateNode = new Size(680, 620);     // 新建节点窗口
public static readonly Size SizeAbout = new Size(440, 560);          // 关于窗口
public static readonly Size SizeFeedback = new Size(520, 460);       // 意见反馈窗口
public static readonly Size SizeUpdate = new Size(520, 440);         // 版本更新提示窗口
public static readonly Size SizeMessageBox = new Size(460, 260);     // 自定义弹窗基准尺寸
public static readonly Size SizeChangeNick = new Size(440, 260);     // 修改昵称窗口
```

---

### 2.7 控件间距、内边距与度量 (Metrics)

```csharp
// GrokTheme.cs (行 91~124)
// 按钮度量
public const int ButtonHeightStandard = 36;       // 标准操作按钮高度 (36px)
public const int ButtonHeightPill = 32;           // 药丸胶囊按钮高度 (32px)
public const int ButtonPaddingHorizontal = 18;    // 按钮文字水平内边距
public const int ButtonRadiusStandard = 8;        // 标准按钮圆角半径
public const int ButtonRadiusPill = 16;           // 药丸按钮全圆角半径

// 模态弹窗右上角关闭叉
public const int CloseButtonSize = 28;            // 叉叉点击区 (28×28px)
public const int CloseButtonMargin = 16;          // 距顶部和右边缘各 16px

// 通用卡片与弹窗内边距
public const int CardPadding = 20;                // 设置卡片内边距
public const int CardMargin = 12;                 // 卡片外间距
public const int ModalPadding = 24;               // 弹窗主体内边距
public const float LineSpacingExtra = 5f;         // 文本行间距加量 (5px 呼吸感)

// 会话列表度量 (SessionListBox)
public const int SessionItemHeight = 72;          // 单个会话项高度 (72px)
public const int SessionCardMarginX = 17;         // 会话选中卡片水平外边距 (左右各缩进 17px)
public const int SessionCardMarginY = 5;          // 会话选中卡片垂直外边距 (上下各缩进 5px)
public const int SessionCardRadius = 14;          // 会话卡片圆角半径
public const int SessionAvatarSize = 48;          // 会话头像直径 (48px 正圆)
public const int SessionAvatarPaddingLeft = 30;   // 头像左边距

// 聊天流度量 (WeChatChatView)
public const float ChatLineSpacingExtra = 5f;     // 气泡文本行高加量
// 本人气泡 4 方向内边距 (支持独立精细排版)
public const int ChatBubblePadSelfLeft = 16;      // 本人气泡左内边距
public const int ChatBubblePadSelfRight = 16;     // 本人气泡右内边距
public const int ChatBubblePadSelfTop = 12;       // 本人气泡上内边距
public const int ChatBubblePadSelfBottom = 12;    // 本人气泡下内边距
// 对方气泡 4 方向内边距
public const int ChatBubblePadPeerLeft = 16;      // 对方气泡左内边距
public const int ChatBubblePadPeerRight = 16;     // 对方气泡右内边距
public const int ChatBubblePadPeerTop = 12;       // 对方气泡上内边距
public const int ChatBubblePadPeerBottom = 12;    // 对方气泡下内边距
// 兼容总边距 (Left + Right / Top + Bottom)
public const int ChatBubblePadSelfH = ChatBubblePadSelfLeft + ChatBubblePadSelfRight; // 32
public const int ChatBubblePadSelfV = ChatBubblePadSelfTop + ChatBubblePadSelfBottom; // 24
public const int ChatBubblePadPeerH = ChatBubblePadPeerLeft + ChatBubblePadPeerRight; // 32
public const int ChatBubblePadPeerV = ChatBubblePadPeerTop + ChatBubblePadPeerBottom; // 24
public const int ChatFileCardWidth = 340;         // 聊天文件卡片宽度
public const int ChatFileCardHeight = 96;         // 聊天文件卡片高度
public const float ChatTimestampFontSize = 9.0f;  // 时间戳字号 (9.0pt)

// 菜单与设置项
public const int MenuItemHeight = 44;             // 浮动菜单行高 (44px)
public const int SettingsRowHeight = 54;          // 设置卡片内部行高 (54px)
public const int RadiusModal = 16;                // 模态窗口外框圆角半径
public const int RadiusMenuHover = 8;             // 菜单项悬停圆角半径
```

---

### 2.8 动态排版与字体系统 (Typography)

`GrokTheme` 的字体是**动态自适应**的，读取 `AppSettings.UiFontFamily`（用户在“设置-外观”中切换的字体），若未设置则默认回退到 `"Microsoft YaHei UI"`：

```csharp
// GrokTheme.cs (行 15~21, 126~153)
public static string FontFamily
{
    get { return !string.IsNullOrEmpty(AppSettings.UiFontFamily) ? AppSettings.UiFontFamily : "Microsoft YaHei UI"; }
}

public static Font FontTitle        { get { return Font(18.0f, FontStyle.Bold); } }    // 弹窗一级大标题
public static Font FontSectionTitle { get { return Font(10.5f, FontStyle.Bold); } }    // 设置分组标签、卡片标题
public static Font FontPrimary      { get { return Font(11.0f, FontStyle.Regular); } } // 主输入框、聊天正文
public static Font FontPrimaryBold  { get { return Font(11.0f, FontStyle.Bold); } }    // 会话主标题加粗
public static Font FontCardItem     { get { return Font(10.5f, FontStyle.Regular); } } // 设置项具体名称
public static Font FontSecondary    { get { return Font(9.5f, FontStyle.Regular); } }  // 辅助说明、状态、弹窗正文
public static Font FontSecondaryBold{ get { return Font(9.5f, FontStyle.Bold); } }     // 强调副文本、群聊发件人
public static Font FontFootnote     { get { return Font(9.0f, FontStyle.Regular); } }  // 极小注释、版本号
public static Font FontButton       { get { return Font(10.0f, FontStyle.Bold); } }    // 标准按钮文字
public static Font FontButtonPill   { get { return Font(9.5f, FontStyle.Bold); } }     // 药丸按钮文字
```

* **使用方式**：在任何需要字体的地方，优先使用 `GrokTheme.FontPrimary` 或 `GrokTheme.Font(12.0f, FontStyle.Bold)`，不要自行 `new Font("Microsoft YaHei UI", ...)`，以便全局动态联动。

---

### 2.9 核心自绘渲染工具函数

`GrokTheme` 封装了一整套消除锯齿、软化字体和防 Tofu（豆腐块白方框）的 GDI+ 绘图函数：

| 函数签名 | 用途说明 |
| :--- | :--- |
| `PrepareSoftText(Graphics g)` | 设置 `TextRenderingHint.AntiAlias`，获得平滑灰阶文字，杜绝 ClearType 产生彩色杂边 |
| `DrawRoundedRectangle(g, pen/brush, bounds, radius)` | 纯数学绘制无断点、无突兀接缝的圆角矩形 |
| `DrawUiTextLeft(g, text, font, color, x, y, w, h)` | 左对齐、垂直严格数学居中绘制文本，带截断省略号 |
| `DrawUiTextCentered(g, text, font, color, bounds)` | 矩形区域水平、垂直完全居中绘制文本 |
| `SanitizeUiText(text)` | 自动剔除会导致 YaHei UI 显示豆腐块 `□` 的 Emoji 代理对与私有区符号 |
| `MeasureUiText(g, text, font)` | 预先精确测量文本所需宽高像素，用于动态气泡和标签自适应 |

---

## 三、 第二层：通用自绘基础控件与渲染引擎

### 3.1 统一自绘按钮控件 (`GrokButton.cs`)

**文件路径**：`src/UI/Controls/GrokButton.cs`  
继承自 WinForms `Button`，天然支持回车键响应（`AcceptButton`）、ESC 键响应（`CancelButton`）及 `DialogResult` 自动关闭父窗体。

#### 1. 四种内置角色 (Role)
* `GrokButtonRole.Primary`：魅紫确认主按钮（高度 36px，圆角 8px，纯白文字）；
* `GrokButtonRole.Secondary`：深灰操作次按钮（高度 36px，圆角 8px，TextPrimary 文字）；
* `GrokButtonRole.Destructive`：暗红危险操作按钮（高度 36px，圆角 8px，红底浅红文字）；
* `GrokButtonRole.Pill`：小巧药丸胶囊按钮（高度 32px，圆角 16px 半圆，用于弹窗底部确认或小卡片内）。

#### 2. 代码声明与使用示例
```csharp
// 创建一个确定主按钮
GrokButton btnSave = new GrokButton("保存", GrokButtonRole.Primary)
{
    Location = new Point(328, 196),
    Size = new Size(84, GrokTheme.ButtonHeightStandard)
};
btnSave.Click += (s, e) => { /* 保存逻辑 */ };
this.Controls.Add(btnSave);

// 创建一个药丸形态的取消按钮并绑定 DialogResult
GrokButton btnCancel = new GrokButton("取消", GrokButtonRole.Secondary, DialogResult.Cancel)
{
    IsPill = true,
    Location = new Point(236, 196),
    Size = new Size(84, GrokTheme.ButtonHeightPill)
};
this.Controls.Add(btnCancel);
```

#### 3. 如何修改按钮外观？
* **修改圆角**：直接设置 `btn.CornerRadius = 12;`（可针对单个按钮覆盖）；
* **全局修改按钮颜色**：在 `GrokTheme.cs` 的 `ButtonPrimary*`、`ButtonSecondary*` 修改对应色值；
* **修改按钮内边距与字体**：在 `GrokButton.ApplyRoleDefaults()` 中调整 `this.Font` 和 `this.Padding`。

---

### 3.2 无边框模态窗基类 (`GrokModalFormBase.cs`)

**文件路径**：`src/UI/Controls/GrokModalFormBase.cs`  
全客户端所有独立弹窗（除复杂的多页面主设置窗口外）**100% 继承自此基类**。它自动接管了：
1. **16px 圆角剪裁**：调用系统 `CreateRoundRectRgn`，窗体尺寸变化时自动安全重建并释放句柄，零 GDI 泄漏；
2. **Windows DWM 真实阴影**：通过重写 `CreateParams` 追加 `CS_DROPSHADOW (0x00020000)`，实现深邃外阴影；
3. **原生鼠标拖拽移动**：窗体空白区域按下鼠标左键即可随意拖动；
4. **1px 微光外框**：自绘 `#2C2C32` 细微边框，深色模式层次分明；
5. **右上角自绘关闭叉**：28×28px 悬停加亮抗锯齿关闭叉（距顶部和右边缘各 16px）；
6. **键盘 ESC 回退**：默认按下 ESC 键触发窗体关闭。

#### 1. 继承此基类开发新窗口示例
```csharp
public class MyCustomDialog : GrokModalFormBase
{
    public MyCustomDialog()
    {
        // 1. 设置尺寸（优先使用 GrokTheme 中定义的基准尺寸）
        this.Size = new Size(480, 320);
        
        // 2. 设置标题（可选，会在左上角自动绘制）
        this.TitleText = "自定义配置";
        
        // 3. 是否显示右上角关闭叉（默认 true）
        this.ShowCloseButton = true;
        
        // 4. 按下 ESC 是否自动退出（默认 true）
        this.CloseOnEscape = true;
        
        // 5. 添加子控件（按钮请一律使用 GrokButton）
        GrokButton btnOk = new GrokButton("确定", GrokButtonRole.Primary, DialogResult.OK)
        {
            Location = new Point(360, 260),
            Size = new Size(90, 36)
        };
        this.Controls.Add(btnOk);
    }
}
```

---

### 3.3 浮动暗黑右键菜单 (`GrokPopupMenu.cs`)

**文件路径**：`src/UI/Controls/GrokPopupMenu.cs`  
彻底取代 Windows 传统灰白直角 `ContextMenuStrip` 的暗黑圆角组件。

* **视觉规范**：
  * 固定宽度：默认 `256px`；
  * 行高：单项严格 `44px`；
  * 圆角：`16px` 全圆角窗体；
  * 悬停底块：`8px` 圆角，普通项 `#2E2E34`，危险项 `#3C181C`；
  * 图标：纯 GDI+ 矢量渲染，尺寸 `18×18px`，位于 `x = 20px`；
  * 边界安全：自动探测当前屏幕的工作区（`Screen.WorkingArea`），贴近屏幕边缘时自动反向展开，绝不超出屏幕。

#### 使用示例：
```csharp
GrokPopupMenu menu = new GrokPopupMenu(256);
menu.AddItem(WeChatIconType.Clear, "清空聊天记录", () => {
    // 点击回调
});
menu.AddSeparator(); // 分割线
menu.AddItem(WeChatIconType.WinClose, "退出登录", () => {
    // 退出
}, isDestructive: true); // 标为危险操作（红字+红悬停底）

// 在屏幕指定位置弹出（如按钮下方）
menu.ShowAt(btn.PointToScreen(new Point(0, btn.Height + 4)));
```

---

### 3.4 图标与控制按钮 (`WeChatButtons.cs`)

**文件路径**：`src/UI/Controls/WeChatButtons.cs`  
包含两个核心自绘图标按钮控件：
1. **`WeChatIconButton`**：
   * `IconType`：枚举 `WeChatIconType`；
   * `IsCircle`：`true` 时为正圆形（如输入框发送圆纽、附件加号纽、搜索放大纽）；`false` 为圆角或贴边矩形（如窗口最小化/关闭叉）；
   * `FillColor` / `HoverFillColor` / `IconColor`；
   * `IsWindowClose`：若是窗口右上角关闭按钮，悬停自动呈现经典危险红 `#E81123`。
2. **`WeChatNavButton`**：
   * 支持激活状态（`IsActive`，激活呈现微信绿或魅紫）、悬停半透明底块与暗黑 Tooltip 联动。

---

### 3.5 双缓冲防闪烁容器 (`DoubleBufferedPanel.cs`)

**文件路径**：`src/UI/Controls/DoubleBufferedPanel.cs`  
开启了 `OptimizedDoubleBuffer`、`AllPaintingInWmPaint`、`UserPaint` 与 `ResizeRedraw`，并压制了 `WM_ERASEBKGND`。在包含大量子控件、频繁滚屏或重绘的容器中（设置视口、聊天视口、主窗体分栏）必须使用此类替代原生 `Panel`，杜绝任何白底闪烁。

---

### 3.6 无边框窗口缩放消息过滤器 (`FormResizeMessageFilter.cs`)

**文件路径**：`src/UI/Controls/FormResizeMessageFilter.cs`  
专为无边框主窗体打造的原生 Windows 消息过滤器：
* **核心功能**：接管 `WM_NCHITTEST (0x0084)` 消息，在窗口四周 `8px` 边缘侦测光标位置，动态派发原生缩放指令（`HTLEFT`, `HTRIGHT`, `HTTOP`, `HTBOTTOM`, `HTTOPLEFT` 等）；
* **光标适配**：让无边框窗体获得与系统原生窗口 100% 一致的双向箭头缩放光标与流畅缩放手感；
* **修改位置**：若要调整缩放灵敏度或热区宽度，修改其中的 `private const int BORDER_PADDING = 8;`。

---

### 3.7 暗黑菜单渲染器与浮动 Tooltip (`WeChatDarkMenuRenderer.cs`)

**文件路径**：`src/UI/Renderers/WeChatDarkMenuRenderer.cs`  
* **`WeChatDarkMenuRenderer`**：微信暗黑风格 `ToolStripProfessionalRenderer`，统一接管原生菜单项的文本色、箭头色、勾选框与背景色；
* **`WeChatDarkTooltip`**：纯自绘暗灰底（`#2A2A2D`）、1px 微光边框（`#46464C`）的浮动 Tooltip 气泡。具备智能屏幕避让与多显示器边缘碰撞检测能力：
  ```csharp
  // 鼠标悬停显示气泡
  WeChatDarkTooltip.ShowTooltip(btnControl, "清空聊天记录");
  // 鼠标离开隐藏气泡
  WeChatDarkTooltip.HideTooltip();
  ```

---

### 3.8 纯矢量 GDI+ 图标引擎 (`WeChatIcon.cs`)

**文件路径**：`src/UI/Renderers/WeChatIcon.cs`  
全客户端**严禁使用 Emoji 字符串（如 ➕、⚙️、📁 等）替代图标**，所有图标 100% 采用纯 GDI+ 矢量数学公式实时绘制。
* **统一绘制方法**：
  ```csharp
  WeChatIcon.Draw(g, WeChatIconType.Search, targetRect, isActive, iconColor);
  ```
* **Mascot 吉祥物头像绘制**：
  ```csharp
  // 支持 14 种有机几何多边形与吉祥物面部特征
  WeChatIcon.DrawMascotAvatarShape(g, avatarRect, shapeIndex, bgColor, hasOnlineDot, borderColor);
  ```

---

## 四、 第三层：主窗口拆分架构与视觉定制 (`MainForm*.cs`)

主窗体文件已在 DIR-005 中彻底解耦为 5 个专职分部类文件，职责边界高度收敛：

```
+---------------------------------------------------------------------------------------------------------+
| [420px 固定宽：_sessionsPanel, #0E0E0E]       | [自适应填充满：_chatPanel, #121212]                      |
|                                              |                                                         |
| 1. 顶部搜索栏 (_searchHeaderPanel, 高 60px)    | 1. 顶部浮动标题药丸栏 (_chatHeader, 高 96px)             |
|    - 胶囊搜索框 (_searchBoxContainer, 高 42px) |    - 76px 居中大药丸 (48px 头像 + 20pt 粗体标题)          |
|    - 46px 搜索放大圆纽 (_btnSearchToggle)      |    - 右上角窗体控制按钮 (— □ ×, 各 56×64px)              |
|                                              | 2. 文件进度横幅 (_progressPanel, 默认隐藏)               |
| 2. 头部 Hero Profile 卡片 (高 175px)          | 3. 聊天流视口 (_chatView: WeChatChatView)                |
|    - 82×82px 珊瑚红 Mascot 头像 + 在线蓝点    |    - 本人紫色气泡 / 对方炭黑气泡                         |
|    - 居中明亮昵称 (12.0pt Soft AA)            |    - 4px 极简自绘暗灰滑块                                |
|                                              | 4. 底部浮动输入胶囊 (_bottomSendPanel, 高 90px)          |
| 3. 会话列表 (_listSessions: SessionListBox)  |    - 64px 浮动药丸 (圆角 32px, 外距 28px/22px)           |
|    - 72px 行高, 14px 圆角卡片                 |    - 左侧 38px (+) 附件圆纽                              |
|    - 全员群聊置顶项 / 好友联系人               |    - 单行暗黑输入框 (11.0pt #F2F2F6)                     |
|                                              |    - 右侧 44px 魅紫 (↑) 发送圆纽                         |
| 4. 底部工具栏 (_leftBottomBar, 高 84px)       |                                                         |
|    - 54×54px 用户圆形文字徽标 (_btnBottomUser)|                                                         |
+---------------------------------------------------------------------------------------------------------+
```

### 4.1 控件树与响应式布局 (`MainForm.Designer.cs`)

* **响应式宽度排版 (`UpdateResponsiveLayout()`)**：
  * 左侧栏宽度在桌面正常尺寸下固定为 `420px`（对标 GrokBot 原生桌面端）；
  * 当窗口总宽度小于 `850px` 时，自动进入小屏自适应模式：
    ```csharp
    int targetSidebarW = 420;
    if (this.Width < 850) {
        targetSidebarW = Math.Max(260, Math.Min(420, (int)(this.Width * 0.35f)));
    }
    ```
  * 最大化还原按钮图标自动根据 `WindowState` 动态切换（`WinMax` $\leftrightarrow$ `WinRestore`）。

---

### 4.2 左侧会话导航栏 (`_sessionsPanel`)

#### 4.2.1 顶部搜索框与搜索圆纽
* **代码定位**：`src/MainForm.Designer.cs`（`_searchHeaderPanel`, `_searchBoxContainer`, `_txtSearch`, `_btnSearchToggle`）
* **修改搜索框胶囊尺寸与圆角**：
  * 容器位置与大小：`_searchBoxContainer.Size = new Size(boxW, 42);`
  * 胶囊圆角：`DrawRoundedRectangle(..., 21);`（21px 为 42px 高度的一半，呈现完美药丸形）
  * 边框色：`GrokTheme.Border` (`#2C2C32`)，底色：`GrokTheme.Level2` (`#202024`)；
  * 右侧搜索圆纽：直径 46px，底色 `GrokTheme.Level2`，悬停 `GrokTheme.Level3Hot`。

#### 4.2.2 头部 Hero Profile 卡片与大头像 (`MainForm.Tray.cs`)
* **代码定位**：`src/MainForm.Tray.cs` 的 `BtnAvatar_Paint`
* **修改卡片高度**：`_heroProfilePanel.Height = 175;`
* **修改 Mascot 头像大小与颜色**：
  * 头像矩形：`Rectangle avRect = new Rectangle(avX, avY, 82, 82);`；
  * 默认底色：`GrokTheme.AccentCoral`（珊瑚红 `#FC2C3E`）；
  * 下方昵称排版：`12.0pt Microsoft YaHei UI Regular` 配合 `GrokTheme.PrepareSoftText(g)` 灰阶平滑抗锯齿。

#### 4.2.3 会话列表自绘 (`SessionListBox.cs` & `MainForm.Chat.cs`)
* **代码定位**：`src/UI/Controls/SessionListBox.cs` 与 `src/MainForm.Chat.cs`（`ListSessions_DrawItem`）
* **修改度量常数**：
  * 行高：`GrokTheme.SessionItemHeight` (72px)；
  * 卡片水平外边距：`GrokTheme.SessionCardMarginX` (17px)；
  * 卡片垂直外边距：`GrokTheme.SessionCardMarginY` (5px)；
  * 卡片圆角：`GrokTheme.SessionCardRadius` (14px)；
  * 头像直径：`GrokTheme.SessionAvatarSize` (48px)。
* **修改卡片颜色**：
  * 选中卡片底色：`GrokTheme.SessionSelected` (`#7946D7` 魅紫)；
  * 悬停底色：`GrokTheme.SessionHover` (`#222226`)；
  * 未读数角标：`DrawUnreadBadge` 中定义，高度 18px，底色 `#FC4455`。

#### 4.2.4 自绘极简暗灰浮动滑块 (Zero White Scrollbar)
* **代码定位**：`src/UI/Controls/SessionListBox.cs`
* **滑块行为与外观**：
  * 静止态：4px 宽度，深灰半透明 `Color.FromArgb(120, 56, 56, 64)`，全圆角胶囊；
  * 悬停/拖拽态：自动变宽为 5px，提亮为高对比浅暗灰 `Color.FromArgb(200, 100, 100, 112)`；
  * 无需滚动时自动 100% 隐藏，彻底屏蔽原生白底滚动条。

#### 4.2.5 底部用户圆形徽标与快捷菜单 (`GrokSettingsMenuForm.cs`)
* **代码定位**：`src/MainForm.Designer.cs`（`_btnBottomUser`）与 `src/UI/Controls/GrokSettingsMenuForm.cs`
* **徽标大小与位置**：`Size = new Size(54, 54); Location = new Point(24, 6);`
* **点击事件**：左键单击弹出 `GrokSettingsMenuForm` 快捷菜单；右键直接呼出完整全局设置中心 `ShowSettingsDialog(0)`。

---

### 4.3 右侧主聊天视口区 (`_chatPanel`)

#### 4.3.1 顶部标题药丸栏与无边框窗体控制钮
* **代码定位**：`src/MainForm.Designer.cs`（`_chatHeader`）
* **居中浮动大药丸**：
  * 药丸高度：`pillH = 76;`，圆角半径 `38px`，底色 `#202022`；
  * 标题文字：`20.0pt Bold`，颜色 `GrokTheme.TextPill`；
  * 右上角控制按钮：`Size = new Size(56, 64);`，紧贴窗口最右上角，关闭叉悬停经典红。

#### 4.3.2 文件传输进度横幅 (`MainForm.Files.cs`)
* **代码定位**：`src/MainForm.Designer.cs` 与 `src/MainForm.Files.cs`
* **外观修改**：高度 44px，底色 `#18181A`，内含平滑进度条 `_progressBar` 与提示标签 `_lblProgressInfo`。

#### 4.3.3 核心聊天流自绘 (`WeChatChatView.cs`)
* **代码定位**：`src/UI/Controls/WeChatChatView.cs`
* **气泡底色与边框**：
  * 本人发送气泡：`GrokTheme.SessionSelected` (`#7946D7` 魅紫)，文字纯白；
  * 对方发送气泡：`GrokTheme.PeerBubble` (`#1E1E21` 炭黑)，边框 `#2C2C32`，文字 `GrokTheme.TextPrimary`；
* **气泡内边距与换行**：
  * 本人内边距：水平 24px，垂直 18px；对方内边距：水平 32px，垂直 24px；
  * 行间距加量：`GrokTheme.ChatLineSpacingExtra` (5f)；
* **文件卡片度量**：宽度 340px，高度 96px，圆角 12px。

#### 4.3.4 底部浮动输入胶囊与发送圆纽
* **代码定位**：`src/MainForm.Designer.cs`（`_bottomSendPanel`, `_inputPanel`, `_btnSendWave`, `_txtInput`）
* **外部容器定位**：`Height = 90; Padding = new Padding(28, 4, 28, 22);`
* **胶囊外框**：净高 64px，圆角 32px，底色 `GrokTheme.Level2`，边框 `GrokTheme.Border`（**严禁发光蓝圈**）；
* **胶囊内元素**：
  * 左侧 `(+)` 附件圆纽：38×38px 正圆，底色 `#2A2A2E`；
  * 中间单行文本框：起点 `x = 60px`，`11.0pt Regular`，前景色 `#F2F2F6`；
  * 右侧 `(↑)` 魅紫发送圆纽：44×44px 正圆，底色 `#8E64DC`，内部绘制纯白向上矢量箭头。

---

### 4.4 系统托盘、未读多会话悬浮预览 (`GrokTrayPreviewPopup.cs`)

**文件路径**：`src/UI/Controls/GrokTrayPreviewPopup.cs` 与 `src/MainForm.Tray.cs`
* **弹出契约**：当有未读消息且托盘闪烁时，鼠标悬停于系统托盘图标上方自动无焦点弹出（`ShowWithoutActivation`）；
* **视觉度量**：
  * 卡片宽度：`320px`；外边框圆角：`12px`；背景色：`#1C1C20`；
  * 头部 (Header)：高度 40px，左侧“未读消息”，右侧魅紫未读总数药丸（如 `[3 条未读]`）；
  * 会话行 (Item Rows)：单行高度 56px，包含 36×36px Mascot 头像、联系人昵称、未读条数标签、右侧时间戳、单行消息截断摘要；
  * 底部 (Footer)：高度 28px，居中辅助文字“点击任意条目立即查看 ›”；
* **边界安全反转**：采用 `Screen.FromPoint` 智能检测任务栏位于底部、顶部、左侧或右侧，自动翻转朝向，确保 100% 完整显示在可见屏幕内。

---

## 五、 第四层：全局设置中心与 8 大子面板 (`GrokSettingsDialog` & `SettingsPanels`)

设置中心在 DIR-005 中已完成完全解耦：主窗口降维为纯导航容器，具体业务卡片下沉到 8 个独立子面板。

### 5.1 设置窗体外壳与左侧导航栏 (`GrokSettingsDialog.cs`)

**文件路径**：`src/UI/Controls/GrokSettingsDialog.cs`
* **基准尺寸**：`1260 × 820`（大屏舒适尺寸）或 `920 × 640`，自适应屏幕尺寸；
* **左侧导航栏**：固定宽度 `SIDEBAR_W = 270;`，底色 `GrokTheme.Level0` (`#0E0E0E`)；
* **导航项目自绘**：
  * 分组标签（“设置”、“功能”、“网络与系统”、“关于”）：字号 9.0pt Bold，颜色 `GrokTheme.TextPlaceholder`；
  * 导航 Tab 卡片：高度 38px，圆角 8px；激活底块 `GrokTheme.Level3` (`#28282C`)，文字纯白加粗；悬停底块 `GrokTheme.Level2` (`#202024`)；左侧配 16×16px 矢量细线图标；
* **视口滚动条**：继承 Zero White Scrollbar 契约，使用自绘 4px 悬浮暗灰滑块。

---

### 5.2 设置子面板抽象基类 (`SettingsPanelBase.cs`)

**文件路径**：`src/UI/Controls/SettingsPanels/SettingsPanelBase.cs`  
所有子面板均继承自 `SettingsPanelBase`。基类提供了一套快速构建标准化 Grok 风格卡片的工厂方法：

| 工厂方法 | 生成控件与规范 |
| :--- | :--- |
| `CreateSectionHeader(text, y)` | 9.0pt Bold 分组副标题（颜色 `#5E5E66`），距卡片顶部 18px |
| `CreateCardPanel(w, h)` | 统一 14px 圆角、`#1C1C1F` 底色、`#2C2C32` 1px 微光边框的标准内容卡片 |
| `CreateDropdownPill(text, minW, h, onClick)` | 28px 高度全圆角下拉药丸按钮，悬停加亮，右侧带 `∨` 箭头 |
| `CreateActionLink(text, w, h, onClick)` | 文本链接药丸按钮，用于“修改昵称 >”、“更改目录 >” |
| `AutoFitHeight()` | 遍历所有子控件自动计算最大底部坐标并设置面板自身高度，供父视口计算滚动行程 |

---

### 5.3 真实 8 大子面板具体修改索引

所有面板文件均存放在 `src/UI/Controls/SettingsPanels/`，以下是源码中的真实卡片与项目结构：

#### 1. `GeneralSettingsPanel.cs` (通用设置)
* **分类 1：“常规”卡片 (高 216px)**：
  * Row 1：语言下拉药丸（跟随系统 简体中文）；
  * Row 2：链接打开方式下拉药丸（智能打开）；
  * Row 3：字体大小阶梯滑块（`SteppedSlider`: 小、默认、中、大、特大，调节 `AppSettings.UiFontSize`）；
  * Row 4：默认文件接收目录展示卡片。
* **分类 2：“权限与偏好”卡片 (高 216px)**：
  * Row 1：托盘未读悬浮预览开关（`ModernToggleSwitch`）；
  * Row 2：自动接收好友文件开关；
  * Row 3：运行时防止休眠与后台驻留开关；
  * Row 4：网络协议通道下拉药丸。
* **分类 3：“支持与关于”卡片 (高 108px)**：
  * Row 1：关于 LanChat（版本号与检查更新按钮）；
  * Row 2：意见与问题反馈按钮（呼出意见反馈弹窗）。

#### 2. `ProfileSettingsPanel.cs` (个人名片)
* **分类 1：“个人名片”卡片 (高 82px)**：
  * 48px Mascot 头像；
  * 11.0pt 加粗当前昵称；
  * 主机名与局域网在线状态；
  * 右侧“修改昵称 >”操作链接。
* **分类 2：“设备标识”卡片 (高 108px)**：
  * 节点识别码 (Node UUID) 与复制按钮；
  * 操作系统环境与体系结构展示。

#### 3. `AppearanceSettingsPanel.cs` (外观偏好)
* **分类 1：“外观偏好”卡片 (高 108px)**：
  * 主题模式下拉药丸（深色 Grok Dark）；
  * 强调色选择下拉药丸（魅紫 `#8E64DC`）。
* **分类 2：“字体与排版”卡片 (高 216px)**：
  * 主界面字体样式选择下拉药丸；
  * 主界面字体大小缩放阶梯滑块；
  * 聊天框字体样式选择下拉药丸；
  * 聊天框字体大小缩放阶梯滑块。

#### 4. `ShortcutsSettingsPanel.cs` (快捷键)
* **分类 1：“快捷操作”卡片 (高 216px)**：
  * 发送消息快捷键徽章芯片（`Enter`）；
  * 多行换行输入快捷键徽章芯片（`Shift + Enter`）；
  * 快速搜索联系人快捷键徽章芯片（`Ctrl + F`）；
  * 清空当前聊天快捷键徽章芯片（`Ctrl + L`）。

#### 5. `FileTransferSettingsPanel.cs` (文件传输)
* **分类 1：“文件偏好”卡片 (高 162px)**：
  * Row 1：自动接收好友文件开关；
  * Row 2：接收文件默认存储目录路径与“更改目录 >”操作链接；
  * Row 3：64KB 分块极速传输校验与 LANC 协议说明。

#### 6. `ChatHistorySettingsPanel.cs` (聊天记录管理)
* **分类 1：“聊天记录管理”卡片 (高 162px)**：
  * Row 1：聊天记录存储目录与“更改目录 >”操作链接（支持数据平滑自动迁移）；
  * Row 2：自动加载历史消息开关；
  * Row 3：“清空全部历史记录”操作链接（红字警示）。

#### 7. `NetworkDeviceSettingsPanel.cs` (物理网络与设备)
* **分类 1：“物理网络与设备”卡片 (高 162px)**：
  * Row 1：物理以太网 / Wi-Fi IP 绑定与端口指示；
  * Row 2：TUN / TAP 虚拟网卡穿透过滤指示；
  * Row 3：多网卡并发单播探测加速指示。

#### 8. `AboutSettingsPanel.cs` (关于产品)
* **分类 1：“关于产品”卡片 (高 140px)**：
  * 56px Mascot 软件大图标；
  * 软件名与架构说明；
  * 右侧“检查更新 >”药丸操作按钮。
* **分类 2：“支持与反馈”卡片 (高 64px)**：
  * 意见与问题反馈行与“立即反馈 >”操作链接。

---

## 六、 第五层：9 大独立模态与悬浮弹窗修改指南

所有独立模态弹窗均位于 `src/UI/Controls/`，全部继承自 `GrokModalFormBase`，按钮全部使用自绘 `GrokButton`。

### 6.1 统一消息确认框 (`GrokMessageBox.cs`)
* **基准尺寸**：`460 × 260`（`GrokTheme.SizeMessageBox`）；
* **调用方式**：`GrokMessageBox.Show(owner, "消息正文", "标题", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);`
* **修改排版**：左侧状态图标（问号、感叹号、错误叉）自绘在 `(24, 56)`；底部按钮容器自动根据枚举动态放置 `GrokButton`。

### 6.2 修改昵称窗口 (`GrokChangeNickForm.cs`)
* **基准尺寸**：`440 × 260`（`GrokTheme.SizeChangeNick`）；
* **输入框**：`_txtNick`（无边框，底色 `#121212`，字体 10.5pt）；底部配 Secondary 取消按钮与 Primary 保存按钮。

### 6.3 关于软件窗口 (`GrokAboutForm.cs`)
* **基准尺寸**：`440 × 560`（`GrokTheme.SizeAbout`）；
* **头像展示**：顶部居中自绘 96×96px 大 Mascot 头像，绘制软件版本号与 .NET 4.0 运行时环境说明。

### 6.4 意见反馈窗口 (`GrokFeedbackForm.cs`)
* **基准尺寸**：`520 × 460`（`GrokTheme.SizeFeedback`）；
* **多行输入卡片**：高度 160px 的圆角深灰卡片，内嵌多行文本框；底部单行联系方式胶囊。

### 6.5 软件更新窗口 (`GrokUpdateForm.cs`)
* **基准尺寸**：`520 × 440`（`GrokTheme.SizeUpdate`）；
* **更新日志展示区**：带有自绘极简暗灰滑块的多行平滑滚动框；操作按钮为“以后再说”与“立即更新”。

### 6.6 新建节点窗口 (`GrokCreateNodeForm.cs`)
* **基准尺寸**：`680 × 620`（`GrokTheme.SizeCreateNode`）；
* **顶部头像生成器**：支持点击切换 6 种不同配色的大头像与节点名称表单。

### 6.7 连接节点窗口 (`GrokConnectNodeForm.cs`)
* **基准尺寸**：`760 × 600`（`GrokTheme.SizeConnectNode`）；
* **顶部搜索胶囊**：40px 高度自绘搜索框与扫描刷新药丸按钮；
* **筛选芯片组**：4 个 32px 药丸胶囊（“全部”、“局域网在线”、“常用节点”、“手动直连”）；
* **双列卡片流**：338×76px 双列节点卡片，内嵌 48px Mascot 头像与连接药丸。

### 6.8 齿轮设置快捷弹出菜单 (`GrokSettingsMenuForm.cs`)
* **基准尺寸**：`342 × 200`；16px 圆角无边框窗体，CS_DROPSHADOW 外阴影；
* **菜单行高**：单项 44px；
* **包含项目**：设置、修改我的昵称（右侧带当前昵称摘要）、清空聊天记录、退出登录（危险项）。

### 6.9 托盘未读多会话悬浮预览卡片 (`GrokTrayPreviewPopup.cs`)
* **基准尺寸**：`320 × (40 + N*56 + 28)`；12px 圆角无边框置顶窗体；
* **包含项目**：顶部总未读数药丸、多会话行（Mascot 头像、昵称、单项未读条数、时间戳、最新消息摘要）、底部外跳指引。

---

## 七、 第六层：业务控制层与数据联动 (Controllers & Presenters)

在 DIR-005 重构中，UI 视图层不再直接操纵底层网络或持有大段业务状态，而是通过轻量 MVP 控制器协同：

```
UI 视图层 (MainForm / ChatView / SessionListBox)
   │               ▲
   │ 调用操作       │ 驱动事件 (SafeInvoke 跨线程调度)
   ▼               │
Controllers & Presenters (src/Controllers/)
   │
   ▼
底层网络服务与持久化存储 (NetworkService / SessionManager / ChatHistoryService)
```

### 7.1 会话业务编排 (`SessionPresenter.cs`)
* **职责**：
  * 维护 `FilteredSessionItems`（根据当前搜索词 `SearchFilter` 实时过滤群聊与好友）；
  * 维护 `SelectedSessionKey` 与 `SelectedPeer`；
  * 提供会话防抖切换 `SelectSession(key, peer)`（避免重复点击同个联系人引发聊天流重绘与闪烁）；
  * 调度未读清除：`ClearActiveChatUnread()`。
* **UI 联动修改点**：若需修改会话列表的排序规则或过滤算法，修改 `SessionPresenter.RefreshFilteredSessions()`。

### 7.2 聊天收发编排 (`ChatPresenter.cs`)
* **职责**：
  * 统一将输入文本打包为 `ChatMessage`，调度 `NetworkService.SendTextMessage`；
  * 派发 `MessageAppended` 事件，驱动 `WeChatChatView.AppendMessage` 即时呈现气泡；
  * 派发系统通知与全局托盘闪动调度。
* **UI 联动修改点**：若需修改消息发送前的过滤校验、系统提示语句格式，修改 `ChatPresenter`。

### 7.3 文件传输调度 (`FileTransferController.cs`)
* **职责**：
  * 文件发送队列装配与异步流式传输；
  * 传输进度更新（驱动 `MainForm.Files.cs` 的进度横幅）；
  * 文件接收完毕落盘，派发 `FileSent` 与 `FileReceived` 事件驱动聊天流呈现文件卡片。
* **UI 联动修改点**：若需修改传输进度刷新频率或传输完成后的提示行为，修改 `FileTransferController`。

---

## 八、 高频修改场景实战 Cookbook (常见需求秒改)

### 场景 1：全局一键更换主品牌色 (魅紫 → 极客蓝 / 翡翠绿)
* **需求**：将发送按钮、本人聊天气泡、会话选中态从原本的魅紫变成极客蓝。
* **修改位置**：`src/UI/Controls/GrokTheme.cs`
```csharp
// 找到 AccentPurpleDeep 与 SessionSelected，将 #7946D7 改为天蓝色 #116DD2
public static readonly Color AccentPurpleDeep = Color.FromArgb(17, 109, 210);  // 原 (121, 70, 215)
public static readonly Color AccentPurple = Color.FromArgb(77, 155, 255);      // 原 (142, 100, 220)
public static readonly Color SessionSelected = AccentPurpleDeep;
public static readonly Color ButtonPrimaryNormal = AccentPurpleDeep;
public static readonly Color ButtonPrimaryHover = AccentPurple;
```
* **效果**：重新编译后，主界面向上发送圆纽、右侧本人聊天气泡、左侧会话选中高亮、设置中心的主按钮全量同步变蓝！

---

### 场景 2：调整全局默认字体与字号阶梯
* **需求**：想把系统默认字体改为 `"Segoe UI"`，并将主正文字号从 11.0pt 调大到 12.0pt。
* **修改位置**：`src/UI/Controls/GrokTheme.cs`
```csharp
// 修改 FontFamily 默认回退值
public static string FontFamily
{
    get
    {
        return !string.IsNullOrEmpty(AppSettings.UiFontFamily) ? AppSettings.UiFontFamily : "Segoe UI";
    }
}

// 修改主文本字号
public static Font FontPrimary { get { return Font(12.0f, FontStyle.Regular); } } // 原 11.0f
```

---

### 场景 3：调整聊天气泡内边距（上下左右 4 方向独立精细排版）
* **需求**：觉得聊天气泡太空旷想要更紧凑，或字体变大后需要扩大上下/左右呼吸感。
* **修改位置**：`src/UI/Controls/GrokTheme.cs`
```csharp
// 调整本人气泡 4 方向内边距 (例如打造紧凑型气泡)
public const int ChatBubblePadSelfLeft = 12;   // 左内边距 (原 16)
public const int ChatBubblePadSelfRight = 12;  // 右内边距 (原 16)
public const int ChatBubblePadSelfTop = 8;     // 上内边距 (原 12)
public const int ChatBubblePadSelfBottom = 8;  // 下内边距 (原 12)

// 调整对方气泡 4 方向内边距
public const int ChatBubblePadPeerLeft = 14;   // 左内边距 (原 16)
public const int ChatBubblePadPeerRight = 14;  // 右内边距 (原 16)
public const int ChatBubblePadPeerTop = 10;    // 上内边距 (原 12)
public const int ChatBubblePadPeerBottom = 10; // 下内边距 (原 12)
```
> [!TIP]
> `WeChatChatView` 渲染引擎已彻底解耦，文字渲染起始点自动按 `padLeft` / `padTop` 动态定位，气泡总宽高自动按 `Left + Right` 与 `Top + Bottom` 扩展，彻底消除单侧偏心或不对称畸变。

---

### 场景 4：调整主窗体默认大小或左侧栏宽度
* **需求**：屏幕较小，希望默认启动窗口为 960×640，左侧会话列表变窄为 320px。
* **修改位置 1**：`src/UI/Controls/GrokTheme.cs`
```csharp
public static readonly Size SizeMainWindow = new Size(960, 640); // 原 1040, 720
```
* **修改位置 2**：`src/MainForm.Designer.cs`
```csharp
// 找到 InitializeComponent 中的 _sessionsPanel
_sessionsPanel.Width = 320; // 原 420

// 找到 UpdateResponsiveLayout 中的 targetSidebarW
int targetSidebarW = 320; // 原 420
```

---

### 场景 5：修改按钮默认圆角为直角或小圆角
* **需求**：团队喜欢偏硬朗的微圆角风格，希望标准按钮圆角从 8px 改为 4px。
* **修改位置**：`src/UI/Controls/GrokTheme.cs`
```csharp
public const int ButtonRadiusStandard = 4; // 原 8
```
全客户端所有 `GrokButton` 实例（若未手动指定 `CornerRadius`）将自动呈现 4px 微圆角。

---

### 场景 6：在全局设置中心中新增一个设置项或卡片行
* **需求**：在“通用设置”面板中新增一行开关配置“声音提示”。
* **修改位置**：`src/UI/Controls/SettingsPanels/GeneralSettingsPanel.cs`
```csharp
// 1. 调整卡片总高度（每加一行 54px，将卡片高度从 216 扩大到 270）
Panel card2 = CreateCardPanel(CARD_W, 270);

// 2. 在 card2.Paint 事件中追加文本行与分割线
g.DrawString("新消息声音提示", f, tb, new PointF(20, 216 + 9));
g.DrawString("收到局域网好友或广播消息时播放柔和提示音。", sf, sb, new PointF(20, 216 + 29));

// 3. 创建开关控件并添加到卡片容器
ModernToggleSwitch swSound = new ModernToggleSwitch(true, (val) => {
    // 保存配置项逻辑
});
swSound.Location = new Point(CARD_W - swSound.Width - 24, 216 + (54 - 22) / 2);
card2.Controls.Add(swSound);
```

---

### 场景 7：新增一个全新的自绘矢量图标并在界面中使用
* **需求**：新增一个音量喇叭图标并在按钮中渲染。
* **步骤 1**：在 `src/UI/Renderers/WeChatIcon.cs` 的 `WeChatIconType` 枚举中追加 `Volume`；
* **步骤 2**：在 `WeChatIcon.Draw` 方法中添加对应的 `case` 分支：
```csharp
case WeChatIconType.Volume:
    using (Pen p = new Pen(color, 1.8f))
    {
        p.StartCap = LineCap.Round;
        p.EndCap = LineCap.Round;
        p.LineJoin = LineJoin.Round;
        
        int cx = bounds.Left + bounds.Width / 2;
        int cy = bounds.Top + bounds.Height / 2;
        
        // 绘制喇叭多边形与声波弧线
        Point[] pts = new Point[] {
            new Point(cx - 6, cy - 3), new Point(cx - 3, cy - 3),
            new Point(cx + 2, cy - 7), new Point(cx + 2, cy + 7),
            new Point(cx - 3, cy + 3), new Point(cx - 6, cy + 3)
        };
        g.DrawPolygon(p, pts);
        g.DrawArc(p, cx + 1, cy - 5, 8, 10, -45, 90);
    }
    break;
```
* **步骤 3**：在任何需要的位置直接使用：
```csharp
WeChatIconButton btnVolume = new WeChatIconButton(WeChatIconType.Volume) {
    Size = new Size(36, 36),
    IsCircle = true
};
```

---

### 场景 8：修改无边框窗体拖拽缩放热区边距 (`FormResizeMessageFilter.cs`)
* **需求**：高分辨率 4K 屏幕下，鼠标贴近边缘触发缩放的热区太窄，希望调宽。
* **修改位置**：`src/UI/Controls/FormResizeMessageFilter.cs`
```csharp
// 将边距从 8px 调大为 12px
private const int BORDER_PADDING = 12; // 原 8
```

---

## 九、 语法约束、单文件限制与编译验证契约

在修改任何 UI 代码时，必须严格遵守以下工程规范（违反将导致编译脚本失败）：

1. **C# 5.0 / .NET Framework 4.0 兼容约束**：
   * 严禁使用 C# 6.0+ 新语法（例如禁止使用字符串插值 `$"..."`，必须用 `string.Format(...)`；禁止使用空值传播符 `?.`；禁止在普通属性使用 `=>` 表达式体）；
   * 本工程依靠系统自带的原生 `csc.exe` 极速单文件编译，零第三方外部 NuGet 依赖。
2. **Zero White Scrollbar 绝对契约 (Rules §9)**：
   * 严禁给任何原生控件设置 `AutoScroll = true` 或引入原生 `VScrollBar`；
   * 任何具有溢出滚动的列表，必须使用现成的自绘暗灰滑块机制。
3. **单文件行数警戒线 (Rules §10)**：
   * 单个 `.cs` 文件达到 1000 行预警，1500 行绝对硬上限。添加新设置卡片时，请往对应的 `SettingsPanels/*.cs` 子面板编写，严禁往 `GrokSettingsDialog.cs` 或 `MainForm.cs` 堆砌代码。
4. **编译验证命令**：
   每次修改完 UI 代码后，打开终端运行：
   ```cmd
   Tools\Compile.bat
   ```
   输出必须为 **0 警告、0 错误**，并成功在 `release/` 目录生成 `LanChat.exe` 方为修改合格。
