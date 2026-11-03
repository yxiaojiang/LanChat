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
            print(f"    [!] git push 警告: {push_proc.stderr.strip()[:120]}")
    except Exception as e:
        print(f"    [!] 同步 Git 警告: {e}")

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
    branch = get_current_branch()
    tag_name = f"v{version}"
    release_title = f"LanChat v{version} 正式版"
    release_notes = f"""### 🚀 LanChat v{version} 发布与更新说明

#### 💬 托盘闪烁未读预览与多联系人卡片 (对标微信/QQ桌面端)
- **多联系人/群聊未读列表浮窗**：当有新消息且托盘图标闪烁时，鼠标悬停在托盘图标上即可自动浮现高质感暗黑预览卡片，不再只是单一静态文字；
- **全要素未读条目展现**：每个未读会话条目包含专属头像（群聊徽标或好友 Mascot 吉祥物）、联系人昵称、未读消息条数气泡、最新一条消息摘要与精确时间戳；
- **交互与快捷跳转**：条目支持鼠标悬停微光高亮；点击任意会话条目即可一键唤醒主界面并自动跳转至该会话窗口，同时清除对应未读计数；
- **多显示器与任务栏智能适配**：采用无焦点置顶顶层卡片，智能根据托盘坐标在任务栏上方/下方翻转并进行屏幕工作区边界夹紧，鼠标移开 300ms 自动平滑隐藏；
- **原生 ToolTip 63 字符安全截断**：同步格式化原生托盘提示为 `LanChat: X条未读 (联系人1、联系人2)` 并严格做 60 字符保护，彻底规避系统异常。

#### 💾 用户昵称与配置持久化保存 (跨版本升级防丢失，Windows & Linux 双端对齐)
- **操作系统用户专有目录漫游**：解决传统聊天软件升级覆盖安装后用户自定义昵称被重置为操作系统默认用户名的问题；
- **Windows 端 AppSettings 架构**：默认将用户昵称与文件传输偏好持久化至 `%AppData%\LanChat\settings.json`（同时支持可执行文件同级便携模式），MSI 覆盖安装、二进制更新、重启后昵称永久保留；
- **Linux 端跨平台同步对齐**：在 `~/.config/lanchat/settings.json`（回退 `~/.local/share/lanchat/settings.json`）中同步实现配置落盘与加载，GUI 与终端 `/nick` 改名均即时生效并永久保存。

#### 🎨 界面交互与细节综合优化
- **发送按钮状态联动**：输入框无文字时发送圆钮默认深灰未激活（不可点击），键入内容后即时点亮为明亮魅紫色；
- **屏幕工作区防溢出**：底栏 ToolTip 文字提示自适应多显示器工作区检测与避让，彻底杜绝超出屏幕边缘被截断；
- **搜索框一键清除**：搜索框右侧内嵌自绘小叉按钮（×），有内容时即时浮现，点击一键清除搜索词；
- **会话右键菜单精简**：移除冗余的“发起会话”与“复制节点 IP”，操作更聚焦高效。"""

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

        # 读取文件二进制流并上传 (增加自动退避重试与全量异常捕获)
        with open(asset_path, "rb") as f:
            file_data = f.read()

        upload_target = f"{upload_url}?name={filename}"
        up_headers = {
            "User-Agent": f"LanChat-Publisher/{version}",
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/octet-stream",
            "Content-Length": str(len(file_data))
        }

        max_retries = 3
        upload_success = False
        for attempt in range(1, max_retries + 1):
            up_req = urllib.request.Request(upload_target, data=file_data, headers=up_headers, method="POST")
            try:
                with urllib.request.urlopen(up_req, timeout=45) as resp:
                    print(f"    [SUCCESS] 上传成功: {filename}")
                    upload_success = True
                    break
            except urllib.error.HTTPError as e:
                err_detail = e.read().decode('utf-8', errors='ignore')
                print(f"    [!] 上传 {filename} 失败 (HTTP {e.code} {e.reason}) [重试 {attempt}/{max_retries}]: {err_detail[:120]}")
            except urllib.error.URLError as e:
                reason_str = str(e.reason)
                if "11001" in reason_str or "getaddrinfo failed" in reason_str:
                    print(f"    [!] 上传 {filename} 网络解析超时/DNS 失败 (uploads.github.com) [重试 {attempt}/{max_retries}]")
                else:
                    print(f"    [!] 上传 {filename} 网络连接异常 ({e.reason}) [重试 {attempt}/{max_retries}]")
            except Exception as ex:
                print(f"    [!] 上传 {filename} 发生未知异常 ({ex}) [重试 {attempt}/{max_retries}]")

            if attempt < max_retries:
                time.sleep(2)

        if not upload_success:
            print(f"    [FAILED] 资产 {filename} 经多次重试仍上传失败，请检查网络连接或运行 ipconfig /flushdns。")

    print("=" * 65)
    print("Release 发布与资产上传全部完成！")
    print(f"查看页面: https://github.com/{REPO}/releases/tag/{tag_name}")
    print("=" * 65)

if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(errors="replace")
    publish()
