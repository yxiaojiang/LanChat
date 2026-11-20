using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using LanChat;

/// <summary>
/// 在独立回环连接上驱动共享网络核心。只启动测试 TCP 监听，不监听或发送局域网广播。
/// </summary>
internal static class LinuxLoopback
{
    /// <summary>接收 Python 文字和文件，再用确认的端口发送回复与文件。</summary>
    /// <param name="args">Python 节点 ID、监听端口、C# 接收目录与可选的旧版默认端口模式。</param>
    /// <returns>通过为 0，收到错误内容或路由被覆盖时为非零。</returns>
    public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        int pythonPort = int.Parse(args[1]);
        using (var service = new NetworkService("CSharp"))
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                ChatMessage incoming = null;
                string savedPath = null;
                long savedSize = 0;
                service.OnMessageReceived += message => incoming = message;
#if LEGACY_FILE_EVENT
                service.OnFileReceived += (nick, name, path, size) => { savedPath = path; savedSize = size; };
#else
                service.OnFileReceived += message =>
                {
                    if (message.SenderId != args[0]) return;
                    savedPath = message.FilePath;
                    savedSize = message.FileSize;
                };
#endif
                service.SaveDirectory = args[2];
                service.AddOrUpdatePeer(args[0], "旧昵称", "Python-Host", IPAddress.Loopback, pythonPort);
                Console.WriteLine("{0}|{1}", service.LocalId, ((IPEndPoint)listener.LocalEndpoint).Port);
                var receive = typeof(NetworkService).GetMethod("HandleIncomingTcpClient",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Accept(listener, receive, service);
                PeerInfo peer = service.GetActivePeers().Single();
                if (incoming == null || incoming.Content != "Python 中文" || peer.NickName != "本机")
                    return 2;
                bool legacyDefault = args.Length > 3 && args[3] == "legacy-default" && pythonPort == NetworkService.TCP_BASE_PORT;
                string expectedHost = legacyDefault ? "LAN-Host" : "Python-Host";
                if (peer.TcpPort != pythonPort || peer.HostName != expectedHost)
                {
                    Console.Error.WriteLine("文字学习后端口和主机名：{0} {1}", peer.TcpPort, peer.HostName);
                    return 3;
                }
                if (!service.SendTextMessage(peer, "CSharp 中文", false))
                    return 4;
                Accept(listener, receive, service);
                if (savedPath == null || savedSize != 311071)
                    return 5;
                using (var completion = new ManualResetEvent(false))
                {
                    bool sent = false;
                    service.SendFileAsync(peer, savedPath, null, (ok, detail) =>
                    {
                        sent = ok;
                        completion.Set();
                    });
                    return completion.WaitOne(12000) && sent ? 0 : 6;
                }
            }
            finally
            {
                listener.Stop();
            }
        }
    }

    private static void Accept(TcpListener listener, MethodInfo receive, NetworkService service)
    {
        using (TcpClient client = listener.AcceptTcpClient())
        {
            client.ReceiveTimeout = 4000;
            receive.Invoke(service, new object[] { client });
        }
    }
}
