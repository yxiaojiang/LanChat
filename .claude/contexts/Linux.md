# Linux.md — Linux 客户端模块上下文

> 更新于 2026-11-18。发布基线 [v2.0.16](https://github.com/yxiaojiang/LanChat/releases/tag/v2.0.16)（a0e49c1）。重构方案 LC-LINUX-001 已采纳，实施进度以 [Plan.md](../Plan.md) 为准。

## 当前分发和入口

| 形态 | 入口与实际内容 |
| --- | --- |
| amd64 Debian 包 | release/LanChat.deb（版本化文件为 lanchat_2.0.16_amd64.deb）；桌面启动 Python/Tk，另含 x64 ELF CLI |
| all Debian 包 | release/lanchat_2.0.16_all.deb；包含 Python 入口和桌面资源 |
| 自包含 CLI | release/linux-x64/lanchat、release/linux-arm64/lanchat；源码 src/linux/LanChat.Linux，链接 Models 和 NetworkService，目标 .NET 9 |
| Python 源码 | src/linux/lanchat.py；目前同一文件管理 Tk GUI、CLI、网络、设置和更新 |

- Debian 启动器由 packaging/build-deb.py 生成；桌面 .desktop 配置 Terminal=false。依赖为 python3，推荐 python3-tk。桌面优先启 GUI，缺 Tk 的提示与回退、SSH 无头和显式 --cli 待按实机矩阵验证。图标资源为 assets/lanchat.svg 与 assets/app.png。
- 配置当前保存昵称与自动接收偏好，走便携同级文件或 ~/.config/lanchat/settings.json，目录不可用时回退 ~/.local/share/lanchat/settings.json。聊天历史当前在进程内。已安装接收目录根据可写性选择。日志入口为 ~/.local/share/lanchat/launcher.log 和 lanchat_python.log、lanchat_dotnet.log。
- Python 基础发现、群聊、私聊和文件传输已有路径，网络事件经队列进入 Tk 主线程。现有桌面界面是三栏，多处使用原生 Tk 部件。可见差距集中在会话持久化、发送结果、节点身份、文件操作及 GrokBot 整体交互。

## 已核查、待修复

- Windows 宿主同机回环验证 Python 与 C# 文字和文件双向互通。群聊系统消息可触发重绘异常，离线发送清掉草稿，自动接收控件初值可能与配置不符。文件按昵称归档会串会话，发送未收到落盘 ACK 也可能误报成功。TCP 短读和两端文字学习覆盖监听端口都已纳入第一阶段。
- Python UDP 使用公告地址的条件比 C# 宽。第一阶段按物理 IPv4 和掩码核对可信网络，保留无法确认的地址来源，供历史归档决策。
- 两架构 ELF 最后入库提交为 da25ed5。已核对本地 v2.0.16 amd64 包携带仓库中的旧 x64 ELF。构建路径和打包入口待修；新旧 ELF、双机互通及 Linux 各发行版的桌面表现仍待实测。

## 已采纳的改造约定

- 第 1 阶段在现有入口修故障及候选包链，第 2 阶段按网络、会话、界面和入口职责拆分源码，标准库 zipapp 构建单个可执行 .pyz。安装和便携路径、已有配置及 --cli 需要迁移回归。按 Windows 的 LAN-<IPv4> 和历史 JSONL 语义做持久化。
- 正式 UI 保持 Tk 轻量运行，构建期 GDI+ 生成符合规范的圆角和图标。原生文件选择器保留系统样式，应用内提示、菜单和接收者选择用暗色窗口内叠加层。先用 Linux 实测选择消息流和资源呈现方式，再做双栏、消息卡片和分类设置。规则和采纳详情见 [Rules.md](../Rules.md) 与 [决策归档](../../PlanDocs/LC-LINUX-001/STATUS.md)。

## CLI 常用命令

- /list 或 /peers 查看在线节点。
- /chat <编号|IP|all> 切换私聊或群聊。
- /send <文件路径> 发送文件。
- /add <IP> 发起直连。
- /refresh 刷新节点。
- /nick <新昵称> 更新昵称。
