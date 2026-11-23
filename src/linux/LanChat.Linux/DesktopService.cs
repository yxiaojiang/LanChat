using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LanChat.Linux
{
    /// <summary>
    /// 将桌面指令和共享网络事件转换为 UTF-8 JSON 行。依赖继承管道和 Linux 配置锁，
    /// 每个进程只有一个写入者，父进程关闭管道时退出并释放网络。
    /// </summary>
    internal sealed class DesktopService
    {
        private const int Version = 2;
        private readonly NetworkService _network;
        private readonly LinuxProfile _profile;
        private readonly Channel<Func<JsonObject>> _output = Channel.CreateUnbounded<Func<JsonObject>>(
            new UnboundedChannelOptions { SingleReader = true });
        private readonly object _gate = new object();
        private readonly Dictionary<(bool Sending, string Id, string Chat), JsonObject> _progress = new();
        private readonly SessionManager _sessions = new();
        private long _sequence;
        private bool _stopping;

        private DesktopService(NetworkService network, LinuxProfile profile)
        {
            _network = network;
            _profile = profile;
            network.OnLogMessage += Console.Error.WriteLine;
            network.OnPeerJoined += OnPeer;
            network.OnPeerUpdated += OnPeer;
            network.OnPeerLeft += _ => Publish("peers");
            network.OnMessageReceived += OnMessage;
            network.OnFileReceived += OnMessage;
            network.OnFileProgress += value => OnProgress(value, null);
        }

        internal static int Run()
        {
            try
            {
                using var profile = new LinuxProfile();
                using var network = profile.CreateNetwork();
                ChatHistoryService.ProfileStorageDirectory = profile.HistoryDirectory;
                return new DesktopService(network, profile).RunPipes().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private async Task<int> RunPipes()
        {
            using var input = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
            _sessions.LoadPersistedHistories();
            _network.Start();

            // 启动时网络回调可能已入队，就绪帧必须先写。
            await output.WriteLineAsync(new JsonObject
            {
                ["event"] = "ready", ["version"] = Version, ["udpPort"] = NetworkService.UDP_PORT,
                ["tcpPort"] = _network.LocalTcpPort
            }.ToJsonString());
            Task writer = WriteOutput(output);
            try
            {
                while (true)
                {
                    Task<string> read = input.ReadLineAsync();
                    if (await Task.WhenAny(read, writer) == writer)
                    {
                        await writer;
                        break;
                    }
                    string line = await read;
                    if (line == null || ProcessLine(line)) break;
                }
            }
            finally
            {
                // 等写入中的会话结束，再停止接收回调，以免释放配置锁后仍写用户数据。
                lock (_gate) _stopping = true;
                _output.Writer.TryComplete();
            }
            await writer;
            return 0;
        }

        private async Task WriteOutput(StreamWriter output)
        {
            await foreach (var makeFrame in _output.Reader.ReadAllAsync())
                await output.WriteLineAsync(makeFrame().ToJsonString());
        }

        private bool ProcessLine(string line)
        {
            string id = null;
            try
            {
                var request = JsonNode.Parse(line)?.AsObject() ?? throw new JsonException("需要请求对象");
                id = Required(request, "id", 100);
                switch (Required(request, "method", 32))
                {
                    case "state":
                        lock (_gate)
                        {
                            Reply(id, new JsonObject
                            {
                                ["id"] = _network.LocalId, ["nick"] = _network.NickName,
                                ["ip"] = _network.GetPrimaryPhysicalIp().ToString(), ["tcpPort"] = _network.LocalTcpPort,
                                ["peers"] = PeerList(),
                                ["sequence"] = _sequence,
                                ["sessions"] = new JsonArray(_sessions.GetAllSessionKeys().Select(key => new JsonObject
                                {
                                    ["key"] = key, ["nick"] = _sessions.GetArchivedNick(key),
                                    ["summary"] = _sessions.GetLastMessageSnippet(key)
                                }).ToArray())
                            });
                        }
                        break;
                    case "history":
                        lock (_gate)
                        {
                            string key = Required(request, "chat", 256);
                            Reply(id, new JsonObject
                            {
                                ["sequence"] = _sequence,
                                ["messages"] = new JsonArray(_sessions.GetMessages(key).TakeLast(2000)
                                    .Select(message => MessageFrame(message, key, null, 0)).ToArray())
                            });
                        }
                        break;
                    case "peers":
                        Reply(id, PeerList());
                        break;
                    case "settings":
                        Reply(id, SettingsFrame());
                        break;
                    case "save-settings":
                        Reply(id, SaveSettings(request));
                        break;
                    case "refresh":
                        _network.BroadcastPresence("HELLO");
                        Reply(id, PeerList());
                        break;
                    case "text":
                        string chat = Required(request, "chat", 256);
                        string message = Required(request, "text", 32768);
                        var targets = chat == "BROADCAST" ? _network.GetActivePeers() : new List<PeerInfo> { FindPeer(chat) };
                        _ = SendText(id, chat, message, targets);
                        break;
                    case "file":
                        SendFile(id, FindPeer(Required(request, "chat", 256)), Required(request, "path", 4096));
                        break;
                    case "quit":
                        Reply(id, null);
                        return true;
                    default:
                        throw new InvalidOperationException("未知桌面操作");
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or JsonException or
                ArgumentException or UnauthorizedAccessException)
            {
                if (id != null) Fail(id, ex.Message);
                else Console.Error.WriteLine(ex.Message);
            }
            return false;
        }

        private static string Required(JsonObject request, string name, int limit)
        {
            if (request[name] is JsonValue value && value.TryGetValue(out string text) &&
                !string.IsNullOrWhiteSpace(text) && text.Length <= limit) return text;
            throw new ArgumentException("无效的 " + name);
        }

        private JsonObject SettingsFrame() => new()
        {
            ["nickname"] = _network.NickName,
            ["autoAcceptFiles"] = _network.AutoAcceptFiles,
            ["downloadDirectory"] = _profile.DownloadDirectory,
            ["historyDirectory"] = _profile.HistoryDirectory,
            ["settingsPath"] = _profile.SettingsPath
        };

        private JsonObject SaveSettings(JsonObject request)
        {
            string nickname = Required(request, "nickname", 80).Trim();
            bool autoAccept = request["autoAcceptFiles"]?.GetValue<bool>() ??
                throw new ArgumentException("缺少文件自动接收选项");
            bool confirm = request["confirmOverride"]?.GetValue<bool>() ?? false;
            string directory = request["downloadDirectory"] == null ? null : Required(request, "downloadDirectory", 4096);
            string historyDirectory = request["historyDirectory"] == null ? null : Required(request, "historyDirectory", 4096);
            // 复制历史与切换写入位置期间，接收线程等待同一把锁。
            lock (_gate)
            {
                if (!_profile.SaveSettings(nickname, autoAccept, directory, confirm, historyDirectory))
                    return new JsonObject
                    {
                        ["backupPath"] = _profile.BackupPath,
                        ["overridePath"] = _profile.OverridePath
                    };
                ChatHistoryService.ProfileStorageDirectory = _profile.HistoryDirectory;
            }
            bool changedNick = _network.NickName != nickname;
            _network.NickName = nickname;
            _network.AutoAcceptFiles = _profile.AutoAcceptFiles;
            _network.SaveDirectory = _profile.DownloadDirectory;
            if (changedNick) _network.BroadcastPresence("HELLO");
            return SettingsFrame();
        }

        private JsonArray PeerList() => new JsonArray(_network.GetActivePeers().OrderBy(peer => peer.LastSeen).Select(peer => new JsonObject
        {
            ["id"] = peer.Id, ["nick"] = peer.NickName, ["ip"] = peer.IP.ToString(),
            ["port"] = peer.TcpPort, ["host"] = peer.HostName,
            ["key"] = SessionManager.GetLanKey(peer.Id, peer.IP)
        }).ToArray());

        private PeerInfo FindPeer(string id) => _network.GetActivePeers().Find(peer => peer.Id == id)
            ?? throw new InvalidOperationException("节点当前离线");

        private async Task SendText(string id, string chat, string text, List<PeerInfo> targets)
        {
            if (targets.Count == 0)
            {
                Fail(id, "没有在线节点");
                return;
            }
            bool broadcast = chat == "BROADCAST";
            bool[] acks = await Task.WhenAll(targets.Select(peer =>
                Task.Run(() => _network.SendTextMessage(peer, text, broadcast))));
            int confirmed = acks.Count(ack => ack);
            if (confirmed > 0)
                OnMessage(new ChatMessage
                {
                    IsSelf = true, IsBroadcast = broadcast, SenderId = _network.LocalId, SenderNick = _network.NickName,
                    TargetId = broadcast ? null : chat,
                    RemoteAddress = broadcast ? null : targets[0].IP, PeerNick = broadcast ? null : targets[0].NickName,
                    Content = text, Timestamp = DateTime.Now
                }, id);
            if (confirmed == 0) Fail(id, "对端未确认消息");
            else Reply(id, new JsonObject { ["confirmed"] = confirmed, ["total"] = targets.Count });
        }

        private void SendFile(string id, PeerInfo target, string path)
        {
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
                throw new IOException("选中的文件已不可用");
            long size = new FileInfo(path).Length;
            _network.SendFileAsync(target, path, value => OnProgress(value, id), (success, reason) =>
            {
                if (!success)
                {
                    Fail(id, reason);
                    return;
                }
                OnMessage(new ChatMessage
                {
                    IsSelf = true, IsFile = true, TargetId = target.Id, SenderId = _network.LocalId, SenderNick = _network.NickName,
                    PeerNick = target.NickName, RemoteAddress = target.IP,
                    Content = Path.GetFileName(path), FilePath = path, FileName = Path.GetFileName(path), FileSize = size,
                    Timestamp = DateTime.Now
                }, id);
                Reply(id, null);
            });
        }

        private void OnMessage(ChatMessage value) => OnMessage(value, null);

        private void OnMessage(ChatMessage value, string operation)
        {
            lock (_gate)
            {
                if (_stopping) return;
                string key = value.IsBroadcast ? SessionManager.BROADCAST_KEY
                    : SessionManager.GetLanKey(value.IsSelf ? value.TargetId : value.SenderId, value.RemoteAddress);
                key = _sessions.ResolveKey(key);
                _sessions.AddMessage(key, value, persist: false);
                string failure = null;
                try { ChatHistoryService.AppendMessage(key, value); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    failure = "无法保存聊天记录：" + ex.Message;
                    Console.Error.WriteLine(failure);
                }
                var frame = MessageFrame(value, key, operation, ++_sequence);
                if (failure != null) frame["storageError"] = failure;
                Publish("message", frame);
            }
        }

        private void OnPeer(PeerInfo peer)
        {
            lock (_gate)
            {
                if (_sessions.LinkPeer(peer.Id, peer.IP))
                    Publish("linked", new JsonObject
                    {
                        ["oldKey"] = peer.Id, ["key"] = SessionManager.GetLanKey(peer.Id, peer.IP)
                    });
                Publish("peers");
            }
        }

        private static JsonObject MessageFrame(ChatMessage value, string key, string operation, long sequence) => new()
        {
            ["sequence"] = sequence, ["operation"] = operation, ["chat"] = key,
            ["nick"] = value.SenderNick, ["peerNick"] = value.PeerNick,
            ["text"] = value.Content, ["self"] = value.IsSelf,
            ["time"] = value.Timestamp.ToString("HH:mm:ss"),
            ["file"] = value.IsFile, ["name"] = value.FileName, ["size"] = value.FileSize,
            ["exists"] = value.IsFile && File.Exists(value.FilePath)
        };

        private void OnProgress(FileTransferProgress value, string operation)
        {
            lock (_gate)
            {
                if (_stopping) return;
                string chat = _sessions.ResolveKey(SessionManager.GetLanKey(value.TransferPeerId, value.TransferPeerAddress));
                var key = (value.IsSending, value.TransferId, chat);
                var frame = new JsonObject
                {
                    ["event"] = "progress", ["operation"] = operation, ["transfer"] = value.TransferId,
                    ["chat"] = chat, ["peerNick"] = value.PeerNick,
                    ["name"] = value.FileName, ["sending"] = value.IsSending, ["received"] = value.TransferredBytes,
                    ["total"] = value.TotalBytes, ["completed"] = value.IsCompleted, ["failed"] = value.IsFailed,
                    ["error"] = value.ErrorMessage
                };
                if (value.IsCompleted || value.IsFailed)
                {
                    Enqueue(frame);
                    return;
                }
                bool enqueue = !_progress.ContainsKey(key);
                _progress[key] = frame;
                if (enqueue)
                    _output.Writer.TryWrite(() =>
                    {
                        lock (_gate)
                        {
                            _progress.Remove(key, out JsonObject latest);
                            return latest;
                        }
                    });
            }
        }

        private void Publish(string name, JsonNode data = null) => Enqueue(new JsonObject { ["event"] = name, ["data"] = data });
        private void Reply(string id, JsonNode result) => Enqueue(new JsonObject { ["id"] = id, ["result"] = result });
        private void Fail(string id, string error) => Enqueue(new JsonObject { ["id"] = id, ["error"] = error });
        private void Enqueue(JsonObject frame) => _output.Writer.TryWrite(() => frame);
    }
}
