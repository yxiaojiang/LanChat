using System;
using System.Net;

namespace LanChat
{
    public class PeerInfo
    {
        public string Id { get; set; }
        public string NickName { get; set; }
        public string HostName { get; set; }
        public IPAddress IP { get; set; }
        public int TcpPort { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsManual { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastMessageTime { get; set; }
        public int UnreadCount { get; set; }

        public string DisplayText
        {
            get
            {
                string name = string.IsNullOrEmpty(NickName) ? "未知用户" : NickName;
                return string.Format("{0} ({1} - {2}:{3})", name, HostName, IP, TcpPort);
            }
        }

        public override string ToString()
        {
            return DisplayText;
        }
    }

    public class ChatMessage
    {
        public string SenderId { get; set; }
        public string SenderNick { get; set; }
        public string TargetId { get; set; } // Null or empty = broadcast
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsSelf { get; set; }
        public bool IsBroadcast { get; set; }
        public bool IsSystem { get; set; }
        public bool IsFile { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
    }

    public class FileTransferProgress
    {
        public string TransferId { get; set; }
        public string FileName { get; set; }
        public long TotalBytes { get; set; }
        public long TransferredBytes { get; set; }
        public double SpeedMBps { get; set; }
        public bool IsSending { get; set; }
        public string PeerNick { get; set; }
        public string LocalPath { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsFailed { get; set; }
        public string ErrorMessage { get; set; }

        public int ProgressPercentage
        {
            get
            {
                if (TotalBytes <= 0) return 0;
                int p = (int)((TransferredBytes * 100) / TotalBytes);
                if (p > 100) return 100;
                if (p < 0) return 0;
                return p;
            }
        }
    }
}
