"""从一次性中间目录构建并校验两种架构的自包含 ELF。"""

import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src/linux/LanChat.Linux/LanChat.Linux.csproj"
RID_MACHINES = {"linux-x64": 62, "linux-arm64": 183}


def check_elf(data, rid):
    """检查可执行文件为目标架构的小端 ELF64，失败时抛出 ValueError。"""
    if len(data) < 64 or data[:7] != b"\x7fELF\x02\x01\x01" or int.from_bytes(data[18:20], "little") != RID_MACHINES[rid]:
        raise ValueError(f"{rid} 的 lanchat 不是对应架构的 ELF64")


def build_linux(output):
    """两种架构均构建并校验后，才将该轮产物复制到输出目录。"""
    with tempfile.TemporaryDirectory(prefix="lanchat-elf-") as tmp:
        stage = Path(tmp)
        for rid in RID_MACHINES:
            print(f"构建 {rid} 自包含 ELF", flush=True)
            subprocess.run(
                ["dotnet", "publish", str(PROJECT), "-c", "Release", "-r", rid, "--self-contained", "true",
                 "--artifacts-path", str(stage / "artifacts"), "-o", str(stage / rid)],
                check=True,
            )
            check_elf((stage / rid / "lanchat").read_bytes(), rid)
        for rid in RID_MACHINES:
            target = output / rid
            target.mkdir(parents=True, exist_ok=True)
            for built_file in (stage / rid).iterdir():
                if built_file.name in ("lanchat", "lanchat.pdb"):
                    shutil.copy2(built_file, target / built_file.name)
            print(f"生成 {target / 'lanchat'}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    try:
        build_linux(args.output.resolve())
    except (OSError, ValueError, subprocess.CalledProcessError) as exc:
        parser.exit(1, f"ELF 构建失败：{exc}\n")
