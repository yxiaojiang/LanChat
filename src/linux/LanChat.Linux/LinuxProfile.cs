using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LanChat.Linux
{
    /// <summary>
    /// 按旧 Linux 启动入口选定配置和接收目录。依赖本地文件系统，
    /// 以配置路径为锁键，作用域覆盖一次 ELF 运行。
    /// </summary>
    internal sealed class LinuxProfile : IDisposable
    {
        private readonly FileStream _lock;
        internal string SettingsPath { get; }
        internal string DownloadDirectory { get; }
        internal string HistoryDirectory { get; }
        private JsonObject _settings;

        internal LinuxProfile()
        {
            string home = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrEmpty(home)) home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string data = Path.Combine(home, ".local", "share", "lanchat");
            string root = Environment.GetEnvironmentVariable("LANCHAT_ROOT") ?? AppContext.BaseDirectory;
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            bool installed = Environment.GetEnvironmentVariable("LANCHAT_INSTALLED") == "1" ||
                root.StartsWith("/usr/") || root.StartsWith("/opt/") || root.StartsWith("/etc/");
            SettingsPath = SelectSettings(root, home, data, installed);
            var settingsFile = new FileInfo(SettingsPath);
            if (settingsFile.Exists && settingsFile.ResolveLinkTarget(true) is FileInfo target) settingsFile = target;
            SettingsPath = settingsFile.FullName;

            string lockDir = Path.Combine(data, "locks");
            Directory.CreateDirectory(lockDir);
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SettingsPath)));
            var handle = File.Open(Path.Combine(lockDir, key), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            try
            {
                if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Linux 配置锁需要 Linux");
                try { handle.Lock(0, 1); }
                catch (IOException ex) { throw new IOException("同一配置正在被另一个 LanChat 进程使用：" + SettingsPath, ex); }
                _settings = ReadSettings(SettingsPath);
                string configured = ReadString("SaveDirectory");
                DownloadDirectory = string.IsNullOrWhiteSpace(configured)
                    ? SelectDownloads(root, home, data, installed)
                    : Path.GetFullPath(configured, root);
                string portableHistory = Path.Combine(root, "ChatHistory");
                string configuredHistory = ReadString("ChatHistoryDirectory");
                HistoryDirectory = !string.IsNullOrWhiteSpace(configuredHistory)
                    ? Path.GetFullPath(configuredHistory, root)
                    : !installed && (Directory.Exists(portableHistory) || TryDirectory(portableHistory, writable: true))
                        ? portableHistory : Path.Combine(data, "ChatHistory");
                _lock = handle;
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        internal NetworkService CreateNetwork(string nickname = null)
        {
            var service = new NetworkService(nickname ?? ReadString("NickName") ?? ReadString("nickname"), DownloadDirectory);
            if ((_settings["AutoAcceptFiles"] ?? _settings["auto_accept"]) is JsonValue accept && accept.TryGetValue(out bool enabled))
                service.AutoAcceptFiles = enabled;
            return service;
        }

        internal void SaveNickname(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname) || nickname.Length > 80 || nickname.Contains('|') ||
                nickname.Contains('\r') || nickname.Contains('\n'))
                throw new ArgumentException("昵称须为 1 至 80 个字符，且不能包含竖线或换行");
            var next = (JsonObject)_settings.DeepClone();
            next["NickName"] = nickname;
            string temporary = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                if (File.Exists(SettingsPath))
                {
                    // 原位置只读时停止写回。由用户确认备用位置，启动器不会代选。
                    using (File.Open(SettingsPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { }
                    string backup = SettingsPath + ".before-electron.bak";
                    if (!File.Exists(backup)) File.Copy(SettingsPath, backup);
                }
                var options = new FileStreamOptions
                {
                    Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None
                };
                if (OperatingSystem.IsLinux()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                using (var stream = new FileStream(temporary, options))
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    next.WriteTo(writer);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporary, SettingsPath, overwrite: true);
                _settings = next;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private string ReadString(string key) =>
            _settings[key] is JsonValue value && value.TryGetValue(out string text) ? text : null;

        private static JsonObject ReadSettings(string file)
        {
            if (!File.Exists(file)) return new JsonObject();
            string content = File.ReadAllText(file);
            return string.IsNullOrWhiteSpace(content) ? new JsonObject() :
                JsonNode.Parse(content) as JsonObject ?? throw new IOException("设置文件需要 JSON 对象：" + file);
        }

        private static string SelectSettings(string root, string home, string data, bool installed)
        {
            string adjacent = Path.Combine(root, "settings.json");
            if (File.Exists(adjacent)) return adjacent;
            if (!installed)
            {
                try
                {
                    using var created = File.Open(adjacent, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
                    return adjacent;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            string config = Path.Combine(home, ".config", "lanchat");
            if (!TryDirectory(config))
            {
                Directory.CreateDirectory(data);
                config = data;
            }
            return Path.Combine(config, "settings.json");
        }

        private static string SelectDownloads(string root, string home, string data, bool installed)
        {
            string portable = Path.Combine(root, "ReceivedFiles");
            if (!installed && TryDirectory(portable, writable: true)) return portable;
            string downloads = Path.Combine(home, "Downloads", "LanChat_Received");
            if (TryDirectory(downloads)) return downloads;
            string local = Path.Combine(data, "ReceivedFiles");
            if (TryDirectory(local)) return local;
            return Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "lanchat_" + Environment.UserName + "_received")).FullName;
        }

        private static bool TryDirectory(string path, bool writable = false)
        {
            try
            {
                Directory.CreateDirectory(path);
                if (writable)
                {
                    using var test = new FileStream(Path.Combine(path, ".probe-" + Guid.NewGuid().ToString("N")),
                        FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
                }
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        /// <summary>结束进程对所选数据配置的独占访问。</summary>
        public void Dispose() => _lock.Dispose();
    }
}
