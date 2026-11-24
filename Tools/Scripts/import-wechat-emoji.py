"""从已核对的微信 4.2.0.13 本机安装导出默认静态表情，供 LanChat 本地读取。"""

import argparse
import hashlib
import os
from pathlib import Path
import struct
import sys
import xml.etree.ElementTree as ET
import zlib


# 此版微信将默认表情及面板顺序编译进 Qt 资源。偏移按 DLL 的完整哈希绑定。
DLL_SHA256 = "afe3d02c58bada3c35009c0cd58ee1f57b2a00fe53237d0dce649107fddb9a20"
TREE, NAMES, DATA = 0x87FDE80, 0x87FC930, 0x86F0260
CLASSIC = (
    "1:微笑 2:撇嘴 3:色 4:发呆 5:得意 6:流泪 7:害羞 8:闭嘴 9:睡 10:大哭 11:尴尬 "
    "12:发怒 13:调皮 14:呲牙 15:惊讶 16:难过 18:抓狂 19:吐 20:偷笑 21:愉快 22:白眼 "
    "23:傲慢 24:困 25:惊恐 26:流汗 27:憨笑 28:悠闲 29:奋斗 30:咒骂 31:疑问 32:嘘 "
    "33:晕 34:敲打 36:擦汗 37:抠鼻 38:鼓掌 39:坏笑 40:左哼哼 41:右哼哼 42:哈欠 "
    "43:鄙视 44:委屈 45:快哭了 46:阴险 76:亲亲 77:爱心 78:心碎 79:拥抱 80:强 "
    "81:弱 82:握手 83:胜利 85:勾引 86:拳头 87:OK 89:啤酒 90:咖啡 91:蛋糕 92:玫瑰 "
    "93:凋谢 94:菜刀 95:炸弹 96:便便 97:月亮 98:太阳 106:猪头 107:跳跳 108:发抖 109:转圈"
)
ASSET_IDS = (
    *range(1, 17), *range(18, 35), 40, 41, 43, 45, 47, 49, 50, 51, 52, 53, 55,
    66, 67, 68, 79, 80, 81, 82, 83, 85, 86, 90, 58, 61, 69, 64, 65, 56, 71,
    75, 76, 77, 63, 93, 94, 96,
)


def read_pack(dll):
    binary = dll.read_bytes()
    if hashlib.sha256(binary).hexdigest() != DLL_SHA256:
        raise ValueError("此工具支持微信 4.2.0.13 的已核对版本，DLL 哈希不匹配")
    _, flags, count, first = struct.unpack_from(">IHII", binary, TREE + 22)
    if (flags, count, first) != (2, 152, 2):
        raise ValueError("微信表情资源目录结构不匹配")
    resources = {}
    for index in range(first, first + count):
        name_offset, flags, _, offset = struct.unpack_from(">IHII", binary, TREE + index * 22)
        position = NAMES + name_offset
        name_length, = struct.unpack_from(">H", binary, position)
        name = binary[position + 6:position + 6 + name_length * 2].decode("utf-16be")
        size, = struct.unpack_from(">I", binary, DATA + offset)
        payload = binary[DATA + offset + 4:DATA + offset + 4 + size]
        resources[name] = zlib.decompress(payload[4:]) if flags == 1 else payload

    modern = {
        item.findtext("key"): (item.findtext("cn-value"), item.findtext("fileName"))
        for item in ET.fromstring(resources["newemoji-config.xml"])
    }
    classic = {
        int(position): (f"[{label}]", f"expression_{asset}.png")
        for (position, label), asset in zip((item.split(":") for item in CLASSIC.split()), ASSET_IDS, strict=True)
    }
    entries = []
    for index, entry in enumerate(ET.fromstring(resources["emojipanel-config.xml"]), 1):
        token, name = classic[index] if index in classic else modern[entry.text]
        png = resources[name]
        if not png.startswith(b"\x89PNG\r\n\x1a\n"):
            raise ValueError(f"表情 {token} 缺少有效 PNG")
        entries.append((token, png))
    if len(entries) != 109 or len({token for token, _ in entries}) != len(entries):
        raise ValueError("微信默认表情数量不匹配或名称重复")
    return entries


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dll", type=Path, default=Path(os.environ.get("ProgramFiles", "C:/Program Files")) /
                        "Tencent/Weixin/4.2.0.13/Weixin.dll")
    if sys.platform == "win32":
        default_output = Path(os.environ["LOCALAPPDATA"]) / "LanChat/WeChatEmoji"
    else:
        default_output = Path(os.environ.get("XDG_CONFIG_HOME") or Path.home() / ".config") / "LanChat/WeChatEmoji"
    parser.add_argument("--output", type=Path, default=default_output)
    args = parser.parse_args()
    try:
        entries = read_pack(args.dll)
        args.output.mkdir(parents=True, exist_ok=True)
        manifest = []
        for index, (token, png) in enumerate(entries, 1):
            file = f"{index:03}.png"
            (args.output / file).write_bytes(png)
            manifest.append(f"{token}\t{file}")
        temporary = args.output / "manifest.tsv.tmp"
        temporary.write_text("\n".join(manifest) + "\n", encoding="utf-8")
        temporary.replace(args.output / "manifest.tsv")
    except (OSError, ValueError, KeyError) as error:
        parser.exit(1, f"{error}\n")
    print(f"已导出 {len(entries)} 个默认表情到 {args.output}")


if __name__ == "__main__":
    main()
