using UnfoldedCircle.Server.Configuration;

namespace UnfoldedCircle.AdbTv.Configuration;

public record AdbConfigurationItem : UnfoldedCircleConfigurationItem
{
    public required string MacAddress { get; init; }
    public required int Port { get; init; }
    public required Manufacturer Manufacturer { get; init; }
}