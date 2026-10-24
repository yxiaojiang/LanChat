#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Build Debian packages (.deb) for LanChat
Produces:
  - lanchat_1.0.0_amd64.deb (Self-contained x86_64 ELF + Python fallback)
  - lanchat_1.0.0_all.deb   (Universal all-architecture zero-dependency edition)
"""

import os
import io
import tarfile
import time
import shutil

VERSION = "2.0.0"
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT_DIR = os.path.dirname(SCRIPT_DIR)
RELEASE_DIR = os.path.join(ROOT_DIR, "release")

def make_ar_header(name: str, size: int) -> bytes:
    # BSD ar format header: 60 bytes
    return (name.ljust(16) +
            str(int(time.time())).ljust(12) +
            "0".ljust(6) +
            "0".ljust(6) +
            "100644".ljust(8) +
            str(size).ljust(10) +
            "`\n").encode("ascii")

def create_deb(deb_path: str, control_fields: dict, files: list):
    """
    files is a list of tuples: (archive_path, data_bytes, mode)
    """
    os.makedirs(os.path.dirname(deb_path), exist_ok=True)
    mtime = int(time.time())

    # 1. debian-binary
    debian_binary = b"2.0\n"

    # 2. control.tar.gz
    ctrl_lines = []
    for k, v in control_fields.items():
        ctrl_lines.append(f"{k}: {v}")
    ctrl_data = ("\n".join(ctrl_lines) + "\n").encode("utf-8")

    buf_ctrl = io.BytesIO()
    with tarfile.open(fileobj=buf_ctrl, mode="w:gz") as tar:
        # control file
        ti = tarfile.TarInfo(name="./control")
        ti.size = len(ctrl_data)
        ti.mtime = mtime
        ti.mode = 0o644
        ti.uname = "root"
        ti.gname = "root"
        tar.addfile(ti, io.BytesIO(ctrl_data))

        # postinst
        postinst = b"#!/bin/sh\nset -e\nchmod +x /usr/bin/lanchat 2>/dev/null || true\nexit 0\n"
        ti_p = tarfile.TarInfo(name="./postinst")
        ti_p.size = len(postinst)
        ti_p.mtime = mtime
        ti_p.mode = 0o755
        ti_p.uname = "root"
        ti_p.gname = "root"
        tar.addfile(ti_p, io.BytesIO(postinst))

    ctrl_gz = buf_ctrl.getvalue()

    # 3. data.tar.gz
    buf_data = io.BytesIO()
    with tarfile.open(fileobj=buf_data, mode="w:gz") as tar:
        # Add parent directories
        dirs = set()
        for fpath, _, _ in files:
            parts = fpath.strip("./").split("/")
            for i in range(1, len(parts)):
                dirs.add("./" + "/".join(parts[:i]))
        for d in sorted(dirs):
            ti_d = tarfile.TarInfo(name=d)
            ti_d.type = tarfile.DIRTYPE
            ti_d.mtime = mtime
            ti_d.mode = 0o755
            ti_d.uname = "root"
            ti_d.gname = "root"
            tar.addfile(ti_d)

        # Add files
        for fpath, data_bytes, mode in files:
            ti = tarfile.TarInfo(name=fpath)
            ti.size = len(data_bytes)
            ti.mtime = mtime
            ti.mode = mode
            ti.uname = "root"
            ti.gname = "root"
            tar.addfile(ti, io.BytesIO(data_bytes))

    data_gz = buf_data.getvalue()

    # 4. Pack into ar archive
    with open(deb_path, "wb") as f:
        f.write(b"!<arch>\n")
        for name, content in [("debian-binary", debian_binary),
                              ("control.tar.gz", ctrl_gz),
                              ("data.tar.gz", data_gz)]:
            f.write(make_ar_header(name, len(content)))
            f.write(content)
            if len(content) % 2 != 0:
                f.write(b"\n")

    print(f"[SUCCESS] Generated: {deb_path} ({len(data_gz) + len(ctrl_gz) + len(debian_binary)} bytes)")

def build_packages():
    print("=" * 60)
    print("        Building LanChat Linux Debian Packages (.deb)")
    print("=" * 60)

    # Read lanchat.py
    py_path = os.path.join(ROOT_DIR, "src", "linux", "lanchat.py")
    with open(py_path, "rb") as f:
        py_bytes = f.read()

    desktop_entry = b"""[Desktop Entry]
Version=1.0
Type=Application
Name=LanChat
GenericName=LAN Messenger
Comment=Lightweight LAN instant messaging and file transfer
Exec=lanchat
Icon=lanchat
Terminal=false
Categories=Network;InstantMessaging;
Keywords=chat;lan;file;transfer;
"""

    svg_path = os.path.join(ROOT_DIR, "assets", "lanchat.svg")
    if os.path.exists(svg_path):
        with open(svg_path, "rb") as f:
            svg_icon = f.read()
    else:
        svg_icon = b"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">
  <rect width="64" height="64" rx="14" fill="#07c160"/>
  <path d="M18 20 C18 15.58 24.27 12 32 12 C39.73 12 46 15.58 46 20 C46 24.42 39.73 28 32 28 C29.98 28 28.07 27.76 26.39 27.32 L20 30 L21.5 25.5 C19.34 23.95 18 22.08 18 20 Z" fill="#ffffff"/>
  <path d="M30 28 C30 25.24 34.48 23 40 23 C45.52 23 50 25.24 50 28 C50 30.76 45.52 33 40 33 C38.56 33 37.2 32.83 36 32.52 L31.43 34.43 L32.5 31.25 C30.96 30.15 30 29.13 30 28 Z" fill="#e0f8ea"/>
</svg>
"""

    png_path = os.path.join(ROOT_DIR, "assets", "app.png")
    png_bytes = None
    if os.path.exists(png_path):
        with open(png_path, "rb") as f:
            png_bytes = f.read()

    launcher_script = """#!/bin/bash
# LanChat Linux Launcher with Diagnostics Logging & Anti-Crash Guard
# Priority: Python 3 WeChat Desktop GUI -> Fallback to Terminal CLI / Native ELF

DIR="/usr/lib/lanchat"
LOG_DIR="$HOME/.local/share/lanchat"
mkdir -p "$LOG_DIR" 2>/dev/null
USER_LOG="$LOG_DIR/launcher.log"
TMP_LOG="/tmp/lanchat_${USER:-default}_launch.log"

log() {
    local MSG="[$(date '+%Y-%m-%d %H:%M:%S')] $1"
    echo "$MSG" >> "$USER_LOG" 2>/dev/null
    echo "$MSG" >> "$TMP_LOG" 2>/dev/null
}

log "========================================================"
log "LanChat 启动器已调用 (PID: $$, USER: ${USER:-unknown})"
log "OS: $(cat /etc/os-release 2>/dev/null | grep PRETTY_NAME | cut -d= -f2 | tr -d '\"' || uname -s)"
log "Kernel: $(uname -r) | Arch: $(uname -m)"
log "Python3: $(python3 --version 2>/dev/null || echo 'not found')"
log "DISPLAY: $DISPLAY | WAYLAND_DISPLAY: $WAYLAND_DISPLAY"
log "ARGS: $*"
log "========================================================"

HAS_GUI_ENV=0
if [ -n "$DISPLAY" ] || [ -n "$WAYLAND_DISPLAY" ]; then
    HAS_GUI_ENV=1
fi

FORCE_CLI=0
for arg in "$@"; do
    if [ "$arg" = "--cli" ] || [ "$arg" = "-c" ]; then
        FORCE_CLI=1
        break
    fi
done

# 1. 桌面图形环境首选：优先直接拉起微信暗黑 GUI (无黑框、全功能)
if [ $HAS_GUI_ENV -eq 1 ] && [ $FORCE_CLI -eq 0 ]; then
    log "[INFO] 检测到图形桌面环境，优先以微信暗黑风格 GUI 启动..."
    if command -v python3 >/dev/null 2>&1; then
        if python3 -c "import tkinter" >/dev/null 2>&1; then
            log "[INFO] 正在启动 Python WeChat GUI: $DIR/lanchat.py"
            python3 "$DIR/lanchat.py" "$@"
            EXIT_CODE=$?
            log "[INFO] Python WeChat GUI 退出码: $EXIT_CODE"
            exit $EXIT_CODE
        else
            log "[WARN] 检测到未安装 python3-tk，图形界面无法初始化"
            MSG="LanChat 启动微信暗黑图形界面需要 python3-tk 组件。\n请在终端运行以下命令安装：\n\nsudo apt install python3-tk"
            if command -v zenity >/dev/null 2>&1; then
                zenity --warning --title="LanChat 提示" --text="$MSG" --width=350 2>/dev/null &
            elif command -v notify-send >/dev/null 2>&1; then
                notify-send "LanChat 提示" "缺少 python3-tk 组件，请执行 sudo apt install python3-tk" 2>/dev/null
            fi
        fi
    fi
fi

# 2. 终端环境自适应检测 (若在无 TTY 环境下需要运行 CLI，则主动拉起终端窗口)
if [ ! -t 0 ] || [ ! -t 1 ]; then
    log "[INFO] 终端命令行模式：检测到无交互终端环境，尝试唤起系统可用终端窗口..."
    for TERM_CMD in x-terminal-emulator gnome-terminal mate-terminal ukui-terminal deepin-terminal konsole xfce4-terminal terminator tilix lxterminal alacritty kitty xterm; do
        if command -v "$TERM_CMD" >/dev/null 2>&1; then
            log "[INFO] 找到可用终端模拟器: $TERM_CMD，正在拉起交互窗口..."
            case "$TERM_CMD" in
                gnome-terminal)
                    exec gnome-terminal -- /usr/bin/lanchat --cli "$@"
                    ;;
                mate-terminal|ukui-terminal|xfce4-terminal|lxterminal)
                    exec "$TERM_CMD" -e "/usr/bin/lanchat --cli $*"
                    ;;
                konsole)
                    exec konsole -e /usr/bin/lanchat --cli "$@"
                    ;;
                *)
                    exec "$TERM_CMD" -e /usr/bin/lanchat --cli "$@"
                    ;;
            esac
        fi
    done
    log "[WARN] 未找到可用终端模拟器，将以无交互后台模式继续执行"
fi

EXIT_CODE=0

# 3. 命令行模式下优先使用 Python 3 通用版
if command -v python3 >/dev/null 2>&1; then
    log "[INFO] 正在使用 Python 3 启动: $DIR/lanchat.py"
    python3 "$DIR/lanchat.py" "$@"
    EXIT_CODE=$?
    log "[INFO] Python 3 退出码: $EXIT_CODE"
elif [ -x "$DIR/lanchat-bin" ]; then
    log "[INFO] Python 3 不可用，尝试启动 Linux 原生自包含 ELF 二进制: $DIR/lanchat-bin"
    "$DIR/lanchat-bin" "$@"
    EXIT_CODE=$?
    log "[INFO] 原生二进制执行退出码: $EXIT_CODE"
else
    log "[FATAL] 系统既无法运行 Python 3，也未找到原生可执行文件"
    echo "[Error] 无法启动 LanChat：未找到兼容的执行环境 (缺少 Python 3)。" >&2
    EXIT_CODE=1
fi

# 4. 防闪退兜底机制：若退出异常且处于交互终端中，阻止窗口秒退并提示查看日志
if [ $EXIT_CODE -ne 0 ] && [ $EXIT_CODE -ne 130 ]; then
    log "[ERROR] LanChat 异常退出，退出码: $EXIT_CODE"
    echo ""
    echo "=========================================================================="
    echo " [LanChat 启动或运行异常 / Launch Error] (退出码: $EXIT_CODE)"
    echo " 程序发生异常退出，详细诊断日志已写入以下位置："
    echo "   1. $USER_LOG"
    echo "   2. $TMP_LOG"
    echo "   3. $LOG_DIR/lanchat_python.log"
    echo "=========================================================================="
    if [ -t 0 ]; then
        read -p "按 [回车键] 关闭此窗口..." _dummy
    fi
fi

exit $EXIT_CODE
""".encode('utf-8')

    # 1. Build lanchat_2.0.0_amd64.deb
    x64_bin_path = os.path.join(RELEASE_DIR, "linux-x64", "lanchat")
    files_amd64 = [
        ("./usr/bin/lanchat", launcher_script, 0o755),
        ("./usr/lib/lanchat/lanchat.py", py_bytes, 0o755),
        ("./usr/share/applications/lanchat.desktop", desktop_entry, 0o644),
        ("./usr/share/icons/hicolor/scalable/apps/lanchat.svg", svg_icon, 0o644),
    ]
    if png_bytes:
        files_amd64.append(("./usr/share/icons/hicolor/256x256/apps/lanchat.png", png_bytes, 0o644))

    if os.path.exists(x64_bin_path):
        with open(x64_bin_path, "rb") as f:
            x64_bytes = f.read()
        files_amd64.append(("./usr/lib/lanchat/lanchat-bin", x64_bytes, 0o755))
        installed_size = (len(x64_bytes) + len(py_bytes)) // 1024
    else:
        installed_size = len(py_bytes) // 1024

    ctrl_amd64 = {
        "Package": "lanchat",
        "Version": VERSION,
        "Section": "net",
        "Priority": "optional",
        "Architecture": "amd64",
        "Maintainer": "LanChat Team <lanchat@local>",
        "Depends": "python3",
        "Recommends": "python3-tk",
        "Installed-Size": str(installed_size),
        "Description": "LanChat - Lightweight LAN Chat and High-Speed File Transfer\n LanChat is a zero-dependency instant messenger and streaming file transfer\n tool for local area networks with WeChat dark GUI.",
    }

    deb_amd64_path = os.path.join(RELEASE_DIR, f"lanchat_{VERSION}_amd64.deb")
    create_deb(deb_amd64_path, ctrl_amd64, files_amd64)

    # Copy also as LanChat.deb for convenience
    shutil.copy2(deb_amd64_path, os.path.join(RELEASE_DIR, "LanChat.deb"))

    # 2. Build lanchat_2.0.0_all.deb (Universal Python-based deb, zero dependencies, works on ARM64, Raspberry Pi, Loongson, FT, etc.)
    launcher_all = """#!/bin/bash
# LanChat Universal Python Launcher with Diagnostics & Anti-Crash Guard
DIR="/usr/lib/lanchat"
LOG_DIR="$HOME/.local/share/lanchat"
mkdir -p "$LOG_DIR" 2>/dev/null
USER_LOG="$LOG_DIR/launcher.log"
TMP_LOG="/tmp/lanchat_${USER:-default}_launch.log"

log() {
    local MSG="[$(date '+%Y-%m-%d %H:%M:%S')] $1"
    echo "$MSG" >> "$USER_LOG" 2>/dev/null
    echo "$MSG" >> "$TMP_LOG" 2>/dev/null
}

log "========================================================"
log "LanChat Universal 启动器已调用 (PID: $$, USER: ${USER:-unknown})"
log "OS: $(cat /etc/os-release 2>/dev/null | grep PRETTY_NAME | cut -d= -f2 | tr -d '\"' || uname -s)"
log "DISPLAY: $DISPLAY | WAYLAND_DISPLAY: $WAYLAND_DISPLAY"
log "ARGS: $*"
log "========================================================"

HAS_GUI_ENV=0
if [ -n "$DISPLAY" ] || [ -n "$WAYLAND_DISPLAY" ]; then
    HAS_GUI_ENV=1
fi

FORCE_CLI=0
for arg in "$@"; do
    if [ "$arg" = "--cli" ] || [ "$arg" = "-c" ]; then
        FORCE_CLI=1
        break
    fi
done

# 图形桌面环境首选直接启动 WeChat GUI
if [ $HAS_GUI_ENV -eq 1 ] && [ $FORCE_CLI -eq 0 ]; then
    log "[INFO] 检测到图形桌面环境，优先以微信暗黑风格 GUI 启动..."
    if command -v python3 >/dev/null 2>&1; then
        if python3 -c "import tkinter" >/dev/null 2>&1; then
            log "[INFO] 启动 GUI: $DIR/lanchat.py"
            python3 "$DIR/lanchat.py" "$@"
            EXIT_CODE=$?
            log "[INFO] GUI 退出码: $EXIT_CODE"
            exit $EXIT_CODE
        else
            log "[WARN] 缺少 python3-tk，图形界面无法初始化"
            MSG="LanChat 启动微信暗黑图形界面需要 python3-tk 组件。\n请在终端运行以下命令安装：\n\nsudo apt install python3-tk"
            if command -v zenity >/dev/null 2>&1; then
                zenity --warning --title="LanChat 提示" --text="$MSG" --width=350 2>/dev/null &
            elif command -v notify-send >/dev/null 2>&1; then
                notify-send "LanChat 提示" "缺少 python3-tk 组件，请执行 sudo apt install python3-tk" 2>/dev/null
            fi
        fi
    fi
fi

# 终端自适应
if [ ! -t 0 ] || [ ! -t 1 ]; then
    log "[INFO] 终端模式：检测到无交互 TTY，尝试唤起系统终端..."
    for TERM_CMD in x-terminal-emulator gnome-terminal mate-terminal ukui-terminal deepin-terminal konsole xfce4-terminal terminator lxterminal xterm; do
        if command -v "$TERM_CMD" >/dev/null 2>&1; then
            case "$TERM_CMD" in
                gnome-terminal) exec gnome-terminal -- /usr/bin/lanchat --cli "$@" ;;
                mate-terminal|ukui-terminal|xfce4-terminal|lxterminal) exec "$TERM_CMD" -e "/usr/bin/lanchat --cli $*" ;;
                konsole) exec konsole -e /usr/bin/lanchat --cli "$@" ;;
                *) exec "$TERM_CMD" -e "/usr/bin/lanchat --cli $*" ;;
            esac
        fi
    done
fi

EXIT_CODE=0
if command -v python3 >/dev/null 2>&1; then
    python3 "$DIR/lanchat.py" "$@"
    EXIT_CODE=$?
elif command -v python >/dev/null 2>&1; then
    python "$DIR/lanchat.py" "$@"
    EXIT_CODE=$?
else
    echo "[Error] 未检测到 Python 3 解释器" >&2
    EXIT_CODE=1
fi

if [ $EXIT_CODE -ne 0 ] && [ $EXIT_CODE -ne 130 ]; then
    echo ""
    echo "=========================================================================="
    echo " [LanChat 运行异常 / Error] (退出码: $EXIT_CODE)"
    echo " 详细日志: $USER_LOG 及 $LOG_DIR/lanchat_python.log"
    echo "=========================================================================="
    if [ -t 0 ]; then
        read -p "按 [回车键] 关闭此窗口..." _dummy
    fi
fi

exit $EXIT_CODE
""".encode('utf-8')
    files_all = [
        ("./usr/bin/lanchat", launcher_all, 0o755),
        ("./usr/lib/lanchat/lanchat.py", py_bytes, 0o755),
        ("./usr/share/applications/lanchat.desktop", desktop_entry, 0o644),
        ("./usr/share/icons/hicolor/scalable/apps/lanchat.svg", svg_icon, 0o644),
    ]
    if png_bytes:
        files_all.append(("./usr/share/icons/hicolor/256x256/apps/lanchat.png", png_bytes, 0o644))

    ctrl_all = {
        "Package": "lanchat",
        "Version": VERSION,
        "Section": "net",
        "Priority": "optional",
        "Architecture": "all",
        "Maintainer": "LanChat Team <lanchat@local>",
        "Depends": "python3",
        "Recommends": "python3-tk",
        "Installed-Size": str(len(py_bytes) // 1024),
        "Description": "LanChat - Universal LAN Chat and File Transfer (Python Edition)\n Pure standard library, zero external dependencies. Works on all CPU architectures with WeChat dark GUI.",
    }

    deb_all_path = os.path.join(RELEASE_DIR, f"lanchat_{VERSION}_all.deb")
    create_deb(deb_all_path, ctrl_all, files_all)

    print("=" * 60)
    print("Debian packaging complete!")
    print(f"  - release/lanchat_{VERSION}_amd64.deb (x86_64 Self-Contained + Fallback)")
    print(f"  - release/LanChat.deb               (Alias)")
    print(f"  - release/lanchat_{VERSION}_all.deb   (Universal Architecture)")
    print("=" * 60)

if __name__ == "__main__":
    build_packages()
