using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Exceptions;
using AdvancedSharpAdbClient.Models;

namespace UnfoldedCircle.AdbTv.AdbTv;

public class AdbTvClientFactory(ILogger<AdbTvClientFactory> logger)
{
    private readonly ILogger<AdbTvClientFactory> _logger = logger;
    private readonly ConcurrentDictionary<AdbTvClientKey, DeviceClient> _clients = new();
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    public async ValueTask<DeviceClient?> TryGetOrCreateClientAsync(AdbTvClientKey adbTvClientKey, CancellationToken cancellationToken)
    {
        try
        {
            if (_clients.TryGetValue(adbTvClientKey, out var client))
            {
                var connectResult = await client.AdbClient.ConnectAsync(adbTvClientKey.IpAddress, adbTvClientKey.Port, cancellationToken);
                if (connectResult.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase) && client.Device.State == DeviceState.Online)
                {
                    try
                    {
                        await client.AdbClient.ExecuteRemoteCommandAsync("true", client.Device, cancellationToken);
                        return client;
                    }
                    catch (AdbException e)
                    {
                        _logger.LogInformation(e, "Client {ClientKey} failed to execute health check command.", adbTvClientKey);
                    }
                }

                _logger.LogDebug("Client {ClientKey} is not connected or not online. Connection result was '{ConnectionResult}', device state was {deviceState}. Removing it.",
                    adbTvClientKey, connectResult, client.Device.State.ToString());
            }

            await _semaphoreSlim.WaitAsync(cancellationToken);
            try
            {
                var startTime = Stopwatch.GetTimestamp();
                var adbClient = new AdbClient();
                string connectResult;
                do
                {
                    connectResult = await adbClient.ConnectAsync(adbTvClientKey.IpAddress, adbTvClientKey.Port, cancellationToken);
                    if (!connectResult.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase))
                        await Task.Delay(100, cancellationToken);
                } while (!connectResult.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase)
                         && (Stopwatch.GetElapsedTime(startTime) < TimeSpan.FromSeconds(5)));

                var deviceData = (await adbClient.GetDevicesAsync(cancellationToken)).FirstOrDefault(x =>
                    x.Serial.Equals($"{adbTvClientKey.IpAddress}:{adbTvClientKey.Port.ToString(NumberFormatInfo.InvariantInfo)}", StringComparison.InvariantCulture));
                var deviceClient = deviceData.CreateDeviceClient();
                if (deviceClient.Device.State != DeviceState.Online)
                {
                    _logger.LogWarning("Device {ClientKey} is not online. Connection result was '{ConnectionResult}', device state was {deviceState}.",
                        adbTvClientKey, connectResult, deviceClient.Device.State.ToString());
                    return null;
                }
                _clients[adbTvClientKey] = deviceClient;
                _logger.LogDebug("Created new client {ClientKey}. Device state is {state}.", adbTvClientKey, deviceClient.Device.State.ToString());
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
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get or create client {ClientKey}", adbTvClientKey);
            return null;
        }
    }

    public async ValueTask TryRemoveClientAsync(AdbTvClientKey adbTvClientKey, CancellationToken cancellationToken)
    {
        if (_clients.TryRemove(adbTvClientKey, out var deviceClient))
        {
            try
            {
                await deviceClient.AdbClient.DisconnectAsync(adbTvClientKey.IpAddress, adbTvClientKey.Port, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to remove client {ClientKey}", adbTvClientKey);
                throw;
            }
        }
    }

    public void RemoveAllClients() => _clients.Clear();
}