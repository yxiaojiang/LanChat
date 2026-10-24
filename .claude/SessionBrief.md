# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-10-24。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.0.0**。
> 核心里程碑：全面对标微信桌面端（WeChat Desktop Dark Mode）95%+ 视觉与交互精度，彻底消除界面所有白色割裂元素；完成 Windows 托盘后台驻留与安全退出闭环；加入全局单实例互斥与唤醒机制；集成 GitHub Releases 在线自动更新检查与 Web 302 重定向降级；为 Linux 端打造 0 依赖微信暗黑原生 GUI（Tkinter 双轨）；新增 `Tools/` 工具链，支持用户一键编译链接并热替换本地安装目录（`CompileAndLink.bat`）以及一键打包 MSI 并发布至 GitHub Releases（`Publish.bat`）；**全面落地架构解耦重构阶段一（UI 控件与服务物理剥离）与阶段二（主窗体业务逻辑 Partial 模块化），彻底消除 God Object，所有单文件均压缩至 650 行以内，原生 `csc.exe` 秒级编译产物恒定保持 ~139KB，构建脚本实现纯 ASCII 与 0-BOM 零乱码治理**。

---

## 一、 本轮工作态与交付成果总结

### 1. 架构解耦重构（阶段一：UI 控件与服务物理剥离）
- **物理模块化拆分与目录重组**：
  - 将原长达 3206 行的巨型 `src/MainForm.cs` 精简瘦身，提取出 5 个高内聚独立模块：
    - `src/UI/Renderers/WeChatIcon.cs`：13 类纯 GDI+ 矢量抗锯齿绘制算法与 `WeChatIconType` 枚举；
    - `src/UI/Renderers/WeChatDarkMenuRenderer.cs`：微信暗黑菜单调色板（`WeChatDarkMenuColorTable`）、暗黑菜单渲染器（`WeChatDarkMenuRenderer`）与全局浮动暗黑气泡提示框（`WeChatDarkTooltip`）；
    - `src/UI/Controls/WeChatButtons.cs`：侧边栏导航按钮（`WeChatNavButton`）与无边框暗黑工具栏图标按钮（`WeChatIconButton`）；
    - `src/UI/Controls/WeChatChatView.cs`：自绘消息流视图控件与排版测算项（`MessageRenderItem`），完整保留平滑滚动与尺寸防越界保护；
    - `src/Services/UpdateService.cs`：GitHub REST API + Web 302 页面重定向双轨降级探测、更新向导弹窗与 MSI 自动下载安装。

### 2. 架构解耦重构（阶段二：主窗体业务逻辑 Partial 模块化）
- **按职责划分领域代码，彻底告别单文件膨胀**：
  - `src/MainForm.cs`（645 行）：主窗体核心构造、控件树构建与布局计算、DWM 暗黑标题栏、Win32 窗口消息拦截（单实例广播唤醒）与 GDI+ 圆角矩形自绘辅助函数；
  - `src/MainForm.Chat.cs`（468 行）：好友上线/离线/信息更新、会话列表自绘与关键字实时筛选、文本消息收发流转、历史记录维护、Emoji 表情弹窗与手动添加 IP 直连握手；
  - `src/MainForm.Files.cs`（268 行）：文件选择与拖拽监听、分块传输进度条更新计算、接收文件保存分发、双击打开文件及定位系统资源管理器；
  - `src/MainForm.Tray.cs`（272 行）：网络底层初始化与物理网卡探测、个人昵称修改弹窗、系统托盘图标常驻、拦截关闭按钮后台静默运行、托盘微信暗黑右键菜单与在线更新委托。

### 3. 构建工具链 0-BOM 纯 ASCII 治理与双轨验证
- **`Tools/` 自动化工具链无缝适配与乱码根治**：
  - 更新 `Tools/Compile.bat`、`Tools/CompileAndLink.bat` 与 `Tools/Publish.bat`，全面引入 `src/MainForm*.cs` 批编译；
  - 彻底去除脚本内的 UTF-8 BOM 标记（避免 Windows `cmd.exe` 将文件头误判为 `'ï»¿@echo'` 语法报错）；
  - 彻底清理脚本内所有中文注释与非 ASCII 字符，完全采用纯英文 ASCII 格式保存，避免在中文 Windows 默认 CP936（GBK）控制台下产生多字节拆解乱码；
  - 经由系统原生 `csc.exe`（.NET Framework 4.0 / C# 5.0）实测验证，编译耗时 < 1 秒，产物 `release/LanChat.exe` 体积稳定在 **139 KB**（142,336 字节），零运行时依赖膨胀。
- **跨平台与现代 SDK 双轨兼容保障**：
  - 修复 `src/linux/LanChat.Linux/LanChat.Linux.csproj` 对跨端共用通信引擎（`Models.cs` 与 `NetworkService.cs`）的相对软链接路径；
  - 在现代 .NET 9 SDK 环境下完成 `LanChat.csproj` 与 `LanChat.Linux.csproj` 全量构建，达成 **0 警告、0 错误**。

---

## 二、 关键约束与设计原则

1. **以最小实现、零冗余依赖为荣**：
   - Windows 端保持系统自带 `csc.exe` 秒级纯编译，免去任何第三方 NuGet 与庞大运行库。
   - Linux 端采用 Python 标准库实现微信暗黑 GUI，兼具跨发行版与跨 CPU 架构兼容性（x86_64、ARM64、龙芯、飞腾）。
2. **严格代码规范与向后兼容**：
   - 保持 C# 5.0 语法兼容，杜绝引入任何破坏轻量原生的 C# 8+ 高版本语言特性；
   - 核心通信契约保持向后兼容：端口 UDP 38888、TCP 38889+，魔数 `0x4C, 0x41, 0x4E, 0x43`（`"LANC"`），LEB128 变长前缀 + UTF-8 字符串编码，64KB 流式分块传输。
3. **纯暗黑界面与视觉规范**：
   - 对标 WeChat Desktop Dark Mode（侧边栏 `#252526`、会话列表 `#202021`、激活绿 `#07c160`、主聊天区 `#1e1e1f`、气泡 `#07c160` 与 `#2c2c2e`），全界面杜绝任何 Win32 原生白色残留。

---

## 三、 模块索引 (当前项目目录架构)

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `src/Program.cs` | 应用程序入口，单实例互斥（Mutex）与全局消息广播唤醒，高 DPI 感知 |
| `src/MainForm.cs` | 主窗体核心构造、控件布局树、系统 DPI、消息广播监听与 GDI+ 绘制辅助（645行） |
| `src/MainForm.Chat.cs` | 聊天交互、好友列表/会话状态维护、文字收发、Emoji 弹窗与手动添加 IP（468行） |
| `src/MainForm.Files.cs` | 文件选择与拖拽监听、流式进度更新、文件接收与系统外壳调用（268行） |
| `src/MainForm.Tray.cs` | 网络初始化、昵称修改、系统托盘右键菜单、防误关最小化与在线更新代理（272行） |
| `src/UI/Renderers/WeChatIcon.cs` | 纯 GDI+ 矢量自绘图标系统（13 类微信高保真图标） |
| `src/UI/Renderers/WeChatDarkMenuRenderer.cs` | 微信暗黑菜单调色板、专业菜单渲染器与暗黑悬浮 Tooltip 气泡 |
| `src/UI/Controls/WeChatButtons.cs` | 侧栏微信导航按钮（`WeChatNavButton`）与无边框透明图标按钮（`WeChatIconButton`） |
| `src/UI/Controls/WeChatChatView.cs` | 自绘流式消息列表视图、虚拟防越界滚动条、气泡与文件卡片渲染 |
| `src/Services/UpdateService.cs` | GitHub Releases 双轨在线检测、降级 Web 302 探测与暗黑更新向导弹窗 |
| `src/Models.cs` | 跨平台共用数据实体（`PeerInfo`, `ChatMessage`, `FileTransferTask`） |
| `src/NetworkService.cs` | 跨平台共用网络引擎（多网卡探测、UDP 发现、TCP 握手、流式分块文件收发） |
| `src/Uninstall.cs` | 独立卸载向导源码，支持安全脱钩与彻底清理 |
| `src/linux/lanchat.py` | Linux 纯标准库微信暗黑 GUI 与 CLI 双模通用版 |
| `src/linux/LanChat.Linux/` | Linux 原生自包含 C# 工程（.NET 9 InvariantGlobalization 开箱即用） |
| `assets/app.ico` / `app.png` / `lanchat.svg` | 全套跨平台高精度应用图标设计资产 |
| `packaging/Package.wxs` | WiX 5 安装包定义，配置目录选择向导、快捷方式与图标绑定 |
| `packaging/build-deb.py` | Linux 多架构标准 Debian 安装包构建脚本（内嵌桌面自适应启动器与日志收集） |
| `packaging/publish-release.py` | GitHub Releases 一键发布与二进制资产覆盖上传工具 |
| `Tools/Compile.bat` | 原生极速纯编译脚本，纯 ASCII 0-BOM，仅生成 `release\LanChat.exe` |
| `Tools/CompileAndLink.bat` | 一键编译链接并将 `release\LanChat.exe` 热替换覆盖至 `D:\soft\LanChat\` |
| `Tools/Publish.bat` | 一键打包 `release\LanChat.msi` 并自动上传发布至 GitHub Releases |
| `release/` | 全平台安装包与二进制输出目录（EXE、MSI、DEB） |

---

## 四、 下一步计划

1. **推进架构重构阶段三（网络通信层内部轻量门面解耦）**：
   - 保持 `NetworkService` 对外 Facade 接口与公开事件不变（确保 `LanChat.Linux` 跨平台软链接 100% 编译通过）；
   - 梳理底层职责：物理网卡与 TUN 冲突消除、UDP 广播与并发单播扫描、LANC 封包编解码、64KB 流式分块传输。
2. **GitHub 仓库设为 Public 验证在线更新**：
   - 将 GitHub 仓库可见性调整为 Public 后，实机点击【🔄 检查新版本...】完成双轨在线更新闭环验证。
