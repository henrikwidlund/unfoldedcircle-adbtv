using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;

using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Exceptions;
using AdvancedSharpAdbClient.Models;

using UnfoldedCircle.AdbTv.Cancellation;
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
                var connectResult = await RunWithRetryWithReturn(() =>
                        deviceClientHolder.DeviceClient.AdbClient.ConnectAsync(adbTvClientKey.IpAddress, adbTvClientKey.Port, cancellationToken),
                    _logger,
                    true,
                    cancellationToken);
                if (connectResult?.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase) is true)
                {
                    try
                    {
                        // check if the cached client is still healthy
                        await RunWithRetry(() =>
                                deviceClientHolder.DeviceClient.AdbClient.ExecuteRemoteCommandAsync("true", deviceClientHolder.DeviceClient.Device, cancellationToken),
                            _logger,
                            true,
                            cancellationToken);
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
                string? connectResult;
                do
                {
                    connectResult = await RunWithRetryWithReturn(() =>
                        adbClient.ConnectAsync(adbTvClientKey.IpAddress, adbTvClientKey.Port, cancellationToken),
                        _logger,
                        true,
                        cancellationToken);
                    if (connectResult?.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase) is not true)
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                } while (connectResult?.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase) is not true &&
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

        static async ValueTask<T?> RunWithRetryWithReturn<T>(Func<Task<T>> func,
            ILogger<AdbTvClientFactory> logger,
            bool allowRetry,
            CancellationToken cancellationToken)
        {
            try
            {
                return await func();
            }
            catch (Exception e)
            {
                if (allowRetry)
                {
                    logger.ActionFailedWillRetry(e);
                    await Task.SafeDelay(500, cancellationToken);
                    return await RunWithRetryWithReturn(func, logger, false, cancellationToken);
                }

                throw;
            }
        }

        static async ValueTask RunWithRetry(Func<Task> func,
            ILogger<AdbTvClientFactory> logger,
            bool allowRetry,
            CancellationToken cancellationToken)
        {
            try
            {
                await func();
            }
            catch (Exception e)
            {
                if (allowRetry)
                {
                    logger.ActionFailedWillRetry(e);
                    await Task.SafeDelay(500, cancellationToken);
                    await RunWithRetry(func, logger, false, cancellationToken);
                    return;
                }

                throw;
            }
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