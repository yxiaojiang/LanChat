# LanChat - 局域网极速快传与轻量通讯工具 (Windows & Linux)

一个专为内网环境打造的**极轻量级、免配置、无服务器**局域网通讯工具。支持 Windows 与 Linux 跨平台互联，具备毫秒级文字聊天与任意二进制大文件的高速传输功能。

---

## 🌟 核心特性

- **WeChat 风格原生暗黑桌面界面（Windows 端）**：
  - 对标微信桌面端三栏经典布局：左侧导航栏、中间会话好友列表、右侧消息与输入窗口。
  - 微信原汁原味的暗黑质感底色与**微信绿 (`#07c160`)** 选中高亮、气泡聊天样式。
  - 支持会话列表实时搜索过滤、表情弹窗（60+ 流行 Emoji）、文件传输卡片与点击一键打开文件/目录。
  - 原生 Windows 10/11 暗黑模式标题栏自适应，拖拽文件直接极速秒传。
- **开箱即用安装包与全自包含依赖**：
  - **Windows 安装包**：提供 `LanChat.msi` 安装包，自动创建桌面与开始菜单快捷方式，支持一键卸载；同时提供单文件绿色版 `LanChat.exe`（仅约 46 KB，依托系统自带 .NET Framework 运行，免任何安装）。
  - **Linux 安装包**：提供 `LanChat.deb` / `lanchat_1.0.0_amd64.deb` Debian/Ubuntu/统信UOS/麒麟V10 官方包格式，以及全架构通用的 `lanchat_1.0.0_all.deb`。**安装包内置全部所需运行时，无需目标电脑安装外部 .NET 或第三方依赖**，`sudo dpkg -i LanChat.deb` 一键装好即用。
- **纯内网 / 无服务器点对点架构（P2P）**：
  - 自动局域网发现与心跳探测（UDP 38888 广播与多网卡扫描）。
  - 智能 TUN/VPN 虚拟网卡代理绕过，出站连接强制绑定物理局域网网卡。
  - 跨网段/VLAN 穿透：提供【添加 IP】与 TCP 单播握手，秒级穿透交换机与路由器。
- **毫秒级实时文字聊天**：
  - 支持局域网全员广播与指定好友一对一私密聊天。
  - 极速响应（局域网延迟 < 5 毫秒）。
- **二进制文件极速流式互传**：
  - 支持任意文件格式（ZIP、EXE、ISO、视频、代码、文档等）。
  - 64KB Chunk 分块流转，支持传输 10GB+ 大文件，极低内存占用。
  - 自带传输进度条、百分比与 MB/s 实时测速。

---

## 📦 安装包与运行方式

### 1. Windows 端

#### 方式 A：标准安装包 (推荐)
直接双击运行 **`LanChat.msi`**（或 `release/LanChat.msi`）：
- 自动安装至 Program Files，创建桌面与开始菜单快捷方式。
- 支持 Windows 控制面板标准卸载。

#### 方式 B：绿色免安装版
直接双击运行 **`LanChat.exe`**：
- 体积仅约 46 KB，无外部依赖，随拷随用。
- **快捷键**：`Enter` 发送消息，`Ctrl + Enter` 换行。
- **传文件**：直接把桌面文件**拖入窗口**或点击工具栏【📁 发送文件】即可发送。
- **接收文件**：默认保存在程序同级的 `ReceivedFiles` 文件夹。

---

### 2. Linux 端 (Ubuntu / Debian / 统信 UOS / 银河麒麟)

#### 方式 A：deb 安装包 (开箱即用，免配依赖)
```bash
sudo dpkg -i release/LanChat.deb
# 或针对 x86_64: sudo dpkg -i release/lanchat_1.0.0_amd64.deb
# 或全架构通用: sudo dpkg -i release/lanchat_1.0.0_all.deb
```
安装完成后：
- 桌面启动器中已生成 **LanChat** 应用图标，点击直接打开。
- 终端中随时输入 `lanchat` 即可启动终端版。

#### 方式 B：自包含独立二进制 (ELF，无需安装任何 .NET)
- **x86_64 (Intel / AMD PC & 服务器)**:
  ```bash
  chmod +x release/linux-x64/lanchat
  ./release/linux-x64/lanchat
  ```
- **ARM64 (aarch64 / 树莓派 / 银河麒麟 ARM 服务器)**:
  ```bash
  chmod +x release/linux-arm64/lanchat
  ./release/linux-arm64/lanchat
  ```

#### 方式 C：通用免安装 Python 单文件版本
内置单文件 `lanchat.py` 仅使用 Python 3 标准库，**无需 `pip install` 任何第三方包**：
```bash
python3 lanchat.py
```

---

## ⌨️ Linux 终端交互常用指令

| 指令 | 说明 | 示例 |
| :--- | :--- | :--- |
| `直接输入文字` | 发送消息给当前会话 (默认是全网广播) | `Hello LAN!` |
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

## 🛠️ 项目构建指南

- **构建 Windows EXE**：
  双击运行 `build.bat`（调用系统自带 `csc.exe`，秒级编译）。
- **构建 Windows MSI 安装包**：
  双击运行 `build-msi.bat`（产出 `LanChat.msi`）。
- **构建 Linux deb 安装包**：
  运行 `python build-deb.py`（产出 `release/LanChat.deb` 等）。
- **一键全平台全包构建**：
  双击运行 `build-all.bat`（一次性产出 Windows EXE、MSI 与 Linux deb 全部产物）。
