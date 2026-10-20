using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace LanChat.Linux
{
    class Program
    {
        private static NetworkService _netService;
        private static PeerInfo _currentChatPeer = null; // null = broadcast
        private static readonly object _consoleLock = new object();
        private static volatile bool _isRunning = true;

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            string initialNick = Environment.UserName;
            if (args.Length > 0 && !args[0].StartsWith("-"))
            {
                initialNick = args[0];
            }

            _netService = new NetworkService(initialNick);

            // Register events
            _netService.OnPeerJoined += peer =>
            {
                PrintColored(ConsoleColor.Green, string.Format("\n[+] 好友上线: {0} ({1} @ {2}:{3})", peer.NickName, peer.HostName, peer.IP, peer.TcpPort));
                PrintPrompt();
            };

            _netService.OnPeerLeft += peerId =>
            {
                PrintColored(ConsoleColor.DarkGray, string.Format("\n[-] 好友离线: {0}", peerId));
                PrintPrompt();
            };

            _netService.OnMessageReceived += msg =>
            {
                if (msg.IsBroadcast)
                {
                    PrintColored(ConsoleColor.Cyan, string.Format("\n[{0}] [📢 广播] {1}: ", msg.Timestamp.ToString("HH:mm:ss"), msg.SenderNick));
                    Console.WriteLine(msg.Content);
                }
                else
                {
                    PrintColored(ConsoleColor.Magenta, string.Format("\n[{0}] [🔒 私聊] {1}: ", msg.Timestamp.ToString("HH:mm:ss"), msg.SenderNick));
                    Console.WriteLine(msg.Content);
                }
                PrintPrompt();
            };

            _netService.OnFileReceived += (senderNick, fileName, localPath, size) =>
            {
                PrintColored(ConsoleColor.Green, string.Format("\n[{0}] 📎 收到文件: {1} ({2})", DateTime.Now.ToString("HH:mm:ss"), fileName, FormatSize(size)));
                PrintColored(ConsoleColor.DarkGreen, string.Format("    来自: {0} | 保存路径: {1}", senderNick, localPath));
                PrintPrompt();
            };

            _netService.OnFileProgress += prog =>
            {
                if (!prog.IsCompleted && !prog.IsFailed && prog.TransferredBytes > 0)
                {
                    // Render lightweight progress indicator
                    lock (_consoleLock)
                    {
                        string action = prog.IsSending ? "发送中" : "接收中";
                        Console.Write(string.Format("\r[{0}] {1}: {2}% ({3} / {4}) - {5:F1} MB/s    ",
                            action, prog.FileName, prog.ProgressPercentage,
                            FormatSize(prog.TransferredBytes), FormatSize(prog.TotalBytes),
                            prog.SpeedMBps));
                    }
                }
            };

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                ExitApp();
            };

            // Start networking
            _netService.Start();

            // Print banner
            PrintBanner();
            PrintPrompt();

            // Main input loop
            while (_isRunning)
            {
                string line = Console.ReadLine();
                if (line == null) break;

                line = line.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    PrintPrompt();
                    continue;
                }

                if (line.StartsWith("/"))
                {
                    HandleCommand(line);
                }
                else
                {
                    SendCurrentMessage(line);
                }

                if (_isRunning)
                {
                    PrintPrompt();
                }
            }

            ExitApp();
        }

        private static void PrintBanner()
        {
            Console.WriteLine();
            PrintColored(ConsoleColor.Cyan, "==========================================================================");
            PrintColored(ConsoleColor.Yellow, "       LanChat for Linux (x86_64 / aarch64) - 局域网极速快传与通讯");
            PrintColored(ConsoleColor.Cyan, "==========================================================================");
            Console.WriteLine(string.Format(" [+] 本机物理 IP:  {0}:{1}", _netService.GetPrimaryPhysicalIp(), _netService.LocalTcpPort));
            Console.WriteLine(string.Format(" [+] 本机昵称:     {0} (主机: {1})", _netService.NickName, _netService.HostName));
            Console.WriteLine(string.Format(" [+] 文件接收目录: {0}", _netService.SaveDirectory));
            Console.WriteLine(" [+] 常用指令:     /list (查看好友)  /chat <序号> (私聊)  /send <文件>");
            Console.WriteLine("                  /add <IP> (直连好友)  /help (查看全部帮助)");
            Console.WriteLine(" [+] 直接输入文本回车即可向全局/私聊发送消息");
            PrintColored(ConsoleColor.Cyan, "==========================================================================");
            Console.WriteLine();
        }

        private static void PrintPrompt()
        {
            lock (_consoleLock)
            {
                if (_currentChatPeer == null)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("[📢 广播群聊] > ");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write(string.Format("[私聊: {0} ({1})] > ", _currentChatPeer.NickName, _currentChatPeer.IP));
                }
                Console.ResetColor();
            }
        }

        private static void SendCurrentMessage(string text)
        {
            if (_currentChatPeer == null)
            {
                // Broadcast
                var peers = _netService.GetActivePeers();
                PrintColored(ConsoleColor.DarkGreen, string.Format("[{0}] 我 (广播): {1}", DateTime.Now.ToString("HH:mm:ss"), text));

                if (peers.Count == 0)
                {
                    PrintColored(ConsoleColor.DarkYellow, "  [!] 提示: 当前在线列表为空，消息已尝试广播至局域网");
                }

                new Thread(() =>
                {
                    foreach (var p in peers)
                    {
                        _netService.SendTextMessage(p, text, true);
                    }
                }) { IsBackground = true }.Start();
            }
            else
            {
                // Private message
                PeerInfo target = _currentChatPeer;
                PrintColored(ConsoleColor.DarkMagenta, string.Format("[{0}] 我 -> {1} (私聊): {2}", DateTime.Now.ToString("HH:mm:ss"), target.NickName, text));

                new Thread(() =>
                {
                    bool ok = _netService.SendTextMessage(target, text, false);
                    if (!ok)
                    {
                        PrintColored(ConsoleColor.Red, string.Format("\n  [x] 向 {0} 发送私聊消息失败，对方可能已离线", target.NickName));
                        PrintPrompt();
                    }
                }) { IsBackground = true }.Start();
            }
        }

        private static void HandleCommand(string cmdLine)
        {
            string[] parts = cmdLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            string cmd = parts[0].ToLowerInvariant();

            switch (cmd)
            {
                case "/help":
                case "/h":
                case "/?":
                    ShowHelp();
                    break;

                case "/list":
                case "/peers":
                case "/ls":
                    ShowPeers();
                    break;

                case "/chat":
                case "/to":
                    if (parts.Length < 2)
                    {
                        PrintColored(ConsoleColor.Yellow, "用法: /chat <编号|IP|all>  例如: /chat 1 或 /chat all");
                        return;
                    }
                    SwitchChat(parts[1]);
                    break;

                case "/send":
                    HandleSendFile(parts);
                    break;

                case "/add":
                    if (parts.Length < 2)
                    {
                        PrintColored(ConsoleColor.Yellow, "用法: /add <IP地址>  例如: /add 192.168.255.147");
                        return;
                    }
                    AddPeerByIp(parts[1]);
                    break;

                case "/refresh":
                case "/scan":
                    PrintColored(ConsoleColor.Cyan, "正在向局域网广播并扫描网段中...");
                    _netService.BroadcastPresence("HELLO");
                    break;

                case "/nick":
                    if (parts.Length < 2)
                    {
                        PrintColored(ConsoleColor.Yellow, "用法: /nick <新昵称>");
                        return;
                    }
                    _netService.NickName = parts[1];
                    _netService.BroadcastPresence("HELLO");
                    PrintColored(ConsoleColor.Green, "昵称已修改为: " + parts[1]);
                    break;

                case "/clear":
                case "/cls":
                    Console.Clear();
                    PrintBanner();
                    break;

                case "/exit":
                case "/quit":
                case "/q":
                    ExitApp();
                    break;

                default:
                    PrintColored(ConsoleColor.Red, "未知指令: " + cmd + " (输入 /help 查看指令说明)");
                    break;
            }
        }

        private static void ShowPeers()
        {
            var peers = _netService.GetActivePeers();
            Console.WriteLine();
            PrintColored(ConsoleColor.Cyan, "--------------------------- 在线好友列表 ---------------------------");
            Console.WriteLine(" #  | 昵称              | 主机名          | IP:端口                ");
            Console.WriteLine("----+-------------------+-----------------+-------------------------");
            PrintColored(ConsoleColor.Green, " 0  | 📢 局域网广播群聊  | ALL PEERS       | 255.255.255.255         ");

            for (int i = 0; i < peers.Count; i++)
            {
                var p = peers[i];
                string line = string.Format(" {0,-2} | {1,-17} | {2,-15} | {3}:{4}", i + 1, p.NickName, p.HostName, p.IP, p.TcpPort);
                if (_currentChatPeer != null && _currentChatPeer.Id == p.Id)
                {
                    PrintColored(ConsoleColor.Magenta, line + " <-- [当前私聊]");
                }
                else
                {
                    Console.WriteLine(line);
                }
            }
            PrintColored(ConsoleColor.Cyan, string.Format("----------------------- 共 {0} 位好友在线 -------------------------", peers.Count));
            Console.WriteLine();
        }

        private static void SwitchChat(string target)
        {
            if (target.Equals("all", StringComparison.OrdinalIgnoreCase) || target == "0")
            {
                _currentChatPeer = null;
                PrintColored(ConsoleColor.Green, "已切换为: 📢 局域网广播群聊模式");
                return;
            }

            var peers = _netService.GetActivePeers();
            int index;
            if (int.TryParse(target, out index) && index >= 1 && index <= peers.Count)
            {
                _currentChatPeer = peers[index - 1];
                PrintColored(ConsoleColor.Magenta, string.Format("已切换当前私聊对象为: {0} ({1}:{2})", _currentChatPeer.NickName, _currentChatPeer.IP, _currentChatPeer.TcpPort));
                return;
            }

            foreach (var p in peers)
            {
                if (p.NickName.Equals(target, StringComparison.OrdinalIgnoreCase) || p.IP.ToString() == target)
                {
                    _currentChatPeer = p;
                    PrintColored(ConsoleColor.Magenta, string.Format("已切换当前私聊对象为: {0} ({1}:{2})", _currentChatPeer.NickName, _currentChatPeer.IP, _currentChatPeer.TcpPort));
                    return;
                }
            }

            PrintColored(ConsoleColor.Red, "未找到目标好友: " + target + " (输入 /list 查看当前列表)");
        }

        private static void HandleSendFile(string[] parts)
        {
            PeerInfo target = _currentChatPeer;
            string filePath = "";

            if (parts.Length == 2)
            {
                // /send <filepath>
                filePath = parts[1];
                if (target == null)
                {
                    var peers = _netService.GetActivePeers();
                    if (peers.Count == 0)
                    {
                        PrintColored(ConsoleColor.Red, "当前没有在线好友，无法发送文件");
                        return;
                    }
                    if (peers.Count == 1)
                    {
                        target = peers[0];
                    }
                    else
                    {
                        PrintColored(ConsoleColor.Yellow, "当前处于广播模式，请指定接收人: /send <好友编号或IP> <文件路径>");
                        return;
                    }
                }
            }
            else if (parts.Length >= 3)
            {
                // /send <peer> <filepath>
                string peerArg = parts[1];
                filePath = parts[2];
                var peers = _netService.GetActivePeers();
                int idx;
                if (int.TryParse(peerArg, out idx) && idx >= 1 && idx <= peers.Count)
                {
                    target = peers[idx - 1];
                }
                else
                {
                    foreach (var p in peers)
                    {
                        if (p.NickName.Equals(peerArg, StringComparison.OrdinalIgnoreCase) || p.IP.ToString() == peerArg)
                        {
                            target = p;
                            break;
                        }
                    }
                }

                if (target == null)
                {
                    PrintColored(ConsoleColor.Red, "未找到指定的好友: " + peerArg);
                    return;
                }
            }
            else
            {
                PrintColored(ConsoleColor.Yellow, "用法: /send <文件路径>  或  /send <好友编号或IP> <文件路径>");
                return;
            }

            if (!File.Exists(filePath))
            {
                PrintColored(ConsoleColor.Red, "文件不存在: " + filePath);
                return;
            }

            FileInfo fi = new FileInfo(filePath);
            PrintColored(ConsoleColor.Cyan, string.Format("\n正在向 [{0}] 发送文件: {1} ({2}) ...", target.NickName, fi.Name, FormatSize(fi.Length)));

            _netService.SendFileAsync(target, filePath,
                prog => { },
                (success, reason) =>
                {
                    Console.WriteLine();
                    if (success)
                    {
                        PrintColored(ConsoleColor.Green, string.Format("文件 {0} 发送成功！", fi.Name));
                    }
                    else
                    {
                        PrintColored(ConsoleColor.Red, string.Format("文件 {0} 发送失败: {1}", fi.Name, reason));
                    }
                    PrintPrompt();
                }
            );
        }

        private static void AddPeerByIp(string ip)
        {
            PrintColored(ConsoleColor.Cyan, "正在向 " + ip + " 发起 TCP 握手探测...");
            _netService.ProbeIp(ip, (success, msg) =>
            {
                Console.WriteLine();
                if (success)
                {
                    PrintColored(ConsoleColor.Green, msg);
                }
                else
                {
                    PrintColored(ConsoleColor.Red, msg);
                }
                PrintPrompt();
            });
        }

        private static void ShowHelp()
        {
            Console.WriteLine();
            PrintColored(ConsoleColor.Yellow, "=========================== LanChat 指令帮助 ===========================");
            Console.WriteLine(" 直接输入文字按回车   : 发送消息给当前会话 (默认是广播)");
            Console.WriteLine(" /list, /peers       : 列出当前局域网所有在线好友");
            Console.WriteLine(" /chat <编号|IP|all> : 切换当前聊天对象 (0 或 all 为广播模式)");
            Console.WriteLine(" /send <文件路径>    : 发送文件给当前私聊的好友");
            Console.WriteLine(" /send <编号> <文件> : 发送文件给指定好友");
            Console.WriteLine(" /add <IP>           : 手动连接好友电脑 (跨网段/VPN必备)");
            Console.WriteLine(" /refresh, /scan     : 重新扫描局域网网段并广播自身");
            Console.WriteLine(" /nick <新昵称>      : 修改自己的昵称并全网同步");
            Console.WriteLine(" /clear              : 清空当前屏幕");
            Console.WriteLine(" /quit, /exit        : 退出程序");
            PrintColored(ConsoleColor.Yellow, "========================================================================");
            Console.WriteLine();
        }

        private static void ExitApp()
        {
            _isRunning = false;
            Console.WriteLine("\n正在断开局域网并安全退出...");
            if (_netService != null)
            {
                _netService.Dispose();
            }
            Environment.Exit(0);
        }

        private static void PrintColored(ConsoleColor color, string text)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ResetColor();
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
        }
    }
}
