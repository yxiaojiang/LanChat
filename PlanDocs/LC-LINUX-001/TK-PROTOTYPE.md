# LC-LINUX-001 · 步骤 3a Tk 原型

2026-11-19 已构建 Canvas 和资源辅助 Text 两种原型。正式选型待 Linux 实机取证。原型与发布构建分开，资源生成仅在 Windows 运行。 [原型源码](../../tests/linux_ui_prototype/prototype.py) · [GDI+ 生成器](../../tests/linux_ui_prototype/ExportAssets.cs)

## Windows 宿主检查

Windows 10 19045，Python 3.13，Tk 8.6.15，`tk windowingsystem=win32`，`tk scaling=1.33465`。两种强调色、四个资源倍率共 56 张 PNG，合计 42,576 字节。100% 和 125% 资源组各成功载入 7 张，模拟资源缺失时仍可启动两种视图。倍率参数选择 PNG 和原型逻辑尺寸，系统缩放保持宿主实测值。

| 资源组与视图 | 2000 条切换（ms） | 20 次滚动定位（ms） | 更换倍率重建消息流（ms） | 滚动比例偏差 |
| --- | ---: | ---: | ---: | ---: |
| 100% 魅紫 Canvas | 206.10 | 75.76 | 238.51 | 0 |
| 100% 魅紫 Text | 50.81 | 183.39 | 48.04 | 0.00388 |
| 125% 湖蓝 Canvas | 204.37 | 61.95 | 250.09 | 0 |
| 125% 湖蓝 Text | 48.21 | 175.48 | 38.16 | 0.00363 |

Canvas 原型支持单条选择复制，Text 可用原生连续选区。宿主实际 Tk 测试验证了叠加层 Escape 和输入焦点恢复。当前滚动基准用固定比例定位，真实滚轮行为和消息锚点留给桌面实测。

## Linux 实机待办

先在 Windows 运行下面的资源生成命令，再将 PNG 目录与源码一起带到 Linux。分别在原生 Xorg 和 GNOME Wayland 登录会话执行原型，记录实际 Tk 后端与是否经过 XWayland。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/linux_ui_prototype/export-assets.ps1 -Output bin/lanchat-ui-prototype
```

```sh
python3 tests/linux_ui_prototype/prototype.py bin/lanchat-ui-prototype --benchmark
python3 tests/linux_ui_prototype/prototype.py bin/lanchat-ui-prototype --benchmark --scale 125 --accent 5C9CFF
python3 tests/linux_ui_prototype/prototype.py bin/lanchat-ui-prototype --scale 125 --accent 5C9CFF
```

- 普通及桌面分数缩放下保存两种方案的截图，核对边缘透明度、对比度、圆钮、胶囊、浮动滑块和气泡角。旧 Tk 和资源回退各跑一次。
- Ctrl+1／Ctrl+2 切换视图，核对滚动锚点、字体、换行和选择复制。使用 fcitx5 与 ibus 分别测试候选窗、预编辑、Enter 提交与发送、Ctrl+Enter 换行。
- Ctrl+M 检查窗口内选人、方向键、Escape、点外关闭和焦点恢复。Ctrl+O 检查系统文件选择器与返回焦点。
- 补齐 Linux 截图和选型结论后关闭 3a，正式双栏及设置再进入 3b。
