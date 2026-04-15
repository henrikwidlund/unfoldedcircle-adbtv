using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using System.Net;

using AdvancedSharpAdbClient.DeviceCommands;

using Microsoft.Extensions.Options;

using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.BackgroundServices;
using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.AdbTv.Json;
using UnfoldedCircle.AdbTv.Logging;
using UnfoldedCircle.AdbTv.Response;
using UnfoldedCircle.AdbTv.WoL;
using UnfoldedCircle.Models.Events;
using UnfoldedCircle.Models.Shared;
using UnfoldedCircle.Models.Sync;
using UnfoldedCircle.Server.Configuration;
using UnfoldedCircle.Server.DependencyInjection;
using UnfoldedCircle.Server.Extensions;
using UnfoldedCircle.Server.Response;
using UnfoldedCircle.Server.WebSocket;

namespace UnfoldedCircle.AdbTv.WebSocket;

internal sealed partial class AdbWebSocketHandler(
    IConfigurationService<AdbConfigurationItem> configurationService,
    AdbTvClientFactory adbTvClientFactory,
    IOptions<UnfoldedCircleOptions> options,
    ILogger<AdbWebSocketHandler> logger) : UnfoldedCircleWebSocketHandler<AdbMediaPlayerCommandId, AdbConfigurationItem>(configurationService, options, logger)
{
    private readonly AdbTvClientFactory _adbTvClientFactory = adbTvClientFactory;

    private readonly ConcurrentDictionary<string, List<string>> _entityIdAppsMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _entityIdActiveAppMap = new(StringComparer.OrdinalIgnoreCase);

    protected override async ValueTask<EntityCommandResult> OnRemoteCommandAsync(
        System.Net.WebSockets.WebSocket socket,
        RemoteEntityCommandMsgData payload,
        string command,
        string wsId,
        CancellationTokenWrapper cancellationTokenWrapper,
        CancellationToken commandCancellationToken)
    {
        var baseIdentifier = payload.MsgData.EntityId.AsMemory().GetBaseIdentifier();
        var manufacturer = (await GetEntitiesAsync(wsId, commandCancellationToken))
            ?.FirstOrDefault(x => x.EntityId.Equals(baseIdentifier.Span, StringComparison.OrdinalIgnoreCase))?.Manufacturer ?? Manufacturer.Android;
        (string commandToSend, CommandType commandType) = GetMappedCommand(command, manufacturer);
        var adbTvClientHolder = await TryGetAdbTvClientHolderAsync(wsId, payload.MsgData.EntityId, commandCancellationToken);
        if (adbTvClientHolder is null)
        {
            _logger.CouldNotFindAdbClient(wsId, baseIdentifier);
            return EntityCommandResult.Failure;
        }

        bool isPowerOn = command.Equals(RemoteButtonConstants.On, StringComparison.OrdinalIgnoreCase);
        bool isPowerOff = command.Equals(RemoteButtonConstants.Off, StringComparison.OrdinalIgnoreCase);
        bool isToggle = command.Equals(RemoteButtonConstants.Toggle, StringComparison.OrdinalIgnoreCase);

        return await ExecuteCommandAsync(adbTvClientHolder, commandToSend, commandType, isPowerOn, isPowerOff, isToggle, commandCancellationToken);
    }

    protected override ValueTask<EntityCommandResult> OnClimateHvacModeCommandAsync(System.Net.WebSockets.WebSocket socket,
        ClimateEntityCommandMsgData payload,
        HvacMode hvacMode,
        string wsId,
        CancellationTokenWrapper cancellationTokenWrapper,
        CancellationToken commandCancellationToken)
        => ValueTask.FromResult(EntityCommandResult.Other);

    protected override ValueTask<EntityCommandResult> OnClimatePowerCommandAsync(System.Net.WebSockets.WebSocket socket,
        ClimateEntityCommandMsgData payload,
        bool powerOn,
        string wsId,
        CancellationTokenWrapper cancellationTokenWrapper,
        CancellationToken commandCancellationToken)
        => ValueTask.FromResult(EntityCommandResult.Other);

    protected override ValueTask<EntityCommandResult> OnClimateTargetTemperatureCommandAsync(System.Net.WebSockets.WebSocket socket,
        ClimateEntityCommandMsgData payload,
        float targetTemperature,
        string wsId,
        CancellationTokenWrapper cancellationTokenWrapper,
        CancellationToken commandCancellationToken)
        => ValueTask.FromResult(EntityCommandResult.Other);

    protected override async ValueTask<SelectCommandResult> OnSelectOptionCommandAsync(System.Net.WebSockets.WebSocket socket,
        SelectEntityCommandMsgData payload,
        string option,
        string wsId,
        CancellationTokenWrapper cancellationTokenWrapper,
        CancellationToken commandCancellationToken)
    {
        if (await StartApp(wsId, payload.MsgData.EntityId, option, cancellationTokenWrapper.RequestAborted))
        {
            var alternateLookup = _entityIdActiveAppMap.GetAlternateLookup<ReadOnlySpan<char>>();
            alternateLookup[payload.MsgData.EntityId.AsSpan().GetBaseIdentifier()] = option;
            return new SelectCommandResult(EntityCommandResult.Other, option);
        }

        return new SelectCommandResult(EntityCommandResult.Failure, string.Empty);
    }

    private async ValueTask<bool> StartApp(string wsId, string entityId, string appIdentifier, CancellationToken cancellationToken)
    {
        var adbTvClientHolder = await TryGetAdbTvClientHolderAsync(wsId, entityId, cancellationToken);
        if (adbTvClientHolder is null)
            return false;

        await adbTvClientHolder.Client.StartAppAsync(appIdentifier, cancellationToken);
        return true;
    }

    protected override async ValueTask<SelectCommandResult> OnSelectFirstLastCommandAsync(System.Net.WebSockets.WebSocket socket,
        SelectEntityCommandMsgData payload,
        bool first,
        string wsId,
        CancellationTokenWrapper cancellationTokenWrapper,
        CancellationToken commandCancellationToken)
    {
        if (!await PopulateApps(wsId, payload.MsgData.EntityId, commandCancellationToken))
            return new SelectCommandResult(EntityCommandResult.Failure, string.Empty);

        var alternateLookup = _entityIdAppsMap.GetAlternateLookup<ReadOnlySpan<char>>();
        var baseIdentifier = payload.MsgData.EntityId.AsMemory().GetBaseIdentifier();
        var apps = alternateLookup[baseIdentifier.Span];
        if (apps.Count == 0)
        {
            _logger.SelectFirstLastNoAppsFound(wsId, payload.MsgData.EntityId);
            return new SelectCommandResult(EntityCommandResult.Failure, string.Empty);
        }

        var app = first
            ? apps[0]
            : apps[^1];

        if (await StartApp(wsId, payload.MsgData.EntityId, app, commandCancellationToken))
        {
            var activeEntityAppAlternativeLookup = _entityIdActiveAppMap.GetAlternateLookup<ReadOnlySpan<char>>();
            activeEntityAppAlternativeLookup[baseIdentifier.Span] = app;
            return new SelectCommandResult(EntityCommandResult.Other, app);
        }

        return new SelectCommandResult(EntityCommandResult.Failure, string.Empty);
    }

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _appFetchSemaphores = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _appFetchLock = new();

    private async ValueTask<bool> PopulateApps(string wsId, string entityId, CancellationToken cancellationToken)
    {
        var baseIdentifier = entityId.AsMemory().GetBaseIdentifier();
        var alternateLookup = _entityIdAppsMap.GetAlternateLookup<ReadOnlySpan<char>>();
        if (alternateLookup.ContainsKey(baseIdentifier.Span))
            return true;

        SemaphoreSlim? semaphore;
        lock (_appFetchLock)
        {
            var alternateAppFetchSemaphores = _appFetchSemaphores.GetAlternateLookup<ReadOnlySpan<char>>();
            if (!alternateAppFetchSemaphores.TryGetValue(baseIdentifier.Span, out semaphore) || semaphore == null)
            {
                semaphore = new SemaphoreSlim(1, 1);
                alternateAppFetchSemaphores[baseIdentifier.Span] = semaphore;
            }
        }

        if (await semaphore.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            if (alternateLookup.ContainsKey(baseIdentifier.Span))
                return true;

            try
            {
                var adbClientHolder = await TryGetAdbTvClientHolderAsync(wsId, entityId, cancellationToken);
                if (adbClientHolder is null)
                {
                    _logger.PopulateAppsYieldedNoApps(wsId, entityId);
                    return false;
                }

                var apps = new List<string>();
                await foreach (string appIdentifier in adbClientHolder.Client.AdbClient.ExecuteRemoteEnumerableAsync("pm list packages -3", adbClientHolder.Client.Device, System.Text.Encoding.UTF8, cancellationToken))
                    apps.Add(appIdentifier.Replace("package:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim());

                alternateLookup[baseIdentifier.Span] = apps;
                return true;
            }
            finally
            {
                semaphore.Release();
            }
        }

        _logger.FailedToAcquireSemaphoreForPopulateApps(wsId, entityId);
        return false;
    }

    protected override async ValueTask<SelectCommandResult> OnSelectNextPreviousCommandAsync(System.Net.WebSockets.WebSocket socket,
        SelectEntityCommandMsgData payload,
        bool next,
        bool cycle,
        string wsId,
        CancellationTokenWrapper cancellationTokenWrapper,
        CancellationToken commandCancellationToken)
    {
        if (!await PopulateApps(wsId, payload.MsgData.EntityId, commandCancellationToken))
            return new SelectCommandResult(EntityCommandResult.Failure, string.Empty);

        var baseIdentifier = payload.MsgData.EntityId.AsMemory().GetBaseIdentifier();
        var entityIdAppsMapAlternate = _entityIdAppsMap.GetAlternateLookup<ReadOnlySpan<char>>();
        var entityIdActiveAppMapAlternate = _entityIdActiveAppMap.GetAlternateLookup<ReadOnlySpan<char>>();
        var apps = entityIdAppsMapAlternate[baseIdentifier.Span];
        if (apps.Count == 0)
        {
            _logger.SelectNextPreviousNoAppsFound(wsId, payload.MsgData.EntityId);
            return new SelectCommandResult(EntityCommandResult.Failure, string.Empty);
        }

        if (!entityIdActiveAppMapAlternate.TryGetValue(baseIdentifier.Span, out var activeApp) ||
            !apps.Contains(activeApp))
            activeApp = apps[0];

        int currentIndex = apps.IndexOf(activeApp);
        int nextIndex = next ? currentIndex + 1 : currentIndex - 1;
        if (cycle)
        {
            if (nextIndex >= apps.Count)
                nextIndex = 0;
            else if (nextIndex < 0)
                nextIndex = apps.Count - 1;
        }
        else
        {
            if (nextIndex >= apps.Count || nextIndex < 0)
            {
                _logger.SelectNextPreviousNoAppsOutOfBounds(wsId, payload.MsgData.EntityId, nextIndex, apps.Count);
                return new SelectCommandResult(EntityCommandResult.Failure, activeApp);
            }
        }

        var app = apps[nextIndex];
        if (await StartApp(wsId, payload.MsgData.EntityId, app, commandCancellationToken))
        {
            entityIdActiveAppMapAlternate[baseIdentifier.Span] = app;
            return new SelectCommandResult(EntityCommandResult.Other, app);
        }
        return new SelectCommandResult(EntityCommandResult.Failure, string.Empty);
    }

    protected override async ValueTask<bool> IsEntityReachableAsync(string wsId, string entityId, CancellationToken cancellationToken)
        => await TryGetAdbTvClientHolderAsync(wsId, entityId, cancellationToken) is not null;

    protected override async ValueTask<EntityCommandResult> OnMediaPlayerCommandAsync(System.Net.WebSockets.WebSocket socket,
        MediaPlayerEntityCommandMsgData<AdbMediaPlayerCommandId> payload,
        string wsId,
        CancellationTokenWrapper cancellationTokenWrapper,
        CancellationToken commandCancellationToken)
    {
        var adbTvClientHolder = await TryGetAdbTvClientHolderAsync(wsId, payload.MsgData.EntityId, commandCancellationToken);
        if (adbTvClientHolder is null)
            return EntityCommandResult.Failure;

        (string command, CommandType commandType) = GetMappedCommand(payload.MsgData.CommandId, adbTvClientHolder.ClientKey.Manufacturer, payload.MsgData.Params?.Source);

        bool isPowerOn = payload.MsgData.CommandId == AdbMediaPlayerCommandId.On;
        bool isPowerOff = payload.MsgData.CommandId == AdbMediaPlayerCommandId.Off;
        bool isToggle = payload.MsgData.CommandId == AdbMediaPlayerCommandId.Toggle;

        return await ExecuteCommandAsync(adbTvClientHolder, command, commandType, isPowerOn, isPowerOff, isToggle, commandCancellationToken);
    }

    protected override async ValueTask OnConnectAsync(ConnectEvent payload, string wsId, CancellationToken cancellationToken)
    {
        var adbTvClientKeys = await TryGetAdbTvClientKeysAsync(wsId, cancellationToken);
        if (adbTvClientKeys is { Length: > 0 })
        {
            foreach (var adbTvClientKey in adbTvClientKeys)
                RemoteStates[adbTvClientKey] = RemoteState.Off;
        }
    }

    protected override ValueTask<bool> OnDisconnectAsync(DisconnectEvent payload, string wsId, CancellationToken cancellationToken)
        => TryDisconnectAdbClientsAsync(wsId, cancellationToken);

    protected override ValueTask OnAbortDriverSetupAsync(AbortDriverSetupEvent payload, string wsId, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    protected override ValueTask OnEnterStandbyAsync(EnterStandbyEvent payload, string wsId, CancellationToken cancellationToken)
    {
        _adbTvClientFactory.RemoveAllClients();
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnExitStandbyAsync(ExitStandbyEvent payload, string wsId, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    private static readonly ConcurrentDictionary<string, RemoteState> ReportedEntityIdStates = [];

    protected override async Task HandleEventUpdatesAsync(System.Net.WebSockets.WebSocket socket, string wsId, SubscribedEntitiesHolder subscribedEntitiesHolder, CancellationToken cancellationToken)
    {
        using var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        do
        {
            await Parallel.ForEachAsync(subscribedEntitiesHolder.SubscribedEntities, cancellationToken,
                async (subscribedEntity, token) =>
                {
                    try
                    {
                        var adbTvClientHolder = await TryGetAdbTvClientHolderAsync(wsId, subscribedEntity.Key, token);
                        var currentState = ReportedEntityIdStates.GetValueOrDefault(subscribedEntity.Key, RemoteState.Unknown);
                        if (adbTvClientHolder is null)
                        {
                            await ReportStateUnknown(socket, wsId, currentState, subscribedEntity.Key, subscribedEntity.Value, token);
                            return;
                        }

                        await ReportStateOff(socket, wsId, currentState, subscribedEntity.Key, subscribedEntity.Value, token);
                    }
                    catch (Exception e)
                    {
                        // This is expected from control flow, no need to spam logs
                        if (e is OperationCanceledException)
                            return;
                        _logger.FailureDuringEvent(e, wsId, subscribedEntity.Key);
                    }
                });
        } while (!cancellationToken.IsCancellationRequested && await periodicTimer.WaitForNextTickAsync(cancellationToken));
    }

    private async ValueTask<EntityCommandResult> ExecuteCommandAsync(
        AdbTvClientHolder adbTvClientHolder,
        string command,
        CommandType commandType,
        bool isPowerOn,
        bool isPowerOff,
        bool isToggle,
        CancellationToken cancellationToken)
    {
        switch (commandType)
        {
            case CommandType.KeyEvent:
                if (isPowerOn)
                    await WakeOnLan.SendWakeOnLanAsync(adbTvClientHolder.ClientKey.MacAddress, IPAddress.Parse(adbTvClientHolder.ClientKey.IpAddress));

                await adbTvClientHolder.Client.SendKeyEventAsync(command, cancellationToken);

                var result = isPowerOn ? EntityCommandResult.PowerOn
                    : isPowerOff ? EntityCommandResult.PowerOff
                    : isToggle ? HandleToggleResult(adbTvClientHolder.ClientKey)
                    : EntityCommandResult.Other;

                if (result == EntityCommandResult.PowerOn)
                    RemoteStates[adbTvClientHolder.ClientKey] = RemoteState.On;
                else if (result == EntityCommandResult.PowerOff)
                    RemoteStates[adbTvClientHolder.ClientKey] = RemoteState.Off;

                return result;
            case CommandType.Raw:
                await adbTvClientHolder.Client.AdbClient.ExecuteRemoteCommandAsync(command,
                    adbTvClientHolder.Client.Device,
                    cancellationToken);
                return EntityCommandResult.Other;
            case CommandType.App:
                await adbTvClientHolder.Client.AdbClient.StartAppAsync(adbTvClientHolder.Client.Device,
                    command,
                    cancellationToken);
                return EntityCommandResult.Other;
            case CommandType.NoOp:
                var noOpIsPowerOn = command.Equals(AdbTvRemoteCommands.PowerStateOn, StringComparison.OrdinalIgnoreCase);
                RemoteStates[adbTvClientHolder.ClientKey] = noOpIsPowerOn ? RemoteState.On : RemoteState.Off;
                return noOpIsPowerOn ? EntityCommandResult.PowerOn : EntityCommandResult.PowerOff;
            case CommandType.Unknown:
            default:
                _logger.UnknownCommand(command);
                return EntityCommandResult.Failure;
        }

        static EntityCommandResult HandleToggleResult(in AdbTvClientKey adbTvClientKey)
        {
            if (RemoteStates.TryGetValue(adbTvClientKey, out var remoteState))
            {
                return remoteState switch
                {
                    RemoteState.On => EntityCommandResult.PowerOff,
                    RemoteState.Off or RemoteState.Unknown => EntityCommandResult.PowerOn,
                    _ => EntityCommandResult.Other
                };
            }

            RemoteStates[adbTvClientKey] = RemoteState.On;
            return EntityCommandResult.PowerOn;
        }
    }

    private async Task ReportStateOff(System.Net.WebSockets.WebSocket socket,
        string wsId,
        RemoteState currentState,
        string entityId,
        HashSet<SubscribedEntity> subscribedEntities,
        CancellationToken cancellationToken)
    {
        if (currentState == RemoteState.Unknown)
        {
            await ReportEntityStatesAsync(socket, wsId, subscribedEntities, State.Off, RemoteState.Off, SelectState.On, cancellationToken);
            ReportedEntityIdStates[entityId] = RemoteState.Off;
        }
    }

    private async Task ReportStateUnknown(System.Net.WebSockets.WebSocket socket,
        string wsId,
        RemoteState currentState,
        string entityId,
        HashSet<SubscribedEntity> subscribedEntities,
        CancellationToken cancellationToken)
    {
        if (currentState != RemoteState.Unknown)
        {
            _logger.CouldNotFindAdbClientString(wsId, entityId);
            await ReportEntityStatesAsync(socket, wsId, subscribedEntities, State.Unknown, RemoteState.Unknown, SelectState.Unknown, cancellationToken);
            ReportedEntityIdStates[entityId] = RemoteState.Unknown;
        }
    }

    private async Task ReportEntityStatesAsync(
        System.Net.WebSockets.WebSocket socket,
        string wsId,
        HashSet<SubscribedEntity> subscribedEntities,
        State mediaPlayerState,
        RemoteState remoteState,
        SelectState selectState,
        CancellationToken cancellationToken)
    {
        foreach (var subscribedEntity in subscribedEntities)
        {
            switch (subscribedEntity.EntityType)
            {
                case EntityType.MediaPlayer:
                    await SendMessageAsync(socket,
                        ResponsePayloadHelpers.CreateMediaPlayerStateChangedResponsePayload(
                            new MediaPlayerStateChangedEventMessageDataAttributes { State = mediaPlayerState },
                            subscribedEntity.EntityId),
                        wsId,
                        cancellationToken);
                    break;
                case EntityType.Remote:
                    await SendMessageAsync(socket,
                        ResponsePayloadHelpers.CreateRemoteStateChangedResponsePayload(
                            new RemoteStateChangedEventMessageDataAttributes { State = remoteState },
                            subscribedEntity.EntityId),
                        wsId,
                        cancellationToken);
                    break;
                case EntityType.Select:
                    await SendMessageAsync(socket,
                        ResponsePayloadHelpers.CreateSelectStateChangedResponsePayload(
                            new SelectStateChangedEventMessageDataAttributes { State = selectState },
                            subscribedEntity.EntityId, AdbTvServerConstants.AppListSelectSuffix),
                        wsId,
                        cancellationToken);
                    break;
            }
        }
    }

    protected override ValueTask<DeviceState> OnGetDeviceStateAsync(GetDeviceStateMsg payload, string wsId, CancellationToken cancellationToken)
        => ValueTask.FromResult(DeviceState.Connected);

    protected override async ValueTask<IReadOnlyCollection<AvailableEntity>> OnGetAvailableEntitiesAsync(GetAvailableEntitiesMsg payload, string wsId, CancellationToken cancellationToken)
        => GetAvailableEntities(await GetEntitiesAsync(wsId, cancellationToken)).ToArray();

    protected override async ValueTask OnSubscribeEventsAsync(System.Net.WebSockets.WebSocket socket, SubscribeEventsMsg payload, string wsId, CancellationTokenWrapper cancellationTokenWrapper,
        CancellationToken commandCancellationToken)
    {
        if (payload.MsgData?.EntityIds is not { Length: > 0 })
            return;

        foreach (string msgDataEntityId in payload.MsgData.EntityIds)
        {
            cancellationTokenWrapper.AddSubscribedEntity(msgDataEntityId);
            var entityType = msgDataEntityId.AsSpan().GetEntityTypeFromIdentifier();
            if (entityType is EntityType.MediaPlayer or EntityType.Select)
                await PopulateApps(wsId, msgDataEntityId, commandCancellationToken);

            var baseIdentifier = msgDataEntityId.AsSpan().GetBaseIdentifier();
            var alternateLookup = _entityIdAppsMap.GetAlternateLookup<ReadOnlySpan<char>>();
            if (!alternateLookup.TryGetValue(baseIdentifier, out var apps))
                apps = [];

            if (entityType == EntityType.MediaPlayer)
            {
                string[] sources = [..apps, AdbTvRemoteCommands.InputHdmi1, AdbTvRemoteCommands.InputHdmi2, AdbTvRemoteCommands.InputHdmi3, AdbTvRemoteCommands.InputHdmi4];
                await SendMessageAsync(socket,
                    ResponsePayloadHelpers.CreateMediaPlayerStateChangedResponsePayload(new MediaPlayerStateChangedEventMessageDataAttributes { SourceList = sources },
                        msgDataEntityId), wsId, commandCancellationToken);
            }
            else if (entityType == EntityType.Select)
            {
                await SendMessageAsync(socket,
                    ResponsePayloadHelpers.CreateSelectStateChangedResponsePayload(new SelectStateChangedEventMessageDataAttributes { Options = apps.ToArray() }, msgDataEntityId, AdbTvServerConstants.AppListSelectSuffix),
                    wsId, commandCancellationToken);
            }
        }
    }

    protected override async ValueTask OnUnsubscribeEventsAsync(UnsubscribeEventsMsg payload, string wsId, CancellationTokenWrapper cancellationTokenWrapper)
    {
        var clientKeys = new HashSet<AdbTvClientKey>();
        if (payload.MsgData?.EntityIds is { Length: > 0 })
        {
            foreach (string msgDataEntityId in payload.MsgData.EntityIds)
            {
                cancellationTokenWrapper.RemoveSubscribedEntity(msgDataEntityId);

                if (await TryGetAdbTvClientKeyAsync(wsId, msgDataEntityId, cancellationTokenWrapper.ApplicationStopping) is { } adbClientKey)
                    clientKeys.Add(adbClientKey);
            }
        }
        // If no specific device or entity was specified, dispose all clients for this websocket ID.
        else if (payload.MsgData is { DeviceId: null, EntityIds: null })
            cancellationTokenWrapper.RemoveAllSubscribedEntities();

        await TryDisconnectAdbClientsAsync(clientKeys, cancellationTokenWrapper.ApplicationStopping);
    }

    protected override async ValueTask<EntityStateChanged[]> OnGetEntityStatesAsync(GetEntityStatesMsg payload, string wsId, CancellationToken cancellationToken)
        => await GetEntitiesAsync(wsId, cancellationToken) is { } entities
            ? AdbTvResponsePayloadHelpers.GetEntityStates(entities.Select(static x => x.EntityId)).ToArray()
            : [];

    protected override ValueTask<SetupDriverUserDataResult> OnSetupDriverUserDataConfirmAsync(System.Net.WebSockets.WebSocket socket, SetDriverUserDataMsg payload, string wsId, CancellationToken cancellationToken)
        => ValueTask.FromResult(SetupDriverUserDataResult.Finalized);

    protected override async ValueTask<SetupDriverUserDataResult> HandleEntityReconfigured(System.Net.WebSockets.WebSocket socket,
        SetDriverUserDataMsg payload,
        string wsId,
        AdbConfigurationItem configurationItem,
        CancellationToken cancellationToken)
    {
        var ipAddress = payload.MsgData.InputValues![AdbTvServerConstants.IpAddressKey];
        var port = payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.PortKey, out var portValue)
            ? int.Parse(portValue, NumberFormatInfo.InvariantInfo)
            : 5555;
        var manufacturer = payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.Manufacturer, out var manufacturerValue)
            ? Manufacturer.Parse(manufacturerValue)
            : Manufacturer.Android;

        var newConfigurationItem = configurationItem with { Host = ipAddress, Port = port, Manufacturer = manufacturer };
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
        var maxWaitTime = payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.MaxMessageHandlingWaitTimeInSecondsKey, out var maxWaitTimeValue)
            ? double.Parse(maxWaitTimeValue, NumberFormatInfo.InvariantInfo)
            : 9.5;
        configuration = configuration with { MaxMessageHandlingWaitTimeInSeconds = maxWaitTime };
        configuration.Entities.Remove(configurationItem);
        configuration.Entities.Add(newConfigurationItem);
        await _configurationService.UpdateConfigurationAsync(configuration, cancellationToken);

        if (!await CheckClientApprovedAsync(wsId, configurationItem.EntityId, cancellationToken))
        {
            await SendMessageAsync(socket, AdbTvResponsePayloadHelpers.CreateDeviceSetupChangeUserInputResponsePayload(),
                wsId, cancellationToken);
            return SetupDriverUserDataResult.Handled;
        }

        return await GetSetupResultForClient(wsId, configurationItem.EntityId, cancellationToken);
    }

    protected override async ValueTask<RestoreResult> HandleRestoreFromBackupAsync(string wsId, string jsonRestoreData, CancellationToken cancellationToken)
    {
        try
        {
            var adbKeyDirectory = GetAdbKeyDirectory();
            var backupData = JsonSerializer.Deserialize(jsonRestoreData, AdbJsonSerializerContext.Default.BackupData);
            if (backupData is null)
            {
                _logger.BackupDataNullDuringRestore(wsId);
                return RestoreResult.Failure;
            }

            await _configurationService.UpdateConfigurationAsync(backupData.Configuration, cancellationToken);
            AdbBackgroundService.RestoreInProgress = true;
            _adbTvClientFactory.RemoveAllClients();
            await File.WriteAllBytesAsync(Path.Combine(adbKeyDirectory, "adbkey"), Convert.FromBase64String(backupData.PrivateKey), cancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(adbKeyDirectory, "adbkey.pub"), Convert.FromBase64String(backupData.PublicKey), cancellationToken);
            return RestoreResult.Success;
        }
        catch (Exception e)
        {
            _logger.ExceptionDuringRestore(e, wsId);
            return RestoreResult.Failure;
        }
        finally
        {
            AdbBackgroundService.RestoreInProgress = false;
        }
    }

    protected override async ValueTask<SetupDriverUserDataResult> HandleCreateNewEntity(System.Net.WebSockets.WebSocket socket, SetDriverUserDataMsg payload, string wsId, CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
        var driverMetadata = await _configurationService.GetDriverMetadataAsync(cancellationToken);
        var ipAddress = payload.MsgData.InputValues![AdbTvServerConstants.IpAddressKey];
        var macAddress = payload.MsgData.InputValues[AdbTvServerConstants.MacAddressKey];
        var entityName = payload.MsgData.InputValues.GetStringValueOrDefault(AdbTvServerConstants.EntityName, $"{driverMetadata.Name["en"]} {ipAddress}");
        var port = payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.PortKey, out var portValue)
            ? int.Parse(portValue, NumberFormatInfo.InvariantInfo)
            : 5555;
        var maxWaitTime = payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.MaxMessageHandlingWaitTimeInSecondsKey, out var maxWaitTimeValue)
            ? double.Parse(maxWaitTimeValue, NumberFormatInfo.InvariantInfo)
            : 9.5;
        var manufacturer = payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.Manufacturer, out var manufacturerValue)
            ? Manufacturer.Parse(manufacturerValue)
            : Manufacturer.Android;

        configuration = configuration with { MaxMessageHandlingWaitTimeInSeconds = maxWaitTime };

        var entity = configuration.Entities.FirstOrDefault(x => x.EntityId.Equals(macAddress, StringComparison.OrdinalIgnoreCase));
        if (entity is null)
        {
            _logger.AddingConfigurationForDevice(macAddress);
            entity = new AdbConfigurationItem
            {
                Host = ipAddress,
                MacAddress = macAddress,
                Port = port,
                EntityName = entityName,
                EntityId = macAddress,
                Manufacturer = manufacturer
            };
        }
        else
        {
            _logger.UpdatingConfigurationForDevice(macAddress);
            configuration.Entities.Remove(entity);
            entity = entity with
            {
                Host = ipAddress,
                MacAddress = macAddress,
                Port = port,
                EntityName = entityName
            };
        }

        configuration.Entities.Add(entity);

        await _configurationService.UpdateConfigurationAsync(configuration, cancellationToken);

        if (!await CheckClientApprovedAsync(wsId, entity.EntityId, cancellationToken))
        {
            await SendMessageAsync(socket, AdbTvResponsePayloadHelpers.CreateDeviceSetupChangeUserInputResponsePayload(),
                wsId, cancellationToken);
            return SetupDriverUserDataResult.Handled;
        }

        return await GetSetupResultForClient(wsId, entity.EntityId, cancellationToken);
    }

    private async ValueTask<SetupDriverUserDataResult> GetSetupResultForClient(string wsId, string entityId, CancellationToken cancellationToken)
    {
        if (await TryGetAdbTvClientHolderAsync(wsId, entityId, cancellationToken)
            is not { Client.Device.State: AdvancedSharpAdbClient.Models.DeviceState.Online })
        {
            _logger.DeviceNotOnlineDuringSetupResult(wsId, entityId);
            return SetupDriverUserDataResult.Error;
        }

        ReportedEntityIdStates[entityId] = RemoteState.On;
        return SetupDriverUserDataResult.Finalized;
    }

    protected override MediaPlayerEntityCommandMsgData<AdbMediaPlayerCommandId>? DeserializeMediaPlayerCommandPayload(JsonDocument jsonDocument)
        => jsonDocument.Deserialize(AdbJsonSerializerContext.Default.MediaPlayerEntityCommandMsgDataAdbMediaPlayerCommandId);

    protected override async ValueTask<string> GetJsonBackupDataAsync(CancellationToken cancellationToken)
    {
        var config = await _configurationService.GetConfigurationAsync(cancellationToken);
        var adbKeyDirectory = GetAdbKeyDirectory();
        var privateKey = Path.Combine(adbKeyDirectory, "adbkey");
        var publicKey = Path.Combine(adbKeyDirectory, "adbkey.pub");
        if (!File.Exists(privateKey))
        {
            _logger.AdbPrivateKeyNotFoundForBackup(privateKey);
            throw new FileNotFoundException("No private key found for backup.", privateKey);
        }

        if (!File.Exists(publicKey))
        {
            _logger.AdbPublicKeyNotFoundForBackup(publicKey);
            throw new FileNotFoundException("No public key found for backup.", publicKey);
        }

        return JsonSerializer.Serialize(new BackupData(config,
                Convert.ToBase64String(await File.ReadAllBytesAsync(publicKey, cancellationToken)),
                Convert.ToBase64String(await File.ReadAllBytesAsync(privateKey, cancellationToken))),
            AdbJsonSerializerContext.Default.BackupData);
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
        if (!string.IsNullOrEmpty(sdkHome))
            return Path.Combine(sdkHome, ".android");

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android");
    }

    protected override async ValueTask<SettingsPage> CreateNewEntitySettingsPageAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
        return CreateSettingsPage(null, configuration.MaxMessageHandlingWaitTimeInSeconds ?? 9.5);
    }

    protected override async ValueTask<SettingsPage> CreateReconfigureEntitySettingsPageAsync(AdbConfigurationItem adbConfigurationItem, CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
        var settingsPage = CreateSettingsPage(adbConfigurationItem, configuration.MaxMessageHandlingWaitTimeInSeconds ?? 9.5);
        return settingsPage with
        {
            Settings = settingsPage.Settings.Where(static x =>
                !x.Id.Equals(AdbTvServerConstants.MacAddressKey, StringComparison.OrdinalIgnoreCase) &&
                !x.Id.Equals(AdbTvServerConstants.EntityName, StringComparison.OrdinalIgnoreCase)).ToArray()
        };
    }

    private static SettingsPage CreateSettingsPage(AdbConfigurationItem? configurationItem, double maxMessageHandlingWaitTimeInSeconds)
    {
        return new SettingsPage
        {
            Title = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = configurationItem is null ? "Add a new device" : "Reconfigure device" },
            Settings = [
                new Setting
                {
                    Id = AdbTvServerConstants.EntityName,
                    Field = new SettingTypeText
                    {
                        Text = new ValueRegex()
                    },
                    Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = "Enter the name of the TV (optional)" }
                },
                new Setting
                {
                    Id = AdbTvServerConstants.MacAddressKey,
                    Field = new SettingTypeText
                    {
                        Text = new ValueRegex
                        {
                            RegEx = AdbTvServerConstants.MacAddressRegex
                        }
                    },
                    Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = "Enter the MAC address of the TV (mandatory)" }
                },
                new Setting
                {
                    Id = AdbTvServerConstants.Manufacturer,
                    Field = new SettingTypeDropdown
                    {
                        Dropdown = new SettingTypeDropdownInner
                        {
                            Items = Manufacturer.GetValues().Select(static x => new SettingTypeDropdownItem
                            {
                                Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = x.ToStringFast(true) },
                                Value = x.ToStringFast()
                            }).ToArray(),
                            Value = configurationItem?.Manufacturer.ToStringFast()
                        }
                    },
                    Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = "Select the manufacturer" }
                },
                new Setting
                {
                    Id = AdbTvServerConstants.IpAddressKey,
                    Field = new SettingTypeText
                    {
                        Text = new ValueRegex
                        {
                            RegEx = AdbTvServerConstants.IpAddressRegex,
                            Value = configurationItem?.Host
                        }
                    },
                    Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = "Enter the IP address of the TV (mandatory)" }
                },
                new Setting
                {
                    Id = AdbTvServerConstants.PortKey,
                    Field = new SettingTypeNumber
                    {
                        Number = new SettingTypeNumberInner
                        {
                            Value = configurationItem?.Port ?? 5555,
                            Min = 1,
                            Max = 65535,
                            Decimals = 0
                        }
                    },
                    Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = "Enter the ADB port of the TV (mandatory)" }
                },
                new Setting
                {
                    Id = AdbTvServerConstants.MaxMessageHandlingWaitTimeInSecondsKey,
                    Field = new SettingTypeNumber
                    {
                        Number = new SettingTypeNumberInner
                        {
                            Value = maxMessageHandlingWaitTimeInSeconds,
                            Min = 0.1,
                            Max = 9.5,
                            Decimals = 1
                        }
                    },
                    Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = "Enter the max wait time for a message to be processed (global setting)" }
                }
            ]
        };
    }

    protected override FrozenSet<EntityType> SupportedEntityTypes { get; } = [EntityType.MediaPlayer, EntityType.Remote, EntityType.Select];
}