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
    private readonly ConcurrentDictionary<AdbTvClientKey, DeviceClient> _clients = new();
    private readonly ConcurrentDictionary<AdbTvClientKey, SemaphoreSlim> _clientSemaphores = new();

    private static readonly TimeSpan MaxWaitGetClientOperations = TimeSpan.FromSeconds(4.5);

    public async ValueTask<DeviceClient?> TryGetOrCreateClientAsync(AdbTvClientKey adbTvClientKey, CancellationToken cancellationToken)
    {
        var clientSemaphore = _clientSemaphores.GetOrAdd(adbTvClientKey, static _ => new SemaphoreSlim(1, 1));
        if (!_clients.TryGetValue(adbTvClientKey, out var deviceClient))
        {
            if (await clientSemaphore.WaitAsync(MaxWaitGetClientOperations, cancellationToken))
            {
                try
                {
                    // a new client was just added by another thread, assume healthy since it was just added.
                    if (_clients.TryGetValue(adbTvClientKey, out deviceClient))
                        return deviceClient;

                    return await CreateDeviceClientAsync(adbTvClientKey, cancellationToken);
                }
                finally
                {
                    clientSemaphore.Release();
                }
            }

            _logger.TimeoutWaitingForSemaphore(adbTvClientKey);
            return _clients!.GetValueOrDefault(adbTvClientKey, null);
        }

        if (!await clientSemaphore.WaitAsync(MaxWaitGetClientOperations, cancellationToken))
        {
            _logger.TimeoutWaitingForSemaphore(adbTvClientKey);
            return _clients!.GetValueOrDefault(adbTvClientKey, null);
        }

        try
        {
            return await GetHealthyClientAsync(adbTvClientKey, deviceClient, cancellationToken);
        }
        finally
        {
            clientSemaphore.Release();
        }
    }

    private async ValueTask<DeviceClient?> CreateDeviceClientAsync(
        AdbTvClientKey adbTvClientKey,
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
            while (Stopwatch.GetElapsedTime(startTime) < MaxWaitGetClientOperations && !cancellationToken.IsCancellationRequested)
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
            _clients[adbTvClientKey] = deviceClient;
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
            if (!IsAdbConnectedResult(connectResult))
            {
                await Task.Delay(100, cancellationToken);
            }
        } while (!IsAdbConnectedResult(connectResult) &&
                 Stopwatch.GetElapsedTime(startTime) < MaxWaitGetClientOperations && !cancellationToken.IsCancellationRequested);

        return connectResult;
    }

    private async ValueTask<DeviceClient?> GetHealthyClientAsync(
        AdbTvClientKey adbTvClientKey,
        DeviceClient deviceClient,
        CancellationToken cancellationToken)
    {
        var connectResult = await RunWithRetryWithReturnAsync(() =>
                deviceClient.AdbClient.ConnectAsync(adbTvClientKey.IpAddress, adbTvClientKey.Port, cancellationToken),
            _logger,
            true,
            cancellationToken);

        if (IsAdbConnectedResult(connectResult) &&
            await RunWithRetryAsync(() =>
                    deviceClient.AdbClient.ExecuteRemoteCommandAsync("true", deviceClient.Device, cancellationToken),
                _logger,
                true,
                cancellationToken))
        {
            return deviceClient;
        }

        return await CreateDeviceClientAsync(adbTvClientKey, cancellationToken);
    }

    private static bool IsAdbConnectedResult(string? connectResult) =>
        connectResult?.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase) is true
        || connectResult?.StartsWith("connected to ", StringComparison.InvariantCultureIgnoreCase) is true;

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
                await deviceClient.AdbClient.DisconnectAsync(adbTvClientKey.IpAddress, adbTvClientKey.Port, cancellationToken);
                _clientSemaphores.TryRemove(adbTvClientKey, out _);
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