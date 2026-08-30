using System.Security.Cryptography;

namespace UnfoldedCircle.AdbTv.WebSocket;

internal static class AppNamesHelper
{
    private const string ResourceName = "UnfoldedCircle.AdbTv.AppNames.dex";
    private static readonly Lazy<HelperData> Data = new(LoadHelperData);

    internal static string RemotePath => Data.Value.RemotePath;
    internal static string DexBase64 => Data.Value.DexBase64;

    private static HelperData LoadHelperData()
    {
        using var resource = typeof(AppNamesHelper).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded Android app names helper resource '{ResourceName}' was not found.");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);

        var dex = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(dex)).ToLowerInvariant()[..12];
        return new HelperData(
            $"/data/local/tmp/uc-adbtv-appnames-{hash}.dex",
            Convert.ToBase64String(dex));
    }

    private sealed record HelperData(string RemotePath, string DexBase64);
}
