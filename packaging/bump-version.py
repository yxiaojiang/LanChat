#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
LanChat 版本管理与全工程原子同步工具
自动递增或设置版本号，并同步更新以下文件：
  1. src/MainForm.cs
  2. src/Services/UpdateService.cs
  3. packaging/Package.wxs
  4. src/linux/lanchat.py
  5. packaging/build-deb.py
"""

import os
import sys
import re

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT_DIR = os.path.dirname(SCRIPT_DIR)

MAIN_CS = os.path.join(ROOT_DIR, "src", "MainForm.cs")
UPDATE_CS = os.path.join(ROOT_DIR, "src", "Services", "UpdateService.cs")
PACKAGE_WXS = os.path.join(ROOT_DIR, "packaging", "Package.wxs")
LINUX_PY = os.path.join(ROOT_DIR, "src", "linux", "lanchat.py")
BUILD_DEB_PY = os.path.join(ROOT_DIR, "packaging", "build-deb.py")

def get_current_version() -> str:
    if os.path.exists(MAIN_CS):
        with open(MAIN_CS, "r", encoding="utf-8") as f:
            for line in f:
                if "APP_VERSION" in line and "=" in line:
                    m = re.search(r'=\s*"([^"]+)"', line)
                    if m:
                        return m.group(1).lstrip("vV")
    return "2.0.0"

def bump_patch_version(ver: str) -> str:
    parts = ver.split(".")
    if len(parts) >= 3:
        try:
            parts[-1] = str(int(parts[-1]) + 1)
            return ".".join(parts)
        except ValueError:
            pass
    elif len(parts) == 2:
        return f"{parts[0]}.{parts[1]}.1"
    return f"{ver}.1"

def update_file_regex(file_path: str, pattern: str, replacement: str):
    if not os.path.exists(file_path):
        return
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()
    new_content, count = re.subn(pattern, replacement, content)
    if count > 0 and new_content != content:
        with open(file_path, "w", encoding="utf-8") as f:
            f.write(new_content)

def sync_all(new_version: str):
    new_version = new_version.lstrip("vV").strip()

    # 1. src/MainForm.cs
    update_file_regex(MAIN_CS,
                      r'(public\s+const\s+string\s+APP_VERSION\s*=\s*")[^"]+(";)',
                      rf'\g<1>{new_version}\g<2>')

    # 2. src/Services/UpdateService.cs
    update_file_regex(UPDATE_CS,
                      r'(public\s+const\s+string\s+APP_VERSION\s*=\s*")[^"]+(";)',
                      rf'\g<1>{new_version}\g<2>')

    # 3. packaging/Package.wxs
    update_file_regex(PACKAGE_WXS,
                      r'(Version=")[^"]+(")',
                      rf'\g<1>{new_version}\g<2>')

    # 4. src/linux/lanchat.py
    update_file_regex(LINUX_PY,
                      r'(APP_VERSION\s*=\s*")[^"]+(")',
                      rf'\g<1>{new_version}\g<2>')

    # 5. packaging/build-deb.py
    update_file_regex(BUILD_DEB_PY,
                      r'(VERSION\s*=\s*")[^"]+(")',
                      rf'\g<1>{new_version}\g<2>')

def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(errors="replace")

    current_ver = get_current_version()

    if len(sys.argv) > 1:
        arg = sys.argv[1].strip()
        if arg in ["--show", "-s"]:
            print(current_ver)
            return
        elif arg.lower() in ["--bump", "-b", "patch", "auto"]:
            target_ver = bump_patch_version(current_ver)
        else:
            target_ver = arg.lstrip("vV")
    else:
        target_ver = bump_patch_version(current_ver)

    sync_all(target_ver)
    print(target_ver)

if __name__ == "__main__":
    main()
