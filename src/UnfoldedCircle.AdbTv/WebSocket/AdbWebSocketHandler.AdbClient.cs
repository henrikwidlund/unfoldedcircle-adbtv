using Theodicean.SharpAdb;
using Theodicean.SharpAdb.Services;

using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.AdbTv.Logging;
using UnfoldedCircle.Server.Extensions;

namespace UnfoldedCircle.AdbTv.WebSocket;

internal sealed partial class AdbWebSocketHandler
{
    private async Task<AdbTvClientKey?> TryGetAdbTvClientKeyAsync(
        string wsId,
        string entityId,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
        if (configuration.Entities.Count == 0)
        {
            _logger.NoConfigurationsFound(wsId);
            return null;
        }

        var localIdentifier = entityId.AsMemory().GetBaseIdentifier();

        var entity = localIdentifier is { Span.IsEmpty: false }
            ? configuration.Entities.FirstOrDefault(x => x.EntityId.AsSpan().Equals(localIdentifier.Span, StringComparison.OrdinalIgnoreCase))
            : null;

        if (entity is not null)
            return new AdbTvClientKey(entity.Host, entity.MacAddress, entity.Port, entity.Manufacturer);

        _logger.NoConfigurationFoundForIdentifier(wsId, entityId);
        return null;
    }

    private async Task<AdbTvClientKey[]?> TryGetAdbTvClientKeysAsync(
        string wsId,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
        if (configuration.Entities.Count == 0)
        {
            _logger.NoConfigurationsFound(wsId);
            return null;
        }

        return configuration.Entities
            .Select(static entity => new AdbTvClientKey(entity.Host, entity.MacAddress, entity.Port, entity.Manufacturer))
            .ToArray();
    }

    private async Task<List<AdbConfigurationItem>?> GetEntitiesAsync(
        string wsId,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
        if (configuration.Entities.Count == 0)
        {
            _logger.NoConfigurationsFound(wsId);
            return null;
        }

        return configuration.Entities;
    }

    private async ValueTask<TimeSpan> GetMaxMessageHandlingWaitTimeSpanAsync(CancellationToken cancellationToken) =>
        await _configurationService.GetConfigurationAsync(cancellationToken) is { MaxMessageHandlingWaitTimeInSeconds: > 0 } configuration
            ? TimeSpan.FromSeconds(configuration.MaxMessageHandlingWaitTimeInSeconds.Value)
            : TimeSpan.FromSeconds(9.5);

    private async Task<AdbTvClientHolder?> TryGetAdbTvClientHolderAsync(
        string wsId,
        string entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(await GetMaxMessageHandlingWaitTimeSpanAsync(cancellationToken));
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
            var adbTvClientKey = await TryGetAdbTvClientKeyAsync(wsId, entityId, linkedCancellationTokenSource.Token);
            if (adbTvClientKey is null)
            {
                _logger.AdbTvClientKeyNotFound(wsId, entityId);
                return null;
            }

            var connection = await _adbTvClientFactory.TryGetOrCreateAdbConnectionAsync(adbTvClientKey.Value, linkedCancellationTokenSource.Token);
            if (connection is null)
            {
                _logger.AdbTvClientHolderNotFound(wsId, entityId);
                return null;
            }
            return new AdbTvClientHolder(connection, adbTvClientKey.Value);
        }
        catch (Exception e)
        {
            _logger.FailedToGetAdbTvClient(e, wsId, entityId);

            return null;
        }
    }

    private async Task<bool> CheckClientApprovedAsync(string wsId,
        string entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            var adbTvClientKey = await TryGetAdbTvClientKeyAsync(wsId, entityId, cancellationToken);
            if (adbTvClientKey is null)
                return false;

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(7));
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                cancellationTokenSource.Token);

            try
            {
                await using var connection = await AdbConnection.ConnectTcpAsync(
                    adbTvClientKey.Value.IpAddress,
                    adbTvClientKey.Value.Port,
                    [await AdbTvClientFactory.GetOrCreateAuthKey(cancellationToken)],
                    options: null,
                    linkedCancellationTokenSource.Token);
                await connection.ExecuteAsync("true", linkedCancellationTokenSource.Token);
                return true;
            }
            catch (AdbAuthenticationException)
            {
                return false;
            }
        }
        catch (Exception e)
        {
            _logger.FailedToCheckClientApproved(e, wsId, entityId);
            return false;
        }
    }

    private async ValueTask<bool> TryDisconnectAdbClientsAsync(
        string wsId,
        CancellationToken cancellationToken)
    {
        if (await TryGetAdbTvClientKeysAsync(wsId, cancellationToken) is not { Length: > 0 } adbTvClientKeys)
            return false;

        await TryDisconnectAdbClientsAsync(adbTvClientKeys, cancellationToken);

        return true;
    }

    private async ValueTask TryDisconnectAdbClientsAsync(
        IEnumerable<AdbTvClientKey> adbTvClientKeys,
        CancellationToken cancellationToken) =>
        await Parallel.ForEachAsync(adbTvClientKeys, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
            async (adbTvClientKey, _) =>
            {
                await _adbTvClientFactory.TryRemoveClientAsync(adbTvClientKey);
            });

    private sealed record AdbTvClientHolder(AdbConnection Connection, in AdbTvClientKey ClientKey);
}
