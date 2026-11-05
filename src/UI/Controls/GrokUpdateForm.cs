using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// GrokBot 100% 像素级对标原生软件更新窗口 (完全对标 Alis7.png 与 Docs §9)
    /// 继承自 GrokModalFormBase，标准尺寸 520×440 (基准度量)，采用自绘 GrokButton
    /// </summary>
    public class GrokUpdateForm : GrokModalFormBase
    {
        private readonly string _newVersion;
        private readonly string _releaseTitle;
        private readonly string _releaseNotes;
        private readonly string _downloadUrl;
        private readonly string _webUrl;
        private readonly Action _onInstallExit;

        private TextBox _txtNotes;
        private GrokButton _btnDownload;
        private GrokButton _btnWeb;
        private GrokButton _btnCancel;
        private int _downloadProgress = -1; // -1: 未开始, 0..100: 下载中, 101: 完成

        public GrokUpdateForm(string version, string releaseTitle, string releaseNotes, string downloadUrl, string webUrl, Action onInstallExit = null)
        {
            _newVersion = version;
            _releaseTitle = string.IsNullOrEmpty(releaseTitle) ? "LanChat v" + version : releaseTitle;
            _releaseNotes = string.IsNullOrEmpty(releaseNotes) ? "此版本包含重要功能改进与问题修复。" : releaseNotes;
            _downloadUrl = downloadUrl;
            _webUrl = webUrl;
            _onInstallExit = onInstallExit;

            this.Size = GrokTheme.SizeUpdate; // 520 x 440
            this.ShowCloseButton = true;
            this.Font = GrokTheme.FontSecondary;

            BuildControls();
        }

        private void BuildControls()
        {
            // 更新说明多行文本框
            _txtNotes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(32, 156),
                Size = new Size(456, 206),
                BackColor = GrokTheme.Level0,
                ForeColor = GrokTheme.TextPrimary,
                Font = GrokTheme.FontSecondary,
                BorderStyle = BorderStyle.None,
                Text = _releaseNotes
            };
            this.Controls.Add(_txtNotes);

            // 底部操作按钮
            _btnDownload = new GrokButton("⚡ 立即更新 (MSI)", GrokButtonRole.Primary)
            {
                Location = new Point(24, 388),
                Size = new Size(160, GrokTheme.ButtonHeightStandard)
            };
            _btnDownload.Click += (s, e) => StartDownload();
            this.Controls.Add(_btnDownload);

            _btnWeb = new GrokButton("🌐 GitHub 发布页", GrokButtonRole.Secondary)
            {
                Location = new Point(194, 388),
                Size = new Size(140, GrokTheme.ButtonHeightStandard)
            };
            _btnWeb.Click += (s, e) =>
            {
                try
                {
                    Process.Start(string.IsNullOrEmpty(_webUrl) ? "https://github.com/yxiaojiang/LanChat/releases" : _webUrl);
                }
                catch { }
            };
            this.Controls.Add(_btnWeb);

            _btnCancel = new GrokButton("稍后再说", GrokButtonRole.Secondary, DialogResult.Cancel)
            {
                Location = new Point(408, 388),
                Size = new Size(88, GrokTheme.ButtonHeightStandard)
            };
            this.Controls.Add(_btnCancel);

            this.CancelButton = _btnCancel;
        }

        private void StartDownload()
        {
            if (string.IsNullOrEmpty(_downloadUrl))
            {
                try
                {
                    Process.Start(string.IsNullOrEmpty(_webUrl) ? "https://github.com/yxiaojiang/LanChat/releases" : _webUrl);
                }
                catch { }
                return;
            }

            _btnDownload.Enabled = false;
            _btnDownload.Text = "⏳ 正在连接...";
            _downloadProgress = 0;
            this.Invalidate();

            string tempDir = Path.Combine(Path.GetTempPath(), "LanChatUpdate");
            try
            {
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
            }
            catch { }

            string fileName = Path.GetFileName(_downloadUrl);
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            {
                fileName = string.Format("LanChat_Setup_v{0}.msi", _newVersion);
            }
            string savePath = Path.Combine(tempDir, fileName);

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "LanChat-Updater/" + MainForm.APP_VERSION);
                        wc.DownloadProgressChanged += (s, ev) =>
                        {
                            if (this.IsDisposed || !this.IsHandleCreated) return;
                            try
                            {
                                this.BeginInvoke(new Action(delegate
                                {
                                    _downloadProgress = ev.ProgressPercentage;
                                    _btnDownload.Text = string.Format("⏳ 下载中 {0}%", ev.ProgressPercentage);
                                    this.Invalidate();
                                }));
                            }
                            catch { }
                        };

                        wc.DownloadFileCompleted += (s, ev) =>
                        {
                            if (this.IsDisposed || !this.IsHandleCreated) return;
                            try
                            {
                                this.BeginInvoke(new Action(delegate
                                {
                                    if (ev.Error != null)
                                    {
                                        _downloadProgress = -1;
                                        _btnDownload.Enabled = true;
                                        _btnDownload.Text = "⚡ 重试下载";
                                        this.Invalidate();
                                        GrokMessageBox.Show(this, "下载更新包失败: " + ev.Error.Message, "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        return;
                                    }

                                    _downloadProgress = 101;
                                    _btnDownload.Text = "✓ 下载完成";
                                    this.Invalidate();

                                    DialogResult dr = GrokMessageBox.Show(
                                        this,
                                        "新版本安装包已下载完成:\n" + savePath + "\n\n是否立即启动安装程序进行升级？",
                                        "下载完成",
                                        MessageBoxButtons.YesNo,
                                        MessageBoxIcon.Question);

                                    if (dr == DialogResult.Yes)
                                    {
                                        try
                                        {
                                            Process.Start("msiexec.exe", string.Format("/i \"{0}\"", savePath));
                                            if (_onInstallExit != null)
                                            {
                                                _onInstallExit();
                                            }
                                            else
                                            {
                                                Application.Exit();
                                            }
                                        }
                                        catch (Exception startEx)
                                        {
                                            GrokMessageBox.Show(this, "无法自动启动安装程序: " + startEx.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        }
                                    }
                                }));
                            }
                            catch { }
                        };

                        wc.DownloadFileAsync(new Uri(_downloadUrl), savePath);
                    }
                }
                catch (Exception ex)
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;
                    try
                    {
                        this.BeginInvoke(new Action(delegate
                        {
                            _downloadProgress = -1;
                            _btnDownload.Enabled = true;
                            _btnDownload.Text = "⚡ 重试下载";
                            this.Invalidate();
                            GrokMessageBox.Show(this, "发起下载失败: " + ex.Message, "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }));
                    }
                    catch { }
                }
            });
        }

        protected override void OnPaintContent(Graphics g)
        {
            // 1. 标题 "软件更新" (18.0pt Bold #FFFFFF)
            using (Font titleFont = GrokTheme.FontTitle)
            using (SolidBrush tb = new SolidBrush(GrokTheme.TextTitle))
            {
                g.DrawString("软件更新", titleFont, tb, new PointF(26, 18));
            }

            // 2. 版本横幅卡片 (高 64px, 圆角 12px)
            Rectangle cardRect = new Rectangle(24, 54, 472, 64);
            using (SolidBrush cb = new SolidBrush(GrokTheme.Level2))
            using (Pen cp = new Pen(GrokTheme.Border, 1f))
            {
                GrokTheme.DrawRoundedRectangle(g, cb, cardRect, 12);
                GrokTheme.DrawRoundedRectangle(g, cp, cardRect, 12);
            }

            // 魅紫闪电图标徽标
            Rectangle rocketBox = new Rectangle(38, 64, 44, 44);
            using (SolidBrush rb = new SolidBrush(Color.FromArgb(40, 36, 56)))
            {
                GrokTheme.DrawRoundedRectangle(g, rb, rocketBox, 10);
            }
            WeChatIcon.Draw(g, WeChatIconType.Bolt, new Rectangle(48, 74, 24, 24), false, GrokTheme.AccentPurpleHot);

            using (Font vf = GrokTheme.FontPrimaryBold)
            using (Font sf = GrokTheme.FontFootnote)
            using (SolidBrush vb = new SolidBrush(GrokTheme.TextTitle))
            using (SolidBrush sb = new SolidBrush(GrokTheme.TextSecondary))
            {
                g.DrawString("发现新版本: v" + _newVersion, vf, vb, new PointF(92, 64));
                g.DrawString("当前版本: v" + MainForm.APP_VERSION + "  ·  更新标题: " + _releaseTitle, sf, sb, new PointF(92, 88));
            }

            // 3. 更新日志说明容器
            using (Font ntf = GrokTheme.FontSecondaryBold)
            using (SolidBrush ntb = new SolidBrush(GrokTheme.TextCard))
            {
                g.DrawString("更新日志 / 发布说明:", ntf, ntb, new PointF(26, 128));
            }

            Rectangle notesBox = new Rectangle(24, 150, 472, 218);
            using (SolidBrush nb = new SolidBrush(GrokTheme.Level0))
            using (Pen np = new Pen(GrokTheme.Border, 1f))
            {
                GrokTheme.DrawRoundedRectangle(g, nb, notesBox, 10);
                GrokTheme.DrawRoundedRectangle(g, np, notesBox, 10);
            }

            // 4. 极简平滑进度条 (下载时显示)
            if (_downloadProgress >= 0)
            {
                Rectangle progBg = new Rectangle(24, 374, 472, 6);
                using (SolidBrush pbg = new SolidBrush(GrokTheme.Level3))
                {
                    GrokTheme.DrawRoundedRectangle(g, pbg, progBg, 3);
                }
                int fillW = Math.Max(6, (int)(progBg.Width * (Math.Min(100, _downloadProgress) / 100f)));
                Rectangle progFill = new Rectangle(24, 374, fillW, 6);
                using (SolidBrush pfill = new SolidBrush(GrokTheme.AccentPurple))
                {
                    GrokTheme.DrawRoundedRectangle(g, pfill, progFill, 3);
                }
            }
        }
    }
}
