#!/usr/bin/env python3
"""使用已锁定的 Electron 和本轮 ELF 构建 Linux 试验包。"""

import argparse
from hashlib import sha256
import io
import json
from contextlib import contextmanager
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
import shutil
import tarfile
import tempfile
import zipfile

from build_linux import ROOT, check_elf


ELECTRON = "45.4.1"
VERSION = "2.0.19"
ARCHITECTURES = {
    "amd64": ("x64", "e1d077c7a087d7e344350ea0c5cd2b7dc5af3d9bf7979474f38e0fc1a560b875"),
    "arm64": ("arm64", "3da04c450fa682e3b80961e92a918a1c2a1a10796527a7e63dec7b8e466b6334"),
}
APP = ROOT / "src/linux/desktop"
DESKTOP_ENTRY = b"""[Desktop Entry]
Type=Application
Name=LanChat
Comment=LAN messaging and file transfer
Exec=lanchat
Icon=lanchat
Terminal=false
Categories=Network;InstantMessaging;
"""
DEPENDENCIES = (
    # 两种 ELF 的 GLIBC 符号版本均低于 2.31。自包含 .NET 动态加载 OpenSSL 3 或 1.1.1。
    "libc6 (>= 2.31), libgcc-s1, libstdc++6, zlib1g, libssl3t64 | libssl3 | libssl1.1 (>= 1.1.1), "
    "libasound2t64 | libasound2, libatk1.0-0t64 | libatk1.0-0, libatk-bridge2.0-0t64 | libatk-bridge2.0-0, "
    "libatspi2.0-0t64 | libatspi2.0-0, libglib2.0-0t64 | libglib2.0-0, "
    "libgtk-3-0t64 | libgtk-3-0, libdbus-1-3, libcairo2, libcups2t64 | libcups2, libdrm2, libexpat1, "
    "libgbm1, libnspr4, libnss3, libpango-1.0-0, libudev1, libx11-6, libx11-xcb1, "
    "libxcb1, libxcomposite1, libxdamage1, libxext6, libxfixes3, libxkbcommon0, libxrandr2"
)


@dataclass(frozen=True)
class ZipArchiveEntry:
    """指向已校验 Electron ZIP 内文件的惰性只读引用。"""

    archive: Path
    member: str
    size: int

    @contextmanager
    def open(self):
        """以流方式读取归档成员，不在磁盘创建临时可执行文件。"""
        with zipfile.ZipFile(self.archive) as zipped:
            with zipped.open(self.member) as source:
                yield source


def file_hash(path):
    source_context = path.open() if isinstance(path, ZipArchiveEntry) else path.open("rb")
    with source_context as source:
        digest = sha256()
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
        return digest.hexdigest()


def tar_entry(archive, name, contents, mode):
    header = tarfile.TarInfo(name)
    header.mode = mode
    header.uname = header.gname = "root"
    if isinstance(contents, ZipArchiveEntry):
        header.size = contents.size
        with contents.open() as source:
            archive.addfile(header, source)
    elif isinstance(contents, Path):
        header.size = contents.stat().st_size
        with contents.open("rb") as source:
            archive.addfile(header, source)
    elif contents is None:
        header.type = tarfile.DIRTYPE
        archive.addfile(header)
    else:
        header.size = len(contents)
        archive.addfile(header, io.BytesIO(contents))


def add_tree(archive, prefix, files):
    directories = set()
    written_files = set()
    for relative, contents, mode in files:
        name = PurePosixPath(prefix) / relative
        # 同一路径写成文件和目录会导致安装时解包失败，在生成归档时就报错。
        if name in directories or name in written_files:
            raise ValueError(f"归档路径冲突：{name}")
        for parent in reversed(name.parents):
            if parent in written_files:
                raise ValueError(f"归档路径冲突：{parent}")
            if parent not in directories and parent != PurePosixPath("."):
                tar_entry(archive, parent.as_posix(), None, 0o755)
                directories.add(parent)
        tar_entry(archive, name.as_posix(), contents, mode)
        written_files.add(name)


def debian_member(output, name, contents, length):
    # ar 成员按偶数对齐，数据从临时文件流入，避免复制整份 Electron 到内存。
    header = f"{name:<16}{0:<12}{0:<6}{0:<6}{'100644':<8}{length:<10}`\n"
    output.write(header.encode("ascii"))
    shutil.copyfileobj(contents, output)
    if length % 2:
        output.write(b"\n")


def build_deb(stage, arch, files):
    installed = [(f"usr/lib/lanchat/{name}", content, 0o4755 if name == "electron/chrome-sandbox" else mode)
                 for name, content, mode in files if name != "lanchat"]
    installed.extend([
        ("usr/bin/lanchat", next(content for name, content, _ in files if name == "lanchat"), 0o755),
        ("usr/share/applications/lanchat.desktop", DESKTOP_ENTRY, 0o644),
        ("usr/share/icons/hicolor/256x256/apps/lanchat.png", ROOT / "assets/app.png", 0o644),
    ])
    size = sum(
        item.stat().st_size if isinstance(item, Path)
        else item.size if isinstance(item, ZipArchiveEntry)
        else len(item)
        for _, item, _ in installed
    )
    control = (
        f"Package: lanchat\nVersion: {VERSION}\nArchitecture: {arch}\n"
        "Maintainer: LanChat Team <lanchat@local>\nSection: net\nPriority: optional\n"
        f"Installed-Size: {(size + 1023) // 1024}\nDepends: {DEPENDENCIES}\n"
        "Description: LanChat Linux desktop client\n"
        " Local network chat and file transfer with an Electron desktop and self-contained CLI.\n"
    ).encode()
    control_data = io.BytesIO()
    with tarfile.open(fileobj=control_data, mode="w:gz") as archive:
        tar_entry(archive, "./control", control, 0o644)
    control_data.seek(0)
    data_file = stage / "data.tar.gz"
    with tarfile.open(data_file, "w:gz", compresslevel=3) as archive:
        add_tree(archive, ".", installed)
    deb = stage / f"lanchat_{VERSION}_{arch}.deb"
    with deb.open("wb") as output, data_file.open("rb") as data:
        output.write(b"!<arch>\n")
        debian_member(output, "debian-binary", io.BytesIO(b"2.0\n"), 4)
        debian_member(output, "control.tar.gz", control_data, len(control_data.getbuffer()))
        debian_member(output, "data.tar.gz", data, data_file.stat().st_size)
    return deb


def build(output, archives, elf, icons):
    for image in ("Search", "Refresh", "Logout", "Add", "UpArrow", "Folder", "BoxWhite",
                  "Emoji", "Clear", "ClearDanger", "Copy", "User", "Settings", "Menu", "Contacts",
                  "SettingsActive", "UserActive", "EmojiActive", "MenuActive", "FolderActive",
                  "ClearActive", "ContactsActive", "RefreshActive",
                  "avatar", "MascotProfile", "MascotWhite", "group", "peer"):
        if not (icons / f"{image}.png").is_file():
            raise ValueError(f"缺少原生导出的图标：{image}.png")
    output.mkdir(parents=True, exist_ok=True)
    # 中间归档放入忽略目录，避开发布产物目录的实时扫描；系统短暂占用时不让清理失败覆盖打包结果。
    stage_root = ROOT / "bin/linux-deb-build/staging"
    stage_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix=".lanchat-desktop-", dir=stage_root, ignore_cleanup_errors=True
    ) as temporary:
        stage = Path(temporary)
        artifacts = []
        manifest = {}
        launcher = (ROOT / "packaging/lanchat-launcher.sh").read_bytes().replace(b"\r\n", b"\n")
        launcher_path = stage / "lanchat-launcher.sh"
        launcher_path.write_bytes(launcher)
        for arch, (cpu, digest) in ARCHITECTURES.items():
            runtime = archives / f"electron-v{ELECTRON}-linux-{cpu}.zip"
            core = elf / f"linux-{cpu}" / "lanchat"
            if file_hash(runtime) != digest:
                raise ValueError(f"Electron 摘要不匹配：{runtime.name}")
            check_elf(core.read_bytes(), f"linux-{cpu}")
            print(f"打包 {arch}，Electron {ELECTRON}", flush=True)
            with zipfile.ZipFile(runtime) as zipped:
                members = {member.filename: member for member in zipped.infolist()}
                electron_member = members.get("electron")
                if electron_member is None:
                    raise ValueError(f"Electron 归档缺少可执行文件：{runtime.name}")
                with zipped.open(electron_member) as source:
                    check_elf(source.read(64), f"linux-{cpu}")
                electron = ZipArchiveEntry(runtime, electron_member.filename, electron_member.file_size)
            files = [("lanchat", launcher_path, 0o755), ("electron/resources/lanchat-bin", core, 0o755)]
            for member in sorted(members.values(), key=lambda item: item.filename):
                relative = PurePosixPath(member.filename)
                # 默认应用归档不会进入 LanChat 载荷，跳过可避免无用读写。
                if member.is_dir() or relative.name == "default_app.asar":
                    continue
                archive_file = ZipArchiveEntry(runtime, member.filename, member.file_size)
                mode = 0o755 if relative.as_posix() in ("electron", "chrome-sandbox", "chrome_crashpad_handler") else 0o644
                files.append((f"electron/{relative.as_posix()}", archive_file, mode))
            for path in sorted(APP.iterdir()):
                if path.is_file():
                    files.append((f"electron/resources/app/{path.name}", path, 0o644))
            for path in sorted(icons.glob("*.png")):
                files.append((f"electron/resources/app/assets/{path.name}", path, 0o644))
            files.append(("electron/resources/app/assets/app.png", ROOT / "assets/app.png", 0o644))
            deb = build_deb(stage, arch, files)
            portable = stage / f"lanchat_{VERSION}_{arch}_portable.tar.gz"
            with tarfile.open(portable, "w:gz", compresslevel=3) as archive:
                add_tree(archive, "LanChat", files)
            standalone = stage / f"lanchat-cli_{VERSION}_{arch}"
            shutil.copyfile(core, standalone)
            standalone.chmod(0o755)
            artifacts.extend([deb, portable, standalone])
            manifest[arch] = {
                "electron_zip_sha256": digest,
                "electron_sha256": file_hash(electron),
                "artifacts": {path.name: file_hash(path) for path in (deb, portable, standalone)},
            }
        artifact_manifest = stage / "SHA256SUMS.json"
        artifact_manifest.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        for artifact in (*artifacts, artifact_manifest):
            artifact.replace(output / artifact.name)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for option in ("output", "archives", "elf", "icons"):
        parser.add_argument(f"--{option}", type=Path, required=True)
    args = parser.parse_args()
    try:
        build(args.output, args.archives, args.elf, args.icons)
    except (OSError, ValueError, zipfile.BadZipFile) as exc:
        parser.exit(1, f"Electron 打包失败：{exc}\n")
