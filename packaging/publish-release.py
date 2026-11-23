#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
LanChat GitHub Release 自动化发布工具
- 自动从 MainForm.cs 探测最新版本号 (或由命令行参数指定)
- 自动检测并创建/更新 GitHub Release (Tag & Notes)
- 智能管理资产：若已存在同名资产自动替换覆盖，避免 HTTP 422 冲突
- 支持流式上传 LanChat.msi、LanChat.exe、LanChat.deb 等二进制包
"""

import os
import sys
import re
import time
import subprocess
import tempfile
import shutil
import urllib.request
import urllib.error
import json

REPO = "yxiaojiang/LanChat"
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT_DIR = os.path.dirname(SCRIPT_DIR)
RELEASE_DIR = os.path.join(ROOT_DIR, "release")

def detect_version():
    if len(sys.argv) > 1 and sys.argv[1].strip() and not sys.argv[1].startswith("-"):
        return sys.argv[1].strip().lstrip("vV")

    main_cs = os.path.join(ROOT_DIR, "src", "MainForm.cs")
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

def get_current_branch():
    try:
        proc = subprocess.run(["git", "rev-parse", "--abbrev-ref", "HEAD"],
                              cwd=ROOT_DIR,
                              stdout=subprocess.PIPE,
                              stderr=subprocess.PIPE,
                              text=True,
                              check=True)
        branch = proc.stdout.strip()
        if branch and branch != "HEAD":
            return branch
    except Exception:
        pass
    return "main"

def sync_git(version: str, branch: str):
    print(f"[+] 正在检查并同步 Git 提交 (分支: {branch}) ...")
    try:
        status_proc = subprocess.run(["git", "status", "--porcelain"],
                                     cwd=ROOT_DIR,
                                     stdout=subprocess.PIPE,
                                     text=True)
        if status_proc.stdout.strip():
            print("    检测到未提交的文件，正在执行 git add 与 git commit...")
            subprocess.run(["git", "add", "-A"], cwd=ROOT_DIR, check=True)
            msg = f"release: 发布 v{version} 正式版与安装包"
            subprocess.run(["git", "commit", "-m", msg], cwd=ROOT_DIR, check=True)
            print(f"    [SUCCESS] 已本地提交: {msg}")

        print(f"    正在推送分支 {branch} 到 origin...")
        push_proc = subprocess.run(["git", "push", "origin", branch],
                                   cwd=ROOT_DIR,
                                   stdout=subprocess.PIPE,
                                   stderr=subprocess.PIPE,
                                   text=True)
        if push_proc.returncode == 0:
            print(f"    [SUCCESS] 分支 {branch} 已成功推送至 GitHub！")
        else:
            raise RuntimeError(f"git push 失败：{push_proc.stderr.strip()[:300]}")
    except Exception as e:
        raise RuntimeError(f"同步 Git 失败，已中止 Release：{e}") from e

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

def release_asset_is_uploaded(version: str, filename: str, file_size: int, headers: dict) -> bool:
    """上传请求超时后查询 Release，确认服务端是否已经收齐文件。"""
    url = f"https://api.github.com/repos/{REPO}/releases/tags/v{version}"
    try:
        req = urllib.request.Request(url, headers=headers)
        with urllib.request.urlopen(req, timeout=30) as response:
            release = json.loads(response.read().decode("utf-8"))
        return any(
            asset.get("name") == filename
            and asset.get("size") == file_size
            and asset.get("state") == "uploaded"
            for asset in release.get("assets", [])
        )
    except Exception:
        return False

def upload_asset_streaming(upload_target: str, asset_path: str, token: str, version: str):
    """用 curl 从文件流式上传 Release 资产，避免把大型 Electron 包整份读入内存。"""
    curl = shutil.which("curl.exe") or shutil.which("curl")
    if not curl:
        raise RuntimeError("未找到 curl；请安装 curl 后重试 Release 资产上传。")

    config_path = None
    escaped_token = token.replace("\\", "\\\\").replace('"', '\\"')
    try:
        with tempfile.NamedTemporaryFile("w", encoding="utf-8", delete=False, suffix=".curl") as config:
            config.write(f'header = "Authorization: Bearer {escaped_token}"\n')
            config_path = config.name

        command = [
            curl, "--config", config_path, "--silent", "--show-error", "--fail",
            "--connect-timeout", "30", "--max-time", "1800", "--request", "POST",
            "--header", "Accept: application/vnd.github+json",
            "--header", "Content-Type: application/octet-stream",
            "--data-binary", "@" + asset_path,
            "--url", upload_target,
        ]
        result = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace")
        if result.returncode:
            raise RuntimeError(result.stderr.strip() or result.stdout.strip() or f"curl 退出码 {result.returncode}")
    finally:
        if config_path and os.path.exists(config_path):
            os.unlink(config_path)

def extract_release_notes(version: str) -> str:
    changelog_path = os.path.join(ROOT_DIR, "CHANGELOG.md")
    if os.path.exists(changelog_path):
        try:
            with open(changelog_path, "r", encoding="utf-8") as f:
                text = f.read()
            pattern = rf'##\s*\[v?{re.escape(version)}\][^\n]*\n(.*?)(?=\n##\s*\[|\Z)'
            m = re.search(pattern, text, re.DOTALL)
            if m:
                raw_notes = m.group(1).strip()
                raw_notes = re.sub(r'\n---\s*$', '', raw_notes).strip()
                return f"### 🚀 LanChat v{version} 发布与更新说明\n\n" + raw_notes
        except Exception as e:
            print(f"[!] 读取 CHANGELOG.md 说明出错: {e}")
    return f"### 🚀 LanChat v{version} 正式版发布与安装包更新"

def publish():
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(errors="replace")
        except Exception:
            pass

    version = detect_version()
    branch = get_current_branch()
    tag_name = f"v{version}"
    release_title = f"LanChat v{version} 正式版"
    release_notes = extract_release_notes(version)

    print("=" * 65)
    print(f"      LanChat GitHub Releases 自动发布工具 (v{version})")
    print(f"      当前分支: {branch} | 目标版本: {tag_name}")
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

    # 1. 检查并同步 Git 提交到远端，确保 tag 准确对应代码
    sync_git(version, branch)

    # 2. 检查仓库状态
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

    # 3. 查询该 Tag 的 Release 是否已存在
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

            # 同步更新已有 Release 的标题与描述
            try:
                patch_url = f"https://api.github.com/repos/{REPO}/releases/{release_id}"
                patch_payload = {
                    "name": release_title,
                    "body": release_notes,
                }
                patch_req = urllib.request.Request(patch_url, data=json.dumps(patch_payload).encode('utf-8'), headers=headers, method="PATCH")
                with urllib.request.urlopen(patch_req) as patch_resp:
                    print(f"[+] 已同步更新 Release {tag_name} 说明文案")
            except Exception as pex:
                print(f"[!] 同步 Release 描述警告: {pex}")

    except urllib.error.HTTPError as e:
        if e.code != 404:
            print(f"[!] 查询 Release 出错: {e}")

    # 4. 若不存在则创建 Release
    if not release_id:
        print(f"[+] 正在创建新 Release: {tag_name} ...")
        create_url = f"https://api.github.com/repos/{REPO}/releases"
        payload = {
            "tag_name": tag_name,
            "target_commitish": branch,
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

    # 5. 上传二进制资产
    assets_to_upload = [
        os.path.join(RELEASE_DIR, "LanChat.exe"),
        os.path.join(RELEASE_DIR, "LanChat.msi"),
    ]
    linux_release_dir = os.path.join(RELEASE_DIR, "linux-electron")
    if os.path.isdir(linux_release_dir):
        assets_to_upload.extend(
            os.path.join(linux_release_dir, name)
            for name in sorted(os.listdir(linux_release_dir))
            if name == "SHA256SUMS.json"
            or name.startswith(f"lanchat_{version}_")
            or name.startswith(f"lanchat-cli_{version}_")
        )

    uploaded_names = set()
    upload_failures = []
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

        upload_target = f"{upload_url}?name={filename}"
        up_headers = dict(headers)

        max_retries = 3
        upload_success = False
        for attempt in range(1, max_retries + 1):
            try:
                upload_asset_streaming(upload_target, asset_path, token, version)
                print(f"    [SUCCESS] 上传成功: {filename}")
                upload_success = True
                break
            except Exception as ex:
                if release_asset_is_uploaded(version, filename, file_size, up_headers):
                    print(f"    [SUCCESS] 请求虽超时，GitHub 已完整收到: {filename}")
                    upload_success = True
                    break
                print(f"    [!] 上传 {filename} 失败 ({ex}) [重试 {attempt}/{max_retries}]")

            if attempt < max_retries:
                time.sleep(2)

        if not upload_success:
            print(f"    [FAILED] 资产 {filename} 经多次重试仍上传失败，请检查网络连接或运行 ipconfig /flushdns。")
            upload_failures.append(filename)

    print("=" * 65)
    if upload_failures:
        print("Release 已创建，但以下资产上传失败：" + ", ".join(upload_failures))
        sys.exit(1)
    print("Release 发布与资产上传全部完成！")
    print(f"查看页面: https://github.com/{REPO}/releases/tag/{tag_name}")
    print("=" * 65)

if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(errors="replace")
    publish()
