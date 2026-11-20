#!/usr/bin/env python3
"""从源码目录启动 LanChat。单文件分发入口由 zipapp 生成。"""

from lanchat_app.app import main


if __name__ == "__main__":
    raise SystemExit(main())
