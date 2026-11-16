# Protocol.md — 网络协议与多网卡/TUN穿透上下文

## 模块定位与职责
定义底层局域网点对点（P2P）通信规范，处理多物理网卡、TUN 代理路由冲突与高速流式传输。

## 源码清单
- `Models.cs`：`PeerInfo`、`ChatMessage`、`FileTransferProgress` 纯 POCO 数据载体（Windows 与 Linux 双端源码级共享）。
- `NetworkService.cs`：顶层网络门面（Facade），对外统一暴露属性、事件与生命周期方法（`Start`、`Dispose`）。
- `NetworkService.Interface.cs`：物理网卡与 TUN 冲突消除、网卡探测、子网掩码计算与本地终端匹配。
- `NetworkService.Discovery.cs`：多物理网卡多套接字 UDP 广播监听、254 子网并发探测、心跳保活与节点生命周期。
- `NetworkService.Transfer.cs`：TCP 监听、`LANC` 协议编解码、文本消息路由与 64KB 流式分块传输引擎。
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

## DIR-006 公网中继方向已采纳（2026-11-16，实现待开始）

- 用户确认 Grok 第 2 轮收敛结论后采纳自建 Vultr Linux 中继；Cloudflare 免费平台方案不再执行。当前分支 `codex/non-lan-network`，main 和标签 `Lanchat-v1.5` 仍为 `69b5310`，程序仍为 `2.0.15`。完整收敛提案/审核快照在提交 `6d7e6a8`；执行入口 `.claude/Plan.md`，短决定 `PlanDocs/DECISIONS.md`。
- 优先 TLS TCP 长连接：C# `SslStream`、Python `ssl`，服务端优先 `asyncio` 标准库；远程遵循系统路由，不调用 `GetLocalEndpointForTarget`。443 不等于 HTTPS，公司出站若只允许 HTTP 代理，阶段 1 重评 WSS/HTTPS，不并行做两套实现。
- 先验证原生 csc 四引用、Linux x64/ARM64 裁剪自包含、Python 标准库的真实 TLS/证书与编解码能力，再冻结远程帧。大端长度＋类型＋负载、UTF-8 JSON 均为候选，不能当作当前已有协议；不得假定原生引用已包含 `System.Text.Json`。
- 保留 LAN LocalId 随机与旧帧；新增持久远程身份、空间授权和稳定会话键。键到 JSONL 文件名需无碰撞，不能仅依赖分隔符替换。LAN 广播不含远程联系人，不因远程失败回落 peer.IP 直连，也不自动上传内网资料。
- 新认证连接验证凭据后替换同设备旧连接；旧关闭回调按连接归属核对，不清除新会话。另一份凭据副本被顶替后停止互抢；本机正常换网/唤醒仍恢复唯一重连流程。
- 去重作用域为服务器＋空间＋服务端认证发送者＋消息 ID；同进程有界缓存，重复 ACK 不重显。对端应用 ACK 不代表人已读或历史可靠写盘；断线未确认标未知，不自动重放正文，不承诺跨重启恰好一次。
- 在线文字后再做显式接受的在线文件中继；阶段 3 定服务级在途数、总缓冲和背压。远程收文件按认证身份/会话定位，不复用 `FileTransferController` 的按昵称查找、找不到归 BROADCAST 行为。
- `ProbeIp` 固定 38889/250ms、LAN 广播 8 秒与 25 秒过期仍是现有局限；不作为中继实施前置改造。公司两 Wi-Fi 的掩码/路由/隔离尚未实测，不能凭 SSID/IP 推断不通。
- Rules 第 6 条已区分 LAN 与公网；当前只完成决定/计划/记忆，未编写远程实现、部署或跨网络验收。
