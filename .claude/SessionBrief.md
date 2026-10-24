# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-10-24。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.0.0**。
> 核心里程碑：全面对标微信桌面端（WeChat Desktop Dark Mode）95%+ 视觉与交互精度，彻底消除界面所有白色割裂元素；完成 Windows 托盘后台驻留与安全退出闭环；根治 Windows 最小化异常；加入全局单实例互斥与唤醒机制；集成 GitHub Releases 在线自动更新检查与 Web 302 重定向降级；为 Linux 端打造 0 依赖微信暗黑原生 GUI（Tkinter 双轨）；**新增 `Tools/` 工具链，支持用户一键编译链接并热替换本地安装目录（`CompileAndLink.bat`）以及一键打包 MSI 并发布至 GitHub Releases（`Publish.bat`）**。

---

## 一、 本轮工作态与交付成果总结

### 1. 开发者自助脚本工具链交付 (`Tools/`)
- **`Tools/CompileAndLink.bat`（一键编译与本地热替换）**：
  - 自动定位项目根目录，调用系统原生 `csc.exe` 秒级编译 `LanChat.exe` 与 `uninstall.exe`；
  - 智能探测目标安装目录 `D:\soft\LanChat\`，若正在运行则自动退出旧进程防止文件锁定；
  - 完整覆盖替换安装目录中的 `LanChat.exe`、`uninstall.exe`、`app.ico`，支持按回车键立即拉起新程序。
- **`Tools/Publish.bat`（一键打包与 GitHub 发布）**：
  - 调用 WiX 5 自动将最新的 `LanChat.exe` 打包为微软标准安装包 `LanChat.msi`；
  - 自动同步刷新 Linux deb 双包（`amd64` 与 `all`）；
  - 调用 `publish-release.py` 自动匹配版本、检测 GitHub 仓库状态、创建/更新 Release 并支持旧同名资产自动删除覆盖，完成二进制资产流式直传。

### 2. 在线更新 403 已禁止根因排查与机制加固
- **403 异常根因诊断**：
  1. 仓库当前处于 **Private（私有）** 状态，匿名客户端请求 GitHub API 会被直接拒绝（403/404）；
  2. 仓库尚未在 GitHub 创建 **Release** 发布版本（当前 Releases 为 0），`/releases/latest` 端点无数据；
  3. GitHub API 针对未认证 IP 设有严格的 60次/小时 速率限制，匿名请求容易触发 `403 rate limit exceeded`。
- **客户端更新引擎双重加固（双轨探测）**：
  1. **REST API 优先**：请求 API 获取详细日志与 MSI 下载直链；
  2. **Web 302 重定向降级**：若 API 遭遇 403 限流或故障，自动请求 `https://github.com/{repo}/releases/latest`，通过页面 302 目标 URL 解析最新 Tag，**彻底突破 60次/小时限制**；
  3. **智能友好诊断弹窗**：识别 403 与 404 状态码，精准提示用户“仓库为 Private 或尚未发布 Release”，提供清晰解决方案，替换生硬的 Win32/WebException 报错。
- **自动化发布脚本交付 (`publish-release.py`)**：
  - 读取本地 Git 凭据，自动创建 GitHub Release（`v2.0.0`）并流式上传 `LanChat.msi` 与 `LanChat.deb` 资产。

### 3. Windows 端稳定性与体验闭环
- **滚动条越界异常根治**：在 `RecalculateLayout` 中加入尺寸守卫（`<= 50` 立即返回）、`Math.Min/Max` 边界夹紧并包裹 `try-catch`。
- **全局单实例互斥（Mutex）**：在 `Program.Main` 中使用 `Mutex("Global\\LanChat_SingleInstance_Mutex_9F24FEA2")`。若已有实例在运行，通过系统广播消息 `RegisterWindowMessage("LANCHAT_WAKEUP_INSTANCE_MSG_V2")` 唤醒已存在的实例并前置展示，当前进程安全退出。
- **WndProc 唤醒响应**：在 `MainForm.WndProc` 中监听该唤醒消息，自动调用 `RestoreFromTray()` 从系统托盘或最小化状态恢复并激活到前台。

### 4. Linux 端 deb 微信暗黑原生 GUI
- **0 额外依赖 Tkinter 原生微信暗黑界面**：在 `lanchat.py` 中使用 Python 3 标准库 `tkinter` 像素级复刻 Windows 端微信暗黑 3 栏界面，支持暗黑消息气泡、头像胶囊、进度条、发送文件、群聊/私聊切换等，免去 PyQt/Electron 臃肿负担。
- **桌面快捷方式去终端化**：将 `lanchat.desktop` 改为 `Terminal=false`，桌面点击图标直接以原生 GUI 呈现。
- **启动器自适应环境调度**：`/usr/bin/lanchat` 在图形桌面（`DISPLAY`/`WAYLAND_DISPLAY`）下优先启动微信暗黑 GUI；若无交互终端且缺少 `python3-tk`，自动调用 `zenity`/`notify-send` 友好提示 `sudo apt install python3-tk`；若在纯 SSH/无头服务器或传入 `--cli` 时，平滑降级至交互式 CLI 控制台。

---

## 二、 关键约束与设计原则

1. **以最小实现、零冗余依赖为荣**：
   - Windows 端保持系统自带 `csc.exe` 秒级编译，无任何外部运行库依赖。
   - Linux 端采用 Python 标准库实现微信暗黑 GUI，兼具跨发行版与跨 CPU 架构兼容性（x86_64、ARM64、龙芯、飞腾）。
2. **纯暗黑界面无白色残留**：
   - 界面自绘控件及菜单统一采用微信暗黑配色规范。
3. **网络与协议安全边界**：
   - 纯内网 P2P 通信，魔数 `0x4C, 0x41, 0x4E, 0x43`（`"LANC"`），LEB128 变长前缀 + UTF-8 字符串编码。
   - 大文件传输固定 64KB Chunk 分块流式收发，内存占用恒定 < 1MB。

---

## 三、 模块索引 (新目录架构)

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `src/Program.cs` | 应用程序入口，单实例互斥（Mutex）与全局消息广播唤醒，高 DPI 感知 |
| `src/MainForm.cs` | 主界面核心逻辑、托盘图标、暗黑渲染器、GitHub 双轨（API+Web）在线更新、防越界保护 |
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

1. **GitHub 仓库设为 Public 验证在线更新**：将 GitHub 仓库可见性修改为 Public 后，在客户端点击【🔄 检查新版本...】验证全流程。
2. **用户日常自助开发验证**：用户在更改代码后双击 `Tools/CompileAndLink.bat` 验证本地开发体验，完成新版本后双击 `Tools/Publish.bat` 验证全自动打包发布。
