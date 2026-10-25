using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// Grok Bot 100% 像素级对标原生软件更新窗口 (完全对标 Alis7.png)
    /// 包含圆角暗黑窗体、版本对比信息卡片、极简平滑进度条与操作按钮
    /// </summary>
    public class GrokUpdateForm : Form
    {
        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private readonly string _newVersion;
        private readonly string _releaseTitle;
        private readonly string _releaseNotes;
        private readonly string _downloadUrl;
        private readonly string _webUrl;
        private readonly Action _onInstallExit;

        private TextBox _txtNotes;
        private Button _btnDownload;
        private Button _btnWeb;
        private Button _btnCancel;
        private bool _isCloseHover = false;
        private int _downloadProgress = -1; // -1: 未开始, 0..100: 下载中, 101: 完成

        public GrokUpdateForm(string version, string releaseTitle, string releaseNotes, string downloadUrl, string webUrl, Action onInstallExit = null)
        {
            _newVersion = version;
            _releaseTitle = string.IsNullOrEmpty(releaseTitle) ? "LanChat v" + version : releaseTitle;
            _releaseNotes = string.IsNullOrEmpty(releaseNotes) ? "此版本包含重要功能改进与问题修复。" : releaseNotes;
            _downloadUrl = downloadUrl;
            _webUrl = webUrl;
            _onInstallExit = onInstallExit;

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(580, 480);
            this.BackColor = Color.FromArgb(22, 22, 24); // #161618
            this.DoubleBuffered = true;
            this.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular);

            BuildControls();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                IntPtr rgn = CreateRoundRectRgn(0, 0, this.Width + 1, this.Height + 1, 16, 16);
                if (rgn != IntPtr.Zero)
                {
                    this.Region = Region.FromHrgn(rgn);
                }
            }
            catch { }
        }

        private void BuildControls()
        {
            // 更新说明多行文本框
            _txtNotes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(34, 158),
                Size = new Size(512, 220),
                BackColor = Color.FromArgb(16, 16, 18),
                ForeColor = Color.FromArgb(225, 225, 230),
                Font = new Font("Microsoft YaHei", 9.5f),
                BorderStyle = BorderStyle.None,
                Text = _releaseNotes
            };
            this.Controls.Add(_txtNotes);

            // 底部立即下载安装按钮
            _btnDownload = new Button
            {
                Text = "⚡ 立即更新 (MSI)",
                Location = new Point(30, 416),
                Size = new Size(160, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold)
            };
            _btnDownload.FlatAppearance.BorderSize = 0;
            _btnDownload.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnDownload.FlatAppearance.MouseDownBackColor = Color.Transparent;

            bool dlHover = false;
            _btnDownload.MouseEnter += (s, e) => { dlHover = true; _btnDownload.Invalidate(); };
            _btnDownload.MouseLeave += (s, e) => { dlHover = false; _btnDownload.Invalidate(); };
            _btnDownload.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, _btnDownload.Width - 1, _btnDownload.Height - 1);
                using (SolidBrush b = new SolidBrush(dlHover ? Color.FromArgb(142, 100, 220) : Color.FromArgb(121, 70, 215)))
                using (Pen p = new Pen(Color.FromArgb(142, 100, 220), 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 18);
                    MainForm.DrawRoundedRectangle(e.Graphics, p, r, 18);
                }
                using (SolidBrush tb = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(_btnDownload.Text, _btnDownload.Font, tb, new RectangleF(0, 1, _btnDownload.Width, _btnDownload.Height), sf);
                }
            };
            _btnDownload.Click += (s, e) => StartDownload();
            this.Controls.Add(_btnDownload);

            // GitHub 网页页面按钮
            _btnWeb = new Button
            {
                Text = "🌐 GitHub 发布页",
                Location = new Point(204, 416),
                Size = new Size(140, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular)
            };
            _btnWeb.FlatAppearance.BorderSize = 0;
            _btnWeb.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnWeb.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool webHover = false;
            _btnWeb.MouseEnter += (s, e) => { webHover = true; _btnWeb.Invalidate(); };
            _btnWeb.MouseLeave += (s, e) => { webHover = false; _btnWeb.Invalidate(); };
            _btnWeb.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, _btnWeb.Width - 1, _btnWeb.Height - 1);
                using (SolidBrush b = new SolidBrush(webHover ? Color.FromArgb(48, 48, 54) : Color.FromArgb(34, 34, 38)))
                using (Pen p = new Pen(Color.FromArgb(52, 52, 58), 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 18);
                    MainForm.DrawRoundedRectangle(e.Graphics, p, r, 18);
                }
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(220, 220, 225)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("🌐 GitHub 发布页", _btnWeb.Font, tb, new RectangleF(0, 1, _btnWeb.Width, _btnWeb.Height), sf);
                }
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

            // 稍后再说按钮
            _btnCancel = new Button
            {
                Text = "稍后再说",
                Location = new Point(460, 416),
                Size = new Size(90, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular)
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnCancel.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool cHover = false;
            _btnCancel.MouseEnter += (s, e) => { cHover = true; _btnCancel.Invalidate(); };
            _btnCancel.MouseLeave += (s, e) => { cHover = false; _btnCancel.Invalidate(); };
            _btnCancel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, _btnCancel.Width - 1, _btnCancel.Height - 1);
                using (SolidBrush b = new SolidBrush(cHover ? Color.FromArgb(48, 48, 54) : Color.FromArgb(32, 32, 36)))
                using (Pen p = new Pen(Color.FromArgb(52, 52, 58), 1f))
                {
                    MainForm.DrawRoundedRectangle(e.Graphics, b, r, 18);
                    MainForm.DrawRoundedRectangle(e.Graphics, p, r, 18);
                }
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(170, 170, 176)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString("稍后再说", _btnCancel.Font, tb, new RectangleF(0, 1, _btnCancel.Width, _btnCancel.Height), sf);
                }
            };
            _btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(_btnCancel);
        }

        private void StartDownload()
        {
            _btnDownload.Enabled = false;
            _btnDownload.Text = "正在下载...";
            _downloadProgress = 0;
            Invalidate(new Rectangle(28, 388, 524, 16));

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string saveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);
                    string savePath = Path.Combine(saveFolder, "LanChat_Setup_v" + _newVersion + ".msi");

                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "LanChat-Client/" + MainForm.APP_VERSION);
                        client.DownloadProgressChanged += (sender, ev) =>
                        {
                            SafeBeginInvoke(() =>
                            {
                                _downloadProgress = ev.ProgressPercentage;
                                Invalidate(new Rectangle(28, 388, 524, 16));
                            });
                        };

                        client.DownloadFileCompleted += (sender, ev) =>
                        {
                            SafeBeginInvoke(() =>
                            {
                                if (ev.Error != null)
                                {
                                    _downloadProgress = -1;
                                    _btnDownload.Enabled = true;
                                    _btnDownload.Text = "⚡ 重试下载";
                                    GrokMessageBox.Show(this, "下载更新包失败: " + ev.Error.Message, "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                                else
                                {
                                    _downloadProgress = 100;
                                    _btnDownload.Text = "下载完成";
                                    DialogResult dr = GrokMessageBox.Show(this, "新版本安装包已下载完成:\n" + savePath + "\n\n是否立即启动安装程序进行升级？", "下载完成", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                    if (dr == DialogResult.Yes)
                                    {
                                        try
                                        {
                                            Process.Start(savePath);
                                            this.Close();
                                            if (_onInstallExit != null) _onInstallExit();
                                        }
                                        catch (Exception startEx)
                                        {
                                            GrokMessageBox.Show(this, "无法自动启动安装程序: " + startEx.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        }
                                    }
                                    else
                                    {
                                        this.Close();
                                    }
                                }
                            });
                        };

                        client.DownloadFileAsync(new Uri(_downloadUrl), savePath);
                    }
                }
                catch (Exception ex)
                {
                    SafeBeginInvoke(() =>
                    {
                        _downloadProgress = -1;
                        _btnDownload.Enabled = true;
                        _btnDownload.Text = "⚡ 立即更新 (MSI)";
                        GrokMessageBox.Show(this, "发起下载失败: " + ex.Message, "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            });
        }

        private void SafeBeginInvoke(Action act)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            try { this.BeginInvoke(act); } catch { }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Rectangle closeRect = new Rectangle(this.Width - 36, 14, 22, 22);
                if (closeRect.Contains(e.Location))
                {
                    this.Close();
                    return;
                }

                if (e.Y < 48)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, 0x00A1, (IntPtr)0x0002, IntPtr.Zero);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Rectangle closeRect = new Rectangle(this.Width - 36, 14, 22, 22);
            bool hover = closeRect.Contains(e.Location);
            if (hover != _isCloseHover)
            {
                _isCloseHover = hover;
                Invalidate(closeRect);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. 背景与微光外边框
            using (Pen borderPen = new Pen(Color.FromArgb(46, 46, 52), 1f))
            {
                MainForm.DrawRoundedRectangle(g, borderPen, new Rectangle(0, 0, this.Width - 1, this.Height - 1), 16);
            }

            // 2. 标题 "软件更新" (完全对标 Alis7.png)
            using (Font titleFont = new Font("Microsoft YaHei", 14f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(245, 245, 250)))
            {
                g.DrawString("软件更新", titleFont, tb, new PointF(28, 18));
            }

            // 3. 右上角极简叉号 (×)
            Rectangle closeRect = new Rectangle(this.Width - 36, 14, 22, 22);
            if (_isCloseHover)
            {
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(48, 48, 54)))
                {
                    g.FillEllipse(hb, closeRect);
                }
            }
            using (Pen cp = new Pen(_isCloseHover ? Color.White : Color.FromArgb(160, 160, 166), 1.5f))
            {
                cp.StartCap = LineCap.Round;
                cp.EndCap = LineCap.Round;
                int pad = 5;
                g.DrawLine(cp, closeRect.X + pad, closeRect.Y + pad, closeRect.Right - pad, closeRect.Bottom - pad);
                g.DrawLine(cp, closeRect.Right - pad, closeRect.Y + pad, closeRect.X + pad, closeRect.Bottom - pad);
            }

            // 4. 版本横幅卡片 (高 68px, 圆角 12px)
            Rectangle cardRect = new Rectangle(28, 58, 524, 68);
            using (SolidBrush cb = new SolidBrush(Color.FromArgb(28, 28, 32)))
            using (Pen cp = new Pen(Color.FromArgb(44, 44, 50), 1f))
            {
                MainForm.DrawRoundedRectangle(g, cb, cardRect, 12);
                MainForm.DrawRoundedRectangle(g, cp, cardRect, 12);
            }

            // 绿色/魅紫火箭图标微标
            Rectangle rocketBox = new Rectangle(42, 70, 44, 44);
            using (SolidBrush rb = new SolidBrush(Color.FromArgb(40, 36, 56)))
            {
                MainForm.DrawRoundedRectangle(g, rb, rocketBox, 10);
            }
            WeChatIcon.Draw(g, WeChatIconType.Bolt, new Rectangle(52, 80, 24, 24), false, Color.FromArgb(144, 90, 251));

            using (Font vf = new Font("Microsoft YaHei", 11f, FontStyle.Bold))
            using (Font sf = new Font("Microsoft YaHei", 9f, FontStyle.Regular))
            using (SolidBrush vb = new SolidBrush(Color.FromArgb(245, 245, 250)))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(160, 160, 166)))
            {
                g.DrawString("发现新版本: v" + _newVersion, vf, vb, new PointF(96, 68));
                g.DrawString("当前版本: v" + MainForm.APP_VERSION + "  ·  更新标题: " + _releaseTitle, sf, sb, new PointF(96, 94));
            }

            // 5. 更新日志说明边框
            using (Font ntf = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold))
            using (SolidBrush ntb = new SolidBrush(Color.FromArgb(210, 210, 216)))
            {
                g.DrawString("更新日志 / 发布说明:", ntf, ntb, new PointF(30, 134));
            }

            Rectangle notesBox = new Rectangle(28, 154, 524, 228);
            using (SolidBrush nb = new SolidBrush(Color.FromArgb(16, 16, 18)))
            using (Pen np = new Pen(Color.FromArgb(40, 40, 46), 1f))
            {
                MainForm.DrawRoundedRectangle(g, nb, notesBox, 10);
                MainForm.DrawRoundedRectangle(g, np, notesBox, 10);
            }

            // 6. 极简平滑进度条 (下载时显示)
            if (_downloadProgress >= 0)
            {
                Rectangle progBg = new Rectangle(28, 394, 524, 8);
                using (SolidBrush pbg = new SolidBrush(Color.FromArgb(36, 36, 42)))
                {
                    MainForm.DrawRoundedRectangle(g, pbg, progBg, 4);
                }
                int fillW = Math.Max(8, (int)(progBg.Width * (_downloadProgress / 100f)));
                Rectangle progFill = new Rectangle(28, 394, fillW, 8);
                using (SolidBrush pfill = new SolidBrush(Color.FromArgb(144, 90, 251)))
                {
                    MainForm.DrawRoundedRectangle(g, pfill, progFill, 4);
                }
            }
        }
    }
}
