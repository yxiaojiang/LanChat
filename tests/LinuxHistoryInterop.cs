using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using LanChat;

/// <summary>用 Windows 实际历史读写器核对 Python JSONL 兼容性，配置指向独立临时目录。</summary>
internal static class LinuxHistoryInterop
{
    private const string Key = "LAN-192.168.40.2";
    private const string Content = "中文\"文件\"\\路径\n下一行\r\t\b\f\u0001";
    private const string FilePath = "C:\\接收\\文件.txt";

    /// <summary>读取 Python 样本并验证字段，将 Windows 样本写回同一个会话。</summary>
    /// <param name="args">隔离历史目录。</param>
    /// <returns>兼容时返回 0，字段不同时返回 1。</returns>
    public static int Main(string[] args)
    {
        AppSettings.ChatHistoryDirectory = args[0];
        ChatMessage item = ChatHistoryService.LoadHistory(Key).Single();
        if (item.Content != Content || item.SenderNick != "节点甲" || item.FilePath != FilePath ||
            !item.IsFile || item.FileSize != 312 || item.Timestamp.Millisecond != 123 ||
            item.RemoteAddress == null || item.RemoteAddress.ToString() != "192.168.40.2")
        {
            return 1;
        }

        ChatHistoryService.SaveMessage(Key, new ChatMessage
        {
            SenderId = "windows",
            SenderNick = "节点乙",
            Content = Content,
            Timestamp = new DateTime(2026, 11, 19, 12, 34, 57, 456),
            IsFile = true,
            FilePath = FilePath,
            RemoteAddress = IPAddress.Parse("192.168.40.2"),
        });
        File.AppendAllText(Path.Combine(args[0], "history_BROADCAST.jsonl"),
            "{\"Content\":\"旧群聊条目\",\"IsBroadcast\":true}\r\n", Encoding.UTF8);
        return 0;
    }
}
