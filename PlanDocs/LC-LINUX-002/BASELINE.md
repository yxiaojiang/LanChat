# LC-LINUX-002 · Windows 基线与验收矩阵

2026-11-20。公开版为 `v2.0.16`（`a0e49c1`）。对齐源件为 `ce1dd63bf7cf329084cc3a7b326bd1fa7ce99387` 加本轮实施前已有工作树差异。Windows 10 Pro 19045 用 Framework64 自带 `csc.exe` 按原生编译脚本的源清单、程序集引用和资源编译。输出写入隔离目录。

| 对照物 | SHA-256 |
| --- | --- |
| 当前 WinForms `LanChat.exe` | `cd005fa1af0dcba8ed248a0c0c8960209ca79c7fb404c9d2e4f922d0a836b52c` |
| 当前 `uninstall.exe` | `f86b57143e9b914ed793e9425c2ff29604796efb3737c1d390ebbbeccf9da118` |
| 公开版 Windows `LanChat.exe` | `00220a74f9aaffc99200828d60fc9935e99412465f3f2854ca15ec4d5a991cfd` |
| 公开版 Windows `LanChat.msi` | `6eb3498ec72864742dd43ed022853908786de0989a93eec11f88b73a0d614b19` |
| 公开版 Linux amd64 / all | `68cfd93816d847a36b454647e983fa7bed56bbddf21500d016f87df2d1b3ba7d` / `82057e4c63d44e617282336dc9ee10860d2a221bdc8467818fe5be46fc570f8d` |
| 旧候选 Linux amd64 / all | `87b1bbb912993ed8e6d737888dca79239b6282dd235e32412a03a537a6ee366c` / `3ff0b098d78ab1ba6a3c4d785bd45f0d39ab1d0b248036e01cf83a4520199d99` |
| 旧候选交接 ZIP | `a14a885fc858b8b6eb0f22e7cbe881b8aa5ba5c6885090f9031ed07eacd998fd` |
| 实施前 Git 差异 / 未跟踪文件归档 | `2a24fedbafc19b654d58f71f06891b13cdd0f2898a7cb9083e66c261dcc8f637` / `d09cf8daaeb298c964777c329d287904b953eb23cb9a594d8568d21d153fa8c6` |

本地保全目录为 `bin/lc-linux-002/baseline/`。`public/`、`candidate/`、`handoff/`、`portable/` 分别保存公开包及 ELF、旧候选、交接 ZIP 和便携样本。`legacy-sha256.txt` 记录文件摘要。公开 v2.0.16 的 EXE、MSI、amd64 和 all 样本的 SHA-256 与 GitHub Release 资产摘要一致。便携历史样本由旧候选 pyz 加固定设置、JSONL 与接收文件组成。 [公开版发布记录](https://github.com/yxiaojiang/LanChat/releases/tag/v2.0.16)

## Windows 窗口及实际入口

截图在 `bin/lc-linux-002/baseline/windows/`。隔离启动器链接本轮实际编译的 WinForms 程序集，打开原窗口和控件，以便与已运行的系统安装版共存。测试设置、历史及接收目录独立。使用 Grok 深色、默认强调色、Microsoft YaHei UI、DPI 120（125%），主客户区 1040×720，窄窗 880×560。样本对话、未读预览与文件卡片为固定测试数据。`geometry.txt` 记录 Win32 客户区及 DPI。

| 操作入口 | Windows 基线及验收动作 | 首包／后续 |
| --- | --- | --- |
| 标题栏、搜索、会话 | 观察 420px 左栏、顶部搜索、头像、群聊置顶和选中/悬停、离线及未读。由私聊和群聊右键分别操作「发送文件…」「删除」「发送文件给所有人…」「清空群聊记录」，确认与取消。 | `01-main-empty`、`03-private-file`、`04-narrow`、`08-confirm`。清理、删除与排序完整回归在第 2～3 步。 [源码](../../src/MainForm.Chat.cs) |
| 节点 | 刷新、手动 IP、列表选择，「连接节点」「创建新节点」与修改昵称。分别记录连接中、失败/成功、换节点、离线和重连后会话行为。 | `06-connect`、`07-create`。首包验自动发现、刷新；直连与重连后续验收。 [连接窗口](../../src/UI/Controls/GrokConnectNodeForm.cs) |
| 聊天 | 从群聊切换到私聊，观察系统行、时间、气泡、正文选择、表情、Enter 与换行键；再验证草稿、失败反馈和非当前会话归属。 | `02-group`、`03-private-file`。首包验收发；完整行为在第 2～3 步。 [输入源码](../../src/MainForm.Chat.cs) |
| 文件 | 附件菜单、多选、拖放、选人。文件卡片显示文件名和大小，并支持「打开文件」「打开目录」。观察接收目录和自动接收开关，验拒收、进度、落盘最终 ACK。 | `03-private-file`、`05-attachment`。首包验证系统选文件、收发及 SHA-256；完整卡片和路径状态随第 2～3 步验证。 [文件源码](../../src/MainForm.Files.cs) |
| 通用、个人主页 | 检查通用下拉、下载路径、反馈和个人主页设备资料，操作昵称修改与复制。语言和链接偏好部分路径只更新按钮文案。 | `settings-1`、`settings-2`。第 3 步按接线状态对齐。 [设置面板](../../src/UI/Controls/SettingsPanels) |
| 外观、快捷键 | 检查三种主题、五种强调色、界面和正文独立字号；核验即时变化与重启保持。快捷键页检查显示项与当前实际输入键。 | `settings-3`、`settings-4`。第 3 步复测。 [外观面板](../../src/UI/Controls/SettingsPanels/AppearanceSettingsPanel.cs) |
| 文件传输、聊天记录 | 检查「更改」接收目录及自动接收，核对「更改」聊天记录位置、全清确认、重启与迁移。 | `settings-5`、`settings-6`。第 2 步保留已有路径，第 3 步核对用户主动迁移和界面。 [聊天记录面板](../../src/UI/Controls/SettingsPanels/ChatHistorySettingsPanel.cs) |
| 设备连接、关于与更新 | 核对设备连接信息、展示性选项、版本、反馈、诊断复制、版本检查与平台对应的安装入口。 | `settings-7`、`settings-8`、`10-maintenance`。第 3 步验收。 [关于面板](../../src/UI/Controls/SettingsPanels/AboutSettingsPanel.cs) |
| 托盘及窗口 | 关闭窗口后可由托盘、桌面启动器恢复，核对「显示主界面」「系统设置…」「检查新版本…」「退出 LanChat」、未读预览及跳转。最小化、还原、最大化、跨屏和系统贴靠分别验证。 | `09-tray-preview`。首包验收显式退出及端口释放，第 3 步验证恢复、通知和窗口。 [托盘源码](../../src/MainForm.Tray.cs) |

WinForms 实测客户区与设计规范表有两处尺寸差异。默认侧栏按 WinForms 420px，设置窗按 WinForms 1260×820 并适应工作区。功能状态以源码入口与实际动作核对，截图用来验收布局。第 3 步再用 100%、125%、150% 及窄窗同尺寸叠图验证两端客户区。 [GrokBot 设计规范](../../Docs/GrokBot_UI_Design_System.md)

## 支持环境与放行

| 发行版 | amd64 | arm64 |
| --- | --- | --- |
| Ubuntu 22.04 LTS | GNOME Xorg | GNOME Xorg |
| Ubuntu 24.04 LTS | GNOME Xorg、原生 GNOME Wayland、GNOME XWayland | GNOME Xorg |

每个发行版和架构组合验证安装、实际旧包升级、回退、GUI、CLI 和退出。Xorg、原生 Wayland 和 XWayland 按实际连接的显示后端取证。原生 Wayland 自绘窗口操作缺失时使用系统边框。托盘恢复实测成功后才允许隐藏，托盘不可确认时保留窗口或任务切换器中的恢复入口。未读始终有应用内入口。fcitx5、ibus 至少覆盖各自的预编辑、确认与发送／换行。KDE、ARM64 Wayland 及额外分数缩放列为未承诺组合。

期末须有物理 x86_64、物理 ARM64 各自完成升级和回退，分别与公开 Windows v2.0.16 物理机做真实 LAN 发现、双向私聊和群聊、双向文件 SHA-256 与退出。记录网卡、路由、端口和发现时间；TUN、多网卡、无默认路由及 v2.0.15、公开旧 ELF 互通均为核心回归。 [采纳定稿](FINAL.md) · [放行顺序](REVIEW-R2.md)

## 测量口径

- 产物按字节、SHA-256、Debian 包字段、Electron 和 ELF 的 ELF64 机器码记账。要区分压缩包、安装后文件以及独立 ELF。
- 窗口在默认缩放、默认主题与指定数据集记录客户区尺寸、字体和实际后端。性能分冷启动至窗口就绪、服务 ready、发现对端；记录空闲状态的进程树 PSS 和 ELF PSS，长历史与 64 KiB 文件流单独采样。采样报告注明并发数、文件大小、宿主／来宾、页缓存状态和加载时间。
