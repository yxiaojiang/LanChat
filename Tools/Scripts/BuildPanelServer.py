#!/usr/bin/env python3
"""提供仅监听本机回环地址的构建面板，并运行固定的项目脚本。"""

from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
import threading
from urllib.parse import parse_qs, urlsplit
import webbrowser


SCRIPTS_DIR = Path(__file__).resolve().parent
TOOLS_DIR = SCRIPTS_DIR.parent
PROJECT_ROOT = TOOLS_DIR.parent
HTML_PATH = SCRIPTS_DIR / "BuildPanel.html"
LOCK = threading.RLock()
PANEL_TOKEN = secrets.token_urlsafe(32)
STATE = {
    "running": False,
    "action": None,
    "action_name": None,
    "started_at": None,
    "exit_code": None,
    "lines": [],
}

ACTIONS = {
    "compile": ("快速编译", SCRIPTS_DIR / "Compile.bat"),
    "link": ("编译并覆盖安装", SCRIPTS_DIR / "CompileAndLink.bat"),
    "publish": ("构建并发布正式版", SCRIPTS_DIR / "Publish.bat"),
    "linux-deb": ("构建 Linux 桌面 deb", None),
}


def now_iso():
    """生成供浏览器解析的本地任务时间戳。"""
    return datetime.now(timezone.utc).isoformat()


def add_log(line):
    """在线程锁内追加一行日志，保留进程输出的原始顺序。"""
    with LOCK:
        STATE["lines"].append(line.rstrip("\r\n"))


def start_action(action, payload):
    """启动白名单任务并初始化本次日志状态。"""
    if action not in ACTIONS:
        raise ValueError("未知任务。")
    if action in ("link", "publish") and payload.get("confirmed") is not True:
        raise PermissionError("此任务需要在界面中明确确认。")
    if os.name != "nt":
        raise RuntimeError("Windows 编译脚本只能在 Windows 上运行。")

    action_name, script = ACTIONS[action]
    environment = os.environ.copy()
    if action == "link":
        environment["LANCHAT_RUN_AFTER_LINK"] = "Y" if payload.get("launchAfterLink") is True else "N"
        command = [os.environ.get("COMSPEC", "cmd.exe"), "/d", "/s", "/c", f'call "{script}"']
        encoding = "mbcs"
    elif action == "publish":
        command = [os.environ.get("COMSPEC", "cmd.exe"), "/d", "/s", "/c", f'call "{script}"']
        encoding = "mbcs"
    elif action == "compile":
        command = [os.environ.get("COMSPEC", "cmd.exe"), "/d", "/s", "/c", f'call "{script}"']
        encoding = "mbcs"
    else:
        command = [sys.executable, str(SCRIPTS_DIR / "BuildLinuxDeb.py")]
        environment["PYTHONUTF8"] = "1"
        environment["PYTHONIOENCODING"] = "utf-8"
        encoding = "utf-8"

    with LOCK:
        if STATE["running"]:
            raise RuntimeError("已有任务正在运行，请等待它结束后再启动新任务。")
        try:
            process = subprocess.Popen(
                command,
                cwd=PROJECT_ROOT,
                env=environment,
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                encoding=encoding,
                errors="replace",
                bufsize=1,
            )
        except OSError as exc:
            raise RuntimeError(f"启动失败：{exc}") from exc
        STATE.update({
            "running": True,
            "action": action,
            "action_name": action_name,
            "started_at": now_iso(),
            "exit_code": None,
            "lines": [f"[{datetime.now().strftime('%H:%M:%S')}] 启动任务：{action_name}"],
        })

    threading.Thread(target=collect_output, args=(process, action_name), daemon=True).start()
    return action_name


def collect_output(process, action_name):
    """异步读取构建进程输出，避免占用 HTTP 请求线程。"""
    try:
        for line in process.stdout:
            add_log(line)
    except (OSError, UnicodeError) as exc:
        add_log(f"[日志读取错误] {exc}")
    finally:
        exit_code = process.wait()
        add_log(f"任务结束：{action_name}，退出码 {exit_code}。")
        with LOCK:
            STATE["running"] = False
            STATE["exit_code"] = exit_code


class BuildPanelHandler(BaseHTTPRequestHandler):
    """返回面板静态页与任务状态，不接受任意文件或命令路径。"""

    server_version = "LanChatBuildPanel/1.0"

    def log_message(self, _format, *_args):
        """将请求访问日志留在面板内，避免混入构建日志。"""

    def send_json(self, status, payload):
        data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        parsed = urlsplit(self.path)
        if parsed.path == "/":
            try:
                page = HTML_PATH.read_text(encoding="utf-8").replace("__PANEL_TOKEN__", PANEL_TOKEN)
            except OSError as exc:
                self.send_error(500, str(exc))
                return
            data = page.encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(data)))
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.send_header("Referrer-Policy", "no-referrer")
            self.end_headers()
            self.wfile.write(data)
            return
        if parsed.path == "/api/status":
            query = parse_qs(parsed.query)
            try:
                cursor = max(0, int(query.get("after", ["0"])[0]))
            except ValueError:
                cursor = 0
            with LOCK:
                lines = STATE["lines"]
                self.send_json(200, {
                    "running": STATE["running"],
                    "actionName": STATE["action_name"],
                    "startedAt": STATE["started_at"],
                    "exitCode": STATE["exit_code"],
                    "lines": lines[cursor:],
                    "next": len(lines),
                })
            return
        self.send_error(404)

    def do_POST(self):
        if self.path != "/api/run":
            self.send_error(404)
            return
        if self.headers.get("X-Panel-Token") != PANEL_TOKEN:
            self.send_json(403, {"error": "页面令牌无效，请刷新面板。"})
            return
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if length <= 0 or length > 4096:
                raise ValueError("请求内容长度无效。")
            payload = json.loads(self.rfile.read(length).decode("utf-8"))
            if not isinstance(payload, dict):
                raise ValueError("请求内容必须是 JSON 对象。")
            action_name = start_action(payload.get("action"), payload)
            self.send_json(202, {"actionName": action_name})
        except (ValueError, PermissionError, RuntimeError, json.JSONDecodeError) as exc:
            self.send_json(400, {"error": str(exc)})


def main():
    """启动随机端口的回环 HTTP 服务，并在默认浏览器打开控制台。"""
    server = ThreadingHTTPServer(("127.0.0.1", 0), BuildPanelHandler)
    server.daemon_threads = True
    address = f"http://127.0.0.1:{server.server_port}/"
    print(f"LanChat 构建控制台：{address}")
    print("保持此窗口打开；按 Ctrl+C 关闭本机服务。")
    webbrowser.open(address)
    try:
        server.serve_forever(poll_interval=0.25)
    except KeyboardInterrupt:
        print("\n正在关闭构建控制台服务。")
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
