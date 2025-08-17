using System.Net;
using System.Net.Sockets;

namespace UnfoldedCircle.AdbTv.WoL;

internal static class WakeOnLan
{
    public static async Task SendWakeOnLan(IPAddress ipAddress, string macAddress)
    {
        byte[] magicPacket = CreateMagicPacket(macAddress);
        var socket = new Socket(ipAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
        await socket.ConnectAsync(ipAddress,9);
        await socket.SendAsync(magicPacket);
    }

    private static byte[] CreateMagicPacket(string macAddress) =>
        Convert.FromHexString(new string('F', 12)
                              + string.Concat(Enumerable.Repeat(
                                  macAddress.Replace(":", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal),
                                  16
                              )));
}