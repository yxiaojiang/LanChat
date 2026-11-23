# Protocol.md — 网络协议与多网卡/TUN穿透上下文

## 模块定位与职责
定义底层局域网点对点（P2P）通信规范，处理多物理网卡、TUN 代理路由冲突与高速流式传输。

## 源码清单
- `Models.cs`：`PeerInfo`、`ChatMessage`、`FileTransferProgress` 纯 POCO 数据载体（Windows 与 Linux 双端源码级共享）。
- `NetworkService.cs`：顶层网络门面（Facade），对外统一暴露属性、事件与生命周期方法（`Start`、`Dispose`）。
- `NetworkService.Interface.cs`：物理网卡与 TUN 冲突消除、网卡探测、子网掩码计算与本地终端匹配。
- `NetworkService.Discovery.cs`：多物理网卡多套接字 UDP 广播监听、254 子网并发探测、心跳保活与节点生命周期。
- `NetworkService.Transfer.cs`：TCP 监听、`LANC` 协议编解码、文本消息路由与 64KB 流式分块传输引擎。
- `lanchat_app/network.py` 中对应的网络服务与协议编解码实现。

## 核心实现机制

Windows 将 TCP 接收端点地址和发送者 ID 用于本地文字、文件会话归档。文件完成事件以 `ChatMessage` 提供这些字段，Linux C# 终端沿用消息模型读取昵称和文件信息。地址直接由连接取得，LANC 文本与文件帧字段兼容原版本。

v2.0.18 的每文件进度是桌面层临时状态：Windows 控制器和 Linux 桌面服务按已有 transfer/request ID 将进度更新到原聊天气泡，成功文件消息复用已有消息模型和历史格式；不增加 LANC 帧字段，也不要求旧版对端升级。剪贴板文件粘贴仅是各桌面端本地附件选择流程，不进入网络协议。

LC-LINUX-001 已在 Python 和共享 C# 文字学习中保留已确认监听端口与主机名。首次通过文字学习时以 38889 作为临时端口，公告可更新。Python 从 Linux 物理网卡读取 IPv4 和掩码，优先用确认同子网的 UDP 源地址，回环或可确认跨网段时才考虑非回环公告地址。Python 接收使用定长读满和 64 KiB 文件流，发送仅在收到对方落盘 ACK 后确认成功。文字和文件事件携带节点 ID 与连接 IPv4，历史键与网络路由独立。Windows 宿主已通过双向非默认端口、中文与文件 SHA-256 验证，仍待真实双机、Ubuntu 和 ARM64 矩阵。 [实施验证](../../PlanDocs/LC-LINUX-001/VALIDATION.md)

LC-LINUX-002 的共享接收核心为每路文件流创建独占临时文件，在接收目录内串行提交并分配不覆盖的目标名。成功 ACK 在提交完成后发送，连接中断报告失败并清理临时文件。Windows 原生并发回环已验证两份同名异内容文件的最终 ACK、完成事件和 SHA-256。第五轮在 amd64 和 arm64 QEMU 回归了 64 MiB 流式收件、并发同名、拒收、缺最终 ACK 和端口学习。通信进程在等待最终 ACK 时强退，重启后保留未确认语义，由用户决定是否重发。Linux 桌面通过本地服务事件的对端标识、IPv4 归档键和操作 ID 显示进度及终态。物理互通按 Plan 取证。 [集成记录](../../PlanDocs/LC-LINUX-002/INTEGRATION.md)

1. **TUN 虚拟网卡穿透与物理绑定**：
   - 过滤带有 `tun`、`tap`、`vpn`、`virtual` 关键词或 MAC 为空的虚拟网卡。
   - `GetLocalEndpointForTarget`：发起 TCP 连接前，比对目标 IP 子网，强制绑定物理网卡 IP（如 `192.168.255.151:0`），防止操作系统按 TUN 虚拟网卡 Metric 0 路由发射 SYN。
2. **多套接字 UDP 监听**：
   - 绑定 `0.0.0.0:38888` 的同时，为每个真实物理网卡单独绑定 `physical_ip:38888`，开启 `ReuseAddress`。
3. **网段高速单播探测（Subnet Sweep）**：
   - 启动与刷新时，除广播外，向同网段 `1~254` 并发投递单播 UDP 包与快速 TCP 握手（250ms 超时），绕开路由器对广播报文的风暴抑制。
4. **低延迟并发调度**：
   - 定时 8 秒心跳仅发 UDP PING，严禁触发全网段 TCP 轮询。
   - 接收到 TCP 消息时直接拉起独立线程读取，严禁投入通用线程池排队，往返耗时控制在 5ms 级别。
   - 所有 TCP Socket 开启 `NoDelay = true`。
