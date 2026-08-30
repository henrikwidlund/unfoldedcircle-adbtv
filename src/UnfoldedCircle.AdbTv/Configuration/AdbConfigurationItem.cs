using UnfoldedCircle.Server.Configuration;

namespace UnfoldedCircle.AdbTv.Configuration;

public sealed record AdbConfigurationItem : UnfoldedCircleConfigurationItem
{
    public required string MacAddress { get; init; }
    public required int Port { get; init; }
    public required Manufacturer Manufacturer { get; init; }

    /// <summary>
    /// When <see langword="true"/>, after exhausting signature-only attempts within the connect budget, the
    /// integration falls through to <c>AUTH(RSAPUBLICKEY)</c> which may trigger an approval
    /// dialog on the device. This provides auto-recovery if the device forgot the key.
    /// When <see langword="false"/>, the integration only attempts signature auth; if it fails the user must
    /// re-run setup to re-pair.
    /// </summary>
    public bool AllowReauth { get; init; } = true;

    /// <summary>
    /// The device GUID returned by Android's wireless-debugging pairing handshake
    /// (<see cref="Theodicean.SharpAdb.Pairing.PeerInfoType.AdbDeviceGuid"/>), when this entity was set up via
    /// wireless pairing rather than a manually-entered IP. When set, this is also the mDNS instance name the
    /// device advertises for <c>_adb-tls-connect._tcp</c>, used to re-resolve the current (frequently-changing)
    /// connect port before each connection attempt. <see cref="Host"/>/<see cref="Port"/> are still kept up to
    /// date as a fallback for when mDNS resolution is unavailable (e.g. multicast blocked on the network).
    /// </summary>
    public string? PairedDeviceGuid { get; init; }
}
