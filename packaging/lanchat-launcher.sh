#!/bin/sh
set -eu

case "$0" in
    /usr/bin/lanchat)
        LANCHAT_ROOT=/usr/lib/lanchat
        LANCHAT_INSTALLED=1
        ;;
    *)
        launcher=$(readlink -f -- "$0")
        LANCHAT_ROOT=${launcher%/*}
        LANCHAT_INSTALLED=0
        ;;
esac
export LANCHAT_ROOT LANCHAT_INSTALLED
core="$LANCHAT_ROOT/electron/resources/lanchat-bin"

case "${1-}" in
    --cli|-c)
        shift
        exec "$core" "$@"
        ;;
esac
if [ -z "${DISPLAY-}${WAYLAND_DISPLAY-}" ]; then
    exec "$core" "$@"
fi

# 桌面入口只接受用于复核实际图形后端的两个参数。
unset ELECTRON_RUN_AS_NODE
case "${1-}" in
    '') exec "$LANCHAT_ROOT/electron/electron" ;;
    --ozone-platform=x11|--ozone-platform=wayland)
        [ "$#" -eq 1 ] && exec "$LANCHAT_ROOT/electron/electron" "$1"
        ;;
esac
printf '%s\n' 'LanChat 桌面不支持该参数。使用 --cli 进入命令行。' >&2
exit 2
