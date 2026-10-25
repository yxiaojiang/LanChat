# Plan.md — 当前活跃计划

> 最近刷新：2026-10-25。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.0.0**。
> 架构解耦重构（阶段一至四）与 Grok Bot 惊艳清爽暗黑界面 100% 高保真还原已全部落地并归档至 `Plan-archive.md`。

---

## 仍未完成 / 活跃计划项

1. **全弹窗与菜单 Grok Bot 惊艳清爽暗黑高保真 100% 全面交付（闭环完成）**：
   - **完成态**：全面对标 `assets/GrokBotPics`（`ConnetctApp.png`, `CreateNewBot.png`, `About.png`, `SendFK.png`, `Alis4.png~Alis7.png`）：
     - **更正业务称谓**：严谨统一为“连接节点”（彻底消除“连接应用”）；
     - **连接节点（GrokConnectNodeForm）**：对标 `ConnetctApp.png` 顶部搜索胶囊、过滤芯片、局域网在线节点推荐卡片流与手动指定 IP 直连握手；
     - **创建新节点（GrokCreateNodeForm）**：对标 `CreateNewBot.png` 居中动态吉祥物预览、起名胶囊输入框、14 种有机形态网格、11 色调色盘与圆角操作按钮；
     - **关于与支持（GrokAboutForm）**：对标 `About.png` 居中 Squircle 吉祥物徽章、版本信息与“复制版本信息”圆角药丸按钮；
     - **修改昵称（GrokChangeNickForm）**：对标 Grok Bot 原生暗黑圆角输入框与紫色保存按钮；
     - **发送反馈（GrokFeedbackForm）**：对标 `SendFK.png` 多行反馈输入框、网络诊断复选框与发送按钮；
     - **全局系统设置（GrokSettingsDialog）**：对标 `Alis4.png~Alis7.png` 左侧通用/电脑/更新/关于导航与现代卡片流；
     - **软件更新向导（GrokUpdateForm）**：对标 `Alis7.png` 版本对比横幅、更新日志与平滑进度条；
     - **统一暗黑提示框（GrokMessageBox）**：彻底取代原生白底 MessageBox；
     - **任务栏图标彻底修复**：配置 `SetCurrentProcessExplicitAppUserModelID`、`WM_SETICON` 双向发送及 `SetClassLongPtr` 写入窗口类，解决任务栏图标缓存旧图标问题。
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
- 视觉规范：深度对标 Grok Bot 桌面客户端现代清爽暗黑设计（侧边栏纯黑 `#0E0E0E`、主聊天区 `#121212`、魅紫 `#7946D7` / `#8E64DC`、深灰卡片 `#1E1E21`、输入胶囊 `#202024`）。
