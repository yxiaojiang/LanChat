using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 设置中心 - 物理网络与设备面板
    /// </summary>
    public class NetworkDeviceSettingsPanel : SettingsPanelBase
    {
        public NetworkDeviceSettingsPanel(SettingsContext context) : base(context)
        {
            BuildComponents();
            AutoFitHeight();
        }

        private void BuildComponents()
        {
            int curY = 4;

            Label lblNet = CreateSectionHeader("物理网络与设备", curY);
            this.Controls.Add(lblNet);
            curY += 28;

            Panel cardNet = CreateCardPanel(CARD_W, 162);
            cardNet.Location = new Point(0, curY);

            string ipStr = Context.NetService != null ? Context.NetService.GetPrimaryPhysicalIp().ToString() : "127.0.0.1";
            int port = Context.NetService != null ? Context.NetService.LocalTcpPort : 38889;

            cardNet.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = new Font(GrokTheme.FontFamily, 10.0f, FontStyle.Regular))
                using (Font sf = new Font(GrokTheme.FontFamily, 8.5f, FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(ColTextPrimary))
                using (SolidBrush sb = new SolidBrush(ColTextSecondary))
                using (Pen sep = new Pen(ColCardDivider, 1f))
                {
                    // Row 1
                    g.DrawString("物理以太网 / Wi-Fi IP 绑定", f, tb, new PointF(20, 9));
                    g.DrawString("IP: " + ipStr + ":" + port + "  ·  UDP 38888 广播服务就绪", sf, sb, new PointF(20, 29));
                    g.DrawLine(sep, 16, 54, CARD_W - 16, 54);

                    // Row 2
                    g.DrawString("TUN / TAP 虚拟网卡穿透过滤", f, tb, new PointF(20, 54 + 9));
                    g.DrawString("智能识别并过滤 Clash / WireGuard 虚拟网卡，防止广播风暴。", sf, sb, new PointF(20, 54 + 29));
                    g.DrawLine(sep, 16, 108, CARD_W - 16, 108);

                    // Row 3
                    g.DrawString("多网卡并发单播探测", f, tb, new PointF(20, 108 + 9));
                    g.DrawString("在局域网复杂交换机拓扑下并发向物理子网各节点单播探测。", sf, sb, new PointF(20, 108 + 29));
                }
            };

            Button btnStatus = CreateDropdownPill("● 局域网在线  ∨", 126, 28, null);
            btnStatus.Location = new Point(CARD_W - btnStatus.Width - 20, (54 - 32) / 2);
            cardNet.Controls.Add(btnStatus);

            ModernToggleSwitch swTun = new ModernToggleSwitch(true, null);
            swTun.Location = new Point(CARD_W - swTun.Width - 24, 54 + (54 - 22) / 2);
            cardNet.Controls.Add(swTun);

            ModernToggleSwitch swUnicast = new ModernToggleSwitch(true, null);
            swUnicast.Location = new Point(CARD_W - swUnicast.Width - 24, 108 + (54 - 22) / 2);
            cardNet.Controls.Add(swUnicast);

            this.Controls.Add(cardNet);
        }
    }
}
