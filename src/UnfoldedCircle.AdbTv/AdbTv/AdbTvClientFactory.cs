using System.Collections.Concurrent;
using System.Diagnostics;

using Theodicean.SharpAdb;
using Theodicean.SharpAdb.Auth;
using Theodicean.SharpAdb.Services;

using UnfoldedCircle.AdbTv.Cancellation;
using UnfoldedCircle.AdbTv.Logging;

namespace UnfoldedCircle.AdbTv.AdbTv;

public class AdbTvClientFactory(ILogger<AdbTvClientFactory> logger)
{
    private readonly ILogger<AdbTvClientFactory> _logger = logger;
    private readonly ConcurrentDictionary<AdbTvClientKey, AdbConnection> _clients = new();
    private readonly ConcurrentDictionary<AdbTvClientKey, SemaphoreSlim> _clientSemaphores = new();

    private static readonly TimeSpan MaxWaitGetClientOperations = TimeSpan.FromSeconds(4.5);
    // We will only have one key for all clients
    private static readonly Lock AuthKeyLock = new();
    private static AdbAuthKey? _cachedAuthKey;

    public async ValueTask<AdbConnection?> TryGetOrCreateAdbConnectionAsync(AdbTvClientKey adbTvClientKey, CancellationToken cancellationToken)
    {
        var clientSemaphore = _clientSemaphores.GetOrAdd(adbTvClientKey, static _ => new SemaphoreSlim(1, 1));
        if (!_clients.TryGetValue(adbTvClientKey, out var connection))
        {
            if (await clientSemaphore.WaitAsync(MaxWaitGetClientOperations, cancellationToken))
            {
                try
                {
                    if (_clients.TryGetValue(adbTvClientKey, out connection))
                        return connection;

                    return await CreateConnectionAsync(adbTvClientKey, cancellationToken);
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
            return await GetHealthyConnectionAsync(adbTvClientKey, connection, cancellationToken);
        }
        finally
        {
            clientSemaphore.Release();
        }
    }

    private async ValueTask<AdbConnection?> CreateConnectionAsync(
        AdbTvClientKey adbTvClientKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var startTime = Stopwatch.GetTimestamp();
            AdbConnection? connection = null;
            Exception? lastException = null;
            while (Stopwatch.GetElapsedTime(startTime) < MaxWaitGetClientOperations && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    connection = await AdbConnection.ConnectTcpAsync(
                        adbTvClientKey.IpAddress,
                        adbTvClientKey.Port,
                        [GetOrCreateAuthKey()],
                        options: null,
                        cancellationToken);
                    break;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    lastException = e;
                    await Task.Delay(100, cancellationToken);
                }
            }

            if (connection is null)
            {
                _logger.DeviceNotOnline(adbTvClientKey, lastException?.Message);
                return null;
            }

            await RunWithRetryAsync(() => connection.ExecuteAsync("true", cancellationToken),
                _logger,
                true,
                cancellationToken);

            _clients[adbTvClientKey] = connection;
            return connection;
        }
        catch (Exception e)
        {
            _logger.FailedToCreateClient(e, adbTvClientKey);
            return null;
        }
    }

    private async ValueTask<AdbConnection?> GetHealthyConnectionAsync(
        AdbTvClientKey adbTvClientKey,
        AdbConnection connection,
        CancellationToken cancellationToken)
    {
        if (connection.FaultException is null &&
            await RunWithRetryAsync(() => connection.ExecuteAsync("true", cancellationToken),
                _logger,
                true,
                cancellationToken))
        {
            return connection;
        }

        _clients.TryRemove(adbTvClientKey, out _);
        try
        {
            await connection.DisposeAsync();
        }
        catch
        {
            // Best effort
        }

        return await CreateConnectionAsync(adbTvClientKey, cancellationToken);
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

    public async ValueTask TryRemoveClientAsync(AdbTvClientKey adbTvClientKey)
    {
        if (_clients.TryRemove(adbTvClientKey, out var connection))
        {
            try
            {
                await connection.DisposeAsync();
            }
            catch (Exception e)
            {
                _logger.FailedToRemoveClient(e, adbTvClientKey);
                throw;
            }
        }
    }

    public async ValueTask RemoveAllClients()
    {
        foreach (var (key, connection) in _clients)
        {
            if (_clients.TryRemove(key, out _))
                await connection.DisposeAsync();
        }
    }

    internal static AdbAuthKey GetOrCreateAuthKey()
    {
        lock (AuthKeyLock)
        {
            if (_cachedAuthKey is { } existing)
                return existing;

            var directory = GetAdbKeyDirectory();
            Directory.CreateDirectory(directory);
            var privateKeyPath = Path.Combine(directory, "adbkey");

            AdbAuthKey key;
            if (File.Exists(privateKeyPath))
            {
                key = AdbAuthKey.LoadFromPem(File.ReadAllText(privateKeyPath));
            }
            else
            {
                key = AdbAuthKey.Generate();
                File.WriteAllText(privateKeyPath, key.ExportPrivateKeyPem());
            }

            _cachedAuthKey = key;
            return key;
        }
    }

    internal static void InvalidateAuthKey()
    {
        lock (AuthKeyLock)
        {
            _cachedAuthKey?.Dispose();
            _cachedAuthKey = null;
        }
    }

    private static string GetAdbKeyDirectory()
    {
        var vendorKeys = Environment.GetEnvironmentVariable("ADB_VENDOR_KEYS");
        if (!string.IsNullOrEmpty(vendorKeys))
        {
            var separatorIndex = vendorKeys.IndexOf(Path.PathSeparator, StringComparison.Ordinal);
            var firstVendorKey = separatorIndex >= 0 ? vendorKeys[..separatorIndex] : vendorKeys;
            return Path.GetDirectoryName(firstVendorKey)!;
        }

        var sdkHome = Environment.GetEnvironmentVariable("ANDROID_SDK_HOME");
        return Path.Combine(!string.IsNullOrEmpty(sdkHome) ? sdkHome : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android");
    }
}
