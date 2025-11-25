using System.Globalization;

using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;

using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.AdbTv.Logging;
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
            _logger.NoConfigurationsFound(wsId);
            return null;
        }

        var localIdentifier = identifier?.AsMemory().GetBaseIdentifier();

        var entity = identifierType switch
        {
            IdentifierType.DeviceId => localIdentifier is { Span.IsEmpty: false }
                ? configuration.Entities.FirstOrDefault(x => x.DeviceId?.AsSpan().Equals(localIdentifier.Value.Span, StringComparison.OrdinalIgnoreCase) is true)
                : configuration.Entities[0],
            IdentifierType.EntityId => localIdentifier is { Span.IsEmpty: false }
                ? configuration.Entities.FirstOrDefault(x => x.EntityId.AsSpan().Equals(localIdentifier.Value.Span, StringComparison.OrdinalIgnoreCase))
                : null,
            _ => throw new ArgumentOutOfRangeException(nameof(identifierType), identifierType, null)
        };

        if (entity is not null)
            return new AdbTvClientKey(entity.Host, entity.MacAddress, entity.Port);

        _logger.NoConfigurationFoundForIdentifier(wsId, identifier, identifierType);
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
            _logger.NoConfigurationsFound(wsId);
            return null;
        }

        if (!string.IsNullOrEmpty(deviceId))
        {
            var localDeviceId = deviceId.AsMemory().GetBaseIdentifier();
            var entity = configuration.Entities.FirstOrDefault(x => x.DeviceId?.AsSpan().Equals(localDeviceId.Span, StringComparison.OrdinalIgnoreCase) == true);
            if (entity is not null)
                return [new AdbTvClientKey(entity.Host, entity.MacAddress, entity.Port)];

            _logger.NoConfigurationFoundForDeviceId(wsId, localDeviceId);
            return null;
        }

        return configuration.Entities
            .Select(static entity => new AdbTvClientKey(entity.Host, entity.MacAddress, entity.Port))
            .ToArray();
    }

    internal enum IdentifierType : sbyte
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
            _logger.NoConfigurationsFound(wsId);
            return null;
        }

        if (!string.IsNullOrEmpty(deviceId))
        {
            var entity = configuration.Entities.FirstOrDefault(x => string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            if (entity is not null)
                return [entity];

            _logger.NoConfigurationFoundForDeviceIdString(wsId, deviceId);
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
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
            var adbTvClientKey = await TryGetAdbTvClientKeyAsync(wsId, identifierType, identifier, linkedCancellationTokenSource.Token);
            if (adbTvClientKey is null)
                return null;

            var deviceClient = await _adbTvClientFactory.TryGetOrCreateClientAsync(adbTvClientKey.Value, linkedCancellationTokenSource.Token);
            if (deviceClient is null)
                return null;

            if (deviceClient.Device.State == AdvancedSharpAdbClient.Models.DeviceState.Online)
                return new AdbTvClientHolder(deviceClient, adbTvClientKey.Value);

            await _adbTvClientFactory.TryRemoveClientAsync(adbTvClientKey.Value, linkedCancellationTokenSource.Token);
            deviceClient = await _adbTvClientFactory.TryGetOrCreateClientAsync(adbTvClientKey.Value, linkedCancellationTokenSource.Token);

            return deviceClient is null ? null : new AdbTvClientHolder(deviceClient, adbTvClientKey.Value);
        }
        catch (Exception e)
        {
            _logger.FailedToGetAdbTvClient(e, wsId, identifier, identifierType);

            return null;
        }
    }

    private async Task<bool> CheckClientApprovedAsync(string wsId,
        string entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            var adbTvClientKey = await TryGetAdbTvClientKeyAsync(wsId, IdentifierType.EntityId, entityId, cancellationToken);
            if (adbTvClientKey is null)
                return false;

            var adbClient = new AdbClient();
            string connectResult;
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(7));
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
        catch (Exception e)
        {
            _logger.FailedToCheckClientApproved(e, wsId, entityId);
            return false;
        }
    }

    private async ValueTask<bool> TryDisconnectAdbClientsAsync(
        string wsId,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        if (await TryGetAdbTvClientKeysAsync(wsId, deviceId, cancellationToken) is not { Length: > 0 } adbTvClientKeys)
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

    private sealed record AdbTvClientHolder(DeviceClient Client, in AdbTvClientKey ClientKey);
}