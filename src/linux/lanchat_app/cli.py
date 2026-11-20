"""在交互终端运行 LanChat，无图形依赖。"""

import threading
import time

from .network import LanChatService
from .storage import log_message


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
                    service.set_nickname(parts[1])
                    print(f"\033[32m[+] 昵称已保存为: {service.nickname}\033[0m")
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
