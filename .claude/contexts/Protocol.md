# Protocol.md — 网络协议与多网卡/TUN穿透上下文

## 模块定位与职责
定义底层局域网点对点（P2P）通信规范，处理多物理网卡、TUN 代理路由冲突与高速流式传输。

## 源码清单
- `Models.cs`：`PeerInfo`、`ChatMessage`、`FileTransferProgress` 数据载体。
- `NetworkService.cs`：核心服务，包含 UDP 广播监听、TCP 监听、多网卡检测、子网单播探测、流式文件收发。
- `lanchat.py` 中对应的网络服务与协议编解码实现。

## 核心实现机制
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
