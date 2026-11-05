using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LanChat
{
    /// <summary>
    /// 局域网物理与虚拟网络适配器详情实体
    /// </summary>
    public class NetworkInterfaceDetail
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public IPAddress IP { get; set; }
        public IPAddress Mask { get; set; }
        public IPAddress BroadcastAddress { get; set; }
        public bool IsVirtualOrTun { get; set; }
        public Socket UdpSocket { get; set; }
    }

    public partial class NetworkService : IDisposable
    {
        #region Interface Discovery

        private void DiscoverInterfaces()
        {
            _activeInterfaces.Clear();

            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    bool isVirt = IsVirtualOrTun(ni);
                    var ipProps = ni.GetIPProperties();

                    foreach (var u in ipProps.UnicastAddresses)
                    {
                        if (u.Address.AddressFamily == AddressFamily.InterNetwork && u.IPv4Mask != null)
                        {
                            byte[] ipBytes = u.Address.GetAddressBytes();
                            byte[] maskBytes = u.IPv4Mask.GetAddressBytes();
                            byte[] bcastBytes = new byte[4];
                            for (int i = 0; i < 4; i++)
                            {
                                bcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                            }

                            _activeInterfaces.Add(new NetworkInterfaceDetail
                            {
                                Name = ni.Name,
                                Description = ni.Description,
                                IP = u.Address,
                                Mask = u.IPv4Mask,
                                BroadcastAddress = new IPAddress(bcastBytes),
                                IsVirtualOrTun = isVirt
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("网卡探测异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 判定指定网卡是否为虚拟网卡、VPN 或 TUN/TAP 适配器（防止广播穿透与路由风暴）
        /// </summary>
        /// <param name="ni">网络接口实体</param>
        /// <returns>true 表示虚拟网卡，false 表示物理真实网卡</returns>
        public static bool IsVirtualOrTun(NetworkInterface ni)
        {
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) return true;

            string desc = (ni.Description ?? "").ToLowerInvariant();
            string name = (ni.Name ?? "").ToLowerInvariant();

            if (desc.Contains("tun") || desc.Contains("tap") || desc.Contains("vpn") ||
                desc.Contains("virtual") || desc.Contains("wsl") || desc.Contains("hyper-v") ||
                desc.Contains("vmware") || desc.Contains("vbox") || desc.Contains("pseudo") ||
                name.Contains("tun") || name.Contains("tap") || name.Contains("vpn"))
            {
                return true;
            }

            try
            {
                byte[] mac = ni.GetPhysicalAddress().GetAddressBytes();
                if (mac == null || mac.Length == 0) return true;
            }
            catch
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取本机首选的主物理局域网 IPv4 地址
        /// </summary>
        /// <returns>物理 IPv4 地址</returns>
        public IPAddress GetPrimaryPhysicalIp()
        {
            foreach (var iface in _activeInterfaces)
            {
                if (!iface.IsVirtualOrTun)
                {
                    return iface.IP;
                }
            }

            if (_activeInterfaces.Count > 0)
            {
                return _activeInterfaces[0].IP;
            }

            return IPAddress.Loopback;
        }

        /// <summary>
        /// 根据目标 IP 匹配最佳的本机源出接口端点，防止多网卡跨路由错位
        /// </summary>
        /// <param name="targetIp">目标 IP 地址</param>
        /// <returns>最佳本地绑定 IPEndPoint</returns>
        public IPEndPoint GetLocalEndpointForTarget(IPAddress targetIp)
        {
            if (IPAddress.IsLoopback(targetIp))
            {
                return new IPEndPoint(IPAddress.Any, 0);
            }

            // 1. Check if target is on the same subnet as any of our physical interfaces
            foreach (var iface in _activeInterfaces)
            {
                if (!iface.IsVirtualOrTun && IsInSameSubnet(iface.IP, targetIp, iface.Mask))
                {
                    return new IPEndPoint(iface.IP, 0);
                }
            }

            // 2. Try any physical interface
            foreach (var iface in _activeInterfaces)
            {
                if (!iface.IsVirtualOrTun)
                {
                    return new IPEndPoint(iface.IP, 0);
                }
            }

            // 3. Fallback
            return new IPEndPoint(IPAddress.Any, 0);
        }

        /// <summary>
        /// 判断两个 IPv4 地址在给定子网掩码下是否属于同一子网
        /// </summary>
        /// <param name="ip1">地址 1</param>
        /// <param name="ip2">地址 2</param>
        /// <param name="mask">子网掩码</param>
        /// <returns>true 表示同子网</returns>
        public static bool IsInSameSubnet(IPAddress ip1, IPAddress ip2, IPAddress mask)
        {
            if (ip1 == null || ip2 == null || mask == null) return false;
            if (ip1.AddressFamily != AddressFamily.InterNetwork || ip2.AddressFamily != AddressFamily.InterNetwork) return false;

            byte[] b1 = ip1.GetAddressBytes();
            byte[] b2 = ip2.GetAddressBytes();
            byte[] m = mask.GetAddressBytes();
            if (b1.Length != 4 || b2.Length != 4 || m.Length != 4) return false;

            for (int i = 0; i < 4; i++)
            {
                if ((b1[i] & m[i]) != (b2[i] & m[i])) return false;
            }
            return true;
        }

        private bool IsInAnyLocalSubnet(IPAddress ip)
        {
            foreach (var iface in _activeInterfaces)
            {
                if (!iface.IsVirtualOrTun && IsInSameSubnet(iface.IP, ip, iface.Mask))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion
    }
}
