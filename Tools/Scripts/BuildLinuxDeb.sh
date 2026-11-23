#!/usr/bin/env bash
set -euo pipefail

# 从仓库根目录定位 Python 构建流程，供 Linux 用户直接运行。
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "${SCRIPT_DIR}/../.." && pwd)"
cd "${PROJECT_ROOT}"
exec python3 "${SCRIPT_DIR}/BuildLinuxDeb.py" "$@"
