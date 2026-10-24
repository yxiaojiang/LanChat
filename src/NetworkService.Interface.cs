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
