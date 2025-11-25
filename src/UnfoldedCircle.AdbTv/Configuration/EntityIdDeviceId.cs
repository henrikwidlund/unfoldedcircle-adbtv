using System.Runtime.InteropServices;

namespace UnfoldedCircle.AdbTv.Configuration;

[StructLayout(LayoutKind.Auto)]
public record struct EntityIdDeviceId(string EntityId, string? DeviceId);