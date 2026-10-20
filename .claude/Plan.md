# Plan.md — 当前活跃计划

> 最近刷新：2026-10-20。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.0**（已发布并在 Git 上打好 `LanChat-v2.0` / `v2.0` 标签）。
> 历史已完成计划项已全部归档至 `Plan-archive.md`。

---

## 仍未完成 / 活跃计划项

1. **v2.0 全平台安装包实机部署与验证**：
   - Windows 端已产出 `release/LanChat.msi`（2.0.0 版，WiX 5 打包，支持自定义路径选择【浏览】、立即运行勾选项、内置免依赖清理卸载程序 `uninstall.exe`、统一桌面/开始菜单应用图标）。
   - Linux 端已产出 `release/LanChat.deb` 与 `release/lanchat_2.0.0_amd64.deb`（内置自包含 Native 二进制与 Python 3 双核，免 .NET 运行时），以及全架构通用包 `release/lanchat_2.0.0_all.deb`。
   - 待用户在不同实机物理局域网环境中进行安装部署体验，并验证桌面快捷方式、图标显示与文件互传。

2. **高级特性预研（可选演进项）**：
   - 系统托盘驻留与未读消息状态角标闪烁/红点提醒。
   - 历史消息与传输记录轻量本地持久化存储。

---

## 已确认仍有效的关键契约

- 端口：UDP 38888（发现与探测），TCP 38889+（通信与流传输）。
- 魔数：`LANC`（`0x4C, 0x41, 0x4E, 0x43`），小端模式，字符串为 LEB128 变长前缀 + UTF-8。
- 零第三方依赖：Windows 端可由系统原生 `csc.exe` 产出绿色版 `LanChat.exe`，并由 WiX 产出 `LanChat.msi`；Linux 端提供自包含 deb 安装包，不依赖宿主预装 .NET。
- 视觉规范：对标 WeChat Desktop Dark Mode（侧边栏 `#252526`、会话列表 `#202021`、激活高亮 `#07c160`、主聊天区 `#1e1e1f`、暗黑菜单 `#2c2c2e`），全界面彻底消除任何白色割裂元素。