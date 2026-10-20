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

VERSION = "1.0.0"
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
RELEASE_DIR = os.path.join(SCRIPT_DIR, "release")
ROOT_DIR = SCRIPT_DIR

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
    py_path = os.path.join(ROOT_DIR, "lanchat.py")
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
Terminal=true
Categories=Network;InstantMessaging;
Keywords=chat;lan;file;transfer;
"""

    svg_icon = b"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">
  <rect width="64" height="64" rx="14" fill="#07c160"/>
  <path d="M18 20 C18 15.58 24.27 12 32 12 C39.73 12 46 15.58 46 20 C46 24.42 39.73 28 32 28 C29.98 28 28.07 27.76 26.39 27.32 L20 30 L21.5 25.5 C19.34 23.95 18 22.08 18 20 Z" fill="#ffffff"/>
  <path d="M30 28 C30 25.24 34.48 23 40 23 C45.52 23 50 25.24 50 28 C50 30.76 45.52 33 40 33 C38.56 33 37.2 32.83 36 32.52 L31.43 34.43 L32.5 31.25 C30.96 30.15 30 29.13 30 28 Z" fill="#e0f8ea"/>
</svg>
"""

    launcher_script = b"""#!/bin/bash
# LanChat Linux Launcher - Zero external dependencies
# Automatically selects self-contained native binary or falls back to system Python 3

DIR="/usr/lib/lanchat"

if [ -x "$DIR/lanchat-bin" ]; then
    "$DIR/lanchat-bin" "$@"
    RET=$?
    if [ $RET -eq 0 ] || [ $RET -eq 130 ]; then
        exit $RET
    fi
fi

if command -v python3 >/dev/null 2>&1; then
    exec python3 "$DIR/lanchat.py" "$@"
elif command -v python >/dev/null 2>&1; then
    exec python "$DIR/lanchat.py" "$@"
fi

echo "[Error] Unable to launch LanChat. No compatible execution environment found." >&2
exit 1
"""

    # 1. Build lanchat_1.0.0_amd64.deb
    x64_bin_path = os.path.join(RELEASE_DIR, "linux-x64", "lanchat")
    files_amd64 = [
        ("./usr/bin/lanchat", launcher_script, 0o755),
        ("./usr/lib/lanchat/lanchat.py", py_bytes, 0o755),
        ("./usr/share/applications/lanchat.desktop", desktop_entry, 0o644),
        ("./usr/share/icons/hicolor/scalable/apps/lanchat.svg", svg_icon, 0o644),
    ]

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
        "Installed-Size": str(installed_size),
        "Description": "LanChat - Lightweight LAN Chat and High-Speed File Transfer\n LanChat is a zero-dependency instant messenger and streaming file transfer\n tool for local area networks. Fully self-contained, no external .NET required.",
    }

    deb_amd64_path = os.path.join(RELEASE_DIR, f"lanchat_{VERSION}_amd64.deb")
    create_deb(deb_amd64_path, ctrl_amd64, files_amd64)

    # Copy also as LanChat.deb for convenience
    shutil.copy2(deb_amd64_path, os.path.join(RELEASE_DIR, "LanChat.deb"))

    # 2. Build lanchat_1.0.0_all.deb (Universal Python-based deb, zero dependencies, works on ARM64, Raspberry Pi, Loongson, FT, etc.)
    launcher_all = b"""#!/bin/bash
exec python3 /usr/lib/lanchat/lanchat.py "$@"
"""
    files_all = [
        ("./usr/bin/lanchat", launcher_all, 0o755),
        ("./usr/lib/lanchat/lanchat.py", py_bytes, 0o755),
        ("./usr/share/applications/lanchat.desktop", desktop_entry, 0o644),
        ("./usr/share/icons/hicolor/scalable/apps/lanchat.svg", svg_icon, 0o644),
    ]

    ctrl_all = {
        "Package": "lanchat",
        "Version": VERSION,
        "Section": "net",
        "Priority": "optional",
        "Architecture": "all",
        "Maintainer": "LanChat Team <lanchat@local>",
        "Installed-Size": str(len(py_bytes) // 1024),
        "Description": "LanChat - Universal LAN Chat and File Transfer (Python Edition)\n Pure standard library, zero external dependencies. Works on all CPU architectures.",
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
