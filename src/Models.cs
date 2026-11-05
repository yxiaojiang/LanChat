using System;
using System.Net;

namespace LanChat
{
    // =========================================================================
    // 局域网节点对端模型
    // =========================================================================

    /// <summary>
    /// 局域网在线节点实体，封装对端的主机名、IP、端口及会话状态
    /// </summary>
    public class PeerInfo
    {
        /// <summary>对端节点唯一标识（形如 "IP:Port"）</summary>
        public string Id { get; set; }

        /// <summary>用户设置的个性昵称</summary>
        public string NickName { get; set; }

        /// <summary>计算机主机名 (MachineName)</summary>
        public string HostName { get; set; }

        /// <summary>局域网 IPv4 地址</summary>
        public IPAddress IP { get; set; }

        /// <summary>TCP 数据与文件监听端口</summary>
        public int TcpPort { get; set; }

        /// <summary>最近一次收到存活心跳的时间戳</summary>
        public DateTime LastSeen { get; set; }

        /// <summary>是否为用户通过 IP 手动添加的直连节点</summary>
        public bool IsManual { get; set; }

        /// <summary>最新接收的一条消息文本摘要</summary>
        public string LastMessage { get; set; }

        /// <summary>最新一条消息的接收时间</summary>
        public DateTime LastMessageTime { get; set; }

        /// <summary>未读消息条数计数器</summary>
        public int UnreadCount { get; set; }

        /// <summary>
        /// 用于在会话列表或下拉中展示的格式化名称
        /// </summary>
        public string DisplayText
        {
            get
            {
                string name = string.IsNullOrEmpty(NickName) ? "未知用户" : NickName;
                return string.Format("{0} ({1} - {2}:{3})", name, HostName, IP, TcpPort);
            }
        }

        /// <summary>
        /// 转换为文本显示
        /// </summary>
        /// <returns>展示名称</returns>
        public override string ToString()
        {
            return DisplayText;
        }
    }

    // =========================================================================
    // 聊天消息实体模型
    // =========================================================================

    /// <summary>
    /// 聊天消息实体，支持单聊、广播、文件传输卡片及系统通知等多种形态
    /// </summary>
    public class ChatMessage
    {
        /// <summary>发送方节点唯一标识</summary>
        public string SenderId { get; set; }

        /// <summary>发送方当时显示的昵称</summary>
        public string SenderNick { get; set; }

        /// <summary>目标接收方标识（为 null 或空字符串时表示局域网广播）</summary>
        public string TargetId { get; set; }

        /// <summary>消息正文文本内容</summary>
        public string Content { get; set; }

        /// <summary>消息产生或接收的时间戳</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>是否为本机发出的消息（决定聊天气泡左右排版）</summary>
        public bool IsSelf { get; set; }

        /// <summary>是否为局域网全员广播消息</summary>
        public bool IsBroadcast { get; set; }

        /// <summary>是否为系统居中状态通知（如上线、离线、握手提示）</summary>
        public bool IsSystem { get; set; }

        /// <summary>是否为文件传输卡片消息</summary>
        public bool IsFile { get; set; }

        /// <summary>文件名称（含扩展名）</summary>
        public string FileName { get; set; }

        /// <summary>本地物理文件路径</summary>
        public string FilePath { get; set; }

        /// <summary>文件大小（字节数）</summary>
        public long FileSize { get; set; }
    }

    // =========================================================================
    // 文件传输实时进度模型
    // =========================================================================

    /// <summary>
    /// 文件传输实时进度与状态实体
    /// </summary>
    public class FileTransferProgress
    {
        /// <summary>传输会话唯一标识 UUID</summary>
        public string TransferId { get; set; }

        /// <summary>传输文件名称</summary>
        public string FileName { get; set; }

        /// <summary>文件总字节数</summary>
        public long TotalBytes { get; set; }

        /// <summary>当前已传输字节数</summary>
        public long TransferredBytes { get; set; }

        /// <summary>瞬时传输速率 (MB/s)</summary>
        public double SpeedMBps { get; set; }

        /// <summary>是否处于本机发送状态（false 表示正在接收对端文件）</summary>
        public bool IsSending { get; set; }

        /// <summary>对端节点昵称</summary>
        public string PeerNick { get; set; }

        /// <summary>本地保存或读取的目标物理路径</summary>
        public string LocalPath { get; set; }

        /// <summary>传输是否已经顺利完毕</summary>
        public bool IsCompleted { get; set; }

        /// <summary>传输是否因网络或 IO 异常而失败</summary>
        public bool IsFailed { get; set; }

        /// <summary>错误详细异常信息</summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 传输百分比进度值 (0 ~ 100)
        /// </summary>
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
