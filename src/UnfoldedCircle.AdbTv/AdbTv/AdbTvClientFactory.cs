using System.Collections.Concurrent;
using System.Globalization;

using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;

namespace UnfoldedCircle.AdbTv.AdbTv;

public class AdbTvClientFactory(ILogger<AdbTvClientFactory> logger)
{
    private readonly ILogger<AdbTvClientFactory> _logger = logger;
    private readonly ConcurrentDictionary<AdbTvClientKey, DeviceClient> _clients = new();
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    public async ValueTask<DeviceClient?> TryGetOrCreateClientAsync(AdbTvClientKey adbTvClientKey, CancellationToken cancellationToken)
    {
        if (_clients.TryGetValue(adbTvClientKey, out var client))
            return client;

        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            var adbClient = new AdbClient();
            string connectResult;
            do
            {
                connectResult = await adbClient.ConnectAsync(adbTvClientKey.IpAddress, adbTvClientKey.Port, cancellationToken);
            } while (!connectResult.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase));

            var deviceData = (await adbClient.GetDevicesAsync(cancellationToken)).FirstOrDefault(x =>
                x.Serial.Equals($"{adbTvClientKey.IpAddress}:{adbTvClientKey.Port.ToString(NumberFormatInfo.InvariantInfo)}", StringComparison.InvariantCulture));
            var deviceClient = deviceData.CreateDeviceClient();
            _clients[adbTvClientKey] = deviceClient;
            return deviceClient;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create client {ClientKey}", adbTvClientKey);
            return null;
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public void TryRemoveClient(in AdbTvClientKey adbTvClientKey)
    {
        try
        {
            _clients.TryRemove(adbTvClientKey, out _);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to remove client {ClientKey}", adbTvClientKey);
            throw;
        }
    }

    public void RemoveAllClients() => _clients.Clear();
}