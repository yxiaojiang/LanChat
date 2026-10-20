#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
LanChat for Linux (Universal x86 / x86_64 / ARM / aarch64 / MIPS)
100% Protocol Compatible with LanChat Windows Version.
Pure Python standard library - ZERO dependencies required!
Features:
  - WeChat Desktop Dark Mode Native GUI (Tkinter)
  - Interactive Terminal CLI Fallback Mode
  - Peer Discovery (UDP 38888), P2P TCP Chat & Chunked File Streaming
  - Online Auto-Update via GitHub Releases
"""

import sys
import os
import socket
import threading
import time
import uuid
import struct
import glob
import traceback
import json
import urllib.request
import webbrowser
import subprocess

try:
    import tkinter as tk
    from tkinter import messagebox, filedialog, simpledialog
    HAS_TK = True
except ImportError:
    HAS_TK = False

APP_VERSION = "2.0.0"
GITHUB_REPO = "yxiaojiang/LanChat"
UDP_PORT = 38888
TCP_BASE_PORT = 38889
MAGIC = b"LANC"
HANDSHAKE_MSG = "[系统自动握手]"

# ==================== 多级持久化日志系统 ====================
LOG_DIR = os.path.expanduser("~/.local/share/lanchat")
try:
    os.makedirs(LOG_DIR, exist_ok=True)
except Exception:
    pass

LOG_FILE_USER = os.path.join(LOG_DIR, "lanchat_python.log")
LOG_FILE_TMP = f"/tmp/lanchat_{os.environ.get('USER', 'default')}.log"

def log_message(level: str, msg: str):
    """记录日志到用户目录及 /tmp 备用目录"""
    t_str = time.strftime("%Y-%m-%d %H:%M:%S")
    formatted = f"[{t_str}] [{level}] {msg}\n"
    for log_path in (LOG_FILE_USER, LOG_FILE_TMP):
        try:
            with open(log_path, "a", encoding="utf-8") as f:
                f.write(formatted)
        except Exception:
            pass

def handle_uncaught_exception(exc_type, exc_value, exc_traceback):
    """全局未捕获异常崩溃记录"""
    if issubclass(exc_type, KeyboardInterrupt):
        sys.__excepthook__(exc_type, exc_value, exc_traceback)
        return
    tb_text = "".join(traceback.format_exception(exc_type, exc_value, exc_traceback))
    log_message("CRITICAL", "未捕获异常崩溃:\n" + tb_text)
    print("\n\033[31m[CRITICAL ERROR] 程序发生未捕获异常崩溃:\033[0m")
    print(tb_text)
    print(f"\033[33m完整错误日志已写入:\n  1. {LOG_FILE_USER}\n  2. {LOG_FILE_TMP}\033[0m\n")

sys.excepthook = handle_uncaught_exception

def determine_save_dir() -> str:
    """智能选择接收文件目录，防止在 /usr/lib 系统只读目录下崩溃"""
    base_dir = os.path.dirname(os.path.abspath(__file__))
    is_system_path = base_dir.startswith("/usr") or base_dir.startswith("/etc") or base_dir.startswith("/opt")
    if not is_system_path:
        preferred = os.path.join(base_dir, "ReceivedFiles")
        try:
            os.makedirs(preferred, exist_ok=True)
            test_file = os.path.join(preferred, ".write_test_" + uuid.uuid4().hex[:8])
            with open(test_file, "w") as f:
                f.write("test")
            os.remove(test_file)
            return preferred
        except Exception:
            pass

    home = os.path.expanduser("~")
    downloads = os.path.join(home, "Downloads", "LanChat_Received")
    try:
        os.makedirs(downloads, exist_ok=True)
        return downloads
    except Exception:
        pass

    local_share = os.path.join(home, ".local", "share", "lanchat", "ReceivedFiles")
    try:
        os.makedirs(local_share, exist_ok=True)
        return local_share
    except Exception:
        pass

    tmp_dir = os.path.join("/tmp", f"lanchat_{os.environ.get('USER', 'default')}_received")
    os.makedirs(tmp_dir, exist_ok=True)
    return tmp_dir

class PeerInfo:
    def __init__(self, peer_id, nick, host, ip, port):
        self.id = peer_id
        self.nick = nick or "未知好友"
        self.host = host or "Linux-Host"
        self.ip = ip
        self.port = port
        self.last_seen = time.time()
        self.last_msg = ""
        self.last_msg_time = ""

    def __str__(self):
        return f"{self.nick} ({self.host} @ {self.ip}:{self.port})"

def write_dotnet_string(s: str) -> bytes:
    data = s.encode('utf-8')
    length = len(data)
    len_bytes = bytearray()
    while length >= 0x80:
        len_bytes.append((length | 0x80) & 0xFF)
        length >>= 7
    len_bytes.append(length & 0x7F)
    return bytes(len_bytes) + data

def read_dotnet_string(sock) -> str:
    length = 0
    shift = 0
    while True:
        raw = sock.recv(1)
        if not raw: return ""
        b = raw[0]
        length |= (b & 0x7F) << shift
        if (b & 0x80) == 0:
            break
        shift += 7
    data = bytearray()
    while len(data) < length:
        chunk = sock.recv(length - len(data))
        if not chunk: break
        data.extend(chunk)
    return data.decode('utf-8', errors='replace')

def format_size(bytes_num: int) -> str:
    if bytes_num < 1024: return f"{bytes_num} B"
    if bytes_num < 1024 * 1024: return f"{bytes_num / 1024.0:.1f} KB"
    if bytes_num < 1024 * 1024 * 1024: return f"{bytes_num / (1024.0 * 1024.0):.2f} MB"
    return f"{bytes_num / (1024.0 * 1024.0 * 1024.0):.2f} GB"

def get_unique_filepath(folder: str, filename: str) -> str:
    os.makedirs(folder, exist_ok=True)
    dest = os.path.join(folder, filename)
    if not os.path.exists(dest): return dest

    name, ext = os.path.splitext(filename)
    count = 1
    while os.path.exists(dest):
        dest = os.path.join(folder, f"{name} ({count}){ext}")
        count += 1
    return dest

# ==================== 核心网络通信服务 ====================
class LanChatService:
    def __init__(self, nickname: str = None):
        self.local_id = uuid.uuid4().hex
        self.nickname = nickname or os.environ.get("USER", "LinuxUser")
        self.hostname = socket.gethostname()
        self.tcp_port = TCP_BASE_PORT
        self.save_dir = determine_save_dir()
        log_message("INFO", f"LanChatService 初始化: 昵称={self.nickname}, 主机={self.hostname}, 存储目录={self.save_dir}")

        self.peers = {} # id -> PeerInfo
        self.lock = threading.Lock()
        self.is_running = True

        self.primary_ip = self.detect_primary_ip()
        self.tcp_sock = None
        self.udp_sock = None

        # 事件回调钩子 (GUI 和 CLI 共用)
        self.on_peer_joined = None
        self.on_peer_updated = None
        self.on_peer_left = None
        self.on_message_received = None
        self.on_file_received = None
        self.on_file_progress = None
        self.auto_accept = True

    def detect_primary_ip(self) -> str:
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            s.connect(("8.8.8.8", 80))
            ip = s.getsockname()[0]
            s.close()
            return ip
        except Exception:
            return "127.0.0.1"

    def start(self):
        # 1. Bind TCP Listener
        for port in range(TCP_BASE_PORT, TCP_BASE_PORT + 50):
            try:
                self.tcp_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                self.tcp_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
                self.tcp_sock.bind(("0.0.0.0", port))
                self.tcp_sock.listen(10)
                self.tcp_port = port
                break
            except Exception:
                continue

        threading.Thread(target=self._tcp_listen_loop, daemon=True).start()

        # 2. Bind UDP Listener
        try:
            self.udp_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            self.udp_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.udp_sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
            self.udp_sock.bind(("0.0.0.0", UDP_PORT))
            threading.Thread(target=self._udp_listen_loop, daemon=True).start()
        except Exception as e:
            log_message("WARN", f"UDP 绑定警告: {e}")

        # 3. Heartbeat
        threading.Thread(target=self._heartbeat_loop, daemon=True).start()

        # 4. Initial broadcast
        self.broadcast_presence("HELLO")

    def _tcp_listen_loop(self):
        while self.is_running:
            try:
                client, addr = self.tcp_sock.accept()
                client.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                threading.Thread(target=self._handle_tcp_client, args=(client, addr), daemon=True).start()
            except Exception:
                if not self.is_running: break

    def _handle_tcp_client(self, client, addr):
        try:
            with client:
                magic = client.recv(4)
                if magic != MAGIC: return

                raw_type = client.recv(1)
                if not raw_type: return
                packet_type = raw_type[0]

                if packet_type == 1: # Text message
                    is_broadcast = client.recv(1)[0] == 1
                    sender_id = read_dotnet_string(client)
                    sender_nick = read_dotnet_string(client)
                    target_id = read_dotnet_string(client)
                    content = read_dotnet_string(client)

                    # Auto learn peer
                    self.add_or_update_peer(sender_id, sender_nick, "LAN-Host", addr[0], TCP_BASE_PORT)
                    client.sendall(b"\x01") # ACK

                    if content == HANDSHAKE_MSG:
                        return

                    tag = "\033[36m[📢 广播]\033[0m" if is_broadcast else "\033[35m[🔒 私聊]\033[0m"
                    t_str = time.strftime("%H:%M:%S")
                    print(f"\n{t_str} {tag} \033[1m{sender_nick}\033[0m: {content}")
                    print("\033[32m[LanChat] > \033[0m", end="", flush=True)

                    if self.on_message_received:
                        try:
                            self.on_message_received(sender_nick, content, is_broadcast, sender_id, time.time())
                        except Exception as ex:
                            log_message("ERROR", f"on_message_received 回调异常: {ex}")

                elif packet_type == 2: # File transfer offer
                    transfer_id = read_dotnet_string(client)
                    sender_id = read_dotnet_string(client)
                    sender_nick = read_dotnet_string(client)
                    file_name = read_dotnet_string(client)
                    file_size = struct.unpack("<q", client.recv(8))[0]

                    if not self.auto_accept:
                        client.sendall(b"\x00")
                        return

                    client.sendall(b"\x01") # Accept

                    safe_name = os.path.basename(file_name)
                    local_path = get_unique_filepath(self.save_dir, safe_name)
                    temp_path = local_path + ".downloading"

                    received_bytes = 0
                    t_start = time.time()
                    with open(temp_path, "wb") as f:
                        while received_bytes < file_size:
                            to_read = min(65536, file_size - received_bytes)
                            chunk = client.recv(to_read)
                            if not chunk: break
                            f.write(chunk)
                            received_bytes += len(chunk)

                    if received_bytes == file_size:
                        if os.path.exists(local_path): os.remove(local_path)
                        os.rename(temp_path, local_path)
                        client.sendall(b"\x01")
                        t_str = time.strftime("%H:%M:%S")
                        print(f"\n\033[32m[{t_str}] 📎 成功接收文件: {safe_name} ({format_size(file_size)})\033[0m")
                        print(f"    来自: {sender_nick} | 保存至: {local_path}")
                        print("\033[32m[LanChat] > \033[0m", end="", flush=True)

                        if self.on_file_received:
                            try:
                                self.on_file_received(sender_nick, safe_name, local_path, file_size)
                            except Exception as ex:
                                log_message("ERROR", f"on_file_received 回调异常: {ex}")
                    else:
                        if os.path.exists(temp_path): os.remove(temp_path)

        except Exception:
            pass

    def _udp_listen_loop(self):
        while self.is_running:
            try:
                data, addr = self.udp_sock.recvfrom(4096)
                if not data: continue
                msg = data.decode('utf-8', errors='ignore')
                self._process_udp_packet(msg, addr)
            except Exception:
                if not self.is_running: break

    def _process_udp_packet(self, packet: str, addr):
        parts = packet.split('|')
        if len(parts) < 2: return
        cmd = parts[0]
        peer_id = parts[1]

        if peer_id == self.local_id: return

        if cmd == "BYE":
            with self.lock:
                if peer_id in self.peers:
                    del self.peers[peer_id]
                    if self.on_peer_left:
                        try: self.on_peer_left(peer_id)
                        except Exception: pass
            return

        if len(parts) >= 5:
            nick = parts[2]
            host = parts[3]
            try: port = int(parts[4])
            except: return

            final_ip = addr[0]
            if len(parts) >= 6 and parts[5] and parts[5] != "127.0.0.1":
                final_ip = parts[5]

            self.add_or_update_peer(peer_id, nick, host, final_ip, port)

            if cmd == "HELLO":
                payload = f"ACK|{self.local_id}|{self.nickname}|{self.hostname}|{self.tcp_port}|{self.primary_ip}".encode('utf-8')
                try: self.udp_sock.sendto(payload, (addr[0], UDP_PORT))
                except: pass

        elif cmd == "PING":
            with self.lock:
                if peer_id in self.peers:
                    self.peers[peer_id].last_seen = time.time()

    def add_or_update_peer(self, peer_id, nick, host, ip, port):
        if peer_id == self.local_id: return
        with self.lock:
            if peer_id not in self.peers:
                peer = PeerInfo(peer_id, nick, host, ip, port)
                self.peers[peer_id] = peer
                print(f"\n\033[32m[+] 发现好友上线: {peer}\033[0m")
                print("\033[32m[LanChat] > \033[0m", end="", flush=True)
                if self.on_peer_joined:
                    try: self.on_peer_joined(peer)
                    except Exception: pass
            else:
                p = self.peers[peer_id]
                p.nick = nick
                p.host = host
                p.ip = ip
                p.port = port
                p.last_seen = time.time()
                if self.on_peer_updated:
                    try: self.on_peer_updated(p)
                    except Exception: pass

    def broadcast_presence(self, cmd="HELLO"):
        payload = f"{cmd}|{self.local_id}|{self.nickname}|{self.hostname}|{self.tcp_port}|{self.primary_ip}".encode('utf-8')
        try: self.udp_sock.sendto(payload, ("255.255.255.255", UDP_PORT))
        except: pass

        if cmd == "HELLO":
            threading.Thread(target=self._sweep_subnet, args=(payload,), daemon=True).start()

    def _sweep_subnet(self, payload):
        ip_parts = self.primary_ip.split('.')
        if len(ip_parts) == 4:
            base = f"{ip_parts[0]}.{ip_parts[1]}.{ip_parts[2]}."
            my_last = int(ip_parts[3])
            try: self.udp_sock.sendto(payload, (base + "255", UDP_PORT))
            except: pass

            def _ping_host(dst_ip):
                try:
                    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                    sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
                    sock.sendto(payload, (dst_ip, UDP_PORT))
                    sock.close()
                except Exception:
                    pass

            for i in range(1, 255):
                if i != my_last:
                    threading.Thread(target=_ping_host, args=(f"{base}{i}",), daemon=True).start()

    def try_tcp_handshake(self, target_ip, port=TCP_BASE_PORT):
        def _worker():
            try:
                s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                s.settimeout(3.0)
                s.connect((target_ip, port))
                buf = bytearray(MAGIC)
                buf.append(1) # Text
                buf.append(0) # Not broadcast
                buf.extend(write_dotnet_string(self.local_id))
                buf.extend(write_dotnet_string(self.nickname))
                buf.extend(write_dotnet_string("DIRECT"))
                buf.extend(write_dotnet_string(HANDSHAKE_MSG))
                s.sendall(buf)
                resp = s.recv(1)
                s.close()
                if resp == b"\x01":
                    self.add_or_update_peer(uuid.uuid4().hex, f"Direct-{target_ip}", "Manual-Node", target_ip, port)
                    log_message("INFO", f"直连成功: {target_ip}:{port}")
            except Exception as e:
                log_message("WARN", f"直连 {target_ip} 失败: {e}")
        threading.Thread(target=_worker, daemon=True).start()

    def send_text_message(self, peer: PeerInfo, text: str, is_broadcast: bool) -> bool:
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.settimeout(4.0)
            s.connect((peer.ip, peer.port))
            buf = bytearray(MAGIC)
            buf.append(1) # Text
            buf.append(1 if is_broadcast else 0)
            buf.extend(write_dotnet_string(self.local_id))
            buf.extend(write_dotnet_string(self.nickname))
            buf.extend(write_dotnet_string(peer.id if not is_broadcast else "ALL"))
            buf.extend(write_dotnet_string(text))
            s.sendall(buf)
            resp = s.recv(1)
            s.close()
            return resp == b"\x01"
        except Exception as ex:
            log_message("WARN", f"发送消息给 {peer.nick} ({peer.ip}) 失败: {ex}")
            return False

    def send_file(self, peer: PeerInfo, file_path: str):
        if not os.path.exists(file_path):
            return

        file_size = os.path.getsize(file_path)
        file_name = os.path.basename(file_path)
        transfer_id = uuid.uuid4().hex

        def _do_send():
            try:
                s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                s.settimeout(5.0)
                s.connect((peer.ip, peer.port))
                s.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)

                buf = bytearray(MAGIC)
                buf.append(2) # File Offer
                buf.extend(write_dotnet_string(transfer_id))
                buf.extend(write_dotnet_string(self.local_id))
                buf.extend(write_dotnet_string(self.nickname))
                buf.extend(write_dotnet_string(file_name))
                buf.extend(struct.pack("<q", file_size))
                s.sendall(buf)

                s.settimeout(10.0)
                accept = s.recv(1)
                if accept != b"\x01":
                    s.close()
                    return

                sent_bytes = 0
                t_start = time.time()
                with open(file_path, "rb") as f:
                    while True:
                        chunk = f.read(65536)
                        if not chunk: break
                        s.sendall(chunk)
                        sent_bytes += len(chunk)
                        el = time.time() - t_start
                        spd = (sent_bytes / (el * 1024 * 1024)) if el > 0 else 0
                        if self.on_file_progress:
                            try: self.on_file_progress(file_name, sent_bytes, file_size, spd, True)
                            except Exception: pass

                s.recv(1)
                s.close()
                if self.on_file_progress:
                    try: self.on_file_progress(file_name, file_size, file_size, 0, False)
                    except Exception: pass

            except Exception as ex:
                log_message("ERROR", f"发送文件失败: {ex}")

        threading.Thread(target=_do_send, daemon=True).start()

    def _heartbeat_loop(self):
        while self.is_running:
            time.sleep(8)
            payload = f"PING|{self.local_id}".encode('utf-8')
            try: self.udp_sock.sendto(payload, ("255.255.255.255", UDP_PORT))
            except: pass

            now = time.time()
            with self.lock:
                expired = [pid for pid, p in self.peers.items() if now - p.last_seen > 25]
                for pid in expired:
                    del self.peers[pid]
                    if self.on_peer_left:
                        try: self.on_peer_left(pid)
                        except Exception: pass

    def close(self):
        self.is_running = False
        payload = f"BYE|{self.local_id}".encode('utf-8')
        try: self.udp_sock.sendto(payload, ("255.255.255.255", UDP_PORT))
        except: pass
        try: self.tcp_sock.close()
        except: pass
        try: self.udp_sock.close()
        except: pass

# ==================== Linux 微信暗黑风格图形界面 (WeChatGui) ====================
if HAS_TK:
    class WeChatGui:
        def __init__(self, service: LanChatService):
            self.service = service
            self.root = tk.Tk()
            self.root.title("LanChat")
            self.root.geometry("980x660")
            self.root.minsize(820, 540)
            self.root.configure(bg="#1e1e1f")

            # 图标绑定
            for icon_path in [
                os.path.join(os.path.dirname(os.path.abspath(__file__)), "app.png"),
                "/usr/share/icons/hicolor/256x256/apps/lanchat.png",
                "/usr/share/pixmaps/lanchat.png"
            ]:
                if os.path.exists(icon_path):
                    try:
                        self._icon_img = tk.PhotoImage(file=icon_path)
                        self.root.iconphoto(True, self._icon_img)
                        break
                    except Exception:
                        pass

            # 数据态
            self.current_conv_key = "BROADCAST"  # "BROADCAST" 或 peer.id
            self.histories = {"BROADCAST": []}   # key -> list of dict
            self.unread_counts = {}              # key -> count
            self.peer_items = {}                 # key -> UI components

            # 颜色规范：深度对标 WeChat Desktop Dark Mode
            self.C_SIDEBAR = "#252526"
            self.C_SESSIONS = "#202021"
            self.C_CHAT = "#1e1e1f"
            self.C_BORDER = "#2b2b2d"
            self.C_GREEN = "#07c160"
            self.C_GREEN_HOVER = "#06ad56"
            self.C_BUBBLE_OTHER = "#2c2c2e"
            self.C_BUBBLE_SELF = "#07c160"
            self.C_TEXT_WHITE = "#f0f0f0"
            self.C_TEXT_MUTED = "#888888"
            self.C_SESSION_SELECT = "#333335"
            self.C_SESSION_HOVER = "#2a2a2b"

            self._build_ui()
            self._wire_callbacks()

            self.root.protocol("WM_DELETE_WINDOW", self._on_close)

        def _build_ui(self):
            # 1. 左侧边栏 (导航与头像)
            self.sidebar = tk.Frame(self.root, bg=self.C_SIDEBAR, width=58)
            self.sidebar.pack(side="left", fill="y")
            self.sidebar.pack_propagate(False)

            self.avatar_canvas = tk.Canvas(self.sidebar, width=42, height=42, bg=self.C_SIDEBAR, highlightthickness=0)
            self.avatar_canvas.pack(pady=(16, 20))
            self._draw_avatar()

            self.btn_nav_chat = tk.Button(
                self.sidebar, text="💬", font=("Microsoft YaHei", 15),
                bg=self.C_SIDEBAR, fg=self.C_GREEN, relief="flat", bd=0, cursor="hand2"
            )
            self.btn_nav_chat.pack(pady=6)

            self.btn_nav_refresh = tk.Button(
                self.sidebar, text="🔄", font=("Microsoft YaHei", 13),
                bg=self.C_SIDEBAR, fg=self.C_TEXT_MUTED, relief="flat", bd=0, cursor="hand2",
                command=self._on_refresh
            )
            self.btn_nav_refresh.pack(pady=6)

            self.btn_nav_add = tk.Button(
                self.sidebar, text="➕", font=("Microsoft YaHei", 13),
                bg=self.C_SIDEBAR, fg=self.C_TEXT_MUTED, relief="flat", bd=0, cursor="hand2",
                command=self._on_add_ip
            )
            self.btn_nav_add.pack(pady=6)

            self.btn_settings = tk.Button(
                self.sidebar, text="≡", font=("Microsoft YaHei", 16, "bold"),
                bg=self.C_SIDEBAR, fg=self.C_TEXT_MUTED, relief="flat", bd=0, cursor="hand2",
                command=self._show_settings_menu
            )
            self.btn_settings.pack(side="bottom", pady=16)

            # 2. 中间会话列表栏
            self.sessions_panel = tk.Frame(self.root, bg=self.C_SESSIONS, width=240)
            self.sessions_panel.pack(side="left", fill="y")
            self.sessions_panel.pack_propagate(False)

            search_frame = tk.Frame(self.sessions_panel, bg=self.C_SESSIONS, height=52)
            search_frame.pack(fill="x", padx=10, pady=(12, 6))

            search_box = tk.Frame(search_frame, bg="#2c2c2e", bd=0)
            search_box.pack(fill="x")

            lbl_lens = tk.Label(search_box, text="🔍", bg="#2c2c2e", fg="#888888", font=("Microsoft YaHei", 9))
            lbl_lens.pack(side="left", padx=(6, 2))

            self.entry_search = tk.Entry(
                search_box, bg="#2c2c2e", fg="#e0e0e0", insertbackground="#ffffff",
                relief="flat", font=("Microsoft YaHei", 9), bd=0
            )
            self.entry_search.pack(side="left", fill="x", expand=True, ipady=4, padx=(0, 6))
            self.entry_search.bind("<KeyRelease>", lambda e: self._render_sessions())

            sep1 = tk.Frame(self.sessions_panel, bg=self.C_BORDER, height=1)
            sep1.pack(fill="x", pady=(6, 0))

            self.sessions_container = tk.Frame(self.sessions_panel, bg=self.C_SESSIONS)
            self.sessions_container.pack(fill="both", expand=True)

            # 3. 右侧主聊天区
            self.chat_panel = tk.Frame(self.root, bg=self.C_CHAT)
            self.chat_panel.pack(side="right", fill="both", expand=True)

            # 顶部标头
            self.header_frame = tk.Frame(self.chat_panel, bg=self.C_CHAT, height=54)
            self.header_frame.pack(fill="x")
            self.header_frame.pack_propagate(False)

            header_info = tk.Frame(self.header_frame, bg=self.C_CHAT)
            header_info.pack(side="left", padx=16, pady=8)

            self.lbl_chat_title = tk.Label(
                header_info, text="📢 局域网全员群聊", font=("Microsoft YaHei", 12, "bold"),
                bg=self.C_CHAT, fg=self.C_TEXT_WHITE
            )
            self.lbl_chat_title.pack(anchor="w")

            self.lbl_chat_sub = tk.Label(
                header_info, text="局域网广播模式 (所有人可见)", font=("Microsoft YaHei", 8),
                bg=self.C_CHAT, fg=self.C_TEXT_MUTED
            )
            self.lbl_chat_sub.pack(anchor="w")

            header_actions = tk.Frame(self.header_frame, bg=self.C_CHAT)
            header_actions.pack(side="right", padx=16, pady=10)

            for text, cmd in [
                ("🔄 刷新", self._on_refresh),
                ("➕ 加IP", self._on_add_ip),
                ("📂 接收目录", self._open_save_folder),
                ("🧹 清屏", self._clear_current_chat)
            ]:
                b = tk.Button(
                    header_actions, text=text, font=("Microsoft YaHei", 8),
                    bg="#262628", fg="#cccccc", activebackground="#333336",
                    activeforeground="#ffffff", relief="flat", bd=0, padx=8, pady=3,
                    cursor="hand2", command=cmd
                )
                b.pack(side="left", padx=3)

            sep2 = tk.Frame(self.chat_panel, bg=self.C_BORDER, height=1)
            sep2.pack(fill="x")

            # 传输进度条条幅 (默认隐藏)
            self.progress_frame = tk.Frame(self.chat_panel, bg="#262628", height=28)
            self.lbl_progress = tk.Label(self.progress_frame, text="", font=("Microsoft YaHei", 8), bg="#262628", fg=self.C_GREEN)
            self.lbl_progress.pack(side="left", padx=12, pady=4)

            # 消息流视图
            msg_container = tk.Frame(self.chat_panel, bg=self.C_CHAT)
            msg_container.pack(fill="both", expand=True)

            self.txt_history = tk.Text(
                msg_container, bg=self.C_CHAT, fg=self.C_TEXT_WHITE,
                relief="flat", bd=0, font=("Microsoft YaHei", 10),
                padx=16, pady=10, state="disabled", wrap="word",
                selectbackground=self.C_GREEN, selectforeground="#ffffff"
            )
            history_scroll = tk.Scrollbar(msg_container, orient="vertical", command=self.txt_history.yview)
            self.txt_history.configure(yscrollcommand=history_scroll.set)

            history_scroll.pack(side="right", fill="y")
            self.txt_history.pack(side="left", fill="both", expand=True)

            # 气泡标签配置
            self.txt_history.tag_configure("sys_msg", justify="center", foreground="#888888", font=("Microsoft YaHei", 8), spacing1=6, spacing3=6)
            self.txt_history.tag_configure("center_time", justify="center", foreground="#666666", font=("Microsoft YaHei", 8), spacing1=6, spacing3=4)
            self.txt_history.tag_configure("other_nick", justify="left", foreground="#999999", font=("Microsoft YaHei", 8), spacing1=6, spacing3=2)
            self.txt_history.tag_configure("other_bubble", justify="left", background=self.C_BUBBLE_OTHER, foreground="#f0f0f0", font=("Microsoft YaHei", 10), lmargin1=10, lmargin2=10, rmargin=140, spacing1=4, spacing3=4)
            self.txt_history.tag_configure("self_nick", justify="right", foreground="#999999", font=("Microsoft YaHei", 8), spacing1=6, spacing3=2)
            self.txt_history.tag_configure("self_bubble", justify="right", background="#069c4e", foreground="#ffffff", font=("Microsoft YaHei", 10), lmargin1=140, rmargin=10, spacing1=4, spacing3=4)
            self.txt_history.tag_configure("file_card", justify="left", background="#28282b", foreground=self.C_GREEN, font=("Microsoft YaHei", 9, "bold"), spacing1=4, spacing3=4)

            # 输入工具栏
            sep3 = tk.Frame(self.chat_panel, bg=self.C_BORDER, height=1)
            sep3.pack(fill="x")

            tool_frame = tk.Frame(self.chat_panel, bg=self.C_CHAT, height=32)
            tool_frame.pack(fill="x", padx=12, pady=4)

            btn_send_file = tk.Button(
                tool_frame, text="📁 发送文件", font=("Microsoft YaHei", 8),
                bg=self.C_CHAT, fg="#cccccc", activebackground="#2c2c2e", activeforeground="#ffffff",
                relief="flat", bd=0, padx=6, pady=2, cursor="hand2", command=self._on_send_file
            )
            btn_send_file.pack(side="left", padx=4)

            self.btn_auto_accept = tk.Button(
                tool_frame, text="⚡ 自动接收: 开", font=("Microsoft YaHei", 8),
                bg=self.C_CHAT, fg=self.C_GREEN, activebackground="#2c2c2e", activeforeground=self.C_GREEN,
                relief="flat", bd=0, padx=6, pady=2, cursor="hand2", command=self._toggle_auto_accept
            )
            self.btn_auto_accept.pack(side="left", padx=4)

            btn_folder = tk.Button(
                tool_frame, text="📂 接收目录", font=("Microsoft YaHei", 8),
                bg=self.C_CHAT, fg="#cccccc", activebackground="#2c2c2e", activeforeground="#ffffff",
                relief="flat", bd=0, padx=6, pady=2, cursor="hand2", command=self._open_save_folder
            )
            btn_folder.pack(side="left", padx=4)

            btn_clear = tk.Button(
                tool_frame, text="🧹 清屏", font=("Microsoft YaHei", 8),
                bg=self.C_CHAT, fg="#cccccc", activebackground="#2c2c2e", activeforeground="#ffffff",
                relief="flat", bd=0, padx=6, pady=2, cursor="hand2", command=self._clear_current_chat
            )
            btn_clear.pack(side="left", padx=4)

            # 多行输入框
            input_container = tk.Frame(self.chat_panel, bg=self.C_CHAT)
            input_container.pack(fill="x", padx=12, pady=(0, 4))

            self.txt_input = tk.Text(
                input_container, bg=self.C_CHAT, fg="#f0f0f0", insertbackground="#ffffff",
                font=("Microsoft YaHei", 10), height=4, relief="flat", bd=0,
                padx=4, pady=4, wrap="word"
            )
            self.txt_input.pack(fill="x")
            self.txt_input.bind("<Return>", self._on_enter_pressed)
            self.txt_input.bind("<Control-Return>", lambda e: self.txt_input.insert("insert", "\n"))

            # 底部发送面板
            bottom_frame = tk.Frame(self.chat_panel, bg=self.C_CHAT, height=36)
            bottom_frame.pack(fill="x", padx=16, pady=(0, 10))

            lbl_tip = tk.Label(bottom_frame, text="按 Enter 发送，Ctrl+Enter 换行", font=("Microsoft YaHei", 8), bg=self.C_CHAT, fg="#666666")
            lbl_tip.pack(side="left")

            self.btn_send = tk.Button(
                bottom_frame, text="发送 (S)", font=("Microsoft YaHei", 9, "bold"),
                bg=self.C_GREEN, fg="#ffffff", activebackground=self.C_GREEN_HOVER,
                activeforeground="#ffffff", relief="flat", bd=0, padx=18, pady=4,
                cursor="hand2", command=self._send_message
            )
            self.btn_send.pack(side="right")

            # 初始化渲染
            self._render_sessions()
            self._append_system_message("欢迎使用 LanChat 局域网即时通讯客户端 (Linux 微信暗黑版)！")
            self._append_system_message(f"本机 IP: {self.service.primary_ip}:{self.service.tcp_port} | 昵称: {self.service.nickname}")

        def _draw_avatar(self):
            self.avatar_canvas.delete("all")
            self.avatar_canvas.create_oval(2, 2, 40, 40, fill=self.C_GREEN, outline="")
            initial = (self.service.nickname[0] if self.service.nickname else "L").upper()
            self.avatar_canvas.create_text(21, 21, text=initial, fill="#ffffff", font=("Microsoft YaHei", 12, "bold"))

        def _wire_callbacks(self):
            self.service.on_peer_joined = lambda p: self.root.after(0, self._handle_peer_joined, p)
            self.service.on_peer_updated = lambda p: self.root.after(0, self._handle_peer_updated, p)
            self.service.on_peer_left = lambda pid: self.root.after(0, self._handle_peer_left, pid)
            self.service.on_message_received = lambda nick, cnt, is_bc, pid, ts: self.root.after(0, self._handle_msg_recv, nick, cnt, is_bc, pid, ts)
            self.service.on_file_received = lambda nick, fn, path, sz: self.root.after(0, self._handle_file_recv, nick, fn, path, sz)
            self.service.on_file_progress = lambda fn, tr, tot, spd, sending: self.root.after(0, self._handle_file_prog, fn, tr, tot, spd, sending)

        def _render_sessions(self):
            for widget in self.sessions_container.winfo_children():
                widget.destroy()

            keyword = self.entry_search.get().strip().lower()

            # 1. 广播群聊
            if not keyword or "群聊" in keyword or "broadcast" in keyword:
                bg = self.C_SESSION_SELECT if self.current_conv_key == "BROADCAST" else self.C_SESSIONS
                f_bc = tk.Frame(self.sessions_container, bg=bg, height=54, cursor="hand2")
                f_bc.pack(fill="x", padx=4, pady=2)
                f_bc.pack_propagate(False)

                lbl_t = tk.Label(f_bc, text="📢 局域网全员群聊", font=("Microsoft YaHei", 9, "bold"), bg=bg, fg=self.C_TEXT_WHITE)
                lbl_t.pack(anchor="w", padx=10, pady=(8, 0))

                with self.service.lock:
                    count = len(self.service.peers)
                lbl_sub = tk.Label(f_bc, text=f"在线成员: {count} 人", font=("Microsoft YaHei", 8), bg=bg, fg=self.C_TEXT_MUTED)
                lbl_sub.pack(anchor="w", padx=10)

                for w in (f_bc, lbl_t, lbl_sub):
                    w.bind("<Button-1>", lambda e: self._select_session("BROADCAST"))

            # 2. 好友列表
            with self.service.lock:
                peers_list = list(self.service.peers.values())

            for p in peers_list:
                if keyword and (keyword not in p.nick.lower() and keyword not in p.ip):
                    continue

                bg = self.C_SESSION_SELECT if self.current_conv_key == p.id else self.C_SESSIONS
                f_p = tk.Frame(self.sessions_container, bg=bg, height=54, cursor="hand2")
                f_p.pack(fill="x", padx=4, pady=2)
                f_p.pack_propagate(False)

                top_box = tk.Frame(f_p, bg=bg)
                top_box.pack(fill="x", padx=10, pady=(6, 0))

                lbl_dot = tk.Label(top_box, text="●", font=("Microsoft YaHei", 8), bg=bg, fg=self.C_GREEN)
                lbl_dot.pack(side="left")

                lbl_nick = tk.Label(top_box, text=p.nick, font=("Microsoft YaHei", 9, "bold"), bg=bg, fg=self.C_TEXT_WHITE)
                lbl_nick.pack(side="left", padx=4)

                if self.unread_counts.get(p.id, 0) > 0:
                    lbl_badge = tk.Label(top_box, text=str(self.unread_counts[p.id]), font=("Microsoft YaHei", 7, "bold"), bg="#fa5151", fg="#ffffff", padx=4)
                    lbl_badge.pack(side="right")

                lbl_msg = tk.Label(f_p, text=p.last_msg or f"{p.ip}:{p.port}", font=("Microsoft YaHei", 8), bg=bg, fg=self.C_TEXT_MUTED)
                lbl_msg.pack(anchor="w", padx=10)

                pid = p.id
                for w in (f_p, top_box, lbl_dot, lbl_nick, lbl_msg):
                    w.bind("<Button-1>", lambda e, k=pid: self._select_session(k))

        def _select_session(self, conv_key):
            self.current_conv_key = conv_key
            self.unread_counts[conv_key] = 0

            if conv_key == "BROADCAST":
                self.lbl_chat_title.config(text="📢 局域网全员群聊")
                with self.service.lock:
                    count = len(self.service.peers)
                self.lbl_chat_sub.config(text=f"局域网广播模式 (共 {count} 位好友在线)")
            else:
                with self.service.lock:
                    peer = self.service.peers.get(conv_key)
                if peer:
                    self.lbl_chat_title.config(text=peer.nick)
                    self.lbl_chat_sub.config(text=f"{peer.host} @ {peer.ip}:{peer.port}")

            self._render_sessions()
            self._redraw_chat_history()

        def _redraw_chat_history(self):
            self.txt_history.configure(state="normal")
            self.txt_history.delete("1.0", "end")
            msgs = self.histories.get(self.current_conv_key, [])
            for item in msgs:
                self._render_message_item(item)
            self.txt_history.configure(state="disabled")
            self.txt_history.see("end")

        def _render_message_item(self, item):
            is_self = item["is_self"]
            sender = item["sender"]
            text = item["text"]
            t_str = item["time"]
            is_file = item.get("is_file", False)

            if item.get("is_sys", False):
                self.txt_history.insert("end", f"{text}\n", "sys_msg")
                return

            if is_self:
                self.txt_history.insert("end", f"{t_str}  我\n", "self_nick")
                if is_file:
                    self.txt_history.insert("end", f" [发送文件] {text} \n\n", "file_card")
                else:
                    self.txt_history.insert("end", f" {text} \n\n", "self_bubble")
            else:
                self.txt_history.insert("end", f"{sender}  {t_str}\n", "other_nick")
                if is_file:
                    self.txt_history.insert("end", f" [接收文件] {text} \n\n", "file_card")
                else:
                    self.txt_history.insert("end", f" {text} \n\n", "other_bubble")

        def _append_message(self, conv_key, is_self, sender, text, is_file=False):
            t_str = time.strftime("%H:%M:%S")
            item = {"is_self": is_self, "sender": sender, "text": text, "time": t_str, "is_file": is_file}
            if conv_key not in self.histories:
                self.histories[conv_key] = []
            self.histories[conv_key].append(item)

            if conv_key == self.current_conv_key:
                self.txt_history.configure(state="normal")
                self._render_message_item(item)
                self.txt_history.configure(state="disabled")
                self.txt_history.see("end")
            else:
                self.unread_counts[conv_key] = self.unread_counts.get(conv_key, 0) + 1
                self._render_sessions()

        def _append_system_message(self, text):
            item = {"is_sys": True, "text": text}
            self.histories["BROADCAST"].append(item)
            if self.current_conv_key == "BROADCAST":
                self.txt_history.configure(state="normal")
                self.txt_history.insert("end", f"{text}\n", "sys_msg")
                self.txt_history.configure(state="disabled")
                self.txt_history.see("end")

        def _on_enter_pressed(self, event):
            self._send_message()
            return "break"

        def _send_message(self):
            text = self.txt_input.get("1.0", "end-1c").strip()
            if not text: return
            self.txt_input.delete("1.0", "end")

            if self.current_conv_key == "BROADCAST":
                self._append_message("BROADCAST", True, self.service.nickname, text)
                with self.service.lock:
                    plist = list(self.service.peers.values())
                for p in plist:
                    threading.Thread(target=self.service.send_text_message, args=(p, text, True), daemon=True).start()
            else:
                with self.service.lock:
                    peer = self.service.peers.get(self.current_conv_key)
                if peer:
                    self._append_message(self.current_conv_key, True, self.service.nickname, text)
                    peer.last_msg = text
                    threading.Thread(target=self.service.send_text_message, args=(peer, text, False), daemon=True).start()
                    self._render_sessions()

        def _handle_msg_recv(self, sender_nick, content, is_broadcast, sender_id, ts):
            conv_key = "BROADCAST" if is_broadcast else sender_id
            self._append_message(conv_key, False, sender_nick, content)
            with self.service.lock:
                if sender_id in self.service.peers:
                    self.service.peers[sender_id].last_msg = content
            self._render_sessions()

        def _handle_file_recv(self, sender_nick, file_name, local_path, file_size):
            self.progress_frame.pack_forget()
            conv_key = self.current_conv_key
            with self.service.lock:
                for pid, p in self.service.peers.items():
                    if p.nick == sender_nick:
                        conv_key = pid
                        break
            text = f"{file_name} ({format_size(file_size)})\n保存至: {local_path}"
            self._append_message(conv_key, False, sender_nick, text, is_file=True)

        def _handle_file_prog(self, file_name, transferred, total, speed, is_sending):
            if is_sending:
                pct = int((transferred * 100) / total) if total > 0 else 100
                self.lbl_progress.config(text=f"📎 发送文件: {file_name} ({pct}%) - {speed:.1f} MB/s")
                self.progress_frame.pack(fill="x", before=self.txt_history.master)
            else:
                self.progress_frame.pack_forget()

        def _handle_peer_joined(self, peer):
            self._append_system_message(f"[+] 好友上线: {peer.nick} ({peer.ip})")
            self._render_sessions()

        def _handle_peer_updated(self, peer):
            self._render_sessions()

        def _handle_peer_left(self, peer_id):
            self._append_system_message(f"[-] 好友已离线")
            self._render_sessions()

        def _on_send_file(self):
            if self.current_conv_key == "BROADCAST":
                messagebox.showinfo("提示", "请先在中间列表中点击选择一位私聊好友，再发送文件。")
                return

            with self.service.lock:
                peer = self.service.peers.get(self.current_conv_key)
            if not peer:
                messagebox.showwarning("提示", "当前好友不在线，无法发送文件。")
                return

            filepath = filedialog.askopenfilename(title="选择要发送的文件")
            if filepath:
                self._append_message(self.current_conv_key, True, self.service.nickname, os.path.basename(filepath), is_file=True)
                self.service.send_file(peer, filepath)

        def _toggle_auto_accept(self):
            self.service.auto_accept = not self.service.auto_accept
            txt = "⚡ 自动接收: 开" if self.service.auto_accept else "⏸ 自动接收: 关"
            fg = self.C_GREEN if self.service.auto_accept else "#888888"
            self.btn_auto_accept.config(text=txt, fg=fg)

        def _open_save_folder(self):
            save_dir = self.service.save_dir
            try:
                if sys.platform == "darwin": subprocess.Popen(["open", save_dir])
                elif sys.platform == "win32": os.startfile(save_dir)
                else: subprocess.Popen(["xdg-open", save_dir])
            except Exception as e:
                messagebox.showerror("打开目录失败", str(e))

        def _clear_current_chat(self):
            self.histories[self.current_conv_key] = []
            self._redraw_chat_history()

        def _on_refresh(self):
            self.service.broadcast_presence("HELLO")
            self._append_system_message("正在重新向局域网广播自身并探测在线好友...")

        def _on_add_ip(self):
            ip = simpledialog.askstring("添加好友 IP", "请输入对方的局域网 IP 地址:", parent=self.root)
            if ip and ip.strip():
                ip = ip.strip()
                self._append_system_message(f"正在主动向 {ip} 发起 TCP 握手探测...")
                self.service.try_tcp_handshake(ip)

        def _show_settings_menu(self):
            menu = tk.Menu(self.root, tearoff=0, bg="#2c2c2e", fg="#f0f0f0", activebackground="#3c3c40", activeforeground="#ffffff")
            menu.add_command(label="✏️ 修改我的昵称...", command=self._on_change_nick)
            menu.add_command(label="⚡ 切换自动接收文件", command=self._toggle_auto_accept)
            menu.add_command(label="📂 打开接收文件夹", command=self._open_save_folder)
            menu.add_command(label="🧹 清空当前聊天", command=self._clear_current_chat)
            menu.add_separator()
            menu.add_command(label="🔄 检查新版本...", command=lambda: self._check_for_updates(False))
            menu.add_command(label="ℹ️ 关于 LanChat", command=self._show_about)
            menu.add_separator()
            menu.add_command(label="🚪 退出 LanChat", command=self._on_close)

            try:
                x = self.btn_settings.winfo_rootx() + 45
                y = self.btn_settings.winfo_rooty() - 100
                menu.post(x, y)
            except Exception:
                pass

        def _on_change_nick(self):
            new_nick = simpledialog.askstring("修改昵称", "请输入新的昵称:", initialvalue=self.service.nickname, parent=self.root)
            if new_nick and new_nick.strip():
                self.service.nickname = new_nick.strip()
                self._draw_avatar()
                self.service.broadcast_presence("HELLO")
                self._append_system_message(f"您的昵称已更改为: {self.service.nickname}")

        def _show_about(self):
            msg = (
                f"LanChat v{APP_VERSION} (Linux 微信暗黑版)\n\n"
                "局域网秒级极速通讯与文件互传\n"
                "UI: 纯标准库自绘 WeChat Desktop Dark Mode\n"
                "零外部运行库依赖\n"
                f"项目主页: https://github.com/{GITHUB_REPO}"
            )
            messagebox.showinfo("关于 LanChat", msg)

        def _check_for_updates(self, silent=False):
            def _worker():
                try:
                    url = f"https://api.github.com/repos/{GITHUB_REPO}/releases/latest"
                    req = urllib.request.Request(url, headers={"User-Agent": f"LanChat-Linux/{APP_VERSION}"})
                    with urllib.request.urlopen(req, timeout=8) as resp:
                        data = json.loads(resp.read().decode('utf-8'))

                    tag = data.get("tag_name", "").lstrip("vV")
                    body = data.get("body", "包含重要稳定性更新。")
                    html_url = data.get("html_url", f"https://github.com/{GITHUB_REPO}/releases")

                    cur_parts = [int(x) for x in APP_VERSION.split('.') if x.isdigit()]
                    tag_parts = [int(x) for x in tag.split('.') if x.isdigit()]

                    if tag_parts > cur_parts:
                        self.root.after(0, lambda: self._show_update_dialog(tag, body, html_url))
                    else:
                        if not silent:
                            self.root.after(0, lambda: messagebox.showinfo("检查更新", f"当前已是最新版本 (v{APP_VERSION})，无需更新。"))
                except Exception as ex:
                    if not silent:
                        self.root.after(0, lambda: messagebox.showwarning("检查更新", f"检查更新失败: {ex}\n请确认能否访问 GitHub。"))

            threading.Thread(target=_worker, daemon=True).start()

        def _show_update_dialog(self, tag, body, html_url):
            dlg = tk.Toplevel(self.root)
            dlg.title("发现新版本")
            dlg.geometry("500x380")
            dlg.configure(bg="#202021")
            dlg.transient(self.root)

            lbl = tk.Label(dlg, text=f"🚀 发现 LanChat 新版本: v{tag}", font=("Microsoft YaHei", 12, "bold"), bg="#202021", fg=self.C_GREEN)
            lbl.pack(pady=(16, 4))

            lbl_sub = tk.Label(dlg, text=f"当前版本: v{APP_VERSION}", font=("Microsoft YaHei", 8), bg="#202021", fg="#888888")
            lbl_sub.pack()

            txt = tk.Text(dlg, bg="#2c2c2e", fg="#dddddd", font=("Microsoft YaHei", 9), height=10, relief="flat", padx=8, pady=8)
            txt.insert("1.0", f"更新日志:\n{body}")
            txt.configure(state="disabled")
            txt.pack(fill="both", expand=True, padx=20, pady=12)

            btn_box = tk.Frame(dlg, bg="#202021")
            btn_box.pack(pady=(0, 16))

            btn_open = tk.Button(btn_box, text="🌐 前往 GitHub 下载", bg=self.C_GREEN, fg="#ffffff", font=("Microsoft YaHei", 9, "bold"), relief="flat", padx=14, pady=4, cursor="hand2", command=lambda: webbrowser.open(html_url))
            btn_open.pack(side="left", padx=8)

            btn_close = tk.Button(btn_box, text="关闭", bg="#3c3c40", fg="#cccccc", relief="flat", padx=14, pady=4, cursor="hand2", command=dlg.destroy)
            btn_close.pack(side="left", padx=8)

        def _on_close(self):
            log_message("INFO", "=== LanChat GUI 退出 ===")
            self.service.close()
            self.root.destroy()

        def run(self):
            self.root.mainloop()

# ==================== 终端命令行备用模式 (CLI) ====================
def run_cli(service: LanChatService):
    print("\n" + "="*70)
    print("      \033[33mLanChat for Linux (Universal Console Edition)\033[0m")
    print("="*70)
    print(f" [+] 本机物理 IP:  {service.primary_ip}:{service.tcp_port}")
    print(f" [+] 本机昵称:     {service.nickname} ({service.hostname})")
    print(f" [+] 文件保存目录: {service.save_dir}")
    print(" [+] 常用命令:     /list (查看好友)  /chat <编号|all> (切换聊天)  /send <文件>")
    print("                  /add <IP> (连接好友)  /help (查看帮助)  /quit (退出)")
    print("="*70 + "\n")

    current_chat = None # None = broadcast

    while True:
        try:
            prompt_str = "\033[32m[📢 广播群聊] > \033[0m" if not current_chat else f"\033[35m[私聊: {current_chat.nick}] > \033[0m"
            line = input(prompt_str).strip()
            if not line: continue

            if line.startswith("/"):
                parts = line.split()
                cmd = parts[0].lower()

                if cmd in ["/help", "/h", "/?"]:
                    print("\n--- 指令帮助 ---")
                    print(" 直接输入文字: 发送消息给当前会话 (默认是全局广播)")
                    print(" /list, /peers: 查看当前所有在线好友")
                    print(" /chat <序号|all>: 切换私聊对象 (0 或 all 为广播模式)")
                    print(" /send <文件路径>: 发送文件给当前好友")
                    print(" /add <IP>: 手动连通对方电脑 (跨网段/VPN必备)")
                    print(" /refresh: 重新向局域网广播并扫描网段")
                    print(" /nick <新昵称>: 修改自己的昵称")
                    print(" /quit, /exit: 退出程序\n")

                elif cmd in ["/list", "/peers"]:
                    with service.lock:
                        plist = list(service.peers.values())
                    print(f"\n--- 在线好友列表 (共 {len(plist)} 人) ---")
                    print(" #  | 昵称              | 主机名          | IP:端口")
                    print(" 0  | 📢 局域网所有人   | ALL PEERS       | 255.255.255.255")
                    for idx, p in enumerate(plist, 1):
                        cur_tag = " <-- [当前私聊]" if current_chat and current_chat.id == p.id else ""
                        print(f" {idx:<2} | {p.nick:<17} | {p.host:<15} | {p.ip}:{p.port}{cur_tag}")
                    print()

                elif cmd in ["/chat", "/to"]:
                    if len(parts) < 2:
                        print("\033[33m用法: /chat <编号|all>\033[0m")
                        continue
                    arg = parts[1].lower()
                    if arg in ["all", "0"]:
                        current_chat = None
                        print("\033[32m已切换为: 📢 局域网广播模式\033[0m")
                    else:
                        with service.lock:
                            plist = list(service.peers.values())
                        try:
                            num = int(arg)
                            if 1 <= num <= len(plist):
                                current_chat = plist[num - 1]
                                print(f"\033[35m已切换私聊对象为: {current_chat.nick} ({current_chat.ip})\033[0m")
                            else:
                                print("\033[31m无效的编号\033[0m")
                        except ValueError:
                            found = False
                            for p in plist:
                                if p.nick.lower() == arg or p.ip == arg:
                                    current_chat = p
                                    found = True
                                    print(f"\033[35m已切换私聊对象为: {current_chat.nick} ({current_chat.ip})\033[0m")
                                    break
                            if not found:
                                print(f"\033[31m未找到好友: {arg}\033[0m")

                elif cmd == "/send":
                    if len(parts) < 2:
                        print("\033[33m用法: /send <文件路径>\033[0m")
                        continue
                    fpath = parts[1]
                    if not current_chat:
                        with service.lock:
                            plist = list(service.peers.values())
                        if len(plist) == 1:
                            service.send_file(plist[0], fpath)
                        else:
                            print("\033[33m请先使用 /chat <编号> 选中接收人，或指定编号\033[0m")
                    else:
                        service.send_file(current_chat, fpath)

                elif cmd == "/add":
                    if len(parts) < 2:
                        print("\033[33m用法: /add <IP地址>\033[0m")
                        continue
                    tip = parts[1]
                    print(f"正在主动连接 {tip} ...")
                    service.try_tcp_handshake(tip)

                elif cmd in ["/refresh", "/scan"]:
                    print("正在广播并扫描局域网网段...")
                    service.broadcast_presence("HELLO")

                elif cmd == "/nick":
                    if len(parts) < 2:
                        print("\033[33m用法: /nick <新昵称>\033[0m")
                        continue
                    service.nickname = parts[1]
                    service.broadcast_presence("HELLO")
                    print(f"\033[32m昵称已更改为: {parts[1]}\033[0m")

                elif cmd in ["/quit", "/exit", "/q"]:
                    break
                else:
                    print(f"\033[31m未知指令: {cmd}\033[0m")

            else:
                if not current_chat:
                    with service.lock:
                        plist = list(service.peers.values())
                    t_str = time.strftime("%H:%M:%S")
                    print(f"\033[32m[{t_str}] 我 (广播): {line}\033[0m")
                    for p in plist:
                        threading.Thread(target=service.send_text_message, args=(p, line, True), daemon=True).start()
                else:
                    t_str = time.strftime("%H:%M:%S")
                    print(f"\033[35m[{t_str}] 我 -> {current_chat.nick} (私聊): {line}\033[0m")
                    target = current_chat
                    def _send_private():
                        ok = service.send_text_message(target, line, False)
                        if not ok:
                            print(f"\n\033[31m[x] 向 {target.nick} 发送私聊消息失败\033[0m")
                            print("\033[32m[LanChat] > \033[0m", end="", flush=True)
                    threading.Thread(target=_send_private, daemon=True).start()

        except KeyboardInterrupt:
            break
        except EOFError:
            log_message("WARN", "标准输入 (stdin) EOF，非交互终端。保持后台网络监听服务运行...")
            print("\n\033[33m[!] 标准输入已关闭。LanChat 网络服务将保持在后台运行 (Ctrl+C 退出)...\033[0m")
            try:
                while service.is_running:
                    time.sleep(1)
            except KeyboardInterrupt:
                break
            break

    print("\n正在安全退出...")
    log_message("INFO", "=== LanChat CLI 安全退出 ===")
    service.close()

# ==================== 主入口与模式探测 ====================
def main():
    log_message("INFO", f"=== LanChat 启动 (PID: {os.getpid()}) ===")
    service = LanChatService()
    service.start()
    log_message("INFO", f"网络服务就绪: IP={service.primary_ip}, TCP={service.tcp_port}, UDP={UDP_PORT}")

    force_cli = "--cli" in sys.argv or "-c" in sys.argv
    has_display = bool(os.environ.get("DISPLAY") or os.environ.get("WAYLAND_DISPLAY") or sys.platform == "win32")

    if HAS_TK and has_display and not force_cli:
        try:
            log_message("INFO", "正在启动 WeChat 微信暗黑风格图形界面 (Tkinter GUI)...")
            gui = WeChatGui(service)
            gui.run()
            return
        except Exception as ex:
            log_message("ERROR", f"启动图形界面失败: {ex}，自动降级至终端命令行模式")
            print(f"[!] 启动图形界面失败 ({ex})，正在降级为命令行模式...")

    if not HAS_TK and has_display and not force_cli and not sys.stdin.isatty():
        msg = "LanChat 启动微信界面需要 python3-tk 组件。\n请在终端执行以下命令进行安装：\n\nsudo apt install python3-tk"
        try:
            import subprocess, shutil
            if shutil.which("zenity"):
                subprocess.Popen(["zenity", "--error", "--title=LanChat 提示", f"--text={msg}", "--width=320"])
            elif shutil.which("notify-send"):
                subprocess.Popen(["notify-send", "LanChat 提示", "缺少 python3-tk 组件，请安装: sudo apt install python3-tk"])
        except Exception:
            pass
        log_message("ERROR", "未检测到 tkinter (python3-tk)，且处于无终端交互环境，已弹出提示并退出")
        return

    run_cli(service)

if __name__ == "__main__":
    main()
