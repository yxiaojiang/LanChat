"""读写 Windows LanChat 可识别的 UTF-8 JSONL 会话历史。"""

from collections import Counter
from datetime import datetime
import json
import os
from pathlib import Path
import re
import tempfile

from .storage import application_directory, is_installed, log_message


BROADCAST = "BROADCAST"
_PEER_PREFIX = "history_peer_"
_ESCAPES = str.maketrans({"\\": "\\\\", '"': '\\"', "\n": "\\n", "\r": "\\r", "\t": "\\t"})
_FILE_KEY = re.compile(r"[A-Za-z0-9_.-]+")


def local_timestamp(ts=None):
    """返回 Windows 历史使用的本地毫秒时间戳，缺省取当前时间。"""
    clock = datetime.now() if ts is None else datetime.fromtimestamp(ts)
    return clock.isoformat(timespec="milliseconds")


def _encode(row):
    # Windows 只识别这五种转义，其余控制字符按 Windows 写入器保留原字节。
    def value_text(value):
        if isinstance(value, str):
            return '"' + value.translate(_ESCAPES) + '"'
        return json.dumps(value, ensure_ascii=False, separators=(",", ":"))
    content = ",".join(f"{value_text(key)}:{value_text(value)}" for key, value in row.items())
    return ("{" + content + "}\n").encode("utf-8")


def _decode(raw):
    try:
        # Windows 可能写 BOM，以及 JSON 严格模式拒绝的其他 C0 字符。
        row = json.loads(raw.decode("utf-8-sig"), strict=False)
    except (ValueError, UnicodeError):
        return None
    return row if isinstance(row, dict) and isinstance(row.get("Content"), str) else None


def _record(key, item):
    row = {
        "SessionKey": key,
        "SenderId": item.get("sender_id", ""),
        "SenderNick": item.get("sender", ""),
        "TargetId": item.get("target_id", ""),
        "PeerNick": item.get("peer_nick", ""),
        "Content": item["text"],
        "Timestamp": item["timestamp"],
        "IsSelf": item.get("is_self", False),
        "IsBroadcast": key == BROADCAST,
        "IsSystem": item.get("is_sys", False),
        "IsFile": item.get("is_file", False),
        "FileName": item.get("file_name", ""),
        "FilePath": item.get("local_path", ""),
        "FileSize": item.get("file_size", 0),
    }
    if item.get("remote_address"):
        row["RemoteAddress"] = item["remote_address"]
    if item.get("operation_id"):
        row["OperationId"] = item["operation_id"]
        row["DeliveryStatus"] = item.get("status", "")
        row["DeliveryState"] = item.get("delivery_state", "pending")
    return row


def _message(row):
    timestamp = row.get("Timestamp") or local_timestamp()
    try:
        time = datetime.fromisoformat(timestamp).strftime("%H:%M:%S")
    except (TypeError, ValueError):
        timestamp = local_timestamp()
        time = timestamp[11:19]
    state = row.get("DeliveryState")
    status = row.get("DeliveryStatus")
    if state == "pending":
        status = "上次运行已退出，传输结果未确认"
    return {
        "sender": row.get("SenderNick") or "",
        "sender_id": row.get("SenderId") or "",
        "target_id": row.get("TargetId") or "",
        "peer_nick": row.get("PeerNick") or "",
        "remote_address": row.get("RemoteAddress"),
        "text": row["Content"],
        "timestamp": timestamp,
        "time": time,
        "is_self": row.get("IsSelf") is True,
        "is_sys": row.get("IsSystem") is True,
        "is_file": row.get("IsFile") is True,
        "file_name": row.get("FileName") or "",
        "local_path": row.get("FilePath") or "",
        "file_size": row.get("FileSize") if type(row.get("FileSize")) is int else 0,
        "operation_id": row.get("OperationId"),
        "status": status,
        "delivery_state": state,
        "failed": state in ("pending", "failed"),
    }


class HistoryStore:
    """管理会话文件。由客户端主线程调用，文件更新先写临时文件再替换。"""

    def __init__(self, directory=None):
        fallback = Path.home() / ".local/share/lanchat/ChatHistory"
        self.directory = Path(directory) if directory is not None else fallback
        if directory is None and not is_installed():
            candidate = application_directory() / "ChatHistory"
            try:
                candidate.mkdir(exist_ok=True)
                with tempfile.TemporaryFile(dir=candidate):
                    pass
            except OSError:
                pass
            else:
                self.directory = candidate

    def _path(self, key):
        if not _FILE_KEY.fullmatch(key):
            raise ValueError("会话键包含路径字符")
        name = "history_BROADCAST" if key == BROADCAST else _PEER_PREFIX + key
        return self.directory / (name + ".jsonl")

    def load(self):
        """读取全部可用会话，跳过坏行并返回按文件分组的记录。"""
        histories = {BROADCAST: []}
        for path in sorted(self.directory.glob("history_*.jsonl")):
            key = BROADCAST if path.name == "history_BROADCAST.jsonl" else path.stem[len(_PEER_PREFIX):]
            if key != BROADCAST and (not path.stem.startswith(_PEER_PREFIX) or not _FILE_KEY.fullmatch(key)):
                continue
            try:
                with path.open("rb") as stream:
                    rows = [row for raw in stream if (row := _decode(raw)) is not None]
            except OSError as exc:
                log_message("WARN", f"无法读取历史 {path.name}：{exc}")
                continue
            histories[key] = sorted((_message(row) for row in rows), key=lambda item: item["timestamp"])
        return histories

    def append(self, key, item):
        """追加一条消息；遇到崩溃留下的半行时先补换行。写入失败抛出 OSError。"""
        self.directory.mkdir(parents=True, exist_ok=True)
        with self._path(key).open("a+b") as stream:
            if stream.seek(0, os.SEEK_END):
                stream.seek(-1, os.SEEK_END)
                if stream.read(1) != b"\n":
                    stream.write(b"\n")
            stream.write(_encode(_record(key, item)))

    def complete(self, key, item):
        """以操作标识更新发送结果，保留原行位置和文件中的其他记录。"""
        path = self._path(key)
        operation = item["operation_id"]
        replaced = False
        try:
            with path.open("rb") as stream:
                rows = stream.readlines()
        except FileNotFoundError:
            rows = []
        for index, raw in enumerate(rows):
            row = _decode(raw)
            if row and row.get("OperationId") == operation:
                rows[index] = _encode(_record(key, item))
                replaced = True
                break
        if replaced:
            self._replace(path, rows)
        else:
            self.append(key, item)

    def merge(self, source, target):
        """把临时会话的已保存记录归入确认的档案，返回是否存在可合并的文件。"""
        old_path, new_path = self._path(source), self._path(target)
        if not old_path.exists():
            return False
        self.directory.mkdir(parents=True, exist_ok=True)
        with old_path.open("rb") as stream:
            moved = [row for raw in stream if (row := _decode(raw)) is not None]
        current = new_path.read_bytes() if new_path.exists() else b""
        rows = [raw.rstrip(b"\r") + b"\n" for raw in current.split(b"\n") if raw.strip(b"\r")]
        existing = Counter(_encode(row) for raw in rows if (row := _decode(raw)) is not None)
        for row in moved:
            row["SessionKey"] = target
            line = _encode(row)
            if existing[line]:
                existing[line] -= 1
            else:
                rows.append(line)
        # 重试合并时从目标文件判重。目标写入完成后才清理旧文件。
        self._replace(new_path, rows)
        old_path.unlink()
        return True

    def clear(self, key):
        """删除指定会话的历史文件，失败抛出 OSError。"""
        self._path(key).unlink(missing_ok=True)

    def _replace(self, path, rows):
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(dir=self.directory, delete=False) as stream:
                temporary = Path(stream.name)
                stream.writelines(rows)
            temporary.replace(path)
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
