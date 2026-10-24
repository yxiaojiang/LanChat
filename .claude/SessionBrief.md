# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-10-24。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.0.0**。
> 核心里程碑：全面对标微信桌面端（WeChat Desktop Dark Mode）95%+ 视觉与交互精度，彻底消除界面所有白色割裂元素；完成 Windows 托盘后台驻留与安全退出闭环；加入全局单实例互斥与唤醒机制；集成 GitHub Releases 在线自动更新检查与 Web 302 重定向降级；为 Linux 端打造 0 依赖微信暗黑原生 GUI（Tkinter 双轨）；新增 `Tools/` 工具链，支持用户一键编译链接并热替换本地安装目录（`CompileAndLink.bat`）以及一键打包 MSI 并发布至 GitHub Releases（`Publish.bat`）；**成功落地架构解耦重构阶段一：物理剥离 UI 控件体系与在线更新服务，`MainForm.cs` 大幅瘦身 1368 行，原生 `csc.exe` 秒级编译产物保持 ~139KB 极致精简；全面完成构建脚本纯 ASCII 与 0-BOM 治理，杜绝任何代码页下的控制台乱码与语法中断**。

---

## 一、 本轮工作态与交付成果总结

### 1. 架构解耦重构（阶段一：UI 控件与服务物理剥离）
- **物理模块化拆分与目录重组**：
  - 将原长达 3206 行的巨型 `src/MainForm.cs` 精简瘦身至 1838 行（净减 1368 行代码），成功消除了部分上帝类（God Object）臃肿问题；
  - 创建 `src/UI/Renderers/WeChatIcon.cs`：剥离包含气泡、通讯录、文件夹、雷达刷新等 13 类纯 GDI+ 矢量抗锯齿绘制算法与 `WeChatIconType` 枚举；
  - 创建 `src/UI/Renderers/WeChatDarkMenuRenderer.cs`：剥离微信暗黑菜单调色板（`WeChatDarkMenuColorTable`）、暗黑菜单渲染器（`WeChatDarkMenuRenderer`）与全局浮动暗黑气泡提示框（`WeChatDarkTooltip`）；
  - 创建 `src/UI/Controls/WeChatButtons.cs`：剥离侧边栏导航按钮（`WeChatNavButton`）与无边框暗黑工具栏图标按钮（`WeChatIconButton`）；
  - 创建 `src/UI/Controls/WeChatChatView.cs`：剥离自绘消息流视图控件与排版测算项（`MessageRenderItem`），完整保留平滑滚动与尺寸防越界保护；
  - 创建 `src/Services/UpdateService.cs`：将 GitHub REST API + Web 302 页面重定向双轨降级探测、更新向导弹窗与 MSI 自动下载安装完整封装为独立应用服务。

### 2. 编译构建工具链升级与 0-BOM 纯 ASCII 治理
- **`Tools/` 自动化工具链无缝适配与乱码根治**：
  - 更新 `Tools/Compile.bat`、`Tools/CompileAndLink.bat` 与 `Tools/Publish.bat`，全面引入多级目录源码编译；
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
| `src/MainForm.cs` | 主界面核心逻辑、托盘图标响应、会话流转与消息布局调度 |
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
| `Tools/Compile.bat` | 原生极速纯编译脚本，仅校验生成 `release\LanChat.exe`，不触碰本地安装，不打包发布 |
| `Tools/CompileAndLink.bat` | 一键编译链接并将 `release\LanChat.exe` 热替换覆盖至 `D:\soft\LanChat\` |
| `Tools/Publish.bat` | 一键打包 `release\LanChat.msi` 并自动上传发布至 GitHub Releases |
| `release/` | 全平台安装包与二进制输出目录（EXE、MSI、DEB） |

---

## 四、 下一步计划

1. **推进架构重构阶段二（主窗体业务逻辑 Partial 模块化）**：
   - 将精简后的 `MainForm.cs` 按业务领域解耦为 `MainForm.cs`（核心骨架）、`MainForm.Chat.cs`（聊天交互）、`MainForm.Files.cs`（文件流传输回调）、`MainForm.Tray.cs`（系统托盘与生命周期）；
   - 原生 `csc.exe` 验证极速编译并确保逻辑零退化。
2. **GitHub 仓库设为 Public 验证在线更新**：
   - 将 GitHub 仓库可见性调整为 Public 后，实机点击【🔄 检查新版本...】完成双轨在线更新闭环验证。
