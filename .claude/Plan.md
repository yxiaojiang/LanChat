# Plan.md — 当前活跃计划

> 最近刷新：2026-10-20。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 项目版本：**LanChat v2.0.0**。
> 历史已完成计划项已全部归档至 `Plan-archive.md`。

---

## 仍未完成 / 活跃计划项

1. **GitHub Release 发布与在线更新全链路验证**：
   - Windows 端已生成支持单实例、托盘常驻、无异常最小化、GitHub 在线更新的 `LanChat.exe` 与 `release/LanChat.msi`。
   - Linux 端已重构并输出具备微信暗黑 GUI、桌面图标免终端启动的 `release/lanchat_2.0.0_amd64.deb`、`release/LanChat.deb` 与 `release/lanchat_2.0.0_all.deb`。
   - 在 GitHub 仓库 `yxiaojiang/LanChat` 发布最新 Release（如附带 `LanChat.msi` 与 `LanChat.deb` 资产），通过客户端【🔄 检查新版本...】验证在线拉取升级。

2. **实机多端互通与边界测试（可选演进项）**：
   - 验证 Windows 与 Linux（如统信 UOS / 银河麒麟 / Ubuntu）在复杂网络及多网卡环境下微信暗黑界面的双向文本与大文件流传输。
   - 本地轻量级离线历史消息持久化支持。

---

## 已确认仍有效的关键契约

- 端口：UDP 38888（发现与探测），TCP 38889+（通信与流传输）。
- 魔数：`LANC`（`0x4C, 0x41, 0x4E, 0x43`），小端模式，字符串为 LEB128 变长前缀 + UTF-8。
- 零第三方依赖：Windows 端由系统原生 `csc.exe` 产出绿色版 `LanChat.exe`，由 WiX 产出 `LanChat.msi`；Linux 端提供纯 Python 标准库 Tkinter 微信暗黑 GUI 与自包含 deb 安装包。
- 视觉规范：对标 WeChat Desktop Dark Mode（侧边栏 `#252526`、会话列表 `#202021`、激活高亮 `#07c160`、主聊天区 `#1e1e1f`、气泡 `#07c160` 与 `#2c2c2e`），全界面无白色割裂元素。
