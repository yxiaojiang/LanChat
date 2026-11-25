using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LanChat
{
    public partial class NetworkService : IDisposable
    {
        #region TCP Listener

        private void InitTcpListener()
        {
            for (int port = TCP_BASE_PORT; port < TCP_BASE_PORT + 50; port++)
            {
                try
                {
                    _tcpListener = new TcpListener(IPAddress.Any, port);
                    _tcpListener.Start();
                    LocalTcpPort = port;
                    break;
                }
                catch
                {
                    // Try next port
                }
            }

            if (LocalTcpPort == 0)
            {
                throw new Exception("无法绑定任何可用的 TCP 端口（38889-38939）");
            }

            _tcpListenThread = new Thread(TcpListenLoop) { IsBackground = true };
            _tcpListenThread.Start();
        }

        private void TcpListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _tcpListener.AcceptTcpClient();
                    client.NoDelay = true; // Instant packet dispatch (Nagle off)

                    // Dedicated thread for incoming connections: 0ms queue delay!
                    new Thread(() => HandleIncomingTcpClient(client)) { IsBackground = true }.Start();
                }
                catch (SocketException)
                {
                    if (!_isRunning) break;
                }
                catch (Exception ex)
                {
                    Log("TCP 监听错误: " + ex.Message);
                }
            }
        }

        private void HandleIncomingTcpClient(TcpClient client)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    byte[] magic = reader.ReadBytes(4);
                    if (magic.Length < 4 || magic[0] != MAGIC[0] || magic[1] != MAGIC[1] || magic[2] != MAGIC[2] || magic[3] != MAGIC[3])
                    {
                        return;
                    }

                    byte packetType = reader.ReadByte();
                    IPAddress remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address;

                    if (packetType == 1) // Text Message
                    {
                        bool isBroadcast = reader.ReadBoolean();
                        string senderId = reader.ReadString();
                        string senderNick = reader.ReadString();
                        string targetId = reader.ReadString();
                        string content = reader.ReadString();

                        // 连接源端口是临时端口。已知节点继续使用公告提供的监听端口与主机名。
                        UpdatePeer(senderId, senderNick, "LAN-Host", remoteIp, null);

                        // Confirm immediately
                        writer.Write((byte)1);
                        writer.Flush();

                        // If it's just an internal handshake ping, don't show in chat log
                        if (content == HANDSHAKE_MSG)
                        {
                            // TCP 确认字节保持兼容。通过现有 UDP ACK 返回完整身份供发起方更新昵称。
                            SendAckTo(remoteIp, UDP_PORT);
                            return;
                        }

                        var msg = new ChatMessage
                        {
                            SenderId = senderId,
                            SenderNick = senderNick,
                            RemoteAddress = remoteIp,
                            TargetId = targetId,
                            Content = content,
                            Timestamp = DateTime.Now,
                            IsSelf = false,
                            IsBroadcast = isBroadcast
                        };

                        if (OnMessageReceived != null)
                        {
                            OnMessageReceived(msg);
                        }
                    }
                    else if (packetType == 2) // File Transfer Request
                    {
                        string transferId = reader.ReadString();
                        string senderId = reader.ReadString();
                        string senderNick = reader.ReadString();
                        string fileName = reader.ReadString();
                        long fileSize = reader.ReadInt64();

                        string safeFileName = Path.GetFileName(fileName);
                        if (!AutoAcceptFiles || fileSize < 0 || string.IsNullOrEmpty(safeFileName) ||
                            safeFileName == "." || safeFileName == "..")
                        {
                            writer.Write((byte)0);
                            writer.Flush();
                            return;
                        }

                        string folder = SaveDirectory;
                        // 各连接独占临时文件，最终名称在落盘时决定。
                        string tempPath = Path.Combine(folder, "." + Guid.NewGuid().ToString("N") + ".downloading");
                        string localPath;

                        var progress = new FileTransferProgress
                        {
                            TransferId = transferId,
                            FileName = safeFileName,
                            TotalBytes = fileSize,
                            TransferredBytes = 0,
                            IsSending = false,
                            PeerNick = senderNick,
                            TransferPeerId = senderId,
                            TransferPeerAddress = remoteIp,
                            LocalPath = Path.Combine(folder, safeFileName)
                        };

                        byte[] chunk = new byte[65536];
                        long receivedBytes = 0;
                        DateTime lastTime = DateTime.Now;
                        long lastBytes = 0;

                        try
                        {
                            Directory.CreateDirectory(folder);
                            using (FileStream fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                            {
                                writer.Write((byte)1);
                                writer.Flush();
                                if (OnFileProgress != null) OnFileProgress(progress);
                                while (receivedBytes < fileSize)
                                {
                                    int toRead = (int)Math.Min(chunk.Length, fileSize - receivedBytes);
                                    int read = stream.Read(chunk, 0, toRead);
                                    if (read <= 0) throw new EndOfStreamException("传输中断");
                                    fs.Write(chunk, 0, read);
                                    receivedBytes += read;

                                    DateTime now = DateTime.Now;
                                    double elapsed = (now - lastTime).TotalSeconds;
                                    if (elapsed >= 0.2 || receivedBytes == fileSize)
                                    {
                                        progress.TransferredBytes = receivedBytes;
                                        progress.SpeedMBps = elapsed > 0 ? (receivedBytes - lastBytes) / (elapsed * 1024.0 * 1024.0) : 0;
                                        lastTime = now;
                                        lastBytes = receivedBytes;
                                        if (OnFileProgress != null) OnFileProgress(progress);
                                    }
                                }
                            }
                            localPath = CommitReceivedFile(tempPath, folder, safeFileName);
                        }
                        catch (Exception ex)
                        {
                            progress.IsFailed = true;
                            progress.ErrorMessage = ex.Message;
                            progress.TransferredBytes = receivedBytes;
                            if (OnFileProgress != null) OnFileProgress(progress);
                            try { writer.Write((byte)0); writer.Flush(); } catch (IOException) { }
                            Log("接收文件失败: " + ex.Message);
                            return;
                        }
                        finally
                        {
                            try { File.Delete(tempPath); }
                            catch (Exception ex) { Log("清理接收临时文件失败: " + ex.Message); }
                        }

                        progress.LocalPath = localPath;
                        progress.IsCompleted = true;
                        progress.TransferredBytes = fileSize;
                        try
                        {
                            writer.Write((byte)1);
                            writer.Flush();
                        }
                        finally
                        {
                            // 在确认发送失败时，已提交的本地文件仍应出现在会话里。
                            if (OnFileProgress != null)
                            {
                                OnFileProgress(progress);
                            }

                            if (OnFileReceived != null)
                            {
                                OnFileReceived(new ChatMessage
                                {
                                    SenderId = senderId,
                                    SenderNick = senderNick,
                                    RemoteAddress = remoteIp,
                                    Content = "[接收文件] " + safeFileName,
                                    Timestamp = DateTime.Now,
                                    IsFile = true,
                                    FileTransferId = transferId,
                                    FileName = safeFileName,
                                    FilePath = localPath,
                                    FileSize = fileSize
                                });
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("TCP 客户端处理异常: " + ex.Message);
            }
        }

        #endregion

        #region TCP Communication & File Transfer

        /// <summary>
        /// 通过 TCP 直连向对端节点或全员广播发送单条文本消息
        /// </summary>
        /// <param name="peer">目标对端节点</param>
        /// <param name="message">消息正文文本</param>
        /// <param name="isBroadcast">是否为广播群聊消息</param>
        /// <returns>对端是否成功确认收到 ACK</returns>
        public bool SendTextMessage(PeerInfo peer, string message, bool isBroadcast)
        {
            try
            {
                IPEndPoint localEp = GetLocalEndpointForTarget(peer.IP);
                using (TcpClient client = new TcpClient(localEp))
                {
                    client.NoDelay = true; // Disable Nagle algorithm for instant delivery
                    var result = client.BeginConnect(peer.IP, peer.TcpPort, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
                    if (!success) return false;
                    client.EndConnect(result);

                    using (NetworkStream stream = client.GetStream())
                    using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                    using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                    {
                        writer.Write(MAGIC);
                        writer.Write((byte)1); // PacketType Text
                        writer.Write(isBroadcast);
                        writer.Write(LocalId);
                        writer.Write(NickName);
                        writer.Write(isBroadcast ? "" : peer.Id);
                        writer.Write(message);
                        writer.Flush();

                        client.ReceiveTimeout = 2000;
                        byte ack = reader.ReadByte();
                        return ack == 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("向 {0} 发送消息失败: {1}", peer.DisplayText, ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 异步启动 64KB 高性能流式分块文件传输
        /// </summary>
        /// <param name="peer">目标对端节点</param>
        /// <param name="filePath">待发送的本地物理文件完整路径</param>
        /// <param name="progressCallback">实时进度回调委托</param>
        /// <param name="completionCallback">传输完毕回调委托 (success, reason)</param>
        public void SendFileAsync(PeerInfo peer, string filePath, Action<FileTransferProgress> progressCallback, Action<bool, string> completionCallback)
        {
            SendFileAsync(peer, filePath, progressCallback, completionCallback, false);
        }

        internal void SendFileAsync(PeerInfo peer, string filePath, Action<FileTransferProgress> progressCallback,
            Action<bool, string> completionCallback, bool allowSourceDelete)
        {
            new Thread(() =>
            {
                string transferId = Guid.NewGuid().ToString("N");
                string fileName = Path.GetFileName(filePath);

                var progress = new FileTransferProgress
                {
                    TransferId = transferId,
                    FileName = fileName,
                    TransferredBytes = 0,
                    IsSending = true,
                    PeerNick = peer.NickName,
                    TransferPeerId = peer.Id,
                    TransferPeerAddress = peer.IP,
                    LocalPath = filePath
                };
                Action<string> fail = reason =>
                {
                    progress.IsFailed = true;
                    progress.ErrorMessage = reason;
                    if (progressCallback != null) progressCallback(progress);
                    if (completionCallback != null) completionCallback(false, reason);
                };

                try
                {
                    long fileSize = new FileInfo(filePath).Length;
                    progress.TotalBytes = fileSize;
                    if (progressCallback != null) progressCallback(progress);
                    IPEndPoint localEp = GetLocalEndpointForTarget(peer.IP);
                    using (TcpClient client = new TcpClient(localEp))
                    {
                        client.NoDelay = true;
                        var result = client.BeginConnect(peer.IP, peer.TcpPort, null, null);
                        bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(4));
                        if (!success)
                        {
                            fail("连接对方超时");
                            return;
                        }
                        client.EndConnect(result);

                        using (NetworkStream stream = client.GetStream())
                        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                        using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                        {
                            writer.Write(MAGIC);
                            writer.Write((byte)2); // PacketType FileOffer
                            writer.Write(transferId);
                            writer.Write(LocalId);
                            writer.Write(NickName);
                            writer.Write(fileName);
                            writer.Write(fileSize);
                            writer.Flush();

                            client.ReceiveTimeout = 10000;
                            byte accept = reader.ReadByte();
                            if (accept != 1)
                            {
                                fail("对方拒绝或未自动接收文件");
                                return;
                            }

                            byte[] buffer = new byte[65536];
                            long sentBytes = 0;
                            DateTime lastTime = DateTime.Now;
                            long lastBytes = 0;

                            // 应用截图在退出时可删除，已打开的发送流仍由传输线程负责关闭。
                            FileShare share = allowSourceDelete ? FileShare.Read | FileShare.Delete : FileShare.Read;
                            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, share))
                            {
                                int read;
                                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    stream.Write(buffer, 0, read);
                                    sentBytes += read;
                                    progress.TransferredBytes = sentBytes;

                                    DateTime now = DateTime.Now;
                                    double elapsed = (now - lastTime).TotalSeconds;
                                    if (elapsed >= 0.2 || sentBytes == fileSize)
                                    {
                                        double speed = (sentBytes - lastBytes) / (elapsed * 1024.0 * 1024.0);
                                        lastTime = now;
                                        lastBytes = sentBytes;

                                        progress.SpeedMBps = speed;
                                        if (progressCallback != null) progressCallback(progress);
                                    }
                                }
                            }
                            stream.Flush();

                            byte doneConfirm = reader.ReadByte();
                            if (doneConfirm == 1)
                            {
                                progress.IsCompleted = true;
                                progress.TransferredBytes = fileSize;
                                if (progressCallback != null) progressCallback(progress);
                                if (completionCallback != null) completionCallback(true, "发送完成");
                            }
                            else
                            {
                                fail("接收方保存失败");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!progress.IsFailed && !progress.IsCompleted) fail("发送失败: " + ex.Message);
                }
            }) { IsBackground = true }.Start();
        }

        #endregion
    }
}
