using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 在 Windows 上通过回环套接字和原生窗口消息验证好友刷新。
    /// 使用独立端口、可控网卡枚举和临时配置，随夹具进程结束而释放。
    /// </summary>
    internal static class FriendRefreshRegression
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly IPAddress PeerAddress = IPAddress.Parse("127.0.0.2");
        private static IPAddress _fixtureAddress = IPAddress.Parse("127.0.0.4");

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        internal static List<NetworkInterfaceDetail> FixtureInterfaces()
        {
            return new List<NetworkInterfaceDetail>
            {
                new NetworkInterfaceDetail
                {
                    Name = "回环夹具",
                    IP = _fixtureAddress,
                    Mask = IPAddress.Parse("255.255.255.255"),
                    BroadcastAddress = _fixtureAddress
                }
            };
        }

        [STAThread]
        private static int Main()
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"NickName\":\"回环本机\"}");
            ChatHistoryService.ProfileStorageDirectory = Path.Combine(directory, "History");
            Directory.CreateDirectory(ChatHistoryService.ProfileStorageDirectory);
            for (int i = 20; i < 36; i++)
            {
                IPAddress ip = IPAddress.Parse("127.0.0." + i);
                ChatHistoryService.SaveMessage(SessionManager.GetLanKey("历史-" + i, ip), new ChatMessage
                {
                    SenderId = "历史-" + i, SenderNick = "隔离归档-" + i, RemoteAddress = ip,
                    Content = "回归用历史", Timestamp = DateTime.Now
                });
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles();
            using (var form = new MainForm())
            {
                form.Show();
                try
                {
                    CheckNetwork(form);
                    CheckListRebuild(form);
                    CheckScrollDrag(form);
                    CheckComposerUi(form);
                    CheckChatSelectionAndDraftSwitch(form);
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine(error.GetType().FullName + " — " + error.Message);
                    Console.Error.WriteLine(error.StackTrace);
                    return 1;
                }
                finally
                {
                    typeof(MainForm).GetField("_isExplicitExit", Private).SetValue(form, true);
                    form.Close();
                }
            }
            return 0;
        }

        private static void CheckNetwork(MainForm form)
        {
            var network = Field<NetworkService>(form, "_netService");
            var refresh = Field<WeChatIconButton>(form, "_btnRefreshFriends");
            var oldListener = new TcpListener(PeerAddress, 0);
            oldListener.Start();
            int port = ((IPEndPoint)oldListener.LocalEndpoint).Port;
            var newListener = new TcpListener(PeerAddress, port);
            using (var udp = new UdpClient(new IPEndPoint(PeerAddress, NetworkService.UDP_PORT)))
            using (var oldLeft = new ManualResetEventSlim())
            {
                try
                {
                    SendIdentity(udp, "old-id", "旧昵称", port);
                    WaitFor(() => network.GetActivePeers().Any(peer => peer.Id == "old-id"), "旧实例已发现");
                    var oldPeer = network.GetActivePeers().Single();
                    oldPeer.UnreadCount = 3;
                    DateTime oldSeen = oldPeer.LastSeen;
                    network.AddOrUpdatePeer("different-port", "同址异端口", "fixture", PeerAddress, port == 65535 ? port - 1 : port + 1);

                    bool leftOutsideLocks = false;
                    network.OnPeerLeft += id =>
                    {
                        if (id != oldPeer.Id) return;
                        leftOutsideLocks = !Monitor.IsEntered(Field<object>(network, "_peerLock")) &&
                            !Monitor.IsEntered(Field<object>(network, "_discoveryLock"));
                        oldLeft.Set();
                    };

                    // 旧实例直接关闭端口。让复核先连到新实例，等 UDP 身份归并后才回 TCP 确认。
                    oldListener.Stop();
                    newListener.Start();
                    Click(refresh);
                    ReadHello(udp, network.LocalId, _fixtureAddress);
                    WaitFor(newListener.Pending, "刷新复核连接同端口新实例");
                    using (var client = newListener.AcceptTcpClient())
                    {
                        ReadHandshake(client, network);
                        SendIdentity(udp, "new-id", "新昵称", port);
                        WaitFor(() => oldLeft.IsSet, "归并后通知旧身份离开");
                        var peers = network.GetActivePeers();
                        var current = peers.Single(peer => peer.TcpPort == port);
                        Require(leftOutsideLocks, "下线通知在锁外派发");
                        Require(current.Id == "new-id" && current.NickName == "新昵称" && current.UnreadCount == 3,
                            "同端口唯一新身份保留公告昵称和旧未读");
                        Require(peers.Any(peer => peer.Id == "different-port"), "保留同地址不同端口节点");
                        DateTime newSeen = current.LastSeen;
                        client.GetStream().WriteByte(0x01);
                        WaitFor(() => !Field<bool>(network, "_checkingPresence"), "等待刷新复核结束");
                        Require(oldPeer.LastSeen == oldSeen && current.LastSeen == newSeen,
                            "晚到 TCP 确认不改变旧身份或误标新身份");
                        Require(network.GetActivePeers().Single() == current, "刷新完成后无旧幽灵节点");
                        CheckHandshakeConflict(network, newListener, current);
                    }
                    Console.WriteLine("LC-FR-001: 无 BYE 同端口换 ID、锁外通知、未读迁移和 TCP 确认隔离通过");

                    var sender = new IPEndPoint(IPAddress.Any, 0);
                    while (udp.Available > 0) udp.Receive(ref sender);
                    var previous = Field<NetworkInterfaceDetail[]>(network, "_activeInterfaces");
                    _fixtureAddress = IPAddress.Parse("127.0.0.5");
                    Click(refresh);
                    ReadHello(udp, network.LocalId, _fixtureAddress);
                    var currentInterfaces = Field<NetworkInterfaceDetail[]>(network, "_activeInterfaces");
                    Require(currentInterfaces != previous && BoundAddress(currentInterfaces).Equals(_fixtureAddress),
                        "刷新先绑定新网卡，再发送新 IPv4 公告");
                    Require(IsClosed(previous.Single().UdpSocket), "旧网卡的 UDP 套接字已关闭");

                    // 模拟同一网卡上一次绑定失败，下一次刷新应真实创建新的监听套接字。
                    var failedAdapter = currentInterfaces.Single();
                    failedAdapter.UdpSocket.Close();
                    failedAdapter.UdpSocket = null;
                    Click(refresh);
                    var rebound = Field<NetworkInterfaceDetail[]>(network, "_activeInterfaces");
                    Require(rebound != currentInterfaces && BoundAddress(rebound).Equals(_fixtureAddress), "重试空 UDP 监听绑定");
                    Click(refresh);
                    Require(Field<NetworkInterfaceDetail[]>(network, "_activeInterfaces") == rebound, "不变网卡复用已有快照");
                    Console.WriteLine("LC-FR-002: 无网卡事件时手动重绑、重试失败监听及复用正常快照通过");
                }
                finally
                {
                    oldListener.Stop();
                    newListener.Stop();
                }
            }
        }

        private static void CheckHandshakeConflict(NetworkService network, TcpListener listener, PeerInfo current)
        {
            // 文字帧可先于 UDP 到达，注入一条同端点旧快照来直接覆盖握手的冲突分支。
            var duplicate = new PeerInfo { Id = "pending-id", IP = current.IP, TcpPort = current.TcpPort, LastSeen = DateTime.MinValue };
            lock (Field<object>(network, "_peerLock"))
                Field<Dictionary<string, PeerInfo>>(network, "_peers").Add(duplicate.Id, duplicate);

            var probe = typeof(NetworkService).GetMethod("TryTcpHandshake", Private);
            var interfaces = Field<NetworkInterfaceDetail[]>(network, "_activeInterfaces");
            DateTime currentSeen = current.LastSeen;
            foreach (PeerInfo expected in new[] { duplicate, null, current })
            {
                if (expected == current) network.RemovePeer(duplicate.Id);
                bool reached = false;
                var worker = new Thread(() => reached = (bool)probe.Invoke(network,
                    new object[] { current.IP, current.TcpPort, interfaces, expected })) { IsBackground = true };
                worker.Start();
                WaitFor(listener.Pending, "待验证的 TCP 探测已连接");
                using (var client = listener.AcceptTcpClient())
                {
                    ReadHandshake(client, network);
                    client.GetStream().WriteByte(0x01);
                }
                Require(worker.Join(3000) && reached == (expected != duplicate), "确认结果符合端点归属");
                Require(duplicate.LastSeen == DateTime.MinValue, "端点冲突时不续期候选旧身份");
                if (expected == current)
                    Require(current.LastSeen > currentSeen, "单一已知端点保留旧版纯 TCP 复核");
                else
                    Require(current.LastSeen == currentSeen, "无身份的探测不续期已知身份");
            }
        }

        private static void CheckListRebuild(MainForm form)
        {
            var list = Field<SessionListBox>(form, "_listSessions");
            var search = Field<TextBox>(form, "_txtSearch");
            list.SelectedIndex = 0;
            list.TopIndex = 8;
            object anchor = list.Items[list.TopIndex];
            Call(form, "RenderSessionList");
            Require(list.SelectedIndex == 0 && list.TopIndex > 0 && list.Items[list.TopIndex] == anchor,
                "选中群聊时重建保留向下浏览的视口锚点");

            var last = (PeerInfo)list.Items[list.Items.Count - 1];
            string target = list.Items.OfType<PeerInfo>().First(peer => peer != last && peer.TcpPort == 0).NickName;
            foreach (bool selectLast in new[] { true, false })
            {
                list.SelectedIndex = selectLast ? list.Items.Count - 1 : 0;
                search.Text = target;
                Require(list.Items.Count == 2 && list.SelectedIndex == -1 && list.TopIndex == 0, "缩表后清空旧选择并夹紧视口");
                PressKey(list, selectLast ? Keys.Home : Keys.Down);
                Require(list.SelectedIndex == 1 && !list.IsHeading(list.SelectedIndex), "搜索缩表后键盘跳到有效行");
                search.Clear();
            }

            using (var window = new Form())
            using (var isolated = new SessionListBox { IsSectionHeading = item => item is SessionPresenter.GroupHeading })
            {
                window.Controls.Add(isolated);
                window.Show();
                isolated.Items.AddRange(new object[] { "群聊", SessionPresenter.GroupHeading.Online,
                    "好友", SessionPresenter.GroupHeading.Offline });
                isolated.SelectedIndex = 0;
                PressKey(isolated, Keys.End);
                Require(isolated.SelectedIndex == 2, "末尾标题回退到最近会话");
                isolated.ClearSessionItems();
                isolated.Items.Add(SessionPresenter.GroupHeading.Offline);
                PressKey(isolated, Keys.Home);
                Require(isolated.SelectedIndex == -1, "只有标题时无选中会话");
            }
            Console.WriteLine("LC-FR-003 / LC-FR-008: 缩表方向键、空分组和滚动锚点通过");
        }

        private static void CheckScrollDrag(MainForm form)
        {
            var list = Field<SessionListBox>(form, "_listSessions");
            var search = Field<TextBox>(form, "_txtSearch");
            list.SelectedIndex = 0;
            list.TopIndex = 0;
            int headerY = list.GetItemRectangle(1).Top + 8;
            int edgeX = list.ClientSize.Width - 5;
            Require(list.IsHeading(list.IndexFromPoint(edgeX, headerY)), "滑块与标题行重叠");
            Mouse(list, 0x0201, 1, 20, headerY);
            Mouse(list, 0x0202, 0, 20, headerY);
            Require(list.SelectedIndex == 0 && !Field<bool>(list, "_isThumbDragging"), "正文区域标题点击保持原选择");
            Mouse(list, 0x0201, 1, edgeX, headerY);
            Require(Field<bool>(list, "_isThumbDragging"), "标题高度上的滑块热区开始拖拽");
            Mouse(list, 0x0200, 1, edgeX, headerY + 110);
            Require(list.TopIndex > 0, "拖拽推动视口");
            Mouse(list, 0x0202, 0, edgeX, headerY + 110);
            Require(!Field<bool>(list, "_isThumbDragging") && list.SelectedIndex == 0, "松开滑块后保留群聊选择");

            search.Text = list.Items.OfType<PeerInfo>().First(peer => peer.TcpPort == 0).NickName;
            Mouse(list, 0x0201, 1, edgeX, 8);
            Require(!Field<bool>(list, "_isThumbDragging") && list.SelectedIndex == -1, "无溢出时右侧标题仍不可选");
            Mouse(list, 0x0201, 1, edgeX, -2);
            Mouse(list, 0x0202, 0, edgeX, -2);
            search.Clear();
            Console.WriteLine("LC-FR-004: 标题行热区拖拽、无溢出拦截和 x64 负坐标通过");
        }

        private static void CheckComposerUi(MainForm form)
        {
            Require(Field<Control>(form, "_heroProfilePanel") is DoubleBufferedPanel,
                "左上角头像面板未启用双缓冲，缩放时可能闪烁");
            using (var recipients = new SessionListBox { SelectionMode = SelectionMode.MultiSimple })
            {
                recipients.Items.Add(new PeerInfo { Id = "multi-1", NickName = "在线一" });
                recipients.Items.Add(new PeerInfo { Id = "multi-2", NickName = "在线二" });
                recipients.Items.Add(new PeerInfo { Id = "multi-3", NickName = "在线三" });
                recipients.SetSelected(0, true);
                recipients.SetSelected(2, true);
                var selected = MainForm.ReadSelectedRecipients(recipients);
                Require(selected.Count == 2 && selected[0].Id == "multi-1" && selected[1].Id == "multi-3",
                    "群聊文件收件人多选结果不完整");
            }
            var list = Field<SessionListBox>(form, "_listSessions");
            int online = list.Items.IndexOf(SessionPresenter.GroupHeading.Online);
            int offline = list.Items.IndexOf(SessionPresenter.GroupHeading.Offline);
            Require(online >= 0 && offline > online, "在线和离线标题均出现");
            Require(list.GetItemHeight(online) == 32 && list.GetItemHeight(offline) == 40, "真实列表的离线标题间距为 8 像素");
            var draw = typeof(MainForm).GetMethod("ListSessions_DrawItem", Private);
            foreach (int index in new[] { online, offline })
            {
                var color = index == online ? GrokTheme.AccentGreen : GrokTheme.TextDestructive;
                using (var bitmap = new Bitmap(168, list.GetItemHeight(index)))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                    draw.Invoke(form, new object[] { list, new DrawItemEventArgs(graphics, list.Font, bounds, index, DrawItemState.None) });
                    int y = index == online ? 16 : 24;
                    Require(Enumerable.Range(40, 40).Any(x => bitmap.GetPixel(x, y).ToArgb() == color.ToArgb()), "标题右侧状态圆点颜色正确");
                }
            }

            var search = Field<TextBox>(form, "_txtSearch");
            search.Text = list.Items.OfType<PeerInfo>().First(peer => peer.TcpPort == 0).NickName;
            Require(list.GetItemHeight(0) == 32, "筛选后只剩离线标题时没有多余间距");
            search.Clear();

            var stageImage = typeof(MainForm).GetMethod("StageClipboardImage", Private);
            var remove = typeof(MainForm).GetMethod("RemoveAttachment", Private);
            var attachments = Field<SessionListBox>(form, "_attachmentList");
            using (var image = new Bitmap(200, 100))
                stageImage.Invoke(form, new object[] { image });
            Require(attachments.Visible && attachments.Items.Count == 1, "粘贴的图片出现在当前会话草稿");
            string file = (string)attachments.Items[0];
            var previews = Field<Dictionary<string, Image>>(form, "_draftImagePreviews");
            Require(File.Exists(file) && previews[file].Size == new Size(24, 12), "截图保存为 PNG 并按比例预览");
            remove.Invoke(form, new object[] { 0 });
            Require(!attachments.Visible && !File.Exists(file) && previews.Count == 0, "移除图片草稿后清理临时图片");

            // 输入框处于隐藏空态时，第一次右键应由外层胶囊转交暗色菜单。
            var input = Field<ChatInputBox>(form, "_txtInput");
            var inputPanel = Field<Panel>(form, "_inputPanel");
            input.Visible = false;
            Clipboard.SetText("右键粘贴文字");
            MethodInfo panelMouseDown = inputPanel.GetType().GetMethod("OnMouseDown", Private);
            Require(panelMouseDown != null, "输入胶囊缺少鼠标按下入口");
            panelMouseDown.Invoke(inputPanel, new object[] { new MouseEventArgs(MouseButtons.Right, 1, 20, 20, 0) });
            GrokPopupMenu menu = Application.OpenForms.OfType<GrokPopupMenu>().FirstOrDefault();
            Require(menu != null, "输入框首次右键未弹出暗色菜单");
            menu.Close();
            Console.WriteLine("LC-CHAT-COMPOSER-001: 联合标题间距、圆点颜色、图像草稿暂存与移除通过");
        }

        private static void CheckChatSelectionAndDraftSwitch(MainForm form)
        {
            var view = Field<WeChatChatView>(form, "_chatView");
            Require(view != null, "聊天视图不存在");
            string content = "甲👨‍👩‍👧‍👦👍🏽🇨🇳❤️1️⃣\r\n乙";
            view.Width = 520;
            view.Height = 300;
            view.SetMessages(new List<ChatMessage> { new ChatMessage { Content = content, Timestamp = DateTime.Now } });
            FieldInfo renderField = view.GetType().GetField("_renderItems", Private);
            Require(renderField != null, "未找到聊天消息行集合");
            var renderItems = (IList)renderField.GetValue(view);
            Require(renderItems != null && renderItems.Count > 0, "聊天视图未生成可选消息行");
            object item = renderItems[0];
            Require(item != null, "聊天消息行为空");
            MethodInfo hit = view.GetType().GetMethod("GetCharIndexAtPoint", Private);
            Require(hit != null, "未找到像素命中方法");
            using (Graphics graphics = view.CreateGraphics())
            using (Font font = view.GetMsgFont())
            {
                var boundaries = new HashSet<int> { 0 };
                for (int cursor = 0; cursor < content.Length; cursor = ChatTextLayout.NextBoundary(content, cursor))
                    boundaries.Add(ChatTextLayout.NextBoundary(content, cursor));
                FieldInfo linesField = item.GetType().GetField("Lines", AnyInstance);
                Require(linesField != null, "未找到消息视觉行字段");
                var lines = (IList)linesField.GetValue(item);
                Require(lines != null && lines.Count > 0, "聊天消息行未生成视觉行");
                foreach (object line in lines)
                {
                    Require(line != null, "消息视觉行为空");
                    FieldInfo boundsField = line.GetType().GetField("Bounds", AnyInstance);
                    Require(boundsField != null, "未找到视觉行边界字段");
                    RectangleF bounds = (RectangleF)boundsField.GetValue(line);
                    for (int x = (int)bounds.Left; x <= (int)bounds.Right; x++)
                    {
                        int index = (int)hit.Invoke(view, new object[] { item, new Point(x, (int)bounds.Top + 2), graphics, font });
                        Require(boundaries.Contains(index),
                            "像素命中落在组合表情内部");
                    }
                }
            }

            view.GetType().GetField("_selectedItem", Private).SetValue(view, item);
            view.SelectAllCurrentMessage();
            view.CopySelectedText();
            Require(Clipboard.GetText() == content, "跨 CRLF 复制改变了原始消息");

            var attachmentList = Field<SessionListBox>(form, "_attachmentList");
            MethodInfo stage = form.GetType().GetMethod("StageClipboardImage", Private);
            using (var image = new Bitmap(32, 16)) stage.Invoke(form, new object[] { image });
            Require(attachmentList.Items.Count == 1, "草稿未进入当前会话");
            string path = (string)attachmentList.Items[0];
            var presenter = Field<SessionPresenter>(form, "_sessionPresenter");
            presenter.SelectPeerChat(new PeerInfo { Id = "draft-peer", NickName = "草稿好友", IP = IPAddress.Parse("127.0.0.250"), TcpPort = 0 });
            Require(attachmentList.Items.Count == 0, "切换会话后草稿泄漏到新会话");
            presenter.SelectSession(SessionPresenter.BROADCAST_TARGET);
            Require(attachmentList.Items.Count == 1 && (string)attachmentList.Items[0] == path,
                "切回原会话后草稿未恢复");
            form.GetType().GetField("_isExplicitExit", Private).SetValue(form, true);
            form.GetType().GetMethod("MainForm_FormClosing", Private).Invoke(form, new object[] { form, new FormClosingEventArgs(CloseReason.UserClosing, false) });
            form.Dispose();
            Require(!File.Exists(path), "关闭窗口未清理未发送截图草稿");
            Console.WriteLine("LC-CC-006: 组合表情像素命中、跨 CRLF 复制、切会话草稿和关闭清理通过");
        }

        private static void SendIdentity(UdpClient udp, string id, string nick, int port)
        {
            byte[] data = Encoding.UTF8.GetBytes(string.Format("ACK|{0}|{1}|fixture|{2}|{3}", id, nick, port, PeerAddress));
            udp.Send(data, data.Length, new IPEndPoint(IPAddress.Loopback, NetworkService.UDP_PORT));
        }

        private static void ReadHello(UdpClient udp, string localId, IPAddress advertisedIp)
        {
            udp.Client.ReceiveTimeout = 1800;
            var sender = new IPEndPoint(IPAddress.Any, 0);
            var parts = Encoding.UTF8.GetString(udp.Receive(ref sender)).Split('|');
            Require(parts.Length == 6 && parts[0] == "HELLO" && parts[1] == localId && parts[5] == advertisedIp.ToString(),
                "收到使用预期网卡 IPv4 的原格式 UDP 公告");
        }

        private static void ReadHandshake(TcpClient client, NetworkService network)
        {
            client.ReceiveTimeout = 1800;
            using (var reader = new BinaryReader(client.GetStream(), Encoding.UTF8, true))
            {
                Require(Encoding.ASCII.GetString(reader.ReadBytes(4)) == "LANC" && reader.ReadByte() == 1 && reader.ReadBoolean() &&
                    reader.ReadString() == network.LocalId && reader.ReadString() == network.NickName && reader.ReadString() == "" &&
                    reader.ReadString() == NetworkService.HANDSHAKE_MSG, "LANC 握手帧及字段保持兼容");
            }
        }

        private static IPAddress BoundAddress(NetworkInterfaceDetail[] interfaces)
        {
            return ((IPEndPoint)interfaces.Single().UdpSocket.LocalEndPoint).Address;
        }

        private static bool IsClosed(Socket socket)
        {
            try { return socket.LocalEndPoint == null; }
            catch (ObjectDisposedException) { return true; }
        }

        private static void Click(Control control) { Call(control, "OnClick", EventArgs.Empty); }

        private static void PressKey(Control control, Keys key)
        {
            control.Focus();
            SendMessage(control.Handle, 0x0100, (IntPtr)key, IntPtr.Zero);
        }

        private static void Mouse(Control control, int message, int buttons, int x, int y)
        {
            uint packed = ((uint)(ushort)y << 16) | (ushort)x;
            SendMessage(control.Handle, message, (IntPtr)buttons, new IntPtr(packed));
        }

        private static void Call(object owner, string method, params object[] args)
        {
            owner.GetType().GetMethod(method, Private).Invoke(owner, args);
        }

        private static T Field<T>(object owner, string name)
        {
            return (T)owner.GetType().GetField(name, Private).GetValue(owner);
        }

        private static void WaitFor(Func<bool> condition, string reason)
        {
            var watch = Stopwatch.StartNew();
            while (!condition() && watch.ElapsedMilliseconds < 4000)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }
            Require(condition(), reason);
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }
    }
}
