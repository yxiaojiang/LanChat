# SessionBrief.md — 当前接手摘要

> 最近刷新 2026-11-18。工作目录 D:\TEmp\LanChat，开发分支 main。[公开基线 v2.0.16](https://github.com/yxiaojiang/LanChat/releases/tag/v2.0.16) 对应 a0e49c1。稳定标签 Lanchat-v1.5 指向 69b5310。

## 当前优先级与已采纳决定

- 首要任务是完成 Linux 桌面客户端重构。LC-LINUX-001 已经 Claude 两轮独立审核，第 2 轮收敛，用户确认采纳三项决定。下一次接手从 [Plan.md](Plan.md) 的 1.1 开始，依次完成主流程与候选包、代码拆分与历史、正式 UI。Tk 实机原型可与前两阶段同期推进。方案、审核和采纳记录见 [归档](../PlanDocs/LC-LINUX-001/STATUS.md)。
- Linux 应用内 UI 对齐 GrokBot 视觉指标，构建期可用 GDI+ 生成图标和圆角资源，随 Python 单文件由 Tk 加载。系统文件及文件夹选择器沿用桌面样式，菜单、消息提示和接收者选择用暗色窗口内叠加层。手写 Python 源码按职责拆分，采用 1000 行预警及 1500 行上限。标准库 zipapp 构建单文件 .pyz，保留桌面、便携和 CLI 用法。阶段 1 在旧文件做局部修复，阶段 2 先拆源码再接入会话历史。

## 已有证据与风险

- 目前 deb 桌面启动的是 Python/Tk，另有 Python 命令行和复用 C# 共享核心的 x86_64、ARM64 ELF CLI。都按真实入口验收。amd64 包内旧 ELF 与仓库 2026-10-20 入库版本字节相同，现有构建输出和打包输入位置不同。修通两架构构建和失败退出，从干净产物打包，并核对包内 ELF 的摘要。候选包验证和正式发布分别记录。
- Windows 宿主回环确认 Python 与 C# 双向文字和文件协议可互通。审计复现了群聊重绘异常、历史控件可编辑、离线草稿丢失、失败状态静默、同名文件错归及短读。Python 与 C# 的文字学习均可能覆盖已发现的非默认端口，阶段 1 一并修。UDP 先核对物理地址和掩码，再决定是否采用公告地址。端口、LANC 帧和流式传输约定保持兼容。
- 历史采用 Windows 的 LAN-<IPv4> 会话键和 JSONL 命名、字段、时间精度及转义语义。节点 ID 允许随启动变化，当前网络端点用于发送。按地址确认后归并，证据不足的历史保持独立。验证旧配置、便携与安装路径、中文、引号、反斜杠和跨平台路径可用性。已有 Windows 会话合并与实时换色已通过隔离回归。
- 真实双机、Linux Xorg／Wayland 下的 Tk 渲染、中文输入、长历史、分数缩放、ARM64 安装和旧 ELF 互通仍待验证。按计划记录版本、环境和结果，未经实机通过的组合标为待验证。Windows 旧历史、双机换版、拖顶贴靠、跨屏及任务栏自动隐藏留在计划后段。互联网方向在 codex/non-lan-network 分支单独推进。
- 首阶段用原生编译脚本验证共享 C# 修复，同时测候选版双向非默认端口，以及两个公开版本各自的兼容表现。UI 原型需比较两种消息流在两千条记录下的滚动和切换耗时，并检查输入法候选窗与发送快捷键，再决定正式实现。

## 入口

- Linux Python 当前源码在 src/linux/lanchat.py，ELF 项目在 src/linux/LanChat.Linux，桌面入口、依赖及包内容看 packaging/build-deb.py。当前产物情况和改造边界见 [Linux 上下文](contexts/Linux.md)。
- C# 文字学习看 NetworkService.Transfer，发现与接口地址看 NetworkService.Discovery 和 NetworkService.Interface，归档对照 SessionManager 与 ChatHistoryService。视觉以 GrokBot 规范和 GrokTheme 为基准，跨端协议约束见 [协议上下文](contexts/Protocol.md)。
