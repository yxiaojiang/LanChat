# Plan.md — 当前活跃计划

> 最近刷新：2026-10-20。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> Windows 端与 Linux 端已完成 v1.0 架构升级：打上 `LanChat-v1.0` / `v1.0` Tag，完成 Windows MSI 安装包与 Linux deb 安装包构建（免外部依赖），Windows UI 全面重构为 WeChat 桌面暗黑三栏风格。

---

## 仍未完成 / 活跃计划项

1. **安装包实机部署与跨发行版验证**：
   - Windows 端已产出 `LanChat.msi`（WiX 5 打包，支持 Program Files 标准安装、桌面及开始菜单快捷方式、控制面板卸载）。
   - Linux 端已产出 `release/LanChat.deb` 与 `lanchat_1.0.0_amd64.deb`（内置自包含 Native 二进制与 Python 3 双核，免 .NET 运行时），以及全架构通用包 `lanchat_1.0.0_all.deb`。
   - 待用户在实机环境中进行安装体验和反馈。

2. **WeChat 风格 UI 持续精细化调优**：
   - 已实现：左侧深色导航栏（头像/状态、聊天、通讯录、文件夹、刷新、添加IP、设置）、中间会话列表（搜索框实时过滤、全员群聊与好友、微信绿 `#07c160` 选中态）、右侧微信暗黑气泡聊天窗口（左右气泡、Emoji 弹窗、文件传输卡片）。
   - 后续可扩展：离线消息本地持久化、托盘最小化与未读消息红点角标。

---

## 已确认仍有效的关键契约

- 端口：UDP 38888（发现与探测），TCP 38889+（通信与流传输）。
- 魔数：`LANC`（`0x4C, 0x41, 0x4E, 0x43`），小端模式，字符串为 LEB128 变长前缀 + UTF-8。
- 零第三方依赖：Windows 端可由 `csc.exe` 产出绿色版 `LanChat.exe`，并由 WiX 产出 `LanChat.msi`；Linux 端提供自包含 deb 安装包，不依赖宿主电脑预装 .NET。
- 视觉规范：对标 WeChat Desktop Dark Mode（侧边栏 `#202022`、会话列表 `#2f2f30`、激活高亮 `#07c160`、主聊天区 `#1e1e1f`）。
