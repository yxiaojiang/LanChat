"""定位安装和便携配置，管理接收目录与诊断日志。"""

import json
import os
from pathlib import Path
import sys
import tempfile
import time
import traceback
import uuid


def application_directory():
    """返回源码启动脚本或 zipapp 所在的目录。"""
    root = Path(__file__).parents[1]
    return (root.parent if root.suffix == ".pyz" and root.is_file() else root).resolve()


def is_installed():
    """按系统安装路径识别安装形态，以便选择用户数据路径。"""
    parts = application_directory().parts
    return len(parts) > 1 and parts[0] == "/" and parts[1] in ("usr", "etc", "opt")


LOG_DIR = os.path.expanduser("~/.local/share/lanchat")
try:
    os.makedirs(LOG_DIR, exist_ok=True)
except Exception:
    pass

LOG_FILE_USER = os.path.join(LOG_DIR, "lanchat_python.log")
LOG_FILE_TMP = os.path.join(tempfile.gettempdir(), f"lanchat_{os.environ.get('USER', 'default')}.log")

def log_message(level: str, msg: str):
    """记录日志到用户目录及 /tmp 备用目录"""
    t_str = time.strftime("%Y-%m-%d %H:%M:%S")
    formatted = f"[{t_str}] [{level}] {msg}\n"
    for log_path in (LOG_FILE_USER, LOG_FILE_TMP):
        try:
            with open(log_path, "a", encoding="utf-8") as f:
                f.write(formatted)
        except Exception:
            pass

def handle_uncaught_exception(exc_type, exc_value, exc_traceback):
    """全局未捕获异常崩溃记录"""
    if issubclass(exc_type, KeyboardInterrupt):
        sys.__excepthook__(exc_type, exc_value, exc_traceback)
        return
    tb_text = "".join(traceback.format_exception(exc_type, exc_value, exc_traceback))
    log_message("CRITICAL", "未捕获异常崩溃:\n" + tb_text)
    print("\n\033[31m[CRITICAL ERROR] 程序发生未捕获异常崩溃:\033[0m")
    print(tb_text)
    print(f"\033[33m完整错误日志已写入:\n  1. {LOG_FILE_USER}\n  2. {LOG_FILE_TMP}\033[0m\n")


def determine_save_dir() -> str:
    """智能选择接收文件目录，防止在 /usr/lib 系统只读目录下崩溃"""
    base_dir = str(application_directory())
    is_system_path = is_installed()
    if not is_system_path:
        preferred = os.path.join(base_dir, "ReceivedFiles")
        try:
            os.makedirs(preferred, exist_ok=True)
            test_file = os.path.join(preferred, ".write_test_" + uuid.uuid4().hex[:8])
            with open(test_file, "w") as f:
                f.write("test")
            os.remove(test_file)
            return preferred
        except Exception:
            pass

    home = os.path.expanduser("~")
    downloads = os.path.join(home, "Downloads", "LanChat_Received")
    try:
        os.makedirs(downloads, exist_ok=True)
        return downloads
    except Exception:
        pass

    local_share = os.path.join(home, ".local", "share", "lanchat", "ReceivedFiles")
    try:
        os.makedirs(local_share, exist_ok=True)
        return local_share
    except Exception:
        pass

    tmp_dir = os.path.join(tempfile.gettempdir(), f"lanchat_{os.environ.get('USER', 'default')}_received")
    os.makedirs(tmp_dir, exist_ok=True)
    return tmp_dir

def get_settings_file_path() -> str:
    """获取配置文件路径：优先便携模式 (当前脚本同级 settings.json)，默认 ~/.config/lanchat/settings.json 或 ~/.local/share/lanchat/settings.json"""
    base_dir = str(application_directory())
    portable = os.path.join(base_dir, "settings.json")
    if os.path.exists(portable):
        return portable
    is_system_path = is_installed()
    if not is_system_path:
        try:
            with open(portable, "a") as f:
                pass
            return portable
        except Exception:
            pass

    home = os.path.expanduser("~")
    config_dir = os.path.join(home, ".config", "lanchat")
    try:
        os.makedirs(config_dir, exist_ok=True)
        return os.path.join(config_dir, "settings.json")
    except Exception:
        fallback = os.path.join(home, ".local", "share", "lanchat")
        os.makedirs(fallback, exist_ok=True)
        return os.path.join(fallback, "settings.json")

def load_settings() -> dict:
    path = get_settings_file_path()
    try:
        if os.path.exists(path):
            with open(path, "r", encoding="utf-8") as f:
                data = json.load(f)
                if isinstance(data, dict):
                    return data
    except Exception:
        pass
    return {}

def save_settings(settings: dict):
    path = get_settings_file_path()
    temporary = None
    parent = os.path.dirname(path)
    try:
        os.makedirs(parent, exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=parent, mode="w", encoding="utf-8", delete=False) as f:
            temporary = Path(f.name)
            json.dump(settings, f, indent=2, ensure_ascii=False)
        temporary.replace(path)
    except Exception as e:
        log_message("WARN", f"保存配置文件失败: {e}")
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)
