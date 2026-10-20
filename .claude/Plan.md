# Plan.md — 当前活跃计划

> 最近刷新：2026-10-20。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.0**。
> 历史已完成计划项已全部归档至 `Plan-archive.md`。

---

## 仍未完成 / 活跃计划项

1. **实机环境验证与问题排查**：
   - Windows 端已生成带有托盘后台驻留与右键退出的 `LanChat.exe` 与 `release/LanChat.msi`。
   - Linux 端已重构并输出具备写权限降级、多级诊断日志与防闪退机制的 `release/lanchat_2.0.0_amd64.deb`、`release/LanChat.deb` 与 `release/lanchat_2.0.0_all.deb`。
   - 待用户在实机 Linux 环境上运行验证：若有异常，直接读取 `~/.local/share/lanchat/launcher.log` 或 `/tmp/lanchat_${USER}_launch.log` 定位。

2. **高级特性预研（可选演进项）**：
   - 历史消息与传输记录轻量本地持久化存储（SQLite/JSON）。

---

## 已确认仍有效的关键契约

- 端口：UDP 38888（发现与探测），TCP 38889+（通信与流传输）。
- 魔数：`LANC`（`0x4C, 0x41, 0x4E, 0x43`），小端模式，字符串为 LEB128 变长前缀 + UTF-8。
- 零第三方依赖：Windows 端由系统原生 `csc.exe` 产出绿色版 `LanChat.exe`，由 WiX 产出 `LanChat.msi`；Linux 端提供自包含 deb 安装包，内嵌原生 ELF 与 Python 双核。
- 视觉规范：对标 WeChat Desktop Dark Mode（侧边栏 `#252526`、会话列表 `#202021`、激活高亮 `#07c160`、主聊天区 `#1e1e1f`、暗黑菜单 `#2c2c2e`），全界面无白色割裂元素。