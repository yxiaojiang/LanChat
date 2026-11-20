"""局域网节点发现与 LANC TCP 传输。服务回调由调用方调度至所需线程。"""

import ipaddress
import os
import socket
import struct
import sys
import tempfile
import threading
import time
import uuid

from .storage import determine_save_dir, load_settings, log_message, save_settings


UDP_PORT = 38888
TCP_BASE_PORT = 38889
MAGIC = b"LANC"
HANDSHAKE_MSG = "[系统自动握手]"


class PeerInfo:
    """在线节点的路由和显示信息，由网络服务维护，随发现和失联更新。"""

    def __init__(self, peer_id, nick, host, ip, port, announced=False, confirmed=False):
        self.id = peer_id
        self.nick = nick or "未知好友"
        self.host = host or "Linux-Host"
        self.ip = ip
        self.port = port
        self.announced = announced
        self.address_confirmed = confirmed
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

def _read_exact(sock, count):
    data = bytearray()
    while len(data) < count:
        chunk = sock.recv(min(count - len(data), 65536))
        if not chunk:
            raise EOFError("TCP 帧提前结束")
        data.extend(chunk)
    return data


def read_dotnet_string(sock) -> str:
    """从连接读取一个 .NET UTF-8 字符串，长度无效或提前 EOF 时抛出异常。"""
    length = 0
    for shift in range(0, 35, 7):
        b = _read_exact(sock, 1)[0]
        if shift == 28 and b > 7:
            raise ValueError("无效的 .NET 字符串长度")
        length |= (b & 0x7F) << shift
        if (b & 0x80) == 0:
            return _read_exact(sock, length).decode("utf-8", errors="replace")


def _usable_ipv4(value):
    try:
        address = ipaddress.IPv4Address(value)
    except ValueError:
        return None
    if address.is_unspecified or address.is_multicast or address.is_reserved:
        return None
    return address


def _physical_ipv4_interfaces():
    # 内核接口表不依赖默认路由。sysfs 设备链用于排除 TUN、网桥和容器虚拟接口。
    if not sys.platform.startswith("linux"):
        return [], False
    import fcntl

    interfaces = []
    complete = True
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
            for _, name in socket.if_nameindex():
                if not os.path.exists(os.path.join("/sys/class/net", name, "device")):
                    continue
                if any(word in name.lower() for word in ("tun", "tap", "vpn", "virtual")):
                    continue
                request = struct.pack("256s", os.fsencode(name))
                try:
                    flags = struct.unpack_from("H", fcntl.ioctl(sock, 0x8913, request), 16)[0]
                except OSError:
                    complete = False
                    continue
                if not flags & 1:  # 跳过管理状态为关闭的接口（IFF_UP）。
                    continue
                try:
                    # SIOCGIFADDR 和 SIOCGIFNETMASK 的 IPv4 sockaddr 位于 ifreq 的首个联合成员。
                    raw_ip = fcntl.ioctl(sock, 0x8915, request)[20:24]
                except OSError:
                    continue
                try:
                    raw_mask = fcntl.ioctl(sock, 0x891B, request)[20:24]
                    mask = socket.inet_ntoa(raw_mask)
                    if mask == "0.0.0.0":
                        raise ValueError("接口未配置子网掩码")
                    interfaces.append(ipaddress.IPv4Interface(f"{socket.inet_ntoa(raw_ip)}/{mask}"))
                except (OSError, ValueError):
                    complete = False
    except OSError:
        complete = False
    return interfaces, complete and bool(interfaces)

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
    """管理局域网发现、在线节点与 LANC 收发。start 启动后台监听，close 停止监听。"""

    def __init__(self, nickname: str = None):
        self.local_id = uuid.uuid4().hex
        settings = load_settings()
        saved_nick = settings.get("NickName") or settings.get("nickname")
        self.nickname = nickname or saved_nick or os.environ.get("USER", "LinuxUser")
        self.hostname = socket.gethostname()
        self.tcp_port = TCP_BASE_PORT
        self.save_dir = determine_save_dir()
        log_message("INFO", f"LanChatService 初始化: 昵称={self.nickname}, 主机={self.hostname}, 存储目录={self.save_dir}")

        self.peers = {} # id -> PeerInfo
        self.lock = threading.Lock()
        self._peer_discovered = threading.Condition(self.lock)
        self._file_commit_lock = threading.Lock()
        self.is_running = True

        self._local_interfaces, self._subnets_known = _physical_ipv4_interfaces()
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
        self.on_file_result = None
        self.auto_accept = settings.get("AutoAcceptFiles", settings.get("auto_accept", True))

    def set_nickname(self, new_nick: str):
        if not new_nick: return
        self.nickname = new_nick.strip()
        settings = load_settings()
        settings["NickName"] = self.nickname
        save_settings(settings)

    def set_auto_accept(self, val: bool):
        self.auto_accept = bool(val)
        settings = load_settings()
        settings["AutoAcceptFiles"] = self.auto_accept
        save_settings(settings)

    def detect_primary_ip(self) -> str:
        """返回公告用的 IPv4，Linux 优先选已枚举的物理接口。"""
        if self._local_interfaces:
            return str(self._local_interfaces[0].ip)
        if sys.platform.startswith("linux"):
            return "127.0.0.1"
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
                s.connect(("8.8.8.8", 80))
                return s.getsockname()[0]
        except Exception:
            return "127.0.0.1"

    def is_local_source(self, ip):
        """判断连接源 IPv4 是否落在已知物理接口子网内。"""
        address = _usable_ipv4(ip)
        return address is not None and any(address in iface.network for iface in self._local_interfaces)

    def start(self):
        # 逐端口尝试，绑定失败的套接字不交给监听线程。
        for port in range(TCP_BASE_PORT, TCP_BASE_PORT + 50):
            listener = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            try:
                if os.name == "nt":
                    listener.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
                else:
                    listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
                listener.bind(("0.0.0.0", port))
                listener.listen(10)
                self.tcp_sock = listener
                self.tcp_port = listener.getsockname()[1]
                break
            except OSError:
                listener.close()
        else:
            raise OSError(f"无法绑定 TCP 监听端口 {TCP_BASE_PORT}–{TCP_BASE_PORT + 49}")

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
                client.settimeout(15.0)
                magic = _read_exact(client, len(MAGIC))
                if magic != MAGIC: return

                packet_type = _read_exact(client, 1)[0]

                if packet_type == 1: # Text message
                    is_broadcast = _read_exact(client, 1)[0] == 1
                    sender_id = read_dotnet_string(client)
                    sender_nick = read_dotnet_string(client)
                    target_id = read_dotnet_string(client)
                    content = read_dotnet_string(client)

                    # 文字帧没有公告端口和主机名。连接地址另随消息交给会话层。
                    self.add_or_update_peer(sender_id, sender_nick, "LAN-Host", addr[0], TCP_BASE_PORT,
                                            announced=False, confirmed=self.is_local_source(addr[0]))
                    client.sendall(b"\x01") # ACK

                    if content == HANDSHAKE_MSG:
                        return

                    tag = "\033[36m[📢 广播]\033[0m" if is_broadcast else "\033[35m[🔒 私聊]\033[0m"
                    t_str = time.strftime("%H:%M:%S")
                    print(f"\n{t_str} {tag} \033[1m{sender_nick}\033[0m: {content}")
                    print("\033[32m[LanChat] > \033[0m", end="", flush=True)

                    if self.on_message_received:
                        try:
                            self.on_message_received(sender_nick, content, is_broadcast, sender_id, time.time(), addr[0])
                        except Exception as ex:
                            log_message("ERROR", f"on_message_received 回调异常: {ex}")

                elif packet_type == 2: # File transfer offer
                    self._receive_file(client, addr[0])

        except (OSError, EOFError, ValueError) as ex:
            log_message("WARN", f"读取 {addr[0]} 的 TCP 帧失败: {ex}")

    def _receive_file(self, client, source_ip):
        read_dotnet_string(client)  # 保留线上的传输标识字段。
        sender_id = read_dotnet_string(client)
        sender_nick = read_dotnet_string(client)
        safe_name = read_dotnet_string(client).replace("\\", "/").rsplit("/", 1)[-1]
        file_size = struct.unpack("<q", _read_exact(client, 8))[0]
        if not self.auto_accept or file_size < 0 or safe_name in ("", ".", ".."):
            client.sendall(b"\x00")
            return

        temp_path = None
        try:
            # 每个连接有独立临时文件，完成时再在锁内分配最终文件名。
            with tempfile.NamedTemporaryFile(dir=self.save_dir, suffix=".downloading", delete=False) as output:
                temp_path = output.name
                client.sendall(b"\x01")
                remaining = file_size
                while remaining:
                    chunk = client.recv(min(65536, remaining))
                    if not chunk:
                        raise EOFError("文件未接收完整")
                    output.write(chunk)
                    remaining -= len(chunk)
            with self._file_commit_lock:
                local_path = get_unique_filepath(self.save_dir, safe_name)
                os.replace(temp_path, local_path)
                temp_path = None
        finally:
            if temp_path:
                os.unlink(temp_path)

        # 即使对方已断开，成功落盘的文件仍需记录到接收者会话。
        try:
            client.sendall(b"\x01")
        finally:
            if self.is_local_source(source_ip):
                self.add_or_update_peer(sender_id, sender_nick, "LAN-Host", source_ip, TCP_BASE_PORT,
                                        announced=False, confirmed=True)
            if self.on_file_received:
                self.on_file_received(sender_nick, safe_name, local_path, file_size, sender_id, source_ip)
            else:
                print(f"\n已接收 {sender_nick} 的文件: {safe_name} ({format_size(file_size)}) | {local_path}")

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

        if not peer_id or peer_id == self.local_id: return

        if cmd == "BYE":
            with self.lock:
                if peer_id in self.peers:
                    del self.peers[peer_id]
                    if self.on_peer_left:
                        try: self.on_peer_left(peer_id)
                        except Exception: pass
            return

        if cmd in ("HELLO", "ACK", "PING") and len(parts) >= 5:
            nick = parts[2]
            host = parts[3]
            try:
                port = int(parts[4])
            except ValueError:
                return
            source = _usable_ipv4(addr[0])
            if source is None or not 1 <= port <= 65535:
                return
            advert = _usable_ipv4(parts[5]) if len(parts) >= 6 and parts[5] else None
            interfaces, subnets_known = self._local_interfaces, self._subnets_known
            final_ip = source
            confirmed = any(source in iface.network for iface in interfaces)
            # 已知物理同网段优先；没有掩码证据的跨网段推断不得替换源地址。
            if not confirmed and (source.is_loopback or subnets_known):
                if advert is not None and not advert.is_loopback:
                    if not any(advert in (iface.network.network_address, iface.network.broadcast_address)
                               for iface in interfaces if iface.network.prefixlen < 31):
                        final_ip, confirmed = advert, True
            self.add_or_update_peer(peer_id, nick, host, str(final_ip), port, confirmed=confirmed)

            if cmd == "HELLO":
                payload = f"ACK|{self.local_id}|{self.nickname}|{self.hostname}|{self.tcp_port}|{self.primary_ip}".encode('utf-8')
                try: self.udp_sock.sendto(payload, (addr[0], UDP_PORT))
                except: pass

        elif cmd == "PING":
            with self.lock:
                if peer_id in self.peers:
                    self.peers[peer_id].last_seen = time.time()

    def add_or_update_peer(self, peer_id, nick, host, ip, port, announced=True, confirmed=False):
        """记录节点与来源可信度。文字只能更新昵称、心跳和临时路由。"""
        if not peer_id or peer_id == self.local_id: return
        with self._peer_discovered:
            if peer_id not in self.peers:
                peer = PeerInfo(peer_id, nick, host, ip, port, announced, confirmed)
                self.peers[peer_id] = peer
                print(f"\n\033[32m[+] 发现好友上线: {peer}\033[0m")
                print("\033[32m[LanChat] > \033[0m", end="", flush=True)
                if self.on_peer_joined:
                    try: self.on_peer_joined(peer)
                    except Exception: pass
            else:
                p = self.peers[peer_id]
                p.nick = nick or p.nick
                if announced:
                    p.host = host or p.host
                    p.port = port
                    p.announced = True
                if p.announced and not announced:
                    if p.ip == ip and confirmed:
                        p.address_confirmed = True
                elif confirmed or not p.address_confirmed:
                    p.ip = ip
                    p.address_confirmed = confirmed
                p.last_seen = time.time()
                if self.on_peer_updated:
                    try: self.on_peer_updated(p)
                    except Exception: pass
            self._peer_discovered.notify_all()

    def broadcast_presence(self, cmd="HELLO"):
        """广播在线状态；HELLO 重新读取物理接口并探测相邻节点。"""
        if cmd == "HELLO":
            self._local_interfaces, self._subnets_known = _physical_ipv4_interfaces()
            self.primary_ip = self.detect_primary_ip()
        payload = f"{cmd}|{self.local_id}|{self.nickname}|{self.hostname}|{self.tcp_port}|{self.primary_ip}".encode('utf-8')
        try: self.udp_sock.sendto(payload, ("255.255.255.255", UDP_PORT))
        except: pass

        if cmd == "HELLO":
            threading.Thread(target=self._sweep_subnet, args=(payload,), daemon=True).start()

    def _sweep_subnet(self, payload):
        sock = self.udp_sock
        if sock is None:
            return
        for iface in self._local_interfaces:
            try:
                sock.sendto(payload, (str(iface.network.broadcast_address), UDP_PORT))
            except OSError:
                continue
            # 大网段只广播，避免刷新时投递大量单播探测。
            if iface.network.prefixlen >= 24:
                for dst in iface.network.hosts():
                    if dst != iface.ip and self.is_running:
                        try:
                            sock.sendto(payload, (str(dst), UDP_PORT))
                        except OSError:
                            pass

    def try_tcp_handshake(self, target_ip, port=TCP_BASE_PORT, callback=None):
        """异步发起手动连接，回调接收连接结果与提示文字。"""
        def report(ok, detail):
            if callback:
                callback(ok, detail)
            else:
                print(f"\n{detail}")

        address = _usable_ipv4(target_ip)
        if address is None:
            report(False, f"无效的 IPv4 地址：{target_ip}")
            return

        def _worker():
            def advertised():
                return next((p for p in self.peers.values() if p.ip == target_ip and p.announced), None)

            # 主动发单播公告，让未发现的节点也有机会返回自己的非默认监听端口。
            if self.udp_sock is not None:
                payload = f"HELLO|{self.local_id}|{self.nickname}|{self.hostname}|{self.tcp_port}|{self.primary_ip}"
                try:
                    self.udp_sock.sendto(payload.encode("utf-8"), (target_ip, UDP_PORT))
                    with self._peer_discovered:
                        self._peer_discovered.wait_for(advertised, timeout=0.35)
                except OSError:
                    pass
            with self.lock:
                peer = advertised()
            endpoint = peer or PeerInfo(f"DIRECT-{target_ip}", f"直连 {target_ip}", "Manual-Node", target_ip, port)
            if not self.send_text_message(endpoint, HANDSHAKE_MSG, False):
                report(False, f"连接 {target_ip}:{endpoint.port} 失败，请确认对方在线。")
                return
            with self.lock:
                discovered = advertised()
            if not discovered:
                self.add_or_update_peer(endpoint.id, endpoint.nick, endpoint.host, target_ip, endpoint.port,
                                        announced=False, confirmed=self.is_local_source(target_ip))
            report(True, f"已连接 {target_ip}:{endpoint.port}")
        threading.Thread(target=_worker, daemon=True).start()

    def send_text_message(self, peer: PeerInfo, text: str, is_broadcast: bool) -> bool:
        """通过 LANC 发送文字，收到 0x01 回执后返回成功。连接失败返回 False。"""
        try:
            with socket.create_connection((peer.ip, peer.port), timeout=4.0) as s:
                s.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                buf = bytearray(MAGIC)
                buf.append(1)  # 文字帧
                buf.append(1 if is_broadcast else 0)
                buf.extend(write_dotnet_string(self.local_id))
                buf.extend(write_dotnet_string(self.nickname))
                buf.extend(write_dotnet_string(peer.id if not is_broadcast else "ALL"))
                buf.extend(write_dotnet_string(text))
                s.sendall(buf)
                return s.recv(1) == b"\x01"
        except Exception as ex:
            log_message("WARN", f"发送消息给 {peer.nick} ({peer.ip}) 失败: {ex}")
            return False

    def send_file(self, peer: PeerInfo, file_path: str, transfer_id=None):
        """在后台分块发送文件，每个传输标识报告进度和一个终态。"""
        file_name = os.path.basename(file_path)
        transfer_id = transfer_id or uuid.uuid4().hex

        def report(ok, detail):
            if self.on_file_result:
                self.on_file_result(transfer_id, ok, detail)
            else:
                print(f"\n{file_name} → {peer.nick}：{detail}")

        def _do_send():
            phase = "打开文件"
            try:
                with open(file_path, "rb") as f:
                    file_size = os.fstat(f.fileno()).st_size
                    phase = "连接"
                    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                        s.settimeout(5.0)
                        s.connect((peer.ip, peer.port))
                        s.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                        buf = bytearray(MAGIC + b"\x02")
                        for field in (transfer_id, self.local_id, self.nickname, file_name):
                            buf.extend(write_dotnet_string(field))
                        buf.extend(struct.pack("<q", file_size))
                        s.sendall(buf)

                        s.settimeout(10.0)
                        phase = "等待接收方回应"
                        accept = s.recv(1)
                        if accept != b"\x01":
                            report(False, "对方拒收" if accept == b"\x00" else "未获得接收许可")
                            return

                        phase = "传输"
                        sent = 0
                        started = last_update = time.monotonic()
                        while sent < file_size:
                            chunk = f.read(min(65536, file_size - sent))
                            if not chunk:
                                raise EOFError("源文件在传输中缩短")
                            s.sendall(chunk)
                            sent += len(chunk)
                            now = time.monotonic()
                            if self.on_file_progress and (now - last_update >= 0.15 or sent == file_size):
                                speed = sent / max(now - started, 0.001) / (1024 * 1024)
                                self.on_file_progress(transfer_id, sent, file_size, speed)
                                last_update = now

                        phase = "等待落盘确认"
                        if s.recv(1) != b"\x01":
                            report(False, "未收到对方的落盘确认")
                            return
            except socket.timeout:
                report(False, f"{phase}超时")
                return
            except (OSError, EOFError, ValueError) as ex:
                log_message("ERROR", f"发送文件失败: {ex}")
                report(False, f"{phase}失败：{ex}")
                return
            report(True, "已接收并保存")

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
