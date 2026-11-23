#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""跨平台构建 Linux 双架构 ELF、Electron 桌面包及 Debian 安装包。"""

import importlib.util
import os
from pathlib import Path
import shutil
import subprocess
import sys
import urllib.request


SCRIPTS_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPTS_DIR.parents[1]
PACKAGING_DIR = PROJECT_ROOT / "packaging"
BUILD_ROOT = PROJECT_ROOT / "bin/linux-deb-build"
ELF_OUTPUT = BUILD_ROOT / "elf"
ELECTRON_CACHE = BUILD_ROOT / "electron"
ICON_DIR = PROJECT_ROOT / "assets/LinuxDesktop"
PACKAGE_OUTPUT = PROJECT_ROOT / "release/linux-electron"


def load_electron_builder():
    """载入现有 Electron 打包入口，以共享其锁定版本和摘要。"""
    sys.path.insert(0, str(PACKAGING_DIR))
    path = PACKAGING_DIR / "build-electron.py"
    spec = importlib.util.spec_from_file_location("lanchat_build_electron", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"无法载入打包器：{path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def prepare_dotnet():
    """选择已安装的 .NET 10 SDK，并将其目录置于后续构建命令的 PATH 首位。"""
    candidates = []
    system_dotnet = shutil.which("dotnet")
    if system_dotnet:
        candidates.append(Path(system_dotnet))
    cached_host = PROJECT_ROOT / "bin/lc-linux-002/cache/dotnet"
    cached_name = "dotnet.exe" if os.name == "nt" else "dotnet"
    candidates.append(cached_host / cached_name)

    seen = set()
    for candidate in candidates:
        key = str(candidate).lower()
        if key in seen or not candidate.is_file():
            continue
        seen.add(key)
        try:
            result = subprocess.run(
                [str(candidate), "--list-sdks"],
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
            )
        except OSError:
            continue
        sdk_list = result.stdout if result.returncode == 0 else ""
        if any(line.split() and line.split()[0].startswith("10.") for line in sdk_list.splitlines()):
            os.environ["PATH"] = str(candidate.parent) + os.pathsep + os.environ.get("PATH", "")
            os.environ["DOTNET_ROOT"] = str(candidate.parent)
            print(f"使用 .NET SDK 10：{candidate}", flush=True)
            return

    raise RuntimeError("未找到 .NET 10 SDK。请安装 .NET 10 SDK，或提供 bin/lc-linux-002/cache/dotnet 缓存。")


def ensure_electron_archives(builder):
    """下载并按打包器锁定的 SHA-256 校验 Electron 官方归档。"""
    ELECTRON_CACHE.mkdir(parents=True, exist_ok=True)
    for _, (cpu, expected_hash) in builder.ARCHITECTURES.items():
        name = f"electron-v{builder.ELECTRON}-linux-{cpu}.zip"
        archive = ELECTRON_CACHE / name
        if archive.is_file() and builder.file_hash(archive) == expected_hash:
            print(f"已复用 Electron 缓存：{name}", flush=True)
            continue

        existing_cache = PROJECT_ROOT / "bin/lc-linux-002/cache" / name
        if existing_cache.is_file() and builder.file_hash(existing_cache) == expected_hash:
            shutil.copy2(existing_cache, archive)
            print(f"已复用项目缓存：{name}", flush=True)
            continue

        url = f"https://github.com/electron/electron/releases/download/v{builder.ELECTRON}/{name}"
        partial = archive.with_suffix(archive.suffix + ".partial")
        print(f"下载 Electron 官方归档：{name}", flush=True)
        request = urllib.request.Request(url, headers={"User-Agent": "LanChat-BuildPanel"})
        try:
            with urllib.request.urlopen(request, timeout=60) as response, partial.open("wb") as target:
                while True:
                    block = response.read(1024 * 1024)
                    if not block:
                        break
                    target.write(block)
            actual_hash = builder.file_hash(partial)
            if actual_hash != expected_hash:
                raise ValueError(f"Electron SHA-256 不匹配：{name}")
            os.replace(partial, archive)
        except Exception:
            partial.unlink(missing_ok=True)
            raise


def build():
    """构建两架构 ELF，并生成 deb、便携包、CLI 与摘要文件。"""
    prepare_dotnet()
    builder = load_electron_builder()
    ELF_OUTPUT.mkdir(parents=True, exist_ok=True)
    PACKAGE_OUTPUT.mkdir(parents=True, exist_ok=True)

    print("[1/3] 构建 Linux x86_64 与 ARM64 自包含 ELF...", flush=True)
    from build_linux import build_linux

    build_linux(ELF_OUTPUT)

    print("[2/3] 准备 Electron 归档并校验 SHA-256...", flush=True)
    ensure_electron_archives(builder)

    print("[3/3] 打包 Debian 安装包与配套产物...", flush=True)
    builder.build(PACKAGE_OUTPUT, ELECTRON_CACHE, ELF_OUTPUT, ICON_DIR)

    print(f"Linux 双架构构建完成：{PACKAGE_OUTPUT}", flush=True)


if __name__ == "__main__":
    try:
        build()
    except (OSError, RuntimeError, ValueError, subprocess.CalledProcessError) as exc:
        print(f"Linux 构建失败：{exc}", file=sys.stderr, flush=True)
        raise SystemExit(1)
