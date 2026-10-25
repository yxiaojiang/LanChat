# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-10-25。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线：`main` 分支 Tag `v2.1.0` / `LanChat-v2.1.0`，Commit `3bf788d`）。
> 核心里程碑：**严格依照 GrokBot 设计规范与 UI 硬约束（Rule 9），圆满完成微信高输入区与 6 联圆形矢量工具栏（方案 A）重塑与热替换验证**！

---

## 一、 本轮工作态与方案 A 交付成果

### 1. 策略隔离与硬约束确立
- **稳定基线锁定与分支隔离**：在 `main` 分支完整锁定 GrokBot 暗黑基线并打上 `v2.1.0` 与 `LanChat-v2.1.0` 标签推送远端，随后创建并切换至 `feature/wechat-ui` 分支，杜绝大步快跑导致界面失控；
- **确立 Rule 9 硬约束**：在 `.claude/Rules.md` 写入第 9 条 UI 硬约束，严禁随意手绘粗糙线条或非标字号，所有控件、字体、图标必须 100% 严格执行 `Docs/GrokBot_UI_Design_System.md`。

### 2. 方案 A：微信高输入区全面对齐落地
- **保留左侧稳定结构**：左侧会话栏保持 420px，完整保留 82x82 Hero 吉祥物、86px 会话卡片与底部三联 Dock，保证功能稳固；
- **重构右侧 160px 微信大输入框**：配置 `#232326` 顶分割线与 `#121212` 暗黑背景，包含 42px 工具栏、多行文本输入框与底部操作条；
- **6 联 34x34 圆形矢量工具栏**：
  - 工具按钮统一为 34x34 正圆容器（`#1C1C20`，鼠标悬停 `#2E2E34`，8px 间距），与左侧成熟圆钮视觉张力完全一致；
  - 纯 GDI+ 1.8f 矢量重绘（`WeChatIcon.cs`）：Emoji（椭圆胶囊眼+温和微笑弧线）、Box（收藏抽屉盒）、Folder（现代文档折角文件夹）、Scissors（交叉利刃+手柄孔）、Mic（胶囊头+U型托架+底座）、Call（45度微仰电话听筒剪影）；
- **彻底消灭系统原生白色滚动条**：多行文本框配置 `ScrollBars = ScrollBars.None`，彻底消除刺眼的 Win32 白色滚动条，完整保留鼠标滚轮与键盘平滑滚动；
- **严格执行排版规范**：输入框字体设定为 `11.0pt (15px)` `Microsoft YaHei UI` `#F2F2F6`（95% 明度）；底部提示文字为 `9.5pt` `#8E8E93`（57% 明度）；发送按钮为 92x32 魅紫 `#8E64DC`（圆角 6px，10.0pt Bold 纯白字，右边距 20px，下边距 10px）。

### 3. 构建与热替换实测
- 系统原生 `csc.exe` 秒级编译，0 警告 0 错误；
- 调用 `Tools/CompileAndLink.bat` 成功热替换至本地安装目录 `D:\soft\LanChat\LanChat.exe`；
- 代码已完整提交并 Push 至远端分支 `feature/wechat-ui`（Commit `0f1102b`）。

---

## 二、 关键约束与设计原则

1. **零外部依赖与原生兼容**：严格遵循 C# 5.0 / .NET Framework 4.0 语法规范，由系统自带 `csc.exe` 秒级直编，产物体积 ~224KB，秒开运行。
2. **UI 规范第一原则（Rule 9）**：所有新绘图标必须使用 1.8f 线宽与圆头圆角，必须置于规整的 34x34 圆钮容器中；排版必须对齐 11.0pt / 9.5pt 色阶规范。
3. **向后兼容与通信契约**：UDP 38888 广播、TCP 38889+、`LANC` 二进制协议与 64KB 流式传输保持 100% 稳定兼容。

---

## 三、 已关闭与未关闭风险

- **已关闭风险**：
  - 避免了一次性重构整个三栏导致的界面紊乱（已通过方案 A 与分支隔离化解）；
  - 解决了 WinForms TextBox 在暗黑模式下暴露原生白色滚动条的系统缺陷；
  - 解决了图标线条粗细不一、缺乏按钮容器衬托的视觉脱节问题。
- **未关闭风险 / 下阶段待办**：
  - 用户将在新会话中验收方案 A 的本地运行效果；
  - 随后按序推进方案 B：在左侧加入 56px 微信极窄导航栏，并移除会话栏底部的“连接节点”与蓝扫描按钮，让 86px 会话卡片纵向铺满。

---

## 四、 模块索引 (当前核心目录架构)

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `src/Program.cs` | 应用程序入口，单实例互斥（Mutex）与广播唤醒，高 DPI 感知，AppUserModelID 注册 |
| `src/MainForm.cs` | 主窗体核心构造、控件布局树、响应式自适应布局、160px 微信大输入框容器构建 |
| `src/MainForm.Chat.cs` | 会话管理与自适应卡片绘制（86px）、文字收发、6 联工具栏按钮响应与 GrokPopupMenu |
| `src/MainForm.Files.cs` | 文件选择与拖拽监听、流式分块进度更新、文件接收与系统外壳调用 |
| `src/MainForm.Tray.cs` | 网络初始化、自适应 Hero 头像绘制（`BtnAvatar_Paint`）、托盘驻留与 GrokPopupMenu 托盘菜单 |
| `src/UI/Renderers/WeChatIcon.cs` | 纯 GDI+ 1.8f 矢量图标与 Grok 吉祥物绘制引擎（Emoji、Box、Folder、Scissors、Mic、Call 等） |
| `src/UI/Controls/WeChatButtons.cs` | 无边框图标按钮（`WeChatIconButton`），扩展支持 34x34 正圆（`IsCircle`）与微光悬停 |
| `src/UI/Controls/WeChatChatView.cs` | Grok 自适应居中流式聊天视图控件（紫色气泡、深灰卡片、代码胶囊、自绘滚动条） |
| `src/UI/Controls/GrokPopupMenu.cs` | 统一圆角浮动弹出菜单（16px圆角、44px舒适行高、8px悬停卡片、矢量图标） |
| `src/UI/Controls/GrokSettingsDialog.cs` | 全局设置窗口（920x640，220px纯黑侧边栏无缝过渡，18pt大标题，54px卡片行，32px药丸下拉） |
| `Docs/GrokBot_UI_Design_System.md` | 完整 GrokBot UI 设计规范与视觉对齐标准文档 |
| `.claude/Rules.md` | 项目硬约束规则库（包含 Rule 9 UI 设计规范硬约束） |
| `Tools/CompileAndLink.bat` | 一键编译链接并将程序热替换覆盖至 `D:\soft\LanChat\` |
