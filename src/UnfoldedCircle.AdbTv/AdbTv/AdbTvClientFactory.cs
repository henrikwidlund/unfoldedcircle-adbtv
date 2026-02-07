using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;

using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Models;

using UnfoldedCircle.AdbTv.Cancellation;
using UnfoldedCircle.AdbTv.Logging;

namespace UnfoldedCircle.AdbTv.AdbTv;

public class AdbTvClientFactory(ILogger<AdbTvClientFactory> logger)
{
    private readonly ILogger<AdbTvClientFactory> _logger = logger;
    private readonly ConcurrentDictionary<AdbTvClientKey, DeviceClientHolder> _clients = new();
    private readonly SemaphoreSlim _globalSemaphore = new(1, 1);

    private static readonly TimeSpan HealthyDeviceTimeout = TimeSpan.FromSeconds(4.5);

    public async ValueTask<DeviceClient?> TryGetOrCreateClientAsync(AdbTvClientKey adbTvClientKey, CancellationToken cancellationToken)
    {
        if (!_clients.TryGetValue(adbTvClientKey, out var deviceClientHolder))
        {
            if (await _globalSemaphore.WaitAsync(HealthyDeviceTimeout, cancellationToken))
            {
                try
                {
                    // a new client was just added by another thread, assume healthy since it was just added.
                    if (_clients.TryGetValue(adbTvClientKey, out deviceClientHolder))
                        return deviceClientHolder.DeviceClient;

                    return await CreateDeviceClientAsync(adbTvClientKey, null, cancellationToken);
                }
                finally
                {
                    _globalSemaphore.Release();
                }
            }

            _logger.TimeoutWaitingForGlobalSemaphore(adbTvClientKey);
            return null;
        }

        if (!await deviceClientHolder.Semaphore.WaitAsync(HealthyDeviceTimeout, cancellationToken))
        {
            _logger.TimeoutWaitingForDeviceSemaphore(adbTvClientKey);
            return null;
        }

        try
        {
            return await GetHealthyClientAsync(adbTvClientKey, deviceClientHolder, cancellationToken);
        }
        finally
        {
            deviceClientHolder.Semaphore.Release();
        }
    }

    private async ValueTask<DeviceClient?> CreateDeviceClientAsync(
        AdbTvClientKey adbTvClientKey,
        SemaphoreSlim? deviceSemaphore,
        CancellationToken cancellationToken)
    {
        try
        {
            var startTime = Stopwatch.GetTimestamp();
            var adbClient = new AdbClient();
            string? connectResult = await ConnectResultAsync(adbTvClientKey, adbClient, _logger, startTime, cancellationToken);

            startTime = Stopwatch.GetTimestamp();
            DeviceClient? deviceClient = null;
            var serial = $"{adbTvClientKey.IpAddress}:{adbTvClientKey.Port.ToString(NumberFormatInfo.InvariantInfo)}";
            while (Stopwatch.GetElapsedTime(startTime) < HealthyDeviceTimeout && !cancellationToken.IsCancellationRequested)
            {
                var deviceData = (await adbClient.GetDevicesAsync(cancellationToken)).FirstOrDefault(x =>
                    x.Serial.Equals(serial, StringComparison.InvariantCulture));
                deviceClient = deviceData?.CreateDeviceClient();
                if (deviceClient is { Device.State: DeviceState.Online } || deviceClient is not null && !RetryStates.Contains(deviceClient.Device.State))
                    break;

                await Task.Delay(100, cancellationToken);
            }

            if (deviceClient is not { Device.State: DeviceState.Online })
            {
                _logger.DeviceNotOnline(adbTvClientKey, connectResult, deviceClient?.Device.State);
                return null;
            }

            await deviceClient.AdbClient.ExecuteRemoteCommandAsync("true", deviceClient.Device, cancellationToken);
            _clients[adbTvClientKey] = new DeviceClientHolder(deviceClient, deviceSemaphore ?? new SemaphoreSlim(1, 1));
            return deviceClient;
        }
        catch (Exception e)
        {
            _logger.FailedToCreateClient(e, adbTvClientKey);
            return null;
        }
    }

    private static async ValueTask<string?> ConnectResultAsync(
        AdbTvClientKey adbTvClientKey,
        AdbClient adbClient,
        ILogger<AdbTvClientFactory> logger,
        long startTime,
        CancellationToken cancellationToken)
    {
        string? connectResult;
        do
        {
            connectResult = await RunWithRetryWithReturnAsync(() =>
                    adbClient.ConnectAsync(adbTvClientKey.IpAddress, adbTvClientKey.Port, cancellationToken),
                logger,
                true,
                cancellationToken);
            if (connectResult?.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase) is not true)
            {
                await Task.Delay(100, cancellationToken);
            }
        } while (connectResult?.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase) is not true &&
                 Stopwatch.GetElapsedTime(startTime) < HealthyDeviceTimeout && !cancellationToken.IsCancellationRequested);

        return connectResult;
    }

    private async ValueTask<DeviceClient?> GetHealthyClientAsync(
        AdbTvClientKey adbTvClientKey,
        DeviceClientHolder deviceClientHolder,
        CancellationToken cancellationToken)
    {
        var connectResult = await RunWithRetryWithReturnAsync(() =>
            deviceClientHolder.DeviceClient.AdbClient.ConnectAsync(adbTvClientKey.IpAddress, adbTvClientKey.Port, cancellationToken),
        _logger,
        true,
        cancellationToken);

        if (connectResult?.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase) is true &&
            await RunWithRetryAsync(() =>
                    deviceClientHolder.DeviceClient.AdbClient.ExecuteRemoteCommandAsync("true", deviceClientHolder.DeviceClient.Device, cancellationToken),
                _logger,
                true,
                cancellationToken))
        {
            return deviceClientHolder.DeviceClient;
        }

        return await CreateDeviceClientAsync(adbTvClientKey, deviceClientHolder.Semaphore, cancellationToken);
    }

    private static async ValueTask<T?> RunWithRetryWithReturnAsync<T>(Func<Task<T>> func,
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
                return await RunWithRetryWithReturnAsync(func, logger, false, cancellationToken);
            }

            throw;
        }
    }

    private static async ValueTask<bool> RunWithRetryAsync(Func<Task> func,
        ILogger<AdbTvClientFactory> logger,
        bool allowRetry,
        CancellationToken cancellationToken)
    {
        try
        {
            await func();
            return true;
        }
        catch (Exception e)
        {
            if (allowRetry)
            {
                logger.ActionFailedWillRetry(e);
                await Task.SafeDelay(500, cancellationToken);
                return await RunWithRetryAsync(func, logger, false, cancellationToken);
            }

            logger.ActionFailedWillNotRetry(e);
            return false;
        }
    }

    private sealed record DeviceClientHolder(DeviceClient DeviceClient, SemaphoreSlim Semaphore);

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