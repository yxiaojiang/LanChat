# LanChat - 局域网极速快传与轻量通讯工具 (Windows & Linux)

一个专为内网环境打造的**极轻量级、免配置、无服务器**局域网通讯工具。支持 Windows 与 Linux 跨平台互联，具备毫秒级文字聊天与任意二进制大文件的高速传输功能。

---

## 🌟 核心特性

- **极致轻量**：
  - Windows 端单文件仅约 **55 KB**，纯原生免安装，双击即用。
  - Linux 端提供已编译好的 ELF 二进制文件（`x86_64` 与 `ARM64`），并附带零依赖的 Python 单文件版本。
- **纯内网 / 无服务器点对点架构（P2P）**：
  - 自动局域网发现与心跳探测。
  - 支持 TUN/VPN 代理与多网卡智能穿透，自动路由物理网卡。
  - 跨网段/VLAN 支持：提供【添加 IP】与 TCP 单播握手，秒级穿透交换机与路由器。
- **毫秒级实时文字聊天**：
  - 支持局域网全局广播与好友一对一私密聊天。
  - 极速响应（局域网延迟 < 5 毫秒）。
- **二进制文件极速流式互传**：
  - 支持任意文件格式（ZIP、EXE、ISO、视频、代码、文档等）。
  - 流式 64KB Chunk 分块流转，支持传输 10GB+ 大文件，极低内存占用。
  - Windows 端支持直接拖拽文件发送；Linux 端支持命令行直接发送，自带进度条与 MB/s 实时测速。

---

## 💻 平台支持与运行方式

### 1. Windows 端
在任意 Windows 7 / 8 / 10 / 11 电脑上，直接双击运行：
```
LanChat.exe
```
- **快捷键**：`Enter` 发送消息，`Shift + Enter` 换行。
- **传文件**：直接把桌面文件**拖入聊天窗口**即可发送。
- **接收文件**：默认保存在程序同级的 `ReceivedFiles` 文件夹。

---

### 2. Linux 端 (支持 x86_64 与 ARM / 树莓派)

Linux 版本位于 `release/` 目录与根目录，提供两种运行方式：

#### 方式 A：运行独立编译好的二进制文件 (ELF)
- **x86_64 (Intel / AMD PC & 服务器)**:
  ```bash
  chmod +x release/linux-x64/lanchat
  ./release/linux-x64/lanchat
  ```
- **ARM64 (aarch64 / 树莓派 3/4/5 / Jetson / 嵌入式开发板)**:
  ```bash
  chmod +x release/linux-arm64/lanchat
  ./release/linux-arm64/lanchat
  ```

#### 方式 B：通用免安装 Python 单文件版本 (任意架构通用)
内置在根目录的 `lanchat.py` 仅使用 Python 3 标准库，**无需 `pip install` 任何第三方包**，在任何 Linux（x86/ARM/MIPS/RISC-V）上直接运行：
```bash
python3 lanchat.py
```

---

## ⌨️ Linux 终端交互常用指令

| 指令 | 说明 | 示例 |
| :--- | :--- | :--- |
| `直接输入文字` | 发送消息给当前会话 (默认是全局广播) | `Hello LAN!` |
| `/list` 或 `/peers` | 查看当前局域网所有在线好友与 IP | `/list` |
| `/chat <编号或all>` | 切换聊天模式 (0 或 all 为广播模式) | `/chat 1` 或 `/chat all` |
| `/send <文件路径>` | 发送文件给当前私聊的好友 | `/send /home/user/test.zip` |
| `/send <编号> <文件>` | 发送文件给指定好友编号 | `/send 1 ./ubuntu.iso` |
| `/add <IP地址>` | 手动连接好友电脑 (跨网段/VPN 必备) | `/add 192.168.255.147` |
| `/refresh` 或 `/scan` | 重新向局域网广播并扫描网段 | `/refresh` |
| `/nick <新昵称>` | 修改自己的昵称并全网同步 | `/nick my_linux` |
| `/clear` | 清屏 | `/clear` |
| `/quit` 或 `/exit` | 安全退出程序 | `/quit` |

---

## 🛠️ 项目目录结构

```
LanChat/
├── LanChat.exe              # Windows 独立轻量可执行程序 (~55KB)
├── MainForm.cs              # Windows WinForms 界面与拖拽交互
├── Models.cs                # 核心数据模型 (跨平台共享)
├── NetworkService.cs        # 核心网络服务 (UDP 广播、TCP 文件流与握手，跨平台共享)
├── Program.cs               # Windows 程序入口
├── build.bat                # Windows 一键编译脚本 (调用原生 csc.exe)
│
├── lanchat.py               # Linux 通用零依赖单文件客户端 (x86/ARM 通用)
├── LanChat.Linux/           # Linux 终端版 C# 源码工程
│   ├── Program.cs           # Linux 交互式终端 UI
│   ├── LanChat.Linux.csproj # Linux .NET 9 工程文件
│   └── build-linux.bat      # Linux 交叉编译构建脚本
│
├── release/                 # 预编译好的 Linux 独立二进制
│   ├── linux-x64/lanchat    # 适用于 Linux x86_64
│   └── linux-arm64/lanchat  # 适用于 Linux ARM64
│
├── .gitignore               # Git 忽略配置
└── README.md                # 项目文档
```

---

## 🔒 网络协议说明

| 功能 | 协议 | 默认端口 | 说明 |
| :--- | :--- | :--- | :--- |
| **节点发现与心跳** | UDP | 38888 | 广播包 + 网段单播快速探测，多网卡独立绑定 |
| **文字消息与文件传输** | TCP | 38889+ | 流式二进制传输协议（`LANC` 魔数标识），支持超大文件极速收发 |
