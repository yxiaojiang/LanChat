# 当前代码审核状态 (CRDocs)

- **Task ID**：`LC-LINUX-002-STEP-3-SLICE-1`
- **Topic**：Linux Electron 桌面重构 — 第 3 步切片 1（主窗客户区对齐：双栏、会话、聊天、文件卡片、暗色菜单与浮动滑块）
- **Status**：**APPROVED**
- **Reviewer**：独立审核方
- **Last updated at**：2026-11-23
- **Review Report**：`CRDocs/REVIEW.md`

## 结论

本轮代码复核结论为 **`APPROVED`**（Round 3）。

Round 2 的阻塞项已闭环：

1. **R2-P0-1**：可编辑控件不再拦截方向键、`Home`/`End`/`PageUp`/`PageDown`；满高后 `input` 与 `syncComposeHeight()` 会刷新滑块；输入框右侧留出 `10px`。
2. **R2-P0-2**：`call_collect()` 在同一次消费中保留 `delete-session` 响应前的事件帧。清空群聊和删除离线会话期望 `peers` 为 0，删除在线好友期望恰好 1 次。文档已改为 .NET SDK 10.0.404，并保留「宿主机无 QEMU、新 ELF 未实跑 delete/history」的边界。
3. **R2-P1-1**：`suppressAttachToggle` 维持到 `pointerup`/`pointercancel` 之后再清，70ms 按压下二次点击可以关闭菜单。
4. **R2-P2**：末尾才滚底、单气泡 `Ctrl+A`，以及滑块和 `+` 按钮单测，均已落地。

本地复核：`"C:\Program Files\nodejs\node.exe" tests/linux_desktop_ui_regression.js` 输出 `linux desktop ui regression: ok`；`python -m py_compile packaging/build-electron.py packaging/build_linux.py tests/linux_desktop_regression.py` 通过。手写文件均低于 1000 行，最长为 `renderer.js` 792 行。

**不挂起等待 VM。** 缺 Linux/QEMU 实跑只说明 `delete`/`history` 还不能记成已通过的 ELF 证据，不再说明测试代码必然失败。第 3 步的图形后端、输入法、缩放、长历史、设置八页和托盘通知仍未通过，见 `PlanDocs/LC-LINUX-002/INTEGRATION.md`。
