# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-11-08。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（最新版本 `v2.0.14`，发布 Tag `v2.0.14`）。
> 核心任务：**DIR-007 全客户端 UI 定制手册针对解耦后新架构的全面迭代与更新（已全部圆满完成）**。

---

## 一、本轮背景与任务动因

用户提出需求：
> “`UI_Customization_Guide.md` 文档本来是说明如何修改所有样式的，目前框架变动后，需要更新该文档，你帮我更新”。

项目在经历了 DIR-005（全客户端架构解耦与上帝类重构）与 DIR-006（全量代码注释补齐）后，代码框架结构发生了一系列根本性变革：
1. **主窗体彻底解耦**：原本 3086 行紧耦合的 `MainForm.cs` 已规范分离为 `MainForm.Designer.cs`（控件树与响应式布局）、`MainForm.Chat.cs`（聊天事件与会话自绘）、`MainForm.Files.cs`（文件传输与拖拽）、`MainForm.Tray.cs`（托盘交互与通知）及 `src/Controllers/`（轻量 MVP 控制器层）；
2. **设置中心组件化**：原 1914 行超限大文件已重构为纯导航外壳 `GrokSettingsDialog.cs` 与 `src/UI/Controls/SettingsPanels/` 下的 8 大独立子面板；
3. **关键缺失控件未文档化**：原文档未包含 `GrokTrayPreviewPopup.cs`（托盘多会话未读悬浮卡片）、`WeChatDarkMenuRenderer.cs` / `WeChatDarkTooltip`（暗黑专业菜单调色板与全局浮动气泡）、`FormResizeMessageFilter.cs`（无边框窗口 8px 边缘原生缩放）、`GrokSettingsMenuForm.cs`（底部齿轮快捷菜单）的样式定制细节；
4. **子面板内容存在旧版偏差**：原文档对 `GeneralSettingsPanel`、`ProfileSettingsPanel`、`AppearanceSettingsPanel` 等面板的卡片归属存在过时描述，亟需对照真实源码予以逐一校正。

---

## 二、文档重构与升级核心成果

对 `Docs/UI_Customization_Guide.md` 进行了全面的架构级重构与内容更新：

### 1. 架构图谱四层解耦演进
将原有的三层法则拓展升级为更具实战指导性的 **四层治理架构**：
- **第一层：设计令牌中枢 (`GrokTheme.cs`)**：统一收敛色彩层级 (Level 0~3)、边框线宽、文本明度、品牌强调色、窗口基准尺寸与动态字体工厂；
- **第二层：通用基础自绘控件与渲染引擎**：详尽覆盖 `GrokButton`、`GrokModalFormBase`、`GrokPopupMenu`、`DoubleBufferedPanel`、`FormResizeMessageFilter`、`WeChatDarkMenuRenderer`、`WeChatDarkTooltip` 与 `WeChatIcon`；
- **第三层：主窗口拆分架构与 9 大独立模态/悬浮弹窗**：明确标注主窗体 5 个分部类的职能分工；
- **第四层：业务控制层与数据联动 (MVP Presenters & Controllers)**：新增第七章节，深入阐释 `SessionPresenter`、`ChatPresenter`、`FileTransferController` 如何解耦业务并安全驱动 UI 刷新。

### 2. 真实 8 大设置面板逐行校正
根据源码实际控件排版，重写了第五章节的 8 大子面板索引：
- **`GeneralSettingsPanel.cs`**：常规（语言、链接打开、字体阶梯滑块、接收目录）、权限与偏好（托盘未读悬浮开关、自动接收开关、后台驻留开关、网络通道）、支持与关于（版本与更新、意见反馈）；
- **`ProfileSettingsPanel.cs`**：个人名片（48px Mascot 头像、昵称修改链接）、设备标识（节点 UUID、OS 环境）；
- **`AppearanceSettingsPanel.cs`**：外观偏好（深色 Grok Dark、魅紫）、字体与排版（主界面字体及大小滑块、聊天框字体及大小滑块）；
- **`ShortcutsSettingsPanel.cs`**：快捷操作（Enter, Shift+Enter, Ctrl+F, Ctrl+L 快捷键芯片）；
- **`FileTransferSettingsPanel.cs`**：文件偏好（自动接收开关、默认存储目录与更改链接、64KB 分块校验）；
- **`ChatHistorySettingsPanel.cs`**：聊天记录管理（存储目录与更改链接、自动加载开关、清空记录危险操作）；
- **`NetworkDeviceSettingsPanel.cs`**：物理网络与设备（IP 绑定、TUN/TAP 过滤、并发单播探测）；
- **`AboutSettingsPanel.cs`**：关于产品（56px Mascot 大图、软件名与检查更新按钮、问题反馈链接）。

### 3. 补齐重要特色自绘控件
- **`GrokTrayPreviewPopup.cs`**：详尽记录了 320px 宽、12px 圆角、无焦点弹出（`ShowWithoutActivation`）、36px Mascot 头像、时间戳与消息截断、智能多屏工作区边缘碰撞反转算法；
- **`FormResizeMessageFilter.cs`**：详解无边框窗体利用 `WM_NCHITTEST` 监听四周 8px 边缘实现原生系统缩放手感的机制与定制方法；
- **`WeChatDarkMenuRenderer.cs` 与 `WeChatDarkTooltip`**：详解专业暗黑菜单调色板与全局抗锯齿浮动气泡的避让策略。

### 4. 扩充高频实战 Cookbook
新增涵盖设置面板新增卡片、新增自定义矢量图标、调整无边框拖拽缩放热区边距等典型实战场景的完整代码范例。

---

## 三、编译与规范验证

- **环境路径**：工作仓库位于 `D:\TEmp\Aritex\LanChat`，文档 `Docs/UI_Customization_Guide.md` 已同步更新并确保多端路径一致；
- **纯编译检验**：运行 `Tools\Compile.bat`，原生 `csc.exe` 验证通过：
  ```
  [2/2] Compiling LanChat.exe ...
        [SUCCESS] LanChat.exe compiled successfully. (Output: release\LanChat.exe)
  ```
  全工程 **0 警告、0 错误**，完全兼容 C# 5.0 / .NET Framework 4.0 约束，所有单文件严格受控在 1000 行警戒线以内。
