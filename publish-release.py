#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
LanChat GitHub Release 一键发布脚本
使用本地已保存的 Git 凭证自动创建 GitHub Release，并上传 LanChat.msi 与 LanChat.deb
"""

import os
import sys
import subprocess
import urllib.request
import urllib.error
import json
import mimetypes

REPO = "yxiaojiang/LanChat"
VERSION = "2.0.0"
TAG_NAME = f"v{VERSION}"
RELEASE_TITLE = f"LanChat v{VERSION} 正式版"
RELEASE_NOTES = """### 🚀 LanChat v2.0.0 重大里程碑更新

- **界面视觉微距重构**：高精度对标 WeChat Desktop 暗黑主题（侧边导航、会话列表、纯暗黑气泡对话流、文件卡片）。
- **Windows 托盘与后台驻留**：点击关闭（X）常驻托盘，支持左键恢复、暗黑右键退出与新消息气泡提醒。
- **全局单实例互斥（Mutex）**：禁止重复多开，再次双击自动唤醒已运行窗口并前置显示。
- **在线自动更新机制**：一键从 GitHub Releases 检查新版本，支持下载 MSI 并一键升级。
- **Linux 微信暗黑原生 GUI**：纯 Python 标准库 Tkinter 自绘，0 外部重型依赖，双端界面完全统一。
- **全链路诊断日志与防闪退**：消除 Linux 权限异常，提供启动器与异常追踪日志。
"""

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
RELEASE_DIR = os.path.join(SCRIPT_DIR, "release")

def get_git_token():
    # 1. 优先读取环境变量
    token = os.environ.get("GITHUB_TOKEN")
    if token:
        return token

    # 2. 从 git credential helper 读取凭据
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
    print("=" * 65)
    print("      LanChat GitHub Releases 一键发布工具")
    print("=" * 65)

    token = get_git_token()
    if not token:
        print("[!] 未找到 GitHub 认证 Token。请设置环境变量 GITHUB_TOKEN 或运行 git 命令配置凭据。")
        sys.exit(1)

    headers = {
        "User-Agent": f"LanChat-Publisher/{VERSION}",
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
                print("    > 注意：仓库目前为 Private (私有)。")
                print("    > 建议前往 GitHub 仓库 Settings -> Danger Zone 将其设为 Public，以便客户端免密在线更新！")
    except Exception as e:
        print(f"[!] 检查仓库失败: {e}")

    # 2. 检查 Release 是否已存在
    release_id = None
    upload_url = None
    try:
        rel_check_url = f"https://api.github.com/repos/{REPO}/releases/tags/{TAG_NAME}"
        req = urllib.request.Request(rel_check_url, headers=headers)
        with urllib.request.urlopen(req) as resp:
            data = json.loads(resp.read().decode('utf-8'))
            release_id = data.get("id")
            upload_url = data.get("upload_url", "").split("{")[0]
            print(f"[+] 发现已存在的 Release: {TAG_NAME} (ID: {release_id})")
    except urllib.error.HTTPError as e:
        if e.code != 404:
            print(f"[!] 查询 Release 出错: {e}")

    # 3. 若不存在则创建 Release
    if not release_id:
        print(f"[+] 正在创建 Release: {TAG_NAME} ...")
        create_url = f"https://api.github.com/repos/{REPO}/releases"
        payload = {
            "tag_name": TAG_NAME,
            "target_commitish": "main",
            "name": RELEASE_TITLE,
            "body": RELEASE_NOTES,
            "draft": False,
            "prerelease": False
        }
        req = urllib.request.Request(create_url, data=json.dumps(payload).encode('utf-8'), headers=headers, method="POST")
        try:
            with urllib.request.urlopen(req) as resp:
                data = json.loads(resp.read().decode('utf-8'))
                release_id = data.get("id")
                upload_url = data.get("upload_url", "").split("{")[0]
                print(f"[SUCCESS] Release {TAG_NAME} 创建成功! (ID: {release_id})")
        except urllib.error.HTTPError as e:
            print(f"[ERROR] 创建 Release 失败: {e.code} {e.reason}")
            print(e.read().decode('utf-8', errors='ignore'))
            sys.exit(1)

    # 4. 上传资产文件
    assets_to_upload = [
        os.path.join(RELEASE_DIR, "LanChat.msi"),
        os.path.join(RELEASE_DIR, "LanChat.deb"),
        os.path.join(RELEASE_DIR, f"lanchat_{VERSION}_all.deb"),
    ]

    for asset_path in assets_to_upload:
        if not os.path.exists(asset_path):
            print(f"[SKIP] 未找到文件: {asset_path}")
            continue

        filename = os.path.basename(asset_path)
        file_size = os.path.getsize(asset_path)
        print(f"[+] 正在上传资产: {filename} ({file_size / 1024 / 1024:.2f} MB) ...")

        # 读取文件
        with open(asset_path, "rb") as f:
            file_data = f.read()

        upload_target = f"{upload_url}?name={filename}"
        up_headers = {
            "User-Agent": f"LanChat-Publisher/{VERSION}",
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/octet-stream",
            "Content-Length": str(len(file_data))
        }

        up_req = urllib.request.Request(upload_target, data=file_data, headers=up_headers, method="POST")
        try:
            with urllib.request.urlopen(up_req) as resp:
                print(f"[SUCCESS] 上传完成: {filename}")
        except urllib.error.HTTPError as e:
            if e.code == 422:
                print(f"[INFO] 资产 {filename} 已存在，跳过。")
            else:
                print(f"[!] 上传 {filename} 失败: {e.code} {e.reason}")

    print("=" * 65)
    print("发布流程已顺利完成！")
    print(f"Release 页面: https://github.com/{REPO}/releases/tag/{TAG_NAME}")
    print("=" * 65)

if __name__ == "__main__":
    publish()
