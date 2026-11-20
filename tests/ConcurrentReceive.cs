using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LanChat;

/// <summary>
/// 使用隔离回环和两个独立文件流，验证共享接收核心的同名提交及最终 ACK。
/// 依赖原生 LanChat 程序集，临时目录由调用者提供。
/// </summary>
internal static class ConcurrentReceive
{
    private static int Main(string[] args)
    {
        var sink = new NetworkService("收件端") { SaveDirectory = args[0] };
        Directory.CreateDirectory(args[0]);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        var terminal = new ConcurrentBag<FileTransferProgress>();
        sink.OnFileProgress += report => { if (report.IsCompleted || report.IsFailed) terminal.Add(report); };
        listener.Start();
        var receive = typeof(NetworkService).GetMethod("HandleIncomingTcpClient", BindingFlags.Instance | BindingFlags.NonPublic);
        var accept = Task.Run(() => {
            var handlers = new Task[2];
            for (int i = 0; i < handlers.Length; i++)
            {
                TcpClient client = listener.AcceptTcpClient();
                handlers[i] = Task.Run(() => receive.Invoke(sink, new object[] { client }));
            }
            Task.WaitAll(handlers);
        });
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        int firstAck, secondAck;
        using (var first = new TcpClient("127.0.0.1", port))
        using (var second = new TcpClient("127.0.0.1", port))
        {
            first.ReceiveTimeout = second.ReceiveTimeout = 5000;
            var left = first.GetStream();
            var right = second.GetStream();
            Offer(left, "one");
            left.WriteByte(65);
            if (!SpinWait.SpinUntil(() => Directory.GetFiles(args[0], "*.downloading").Length == 1, 4000))
                throw new InvalidOperationException("预期第一路进入接收落盘");
            Offer(right, "two");
            right.WriteByte(66);
            Thread.Sleep(200);
            left.WriteByte(65);
            try { right.WriteByte(66); } catch (IOException) { }
            firstAck = ReadAck(left);
            secondAck = ReadAck(right);
        }
        if (!accept.Wait(5000)) throw new TimeoutException("并发接收未结束");
        listener.Stop();
        var received = Directory.GetFiles(args[0]);
        using (var algorithm = SHA256.Create())
        {
            Func<byte[], string> hash = data => BitConverter.ToString(algorithm.ComputeHash(data)).Replace("-", "");
            foreach (var path in received)
                Console.WriteLine("{0} {1}", Path.GetFileName(path), hash(File.ReadAllBytes(path)));
            var hashes = new[] { hash(new byte[] {65, 65}), hash(new byte[] {66, 66}) };
            Console.WriteLine("Final ACK: {0}, {1}; Complete events: {2}", firstAck, secondAck, terminal.Count(r => r.IsCompleted));
            return firstAck == 1 && secondAck == 1 && received.Length == 2 &&
                terminal.Count(r => r.IsCompleted) == 2 &&
                received.Select(path => hash(File.ReadAllBytes(path))).OrderBy(v => v).SequenceEqual(hashes.OrderBy(v => v))
                ? 0 : 1;
        }
    }

    private static void Offer(Stream stream, string id)
    {
        var writer = new BinaryWriter(stream, new UTF8Encoding(false), true);
        writer.Write(new byte[] { 76, 65, 78, 67 });
        writer.Write((byte)2);
        writer.Write(id);
        writer.Write("fixture-" + id);
        writer.Write("fixture");
        writer.Write("same-name.txt");
        writer.Write(2L);
        writer.Flush();
        if (stream.ReadByte() != 1) throw new IOException("文件协商失败");
    }

    private static int ReadAck(Stream stream)
    {
        try { return stream.ReadByte(); }
        catch (IOException) { return -1; }
    }
}
