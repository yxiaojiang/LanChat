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

#### 🔔 全链路消息通知提醒
- **未读微信红点**：收到对方新消息时，左侧会话列表好友项实时显示红点徽标（Unread Badge）；
- **任务栏黄色高亮闪烁**：收到新消息时，Windows 任务栏图标高亮黄色闪烁提醒（`FlashWindowEx`）；
- **系统托盘呼吸闪烁**：后台运行时托盘图标呼吸交替闪烁；
- **点击即时消除**：用户激活会话或主窗口后，红点、任务栏高亮与托盘闪烁自动消除。

#### 🎨 视觉与交互体验优化
- **对方信息加倍放大**：顶部会话栏对方昵称与头像整体放大 2 倍，大幅提升高分屏与远距离视觉辨识度；
- **右上角控制按钮复原**：重构顶部窗口控制栏（最小化/最大化/关闭），精确锁定 56×64 原生几何尺寸，修复纵向拉伸变形；
- **移除冗余菜单项**：移除左下角“发现新版本/安装”更新条，自身头像改为静态不可点击，避免误触；
- **输入区域体验重构**：底部小十字（+）按钮放大 1.5 倍，移除语音按钮，文本输入框宽度自适应拉伸。

#### 🛡️ 外机兼容与图标资源固化
- **图标全平台固化**：编译管线直接内嵌 `LanChat.app.ico` 资源流，新增 GDI+ 紫色 Mascot 动态自绘降级保障，彻底修复外机部署时 Windows 任务栏与托盘显示“红黄蓝方块”默认图标的问题；
- **安装包完备性**：WiX 安装包同步打包 `app.ico` 物理文件，确保快捷方式与控制面板图标正常解析。

#### 🏷️ 品牌与版权更新
- 全局版权与组织所有者由 Aritex 全面更新为 **ChaseFraser**。
"""

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
