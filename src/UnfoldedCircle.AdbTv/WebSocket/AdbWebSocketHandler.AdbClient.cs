using System.Globalization;

using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;

using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.Server.Extensions;

namespace UnfoldedCircle.AdbTv.WebSocket;

internal sealed partial class AdbWebSocketHandler
{
    private async Task<AdbTvClientKey?> TryGetAdbTvClientKeyAsync(
        string wsId,
        IdentifierType identifierType,
        string? identifier,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
        if (configuration.Entities.Count == 0)
        {
            _logger.LogInformation("[{WSId}] WS: No configurations found", wsId);
            return null;
        }

        var localIdentifier = identifier.GetNullableBaseIdentifier();

        var entity = identifierType switch
        {
            IdentifierType.DeviceId => !string.IsNullOrWhiteSpace(localIdentifier)
                ? configuration.Entities.Find(x => string.Equals(x.DeviceId, localIdentifier, StringComparison.OrdinalIgnoreCase))
                : configuration.Entities[0],
            IdentifierType.EntityId => !string.IsNullOrWhiteSpace(localIdentifier)
                ? configuration.Entities.Find(x => string.Equals(x.EntityId, localIdentifier, StringComparison.OrdinalIgnoreCase))
            : null,
            _ => throw new ArgumentOutOfRangeException(nameof(identifierType), identifierType, null)
        };

        if (entity is not null)
            return new AdbTvClientKey(entity.Host, entity.MacAddress, entity.Port);

        _logger.LogInformation("[{WSId}] WS: No configuration found for identifier '{Identifier}' with type {Type}",
            wsId, identifier, identifierType.ToString());
        return null;
    }

    private async Task<AdbTvClientKey[]?> TryGetAdbTvClientKeysAsync(
        string wsId,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
        if (configuration.Entities.Count == 0)
        {
            _logger.LogInformation("[{WSId}] WS: No configurations found", wsId);
            return null;
        }

        var localDeviceId = deviceId.GetNullableBaseIdentifier();
        if (!string.IsNullOrEmpty(localDeviceId))
        {
            var entity = configuration.Entities.Find(x => string.Equals(x.DeviceId, localDeviceId, StringComparison.OrdinalIgnoreCase));
            if (entity is not null)
                return [new AdbTvClientKey(entity.Host, entity.MacAddress, entity.Port)];

            _logger.LogInformation("[{WSId}] WS: No configuration found for device ID '{DeviceId}'", wsId, localDeviceId);
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

    private async Task<List<AdbConfigurationItem>?> GetEntitiesAsync(
        string wsId,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
        if (configuration.Entities.Count == 0)
        {
            _logger.LogInformation("[{WSId}] WS: No configurations found", wsId);
            return null;
        }

        if (!string.IsNullOrEmpty(deviceId))
        {
            var entity = configuration.Entities.Find(x => string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            if (entity is not null)
                return [entity];

            _logger.LogInformation("[{WSId}] WS: No configuration found for device ID '{DeviceId}'", wsId, deviceId);
            return null;
        }

        return configuration.Entities;
    }

    private async Task<AdbTvClientHolder?> TryGetAdbTvClientHolderAsync(
        string wsId,
        string? identifier,
        IdentifierType identifierType,
        CancellationToken cancellationToken)
    {
        var adbTvClientKey = await TryGetAdbTvClientKeyAsync(wsId, identifierType, identifier, cancellationToken);
        if (adbTvClientKey is null)
            return null;

        var deviceClient = await _adbTvClientFactory.TryGetOrCreateClientAsync(adbTvClientKey.Value, cancellationToken);
        if (deviceClient is null)
            return null;

        if (deviceClient.Device.State == AdvancedSharpAdbClient.Models.DeviceState.Online)
            return new AdbTvClientHolder(deviceClient, adbTvClientKey.Value);

        await _adbTvClientFactory.TryRemoveClientAsync(adbTvClientKey.Value, cancellationToken);
        deviceClient = await _adbTvClientFactory.TryGetOrCreateClientAsync(adbTvClientKey.Value, cancellationToken);

        return deviceClient is null ? null : new AdbTvClientHolder(deviceClient, adbTvClientKey.Value);
    }

    private async Task<bool> CheckClientApprovedAsync(string wsId,
        string entityId,
        CancellationToken cancellationToken)
    {
        var adbTvClientKey = await TryGetAdbTvClientKeyAsync(wsId, IdentifierType.EntityId, entityId, cancellationToken);
        if (adbTvClientKey is null)
            return false;

        var adbClient = new AdbClient();
        string connectResult;
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(9));
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
            cancellationTokenSource.Token);
        do
        {
            connectResult = await adbClient.ConnectAsync(adbTvClientKey.Value.IpAddress, adbTvClientKey.Value.Port, linkedCancellationTokenSource.Token);
        } while (!connectResult.StartsWith("already connected to ", StringComparison.InvariantCultureIgnoreCase));

        // ReSharper disable once PossiblyMistakenUseOfCancellationToken
        var deviceData = (await adbClient.GetDevicesAsync(cancellationToken)).FirstOrDefault(x =>
            x.Serial.Equals($"{adbTvClientKey.Value.IpAddress}:{adbTvClientKey.Value.Port.ToString(NumberFormatInfo.InvariantInfo)}", StringComparison.InvariantCulture));
        return deviceData is { State: AdvancedSharpAdbClient.Models.DeviceState.Online };
    }

    private async ValueTask<bool> TryDisconnectAdbClientsAsync(
        string wsId,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        var adbTvClientKeys = await TryGetAdbTvClientKeysAsync(wsId, deviceId, cancellationToken);
        if (adbTvClientKeys is not { Length: > 0 })
            return false;

        await TryDisconnectAdbClientsAsync(adbTvClientKeys, cancellationToken);

        return true;
    }

    private async ValueTask TryDisconnectAdbClientsAsync(
        IEnumerable<AdbTvClientKey> adbTvClientKeys,
        CancellationToken cancellationToken) =>
        await Parallel.ForEachAsync(adbTvClientKeys, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
            async (adbTvClientKey, localCancellationToken) =>
            {
                await _adbTvClientFactory.TryRemoveClientAsync(adbTvClientKey, localCancellationToken);
            });

    private async Task<Models.Shared.DeviceState> GetDeviceStateAsync(AdbTvClientHolder? adbTvClientHolder, CancellationToken cancellationToken)
    {
        if (adbTvClientHolder is null)
            return Models.Shared.DeviceState.Disconnected;
        
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(9));
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, cancellationTokenSource.Token);
            while (!linkedCancellationTokenSource.IsCancellationRequested)
            {
                if (adbTvClientHolder.Client.Device.State == AdvancedSharpAdbClient.Models.DeviceState.Online)
                    return Models.Shared.DeviceState.Connected;

                await Task.Delay(TimeSpan.FromMilliseconds(100), linkedCancellationTokenSource.Token);
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