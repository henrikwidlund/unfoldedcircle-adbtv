using UnfoldedCircle.Server.Configuration;

namespace UnfoldedCircle.AdbTv.Configuration;

public record AdbConfigurationItem : UnfoldedCircleConfigurationItem
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
}
