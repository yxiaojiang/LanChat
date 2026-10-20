using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace LanChat
{
    public class MainForm : Form
    {
        private NetworkService _netService;
        private readonly object _uiLock = new object();

        // UI Controls
        private Panel _topPanel;
        private Label _lblStatus;
        private Label _lblLocalInfo;
        private FlowLayoutPanel _topActionsFlow;
        private Label _lblNick;
        private TextBox _txtNick;
        private Button _btnSaveNick;
        private Button _btnRefresh;
        private Button _btnAddIp;
        private Button _btnOpenFolder;

        private SplitContainer _splitContainer;
        private Panel _leftHeaderPanel;
        private Label _lblPeersHeader;
        private ListBox _listPeers;
        private Label _lblLeftFooter;

        private Panel _chatHeaderPanel;
        private Label _lblChatTitle;
        private RichTextBox _rtbChat;
        private Panel _progressPanel;
        private Label _lblProgressInfo;
        private ProgressBar _progressBar;

        private Panel _toolPanel;
        private Button _btnSendFile;
        private CheckBox _chkAutoAccept;
        private Button _btnClearChat;

        private Panel _inputPanel;
        private TextBox _txtInput;
        private Button _btnSend;

        private const string BROADCAST_TARGET = "📢 局域网所有人 (广播群聊)";
        private PeerInfo _selectedPeer = null;

        public MainForm()
        {
            InitializeComponent();
            InitializeNetwork();
        }

        private void InitializeComponent()
        {
            this.Text = "LanChat - 局域网快聊与文件极速互传";
            this.Size = new Size(980, 680);
            this.MinimumSize = new Size(820, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            // 1. Top Panel
            _topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                BackColor = Color.FromArgb(241, 245, 249),
                Padding = new Padding(12, 11, 12, 11)
            };

            _lblStatus = new Label
            {
                Text = "● 在线",
                ForeColor = Color.FromArgb(22, 163, 74),
                Font = new Font("Microsoft YaHei", 10f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 16)
            };

            _lblLocalInfo = new Label
            {
                Text = "本机: 初始化中...",
                ForeColor = Color.FromArgb(15, 23, 42),
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(80, 16)
            };

            // Flow layout on the right for actions
            _topActionsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _lblNick = new Label
            {
                Text = "我的昵称:",
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true,
                Margin = new Padding(0, 6, 4, 0)
            };

            _txtNick = new TextBox
            {
                Text = Environment.UserName,
                Width = 100,
                Font = new Font("Microsoft YaHei", 9.5f),
                Margin = new Padding(0, 2, 6, 0)
            };
            _txtNick.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnSaveNick_Click(s, e); };

            _btnSaveNick = new Button
            {
                Text = "改名",
                Size = new Size(54, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnSaveNick.Click += BtnSaveNick_Click;

            _btnRefresh = new Button
            {
                Text = "🔄 刷新好友",
                Size = new Size(115, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnRefresh.Click += (s, e) => {
                _netService.BroadcastPresence("HELLO");
                AppendChatSystem("已向局域网广播并扫描同网段好友...");
            };

            _btnAddIp = new Button
            {
                Text = "➕ 添加IP",
                Size = new Size(95, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnAddIp.Click += BtnAddIp_Click;

            _btnOpenFolder = new Button
            {
                Text = "📂 接收文件夹",
                Size = new Size(125, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System,
                Margin = new Padding(0, 0, 0, 0)
            };
            _btnOpenFolder.Click += (s, e) => {
                try
                {
                    if (!Directory.Exists(_netService.SaveDirectory))
                    {
                        Directory.CreateDirectory(_netService.SaveDirectory);
                    }
                    Process.Start("explorer.exe", _netService.SaveDirectory);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "打开文件夹失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            _topActionsFlow.Controls.AddRange(new Control[] {
                _lblNick, _txtNick, _btnSaveNick, _btnRefresh, _btnAddIp, _btnOpenFolder
            });

            _topPanel.Controls.Add(_lblStatus);
            _topPanel.Controls.Add(_lblLocalInfo);
            _topPanel.Controls.Add(_topActionsFlow);

            // 2. Split Container
            _splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 265,
                SplitterWidth = 5,
                BackColor = Color.FromArgb(226, 232, 240)
            };

            // Left Sidebar: Peer List
            _splitContainer.Panel1.BackColor = Color.White;
            _leftHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            _lblPeersHeader = new Label
            {
                Text = "在线成员列表 (0 人)",
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Location = new Point(12, 9),
                AutoSize = true
            };
            _leftHeaderPanel.Controls.Add(_lblPeersHeader);

            _lblLeftFooter = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                Text = "💡 支持拖拽文件至右侧直接发送",
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Microsoft YaHei", 8.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(248, 250, 252)
            };

            _listPeers = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei", 10f),
                ItemHeight = 34,
                DrawMode = DrawMode.OwnerDrawFixed,
                IntegralHeight = false
            };
            _listPeers.DrawItem += ListPeers_DrawItem;
            _listPeers.SelectedIndexChanged += ListPeers_SelectedIndexChanged;

            _splitContainer.Panel1.Controls.Add(_listPeers);
            _splitContainer.Panel1.Controls.Add(_lblLeftFooter);
            _splitContainer.Panel1.Controls.Add(_leftHeaderPanel);

            // Right Area: Chat & Transfer
            _splitContainer.Panel2.BackColor = Color.White;

            _chatHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(14, 10, 14, 10)
            };
            _lblChatTitle = new Label
            {
                Text = BROADCAST_TARGET,
                Font = new Font("Microsoft YaHei", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                AutoSize = true,
                Location = new Point(14, 11)
            };
            _chatHeaderPanel.Controls.Add(_lblChatTitle);

            // Progress Panel (Collapsible)
            _progressPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(239, 246, 255),
                Visible = false,
                Padding = new Padding(12, 6, 12, 6)
            };
            _lblProgressInfo = new Label
            {
                Text = "准备传输...",
                Font = new Font("Microsoft YaHei", 9f),
                ForeColor = Color.FromArgb(30, 64, 175),
                AutoSize = true,
                Location = new Point(12, 6)
            };
            _progressBar = new ProgressBar
            {
                Location = new Point(12, 28),
                Height = 16,
                Width = 500,
                Style = ProgressBarStyle.Continuous
            };
            _progressPanel.Controls.Add(_lblProgressInfo);
            _progressPanel.Controls.Add(_progressBar);
            _progressPanel.Resize += (s, e) => {
                _progressBar.Width = _progressPanel.Width - 24;
            };

            // Chat Box
            _rtbChat = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Microsoft YaHei", 10f),
                Padding = new Padding(12),
                AllowDrop = true
            };
            _rtbChat.DragEnter += MainForm_DragEnter;
            _rtbChat.DragDrop += MainForm_DragDrop;

            // Toolbar
            _toolPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(10, 4, 10, 4)
            };

            _btnSendFile = new Button
            {
                Text = "📎 发送文件...",
                Location = new Point(10, 4),
                Size = new Size(110, 28),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            _btnSendFile.Click += BtnSendFile_Click;

            _chkAutoAccept = new CheckBox
            {
                Text = "自动接收文件",
                Checked = true,
                AutoSize = true,
                Location = new Point(130, 8),
                ForeColor = Color.FromArgb(51, 65, 85)
            };
            _chkAutoAccept.CheckedChanged += (s, e) => {
                _netService.AutoAcceptFiles = _chkAutoAccept.Checked;
            };

            _btnClearChat = new Button
            {
                Text = "🧹 清屏",
                Location = new Point(250, 4),
                Size = new Size(68, 28),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            _btnClearChat.Click += (s, e) => _rtbChat.Clear();

            _toolPanel.Controls.AddRange(new Control[] { _btnSendFile, _chkAutoAccept, _btnClearChat });

            // Input Area
            _inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 100,
                BackColor = Color.White,
                Padding = new Padding(10, 6, 10, 8)
            };

            _txtInput = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Microsoft YaHei", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                AllowDrop = true
            };
            _txtInput.KeyDown += TxtInput_KeyDown;
            _txtInput.DragEnter += MainForm_DragEnter;
            _txtInput.DragDrop += MainForm_DragDrop;

            Panel sendButtonPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 95,
                Padding = new Padding(8, 0, 0, 0)
            };
            _btnSend = new Button
            {
                Text = "发送\n(Enter)",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold)
            };
            _btnSend.FlatAppearance.BorderSize = 0;
            _btnSend.Click += BtnSend_Click;
            sendButtonPanel.Controls.Add(_btnSend);

            _inputPanel.Controls.Add(_txtInput);
            _inputPanel.Controls.Add(sendButtonPanel);

            // Add controls to Panel2
            _splitContainer.Panel2.Controls.Add(_rtbChat);
            _splitContainer.Panel2.Controls.Add(_progressPanel);
            _splitContainer.Panel2.Controls.Add(_chatHeaderPanel);
            _splitContainer.Panel2.Controls.Add(_toolPanel);
            _splitContainer.Panel2.Controls.Add(_inputPanel);

            this.Controls.Add(_splitContainer);
            this.Controls.Add(_topPanel);

            this.FormClosing += MainForm_FormClosing;
        }

        private void InitializeNetwork()
        {
            try
            {
                _netService = new NetworkService(_txtNick.Text.Trim());
                _netService.OnPeerJoined += (p) => SafeInvoke(() => PeerJoined(p));
                _netService.OnPeerUpdated += (p) => SafeInvoke(() => PeerUpdated(p));
                _netService.OnPeerLeft += (pid) => SafeInvoke(() => PeerLeft(pid));
                _netService.OnMessageReceived += (msg) => SafeInvoke(() => HandleReceivedMessage(msg));
                _netService.OnFileProgress += (prog) => SafeInvoke(() => HandleFileProgress(prog));
                _netService.OnFileReceived += (nick, fn, path, sz) => SafeInvoke(() => HandleFileReceived(nick, fn, path, sz));
                _netService.OnLogMessage += (log) => { };

                _netService.Start();

                IPAddress primaryIp = _netService.GetPrimaryPhysicalIp();
                _lblLocalInfo.Text = string.Format("本机: {0}:{1}", primaryIp, _netService.LocalTcpPort);

                RefreshPeerListUI();

                AppendChatSystem("LanChat 启动成功！已开始在局域网内搜索好友...");
                AppendChatSystem(string.Format("物理网卡绑定 IP: {0}", primaryIp));
                AppendChatSystem(string.Format("本地保存目录: {0}", _netService.SaveDirectory));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "启动网络服务失败: " + ex.Message, "网络错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region Peer List Handling

        private void PeerJoined(PeerInfo peer)
        {
            RefreshPeerListUI();
            AppendChatSystem(string.Format("发现好友上线: {0} ({1} - {2})", peer.NickName, peer.HostName, peer.IP));
        }

        private void PeerUpdated(PeerInfo peer)
        {
            RefreshPeerListUI();
        }

        private void PeerLeft(string peerId)
        {
            RefreshPeerListUI();
        }

        private void RefreshPeerListUI()
        {
            var peers = _netService.GetActivePeers();
            _lblPeersHeader.Text = string.Format("在线成员列表 ({0} 人)", peers.Count);

            object currentSelected = _listPeers.SelectedItem;
            _listPeers.BeginUpdate();
            _listPeers.Items.Clear();

            // First item is always broadcast
            _listPeers.Items.Add(BROADCAST_TARGET);

            foreach (var p in peers)
            {
                _listPeers.Items.Add(p);
            }

            // Restore selection if possible
            if (currentSelected != null)
            {
                int index = -1;
                for (int i = 0; i < _listPeers.Items.Count; i++)
                {
                    if (_listPeers.Items[i].ToString() == currentSelected.ToString())
                    {
                        index = i;
                        break;
                    }
                }
                if (index >= 0) _listPeers.SelectedIndex = index;
                else _listPeers.SelectedIndex = 0;
            }
            else
            {
                _listPeers.SelectedIndex = 0;
            }

            _listPeers.EndUpdate();
        }

        private void ListPeers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_listPeers.SelectedItem == null) return;

            if (_listPeers.SelectedItem is string && (string)_listPeers.SelectedItem == BROADCAST_TARGET)
            {
                _selectedPeer = null;
                _lblChatTitle.Text = BROADCAST_TARGET;
                _lblChatTitle.ForeColor = Color.FromArgb(30, 41, 59);
            }
            else if (_listPeers.SelectedItem is PeerInfo)
            {
                _selectedPeer = (PeerInfo)_listPeers.SelectedItem;
                _lblChatTitle.Text = string.Format("👤 与 {0} 的私聊 ({1}:{2})", _selectedPeer.NickName, _selectedPeer.IP, _selectedPeer.TcpPort);
                _lblChatTitle.ForeColor = Color.FromArgb(29, 78, 216);
            }
        }

        private void ListPeers_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color bgColor = isSelected ? Color.FromArgb(224, 238, 254) : Color.White;
            Color textColor = isSelected ? Color.FromArgb(29, 78, 216) : Color.FromArgb(51, 65, 85);

            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
            }

            object item = _listPeers.Items[e.Index];
            string text = item.ToString();

            using (SolidBrush textBrush = new SolidBrush(textColor))
            {
                Font f = isSelected ? new Font(this.Font, FontStyle.Bold) : this.Font;
                Rectangle textRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 6, e.Bounds.Width - 16, e.Bounds.Height - 8);
                e.Graphics.DrawString(text, f, textBrush, textRect);
            }

            using (Pen pen = new Pen(Color.FromArgb(241, 245, 249)))
            {
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
        }

        #endregion

        #region Chat & Messaging

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift && !e.Control)
            {
                e.SuppressKeyPress = true;
                SendMessage();
            }
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            SendMessage();
        }

        private void SendMessage()
        {
            string text = _txtInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _txtInput.Clear();

            if (_selectedPeer == null)
            {
                // Broadcast
                var peers = _netService.GetActivePeers();
                AppendChatMessage("我 (广播)", text, DateTime.Now, Color.FromArgb(22, 101, 52), true);

                new System.Threading.Thread(() => {
                    foreach (var peer in peers)
                    {
                        _netService.SendTextMessage(peer, text, true);
                    }
                }) { IsBackground = true }.Start();
            }
            else
            {
                // Send to selected peer
                PeerInfo target = _selectedPeer;
                AppendChatMessage(string.Format("我 -> {0} (私聊)", target.NickName), text, DateTime.Now, Color.FromArgb(126, 34, 206), true);

                new System.Threading.Thread(() => {
                    bool ok = _netService.SendTextMessage(target, text, false);
                    if (!ok)
                    {
                        SafeInvoke(() => AppendChatSystem(string.Format("向 {0} 发送私聊消息失败，对方可能已离线", target.NickName)));
                    }
                }) { IsBackground = true }.Start();
            }
        }

        private void HandleReceivedMessage(ChatMessage msg)
        {
            if (msg.IsBroadcast)
            {
                AppendChatMessage(string.Format("{0} (广播)", msg.SenderNick), msg.Content, msg.Timestamp, Color.FromArgb(29, 78, 216), false);
            }
            else
            {
                AppendChatMessage(string.Format("{0} (私聊)", msg.SenderNick), msg.Content, msg.Timestamp, Color.FromArgb(126, 34, 206), false);
            }
        }

        private void AppendChatMessage(string sender, string content, DateTime time, Color headerColor, bool isSelf)
        {
            _rtbChat.SelectionStart = _rtbChat.TextLength;
            _rtbChat.SelectionLength = 0;

            _rtbChat.SelectionColor = Color.FromArgb(148, 163, 184);
            _rtbChat.SelectionFont = new Font("Microsoft YaHei", 8.5f);
            _rtbChat.AppendText(string.Format("[{0}] ", time.ToString("HH:mm:ss")));

            _rtbChat.SelectionColor = headerColor;
            _rtbChat.SelectionFont = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold);
            _rtbChat.AppendText(sender + ":\r\n");

            _rtbChat.SelectionColor = Color.FromArgb(30, 41, 59);
            _rtbChat.SelectionFont = new Font("Microsoft YaHei", 10f);
            _rtbChat.AppendText(content + "\r\n\r\n");

            _rtbChat.ScrollToCaret();
        }

        private void AppendChatSystem(string text)
        {
            _rtbChat.SelectionStart = _rtbChat.TextLength;
            _rtbChat.SelectionLength = 0;
            _rtbChat.SelectionColor = Color.FromArgb(100, 116, 139);
            _rtbChat.SelectionFont = new Font("Microsoft YaHei", 8.5f, FontStyle.Italic);
            _rtbChat.AppendText(string.Format("[{0}] [系统] {1}\r\n", DateTime.Now.ToString("HH:mm:ss"), text));
            _rtbChat.ScrollToCaret();
        }

        #endregion

        #region File Transfer & Drag Drop

        private void BtnSendFile_Click(object sender, EventArgs e)
        {
            if (_selectedPeer == null)
            {
                var peers = _netService.GetActivePeers();
                if (peers.Count == 0)
                {
                    MessageBox.Show(this, "局域网内暂无其他在线好友，请等待好友加入或点击【➕ 添加IP】！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (OpenFileDialog ofd = new OpenFileDialog { Title = "选择要发送的文件", Multiselect = true })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        PromptPeerAndSendFiles(ofd.FileNames);
                    }
                }
            }
            else
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Title = "选择要发送给 " + _selectedPeer.NickName + " 的文件", Multiselect = true })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        SendFilesToPeer(_selectedPeer, ofd.FileNames);
                    }
                }
            }
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            if (_selectedPeer != null)
            {
                SendFilesToPeer(_selectedPeer, files);
            }
            else
            {
                PromptPeerAndSendFiles(files);
            }
        }

        private void PromptPeerAndSendFiles(string[] filePaths)
        {
            var peers = _netService.GetActivePeers();
            if (peers.Count == 0)
            {
                MessageBox.Show(this, "当前无在线好友，无法发送文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (peers.Count == 1)
            {
                SendFilesToPeer(peers[0], filePaths);
                return;
            }

            using (Form picker = new Form())
            {
                picker.Text = "选择接收文件的成员";
                picker.Size = new Size(360, 260);
                picker.StartPosition = FormStartPosition.CenterParent;
                picker.FormBorderStyle = FormBorderStyle.FixedDialog;
                picker.MaximizeBox = false;
                picker.MinimizeBox = false;

                ListBox lb = new ListBox { Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei", 9.5f) };
                foreach (var p in peers) lb.Items.Add(p);
                lb.SelectedIndex = 0;

                Button btnOk = new Button { Text = "确定发送", Dock = DockStyle.Bottom, Height = 36, DialogResult = DialogResult.OK };
                picker.Controls.Add(lb);
                picker.Controls.Add(btnOk);

                if (picker.ShowDialog(this) == DialogResult.OK && lb.SelectedItem is PeerInfo)
                {
                    SendFilesToPeer((PeerInfo)lb.SelectedItem, filePaths);
                }
            }
        }

        private void SendFilesToPeer(PeerInfo peer, string[] filePaths)
        {
            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath)) continue;

                string fileName = Path.GetFileName(filePath);
                long fileSize = new FileInfo(filePath).Length;

                AppendChatSystem(string.Format("开始向 [{0}] 发送文件: {1} ({2})", peer.NickName, fileName, FormatFileSize(fileSize)));

                _progressPanel.Visible = true;
                _progressBar.Value = 0;
                _lblProgressInfo.Text = string.Format("正在发送 {0} 至 {1}...", fileName, peer.NickName);

                _netService.SendFileAsync(peer, filePath,
                    prog => SafeInvoke(() => HandleFileProgress(prog)),
                    (success, reason) => SafeInvoke(() => {
                        _progressPanel.Visible = false;
                        if (success)
                        {
                            AppendChatSystem(string.Format("文件 {0} 发送成功！", fileName));
                        }
                        else
                        {
                            AppendChatSystem(string.Format("文件 {0} 发送失败: {1}", fileName, reason));
                        }
                    })
                );
            }
        }

        private void HandleFileProgress(FileTransferProgress prog)
        {
            _progressPanel.Visible = true;
            _progressBar.Value = prog.ProgressPercentage;

            string action = prog.IsSending ? "发送给 " + prog.PeerNick : "来自 " + prog.PeerNick + " 接收";
            _lblProgressInfo.Text = string.Format("[{0}] {1} ({2}/{3}) - {4:F1} MB/s [{5}%]",
                action, prog.FileName,
                FormatFileSize(prog.TransferredBytes),
                FormatFileSize(prog.TotalBytes),
                prog.SpeedMBps,
                prog.ProgressPercentage);

            if (prog.IsCompleted || prog.IsFailed)
            {
                _progressPanel.Visible = false;
            }
        }

        private void HandleFileReceived(string senderNick, string fileName, string localPath, long size)
        {
            _progressPanel.Visible = false;

            _rtbChat.SelectionStart = _rtbChat.TextLength;
            _rtbChat.SelectionLength = 0;

            _rtbChat.SelectionColor = Color.FromArgb(148, 163, 184);
            _rtbChat.SelectionFont = new Font("Microsoft YaHei", 8.5f);
            _rtbChat.AppendText(string.Format("[{0}] ", DateTime.Now.ToString("HH:mm:ss")));

            _rtbChat.SelectionColor = Color.FromArgb(22, 101, 52);
            _rtbChat.SelectionFont = new Font("Microsoft YaHei", 10f, FontStyle.Bold);
            _rtbChat.AppendText(string.Format("📎 接收文件成功: {0} ({1})\r\n", fileName, FormatFileSize(size)));

            _rtbChat.SelectionColor = Color.FromArgb(71, 85, 105);
            _rtbChat.SelectionFont = new Font("Microsoft YaHei", 9f);
            _rtbChat.AppendText(string.Format("   来自: {0}\r\n   保存路径: {1}\r\n\r\n", senderNick, localPath));

            _rtbChat.ScrollToCaret();
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
        }

        #endregion

        #region Actions

        private void BtnSaveNick_Click(object sender, EventArgs e)
        {
            string newNick = _txtNick.Text.Trim();
            if (string.IsNullOrEmpty(newNick))
            {
                MessageBox.Show(this, "昵称不能为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _netService.NickName = newNick;
            _netService.BroadcastPresence("HELLO");
            AppendChatSystem("昵称已修改为: " + newNick);
        }

        private void BtnAddIp_Click(object sender, EventArgs e)
        {
            using (Form inputForm = new Form())
            {
                inputForm.Text = "手动添加好友 IP (直连握手)";
                inputForm.Size = new Size(390, 165);
                inputForm.StartPosition = FormStartPosition.CenterParent;
                inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                inputForm.MaximizeBox = false;
                inputForm.MinimizeBox = false;

                Label lblPrompt = new Label
                {
                    Text = "请输入对方计算机的 IPv4 地址 (例如 192.168.255.147):",
                    Location = new Point(16, 16),
                    AutoSize = true,
                    Font = new Font("Microsoft YaHei", 9f)
                };

                TextBox txtIp = new TextBox
                {
                    Location = new Point(18, 42),
                    Width = 338,
                    Font = new Font("Microsoft YaHei", 10f)
                };

                Button btnOk = new Button
                {
                    Text = "连接握手",
                    Location = new Point(180, 78),
                    Size = new Size(85, 30),
                    DialogResult = DialogResult.OK
                };

                Button btnCancel = new Button
                {
                    Text = "取消",
                    Location = new Point(275, 78),
                    Size = new Size(80, 30),
                    DialogResult = DialogResult.Cancel
                };

                inputForm.Controls.AddRange(new Control[] { lblPrompt, txtIp, btnOk, btnCancel });
                inputForm.AcceptButton = btnOk;
                inputForm.CancelButton = btnCancel;

                if (inputForm.ShowDialog(this) == DialogResult.OK)
                {
                    string ipStr = txtIp.Text.Trim();
                    if (!string.IsNullOrEmpty(ipStr))
                    {
                        AppendChatSystem("正在主动向 " + ipStr + " 发起 TCP 直连握手...");
                        _netService.ProbeIp(ipStr, (success, msg) => {
                            SafeInvoke(() => AppendChatSystem(msg));
                        });
                    }
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_netService != null)
            {
                _netService.Dispose();
            }
        }

        private void SafeInvoke(Action action)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch { }
        }

        #endregion
    }
}
