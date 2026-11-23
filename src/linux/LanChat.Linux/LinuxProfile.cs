using System;
using System.Collections.Generic;
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
        private FileStream _overrideLock;
        private readonly string _lockDirectory;
        private readonly string _overridePath;
        private bool _awaitingConfirmation;
        internal string SettingsPath { get; private set; }
        internal string DownloadDirectory { get; private set; }
        internal string HistoryDirectory { get; private set; }
        internal string OverridePath => _overridePath;
        internal string BackupPath => _overridePath + ".before-electron.bak";
        internal bool AutoAcceptFiles =>
            (_settings["AutoAcceptFiles"] ?? _settings["auto_accept"]) is not JsonValue accept ||
            !accept.TryGetValue(out bool enabled) || enabled;
        internal int ThemeMode => ReadPreset("ThemeMode", 3);
        internal int AccentPreset => ReadPreset("AccentPreset", 5);
        internal string UiFontFamily => ReadNonEmpty("UiFontFamily") ?? "Microsoft YaHei UI";
        internal double UiFontSize => ReadSize("UiFontSize", 8.0, 24.0, 10.0);
        internal string ChatFontFamily => ReadNonEmpty("ChatFontFamily") ?? "Microsoft YaHei UI";
        internal double ChatFontSize => ReadSize("ChatFontSize", 8.0, 26.0, 10.5);
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

            _lockDirectory = Path.Combine(data, "locks");
            Directory.CreateDirectory(_lockDirectory);
            string key = LockKey(SettingsPath);
            _overridePath = Path.Combine(data, "settings-overrides", key, "settings.json");
            var handle = LockSettings(SettingsPath);
            try
            {
                if (File.Exists(_overridePath))
                {
                    // 原配置锁继续持有，备用配置也使用同一套锁，防止其他入口绕开互斥。
                    _overrideLock = LockSettings(_overridePath);
                    SettingsPath = _overridePath;
                }
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
                _overrideLock?.Dispose();
                handle.Dispose();
                throw;
            }
        }

        internal NetworkService CreateNetwork(string nickname = null)
        {
            var service = new NetworkService(nickname ?? ReadString("NickName") ?? ReadString("nickname"), DownloadDirectory);
            service.AutoAcceptFiles = AutoAcceptFiles;
            return service;
        }

        internal bool SaveSettings(string nickname, bool? autoAccept = null, string downloadDirectory = null,
            bool confirmOverride = false, string historyDirectory = null,
            int? themeMode = null, int? accentPreset = null,
            string uiFontFamily = null, double? uiFontSize = null,
            string chatFontFamily = null, double? chatFontSize = null)
        {
            if (string.IsNullOrWhiteSpace(nickname) || nickname.Length > 80 || nickname.Contains('|') ||
                nickname.Contains('\r') || nickname.Contains('\n'))
                throw new ArgumentException("昵称须为 1 至 80 个字符，且不能包含竖线或换行");
            var next = (JsonObject)_settings.DeepClone();
            next["NickName"] = nickname;
            if (autoAccept.HasValue) next["AutoAcceptFiles"] = autoAccept.Value;
            if (themeMode.HasValue)
            {
                if (themeMode.Value < 0 || themeMode.Value >= 3) throw new ArgumentException("主题模式无效");
                next["ThemeMode"] = themeMode.Value;
            }
            if (accentPreset.HasValue)
            {
                if (accentPreset.Value < 0 || accentPreset.Value >= 5) throw new ArgumentException("强调色预设无效");
                next["AccentPreset"] = accentPreset.Value;
            }
            if (uiFontFamily != null)
            {
                string font = uiFontFamily.Trim();
                if (string.IsNullOrEmpty(font) || font.Length > 128 || font.Contains('\r') || font.Contains('\n'))
                    throw new ArgumentException("主界面字体样式无效");
                next["UiFontFamily"] = font;
            }
            if (uiFontSize.HasValue)
            {
                if (double.IsNaN(uiFontSize.Value) || double.IsInfinity(uiFontSize.Value) ||
                    uiFontSize.Value < 8.0 || uiFontSize.Value > 24.0)
                    throw new ArgumentException("主界面字号无效");
                next["UiFontSize"] = Math.Round(uiFontSize.Value, 1);
            }
            if (chatFontFamily != null)
            {
                string font = chatFontFamily.Trim();
                if (string.IsNullOrEmpty(font) || font.Length > 128 || font.Contains('\r') || font.Contains('\n'))
                    throw new ArgumentException("聊天框字体样式无效");
                next["ChatFontFamily"] = font;
            }
            if (chatFontSize.HasValue)
            {
                if (double.IsNaN(chatFontSize.Value) || double.IsInfinity(chatFontSize.Value) ||
                    chatFontSize.Value < 8.0 || chatFontSize.Value > 26.0)
                    throw new ArgumentException("聊天框字号无效");
                next["ChatFontSize"] = Math.Round(chatFontSize.Value, 1);
            }
            if (downloadDirectory != null && downloadDirectory != DownloadDirectory)
            {
                if (!Path.IsPathFullyQualified(downloadDirectory) || !TryDirectory(downloadDirectory, writable: true))
                    throw new IOException("文件接收目录不可写：" + downloadDirectory);
                next["SaveDirectory"] = downloadDirectory;
            }
            if (historyDirectory != null)
            {
                if (!Path.IsPathFullyQualified(historyDirectory))
                    throw new ArgumentException("聊天记录目录必须是绝对路径");
                historyDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(historyDirectory));
                if (historyDirectory == Path.TrimEndingDirectorySeparator(HistoryDirectory)) historyDirectory = null;
                else next["ChatHistoryDirectory"] = historyDirectory;
            }
            if (confirmOverride)
            {
                if (!_awaitingConfirmation) throw new InvalidOperationException("请先保存设置并确认备用位置");
                var alternate = LockSettings(_overridePath);
                try { CommitSettings(_overridePath, next, historyDirectory, overwrite: false); }
                catch
                {
                    alternate.Dispose();
                    throw;
                }
                _overrideLock = alternate;
                SettingsPath = _overridePath;
                _awaitingConfirmation = false;
            }
            else
            {
                try
                {
                    if (File.Exists(SettingsPath))
                    {
                        using (File.Open(SettingsPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { }
                        Backup(SettingsPath, SettingsPath + ".before-electron.bak");
                    }
                    CommitSettings(SettingsPath, next, historyDirectory);
                    _awaitingConfirmation = false;
                }
                catch (Exception ex) when (SettingsPath != _overridePath && File.Exists(SettingsPath) &&
                    (ex is UnauthorizedAccessException || ex is IOException { HResult: 30 }))
                {
                    // Linux 的 EROFS 为 30。先把原字节保全到用户目录，再询问是否启用备用位置。
                    Directory.CreateDirectory(Path.GetDirectoryName(_overridePath));
                    Backup(SettingsPath, BackupPath);
                    _awaitingConfirmation = true;
                    return false;
                }
            }
            _settings = next;
            if (downloadDirectory != null) DownloadDirectory = downloadDirectory;
            if (historyDirectory != null) HistoryDirectory = historyDirectory;
            return true;
        }

        private void CommitSettings(string path, JsonObject settings, string historyDirectory, bool overwrite = true)
        {
            var copied = new List<string>();
            try
            {
                if (historyDirectory != null) CopyHistory(historyDirectory, copied);
                WriteAtomic(path, stream => WriteJson(stream, settings), overwrite);
            }
            catch
            {
                foreach (string file in copied)
                {
                    try { File.Delete(file); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Console.Error.WriteLine("未能清理聊天记录副本：" + ex.Message);
                    }
                }
                throw;
            }
        }

        private void CopyHistory(string destination, List<string> copied)
        {
            try
            {
                if (!TryDirectory(destination, writable: true))
                    throw new IOException("目标目录不可写");
                // 仅在目录中没有历史记录时迁移，源文件留给旧版回退使用。
                using var existing = Directory.EnumerateFileSystemEntries(destination, "history_*.jsonl").GetEnumerator();
                if (existing.MoveNext()) throw new IOException("目标目录已有聊天记录，请选择其他目录");
                if (!Directory.Exists(HistoryDirectory)) return;
                foreach (string source in Directory.EnumerateFiles(HistoryDirectory, "history_*.jsonl"))
                {
                    string file = Path.Combine(destination, Path.GetFileName(source));
                    CopyAtomic(source, file);
                    copied.Add(file);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException("无法迁移聊天记录：" + ex.Message, ex);
            }
        }

        private static string LockKey(string path) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)));

        private FileStream LockSettings(string path)
        {
            if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Linux 配置锁需要 Linux");
            var handle = File.Open(Path.Combine(_lockDirectory, LockKey(path)), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.ReadWrite);
            try
            {
                handle.Lock(0, 1);
                return handle;
            }
            catch (IOException ex)
            {
                handle.Dispose();
                throw new IOException("同一配置正在被另一个 LanChat 进程使用：" + path, ex);
            }
        }

        private static void Backup(string source, string backup)
        {
            if (File.Exists(backup)) return;
            CopyAtomic(source, backup);
        }

        private static void CopyAtomic(string source, string destination)
        {
            WriteAtomic(destination, stream =>
            {
                using var original = File.OpenRead(source);
                original.CopyTo(stream);
            }, overwrite: false);
        }

        private static void WriteJson(Stream stream, JsonObject settings)
        {
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            settings.WriteTo(writer);
            writer.Flush();
        }

        private static void WriteAtomic(string path, Action<FileStream> write, bool overwrite = true)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var options = new FileStreamOptions
                {
                    Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None
                };
                if (OperatingSystem.IsLinux()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                using (var stream = new FileStream(temporary, options))
                {
                    write(stream);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporary, path, overwrite);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private string ReadString(string key) =>
            _settings[key] is JsonValue value && value.TryGetValue(out string text) ? text : null;

        private string ReadNonEmpty(string key)
        {
            string text = ReadString(key)?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private int ReadPreset(string key, int count)
        {
            if (_settings[key] is JsonValue value)
            {
                if (value.TryGetValue(out int iv) && iv >= 0 && iv < count) return iv;
                if (value.TryGetValue(out double dv) && dv >= 0 && dv < count && Math.Abs(dv - Math.Truncate(dv)) < 1e-6) return (int)dv;
            }
            return 0;
        }

        private double ReadSize(string key, double min, double max, double fallback)
        {
            if (_settings[key] is JsonValue value && value.TryGetValue(out double sz) &&
                !double.IsNaN(sz) && !double.IsInfinity(sz) && sz >= min && sz <= max)
                return Math.Round(sz, 1);
            return fallback;
        }

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
        public void Dispose()
        {
            _overrideLock?.Dispose();
            _lock.Dispose();
        }
    }
}
