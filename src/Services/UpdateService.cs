using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace LanChat
{
    #region Online Update (GitHub Releases Service)

    /// <summary>
    /// GitHub Releases 在线自动更新服务
    /// 支持双轨探测：GitHub REST API 优先（获取详细发布说明） + Web 302 重定向降级（免 60次/小时 API 限流）
    /// 微信暗黑风格更新向导弹窗与 MSI 流式下载及自动运行
    /// </summary>
    public static class UpdateService
    {
        public const string APP_VERSION = "2.0.0";
        public const string GITHUB_REPO = "yxiaojiang/LanChat";

        private static string ExtractJsonString(string json, string key)
        {
            var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"((?:\\\\\"|[^\"])*)\"");
            if (match.Success)
            {
                return match.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
            }
            return "";
        }

        public static void CheckForUpdates(Form owner, bool silentIfNotNew, Action onInstallExit = null)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string tagName = null;
                string releaseName = null;
                string htmlUrl = null;
                string body = null;
                string downloadUrl = "https://github.com/" + GITHUB_REPO + "/releases/latest/download/LanChat.msi";
                Exception lastEx = null;

                // 方案 1: 优先尝试 GitHub REST API 获取完整更新日志与附件列表
                try
                {
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
                    string url = "https://api.github.com/repos/" + GITHUB_REPO + "/releases/latest";
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.UserAgent = "LanChat-Client/" + APP_VERSION;
                    request.Timeout = 8000;

                    string json;
                    using (var response = request.GetResponse())
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        json = reader.ReadToEnd();
                    }

                    tagName = ExtractJsonString(json, "tag_name").TrimStart('v', 'V');
                    releaseName = ExtractJsonString(json, "name");
                    htmlUrl = ExtractJsonString(json, "html_url");
                    body = ExtractJsonString(json, "body").Replace("\\r\\n", Environment.NewLine).Replace("\\n", Environment.NewLine);

                    var assetMatch = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*LanChat\\.msi[^\"]*)\"");
                    if (assetMatch.Success)
                    {
                        downloadUrl = assetMatch.Groups[1].Value;
                    }
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }

                // 方案 2: 若 API 遇到 403 频率限制，降级尝试通过 Web 页面 302 重定向探测最新 Tag (无 60次/小时 API 限制)
                if (string.IsNullOrEmpty(tagName))
                {
                    try
                    {
                        string webUrl = "https://github.com/" + GITHUB_REPO + "/releases/latest";
                        var webReq = (HttpWebRequest)WebRequest.Create(webUrl);
                        webReq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                        webReq.AllowAutoRedirect = true;
                        webReq.Timeout = 8000;
                        using (var webResp = webReq.GetResponse())
                        {
                            string finalUrl = webResp.ResponseUri.ToString();
                            if (finalUrl.Contains("/releases/tag/"))
                            {
                                int idx = finalUrl.LastIndexOf("/releases/tag/");
                                tagName = finalUrl.Substring(idx + "/releases/tag/".Length).TrimStart('v', 'V');
                                releaseName = "LanChat v" + tagName;
                                htmlUrl = finalUrl;
                                body = "包含重要功能与稳定性改进，请点击【立即更新】下载升级。";
                                lastEx = null;
                            }
                        }
                    }
                    catch (Exception ex2)
                    {
                        if (lastEx == null) lastEx = ex2;
                    }
                }

                // 成功解析版本号
                if (!string.IsNullOrEmpty(tagName))
                {
                    try
                    {
                        Version currentVer = new Version(APP_VERSION);
                        Version latestVer;
                        string cleanTag = Regex.Match(tagName, @"\d+(\.\d+)+").Value;
                        if (!Version.TryParse(cleanTag, out latestVer))
                        {
                            latestVer = currentVer;
                        }

                        if (latestVer > currentVer)
                        {
                            SafeInvoke(owner, () => ShowUpdateDialog(owner, tagName, releaseName, body, downloadUrl, htmlUrl, onInstallExit));
                        }
                        else
                        {
                            if (!silentIfNotNew)
                            {
                                SafeInvoke(owner, () =>
                                {
                                    MessageBox.Show(owner, "当前已是最新版本 (v" + APP_VERSION + ")，无需更新。", "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                });
                            }
                        }
                        return;
                    }
                    catch { }
                }

                // 若失败且非静默模式，提供精准诊断信息
                if (!silentIfNotNew && lastEx != null)
                {
                    SafeInvoke(owner, () =>
                    {
                        string tip = lastEx.Message;
                        var webEx = lastEx as WebException;
                        if (webEx != null)
                        {
                            var resp = webEx.Response as HttpWebResponse;
                            if (resp != null)
                            {
                                if (resp.StatusCode == HttpStatusCode.Forbidden) // 403
                                {
                                    tip = "访问被禁止 (403 Forbidden)。\n\n可能原因：\n1. 当前 GitHub 仓库为「私有仓库 (Private)」，外部客户端未授权无法访问。\n   解决建议：请在 GitHub 仓库 Settings 中将仓库可见性调整为「Public (公开)」；\n2. 触发了 GitHub API 匿名请求频率限制 (60次/小时)，请稍后重试。";
                                }
                                else if (resp.StatusCode == HttpStatusCode.NotFound) // 404
                                {
                                    tip = "未在 GitHub 找到发布版本 (404 Not Found)。\n\n可能原因：\n1. 仓库尚未在 GitHub 创建 Release 发布版本（当前 Releases 列表为空）；\n2. 仓库当前设置为「私有仓库 (Private)」，未登录状态下不可见。\n\n解决建议：请前往 GitHub 仓库页面发布 Release，并将仓库设置为 Public。";
                                }
                            }
                        }
                        MessageBox.Show(owner, "检查新版本失败。\n\n" + tip, "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    });
                }
            });
        }

        public static void ShowUpdateDialog(Form owner, string version, string releaseTitle, string releaseNotes, string downloadUrl, string webUrl, Action onInstallExit = null)
        {
            Form dlg = new Form
            {
                Text = "发现新版本 - LanChat",
                Size = new Size(540, 440),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(32, 32, 33),
                ForeColor = Color.FromArgb(235, 235, 235),
                Font = new Font("Microsoft YaHei", 9f)
            };
            if (owner != null && owner.Icon != null) dlg.Icon = owner.Icon;

            Label lblTitle = new Label
            {
                Text = "🚀 发现 LanChat 新版本: v" + version,
                Font = new Font("Microsoft YaHei", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(7, 193, 96),
                Location = new Point(24, 20),
                AutoSize = true
            };

            Label lblSub = new Label
            {
                Text = string.Format("当前版本: v{0}  |  更新标题: {1}", APP_VERSION, string.IsNullOrEmpty(releaseTitle) ? "LanChat v" + version : releaseTitle),
                ForeColor = Color.FromArgb(160, 160, 160),
                Location = new Point(25, 48),
                AutoSize = true
            };

            Label lblNoteTitle = new Label
            {
                Text = "更新日志 / 发布说明:",
                ForeColor = Color.FromArgb(200, 200, 200),
                Location = new Point(25, 78),
                AutoSize = true
            };

            TextBox txtNotes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(44, 44, 46),
                ForeColor = Color.FromArgb(220, 220, 220),
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(25, 102),
                Size = new Size(475, 210),
                Text = string.IsNullOrEmpty(releaseNotes) ? "此版本包含重要功能改进与问题修复。" : releaseNotes
            };

            ProgressBar prgDownload = new ProgressBar
            {
                Location = new Point(25, 322),
                Size = new Size(475, 16),
                Visible = false
            };

            Button btnDownload = new Button
            {
                Text = "⚡ 立即下载安装 (MSI)",
                BackColor = Color.FromArgb(7, 193, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold),
                Location = new Point(25, 350),
                Size = new Size(180, 36),
                Cursor = Cursors.Hand
            };
            btnDownload.FlatAppearance.BorderSize = 0;

            Button btnWeb = new Button
            {
                Text = "🌐 GitHub 页面",
                BackColor = Color.FromArgb(50, 50, 52),
                ForeColor = Color.FromArgb(220, 220, 220),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(220, 350),
                Size = new Size(130, 36),
                Cursor = Cursors.Hand
            };
            btnWeb.FlatAppearance.BorderSize = 0;

            Button btnCancel = new Button
            {
                Text = "稍后再说",
                BackColor = Color.FromArgb(44, 44, 46),
                ForeColor = Color.FromArgb(180, 180, 180),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(370, 350),
                Size = new Size(130, 36),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => dlg.Close();

            btnWeb.Click += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(string.IsNullOrEmpty(webUrl) ? "https://github.com/" + GITHUB_REPO + "/releases" : webUrl);
                }
                catch { }
            };

            btnDownload.Click += (s, e) =>
            {
                btnDownload.Enabled = false;
                btnDownload.Text = "正在下载更新...";
                prgDownload.Visible = true;
                prgDownload.Value = 0;

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        string saveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                        if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);
                        string savePath = Path.Combine(saveFolder, "LanChat_Setup_v" + version + ".msi");

                        using (var client = new WebClient())
                        {
                            client.Headers.Add("User-Agent", "LanChat-Client/" + APP_VERSION);
                            client.DownloadProgressChanged += (sender, ev) =>
                            {
                                SafeInvoke(dlg, () =>
                                {
                                    if (prgDownload.Visible) prgDownload.Value = ev.ProgressPercentage;
                                });
                            };

                            client.DownloadFileCompleted += (sender, ev) =>
                            {
                                SafeInvoke(dlg, () =>
                                {
                                    if (ev.Error != null)
                                    {
                                        MessageBox.Show(dlg, "下载更新失败: " + ev.Error.Message, "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        btnDownload.Enabled = true;
                                        btnDownload.Text = "⚡ 重试下载";
                                    }
                                    else
                                    {
                                        btnDownload.Text = "下载完成！";
                                        var res = MessageBox.Show(dlg, "新版本安装包已下载至:\n" + savePath + "\n\n是否立即启动安装程序进行升级？", "下载完成", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                        if (res == DialogResult.Yes)
                                        {
                                            try
                                            {
                                                System.Diagnostics.Process.Start(savePath);
                                                dlg.Close();
                                                if (onInstallExit != null)
                                                {
                                                    onInstallExit();
                                                }
                                            }
                                            catch (Exception startEx)
                                            {
                                                MessageBox.Show(dlg, "无法自动启动安装程序: " + startEx.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                            }
                                        }
                                        else
                                        {
                                            dlg.Close();
                                        }
                                    }
                                });
                            };

                            client.DownloadFileAsync(new Uri(downloadUrl), savePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        SafeInvoke(dlg, () =>
                        {
                            MessageBox.Show(dlg, "发起下载失败: " + ex.Message, "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            btnDownload.Enabled = true;
                            btnDownload.Text = "⚡ 立即下载安装 (MSI)";
                        });
                    }
                });
            };

            dlg.Controls.AddRange(new Control[] { lblTitle, lblSub, lblNoteTitle, txtNotes, prgDownload, btnDownload, btnWeb, btnCancel });
            if (owner != null)
            {
                dlg.ShowDialog(owner);
            }
            else
            {
                dlg.ShowDialog();
            }
        }

        private static void SafeInvoke(Control ctrl, Action action)
        {
            if (ctrl == null || ctrl.IsDisposed || !ctrl.IsHandleCreated) return;
            try
            {
                if (ctrl.InvokeRequired)
                {
                    ctrl.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch { }
        }
    }

    #endregion
}
