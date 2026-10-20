# Windows.md — Windows 客户端模块上下文

## 模块定位与职责
Windows 桌面客户端，采用原生 C# WinForms 开发，定位为**零安装、无外部依赖、支持 MSI 标准向导安装与绿色单文件执行**的现代化桌面应用。界面风格全面对标 WeChat Desktop 暗黑主题。

## 核心源码与构建物清单
- `Program.cs`：应用程序入口，开启高 DPI 感知（`SetProcessDPIAware`），注册全局未捕获异常兜底处理。
- `MainForm.cs`：主窗体，包含全套 WeChat 风格三栏暗黑 UI（左侧导航条、中间会话与实时搜索列表、右侧气泡聊天工作区、Emoji 表情选择器、文件卡片）。
- `Uninstall.cs`：独立卸载程序源码，无依赖秒级编译。
- `uninstall.exe`：安装目录自带的卸载程序 (~7 KB)，支持安全脱钩、一键彻底清理。
- `Package.wxs`：WiX Toolset 安装包定义，定义组件、安装路径自选、桌面/开始菜单快捷方式、ExitDialog 启动复选框。
- `build.bat`：一键编译脚本，调用系统自带 `csc.exe` 秒级生成绿色免安装版 `LanChat.exe` (~46 KB)。
- `build-msi.bat`：一键构建 Windows 标准向导安装包 `LanChat.msi`。
- `LanChat.msi`：生成的 Windows Installer 安装包文件。

## 关键设计点
1. **MSI 中文向导与自定义安装**：
   - 欢迎向导 -> 目标文件夹选择（支持点击【浏览】任意指定盘符路径） -> 准备安装 -> 进度展示 -> 完成向导。
   - 完成界面支持【☑ 立即运行 LanChat】，点击【完成】直接开启使用。
   - 安装目录自带 `uninstall.exe`，支持双击完全清除安装目录与快捷方式，不留痕迹。
2. **Program Files 写权限自适应**：
   - 智能探测根目录可写性：便携绿色模式使用当前目录 `ReceivedFiles`；安装在 `Program Files` 时自动将接收文件转存至用户目录 `Downloads\LanChat_Received`，杜绝访问被拒绝报错。
3. **WeChat Desktop 暗黑设计语言**：
   - 侧边栏：`#202022`，支持头像弹窗、导航切换。
   - 会话列表：`#2f2f30`，选中项为微信绿 `#07c160`，支持根据昵称、IP 实时模糊搜索过滤。
   - 聊天气泡：自主实现 `WeChatChatView` 双缓冲自绘，对方暗灰 `#2c2c2e` 气泡居左，己方微信绿 `#07c160` 气泡居右，带三角指示器。
   - 表情弹窗：支持 60+ 流行 Emoji 点击快速输入。
