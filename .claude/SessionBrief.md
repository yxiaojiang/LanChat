# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-10-29。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线：`main` 分支 Tag `v2.1.0` / `LanChat-v2.1.0`，Commit `3bf788d`）。
> 核心里程碑：**用户确认 GrokBot 1:1**；P0 已在代码侧恢复 64px 浮动输入胶囊（替换方案 A 的 160px 微信高输入区）；**方案 B 冻结**。

---

## 一、 本轮工作态（GrokBot 1:1 / P0 胶囊恢复）

### 1. 策略转向
- 用户确认优先 **GrokBot 1:1**，不再继续推进微信布局方案 B；
- **方案 B（56px 窄导航）冻结**，待 GrokBot 1:1 完成后再评估；
- Rule 9 与 `Docs/GrokBot_UI_Design_System.md` 继续作为唯一视觉契约。

### 2. P0：恢复 64px 浮动输入胶囊（本步代码已完成）
- 移除方案 A 的 160px 微信高输入区与 6 联工具栏（Emoji/Box/Folder/Scissors/Mic/Call 作为底栏工具条）；
- 恢复 Docs §4.2 / §8 胶囊：高 64 / 半径 32 / bg `#202024` / 边框 `#2C2C32` / 左右边距 28 / 底边距 22；
- (+) 36×36 `#2A2A2E`（`WeChatIconType.Add`）→ `ShowInputPlusContextMenu`；Mic 32×32；发送 44×44 魅紫 ↑（`_btnSendWave` → `BtnSend_Click`）；
- 单行输入 `Microsoft YaHei UI` 11.0pt `#F2F2F6`，占位 `#5E5E66`；Enter 发送；保留拖放；
- 文件：`src/MainForm.cs`、`src/MainForm.Chat.cs`；未改网络/协议。

### 3. 待父代理 / 用户 Windows 实机验证
- 请用 `Tools/CompileAndLink.bat` 在本机编译热替换后目视：胶囊浮动、(+)/Mic/↑、Enter 发送、拖放、附件菜单。
- 本步未 Push；Linux box 不跑 WinForms GUI 编译。

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
  - Windows 实机验收 P0 64px 胶囊（编译热替换）；
  - 继续 GrokBot 1:1 其余对齐项；**方案 B 冻结**。

---

## 四、 模块索引 (当前核心目录架构)

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `src/Program.cs` | 应用程序入口，单实例互斥（Mutex）与广播唤醒，高 DPI 感知，AppUserModelID 注册 |
| `src/MainForm.cs` | 主窗体核心构造、控件布局树、响应式自适应布局、**64px GrokBot 浮动输入胶囊** |
| `src/MainForm.Chat.cs` | 会话管理与自适应卡片绘制（86px）、文字收发、输入 KeyDown/发送与 GrokPopupMenu |
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
