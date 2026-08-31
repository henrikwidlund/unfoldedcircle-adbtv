using System.Security.Cryptography;

namespace UnfoldedCircle.AdbTv.WebSocket;

internal static class AppNamesHelper
{
    private const string ResourceName = "UnfoldedCircle.AdbTv.AppNames.dex";

    // The helper is only a few KiB and may need to be uploaded to multiple devices. Keep the
    // Base64 representation cached; the temporary byte[] used while loading is not retained.
    private static readonly Lazy<HelperData> Data = new(LoadHelperData);

    internal static string RemotePath => Data.Value.HelperRemotePath;
    internal static string DexBase64 => Data.Value.HelperDexBase64;

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

    private sealed record HelperData(string HelperRemotePath, string HelperDexBase64);
}
