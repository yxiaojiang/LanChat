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
- **全中文安装向导**：欢迎界面 -> 自定义安装目录（点击【浏览】自由选择安装盘符与路径） -> 准备安装 -> 安装进度。
- **完成即用**：安装完成界面提供【☑ 立即运行 LanChat】选项，点击【完成】直接启动应用。
- **自动创建快捷方式**：生成桌面与开始菜单快捷方式。
- **一键彻底卸载**：安装目录内自带 **`uninstall.exe`**，双击即可完全卸载程序与全部关联文件，不留任何痕迹；同时也支持 Windows 控制面板/应用管理标准卸载。
- **权限安全适配**：智能识别安装目录写权限，当安装在系统保护的 `Program Files` 时，接收文件自动安全存入用户专属的 `Downloads\LanChat_Received`，彻底避免“访问被拒绝”报错。

#### 方式 B：绿色免安装版
直接双击运行 **`LanChat.exe`**：
- 体积仅约 46 KB，无外部依赖，随拷随用。
- **快捷键**：`Enter` 发送消息，`Ctrl + Enter` 换行。
- **传文件**：直接把桌面文件**拖入窗口**或点击工具栏【📁 发送文件】即可发送。
- **接收文件**：绿色便携模式下自动保存在程序同级的 `ReceivedFiles` 文件夹。

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

## 📂 规范工程目录结构

```
LanChat/
├── src/                          # 核心业务源代码
│   ├── Program.cs                # 程序入口与单实例互斥唤醒 (Mutex)
│   ├── MainForm.cs               # WeChat 暗黑桌面主界面与消息流引擎
│   ├── Models.cs                 # 跨平台共用网络数据实体
│   ├── NetworkService.cs         # 核心网络通信服务与分块流式传输引擎
│   ├── Uninstall.cs              # 绿色卸载向导源码
│   ├── LanChat.csproj            # .NET 9 项目工程文件
│   └── linux/                    # Linux 端源码
│       ├── lanchat.py            # 纯 Python 3 标准库 WeChat 暗黑 GUI
│       └── LanChat.Linux/        # .NET 9 自包含 Linux 控制台项目
│
├── assets/                       # 图标与高精度设计资源
│   ├── app.ico                   # Windows 多分辨率应用图标 (256x256 ~ 16x16)
│   ├── app.png                   # Linux 256x256 高清启动图标
│   ├── lanchat.svg               # Linux 专用矢量应用图标
│   └── create_icon.py            # 图标自动生成工具
│
├── packaging/                    # 标准安装包打包配置与引擎
│   ├── Package.wxs               # WiX 5 中文安装向导配置文件
│   ├── build-deb.py              # Linux 多架构 Debian (.deb) 标准打包引擎
│   ├── publish-release.py        # GitHub Releases 自动化发布与资产挂载引擎
│   ├── build.bat                 # 基础编译脚本 (Legacy)
│   ├── build-msi.bat             # WiX 打包脚本 (Legacy)
│   └── build-all.bat             # 全平台一键构建脚本 (Legacy)
│
├── Tools/                        # 开发者常用高频自动化工具 (核心入口)
│   ├── Compile.bat               # 纯编译 (仅编译生成 release\LanChat.exe)
│   ├── CompileAndLink.bat        # 编译并热替换覆盖本地安装目录 D:\soft\LanChat\
│   └── Publish.bat               # 一键打包 MSI 并自动推送至 GitHub Releases
│
├── release/                      # 全平台编译与构建安装包产物
│   ├── LanChat.exe               # 绿色版免安装客户端 (~46 KB)
│   ├── LanChat.msi               # Windows 微软标准安装包 (~765 KB)
│   ├── LanChat.deb               # Linux 64位 Debian/Ubuntu 安装包 (~6 MB)
│   ├── lanchat_2.0.0_all.deb     # Linux 全架构通用版安装包 (~35 KB)
│   ├── linux-x64/                # 自包含 Linux 原生 ELF 独立二进制
│   └── linux-arm64/              # aarch64 架构原生二进制
│
├── .claude/                      # 架构上下文记忆、规范与开发规则
├── .gitignore                    # Git 忽略配置
└── README.md                     # 说明文档
```

---

## 🛠️ 项目构建与开发者快捷指令

在 `Tools/` 文件夹中为您提供了 3 个一键自动化脚本：

1. **`Tools/Compile.bat`（纯编译）**：
   调用系统原生 `csc.exe` 极速编译链接 `release\LanChat.exe` 与 `release\uninstall.exe`，不触碰本地安装，用于快速排查代码语法。
2. **`Tools/CompileAndLink.bat`（编译覆盖）**：
   编译后自动检测并安全退出后台运行中的旧程序，将最新版程序热替换覆盖至 `D:\soft\LanChat\`，按回车即可立即启动全新程序体验。
3. **`Tools/Publish.bat`（一键发布）**：
   自动调用 WiX 5 将最新程序打包为 `release\LanChat.msi`，刷新 Linux deb 包，并自动创建/更新对应版本的 GitHub Release 并流式挂载安装包资产。
