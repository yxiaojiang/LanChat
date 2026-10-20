#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
LanChat for Linux (Universal x86 / x86_64 / ARM / aarch64 / MIPS)
100% Protocol Compatible with LanChat Windows Version.
Pure Python standard library - ZERO dependencies required!
"""

import sys
import os
import socket
import threading
import time
import uuid
import struct
import glob

UDP_PORT = 38888
TCP_BASE_PORT = 38889
MAGIC = b"LANC"
HANDSHAKE_MSG = "[系统自动握手]"

class PeerInfo:
    def __init__(self, peer_id, nick, host, ip, port):
        self.id = peer_id
        self.nick = nick or "未知好友"
        self.host = host or "Linux-Host"
        self.ip = ip
        self.port = port
        self.last_seen = time.time()

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

class LanChatService:
    def __init__(self, nickname: str = None):
        self.local_id = uuid.uuid4().hex
        self.nickname = nickname or os.environ.get("USER", "LinuxUser")
        self.hostname = socket.gethostname()
        self.tcp_port = TCP_BASE_PORT
        self.save_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "ReceivedFiles"))
        os.makedirs(self.save_dir, exist_ok=True)

        self.peers = {} # id -> PeerInfo
        self.lock = threading.Lock()
        self.is_running = True

        self.primary_ip = self.detect_primary_ip()
        self.tcp_sock = None
        self.udp_sock = None

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
            print(f"[!] UDP 绑定警告: {e}")

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

                elif packet_type == 2: # File transfer offer
                    transfer_id = read_dotnet_string(client)
                    sender_id = read_dotnet_string(client)
                    sender_nick = read_dotnet_string(client)
                    file_name = read_dotnet_string(client)
                    file_size = struct.unpack("<q", client.recv(8))[0]

                    # Auto accept
                    client.sendall(b"\x01")

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
            else:
                self.peers[peer_id].nick = nick
                self.peers[peer_id].host = host
                self.peers[peer_id].ip = ip
                self.peers[peer_id].port = port
                self.peers[peer_id].last_seen = time.time()

    def broadcast_presence(self, cmd="HELLO"):
        payload = f"{cmd}|{self.local_id}|{self.nickname}|{self.hostname}|{self.tcp_port}|{self.primary_ip}".encode('utf-8')
        # 1. Global broadcast
        try: self.udp_sock.sendto(payload, ("255.255.255.255", UDP_PORT))
        except: pass

        # 2. Subnet broadcast and fast unicast sweep
        if cmd == "HELLO":
            threading.Thread(target=self._sweep_subnet, args=(payload,), daemon=True).start()

    def _sweep_subnet(self, payload):
        ip_parts = self.primary_ip.split('.')
        if len(ip_parts) == 4:
            base = f"{ip_parts[0]}.{ip_parts[1]}.{ip_parts[2]}."
            my_last = int(ip_parts[3])

            # Subnet broadcast
            try: self.udp_sock.sendto(payload, (base + "255", UDP_PORT))
            except: pass

            # Unicast UDP sweep
            for i in range(1, 255):
                if i == my_last: continue
                target_ip = base + str(i)
                try: self.udp_sock.sendto(payload, (target_ip, UDP_PORT))
                except: pass

            # Fast TCP Handshake probe to port 38889
            for i in range(1, 255):
                if i == my_last: continue
                target_ip = base + str(i)
                threading.Thread(target=self.try_tcp_handshake, args=(target_ip,), daemon=True).start()

    def try_tcp_handshake(self, target_ip):
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.settimeout(0.35)
            s.connect((target_ip, TCP_BASE_PORT))
            s.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)

            # Send Handshake message
            buf = bytearray(MAGIC)
            buf.append(1) # Text
            buf.append(1) # isBroadcast
            buf.extend(write_dotnet_string(self.local_id))
            buf.extend(write_dotnet_string(self.nickname))
            buf.extend(write_dotnet_string(""))
            buf.extend(write_dotnet_string(HANDSHAKE_MSG))
            s.sendall(buf)

            s.settimeout(1.2)
            ack = s.recv(1)
            if ack == b"\x01":
                self.add_or_update_peer(target_ip, f"好友 ({target_ip})", "LAN-PC", target_ip, TCP_BASE_PORT)
            s.close()
        except Exception:
            pass

    def send_text_message(self, peer: PeerInfo, text: str, is_broadcast=False) -> bool:
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.settimeout(2.0)
            s.connect((peer.ip, peer.port))
            s.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)

            buf = bytearray(MAGIC)
            buf.append(1) # Text
            buf.append(1 if is_broadcast else 0)
            buf.extend(write_dotnet_string(self.local_id))
            buf.extend(write_dotnet_string(self.nickname))
            buf.extend(write_dotnet_string("" if is_broadcast else peer.id))
            buf.extend(write_dotnet_string(text))
            s.sendall(buf)

            ack = s.recv(1)
            s.close()
            return ack == b"\x01"
        except Exception as e:
            return False

    def send_file(self, peer: PeerInfo, file_path: str):
        if not os.path.exists(file_path):
            print(f"\033[31m[x] 文件不存在: {file_path}\033[0m")
            return

        file_size = os.path.getsize(file_path)
        file_name = os.path.basename(file_path)
        transfer_id = uuid.uuid4().hex

        print(f"\n\033[36m正在向 [{peer.nick}] 发送文件: {file_name} ({format_size(file_size)}) ...\033[0m")

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
                    print(f"\n\033[31m[x] 对方拒绝或未自动接收文件\033[0m")
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
                        pct = int((sent_bytes * 100) / file_size) if file_size > 0 else 100
                        el = time.time() - t_start
                        spd = (sent_bytes / (el * 1024 * 1024)) if el > 0 else 0
                        sys.stdout.write(f"\r  -> 传输中: {pct}% ({format_size(sent_bytes)}/{format_size(file_size)}) - {spd:.1f} MB/s   ")
                        sys.stdout.flush()

                confirm = s.recv(1)
                s.close()
                print(f"\n\033[32m[✓] 文件 {file_name} 发送完成！\033[0m")
                print("\033[32m[LanChat] > \033[0m", end="", flush=True)

            except Exception as ex:
                print(f"\n\033[31m[x] 文件发送失败: {ex}\033[0m")
                print("\033[32m[LanChat] > \033[0m", end="", flush=True)

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

    def close(self):
        self.is_running = False
        payload = f"BYE|{self.local_id}".encode('utf-8')
        try: self.udp_sock.sendto(payload, ("255.255.255.255", UDP_PORT))
        except: pass
        try: self.tcp_sock.close()
        except: pass
        try: self.udp_sock.close()
        except: pass

def main():
    service = LanChatService()
    service.start()

    print("\n" + "="*70)
    print("      \033[33mLanChat for Linux (Universal Python Edition)\033[0m")
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
                    threading.Thread(target=service.try_tcp_handshake, args=(tip,), daemon=True).start()

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
                # Send text message
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

        except (KeyboardInterrupt, EOFError):
            break

    print("\n正在安全退出...")
    service.close()

if __name__ == "__main__":
    main()
