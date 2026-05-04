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
    private static readonly ConcurrentDictionary<AdbTvClientKey, AdbConnection> Clients = new();
    private static readonly ConcurrentDictionary<AdbTvClientKey, SemaphoreSlim> ClientSemaphores = new();

    private static readonly TimeSpan MaxWaitGetClientOperations = TimeSpan.FromSeconds(4.5);
    // We will only have one key for all clients
    private static readonly SemaphoreSlim AuthKeyLock = new(1, 1);
    private static AdbAuthKey? _cachedAuthKey;

    public async ValueTask<AdbConnection?> TryGetOrCreateAdbConnectionAsync(AdbTvClientKey adbTvClientKey, CancellationToken cancellationToken)
    {
        var clientSemaphore = ClientSemaphores.GetOrAdd(adbTvClientKey, static _ => new SemaphoreSlim(1, 1));
        if (!Clients.TryGetValue(adbTvClientKey, out var connection))
        {
            if (await clientSemaphore.WaitAsync(MaxWaitGetClientOperations, cancellationToken))
            {
                try
                {
                    if (Clients.TryGetValue(adbTvClientKey, out connection))
                        return connection;

                    return await CreateConnectionAsync(adbTvClientKey, cancellationToken);
                }
                finally
                {
                    clientSemaphore.Release();
                }
            }

            _logger.TimeoutWaitingForSemaphore(adbTvClientKey);
            return Clients!.GetValueOrDefault(adbTvClientKey, null);
        }

        if (!await clientSemaphore.WaitAsync(MaxWaitGetClientOperations, cancellationToken))
        {
            _logger.TimeoutWaitingForSemaphore(adbTvClientKey);
            return Clients!.GetValueOrDefault(adbTvClientKey, null);
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
                        [await GetOrCreateAuthKey(cancellationToken)],
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

            if (!await RunWithRetryAsync(() => connection.ExecuteAsync("true", cancellationToken),
                    _logger,
                    true,
                    cancellationToken))
            {
                await connection.DisposeAsync();
                return null;
            }

            Clients[adbTvClientKey] = connection;
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

        Clients.TryRemove(adbTvClientKey, out _);
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
        if (Clients.TryRemove(adbTvClientKey, out var connection))
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

    public static async ValueTask RemoveAllClients()
    {
        foreach (var (key, connection) in Clients)
        {
            if (!Clients.TryRemove(key, out _))
                continue;

            try
            {
                await connection.DisposeAsync();
            }
            catch
            {
                // Intentionally ignore
            }
        }
    }

    internal static async ValueTask<AdbAuthKey> GetOrCreateAuthKey(CancellationToken cancellationToken)
    {
        if (!await AuthKeyLock.WaitAsync(MaxWaitGetClientOperations, cancellationToken))
        {
            throw new TimeoutException("Timed out waiting to acquire auth key lock");
        }

        try
        {
            if (_cachedAuthKey is { } existing)
                return existing;

            var directory = GetAdbKeyDirectory();
            Directory.CreateDirectory(directory);
            var privateKeyPath = Path.Combine(directory, "adbkey");

            AdbAuthKey key;
            if (File.Exists(privateKeyPath))
            {
                key = AdbAuthKey.LoadFromPem(await File.ReadAllTextAsync(privateKeyPath, cancellationToken));
            }
            else
            {
                key = AdbAuthKey.Generate();

                var fileStreamOptions = new FileStreamOptions
                {
                    Mode = FileMode.Create,
                    Access = FileAccess.Write,
                    Share = FileShare.None
                };

                if (!OperatingSystem.IsWindows())
                    fileStreamOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

                await using var adbKeyStream = new FileStream(privateKeyPath, fileStreamOptions);
                await using var streamWriter = new StreamWriter(adbKeyStream);
                await streamWriter.WriteAsync(key.ExportPrivateKeyPem().AsMemory(), cancellationToken);
            }

            _cachedAuthKey = key;
            return key;
        }
        finally
        {
            AuthKeyLock.Release();
        }
    }

    internal static async ValueTask ReplacePrivateKeyAsync(byte[] pemBytes, CancellationToken cancellationToken)
    {
        if (!await AuthKeyLock.WaitAsync(MaxWaitGetClientOperations, cancellationToken))
            throw new TimeoutException("Timed out waiting to acquire auth key lock");

        try
        {
            _cachedAuthKey?.Dispose();
            _cachedAuthKey = null;

            var directory = GetAdbKeyDirectory();
            Directory.CreateDirectory(directory);
            var privateKeyPath = Path.Combine(directory, "adbkey");

            var fileStreamOptions = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None
            };

            if (!OperatingSystem.IsWindows())
                fileStreamOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

            await using var stream = new FileStream(privateKeyPath, fileStreamOptions);
            await stream.WriteAsync(pemBytes, cancellationToken);
        }
        finally
        {
            AuthKeyLock.Release();
        }

        await RemoveAllClients();
    }

    internal static string GetAdbKeyDirectory()
    {
        var vendorKeys = Environment.GetEnvironmentVariable("ADB_VENDOR_KEYS");
        if (!string.IsNullOrEmpty(vendorKeys))
        {
            var separatorIndex = vendorKeys.IndexOf(Path.PathSeparator, StringComparison.Ordinal);
            var firstVendorKey = separatorIndex >= 0 ? vendorKeys[..separatorIndex] : vendorKeys;
            // ADB_VENDOR_KEYS entries can be either a directory containing keys or a path to a key file.
            return Directory.Exists(firstVendorKey) ? firstVendorKey : Path.GetDirectoryName(firstVendorKey)!;
        }

        var sdkHome = Environment.GetEnvironmentVariable("ANDROID_SDK_HOME");
        return Path.Combine(!string.IsNullOrEmpty(sdkHome) ? sdkHome : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android");
    }
}
