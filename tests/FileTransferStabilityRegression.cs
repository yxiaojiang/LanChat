using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LanChat;

/// <summary>
/// 文件传输稳定性边界回归夹具。
/// <para>职责：验证零字节、64KB 文件的速率有限性，以及出站 6/2 并发上限。</para>
/// <para>依赖：共享 NetworkService 的 LANC TCP 实现和回环 TCP 假对端。</para>
/// <para>边界：只使用回环连接，不改变生产协议和用户文件。</para>
/// <para>生命周期：由 PowerShell 夹具创建临时目录并在进程结束后清理。</para>
/// </summary>
internal static class FileTransferStabilityRegression
{
    private static readonly byte[] Magic = { 76, 65, 78, 67 };

    private sealed class ConcurrencyState
    {
        internal int Current;
        internal int Maximum;
    }

    private static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("需要一个临时目录参数");
        string root = args[0];
        Directory.CreateDirectory(root);
        VerifyReceiveBoundaries(Path.Combine(root, "receive"));
        VerifyOutgoingLimits(Path.Combine(root, "send"));
        Console.WriteLine("文件传输稳定性边界回归通过：0 字节、64KB、全局 6、单目标 2");
        return 0;
    }

    private static void VerifyReceiveBoundaries(string directory)
    {
        Directory.CreateDirectory(directory);
        var terminal = new ConcurrentBag<FileTransferProgress>();
        using (var receiver = new NetworkService("边界收件端", directory))
        {
            receiver.OnFileProgress += value =>
            {
                if (value.IsCompleted || value.IsFailed) terminal.Add(value.Snapshot());
                Require(!double.IsNaN(value.SpeedMBps) && !double.IsInfinity(value.SpeedMBps) && value.SpeedMBps >= 0,
                    "接收速率出现 NaN、Infinity 或负数");
            };

            VerifySingleReceive(receiver, "empty.bin", new byte[0]);
            VerifySingleReceive(receiver, "small.bin", CreateBytes(65536, 17));
            VerifySingleReceive(receiver, "large.bin", CreateBytes(1024 * 1024 + 123, 29));
        }
        Require(terminal.Count(value => value.IsCompleted) == 3, "零字节、64KB 和大文件接收未全部完成");
    }

    private static void VerifySingleReceive(NetworkService receiver, string name, byte[] payload)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Exception failure = null;
        MethodInfo handler = typeof(NetworkService).GetMethod("HandleIncomingTcpClient", BindingFlags.Instance | BindingFlags.NonPublic);
        var accept = new Thread(() =>
        {
            try
            {
                using (TcpClient client = listener.AcceptTcpClient()) handler.Invoke(receiver, new object[] { client });
            }
            catch (Exception ex) { failure = ex; }
        });
        accept.Start();
        try
        {
            using (var client = new TcpClient(IPAddress.Loopback.ToString(), ((IPEndPoint)listener.LocalEndpoint).Port))
            using (NetworkStream stream = client.GetStream())
            {
                client.ReceiveTimeout = 5000;
                var writer = new BinaryWriter(stream, new UTF8Encoding(false), true);
                writer.Write(Magic);
                writer.Write((byte)2);
                writer.Write(Guid.NewGuid().ToString("N"));
                writer.Write("边界发送端");
                writer.Write("fixture");
                writer.Write(name);
                writer.Write((long)payload.Length);
                writer.Flush();
                Require(stream.ReadByte() == 1, name + " 协商失败");
                if (payload.Length > 0) stream.Write(payload, 0, payload.Length);
                stream.Flush();
                Require(stream.ReadByte() == 1, name + " 最终确认失败");
            }
            Require(accept.Join(5000), name + " 接收线程未结束");
            if (failure != null) throw failure;
        }
        finally { listener.Stop(); }
    }

    private static void VerifyOutgoingLimits(string root)
    {
        Directory.CreateDirectory(root);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int completed = 0;
        bool allSuccessful = true;
        var concurrency = new ConcurrencyState();
        var perPeer = new ConcurrentDictionary<string, int>();
        var maximumPerPeer = new ConcurrentDictionary<string, int>();
        var handlers = new List<Task>();
        const int expected = 16;

        var accept = Task.Factory.StartNew(() =>
        {
            for (int i = 0; i < expected; i++)
            {
                TcpClient client = listener.AcceptTcpClient();
                handlers.Add(Task.Factory.StartNew(() => HandleOutgoingOffer(client, concurrency,
                    perPeer, maximumPerPeer), TaskCreationOptions.LongRunning));
            }
        }, TaskCreationOptions.LongRunning);

        using (var sender = new NetworkService("边界发送端", root))
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            for (int peerIndex = 0; peerIndex < 4; peerIndex++)
            {
                var peer = new PeerInfo
                {
                    Id = "peer-" + peerIndex,
                    NickName = "节点" + peerIndex,
                    IP = IPAddress.Loopback,
                    TcpPort = port
                };
                for (int fileIndex = 0; fileIndex < 4; fileIndex++)
                {
                    string path = Path.Combine(root, "peer-" + peerIndex + "-file-" + fileIndex + ".bin");
                    File.WriteAllBytes(path, CreateBytes(65536, peerIndex * 10 + fileIndex));
                    sender.SendFileAsync(peer, path, value =>
                    {
                        Require(!double.IsNaN(value.SpeedMBps) && !double.IsInfinity(value.SpeedMBps) && value.SpeedMBps >= 0,
                            "发送速率出现 NaN、Infinity 或负数");
                    }, (success, reason) =>
                    {
                        if (!success) allSuccessful = false;
                        if (Interlocked.Increment(ref completed) == expected) { }
                    });
                }
            }

            Require(SpinWait.SpinUntil(() => Thread.VolatileRead(ref completed) == expected, 30000), "有界发送队列未完成");
        }

        Require(accept.Wait(10000), "发送假对端未接受全部连接");
        Task.WaitAll(handlers.ToArray(), 10000);
        listener.Stop();
        Require(allSuccessful, "有界发送回归存在失败文件");
        Require(concurrency.Maximum <= 6, "全局出站并发超过 6：" + concurrency.Maximum);
        Require(concurrency.Maximum >= 2, "并发回归未观察到有效并行发送");
        foreach (KeyValuePair<string, int> item in maximumPerPeer)
            Require(item.Value <= 2, item.Key + " 的出站并发超过 2：" + item.Value);
    }

    private static void HandleOutgoingOffer(TcpClient client, ConcurrencyState concurrency,
        ConcurrentDictionary<string, int> perPeer, ConcurrentDictionary<string, int> maximumPerPeer)
    {
        string peerKey = "未知目标";
        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                client.ReceiveTimeout = 10000;
                var reader = new BinaryReader(stream, new UTF8Encoding(false), true);
                Require(reader.ReadBytes(4).SequenceEqual(Magic) && reader.ReadByte() == 2, "LANC 文件报价不兼容");
                reader.ReadString();
                reader.ReadString();
                reader.ReadString();
                string fileName = reader.ReadString();
                long size = reader.ReadInt64();
                peerKey = fileName.Split('-')[0] + "-" + fileName.Split('-')[1];
                int now = Interlocked.Increment(ref concurrency.Current);
                UpdateMaximum(ref concurrency.Maximum, now);
                int peerNow = perPeer.AddOrUpdate(peerKey, 1, (key, value) => value + 1);
                UpdateMaximum(maximumPerPeer, peerKey, peerNow);
                stream.WriteByte(1);
                stream.Flush();
                Thread.Sleep(100);
                byte[] body = new byte[(int)size];
                ReadFully(stream, body, 0, body.Length);
                stream.WriteByte(1);
                stream.Flush();
                perPeer.AddOrUpdate(peerKey, 0, (key, value) => value - 1);
                Interlocked.Decrement(ref concurrency.Current);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("假对端失败：" + ex.Message);
            Interlocked.Decrement(ref concurrency.Current);
            perPeer.AddOrUpdate(peerKey, 0, (key, value) => Math.Max(0, value - 1));
        }
    }

    private static void ReadFully(Stream stream, byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            int read = stream.Read(buffer, offset, count);
            if (read <= 0) throw new EndOfStreamException("假对端收到的文件提前结束");
            offset += read;
            count -= read;
        }
    }

    private static void UpdateMaximum(ref int maximum, int value)
    {
        int old;
        do
        {
            old = Thread.VolatileRead(ref maximum);
            if (value <= old) return;
        } while (Interlocked.CompareExchange(ref maximum, value, old) != old);
    }

    private static void UpdateMaximum(ConcurrentDictionary<string, int> maximum, string key, int value)
    {
        maximum.AddOrUpdate(key, value, (name, old) => Math.Max(old, value));
    }

    private static byte[] CreateBytes(int count, int seed)
    {
        var bytes = new byte[count];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)((i + seed) % 251);
        return bytes;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
