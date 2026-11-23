"""在仅含回环的 Linux 网络命名空间运行已打包的桌面服务回归。"""

from concurrent.futures import ThreadPoolExecutor
import hashlib
import itertools
import json
import os
from pathlib import Path
from queue import Queue
import socket
import struct
import subprocess
import sys
import tempfile
from threading import Barrier, Thread


# 在软件模拟器运行时可为启动、握手和大文件传输预留更多时间。
TIMEOUT_SCALE = float(os.environ.get("LANCHAT_TEST_TIMEOUT_SCALE", "1"))


def frames(stream, output):
    for line in stream:
        output.put(json.loads(line))
    output.put(None)


class Service:
    def __init__(self, executable, root, home):
        self.executable = executable
        self.env = dict(os.environ, HOME=str(home), LANCHAT_ROOT=str(root), LANCHAT_INSTALLED="1")

    def __enter__(self):
        self.process = subprocess.Popen(
            [self.executable, "--desktop-service"], env=self.env,
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True, encoding="utf-8",
        )
        self.events = Queue()
        self.ids = itertools.count()
        self.reader = Thread(target=frames, args=(self.process.stdout, self.events), daemon=True)
        self.reader.start()
        try:
            self.ready = self.wait(lambda row: row.get("event") == "ready")
        except Exception:
            self.process.kill()
            self.__exit__()
            raise
        return self

    def __exit__(self, *_exception):
        self.process.stdin.close()
        try:
            self.process.wait(timeout=20 * TIMEOUT_SCALE)
        except subprocess.TimeoutExpired:
            self.process.kill()
            self.process.wait()
            raise
        finally:
            self.reader.join(timeout=5 * TIMEOUT_SCALE)
            self.process.stdout.close()

    def wait(self, predicate):
        while True:
            row = self.events.get(timeout=35 * TIMEOUT_SCALE)
            assert row is not None, "桌面服务提前退出"
            if predicate(row):
                return row

    def send(self, method, **fields):
        request = str(next(self.ids))
        self.process.stdin.write(json.dumps(dict(id=request, method=method, **fields)) + "\n")
        self.process.stdin.flush()
        return request

    def reply(self, request):
        return self.wait(lambda row: row.get("id") == request)

    def call_collect(self, method, **fields):
        request = self.send(method, **fields)
        events = []
        while True:
            row = self.events.get(timeout=35 * TIMEOUT_SCALE)
            assert row is not None, "桌面服务提前退出"
            if row.get("id") == request:
                return row, events
            events.append(row)

    def call(self, method, **fields):
        return self.reply(self.send(method, **fields))["result"]


def file_offer(transfer, sender, name, size):
    encoded = [value.encode("utf-8") for value in (transfer, sender, sender, name)]
    assert all(len(value) < 128 for value in encoded)
    fields = b"".join(bytes([len(value)]) + value for value in encoded)
    return b"LANC\x02" + fields + struct.pack("<q", size)


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as data:
        for block in iter(lambda: data.read(65536), b""):
            digest.update(block)
    return digest.hexdigest()


def settings_regression(executable, bundle, home):
    original = bundle / "settings.json"
    seed = b'{"NickName":"original","Unknown":true,"AutoAcceptFiles":true}\n'
    original.write_bytes(seed)
    original.chmod(0o444)
    with Service(executable, bundle, home) as service:
        assert service.call("settings")["settingsPath"] == str(original)
        attempted = service.call("save-settings", nickname="cancelled", autoAcceptFiles=False)
        backup = Path(attempted["backupPath"])
        override = Path(attempted["overridePath"])
        assert backup.read_bytes() == seed and original.read_bytes() == seed and not override.exists()
        same_profile = subprocess.run(
            [executable, "--desktop-service"], env=service.env, input="", text=True,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=15 * TIMEOUT_SCALE,
        )
        assert same_profile.returncode == 1 and "同一配置" in same_profile.stderr

    scripted = subprocess.run(
        [executable, "--cli"], env=service.env, input="/nick piped\n/quit\n", text=True, encoding="utf-8",
        capture_output=True, timeout=35 * TIMEOUT_SCALE, check=True,
    )
    assert "请在交互终端使用 /nick" in scripted.stdout and not override.exists()
    assert original.read_bytes() == seed and backup.read_bytes() == seed

    with Service(executable, bundle, home) as service:
        assert service.call("settings")["nickname"] == "original"
        service.call("save-settings", nickname="accepted", autoAcceptFiles=False)
        result = service.call(
            "save-settings", nickname="accepted", autoAcceptFiles=False, confirmOverride=True,
            themeMode=2, accentPreset=3, uiFontFamily="SimHei", uiFontSize=12.0,
            chatFontFamily="Consolas", chatFontSize=13.0,
        )
        assert result["settingsPath"] == str(override) and result["autoAcceptFiles"] is False
        assert result["themeMode"] == 2 and result["accentPreset"] == 3
        assert result["uiFontFamily"] == "SimHei" and result["uiFontSize"] == 12.0
        assert result["chatFontFamily"] == "Consolas" and result["chatFontSize"] == 13.0
        saved = json.loads(override.read_bytes())
        assert saved["Unknown"] is True
        assert saved["ThemeMode"] == 2 and saved["AccentPreset"] == 3
        assert saved["UiFontFamily"] == "SimHei" and saved["UiFontSize"] == 12.0
        assert saved["ChatFontFamily"] == "Consolas" and saved["ChatFontSize"] == 13.0

    with Service(executable, bundle, home) as service:
        result = service.call("settings")
        assert result["settingsPath"] == str(override) and result["nickname"] == "accepted"
        assert result["autoAcceptFiles"] is False and original.read_bytes() == backup.read_bytes() == seed
        assert result["themeMode"] == 2 and result["accentPreset"] == 3
        assert result["uiFontFamily"] == "SimHei" and result["uiFontSize"] == 12.0
        assert result["chatFontFamily"] == "Consolas" and result["chatFontSize"] == 13.0
    print("只读设置备份、取消、确认、外观持久化、锁互斥、重启及原字节通过")


def transfer_regression(executable, bundle, home):
    # 占用默认端口，服务和对端都必须使用实际监听端口。
    with socket.socket() as reserved, socket.socket() as listener:
        reserved.bind(("127.0.0.1", 38889))
        reserved.listen()
        listener.bind(("127.0.0.2", 0))
        listener.listen()
        listener.settimeout(35 * TIMEOUT_SCALE)
        with Service(executable, bundle, home) as service, ThreadPoolExecutor(max_workers=2) as pool:
            actual = service.ready["tcpPort"]
            assert actual != 38889
            settings = service.call("save-settings", nickname="accepted", autoAcceptFiles=True)
            receive_dir = Path(settings["downloadDirectory"])

            def incoming(address):
                conn = socket.create_connection(("127.0.0.1", actual), timeout=25 * TIMEOUT_SCALE,
                                                source_address=(address, 0))
                return conn

            with incoming("127.0.0.2") as conn:
                conn.sendall(file_offer("short-size", "sender-1", "short.bin", 12)[:-4])
                conn.shutdown(socket.SHUT_WR)
                assert conn.recv(1) == b""
            with incoming("127.0.0.2") as conn:
                conn.sendall(file_offer("interrupted", "sender-1", "partial.bin", 512))
                assert conn.recv(1) == b"\x01"
                conn.sendall(b"x" * 10)
                conn.shutdown(socket.SHUT_WR)
                assert conn.recv(1) == b"\x00"
            failure = service.wait(lambda row: row.get("transfer") == "interrupted" and row.get("failed"))
            assert failure["received"] == 10
            assert not (receive_dir / "partial.bin").exists() and not list(receive_dir.glob(".*.downloading"))

            barrier = Barrier(2)

            def concurrent_file(address, transfer, chunk, count):
                digest = hashlib.sha256()
                with incoming(address) as conn:
                    offer = file_offer(transfer, transfer, "same.bin", len(chunk) * count)
                    # 将报头拆成两次写入，正文持续通过固定大小块发送。
                    conn.sendall(offer[:3])
                    conn.sendall(offer[3:])
                    assert conn.recv(1) == b"\x01"
                    barrier.wait(timeout=15 * TIMEOUT_SCALE)
                    for _ in range(count):
                        conn.sendall(chunk)
                        digest.update(chunk)
                    assert conn.recv(1) == b"\x01"
                return digest.hexdigest()

            first = pool.submit(concurrent_file, "127.0.0.2", "sender-1", b"A" * 65536, 1024)
            second = pool.submit(concurrent_file, "127.0.0.3", "sender-2", b"B" * 65536, 17)
            expected = {first.result(timeout=45 * TIMEOUT_SCALE), second.result(timeout=45 * TIMEOUT_SCALE)}
            received = list(receive_dir.glob("same*.bin"))
            assert len(received) == 2 and {sha256(path) for path in received} == expected
            completed = [service.wait(lambda row: row.get("event") == "progress" and row.get("completed"))
                         for _ in received]
            assert {row["chat"] for row in completed} == {"LAN-127.0.0.2", "LAN-127.0.0.3"}
            print("定长短读、中断清理、并发同名、64 MiB 流式传输及两个最终 ACK 通过", sorted(expected))

            def announce(peer):
                with socket.socket(type=socket.SOCK_DGRAM) as udp:
                    udp.bind(("127.0.0.2", 0))
                    message = f"ACK|{peer}|对端|test-host|{listener.getsockname()[1]}|127.0.0.2"
                    udp.sendto(message.encode(), ("127.0.0.1", service.ready["udpPort"]))
                service.wait(lambda row: row.get("event") == "peers")

            announce("sender-1")
            known = service.call("peers")
            assert any(row["id"] == "sender-1" and row["port"] == listener.getsockname()[1] for row in known)
            fixture = bundle / "out.bin"
            fixture.write_bytes(b"outgoing")

            def fake_receiver(accept, final_ack):
                with listener.accept()[0] as conn, conn.makefile("rb") as stream:
                    conn.settimeout(15 * TIMEOUT_SCALE)
                    assert stream.read(5) == b"LANC\x02"
                    for _ in range(4):
                        stream.read(stream.read(1)[0])
                    size, = struct.unpack("<q", stream.read(8))
                    conn.sendall(bytes([accept]))
                    if accept:
                        assert stream.read(size) == fixture.read_bytes()
                        if final_ack is not None:
                            conn.sendall(bytes([final_ack]))

            for accept, ack, ok in ((0, None, False), (1, None, False), (1, 0, False), (1, 1, True)):
                receiver = pool.submit(fake_receiver, accept, ack)
                request = service.send("file", chat="sender-1", path=str(fixture))
                terminal = service.wait(lambda row: row.get("operation") == request and
                                        (row.get("failed") or row.get("completed")))
                assert terminal["completed"] is ok and terminal["failed"] is not ok
                assert terminal["chat"] == "LAN-127.0.0.2" and terminal["name"] == fixture.name
                assert ("result" in service.reply(request)) is ok
                receiver.result(timeout=25 * TIMEOUT_SCALE)
            print("非默认端口发送、拒收、缺最终 ACK、落盘失败 ACK 与成功终态通过")

        with Service(executable, bundle, home) as service:
            previous = service.call("history", chat="LAN-127.0.0.2")["messages"]
            assert len(previous) == 2
            assert previous[0]["name"].startswith("same") and previous[1]["name"] == "out.bin"
            announce("changed-node-id")
            assert any(row["id"] == "changed-node-id" and row["key"] == "LAN-127.0.0.2"
                       for row in service.call("peers"))
            assert service.call("history", chat="LAN-127.0.0.2")["messages"] == previous
            print("同 IP 换节点保留历史，服务重启后恢复会话通过")


def receive_fixture(service, name, contents):
    with socket.create_connection(("127.0.0.1", service.ready["tcpPort"]), timeout=25 * TIMEOUT_SCALE,
                                  source_address=("127.0.0.2", 0)) as conn:
        conn.sendall(file_offer(name, "history-fixture", name, len(contents)))
        assert conn.recv(1) == b"\x01"
        conn.sendall(contents)
        assert conn.recv(1) == b"\x01"
    message = service.wait(lambda row: row.get("event") == "message" and row["data"]["name"] == name)
    assert "storageError" not in message["data"], message


def crash_regression(executable, bundle, home):
    fixture = bundle / "pending.bin"
    fixture.write_bytes(b"pending")
    chat = "LAN-127.0.0.2"
    with socket.socket() as listener:
        listener.bind(("127.0.0.2", 0))
        listener.listen()
        listener.settimeout(35 * TIMEOUT_SCALE)
        with Service(executable, bundle, home) as service:
            with socket.socket(type=socket.SOCK_DGRAM) as udp:
                udp.bind(("127.0.0.2", 0))
                peer_port = listener.getsockname()[1]
                udp.sendto(f"ACK|pending-peer|pending|test-host|{peer_port}|127.0.0.2".encode(),
                           ("127.0.0.1", service.ready["udpPort"]))
            service.wait(lambda row: row.get("event") == "peers")
            request = service.send("file", chat="pending-peer", path=str(fixture))
            with listener.accept()[0] as conn, conn.makefile("rb") as stream:
                conn.settimeout(15 * TIMEOUT_SCALE)
                assert stream.read(5) == b"LANC\x02"
                for _ in range(4):
                    stream.read(stream.read(1)[0])
                size, = struct.unpack("<q", stream.read(8))
                conn.sendall(b"\x01")
                assert stream.read(size) == fixture.read_bytes()
                # 对端保留连接和最终回执，模拟发送已传完但结果仍未确认。
                service.process.kill()
                assert conn.recv(1) == b""
            while (row := service.events.get(timeout=35 * TIMEOUT_SCALE)) is not None:
                assert row.get("id") != request and not row.get("completed"), row
        with Service(executable, bundle, home) as restarted:
            assert restarted.call("history", chat=chat)["messages"] == []
            listener.settimeout(1)
            try:
                with listener.accept()[0]:
                    raise AssertionError("服务重启后自行重发了未确认文件")
            except socket.timeout:
                pass
    print("发送完成正文但未收回执时强退服务、端口和配置锁恢复、重启后没有重发通过")


def history_regression(executable, bundle, home):
    chat = "LAN-127.0.0.2"
    with Service(executable, bundle, home) as service:
        settings = service.call("save-settings", nickname="history", autoAcceptFiles=True)
        old_history = Path(settings["historyDirectory"])
        receive_dir = Path(settings["downloadDirectory"])
        receive_fixture(service, "before.bin", b"old")
        previous = service.call("history", chat=chat)["messages"]
        assert len(previous) == 1 and previous[0]["exists"]
        assert previous[0]["timestamp"] and Path(previous[0]["path"]) == receive_dir / "before.bin"
        original = {file.name: file.read_bytes() for file in old_history.glob("history_*.jsonl")}
        assert original
        occupied = bundle / "occupied"
        occupied.mkdir()
        occupied_file = occupied / next(iter(original))
        occupied_file.write_bytes(b"existing destination data")
        failure = service.reply(service.send("save-settings", nickname="history",
                                             autoAcceptFiles=True, historyDirectory=str(occupied)))
        assert "目标目录已有聊天记录" in failure["error"]
        assert occupied_file.read_bytes() == b"existing destination data"
        assert service.call("settings")["historyDirectory"] == str(old_history)

        migrated = bundle / "migrated"
        result = service.call("save-settings", nickname="history",
                              autoAcceptFiles=True, historyDirectory=str(migrated))
        assert result["historyDirectory"] == str(migrated)
        assert {file.name: file.read_bytes() for file in migrated.glob("history_*.jsonl")} == original
        assert service.call("history", chat=chat)["messages"] == previous
        receive_fixture(service, "after.bin", b"new")
        assert (receive_dir / "before.bin").read_bytes() == b"old"
        assert (receive_dir / "after.bin").read_bytes() == b"new"
        assert {file.name: file.read_bytes() for file in old_history.glob("history_*.jsonl")} == original

    with Service(executable, bundle, home) as service:
        assert service.call("settings")["historyDirectory"] == str(migrated)
        restored = service.call("history", chat=chat)["messages"]
        assert [(row["name"], row["size"], row["exists"]) for row in restored] == [
            ("before.bin", 3, True), ("after.bin", 3, True),
        ], restored
        entries = [json.loads(line) for file in migrated.glob("history_*.jsonl")
                   for line in file.read_text(encoding="utf-8-sig").splitlines()]
        assert [Path(item["FilePath"]) for item in entries] == [
            receive_dir / "before.bin", receive_dir / "after.bin",
        ], entries
        clear_reply, clear_events = service.call_collect("clear-all-histories")
        assert "result" in clear_reply
        assert any(row.get("event") == "histories-cleared" for row in clear_events)
        assert service.call("history", chat=chat)["messages"] == []
        print("用户主动迁移历史、冲突时保留原数据、迁移后追加、全清与重启恢复通过")


def delete_session_regression(executable, bundle, home):
    with socket.socket() as listener, Service(executable, bundle, home) as service:
        listener.bind(("127.0.0.2", 0))
        listener.listen()
        failed = service.reply(service.send("delete-session", chat="BROADCAST", removePeer=True))
        assert "不能删除全员群聊" in failed["error"]

        with socket.socket(type=socket.SOCK_DGRAM) as udp:
            udp.bind(("127.0.0.2", 0))
            udp.sendto(f"ACK|online-peer|在线节点|test-host|{listener.getsockname()[1]}|127.0.0.2".encode(),
                       ("127.0.0.1", service.ready["udpPort"]))
        service.wait(lambda row: row.get("event") == "peers")

        receive_fixture(service, "drop.bin", b"payload")
        history_rows = service.call("history", chat="LAN-127.0.0.2")["messages"]
        assert len(history_rows) == 1 and history_rows[0]["exists"] and history_rows[0]["timestamp"]
        assert Path(history_rows[0]["path"]).name == "drop.bin"

        # 清空群聊时不移除群聊本身，也不应额外广播 peers；先通过同步屏障清空先前的异步事件。
        service.call_collect("history", chat="BROADCAST")
        clear_reply, clear_events = service.call_collect("delete-session", chat="BROADCAST", removePeer=False)
        assert "result" in clear_reply
        hist_reply, hist_events = service.call_collect("history", chat="BROADCAST")
        assert hist_reply["result"]["messages"] == []
        assert not any(row.get("event") == "peers" for row in clear_events + hist_events)

        # 删除在线好友应清空历史、移出在线列表，且仅由 OnPeerLeft 触发一次 peers 广播。
        del_reply, del_events = service.call_collect("delete-session", chat="LAN-127.0.0.2", removePeer=True)
        assert not any(peer["id"] == "online-peer" for peer in del_reply["result"])
        after_reply, after_events = service.call_collect("history", chat="LAN-127.0.0.2")
        assert after_reply["result"]["messages"] == []
        peer_events = [row for row in del_events + after_events if row.get("event") == "peers"]
        assert len(peer_events) == 1, peer_events

        # 删除不存在于在线列表的离线会话也不应报错或触发 peers 广播。
        off_reply, off_events = service.call_collect("delete-session", chat="LAN-127.0.0.99", removePeer=True)
        assert "result" in off_reply
        off_hist, off_after = service.call_collect("history", chat="LAN-127.0.0.99")
        assert off_hist["result"]["messages"] == []
        assert not any(row.get("event") == "peers" for row in off_events + off_after)
    print("delete-session（禁止删群聊、清空群聊、删除在线/离线好友及单次 peers 事件）通过")


def main():
    executable = str(Path(sys.argv[1]).resolve(strict=True))
    cases = {"settings": settings_regression, "history": history_regression,
             "transfer": transfer_regression, "crash": crash_regression,
             "delete": delete_session_regression}
    selected = sys.argv[2:] or list(cases)
    with tempfile.TemporaryDirectory(prefix="lanchat-desktop-") as directory:
        for case in selected:
            base = Path(directory) / case
            bundle, home = base / "bundle", base / "home"
            bundle.mkdir(parents=True)
            home.mkdir()
            cases[case](executable, bundle, home)


if __name__ == "__main__":
    main()
