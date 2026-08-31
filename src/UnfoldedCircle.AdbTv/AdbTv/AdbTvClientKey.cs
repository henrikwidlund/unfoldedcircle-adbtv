using UnfoldedCircle.AdbTv.Configuration;

namespace UnfoldedCircle.AdbTv.AdbTv;

/// <summary>
/// Identifies a configured device. <see cref="IpAddress"/>/<see cref="Port"/> are the last-known
/// connect address: for wirelessly-paired devices (<see cref="PairedDeviceGuid"/> set),
/// <see cref="AdbTvClientFactory"/> re-resolves the actual current address via mDNS before each
/// (re)connect and only falls back to these values if that resolution fails.
/// </summary>
public readonly record struct AdbTvClientKey(
    string IpAddress, string MacAddress, int Port, Manufacturer Manufacturer, bool AllowReauth, string? PairedDeviceGuid = null);
