# Plan.md — 当前活跃计划

> 最近刷新：2026-10-25。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.1.0**。
> GrokBot UI 设计规范确立、全盘废除系统直角菜单（`GrokPopupMenu`）、主输入胶囊与全局设置界面重塑已全部落地。

---

## 仍未完成 / 活跃计划项

1. **GrokBot UI 全面设计规范与视觉对齐交付（已闭环落地）**：
   - **完成态**：
     - **编写设计系统规范**：建立 `Docs/GrokBot_UI_Design_System.md` 与 `.claude/contexts/GrokUI.md`，制定 5 级暗黑纵深、字体排版矩阵、边框距离法则与圆角比例体系；
     - **废除 ContextMenuStrip**：通用圆角浮动菜单 `GrokPopupMenu` 全盘替换会话加号菜单、输入附件菜单、列表右键菜单与托盘右键菜单；
     - **主输入胶囊重构**：消除蓝色发光边框，重构为 64px 高、32px 满圆角 `#202024` 胶囊（1px 微光边框 `#2C2C32`），11.0pt `#F2F2F6` 居中文本，36x36 附件圆钮，细线矢量麦克风，44x44 魅紫向上箭头 `↑` 发送圆钮；
     - **设置窗口（GrokSettingsDialog）重塑**：扩充为 920x640 宽屏比例，移除侧边栏分割线，依靠 `#0E0E0E` 与 `#121212` 纯底色自然分离，18.0pt 加粗明亮大标题，10.5pt 加粗中性灰分组标签，54px 卡片行，22px 内边距，32px 药丸下拉胶囊；
     - **举一反三全窗体统一**：`GrokConnectNodeForm`、`GrokCreateNodeForm`、`GrokFeedbackForm`、`GrokChangeNickForm`、`GrokUpdateForm` 窗口大标题统一升级为 18.0pt 加粗明亮纯白，外边框微光与阴影统一；
     - **称谓与图标闭环**：全代码库统一为“连接节点”；通过 `SetCurrentProcessExplicitAppUserModelID`、`WM_SETICON` 双向发送及 `SetClassLongPtr` 彻底刷新任务栏紫吉祥物图标。
   - **验证**：系统原生 `csc.exe` 耗时 < 1 秒，0 警告 0 错误编译通过，并热替换至 `D:\soft\LanChat\`。

2. **GitHub 仓库设为 Public 并完成在线更新闭环验证**：
   - Release `v2.0.0` 已通过 `Publish.bat` 成功更新挂载 `LanChat.msi` 与 `LanChat.deb` 资产。
   - 需用户在 GitHub 仓库页面将其可见性从 **Private** 切换为 **Public**（以允许客户端免密匿名拉取更新）。
   - 在客户端验证【🔄 检查新版本...】拉取与更新向导展示。

3. **实机多端互通与网络边界测试（可选演进项）**：
   - 验证 Windows 与 Linux（如统信 UOS / 银河麒麟 / Ubuntu）在复杂网络及多网卡环境下界面的双向文本与大文件流传输。
   - 本地轻量级离线历史消息持久化支持。

---

## 已确认仍有效的关键契约

- 端口：UDP 38888（发现与探测），TCP 38889+（通信与流传输）。
- 魔数：`LANC`（`0x4C, 0x41, 0x4E, 0x43`），小端模式，字符串为 LEB128 变长前缀 + UTF-8。
- 零第三方依赖：Windows 端由系统原生 `csc.exe` 产出绿色版 `LanChat.exe`，由 WiX 产出 `LanChat.msi`；Linux 端提供纯 Python 标准库 Tkinter 暗黑 GUI 与自包含 deb 安装包。
- 编译与语法：严格保持 C# 5.0 / .NET Framework 4.0 语法兼容，杜绝引入任何破坏轻量原生的 C# 8+ 高版本特性。
- 在线更新：支持 GitHub REST API 优先 + Web 302 重定向免限流降级双轨架构。
- 视觉规范：深度对标 Grok Bot 桌面客户端现代清爽暗黑设计（侧边栏纯黑 `#0E0E0E`、主聊天区 `#121212`、魅紫 `#8E64DC`、深灰卡片 `#1C1C1F`、输入胶囊 `#202024`）。
