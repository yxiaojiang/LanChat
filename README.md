# LanChat · 局域网通讯与高速文件传输

LanChat 是一个无服务器、纯局域网的 Windows / Linux 点对点通讯工具，支持文字聊天、群聊、静态表情、剪贴板附件和大文件流式传输。

## 当前稳定版本

- 产品标签：`V3.0`
- 程序输出版本：`3.0.0`
- 发布页：[LanChat V3.0 / v3.0.0](https://github.com/yxiaojiang/LanChat/releases/tag/v3.0.0)
- 后续统一开发分支：`codex/v4.0-cross-platform-unification`

`V3.0` 是当前双端可稳定使用的封版标签；程序输出版本为 `3.0.0`，用于安装包、更新检查和 Linux 资产命名。

## 功能

### Windows 与 Linux 通用

- UDP 局域网发现、TCP 点对点通信和群聊广播。
- 文字消息、会话搜索、在线/离线分组、未读计数和历史记录。
- 内置静态表情包、Unicode 组合表情和缺少资源时的文字回退。
- 文件选择、多选、拖放、剪贴板文件/图片附件。
- 逐文件传输进度、百分比、速度、完成/失败状态。
- 文件消息支持打开文件和打开所在目录。
- 自动接收文件、接收目录设置、聊天记录目录迁移和清空历史。
- 深色主题、强调色、界面字体和聊天字体设置。

### Windows 客户端

- WinForms 原生暗色界面，支持 Windows 10/11。
- 输入区支持多行编辑、`Enter` 发送、`Shift+Enter` 换行。
- 内置表情按可见像素放大并与正文视觉基线对齐。
- 主题色光标与 RichEdit 文字/表情合成渲染，避免重复白色光标和闪烁。
- 鼠标点击表情内部时自动收拢到表情令牌边界。
- Windows MSI 安装包与绿色单文件 EXE。

### Linux 客户端

- Electron 桌面客户端，使用同架构自包含 C# ELF 作为通信服务。
- 提供 x86_64（amd64）和 ARM64 两套桌面包、便携包及独立 CLI。
- 设置中心包含通用、个人主页、外观、快捷键、文件传输、聊天记录、设备连接、关于与更新八页。
- 支持 Ubuntu、Debian 及兼容 Debian 包管理的 Linux 发行版。

## 下载与安装

请从 [GitHub Release](https://github.com/yxiaojiang/LanChat/releases/tag/v3.0.0) 下载对应资产。

### Windows

1. 推荐安装 `LanChat.msi`。
2. 免安装使用 `LanChat.exe`。

### Linux amd64

```bash
sudo dpkg -i lanchat_3.0.0_amd64.deb
```

便携包：`lanchat_3.0.0_amd64_portable.tar.gz`<br>
独立 CLI：`lanchat-cli_3.0.0_amd64`

### Linux ARM64

```bash
sudo dpkg -i lanchat_3.0.0_arm64.deb
```

便携包：`lanchat_3.0.0_arm64_portable.tar.gz`<br>
独立 CLI：`lanchat-cli_3.0.0_arm64`

安装包内含对应 Electron 运行时和同架构 ELF，不要求目标机器预装 .NET 或 Python 运行时。

## Linux CLI 常用命令

```text
/list 或 /peers       查看在线节点
/chat all             切换到全员群聊
/chat <编号>          切换到私聊
/send <文件路径>      发送文件
/add <IPv4>           手动连接指定节点
/refresh              刷新局域网节点
/nick <新昵称>        修改昵称
/quit                 退出
```

## 开发与构建

```text
Tools/Scripts/Compile.bat      Windows 原生快速编译
Tools/Scripts/Publish.bat      构建 Windows/Linux 资产并发布 Release
```

主要目录：

```text
src/                         Windows 客户端、共享模型与网络核心
src/linux/desktop/           Linux Electron 界面
src/linux/LanChat.Linux/     Linux 自包含 C# 服务与 CLI
packaging/                   Windows MSI、Linux 包与 Release 脚本
tests/                       Windows、Linux IPC/UI 与传输回归
.claude/                     项目规则、当前态和模块记忆
```

## 版本路线

- `V3.0`：当前双端稳定使用版本，程序输出版本 `3.0.0`。
- `V4.0`：在 `codex/v4.0-cross-platform-unification` 分支上继续推进 Windows 与 Linux 的功能统一，包括桌面生命周期、节点管理 GUI、设置项真实接线、更新维护入口及跨桌面环境验收。

## 协议与安全边界

LanChat 面向可信局域网环境，使用现有 LANC 网络封包和 JSONL 历史格式。Windows 与 Linux 共用网络核心，保持旧版本文字、文件和历史兼容；项目不依赖中心服务器。

## 许可证

本仓库当前以项目自有源码和资源为主，具体使用范围以仓库中的版权与发布说明为准。
