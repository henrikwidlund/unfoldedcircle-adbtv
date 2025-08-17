using System.Diagnostics;
using System.Globalization;

using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;

using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.Configuration;

namespace UnfoldedCircle.AdbTv.WebSocket;

internal sealed partial class AdbWebSocketHandler
{
    private async Task<AdbTvClientKey?> TryGetAdbTvClientKey(
        string wsId,
        IdentifierType identifierType,
        string? identifier,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetConfiguration(cancellationToken);
        if (configuration.Entities.Count == 0)
        {
            _logger.LogInformation("[{WSId}] WS: No configurations found", wsId);
            return null;
        }

        var entity = identifierType switch
        {
            IdentifierType.DeviceId => !string.IsNullOrWhiteSpace(identifier)
                ? configuration.Entities.Find(x => string.Equals(x.DeviceId, identifier, StringComparison.Ordinal))
                : configuration.Entities[0],
            IdentifierType.EntityId => !string.IsNullOrWhiteSpace(identifier)
                ? configuration.Entities.Find(x => string.Equals(x.EntityId, identifier, StringComparison.Ordinal))
            : null,
            _ => throw new ArgumentOutOfRangeException(nameof(identifierType), identifierType, null)
        };

        if (entity is not null)
            return new AdbTvClientKey(entity.Host, entity.MacAddress, entity.Port);

        _logger.LogInformation("[{WSId}] WS: No configuration found for identifier '{Identifier}' with type {Type}",
            wsId, identifier, identifierType.ToString());
        return null;
    }

    private async Task<AdbTvClientKey[]?> TryGetAdbTvClientKeys(
        string wsId,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetConfiguration(cancellationToken);
        if (configuration.Entities.Count == 0)
        {
            _logger.LogInformation("[{WSId}] WS: No configurations found", wsId);
            return null;
        }

        if (!string.IsNullOrEmpty(deviceId))
        {
            var entity = configuration.Entities.Find(x => string.Equals(x.DeviceId, deviceId, StringComparison.Ordinal));
            if (entity is not null)
                return [new AdbTvClientKey(entity.Host, entity.MacAddress, entity.Port)];

            _logger.LogInformation("[{WSId}] WS: No configuration found for device ID '{DeviceId}'", wsId, deviceId);
            return null;
        }

        return configuration.Entities
            .Select(static entity => new AdbTvClientKey(entity.Host, entity.MacAddress, entity.Port))
            .ToArray();
    }

    private enum IdentifierType
    {
        DeviceId,
        EntityId
    }

    private async Task<List<AdbConfigurationItem>?> GetEntities(
        string wsId,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetConfiguration(cancellationToken);
        if (configuration.Entities.Count == 0)
        {
            _logger.LogInformation("[{WSId}] WS: No configurations found", wsId);
            return null;
        }

        if (!string.IsNullOrEmpty(deviceId))
        {
            var entity = configuration.Entities.Find(x => string.Equals(x.DeviceId, deviceId, StringComparison.Ordinal));
            if (entity is not null)
                return [entity];

            _logger.LogInformation("[{WSId}] WS: No configuration found for device ID '{DeviceId}'", wsId, deviceId);
            return null;
        }

        return configuration.Entities;
    }

    private async Task<AdbTvClientHolder?> TryGetAdbTvClientHolder(
        string wsId,
        string? identifier,
        IdentifierType identifierType,
        CancellationToken cancellationToken)
    {
        var adbTvClientKey = await TryGetAdbTvClientKey(wsId, identifierType, identifier, cancellationToken);
        if (adbTvClientKey is null)
            return null;

        var deviceClient = await _adbTvClientFactory.TryGetOrCreateClient(adbTvClientKey.Value, cancellationToken);
        if (deviceClient is null)
            return null;

        if (deviceClient.Device.State == AdvancedSharpAdbClient.Models.DeviceState.Online)
            return new AdbTvClientHolder(deviceClient, adbTvClientKey.Value);

        _adbTvClientFactory.TryRemoveClient(adbTvClientKey.Value);
        deviceClient = await _adbTvClientFactory.TryGetOrCreateClient(adbTvClientKey.Value, cancellationToken);

        return deviceClient is null ? null : new AdbTvClientHolder(deviceClient, adbTvClientKey.Value);
    }

    private async Task<bool> CheckClientApproved(string wsId,
        string entityId,
        CancellationToken cancellationToken)
    {
        var adbTvClientKey = await TryGetAdbTvClientKey(wsId, IdentifierType.EntityId, entityId, cancellationToken);
        if (adbTvClientKey is null)
            return false;

        var startTimeStamp = Stopwatch.GetTimestamp();
        var adbClient = new AdbClient();
        string connectResult;
        do
        {
            connectResult = await adbClient.ConnectAsync(adbTvClientKey.Value.IpAddress, adbTvClientKey.Value.Port, cancellationToken);
        } while (!connectResult.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase)
                 && Stopwatch.GetElapsedTime(startTimeStamp).TotalSeconds < 10);

        var deviceData = (await adbClient.GetDevicesAsync(cancellationToken)).FirstOrDefault(x =>
            x.Serial.Equals($"{adbTvClientKey.Value.IpAddress}:{adbTvClientKey.Value.Port.ToString(NumberFormatInfo.InvariantInfo)}", StringComparison.InvariantCulture));
        return deviceData is { State: AdvancedSharpAdbClient.Models.DeviceState.Online };
    }

    private async ValueTask<bool> TryDisconnectAdbClients(
        string wsId,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        var adbTvClientKeys = await TryGetAdbTvClientKeys(wsId, deviceId, cancellationToken);
        if (adbTvClientKeys is not { Length: > 0 })
            return false;

        foreach (var adbTvClientKey in adbTvClientKeys)
            _adbTvClientFactory.TryRemoveClient(adbTvClientKey);

        return true;
    }

    private Models.Shared.DeviceState GetDeviceState(AdbTvClientHolder? adbTvClientHolder)
    {
        if (adbTvClientHolder is null)
            return Models.Shared.DeviceState.Disconnected;
        
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(9));
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                if (adbTvClientHolder.Client.Device.State == AdvancedSharpAdbClient.Models.DeviceState.Online)
                    return Models.Shared.DeviceState.Connected;
            }

            return Models.Shared.DeviceState.Disconnected;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get device state");
            return Models.Shared.DeviceState.Error;
        }
    }
    
    private sealed record AdbTvClientHolder(DeviceClient Client, in AdbTvClientKey ClientKey);
}