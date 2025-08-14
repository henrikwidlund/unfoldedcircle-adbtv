using System.Collections.Frozen;

using UnfoldedCircle.Models.Events;

namespace UnfoldedCircle.AdbTv.AdbTv;

public static class AdbTvEntitySettings
{
    public static readonly FrozenSet<RemoteFeature> RemoteFeatures = new[]
    {
        RemoteFeature.OnOff,
        RemoteFeature.SendCmd,
        RemoteFeature.Toggle
    }.ToFrozenSet();
}