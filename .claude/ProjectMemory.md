# ProjectMemory.md — 稳定全局契约与架构设计

> 本文件仅存放长期有效、跨平台的底层网络协议、多网卡/TUN 穿透契约与项目硬约束。模块当前态见 `contexts/`。

---

## 一、项目架构与定位

- **项目定位**：以极轻量、无服务器（P2P）局域网通讯为基础，支持即时文字与大文件流式收发。DIR-006 已采纳可选自建公网中继方向，尚未实现；关闭远程功能时局域网独立运行。
- **跨平台对齐**：
  - **Windows 端**：C# WinForms，利用系统内置 `csc.exe` 构建单文件绿色可执行程序，支持高 DPI 自适应与文件拖拽。编译器路径、Framework 目标、实际运行时与操作系统版本分开核对，不能由 `v4.0.30319` 推断 TLS 能力。
  - **Linux 端**：
    1. C# 终端交互应用（`LanChat.Linux`），提供预编译好的 ELF 二进制（`release/linux-x64/lanchat` 和 `release/linux-arm64/lanchat`）。
    2. Python 3 单文件版（`lanchat.py`），纯标准库编写，全架构通用（x86/ARM/MIPS/RISC-V/树莓派）。
  - **核心逻辑复用**：`NetworkService.cs` 和 `Models.cs` 100% 平台无关，Windows 与 Linux C# 客户端直接源码级链接复用，杜绝协议分歧。

---

## 二、底层网络通信契约

### 1. 端口与魔数
- **魔数**：`0x4C, 0x41, 0x4E, 0x43`（ASCII: `"LANC"`），所有 TCP 传输与握手帧的第一道合法性校验。
- **UDP 发现端口**：`38888`（启用 `ReuseAddress` 与 `Broadcast`）。
- **TCP 数据端口**：`38889` 开始自动尝试绑定（若端口占用自动探测 `38890~38939`）。

### 2. 节点发现与网段探测
- **UDP 报文格式**：`CMD|ID|NICKNAME|HOSTNAME|TCP_PORT|ANNOUNCED_IP`
  - `CMD`：`HELLO`（上线/刷新）、`ACK`（响应）、`PING`（心跳）、`BYE`（下线退出）。
  - `ANNOUNCED_IP`：发送方显式声明的物理网卡 IP，避免因路由中转导致对方取错源 IP。
- **单播网段扫描（Subnet Sweep）**：
  - 启动及用户手动刷新时，除向 `255.255.255.255` 和子网广播（如 `192.168.255.255`）发送外，在 15ms 内对 `/24` 网段（`1~254`）发起并发单播 UDP 与 TCP 38889 探测。
  - **硬约束**：8 秒心跳定时器（`HeartbeatLoop`）**严禁触发全网段扫描**，心跳仅发送 UDP PING，彻底避免线程池饥饿。

### 3. TCP 帧协议（小端模式）
- **类型 1：文字消息**
  - 结构：`[MAGIC (4B)] [Type=1 (1B)] [isBroadcast (1B)] [SenderId (Str)] [SenderNick (Str)] [TargetId (Str)] [Content (Str)]`
  - 字符序列化：采用 .NET 标准的 LEB128 变长字节长度前缀 + UTF-8 字节。
  - 自动学习：收到任何合法的 Type 1 帧时，接收方自动提取对端 IP 与身份并纳入好友列表。
  - 系统握手标识：若 `Content == "[系统自动握手]"`，接收方仅建立邻居关系并回复 `1`（ACK），不渲染聊天气泡。
- **类型 2：二进制文件传输**
  - 协商帧：`[MAGIC (4B)] [Type=2 (1B)] [TransferId (Str)] [SenderId (Str)] [SenderNick (Str)] [FileName (Str)] [FileSize (Int64, 8B)]`
  - 接收方确认：回复 `1`（接收）或 `0`（拒绝）。
  - 数据流：分块流式传输（固定 64KB Chunk），支持 10GB+ 超大文件，内存占用恒定 < 1MB。
  - 完工确认：文件流结束双方校验字节长度并回复 `1`，临时文件重命名并防重名自增。

---

## 三、TUN 代理 / 多网卡穿透准则

1. **虚拟网卡自动识别过滤**：通过网卡描述符、名称关键字（`tun`、`tap`、`vpn`、`virtual`、`wsl`、`vmware`）以及物理 MAC 地址是否为空，准确区分物理网卡（如无线 WLAN、有线以太网）与虚拟网卡。
2. **多套接字绑定**：UDP 监听除绑定 `0.0.0.0` 外，必须同时为每个物理网卡单独绑定套接字，防止 TUN 驱动在底层吞噬物理网络广播。
3. **局域网出站定向绑定（Bypass TUN）**：现有 LAN TCP 连接和文件发送使用 `GetLocalEndpointForTarget` 显式选择物理网卡，绕过 TUN 路由干扰。此契约不适用于 DIR-006 公网中继；远程连接遵循系统路由，不调用该绑定路径。
4. **即时无延迟响应**：
   - 所有 TCP Socket 必须启用 `NoDelay = true`（关闭 Nagle 算法）。
   - 接收连接必须使用独立专用线程即时读取，严禁投入系统通用 ThreadPool 排队。

---

## 四、DIR-006 已采纳边界（实现待验证）

- 保留以上 LANC 端口、魔数与帧结构；远程采用独立版本化协议，经用户自有 Linux 中继传输，优先 TLS TCP 长连接，Python 标准库服务端。候选帧与编解码方案须通过三条真实产物验证后才冻结，当前不是已实现的底层协议。
- 安全范围以 Rules 第 6 条为准：可信内网假设仅覆盖旧 LANC；公网必须认证、授权、TLS 和资源限额。LAN/远程路由与广播隔离，默认不开启公网连接。
- C# 核心/Models 跨 Windows 与 Linux 共用，Python 对齐同一远程契约；稳定远程身份与现有每次启动随机的 LAN LocalId 分开。首版仅在线私聊/受限在线文件，不承诺离线投递、端到端加密或恰好一次。
- 决定入口为 `PlanDocs/DECISIONS.md` 的 DIR-006；当前执行与验收只维护 `.claude/Plan.md`，模块接入点见 `contexts/Protocol.md`。
