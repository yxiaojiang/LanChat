#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
LanChat GitHub Release 自动化发布工具
- 自动从 MainForm.cs 探测最新版本号 (或由命令行参数指定)
- 自动检测并创建/更新 GitHub Release (Tag & Notes)
- 智能管理资产：若已存在同名资产自动替换覆盖，避免 HTTP 422 冲突
- 支持流式上传 LanChat.msi、LanChat.deb 等二进制包
"""

import os
import sys
import re
import subprocess
import urllib.request
import urllib.error
import json

REPO = "yxiaojiang/LanChat"
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
RELEASE_DIR = os.path.join(SCRIPT_DIR, "release")

def detect_version():
    if len(sys.argv) > 1 and sys.argv[1].strip() and not sys.argv[1].startswith("-"):
        return sys.argv[1].strip().lstrip("vV")

    main_cs = os.path.join(SCRIPT_DIR, "MainForm.cs")
    if os.path.exists(main_cs):
        try:
            with open(main_cs, "r", encoding="utf-8") as f:
                for line in f:
                    if "APP_VERSION" in line and "=" in line:
                        m = re.search(r'=\s*"([^"]+)"', line)
                        if m:
                            return m.group(1).lstrip("vV")
        except Exception:
            pass
    return "2.0.0"

def get_git_token():
    # 1. 优先读取环境变量
    token = os.environ.get("GITHUB_TOKEN")
    if token:
        return token

    # 2. 从 git credential helper 读取已缓存凭据
    try:
        proc = subprocess.Popen(["git", "credential", "fill"],
                                stdin=subprocess.PIPE,
                                stdout=subprocess.PIPE,
                                stderr=subprocess.PIPE,
                                text=True)
        out, _ = proc.communicate(input="protocol=https\nhost=github.com\n\n", timeout=5)
        for line in out.splitlines():
            if line.startswith("password="):
                return line.split("=", 1)[1].strip()
    except Exception:
        pass
    return None

def publish():
    version = detect_version()
    tag_name = f"v{version}"
    release_title = f"LanChat v{version} 正式版"
    release_notes = f"""### 🚀 LanChat v{version} 发布说明

- **精简原生极速**：Windows 端零运行库依赖，免安装秒开；
- **全套微信暗黑桌面 UI**：双端（Windows WinForms / Linux Tkinter）微距级对标 WeChat Desktop 暗黑主题；
- **后台托盘与单实例互斥**：点击关闭常驻托盘，重复启动自动呼出前台激活；
- **双轨在线更新机制**：内置 GitHub Releases 在线检测与一键高速更新向导；
- **局域网流式文件传输**：支持多网卡自动绑定与流式高带宽传输。
"""

    print("=" * 65)
    print(f"      LanChat GitHub Releases 自动发布工具 (v{version})")
    print("=" * 65)

    token = get_git_token()
    if not token:
        print("[!] 未找到 GitHub 认证 Token。请配置 GitHub 凭据或设置环境变量 GITHUB_TOKEN。")
        sys.exit(1)

    headers = {
        "User-Agent": f"LanChat-Publisher/{version}",
        "Authorization": f"Bearer {token}",
        "Accept": "application/vnd.github+json"
    }

    # 1. 检查仓库状态
    repo_url = f"https://api.github.com/repos/{REPO}"
    try:
        req = urllib.request.Request(repo_url, headers=headers)
        with urllib.request.urlopen(req) as resp:
            repo_info = json.loads(resp.read().decode('utf-8'))
            is_private = repo_info.get("private", False)
            print(f"[+] 仓库: {REPO} (私有状态: {is_private})")
            if is_private:
                print("    > 友情提示：仓库目前为 Private (私有)。")
                print("    > 建议前往 GitHub 仓库 Settings -> Danger Zone 将其设为 Public，以便客户端免密在线更新！")
    except Exception as e:
        print(f"[!] 检查仓库信息警告: {e}")

    # 2. 查询该 Tag 的 Release 是否已存在
    release_id = None
    upload_url = None
    existing_assets = {}
    try:
        rel_check_url = f"https://api.github.com/repos/{REPO}/releases/tags/{tag_name}"
        req = urllib.request.Request(rel_check_url, headers=headers)
        with urllib.request.urlopen(req) as resp:
            data = json.loads(resp.read().decode('utf-8'))
            release_id = data.get("id")
            upload_url = data.get("upload_url", "").split("{")[0]
            print(f"[+] 找到已存在的 Release: {tag_name} (ID: {release_id})")

            # 读取已有资产列表，方便冲突时删除替换
            for a in data.get("assets", []):
                existing_assets[a.get("name")] = a.get("id")
    except urllib.error.HTTPError as e:
        if e.code != 404:
            print(f"[!] 查询 Release 出错: {e}")

    # 3. 若不存在则创建 Release
    if not release_id:
        print(f"[+] 正在创建新 Release: {tag_name} ...")
        create_url = f"https://api.github.com/repos/{REPO}/releases"
        payload = {
            "tag_name": tag_name,
            "target_commitish": "main",
            "name": release_title,
            "body": release_notes,
            "draft": False,
            "prerelease": False
        }
        req = urllib.request.Request(create_url, data=json.dumps(payload).encode('utf-8'), headers=headers, method="POST")
        try:
            with urllib.request.urlopen(req) as resp:
                data = json.loads(resp.read().decode('utf-8'))
                release_id = data.get("id")
                upload_url = data.get("upload_url", "").split("{")[0]
                print(f"[SUCCESS] Release {tag_name} 创建成功! (ID: {release_id})")
        except urllib.error.HTTPError as e:
            print(f"[ERROR] 创建 Release 失败: {e.code} {e.reason}")
            print(e.read().decode('utf-8', errors='ignore'))
            sys.exit(1)

    # 4. 上传二进制资产
    assets_to_upload = [
        os.path.join(RELEASE_DIR, "LanChat.msi"),
        os.path.join(SCRIPT_DIR, "LanChat.msi"),
        os.path.join(RELEASE_DIR, "LanChat.deb"),
        os.path.join(RELEASE_DIR, f"lanchat_{version}_all.deb"),
    ]

    uploaded_names = set()
    for asset_path in assets_to_upload:
        if not os.path.exists(asset_path):
            continue

        filename = os.path.basename(asset_path)
        if filename in uploaded_names:
            continue
        uploaded_names.add(filename)

        file_size = os.path.getsize(asset_path)
        print(f"[+] 准备上传资产: {filename} ({file_size / 1024 / 1024:.2f} MB) ...")

        # 若已存在同名资产，先删除旧资产，实现无缝覆盖替换
        if filename in existing_assets:
            old_asset_id = existing_assets[filename]
            print(f"    检测到已有旧资产 (ID: {old_asset_id})，正在删除覆盖...")
            del_url = f"https://api.github.com/repos/{REPO}/releases/assets/{old_asset_id}"
            del_req = urllib.request.Request(del_url, headers=headers, method="DELETE")
            try:
                with urllib.request.urlopen(del_req) as del_resp:
                    pass
            except Exception as del_ex:
                print(f"    删除旧资产警告: {del_ex}")

        # 读取文件二进制流并上传
        with open(asset_path, "rb") as f:
            file_data = f.read()

        upload_target = f"{upload_url}?name={filename}"
        up_headers = {
            "User-Agent": f"LanChat-Publisher/{version}",
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/octet-stream",
            "Content-Length": str(len(file_data))
        }

        up_req = urllib.request.Request(upload_target, data=file_data, headers=up_headers, method="POST")
        try:
            with urllib.request.urlopen(up_req) as resp:
                print(f"    [SUCCESS] 上传成功: {filename}")
        except urllib.error.HTTPError as e:
            print(f"    [!] 上传 {filename} 失败: {e.code} {e.reason}")
            print(e.read().decode('utf-8', errors='ignore'))

    print("=" * 65)
    print("Release 发布与资产上传全部完成！")
    print(f"查看页面: https://github.com/{REPO}/releases/tag/{tag_name}")
    print("=" * 65)

if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(errors="replace")
    publish()
