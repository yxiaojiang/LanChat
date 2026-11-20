"""选择桌面或命令行入口，在需要图形界面时才导入 Tk。"""

import os
import shutil
import subprocess
import sys

from .cli import run_cli
from .network import LanChatService, UDP_PORT
from .storage import handle_uncaught_exception, log_message


def main():
    """启动 LanChat，返回进程退出码。显式 CLI 和无头模式不加载 Tk。"""
    sys.excepthook = handle_uncaught_exception
    log_message("INFO", f"=== LanChat 启动 (PID: {os.getpid()}) ===")
    force_cli = "--cli" in sys.argv or "-c" in sys.argv
    has_display = bool(os.environ.get("DISPLAY") or os.environ.get("WAYLAND_DISPLAY") or sys.platform == "win32")
    gui_type = None
    if has_display and not force_cli:
        try:
            from .gui import WeChatGui
            gui_type = WeChatGui
        except ImportError as exc:
            log_message("ERROR", f"无法加载 Tk 桌面界面：{exc}")
            if not sys.stdin.isatty():
                message = "缺少 Tk，图形界面需要 python3-tk。请运行 sudo apt install python3-tk"
                zenity, notify = shutil.which("zenity"), shutil.which("notify-send")
                try:
                    if zenity:
                        subprocess.Popen([zenity, "--error", "--title=LanChat", f"--text={message}"])
                    elif notify:
                        subprocess.Popen([notify, "LanChat", message])
                except OSError as notify_error:
                    log_message("WARN", f"无法显示桌面提示：{notify_error}")
                print(message, file=sys.stderr)
                return 1

    service = LanChatService()
    try:
        if gui_type is not None:
            try:
                gui = gui_type(service)
            except Exception as exc:
                log_message("ERROR", f"启动图形界面失败：{exc}")
                print(f"启动图形界面失败：{exc}", file=sys.stderr)
                if not sys.stdin.isatty():
                    return 1
            else:
                service.start()
                gui.run()
                return 0
        service.start()
        log_message("INFO", f"网络服务就绪：{service.primary_ip}:{service.tcp_port}，UDP {UDP_PORT}")
        run_cli(service)
        return 0
    finally:
        if service.is_running:
            service.close()
