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

            if (_tcpListener == null)
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

                    if (packetType == 1) // Text Message
                    {
                        bool isBroadcast = reader.ReadBoolean();
                        string senderId = reader.ReadString();
                        string senderNick = reader.ReadString();
                        string targetId = reader.ReadString();
                        string content = reader.ReadString();

                        IPAddress remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address;

                        // Auto learn peer immediately!
                        AddOrUpdatePeer(senderId, senderNick, "LAN-Host", remoteIp, TCP_BASE_PORT);

                        // Confirm immediately
                        writer.Write((byte)1);
                        writer.Flush();

                        // If it's just an internal handshake ping, don't show in chat log
                        if (content == HANDSHAKE_MSG)
                        {
                            return;
                        }

                        var msg = new ChatMessage
                        {
                            SenderId = senderId,
                            SenderNick = senderNick,
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

                        if (!AutoAcceptFiles)
                        {
                            writer.Write((byte)0);
                            writer.Flush();
                            return;
                        }

                        writer.Write((byte)1);
                        writer.Flush();

                        string safeFileName = Path.GetFileName(fileName);
                        string localPath = GetUniqueFilePath(SaveDirectory, safeFileName);
                        string tempPath = localPath + ".downloading";

                        var progress = new FileTransferProgress
                        {
                            TransferId = transferId,
                            FileName = safeFileName,
                            TotalBytes = fileSize,
                            TransferredBytes = 0,
                            IsSending = false,
                            PeerNick = senderNick,
                            LocalPath = localPath
                        };

                        bool success = false;
                        byte[] chunk = new byte[65536];
                        long receivedBytes = 0;
                        DateTime lastTime = DateTime.Now;
                        long lastBytes = 0;

                        using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            while (receivedBytes < fileSize)
                            {
                                int toRead = (int)Math.Min(chunk.Length, fileSize - receivedBytes);
                                int read = stream.Read(chunk, 0, toRead);
                                if (read <= 0) break;

                                fs.Write(chunk, 0, read);
                                receivedBytes += read;

                                DateTime now = DateTime.Now;
                                double elapsed = (now - lastTime).TotalSeconds;
                                if (elapsed >= 0.2 || receivedBytes == fileSize)
                                {
                                    double speed = (receivedBytes - lastBytes) / (elapsed * 1024.0 * 1024.0);
                                    lastTime = now;
                                    lastBytes = receivedBytes;

                                    progress.TransferredBytes = receivedBytes;
                                    progress.SpeedMBps = speed;
                                    if (OnFileProgress != null)
                                    {
                                        OnFileProgress(progress);
                                    }
                                }
                            }

                            if (receivedBytes == fileSize)
                            {
                                success = true;
                            }
                        }

                        if (success)
                        {
                            if (File.Exists(localPath)) File.Delete(localPath);
                            File.Move(tempPath, localPath);

                            progress.IsCompleted = true;
                            progress.TransferredBytes = fileSize;
                            if (OnFileProgress != null)
                            {
                                OnFileProgress(progress);
                            }

                            if (OnFileReceived != null)
                            {
                                OnFileReceived(senderNick, safeFileName, localPath, fileSize);
                            }

                            writer.Write((byte)1);
                            writer.Flush();
                        }
                        else
                        {
                            if (File.Exists(tempPath)) File.Delete(tempPath);
                            progress.IsFailed = true;
                            progress.ErrorMessage = "传输中断";
                            if (OnFileProgress != null)
                            {
                                OnFileProgress(progress);
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

        public void SendFileAsync(PeerInfo peer, string filePath, Action<FileTransferProgress> progressCallback, Action<bool, string> completionCallback)
        {
            new Thread(() =>
            {
                string transferId = Guid.NewGuid().ToString("N");
                string fileName = Path.GetFileName(filePath);
                FileInfo fi = new FileInfo(filePath);
                long fileSize = fi.Length;

                var progress = new FileTransferProgress
                {
                    TransferId = transferId,
                    FileName = fileName,
                    TotalBytes = fileSize,
                    TransferredBytes = 0,
                    IsSending = true,
                    PeerNick = peer.NickName,
                    LocalPath = filePath
                };

                try
                {
                    IPEndPoint localEp = GetLocalEndpointForTarget(peer.IP);
                    using (TcpClient client = new TcpClient(localEp))
                    {
                        client.NoDelay = true;
                        var result = client.BeginConnect(peer.IP, peer.TcpPort, null, null);
                        bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(4));
                        if (!success)
                        {
                            if (completionCallback != null) completionCallback(false, "连接对方超时");
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
                                if (completionCallback != null) completionCallback(false, "对方拒绝或未自动接收文件");
                                return;
                            }

                            byte[] buffer = new byte[65536];
                            long sentBytes = 0;
                            DateTime lastTime = DateTime.Now;
                            long lastBytes = 0;

                            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                int read;
                                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    stream.Write(buffer, 0, read);
                                    sentBytes += read;

                                    DateTime now = DateTime.Now;
                                    double elapsed = (now - lastTime).TotalSeconds;
                                    if (elapsed >= 0.2 || sentBytes == fileSize)
                                    {
                                        double speed = (sentBytes - lastBytes) / (elapsed * 1024.0 * 1024.0);
                                        lastTime = now;
                                        lastBytes = sentBytes;

                                        progress.TransferredBytes = sentBytes;
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
                                if (completionCallback != null) completionCallback(false, "接收方保存失败");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    progress.IsFailed = true;
                    progress.ErrorMessage = ex.Message;
                    if (progressCallback != null) progressCallback(progress);
                    if (completionCallback != null) completionCallback(false, "发送失败: " + ex.Message);
                }
            }) { IsBackground = true }.Start();
        }

        #endregion
    }
}
