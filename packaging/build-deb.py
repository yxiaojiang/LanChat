#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""打包 LanChat 的 amd64、all Debian 包和便携 Python zipapp。"""

import os
import io
import tarfile
import time
import shutil
import argparse
from pathlib import Path
import tempfile
import zipapp

from build_linux import RID_MACHINES, check_elf

VERSION = "2.0.24"
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

def build_zipapp(zip_path):
    """生成便携 Python 归档，成功后原子替换目标文件。"""
    zip_path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".lanchat-zipapp-", dir=zip_path.parent) as tmp:
        source = Path(tmp) / "source"
        package = source / "lanchat_app"
        shutil.copytree(Path(ROOT_DIR) / "src/linux/lanchat_app", package,
                        ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
        shutil.copyfile(Path(ROOT_DIR) / "assets/app.png", package / "app.png")
        (source / "__main__.py").write_text(
            "from lanchat_app.app import main\nraise SystemExit(main())\n", encoding="utf-8"
        )
        prepared = Path(tmp) / zip_path.name
        zipapp.create_archive(source, prepared, interpreter="/usr/bin/env python3", compressed=True)
        prepared.replace(zip_path)


def build_packages(elf_dir, output_dir):
    """使用显式传入的本轮 ELF 目录生成 amd64 与 all 包，缺失或架构错误时失败。"""
    # 在写包之前校验两个架构，避免缺失时留下同名候选包。
    for rid in RID_MACHINES:
        path = elf_dir / rid / "lanchat"
        data = path.read_bytes()
        check_elf(data, rid)
        if rid == "linux-x64":
            x64_bytes = data
    output_dir.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".lanchat-deb-", dir=output_dir) as tmp:
        stage = Path(tmp)
        zip_path = stage / "lanchat.pyz"
        build_zipapp(zip_path)
        _assemble_debs(stage, zip_path.read_bytes(), x64_bytes)
        generated = sorted(stage.iterdir())
        for artifact in generated:
            artifact.replace(output_dir / artifact.name)
    for artifact in generated:
        print(f"[SUCCESS] {output_dir / artifact.name}")


def _assemble_debs(output_dir, py_bytes, x64_bytes):
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

    launcher_script = r"""#!/bin/sh
# 桌面、有终端及无头模式由 Python 入口统一判定。
DIR=/usr/lib/lanchat
LOG_DIR="$HOME/.local/share/lanchat"
if mkdir -p "$LOG_DIR" 2>/dev/null; then
    printf '%s LanChat 启动 DISPLAY=%s WAYLAND_DISPLAY=%s\n' \
        "$(date '+%Y-%m-%d %H:%M:%S')" "$DISPLAY" "$WAYLAND_DISPLAY" >>"$LOG_DIR/launcher.log"
fi
if command -v python3 >/dev/null 2>&1; then
    exec python3 "$DIR/lanchat.pyz" "$@"
fi
if [ -x "$DIR/lanchat-bin" ]; then
    exec "$DIR/lanchat-bin" "$@"
fi
printf '%s\n' '无法启动 LanChat：未找到 Python 3。' >&2
exit 1
""".encode("utf-8")

    files_amd64 = [
        ("./usr/bin/lanchat", launcher_script, 0o755),
        ("./usr/lib/lanchat/lanchat.pyz", py_bytes, 0o755),
        ("./usr/share/applications/lanchat.desktop", desktop_entry, 0o644),
        ("./usr/share/icons/hicolor/scalable/apps/lanchat.svg", svg_icon, 0o644),
    ]
    if png_bytes:
        files_amd64.append(("./usr/share/icons/hicolor/256x256/apps/lanchat.png", png_bytes, 0o644))

    files_amd64.append(("./usr/lib/lanchat/lanchat-bin", x64_bytes, 0o755))
    installed_size = (len(x64_bytes) + len(py_bytes)) // 1024

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

    deb_amd64_path = os.path.join(output_dir, f"lanchat_{VERSION}_amd64.deb")
    create_deb(deb_amd64_path, ctrl_amd64, files_amd64)

    # Copy also as LanChat.deb for convenience
    shutil.copy2(deb_amd64_path, os.path.join(output_dir, "LanChat.deb"))

    # all 包和 amd64 共用桌面及命令行入口。
    files_all = [
        ("./usr/bin/lanchat", launcher_script, 0o755),
        ("./usr/lib/lanchat/lanchat.pyz", py_bytes, 0o755),
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

    deb_all_path = os.path.join(output_dir, f"lanchat_{VERSION}_all.deb")
    create_deb(deb_all_path, ctrl_all, files_all)

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--elf-dir", type=Path, help="本轮双架构 ELF 输出目录")
    mode.add_argument("--zipapp-only", action="store_true", help="仅生成便携 Python 归档")
    parser.add_argument("--output-dir", type=Path, default=Path(RELEASE_DIR))
    args = parser.parse_args()
    try:
        if args.zipapp_only:
            archive = args.output_dir / "lanchat.pyz"
            build_zipapp(archive)
            print(f"[SUCCESS] {archive}")
        else:
            build_packages(args.elf_dir, args.output_dir)
    except (OSError, ValueError) as exc:
        parser.exit(1, f"打包失败：{exc}\n")
