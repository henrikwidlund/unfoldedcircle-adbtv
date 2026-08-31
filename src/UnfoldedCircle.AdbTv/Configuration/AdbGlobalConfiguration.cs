using UnfoldedCircle.Server.Configuration;

namespace UnfoldedCircle.AdbTv.Configuration;

public sealed record AdbGlobalConfiguration : UnfoldedCircleGlobalConfiguration
{
    public ushort PollingIntervalSeconds { get; init; } = 5;
}
