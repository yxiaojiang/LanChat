# SessionBrief.md — 当前接手摘要

> 最近刷新：2026-11-02。仓库：`https://github.com/yxiaojiang/LanChat.git`。
> 当前分支：`feature/wechat-ui`（稳定基线 Tag `v2.1.0`，当前开发版本 `v2.0.4`）。
> 核心里程碑：**GrokBot 1:1**；P0 64px 胶囊已恢复；**本步已完成：1) Windows Installer 1638 升级拦截彻底根治（WiX 移除硬编码 ProductCode，改由构建时动态自增生成 GUID，结合 UpgradeCode 契约实现全自动 MajorUpgrade）；2) 卸载程序 `src/Uninstall.cs` 升级为 Win32 `MsiEnumRelatedProducts` 动态探测已安装 ProductCode；3) 发布全工程版本原子升级至 `v2.0.4` 并全量上传 GitHub Releases 闭环**；方案 B 冻结。
> 注：WinForms/GDI+ 无法与 Electron 像素级完全一致；本步用 grayscale AntiAlias + Bold/luma 逼近。

---

## 一、 本轮工作态（MSI 1638 升级冲突根治 / 全自动 MajorUpgrade / 卸载动态感知）

### 1. 用户反馈
- 客户端检测到在线更新并下载完成后，启动 MSI 安装程序弹出 Windows Installer 阻断错误：“已经安装了该产品的另一个版本。无法继续安装此版本。可以使用‘控制面板’中‘添加/删除程序’来配置或删除该产品现有版本。”（错误码 1638）。

### 2. 根因剖析与根治方案
- **根因分析：**
  - 在 Windows Installer 升级规范中，大版本平滑升级（`MajorUpgrade`）的硬性契约是：`UpgradeCode` 必须在全产品生命周期内全局恒定（用于系统识别产品族），而每次打包发布新版本时 `ProductCode` **必须不同**。
  - 原工程 `packaging/Package.wxs` 中静态写死了 `ProductCode="{7E993262-EAA5-4A37-975D-39F24FEA21D2}"`。无论版本如何自增，打出的 MSI 包 `ProductCode` 永远完全一致。
  - 当用户机器上已装有旧版（如 2.0.0）时，运行相同 `ProductCode` 的新安装包会被 Windows Installer 判定为冲突安装，直接抛出 1638 错误终止。
- **根治措施：**
  - **WiX 5 打包配置纠正（`packaging/Package.wxs`）：** 剔除 `<Package>` 标签中硬编码的 `ProductCode` 属性。WiX 5 在未显式指定 `ProductCode` 时，会在每次构建时自动为安装包生成全新的唯一 GUID，同时严格保持 `UpgradeCode="{7E993262-EAA5-4A37-975D-39F24FEA21D1}"` 不变；
  - **卸载向导动态感知升级（`src/Uninstall.cs`）：** 原卸载程序中亦静态写死了旧的 `PRODUCT_CODE`。本次引入原生 Win32 `msi.dll` 的 `MsiEnumRelatedProducts` API，卸载时根据静态 `UpgradeCode` 动态获取当前操作系统中实际安装生效的动态 `ProductCode`，并保留静态旧 Code 作为兜底，保证无论是旧版还是未来任意动态版本均可一键干净卸载。
  - **全工程版本原子推进至 `v2.0.4`：** 调用 `packaging/bump-version.py 2.0.4` 同步 5 处核心文件，执行 `Tools/Publish.bat` 成功编译生成 `LanChat.msi`、`uninstall.exe` 及 Linux deb 包，并流式发布至 GitHub Releases `v2.0.4`。
  - **实机属性验证：** 验证生成的 `release\LanChat.msi` 属性表：`ProductCode` 动态变更为 `{20BFE067-AFE5-4E7D-9441-62817193E585}`，`UpgradeCode` 为 `{7E993262-EAA5-4A37-975D-39F24FEA21D1}`，符合 Windows Installer `MajorUpgrade` 规范。用户通过旧版客户端更新时将自动静默卸载旧版本并平滑安装最新版。

### 3. 已知剩余缺口（下一 pass）
- Create 窗 Docs `680×620`、About Docs `440×560` 需重排绝对布局后才能改尺寸。
- Settings 卡片内部分 RGB 与 54px 行已较近，仍可对照 Alis4 像素微调。
- WinForms ListBox 原生滚动条、无 CS_DROPSHADOW 的平台差异无法 1:1 GrokBot。
- `GrokTheme` 令牌尚未全面替换散落的 `FromArgb`。

---

## 二、 关键约束与设计原则

1. C# 5.0 / .NET Framework 4.0，`csc` 直编。
2. Rule 9 + `Docs/GrokBot_UI_Design_System.md`。
3. UDP 38888 / TCP 38889+ / `LANC` 不变。
4. WiX 安装包 `UpgradeCode` 绝不可变，`ProductCode` 必须动态自增。

---

## 三、 模块索引（增量）

| 模块 / 文件 | 说明 |
| :--- | :--- |
| `packaging/Package.wxs` | 移除硬编码 `ProductCode`，构建期自动随机 GUID 启用全自动 MajorUpgrade |
| `src/Uninstall.cs` | 引入 `MsiEnumRelatedProducts` 动态获取当前系统的 ProductCode 进行卸载 |
| `packaging/bump-version.py` | 全工程版本号自动递增与原子同步工具 |
| `Tools/Publish.bat` | Windows 全自动版本自增编译打包与发布脚本 |
| `Tools/Compile.bat` | 极速原生 csc 纯编译校验 |
