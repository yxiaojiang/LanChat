using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LanChat
{
    /// <summary>
    /// 全局应用持久化配置服务
    /// 解决软件升级、覆盖安装后用户昵称、偏好设置、字体偏好与聊天记录存储路径丢失的问题
    /// 遵循 Windows 应用程序标准：%AppData%\LanChat\settings.json
    /// 同时支持便携模式：同级目录下若存在 settings.json 则优先使用
    /// </summary>
    public static class AppSettings
    {
        private static readonly object _lock = new object();
        private static string _configPath;

        // =========================================================================
        // 公共持久化配置属性
        // =========================================================================

        /// <summary>本机用户自定义个性昵称</summary>
        public static string NickName { get; set; }

        /// <summary>是否自动接收来自好友的文件</summary>
        public static bool AutoAcceptFiles { get; set; }

        /// <summary>文件接收保存的默认物理目录</summary>
        public static string SaveDirectory { get; set; }

        /// <summary>全局 UI 界面字体族名称</summary>
        public static string UiFontFamily { get; set; }

        /// <summary>全局 UI 界面字体大小（pt）</summary>
        public static float UiFontSize { get; set; }

        /// <summary>聊天气泡正文字体族名称</summary>
        public static string ChatFontFamily { get; set; }

        /// <summary>聊天气泡正文字体大小（pt）</summary>
        public static float ChatFontSize { get; set; }

        /// <summary>聊天记录 SQLite/JSON 存储自定义物理目录</summary>
        public static string ChatHistoryDirectory { get; set; }

        // =========================================================================
        // 静态初始化与配置加载/保存
        // =========================================================================

        static AppSettings()
        {
            AutoAcceptFiles = true;
            UiFontFamily = "Microsoft YaHei UI";
            UiFontSize = 10.0f;
            ChatFontFamily = "Microsoft YaHei UI";
            ChatFontSize = 10.5f;

            try
            {
                // 1. 优先检查可执行文件同级目录 (便携模式)
                string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                if (File.Exists(localPath))
                {
                    _configPath = localPath;
                }
                else
                {
                    // 2. 默认使用用户标准配置漫游目录 (%AppData%\LanChat\settings.json)
                    // 无论 MSI 升级卸载还是热覆盖，该目录均受操作系统保护永久保留
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string configDir = Path.Combine(appData, "LanChat");
                    _configPath = Path.Combine(configDir, "settings.json");
                }
            }
            catch
            {
                _configPath = "settings.json";
            }
        }

        /// <summary>
        /// 从配置文件读取并反序列化用户持久化配置项
        /// </summary>
        public static void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_configPath)) return;

                    string json = File.ReadAllText(_configPath, Encoding.UTF8);
                    if (string.IsNullOrEmpty(json)) return;

                    Match mNick = Regex.Match(json, "\"NickName\"\\s*:\\s*\"([^\"]*)\"");
                    if (mNick.Success)
                    {
                        string nick = mNick.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(nick))
                        {
                            NickName = UnescapeJson(nick);
                        }
                    }

                    Match mAuto = Regex.Match(json, "\"AutoAcceptFiles\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
                    if (mAuto.Success)
                    {
                        AutoAcceptFiles = bool.Parse(mAuto.Groups[1].Value);
                    }

                    Match mSaveDir = Regex.Match(json, "\"SaveDirectory\"\\s*:\\s*\"([^\"]*)\"");
                    if (mSaveDir.Success)
                    {
                        SaveDirectory = UnescapeJson(mSaveDir.Groups[1].Value.Trim());
                    }

                    Match mUiFont = Regex.Match(json, "\"UiFontFamily\"\\s*:\\s*\"([^\"]*)\"");
                    if (mUiFont.Success)
                    {
                        string val = UnescapeJson(mUiFont.Groups[1].Value.Trim());
                        if (!string.IsNullOrEmpty(val)) UiFontFamily = val;
                    }

                    Match mUiSize = Regex.Match(json, "\"UiFontSize\"\\s*:\\s*([0-9]+(?:\\.[0-9]+)?)");
                    if (mUiSize.Success)
                    {
                        float sz;
                        if (float.TryParse(mUiSize.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out sz) && sz >= 8f && sz <= 24f)
                        {
                            UiFontSize = sz;
                        }
                    }

                    Match mChatFont = Regex.Match(json, "\"ChatFontFamily\"\\s*:\\s*\"([^\"]*)\"");
                    if (mChatFont.Success)
                    {
                        string val = UnescapeJson(mChatFont.Groups[1].Value.Trim());
                        if (!string.IsNullOrEmpty(val)) ChatFontFamily = val;
                    }

                    Match mChatSize = Regex.Match(json, "\"ChatFontSize\"\\s*:\\s*([0-9]+(?:\\.[0-9]+)?)");
                    if (mChatSize.Success)
                    {
                        float sz;
                        if (float.TryParse(mChatSize.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out sz) && sz >= 8f && sz <= 26f)
                        {
                            ChatFontSize = sz;
                        }
                    }

                    Match mHistDir = Regex.Match(json, "\"ChatHistoryDirectory\"\\s*:\\s*\"([^\"]*)\"");
                    if (mHistDir.Success)
                    {
                        string val = UnescapeJson(mHistDir.Groups[1].Value.Trim());
                        if (!string.IsNullOrEmpty(val)) ChatHistoryDirectory = val;
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 将当前内存中的用户偏好配置持久化序列化至本地 JSON 文件
        /// </summary>
        public static void Save()
        {
            lock (_lock)
            {
                try
                {
                    string dir = Path.GetDirectoryName(_configPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("{");
                    sb.AppendLine(string.Format("  \"NickName\": \"{0}\",", EscapeJson(NickName ?? string.Empty)));
                    sb.AppendLine(string.Format("  \"AutoAcceptFiles\": {0},", AutoAcceptFiles ? "true" : "false"));
                    sb.AppendLine(string.Format("  \"SaveDirectory\": \"{0}\",", EscapeJson(SaveDirectory ?? string.Empty)));
                    sb.AppendLine(string.Format("  \"UiFontFamily\": \"{0}\",", EscapeJson(UiFontFamily ?? "Microsoft YaHei UI")));
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"UiFontSize\": {0:0.0},", UiFontSize));
                    sb.AppendLine(string.Format("  \"ChatFontFamily\": \"{0}\",", EscapeJson(ChatFontFamily ?? "Microsoft YaHei UI")));
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"ChatFontSize\": {0:0.0},", ChatFontSize));
                    sb.AppendLine(string.Format("  \"ChatHistoryDirectory\": \"{0}\"", EscapeJson(ChatHistoryDirectory ?? string.Empty)));
                    sb.AppendLine("}");

                    File.WriteAllText(_configPath, sb.ToString(), Encoding.UTF8);
                }
                catch { }
            }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string UnescapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
