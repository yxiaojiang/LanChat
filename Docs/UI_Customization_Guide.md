# LanChat 全客户端 UI 元素样式与界面修改指南 (UI Customization Guide)

> **适用版本**：LanChat v2.0.14+  
> **文档位置**：`Docs/UI_Customization_Guide.md`  
> **制定基准**：对标 `.claude/Rules.md` 第 9 条（GrokBot Design System & Zero White Scrollbar 契约）及 `Docs/GrokBot_UI_Design_System.md`。  
> **目标读者**：开发人员、UI 定制者。本文档提供全应用每一个窗口、容器、按钮、字体、颜色、圆角、间距与图标的精确修改位置与代码示例，确保任何人无需猜测即可自主调整界面样式。

---

## 目录

1. [样式体系架构总览与“三层修改法则”](#一-样式体系架构总览与三层修改法则)
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
3. [第二层：通用基础控件修改指南](#三-第二层通用基础控件修改指南)
   - [3.1 统一自绘按钮控件 (`GrokButton.cs`)](#31-统一自绘按钮控件-grokbuttoncs)
   - [3.2 无边框模态窗基类 (`GrokModalFormBase.cs`)](#32-无边框模态窗基类-grokmodalformbasecs)
   - [3.3 浮动暗黑右键菜单 (`GrokPopupMenu.cs`)](#33-浮动暗黑右键菜单-grokpopupmenucs)
   - [3.4 图标与控制按钮 (`WeChatButtons.cs`)](#34-图标与控制按钮-wechatbuttonscs)
   - [3.5 双缓冲防闪烁容器 (`DoubleBufferedPanel.cs`)](#35-双缓冲防闪烁容器-doublebufferedpanelcs)
4. [第三层：主窗口界面详细修改 (`MainForm*.cs`)](#四-第三层主窗口界面详细修改-mainformcs)
   - [4.1 左侧会话导航栏 (`_sessionsPanel`)](#41-左侧会话导航栏-_sessionspanel)
     - [4.1.1 顶部搜索框与搜索圆钮](#411-顶部搜索框与搜索圆钮)
     - [4.1.2 头部 Hero Profile 卡片与大头像](#412-头部-hero-profile-卡片与大头像)
     - [4.1.3 会话列表自绘 (`SessionListBox` & `ListSessions_DrawItem`)](#413-会话列表自绘-sessionlistbox--listsessions_drawitem)
     - [4.1.4 自绘极简暗灰浮动滑块 (Zero White Scrollbar)](#414-自绘极简暗灰浮动滑块-zero-white-scrollbar)
     - [4.1.5 底部用户圆形徽标与快捷菜单](#415-底部用户圆形徽标与快捷菜单)
   - [4.2 右侧主聊天视口区 (`_chatPanel`)](#42-右侧主聊天视口区-_chatpanel)
     - [4.2.1 顶部标题药丸栏与无边框窗体控制钮](#421-顶部标题药丸栏与无边框窗体控制钮)
     - [4.2.2 文件传输进度横幅](#422-文件传输进度横幅)
     - [4.2.3 核心聊天流自绘 (`WeChatChatView.cs`)](#423-核心聊天流自绘-wechatchatviewcs)
     - [4.2.4 底部浮动输入胶囊与发送圆钮](#424-底部浮动输入胶囊与发送圆钮)
5. [第四层：全局设置中心与子面板修改 (`GrokSettingsDialog` & `SettingsPanels`)](#五-第四层全局设置中心与子面板修改-groksettingsdialog--settingspanels)
   - [5.1 设置窗体外壳与左侧导航栏 (`GrokSettingsDialog.cs`)](#51-设置窗体外壳与左侧导航栏-groksettingsdialogcs)
   - [5.2 设置子面板抽象基类 (`SettingsPanelBase.cs`)](#52-设置子面板抽象基类-settingspanelbasecs)
   - [5.3 8 大子面板具体修改索引](#53-8-大致面板具体修改索引)
6. [第五层：7 大独立模态弹窗修改指南](#六-第五层7-大独立模态弹窗修改指南)
   - [6.1 统一消息确认框 (`GrokMessageBox.cs`)](#61-统一消息确认框-grokmessageboxcs)
   - [6.2 修改昵称窗口 (`GrokChangeNickForm.cs`)](#62-修改昵称窗口-grokchangenickformcs)
   - [6.3 关于软件窗口 (`GrokAboutForm.cs`)](#63-关于软件窗口-grokaboutformcs)
   - [6.4 意见反馈窗口 (`GrokFeedbackForm.cs`)](#64-意见反馈窗口-grokfeedbackformcs)
   - [6.5 软件更新窗口 (`GrokUpdateForm.cs`)](#65-软件更新窗口-grokupdateformcs)
   - [6.6 新建节点窗口 (`GrokCreateNodeForm.cs`)](#66-新建节点窗口-grokcreatenodeformcs)
   - [6.7 连接节点窗口 (`GrokConnectNodeForm.cs`)](#67-连接节点窗口-grokconnectnodeformcs)
7. [第六层：纯矢量 GDI+ 图标引擎修改 (`WeChatIcon.cs`)](#七-第六层纯矢量-gdi-图标引擎修改-wechaticoncs)
8. [高频修改场景实战 Cookbook (常见需求秒改)](#八-高频修改场景实战-cookbook-常见需求秒改)
   - [场景 1：全局一键更换主品牌色 (魅紫 → 极客蓝 / 翡翠绿)](#场景-1全局一键更换主品牌色-魅紫--极客蓝--翡翠绿)
   - [场景 2：调整全局默认字体与字号阶梯](#场景-2调整全局默认字体与字号阶梯)
   - [场景 3：调整聊天气泡内边距与最大宽度](#场景-3调整聊天气泡内边距与最大宽度)
   - [场景 4：调整主窗体默认大小或左侧栏宽度](#场景-4调整主窗体默认大小或左侧栏宽度)
   - [场景 5：修改按钮默认圆角为直角或小圆角](#场景-5修改按钮默认圆角为直角或小圆角)
9. [语法约束与编译验证契约](#九-语法约束与编译验证契约)

---

## 一、 样式体系架构总览与“三层修改法则”

项目已将原本分散在各个 Form 和控件中的硬编码颜色、尺寸、字体收敛为规范的**分层管理架构**。修改 UI 时遵循以下三层法则：

```
+-----------------------------------------------------------------------------------+
| 第一层：全局设计令牌中枢 (Single Source of Truth)                                   |
|   文件：src/UI/Controls/GrokTheme.cs                                               |
|   管理：背景色阶(Level0~3)、文字颜色、品牌强调色、基准窗体尺寸、全局字体、间距与圆角   |
+-----------------------------------------------------------------------------------+
                                         │
                                         ▼
+-----------------------------------------------------------------------------------+
| 第二层：统一自绘公共控件体系 (Base Components)                                      |
|   控件：GrokButton, GrokModalFormBase, GrokPopupMenu, DoubleBufferedPanel, Icons   |
|   管理：按钮 4 种角色、模态窗 16px 圆角+阴影+拖拽外壳、暗黑右键菜单、矢量图标绘制引擎 |
+-----------------------------------------------------------------------------------+
                                         │
                                         ▼
+-----------------------------------------------------------------------------------+
| 第三层：视图层与业务面板 (Views & Dialogs)                                          |
|   主窗体：MainForm (Designer/Chat/Files/Tray)                                      |
|   核心视图：WeChatChatView (聊天流), SessionListBox (会话列表)                       |
|   设置中心：GrokSettingsDialog + 8 大独立子面板 (SettingsPanels/*.cs)              |
|   独立弹窗：GrokMessageBox, GrokAboutForm, GrokCreateNodeForm 等 7 大模态窗        |
+-----------------------------------------------------------------------------------+
```

### 修改定位口诀：
1. **想改“全局通用属性”**（如主色调、字体、背景深浅、弹窗默认大小）：**只改 `GrokTheme.cs`**，全客户端自动联动。
2. **想改“某一类控件的外观动效”**（如所有按钮的悬停亮度、所有弹窗的右上角关闭叉）：**修改对应的公共控件**（如 `GrokButton.cs`、`GrokModalFormBase.cs`）。
3. **想改“特定界面的专属排版与布局”**（如主窗口搜索框位置、聊天气泡特定细节、某个弹窗独有的卡片）：**进入具体的窗体或子面板文件修改**。

---

## 二、 第一层：全局设计令牌中枢修改 (`GrokTheme.cs`)

**文件路径**：`src/UI/Controls/GrokTheme.cs`  
这是整个客户端样式的“心脏”。所有常量均使用 C# 5.0 / .NET Framework 4.0 语法维护。

### 2.1 背景层级与纵深色彩 (Elevation)

客户端采用 5 级暗黑纵深分层矩阵，依靠微妙的明暗色阶呈现立体感，严禁在暗黑底色上画生硬的白色反光边框。

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
public static readonly Color InputPlus = Color.FromArgb(42, 42, 46);        // #2A2A2E 输入框左侧 (+) 圆钮底色
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
public const int ChatBubblePadSelfH = 24;         // 本人消息气泡水平内边距
public const int ChatBubblePadSelfV = 18;         // 本人消息气泡垂直内边距
public const int ChatBubblePadPeerH = 32;         // 对方消息气泡水平内边距
public const int ChatBubblePadPeerV = 24;         // 对方消息气泡垂直内边距
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
| `PrepareSoftText(Graphics g)` | 设置 `TextRenderingHint.AntiAlias`，获得类似 Electron/Chromium 的平滑灰阶文字，杜绝 Windows 传统 ClearType 产生彩色杂边 |
| `DrawRoundedRectangle(g, pen/brush, bounds, radius)` | 纯数学绘制无断点、无突兀接缝的圆角矩形 |
| `DrawUiTextLeft(g, text, font, color, x, y, w, h)` | 左对齐、垂直严格数学居中绘制文本，带截断省略号 |
| `DrawUiTextCentered(g, text, font, color, bounds)` | 矩形区域水平、垂直完全居中绘制文本 |
| `SanitizeUiText(text)` | 自动剔除会导致 YaHei UI 显示豆腐块 `□` 的 Emoji 代理对与私有区符号 |
| `MeasureUiText(g, text, font)` | 预先精确测量文本所需宽高像素，用于动态气泡和标签自适应 |

---

## 三、 第二层：通用基础控件修改指南

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
* **修改按钮内边距与字体**：在 `GrokButton.ApplyRoleDefaults()`（行 105~150）中调整 `this.Font` 和 `this.Padding`。

---

### 3.2 无边框模态窗基类 (`GrokModalFormBase.cs`)

**文件路径**：`src/UI/Controls/GrokModalFormBase.cs`  
全客户端所有独立弹窗（除复杂的主设置窗口外）**100% 继承自此基类**。它自动接管了：
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

#### 2. 如何修改弹窗外壳细节？
* **调整右上角关闭叉位置与大小**：修改 `GrokTheme.CloseButtonSize` (28) 和 `GrokTheme.CloseButtonMargin` (16)；
* **修改弹窗外边框颜色**：在 `GrokModalFormBase.OnPaint`（行 165~190）修改 `GrokTheme.Border`；
* **修改弹窗背景色**：修改构造函数中的 `this.BackColor = GrokTheme.Level1;`。

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

#### 1. `WeChatIconButton`（主界面最常用的圆纽与窗体控制钮）
* **属性说明**：
  * `IconType`：枚举 `WeChatIconType`（详见第七节）；
  * `IsCircle`：`true` 时为正圆形（如输入框发送键、附件加号键、搜索放大圆纽）；`false` 为圆角或贴边矩形（如窗口最小化/关闭叉）；
  * `FillColor`：静止态底色；
  * `HoverFillColor`：鼠标悬停底色；
  * `IconColor`：矢量图标线条颜色；
  * `IsWindowClose`：若是窗口右上角关闭按钮，悬停自动呈现系统经典危险红 `#E81123`。

#### 2. `WeChatNavButton`（导航栏图标按钮）
* 支持激活状态（`IsActive`，激活呈现微信绿 `#00A361` 或魅紫）、悬停半透明底块与跟随暗黑 Tooltip 提示。

---

### 3.5 双缓冲防闪烁容器 (`DoubleBufferedPanel.cs`)

**文件路径**：`src/UI/Controls/DoubleBufferedPanel.cs`  
开启了 `OptimizedDoubleBuffer`、`AllPaintingInWmPaint`、`UserPaint` 与 `ResizeRedraw`，并压制了 `WM_ERASEBKGND`，在包含大量子控件、频繁滚屏或重绘的容器中（如设置视口、聊天视口、主窗体分栏）必须使用此类替代系统原生 `Panel`，杜绝任何白底闪烁。

---

## 四、 第三层：主窗口界面详细修改 (`MainForm*.cs`)

主窗体文件已在 DIR-005 中进行了规范拆分，界面排版与视觉控件集中在 **`src/MainForm.Designer.cs`** 与 **`src/MainForm.Chat.cs`**。

```
+---------------------------------------------------------------------------------------------------------+
| [420px 固定宽：_sessionsPanel, #0E0E0E]       | [自适应填充满：_chatPanel, #121212]                      |
|                                              |                                                         |
| 1. 顶部搜索栏 (_searchHeaderPanel, 高 60px)    | 1. 顶部浮动标题药丸栏 (_chatHeader, 高 96px)             |
|    - 胶囊搜索框 (_searchBoxContainer, 高 42px) |    - 76px 居中大药丸 (48px 头像 + 20pt 粗体标题)          |
|    - 46px 搜索放大圆纽 (_btnSearchToggle)      |    - 右上角窗体控制按钮 (— □ ×, 各 56×64px)              |
|                                              | 2. 文件进度横幅 (_progressPanel, 默认隐藏)               |
| 2. 头部 Hero Profile 卡片 (高 175px)          | 3. 聊天流视口 (_chatView: WeChatChatView)                |
|    - 82×82px 珊瑚红 Mascot 头像 + 在线蓝点    |    - 本人紫色气泡 / 对方深灰气泡                          |
|    - 居中明亮昵称 (11.0pt Bold)               |    - 4px 极简自绘暗灰滑块                                |
|                                              | 4. 底部浮动输入胶囊 (_bottomSendPanel, 高 90px)          |
| 3. 会话列表 (_listSessions: SessionListBox)  |    - 64px 浮动药丸 (圆角 32px, 外距 28px/22px)           |
|    - 72px 行高, 14px 圆角卡片                 |    - 左侧 36px (+) 附件圆钮                              |
|    - 全员群聊置顶项 / 好友联系人               |    - 单行暗黑输入框 (11.0pt #F2F2F6)                     |
|                                              |    - 右侧 44px 魅紫 (↑) 发送圆钮                         |
| 4. 底部工具栏 (_leftBottomBar, 高 84px)       |                                                         |
|    - 54×54px 用户圆形文字徽标 (_btnBottomUser)|                                                         |
+---------------------------------------------------------------------------------------------------------+
```

---

### 4.1 左侧会话导航栏 (`_sessionsPanel`)

#### 4.1.1 顶部搜索框与搜索圆钮
* **代码定位**：`MainForm.Designer.cs`（行 125~211）
* **修改搜索框胶囊尺寸与圆角**：
  * 容器位置与大小：`_searchBoxContainer.Size = new Size(330, 42);`
  * 胶囊圆角：`DrawRoundedRectangle(..., 21);`（21px 为 42px 高度的一半，呈现完美药丸形）
  * 边框色：`Color.FromArgb(44, 44, 50)` (`GrokTheme.Border`)
  * 底色：`Color.FromArgb(32, 32, 36)` (`GrokTheme.Level2`)
* **修改搜索框文字与字号**：
  * 文字颜色：`_txtSearch.ForeColor = GrokTheme.TextPrimary;`
  * 字体：`_txtSearch.Font = new Font("Microsoft YaHei UI", 10.0f);`
* **修改右侧搜索圆钮**：
  * 直径：`int btnSize = 46;`，位置在右侧外距 16px；
  * 底色：`FillColor = GrokTheme.Level2`，悬停 `HoverFillColor = GrokTheme.Level3Hot`。

#### 4.1.2 头部 Hero Profile 卡片与大头像
* **代码定位**：`MainForm.Designer.cs`（行 214~224）及 `MainForm.Chat.cs` 中的 `BtnAvatar_Paint`
* **修改卡片高度**：`_heroProfilePanel.Height = 175;`
* **修改 Mascot 头像大小与颜色**：
  * 在 `BtnAvatar_Paint` 中：头像矩形 `Rectangle avatarRect = new Rectangle(avX, avY, 82, 82);`；
  * 默认底色：`Color.FromArgb(252, 44, 62)`（`GrokTheme.AccentCoral` 珊瑚红）；
  * 右下角在线小蓝点：直径 16px，底色 `Color.FromArgb(77, 155, 255)` (`GrokTheme.AccentBlue`)；
  * 下方昵称字号：`Font(11.0f, FontStyle.Bold)`，颜色 `GrokTheme.TextTitle`。

#### 4.1.3 会话列表自绘 (`SessionListBox` & `ListSessions_DrawItem`)
* **代码定位**：`MainForm.Designer.cs`（行 278~294）与 `MainForm.Chat.cs`（行 169~350）
* **修改会话行高与卡片外边距**：
  * 行高：`GrokTheme.SessionItemHeight = 72;`
  * 卡片水平缩进：`GrokTheme.SessionCardMarginX = 17;`（若想贴边可改为 4~8）
  * 卡片垂直缩进：`GrokTheme.SessionCardMarginY = 5;`
  * 卡片圆角：`GrokTheme.SessionCardRadius = 14;`
* **修改选中态与置顶态颜色**：
  * 选中卡片底色：`GrokTheme.SessionSelected` (`#7946D7` 魅紫)；
  * 选中卡片外描边：`Color.FromArgb(145, 95, 240)`；
  * 置顶项底色（未选中时）：`Color.FromArgb(24, 21, 32)`，边框 `#2A2438`；
  * 悬停底色：`GrokTheme.SessionHover` (`#222226`)。
* **修改头像与文字排版**：
  * 头像直径：`GrokTheme.SessionAvatarSize = 48;`，左边距 `GrokTheme.SessionAvatarPaddingLeft = 30;`；
  * 标题位置：`textX = r.Left + 100; titleY = r.Top + 14;`，字号 `11.0f Bold`；
  * 摘要位置：`subY = r.Top + 38;`，字号 `9.5f Regular`，颜色 `GrokTheme.TextSubtitle`；
  * 未读数红点：在 `DrawUnreadBadge` 中定义，高度 18px，底色 `Color.FromArgb(255, 68, 85)`。

#### 4.1.4 自绘极简暗灰浮动滑块 (Zero White Scrollbar)
* **代码定位**：`src/UI/Controls/SessionListBox.cs`（行 80~240）
* **滑块行为与外观**：
  * 彻底禁用了 Windows 原生白底 `WS_VSCROLL`；
  * 静止态：4px 宽度，深灰半透明 `Color.FromArgb(120, 56, 56, 64)`，全圆角胶囊；
  * 悬停/拖拽态：自动变宽为 5px，提亮为高对比浅暗灰 `Color.FromArgb(200, 100, 100, 112)`；
  * 内容无需滚动时自动 100% 隐藏。

#### 4.1.5 底部用户圆形徽标与快捷菜单
* **代码定位**：`MainForm.Designer.cs`（行 233~276）
* **徽标大小与位置**：`Size = new Size(54, 54); Location = new Point(24, 6);`
* **徽标内部文字**：截取当前昵称前两位，字体 `Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold)`，颜色 `GrokTheme.TextPill`；
* **点击事件**：左键单击弹出 `GrokSettingsMenuForm` 快捷菜单；右键直接呼出完整全局设置中心 `ShowSettingsDialog(0)`。

---

### 4.2 右侧主聊天视口区 (`_chatPanel`)

#### 4.2.1 顶部标题药丸栏与无边框窗体控制钮
* **代码定位**：`MainForm.Designer.cs`（行 310~406）
* **修改居中浮动大药丸**：
  * 药丸高度：`pillH = 76;`，圆角半径 `38px`；
  * 底色：`Color.FromArgb(32, 32, 34)`，边框 `Color.FromArgb(46, 46, 50)`；
  * 内部 Mascot 头像：48×48px，群聊魅紫，单聊橙黄；
  * 标题文字：`Font("Microsoft YaHei UI", 20.0f, FontStyle.Bold)`，颜色 `GrokTheme.TextPill`。
* **修改右上角无边框窗口按钮 (— □ ×)**：
  * 单个按钮尺寸：`Size = new Size(56, 64);`（紧贴窗口最右上角）；
  * 图标颜色：`Color.FromArgb(205, 205, 210)`；
  * 悬停背景：常规为 `Color.FromArgb(55, 55, 58)`，关闭叉自动呈现经典红。

#### 4.2.2 文件传输进度横幅
* **代码定位**：`MainForm.Designer.cs`（行 408~436）
* **外观修改**：高度 44px，底色 `Color.FromArgb(24, 24, 26)`，内含 10px 高度的平滑进度条 `_progressBar` 与提示标签 `_lblProgressInfo`。

#### 4.2.3 核心聊天流自绘 (`WeChatChatView.cs`)
* **代码定位**：`src/UI/Controls/WeChatChatView.cs`
* **气泡底色与边框**（行 745~747）：
  * 本人发送气泡：`GrokTheme.SessionSelected` (`#7946D7` 魅紫)，文字纯白；
  * 对方发送气泡：`GrokTheme.PeerBubble` (`#1E1E21` 炭黑)，边框 `GrokTheme.Border` (`#2C2C32`)，文字 `GrokTheme.TextPrimary`；
  * 文本选中高亮色：`Color.FromArgb(170, 70, 115, 235)`（半透明深蓝）。
* **气泡内边距与换行**（行 648~665）：
  * 本人内边距：水平 `GrokTheme.ChatBubblePadSelfH` (24px)，垂直 `ChatBubblePadSelfV` (18px)；
  * 对方内边距：水平 `GrokTheme.ChatBubblePadPeerH` (32px)，垂直 `ChatBubblePadPeerV` (24px)；
  * 行间距加量：`GrokTheme.ChatLineSpacingExtra` (5f)。
* **文件卡片度量**（行 625~645）：
  * 宽度 `GrokTheme.ChatFileCardWidth` (340px)，高度 `GrokTheme.ChatFileCardHeight` (96px)；
  * 圆角 12px，包含左侧文件图标、中间文件名与大小、右侧操作药丸。
* **发件人与时间戳**（行 737~744）：
  * 群聊对方发件人昵称颜色：`Color.FromArgb(164, 132, 245)` (淡紫)；
  * 居中时间戳字号：`GrokTheme.ChatTimestampFontSize` (9.0pt)。
* **自绘滚动条**（行 900~960）：
  * 采用纯自绘 4px/5px 极简暗灰浮动胶囊，严禁使用原生滚动条。

#### 4.2.4 底部浮动输入胶囊与发送圆钮
* **代码定位**：`MainForm.Designer.cs`（行 447~575）
* **外部容器定位**：
  * `_bottomSendPanel.Height = 90;`
  * `Padding = new Padding(28, 4, 28, 22);`（左右各留出 28px 呼吸边距，底部悬浮 22px）
* **胶囊外框与底色**：
  * 高度：`_inputPanel.Height = 64;`（内边距留出后净高 64px）；
  * 圆角：`int radius = Math.Max(8, _inputPanel.Height / 2);`（半圆 32px 全圆角胶囊）；
  * 底色：`Color.FromArgb(32, 32, 36)` (`GrokTheme.Level2`)；
  * 边框：`Color.FromArgb(44, 44, 50)` (`GrokTheme.Border`，**严禁发光蓝圈**)。
* **胶囊内部元素**：
  * **左侧 `(+)` 附件圆钮**：`38×38px` 正圆，距左侧 13px 垂直居中，底色 `#2A2A2E`，悬停 `#38383E`，图标浅灰 `#A0A0A5`；点击呼出功能弹出菜单；
  * **中间单行无边框输入框 (`_txtInput`)**：起点 `x = 60px`，终点距发送键左侧 16px，垂直居中；字体 `11.0pt Regular`，前景色 `#F2F2F6`；未输入时自绘占位符（`#5E5E66` 38%明度）；
  * **右侧 `(↑)` 魅紫发送圆钮 (`_btnSendWave`)**：`44×44px` 正圆，距胶囊右缘 10px 垂直居中；底色 `Color.FromArgb(142, 100, 220)` (`#8E64DC`)，悬停 `#9D75EC`，内部绘制加粗纯白居中向上矢量细箭头。

---

## 五、 第四层：全局设置中心与子面板修改 (`GrokSettingsDialog` & `SettingsPanels`)

设置中心已完成 SRP 解耦重构，主窗口降维为纯导航容器，具体业务卡片下沉到 8 个独立子面板。

### 5.1 设置窗体外壳与左侧导航栏 (`GrokSettingsDialog.cs`)

**文件路径**：`src/UI/Controls/GrokSettingsDialog.cs`
* **基准尺寸**：`1260 × 820`（大屏舒适尺寸）或 `920 × 640`，在屏幕不足时自适应 `Screen.WorkingArea - 40`；
* **左侧导航栏**：固定宽度 `SIDEBAR_W = 270;`，底色 `GrokTheme.Level0` (`#0E0E0E`)；与右侧主内容区 `#121212` 仅依靠色差分割，**坚决不画竖向单线**；
* **导航项目自绘**：
  * 分组标签（如“设置”、“功能”、“关于”）：字号 9.5pt，颜色 `GrokTheme.TextPlaceholder`；
  * 导航 Tab 卡片：高度 44px，圆角 10px；激活底块 `GrokTheme.Level3` (`#28282C`)，文字纯白；悬停底块 `GrokTheme.Level2` (`#202024`)；左侧配细线矢量图标；
* **右侧视口滚动条**：继承 Zero White Scrollbar 契约，使用自绘 4px 悬浮暗灰滑块。

---

### 5.2 设置子面板抽象基类 (`SettingsPanelBase.cs`)

**文件路径**：`src/UI/Controls/SettingsPanels/SettingsPanelBase.cs`  
所有子面板均继承自 `SettingsPanelBase`。基类提供了一套快速构建标准化 Grok 风格卡片的工厂方法：

| 工厂方法 | 生成控件与规范 |
| :--- | :--- |
| `CreateSectionHeader(text, y)` | 11.0pt Regular 分组副标题（颜色 `#A1A1A6`），距卡片顶部 18px |
| `CreateCardPanel(w, h)` | 统一 12px 圆角、`#1C1C1F` 底色、`#2C2C32` 1px 微光边框的标准内容卡片 |
| `CreateDropdownPill(text, minW, h, onClick)` | 32px 高度全圆角下拉药丸按钮，悬停加亮，右侧带 `∨` 箭头 |
| `CreateSwitchToggle(initial, onToggled)` | 44×24px 极简自绘滑动开关（开启翠绿 `#1FD67E`，关闭深灰 `#4A4A50`，白圆钮滑块） |
| `CreateCardRow(parent, label, tip, control, y, hasDivider)` | 标准 54px 高度卡片行，左侧标题与副说明，右侧容纳按钮/开关，底部带左右缩进 20px 的细分割线 |
| `CreatePillButton(text, minW, h, isPrimary, onClick)` | 统一样式药丸操作按钮（可选魅紫 Primary 或深灰 Secondary） |

---

### 5.3 8 大子面板具体修改索引

所有面板文件均存放在 `src/UI/Controls/SettingsPanels/`：

1. **`GeneralSettingsPanel.cs` (通用设置)**：
   * 账户信息卡片（头像、当前昵称、修改昵称按钮）；
   * 主题模式下拉药丸（深色 Grok）；
   * 强调色选择药丸（魅紫、极客蓝、珊瑚红、在线绿）；
   * 界面语言下拉药丸；
   * 开机自动启动开关、局域网设备自发现开关。
2. **`ProfileSettingsPanel.cs` (个人主页与节点)**：
   * 82px Mascot 头像展示卡片；
   * 昵称修改卡片与节点标识 GUID 芯片。
3. **`AppearanceSettingsPanel.cs` (外观偏好)**：
   * 界面字体下拉选择药丸（可选择系统已安装字体，如微软雅黑、苹方、Segoe UI 等）；
   * 字体大小滑块或药丸；
   * 气泡圆角与内边距风格预设。
4. **`ShortcutsSettingsPanel.cs` (快捷键)**：
   * 发送消息快捷键（Enter / Ctrl+Enter）；
   * 截图快捷键、主窗口快速唤出快捷键配置行。
5. **`FileTransferSettingsPanel.cs` (文件传输)**：
   * 默认文件接收目录选择框与“浏览...”药丸按钮；
   * 自动接收文件开关；
   * 传输速度与并发分块限制配置。
6. **`ChatHistorySettingsPanel.cs` (聊天记录)**：
   * 本地 SQLite/JSON 记录存储路径展示；
   * “导出聊天记录...”药丸按钮；
   * “清空所有历史记录”危险操作按钮（红字红底）。
7. **`NetworkDeviceSettingsPanel.cs` (网络与系统设备)**：
   * 本机活动网卡下拉选择药丸；
   * 绑定 IP、广播地址、TUN/TAP 虚拟网卡穿透状态指示灯。
8. **`AboutSettingsPanel.cs` (关于与更新)**：
   * 软件大 Logo、版本号徽标（如 `v2.0.14`）；
   * “检查更新...”主要药丸按钮；
   * 开源协议与作者信息卡片。

---

## 六、 第五层：7 大独立模态弹窗修改指南

所有独立弹窗均位于 `src/UI/Controls/`，全部继承自 `GrokModalFormBase`，按钮全部使用自绘 `GrokButton`。

### 6.1 统一消息确认框 (`GrokMessageBox.cs`)
* **基准尺寸**：`460 × 260`（`GrokTheme.SizeMessageBox`）；
* **调用方式**：`GrokMessageBox.Show(owner, "消息正文", "标题", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);`
* **修改排版**：
  * 左侧状态图标（问号、感叹号、错误叉）自绘在 `(24, 56)`；
  * 右侧消息文字从 `x = 72` 开始，自动多行折行并计算高度；
  * 底部按钮容器自动放置 `GrokButton`（确定/取消/是/否），按回车触发主按钮，按 ESC 触发次按钮。

### 6.2 修改昵称窗口 (`GrokChangeNickForm.cs`)
* **基准尺寸**：`440 × 260`（`GrokTheme.SizeChangeNick`）；
* **修改输入框**：`_txtNick`（位于 `(36, 134)`，宽 368px，无边框，底色 `#121212`，字体 10.5pt）；
* **修改按钮**：底部取消按钮（Secondary 84px）、保存按钮（Primary 84px）。

### 6.3 关于软件窗口 (`GrokAboutForm.cs`)
* **基准尺寸**：`440 × 560`（`GrokTheme.SizeAbout`）；
* **修改头像展示**：顶部居中自绘 96×96px 大 Mascot 头像；
* **版本与架构信息**：中间绘制 16.0pt 粗体软件名与 9.5pt 版本号、.NET 运行库环境。

### 6.4 意见反馈窗口 (`GrokFeedbackForm.cs`)
* **基准尺寸**：`520 × 460`（`GrokTheme.SizeFeedback`）；
* **多行输入卡片**：高度 160px 的圆角深灰卡片，内嵌多行文本框；
* **联系方式输入行**：高度 42px 的单行输入胶囊。

### 6.5 软件更新窗口 (`GrokUpdateForm.cs`)
* **基准尺寸**：`520 × 440`（`GrokTheme.SizeUpdate`）；
* **更新日志展示区**：带有自绘极简暗灰滑块的多行平滑滚动框；
* **操作按钮**：“以后再说”（Secondary）与“立即更新”（Primary）。

### 6.6 新建节点窗口 (`GrokCreateNodeForm.cs`)
* **基准尺寸**：`680 × 620`（`GrokTheme.SizeCreateNode`）；
* **顶部头像生成器**：支持点击切换 6 种不同配色的大头像；
* **参数表单流**：节点名称、监听端口、自动加入组播群聊开关。

### 6.7 连接节点窗口 (`GrokConnectNodeForm.cs`)
* **基准尺寸**：`760 × 600`（`GrokTheme.SizeConnectNode`）；
* **顶部搜索胶囊**：40px 高度自绘搜索框；
* **筛选芯片组**：4 个 32px 药丸胶囊（“全部”、“活跃”、“群聊”、“离线”）；
* **局域网卡片流**：双缓冲平滑滚动的节点卡片列表。

---

## 七、 第六层：纯矢量 GDI+ 图标引擎修改 (`WeChatIcon.cs`)

**文件路径**：`src/UI/Renderers/WeChatIcon.cs`  
全客户端**严禁使用 Emoji 字符串（如 ➕、⚙️、📁 等）替代图标**，所有图标 100% 采用纯 GDI+ 矢量数学公式实时绘制。

### 1. 矢量绘制硬约束 (Rules §9)
* **线宽**：统一保持 `1.6px ~ 2.0px`；
* **端点与折角**：画笔必须设置 `pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;`，杜绝生硬尖角；
* **抗锯齿**：`g.SmoothingMode = SmoothingMode.AntiAlias;`。

### 2. 图标枚举与常用绘制映射
```csharp
public enum WeChatIconType
{
    None,
    Chat,       // 气泡聊天
    Contacts,   // 双人通讯录
    Folder,     // 文件夹
    Refresh,    // 旋转箭头刷新
    Add,        // 纯居中十字细线加号 (+)
    Menu,       // 三横线汉堡菜单
    Search,     // 放大镜
    Clear,      // 扫帚/清除
    WinMin,     // 窗口最小化横线
    WinMax,     // 窗口最大化方框
    WinRestore, // 窗口还原双层方框
    WinClose,   // 窗口关闭对角叉 (×)
    UpArrow,    // 粗线条居中对称向上箭头 (↑)
    Mic,        // 麦克风
    Settings,   // 齿轮设置
    User,       // 单人用户头像轮廓
    Dismiss     // 搜索框清除小叉 (×)
}
```

### 3. 如何新增或修改一个矢量图标？
在 `WeChatIcon.cs` 的 `Draw(Graphics g, WeChatIconType type, Rectangle bounds, bool active, Color color)` 方法中追加 `case` 分支：

```csharp
case WeChatIconType.MyNewIcon:
    using (Pen p = new Pen(color, 1.8f))
    {
        p.StartCap = LineCap.Round;
        p.EndCap = LineCap.Round;
        p.LineJoin = LineJoin.Round;
        
        // 计算居中坐标 (cx, cy)
        int cx = bounds.Left + bounds.Width / 2;
        int cy = bounds.Top + bounds.Height / 2;
        
        // 绘制自定义几何线段（例如对角线或折线）
        g.DrawLine(p, cx - 6, cy - 6, cx + 6, cy + 6);
    }
    break;
```

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
* **效果**：重新编译后，主界面向上发送圆钮、右侧本人聊天气泡、左侧会话选中高亮、设置中心的主按钮全量同步变蓝！

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

### 场景 3：调整聊天气泡内边距与最大宽度
* **需求**：觉得聊天气泡太宽或者内边距偏大，想要更加紧凑。
* **修改位置**：`src/UI/Controls/GrokTheme.cs`
```csharp
public const int ChatBubblePadSelfH = 16;  // 原 24，更紧凑的横向间距
public const int ChatBubblePadSelfV = 12;  // 原 18，更紧凑的纵向间距
public const int ChatBubblePadPeerH = 20;  // 原 32
public const int ChatBubblePadPeerV = 16;  // 原 24
```

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

## 九、 语法约束与编译验证契约

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
   输出必须为 **0 警告、0 错误**，并成功在根目录生成 `LanChat.exe` 方为修改合格。
