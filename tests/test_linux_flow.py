"""在临时目录和受控连接上回归 Linux 客户端主流程。"""

import contextlib
from concurrent.futures import ThreadPoolExecutor
import hashlib
import importlib
import io
import ipaddress
import os
from pathlib import Path
import queue
import shutil
import socket
import struct
import subprocess
import sys
import tempfile
import threading
import unittest
from types import SimpleNamespace
from unittest.mock import Mock, patch


class FragmentedConnection:
    """模拟 TCP 按任意边界交付字节，同时保留接收方回执。"""

    def __init__(self, *fragments):
        self.fragments = list(fragments)
        self.replies = bytearray()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()

    def close(self):
        pass

    def setsockopt(self, *_):
        pass

    def settimeout(self, *_):
        pass

    def connect(self, *_):
        pass

    def recv(self, count):
        if not self.fragments:
            return b""
        chunk, self.fragments[0] = self.fragments[0][:count], self.fragments[0][count:]
        if not self.fragments[0]:
            self.fragments.pop(0)
        return chunk

    def sendall(self, data):
        self.replies.extend(data)


class LinuxFlow(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        cls.addClassCleanup(cls.tmp.cleanup)
        path = Path(cls.tmp.name)
        source = Path(__file__).resolve().parents[1] / "src" / "linux" / "lanchat_app"
        shutil.copytree(source, path / "lanchat_app", ignore=shutil.ignore_patterns("__pycache__"))
        cls.enterClassContext(patch.dict(os.environ, {"USERPROFILE": str(path), "HOME": str(path)}))
        cls.enterClassContext(patch.object(sys, "path", [str(path), *sys.path]))
        cls.app = importlib.import_module("lanchat_app.network")
        importlib.import_module("lanchat_app.storage").LOG_FILE_TMP = str(path / "fallback.log")

    def setUp(self):
        capture = contextlib.redirect_stdout(io.StringIO())
        capture.__enter__()
        self.addCleanup(capture.__exit__, None, None, None)
        self.service = self.app.LanChatService("本机")
        directory = tempfile.TemporaryDirectory(dir=self.tmp.name)
        self.addCleanup(directory.cleanup)
        self.service.save_dir = directory.name
        self.service.auto_accept = True

    def gui(self, key):
        try:
            from lanchat_app.gui import WeChatGui
        except ImportError:
            self.skipTest("环境缺少 Tk")
        gui = WeChatGui.__new__(WeChatGui)
        from lanchat_app.history import HistoryStore
        from lanchat_app.sessions import SessionState
        gui.service = self.service
        gui.current_conv_key = key
        gui.sessions = SessionState(HistoryStore(Path(self.service.save_dir) / "ChatHistory"))
        gui.txt_history = Mock()
        gui.txt_input = Mock()
        gui.event_queue = queue.Queue()
        gui._render_sessions = Mock()
        return gui

    def text(self, content="中文短读"):
        return b"".join(self.app.write_dotnet_string(s) for s in ("peer", "同名", "local", content))

    def file_header(self, size):
        strings = b"".join(self.app.write_dotnet_string(s) for s in ("transfer", "peer", "同名", "例.txt"))
        return b"LANC\x02" + strings + struct.pack("<q", size)

    def test_initial_group_redraw(self):
        gui = self.gui("BROADCAST")
        gui.sessions.histories["BROADCAST"].append({"is_sys": True, "text": "节点上线"})
        gui._redraw_chat_history()
        gui.txt_history.configure.assert_called_with(state="disabled")

    def test_render_failure_keeps_history_readonly(self):
        gui = self.gui("BROADCAST")
        gui.sessions.histories["BROADCAST"].append({})
        gui._render_message_item = Mock(side_effect=RuntimeError("模拟渲染故障"))
        with self.assertRaises(RuntimeError):
            gui._redraw_chat_history()
        gui.txt_history.configure.assert_called_with(state="disabled")

    def test_offline_private_draft(self):
        gui = self.gui("offline")
        gui.txt_input.get.return_value = "待发送"
        gui._update_send_btn_state = Mock()
        gui._send_message()
        gui.txt_input.delete.assert_not_called()

    def test_tcp_text_keeps_advertised_port_and_host(self):
        self.service._process_udp_packet("ACK|peer|旧昵称|workstation|41907|127.0.0.1", ("127.0.0.1", 0))
        conn = FragmentedConnection(b"LANC\x01\x00" + self.text())
        self.service._handle_tcp_client(conn, ("127.0.0.1", 53001))
        peer = self.service.peers["peer"]
        self.assertEqual((peer.port, peer.host, peer.nick), (41907, "workstation", "同名"))
        self.assertEqual(conn.replies, b"\x01")

    def test_short_magic_and_truncated_text(self):
        self.service.on_message_received = Mock()
        conn = FragmentedConnection(b"LA", b"NC\x01\x00", self.text()[:-2], self.text()[-2:])
        self.service._handle_tcp_client(conn, ("127.0.0.1", 0))
        self.assertEqual(conn.replies, b"\x01")
        cut = FragmentedConnection(b"LANC\x01\x00", self.text()[:-2])
        self.service._handle_tcp_client(cut, ("127.0.0.1", 0))
        self.assertFalse(cut.replies)
        self.service.on_message_received.assert_called_once()

    def test_short_file_size_and_ack(self):
        self.service.on_file_received = Mock()
        header = self.file_header(3)
        conn = FragmentedConnection(header[:-6], header[-6:], b"abc")
        self.service._handle_tcp_client(conn, ("127.0.0.1", 0))
        self.assertEqual(conn.replies, b"\x01\x01")
        self.assertEqual((Path(self.service.save_dir) / "例.txt").read_bytes(), b"abc")

    def test_file_same_nickname_uses_sender_id(self):
        gui = self.gui("BROADCAST")
        gui._append_message = Mock()
        gui.progress_frame = Mock()
        self.service.add_or_update_peer("other", "同名", "other", "127.0.0.2", 41908)
        self.service.add_or_update_peer("peer", "同名", "peer", "127.0.0.1", 41907)
        gui._wire_callbacks()
        self.service._handle_tcp_client(FragmentedConnection(self.file_header(0)), ("127.0.0.1", 0))
        event, data = gui.event_queue.get_nowait()
        self.assertEqual(event, "file_recv")
        gui._handle_file_recv(*data)
        args, details = gui._append_message.call_args
        self.assertEqual(args[0], gui.sessions.key_for("peer", "127.0.0.1", False))
        self.assertEqual((details["sender_id"], details["remote_address"]), ("peer", "127.0.0.1"))

    def test_file_without_final_ack_is_failure(self):
        with tempfile.NamedTemporaryFile(dir=self.service.save_dir, delete=False) as payload:
            payload.write(b"abc")
        peer = self.app.PeerInfo("peer", "同名", "peer", "127.0.0.1", 41907)
        self.service.on_file_progress = Mock()
        self.service.on_file_result = Mock()
        conn = FragmentedConnection(b"\x01")
        with patch.object(self.app.socket, "socket", return_value=conn), patch.object(
            self.app.threading, "Thread", side_effect=lambda target, **_: SimpleNamespace(start=target)
        ):
            self.service.send_file(peer, payload.name)
        self.service.on_file_result.assert_called_once()
        self.assertIn("确认", self.service.on_file_result.call_args.args[-1])

    def test_file_rejection_and_final_ack_have_distinct_results(self):
        with tempfile.NamedTemporaryFile(dir=self.service.save_dir, delete=False) as payload:
            payload.write(b"abc")
        peer = self.app.PeerInfo("peer", "同名", "peer", "127.0.0.1", 41907)
        self.service.on_file_result = Mock()
        with patch.object(self.app.threading, "Thread", side_effect=lambda target, **_: SimpleNamespace(start=target)):
            for operation, answer in (("rejected", FragmentedConnection(b"\x00")),
                                      ("accepted", FragmentedConnection(b"\x01", b"\x01"))):
                with patch.object(self.app.socket, "socket", return_value=answer):
                    self.service.send_file(peer, payload.name, operation)
        self.assertEqual([entry.args for entry in self.service.on_file_result.call_args_list],
                         [("rejected", False, "对方拒收"), ("accepted", True, "已接收并保存")])

    def test_file_final_ack_timeout_is_failure(self):
        with tempfile.NamedTemporaryFile(dir=self.service.save_dir, delete=False) as payload:
            payload.write(b"abc")
        conn = FragmentedConnection()
        conn.recv = Mock(side_effect=[b"\x01", socket.timeout()])
        self.service.on_file_result = Mock()
        peer = self.app.PeerInfo("peer", "同名", "peer", "127.0.0.1", 41907)
        with patch.object(self.app.socket, "socket", return_value=conn), patch.object(
            self.app.threading, "Thread", side_effect=lambda target, **_: SimpleNamespace(start=target)
        ):
            self.service.send_file(peer, payload.name, "timeout")
        self.service.on_file_result.assert_called_once_with("timeout", False, "等待落盘确认超时")

    def test_interrupted_and_parallel_same_file_name(self):
        header = self.file_header(3)
        self.service.auto_accept = False
        rejected = FragmentedConnection(header)
        self.service._handle_tcp_client(rejected, ("127.0.0.1", 0))
        self.assertEqual(rejected.replies, b"\x00")
        self.service.auto_accept = True
        interrupted = FragmentedConnection(header, b"ab")
        self.service._handle_tcp_client(interrupted, ("127.0.0.1", 0))
        self.assertEqual(interrupted.replies, b"\x01")
        self.assertEqual(list(Path(self.service.save_dir).iterdir()), [])
        transfers = [FragmentedConnection(header, data) for data in (b"abc", b"xyz")]
        with ThreadPoolExecutor(2) as pool:
            list(pool.map(lambda conn: self.service._handle_tcp_client(conn, ("127.0.0.1", 0)), transfers))
        self.assertTrue(all(conn.replies == b"\x01\x01" for conn in transfers))
        self.assertEqual({p.read_bytes() for p in Path(self.service.save_dir).iterdir()}, {b"abc", b"xyz"})

    def test_udp_source_selection_and_port_validation(self):
        self.service._local_interfaces = [ipaddress.IPv4Interface("192.168.48.7/23")]
        for source, advertised, known, expected, confirmed in (
            ("192.168.49.2", "10.0.0.1", True, "192.168.49.2", True),
            ("198.18.0.2", "192.168.49.2", True, "192.168.49.2", True),
            ("198.18.0.2", "192.168.49.2", False, "198.18.0.2", False),
            ("127.0.0.2", "192.168.49.2", False, "192.168.49.2", True),
            ("198.18.0.2", "224.1.1.1", True, "198.18.0.2", False),
        ):
            with self.subTest(source=source, advertised=advertised, known=known):
                self.service.peers.clear()
                self.service._subnets_known = known
                self.service._process_udp_packet(f"ACK|peer|昵称|host|41908|{advertised}", (source, 0))
                peer = self.service.peers["peer"]
                self.assertEqual((peer.ip, peer.address_confirmed), (expected, confirmed))
        for port in ("0", "-1", "65536", "bad"):
            self.service._process_udp_packet(f"ACK|invalid|昵称|host|{port}|192.168.49.2", ("192.168.49.2", 0))
        self.assertNotIn("invalid", self.service.peers)

    def test_physical_subnet_without_default_route_ignores_tun(self):
        fcntl = Mock()
        def ioctl(_sock, code, request):
            self.assertEqual(request.split(b"\x00")[0], b"eth0")
            fields = bytearray(32)
            if code == 0x8913:
                struct.pack_into("H", fields, 16, 1)
            else:
                fields[20:24] = socket.inet_aton("192.168.48.7" if code == 0x8915 else "255.255.254.0")
            return bytes(fields)
        fcntl.ioctl.side_effect = ioctl
        with patch.object(self.app.sys, "platform", "linux"), patch.dict(sys.modules, {"fcntl": fcntl}), patch.object(
            self.app.socket, "if_nameindex", return_value=[(1, "eth0"), (2, "tun0"), (3, "veth0")]
        ), patch.object(self.app.os.path, "exists", side_effect=lambda p: "veth0" not in p), patch.object(
            self.app.socket, "socket"
        ) as make_socket:
            interfaces, known = self.app._physical_ipv4_interfaces()
            self.assertTrue(known)
            self.assertEqual(interfaces, [ipaddress.IPv4Interface("192.168.48.7/23")])
            self.service._local_interfaces = interfaces
            self.assertEqual(self.service.detect_primary_ip(), "192.168.48.7")
            self.assertTrue(self.service.is_local_source("192.168.49.2"))
            make_socket.assert_called_once()

    def test_direct_probe_adopts_nondefault_announced_port(self):
        reply = queue.Queue()
        self.service.udp_sock = Mock()
        self.service.udp_sock.sendto.side_effect = lambda *_: self.service._process_udp_packet(
            "ACK|peer|节点|workstation|41907", ("127.0.0.1", 0)
        )
        self.service.send_text_message = Mock(return_value=True)
        self.service.try_tcp_handshake("127.0.0.1", callback=lambda *data: reply.put(data))
        self.assertEqual(reply.get(timeout=3), (True, "已连接 127.0.0.1:41907"))
        self.assertEqual(self.service.send_text_message.call_args.args[0].port, 41907)
        self.assertEqual(list(self.service.peers), ["peer"])

    def test_group_file_choices(self):
        gui = self.gui("BROADCAST")
        gui._append_system_message = Mock()
        gui._choose_file = Mock()
        gui.overlay = Mock()
        gui._on_send_file()
        gui._append_system_message.assert_called_once()
        self.service.add_or_update_peer("first", "同名", "host", "127.0.0.2", 41908)
        gui._on_send_file()
        gui._choose_file.assert_called_once_with("BROADCAST", "first")
        self.service.add_or_update_peer("second", "同名", "host", "127.0.0.1", 41907)
        gui._on_send_file()
        actions = gui.overlay.show.call_args.args[1]
        second = next(action for label, action in actions if "127.0.0.1:41907" in label)
        second()
        gui._choose_file.assert_called_with("BROADCAST", "second")

    def test_file_progress_and_terminal_state_keep_original_session(self):
        gui = self.gui("peer")
        gui._redraw_chat_history = Mock()
        first, second = {"transient": True}, {"transient": True}
        gui.sessions.pending = {"first": ("BROADCAST", first), "second": ("peer", second)}
        gui._handle_file_prog("first", 5, 10, 1)
        gui._finish_operation("second", False, "对方拒收")
        gui._finish_operation("first", True, "已接收并保存")
        self.assertEqual((first["status"], first["delivery_state"]), ("已接收并保存", "delivered"))
        self.assertEqual((second["status"], second["delivery_state"]), ("对方拒收", "failed"))
        gui._redraw_chat_history.assert_called_once()
        self.assertEqual(gui.sessions.pending, {})

    def test_windows_native_csharp_roundtrip_nondefault_ports(self):
        if os.name != "nt":
            self.skipTest("需要 Windows 原生 C# 编译器")
        csc = Path(os.environ["WINDIR"]) / "Microsoft.NET/Framework64/v4.0.30319/csc.exe"
        if not csc.is_file():
            self.skipTest("未找到 Windows 原生 C# 编译器")
        repo = Path(__file__).resolve().parents[1]
        src = Path(os.environ.get("LANCHAT_CSHARP_FIXTURE_SOURCE", repo / "src"))
        exe = Path(self.service.save_dir) / "loopback.exe"
        subprocess.run([str(csc), "/nologo", f"/out:{exe}", str(repo / "tests/LinuxLoopback.cs"),
                        str(src / "Models.cs"), *map(str, src.glob("NetworkService*.cs"))], check=True)

        listener = socket.socket()
        listener.settimeout(0.25)
        listener.bind(("127.0.0.1", 0))
        listener.listen()
        self.addCleanup(self.service.close)
        self.service.tcp_sock = listener
        python_port = listener.getsockname()[1]
        self.assertNotEqual(python_port, self.app.TCP_BASE_PORT)
        incoming = queue.Queue()
        files = queue.Queue()
        results = queue.Queue()
        self.service.on_message_received = lambda *data: incoming.put(data)
        self.service.on_file_received = lambda *data: files.put(data)
        self.service.on_file_result = lambda *data: results.put(data)
        csharp_received = Path(self.service.save_dir) / "csharp"
        csharp_received.mkdir()
        payload = Path(self.service.save_dir) / "文件.bin"
        payload.write_bytes((bytes(range(251)) * 1240)[:311071])
        threading.Thread(target=self.service._tcp_listen_loop, daemon=True).start()
        with subprocess.Popen([str(exe), self.service.local_id, str(python_port), str(csharp_received)],
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8") as proc:
            try:
                peer_id, csharp_port = proc.stdout.readline().strip().split("|")
                self.assertNotEqual(int(csharp_port), self.app.TCP_BASE_PORT)
                self.service._process_udp_packet(f"ACK|{peer_id}|旧昵称|CSharp-Host|{csharp_port}", ("127.0.0.1", 0))
                self.assertTrue(self.service.send_text_message(self.service.peers[peer_id], "Python 中文", False))
                try:
                    self.assertEqual(incoming.get(timeout=3)[:2], ("CSharp", "CSharp 中文"))
                except queue.Empty:
                    _, stderr = proc.communicate(timeout=1)
                    self.fail(stderr or "没有收到 C# 回复")
                peer = self.service.peers[peer_id]
                self.assertEqual((peer.port, peer.host), (int(csharp_port), "CSharp-Host"))
                self.service.send_file(peer, str(payload), "file-roundtrip")
                self.assertEqual(results.get(timeout=10), ("file-roundtrip", True, "已接收并保存"))
                (sender, file_name, received_path, file_size, sender_id, source) = files.get(timeout=10)
                self.assertEqual((sender, file_size, sender_id, source), ("CSharp", 311071, peer_id, "127.0.0.1"))
                self.assertEqual(file_name, payload.name)
                for path in (Path(received_path), csharp_received / payload.name):
                    self.assertEqual(hashlib.sha256(path.read_bytes()).digest(), hashlib.sha256(payload.read_bytes()).digest())
                _, stderr = proc.communicate(timeout=6)
                self.assertEqual(proc.returncode, 0, stderr)
            finally:
                if proc.poll() is None:
                    proc.kill()

    def test_windows_history_jsonl_roundtrip(self):
        if os.name != "nt":
            self.skipTest("需要 Windows 原生 C# 编译器")
        csc = Path(os.environ["WINDIR"]) / "Microsoft.NET/Framework64/v4.0.30319/csc.exe"
        if not csc.is_file():
            self.skipTest("未找到 Windows 原生 C# 编译器")
        from lanchat_app.history import HistoryStore

        repo = Path(__file__).resolve().parents[1]
        root = Path(self.service.save_dir)
        store = HistoryStore(root / "ChatHistory")
        key = "LAN-192.168.40.2"
        payload = '中文"文件"\\路径\n下一行\r\t\b\f\x01'
        file_path = "C:\\接收\\文件.txt"
        store.append(key, {"text": payload, "sender": "节点甲", "remote_address": "192.168.40.2",
                           "timestamp": "2026-11-19T12:34:56.123", "is_file": True, "file_size": 312,
                           "local_path": file_path})
        raw = (store.directory / f"history_peer_{key}.jsonl").read_bytes()
        self.assertIn("中文".encode(), raw)
        self.assertIn(b"\x08\x0c\x01", raw)
        self.assertNotIn(b"\\u", raw)
        exe = root / "history-interop.exe"
        subprocess.run([str(csc), "/nologo", f"/out:{exe}", str(repo / "tests/LinuxHistoryInterop.cs"),
                        str(repo / "src/Models.cs"), *[str(repo / "src/Services" / (file + ".cs")) for file in
                                                      ("AppSettings", "SessionManager", "ChatHistoryService")]],
                       check=True)
        subprocess.run([str(exe), str(store.directory)], check=True, timeout=10)
        rows = store.load()
        self.assertEqual([row["text"] for row in rows[key]], [payload, payload])
        self.assertEqual(rows[key][1]["timestamp"], "2026-11-19T12:34:57.456")
        self.assertEqual(rows[key][1]["local_path"], file_path)
        self.assertEqual(rows["BROADCAST"][0]["text"], "旧群聊条目")
        self.assertIsNone(rows["BROADCAST"][0]["remote_address"])

    def test_history_interrupted_pending_and_clear(self):
        from lanchat_app.history import HistoryStore
        directory = Path(self.service.save_dir) / "ChatHistory"
        store = HistoryStore(directory)
        a = {"text": "传输", "timestamp": "2026-11-19T12:00:00.100", "operation_id": "pending", "is_self": True}
        store.append("BROADCAST", a)
        with (directory / "history_BROADCAST.jsonl").open("ab") as stream:
            stream.write(b'{"Content": "incomplete\xe4')
        b = {"text": "后来", "timestamp": "2026-11-19T12:00:01.200"}
        store.append("BROADCAST", b)
        rows = HistoryStore(directory).load()["BROADCAST"]
        self.assertEqual([row["text"] for row in rows], ["传输", "后来"])
        self.assertIn("未确认", rows[0]["status"])
        a.update(delivery_state="failed", status="对方拒收")
        store.complete("BROADCAST", a)
        rows = store.load()["BROADCAST"]
        self.assertEqual(len(rows), 2)
        self.assertEqual(rows[0]["status"], "对方拒收")
        store.clear("BROADCAST")
        self.assertEqual(store.load()["BROADCAST"], [])

    def test_history_keeps_unconfirmed_addresses_and_reconciles_same_ip(self):
        from lanchat_app.history import HistoryStore
        from lanchat_app.sessions import SessionState
        store = HistoryStore(Path(self.service.save_dir) / "ChatHistory")
        state = SessionState(store)
        route = self.app.PeerInfo("peer", "同名", "host", "192.168.40.2", 41907, True, True)
        other = self.app.PeerInfo("other", "同名", "host", "192.168.40.3", 41908, True, True)
        direct = state.key_for("DIRECT-192.168.40.2", "192.168.40.2", False)
        uncertain = state.key_for("peer", "192.168.99.2", False)
        item = {"text": "同地址", "timestamp": "2026-11-19T12:00:00.100", "sender": "原档案"}
        for key in (direct, uncertain):
            store.append(key, item)
        state = SessionState(store)
        state.reconcile(route)
        state.reconcile(route)
        stable = "LAN-192.168.40.2"
        self.assertNotIn(direct, state.histories)
        self.assertIn(uncertain, state.histories)
        self.assertEqual(len(store.load()[stable]), 1)
        self.assertIn("LAN-192.168.40.3", state.active_routes([route, other]))
        new = self.app.PeerInfo("new", "新实例", "host", route.ip, 41999, True, True)
        self.assertEqual(state.route(stable, [route, new]).port, 41999)
        self.assertEqual(next(entry for entry in state.list_sessions([]) if entry.key == stable).nick, "原档案")

    def test_history_installed_and_portable_paths(self):
        from lanchat_app import history, storage
        root = Path(self.service.save_dir)
        portable = root / "portable"
        portable.mkdir()
        (portable / "settings.json").write_text('{"nickname":"便携","auto_accept":false}', encoding="utf-8")
        with patch.object(storage, "application_directory", return_value=portable), patch.object(
            history, "application_directory", return_value=portable
        ), patch.object(history.Path, "home", return_value=root):
            self.assertEqual(storage.load_settings()["nickname"], "便携")
            self.assertEqual(history.HistoryStore().directory, portable / "ChatHistory")
            (portable / "ChatHistory").rmdir()
            (portable / "ChatHistory").touch()
            self.assertEqual(history.HistoryStore().directory, root / ".local/share/lanchat/ChatHistory")
        with patch.object(storage, "is_installed", return_value=True), patch.object(
            history, "is_installed", return_value=True
        ), patch.object(history.Path, "home", return_value=root):
            self.assertEqual(history.HistoryStore().directory, root / ".local/share/lanchat/ChatHistory")
            self.assertEqual(Path(storage.get_settings_file_path()).name, "settings.json")

    def test_window_launch_restores_saved_history(self):
        try:
            from lanchat_app.gui import WeChatGui, tk
        except ImportError:
            self.skipTest("环境缺少 Tk")
        from lanchat_app.history import HistoryStore
        directory = Path(self.service.save_dir) / "ChatHistory"
        store = HistoryStore(directory)
        store.append("BROADCAST", {"text": "已保存", "timestamp": "2026-11-19T12:00:00.100", "is_sys": True})
        self.service.auto_accept = False
        try:
            with patch("lanchat_app.sessions.HistoryStore", return_value=store):
                gui = WeChatGui(self.service)
        except tk.TclError as exc:
            self.skipTest(f"没有可用的图形会话：{exc}")
        self.addCleanup(gui._on_close)
        gui.root.update()
        self.assertEqual(gui.btn_auto_accept.cget("text"), "自动接收: 关")
        self.assertEqual(gui.txt_history.cget("state"), "disabled")
        self.assertIn("已保存", gui.txt_history.get("1.0", "end"))
        gui.txt_input.focus_force()
        gui.overlay.show("选择节点", [("取消", lambda: None)])
        gui.root.update()
        self.assertIsNotNone(gui.overlay.panel)
        gui.root.focus_get().event_generate("<Escape>")
        gui.root.update()
        self.assertIsNone(gui.overlay.panel)
        self.assertEqual(gui.root.focus_get(), gui.txt_input)


if __name__ == "__main__":
    unittest.main()
