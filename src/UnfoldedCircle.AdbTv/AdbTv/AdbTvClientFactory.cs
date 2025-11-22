using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;

using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Exceptions;
using AdvancedSharpAdbClient.Models;

using UnfoldedCircle.AdbTv.Logging;

namespace UnfoldedCircle.AdbTv.AdbTv;

public class AdbTvClientFactory(ILogger<AdbTvClientFactory> logger)
{
    private readonly ILogger<AdbTvClientFactory> _logger = logger;
    private readonly ConcurrentDictionary<AdbTvClientKey, DeviceClientHolder> _clients = new();
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan HealthyDeviceTimeout = TimeSpan.FromSeconds(5);

    public async ValueTask<DeviceClient?> TryGetOrCreateClientAsync(AdbTvClientKey adbTvClientKey, CancellationToken cancellationToken)
    {
        try
        {
            if (_clients.TryGetValue(adbTvClientKey, out var deviceClientHolder) && Stopwatch.GetElapsedTime(deviceClientHolder.AddedAt) < CacheDuration)
            {
                var connectResult = await deviceClientHolder.DeviceClient.AdbClient.ConnectAsync(adbTvClientKey.IpAddress, adbTvClientKey.Port, cancellationToken);
                if (connectResult.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        // check if the cached client is still healthy
                        await deviceClientHolder.DeviceClient.AdbClient.ExecuteRemoteCommandAsync("true", deviceClientHolder.DeviceClient.Device, cancellationToken);
                        return deviceClientHolder.DeviceClient;
                    }
                    catch (AdbException e)
                    {
                        _logger.ClientFailedHealthCheck(e, adbTvClientKey);
                    }
                }
            }

            await _semaphoreSlim.WaitAsync(cancellationToken);
            // another thread might have created the client while we were waiting for the semaphore
            if (_clients.TryGetValue(adbTvClientKey, out deviceClientHolder) && Stopwatch.GetElapsedTime(deviceClientHolder.AddedAt) < CacheDuration)
                return deviceClientHolder.DeviceClient;

            try
            {
                var startTime = Stopwatch.GetTimestamp();
                var adbClient = new AdbClient();
                string connectResult;
                do
                {
                    connectResult = await adbClient.ConnectAsync(adbTvClientKey.IpAddress, adbTvClientKey.Port, cancellationToken);
                    if (!connectResult.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase))
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                } while (!connectResult.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase) &&
                         Stopwatch.GetElapsedTime(startTime) < HealthyDeviceTimeout && !cancellationToken.IsCancellationRequested);

                startTime = Stopwatch.GetTimestamp();
                DeviceClient? deviceClient = null;
                var serial = $"{adbTvClientKey.IpAddress}:{adbTvClientKey.Port.ToString(NumberFormatInfo.InvariantInfo)}";
                while (Stopwatch.GetElapsedTime(startTime) < HealthyDeviceTimeout && !cancellationToken.IsCancellationRequested)
                {
                    var deviceData = (await adbClient.GetDevicesAsync(cancellationToken)).FirstOrDefault(x =>
                        x.Serial.Equals(serial, StringComparison.InvariantCulture));
                    deviceClient = deviceData.CreateDeviceClient();
                    if (deviceClient.Device.State == DeviceState.Online || !RetryStates.Contains(deviceClient.Device.State))
                        break;

                    await Task.Delay(100, cancellationToken);
                }

                if (deviceClient is { Device.State: DeviceState.Online })
                {
                    await deviceClient.AdbClient.ExecuteRemoteCommandAsync("true", deviceClient.Device, cancellationToken);
                    _clients[adbTvClientKey] = new DeviceClientHolder(deviceClient, Stopwatch.GetTimestamp());
                    return deviceClient;
                }

                _logger.DeviceNotOnline(adbTvClientKey, connectResult, deviceClient?.Device.State);

                return null;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }
        catch (Exception e)
        {
            _logger.FailedToGetOrCreateClient(e, adbTvClientKey);
            return null;
        }
    }

    private sealed record DeviceClientHolder(DeviceClient DeviceClient, long AddedAt);

    private static readonly FrozenSet<DeviceState> RetryStates =
    [
        DeviceState.Connecting,
        DeviceState.Offline,
        DeviceState.Unauthorized,
        DeviceState.Unknown
    ];

    public async ValueTask TryRemoveClientAsync(AdbTvClientKey adbTvClientKey, CancellationToken cancellationToken)
    {
        if (_clients.TryRemove(adbTvClientKey, out var deviceClient))
        {
            try
            {
                await deviceClient.DeviceClient.AdbClient.DisconnectAsync(adbTvClientKey.IpAddress, adbTvClientKey.Port, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.FailedToRemoveClient(e, adbTvClientKey);
                throw;
            }
        }
    }

    public void RemoveAllClients() => _clients.Clear();
}