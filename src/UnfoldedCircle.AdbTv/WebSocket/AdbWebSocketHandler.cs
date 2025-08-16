using System.Collections.Frozen;
using System.Globalization;
using System.Net;

using AdvancedSharpAdbClient.DeviceCommands;

using Microsoft.Extensions.Options;

using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.Configuration;
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
    ILogger<AdbWebSocketHandler> logger) : UnfoldedCircleWebSocketHandler<MediaPlayerCommandId, AdbConfigurationItem>(configurationService, options, logger)
{
    private readonly AdbTvClientFactory _adbTvClientFactory = adbTvClientFactory;

    protected override async ValueTask<EntityCommandResult> OnRemoteCommand(
        System.Net.WebSockets.WebSocket socket,
        RemoteEntityCommandMsgData payload,
        string command,
        string wsId,
        CancellationTokenWrapper cancellationTokenWrapper)
    {
        (string commandToSend, CommandType commandType) = GetMappedCommand(command);
        var adbTvClientHolder = await TryGetAdbTvClientHolder(wsId, payload.MsgData.EntityId, IdentifierType.EntityId, cancellationTokenWrapper.RequestAborted);
        if (adbTvClientHolder is null)
        {
            _logger.LogWarning("[{WSId}] WS: Could not find Oppo client for entity ID '{EntityId}'", wsId, payload.MsgData.EntityId);
            return EntityCommandResult.Failure;
        }

        switch (commandType)
        {
            case CommandType.KeyEvent:
                if (command.Equals(RemoteButtonConstants.On, StringComparison.OrdinalIgnoreCase))
                    await WakeOnLan.SendWakeOnLanAsync(IPAddress.Parse(adbTvClientHolder.ClientKey.IpAddress), adbTvClientHolder.ClientKey.MacAddress);

                await adbTvClientHolder.Client.SendKeyEventAsync(commandToSend, cancellationTokenWrapper.ApplicationStopping);

                var result = commandType switch
                {
                    CommandType.KeyEvent when command.Equals(RemoteButtonConstants.On, StringComparison.OrdinalIgnoreCase) => EntityCommandResult.PowerOn,
                    CommandType.KeyEvent when command.Equals(RemoteButtonConstants.Off, StringComparison.OrdinalIgnoreCase) => EntityCommandResult.PowerOff,
                    CommandType.KeyEvent when command.Equals(RemoteButtonConstants.Toggle, StringComparison.OrdinalIgnoreCase) => HandleToggleResult(adbTvClientHolder.ClientKey),
                    _ => EntityCommandResult.Other
                };

                if (result == EntityCommandResult.PowerOn)
                    RemoteStates[adbTvClientHolder.ClientKey] = RemoteState.On;
                else if (result == EntityCommandResult.PowerOff)
                    RemoteStates[adbTvClientHolder.ClientKey] = RemoteState.Off;

                return result;
            case CommandType.Raw:
                await adbTvClientHolder.Client.AdbClient.ExecuteRemoteCommandAsync(commandToSend,
                    adbTvClientHolder.Client.Device,
                    cancellationTokenWrapper.ApplicationStopping);
                return EntityCommandResult.Other;
            case CommandType.App:
                await adbTvClientHolder.Client.AdbClient.StartAppAsync(adbTvClientHolder.Client.Device,
                    commandToSend,
                    cancellationTokenWrapper.ApplicationStopping);
                return EntityCommandResult.Other;
            case CommandType.Unknown:
            default:
                logger.LogWarning("Unknown command '{Command}'", command);
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

    protected override async ValueTask<bool> IsEntityReachable(string wsId, string entityId, CancellationToken cancellationToken)
        => await TryGetAdbTvClientHolder(wsId, entityId, IdentifierType.EntityId, cancellationToken) is not null;

    protected override ValueTask<EntityCommandResult> OnMediaPlayerCommand(System.Net.WebSockets.WebSocket socket,
        MediaPlayerEntityCommandMsgData<MediaPlayerCommandId> payload,
        string wsId,
        CancellationTokenWrapper cancellationTokenWrapper)
        => ValueTask.FromResult(EntityCommandResult.Failure);

    protected override async ValueTask OnConnect(ConnectEvent payload, string wsId, CancellationToken cancellationToken)
    {
        var adbTvClientKeys = await TryGetAdbTvClientKeys(wsId, null, cancellationToken);
        if (adbTvClientKeys is { Length: > 0 })
        {
            foreach (var adbTvClientKey in adbTvClientKeys)
                RemoteStates[adbTvClientKey] = RemoteState.Off;
        }
    }

    protected override ValueTask<bool> OnDisconnect(DisconnectEvent payload, string wsId, CancellationToken cancellationToken)
        => TryDisconnectAdbClients(wsId, payload.MsgData?.DeviceId, cancellationToken);

    protected override ValueTask OnAbortDriverSetup(AbortDriverSetupEvent payload, string wsId, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    protected override ValueTask OnEnterStandby(EnterStandbyEvent payload, string wsId, CancellationToken cancellationToken)
    {
        _adbTvClientFactory.RemoveAllClients();
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnExitStandby(ExitStandbyEvent payload, string wsId, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    protected override Task HandleEventUpdates(System.Net.WebSockets.WebSocket socket, string entityId, string wsId, CancellationTokenWrapper cancellationTokenWrapper)
        => Task.CompletedTask;

    protected override ValueTask<DeviceState> OnGetDeviceState(GetDeviceStateMsg payload, string wsId, CancellationToken cancellationToken)
            => ValueTask.FromResult(DeviceState.Connected);

    protected override async ValueTask<EntityState> GetEntityState(AdbConfigurationItem entity, string wsId, CancellationToken cancellationToken) =>
        await TryGetAdbTvClientHolder(wsId, entity.EntityId, IdentifierType.EntityId, cancellationToken) is null
            ? EntityState.Disconnected : EntityState.Connected;

    protected override async ValueTask<IReadOnlyCollection<AvailableEntity>> OnGetAvailableEntities(GetAvailableEntitiesMsg payload, string wsId, CancellationToken cancellationToken)
    {
        var entities = await GetEntities(wsId, payload.MsgData.Filter?.DeviceId, cancellationToken);
        return GetAvailableEntities(entities, payload).ToArray();
    }

    protected override ValueTask OnSubscribeEvents(System.Net.WebSockets.WebSocket socket, CommonReq payload, string wsId, CancellationTokenWrapper cancellationTokenWrapper)
        => ValueTask.CompletedTask;

    protected override ValueTask OnUnsubscribeEvents(UnsubscribeEventsMsg payload, string wsId, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    protected override async ValueTask<EntityStateChanged[]> OnGetEntityStates(GetEntityStatesMsg payload, string wsId, CancellationToken cancellationToken)
    {
        var entities = await GetEntities(wsId, payload.MsgData?.DeviceId, cancellationToken);
        return entities is null
            ? []
            : AdbTvResponsePayloadHelpers.GetEntityStates(entities.Select(static x => new EntityIdDeviceId(x.EntityId, x.DeviceId))).ToArray();
    }

    protected override async ValueTask OnSetupDriverUserData(System.Net.WebSockets.WebSocket socket, SetDriverUserDataMsg payload, string wsId, CancellationToken cancellationToken)
    {
        if (TryGetEntityIdFromSocket(wsId, out var entityId))
        {
            var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
            var entity = configuration.Entities.FirstOrDefault(x => x.EntityId.Equals(entityId, StringComparison.OrdinalIgnoreCase));

            if (entity is null)
            {
                _logger.LogError("Could not find configuration item with id: {EntityId}", entityId);
                await SendAsync(socket,
                    ResponsePayloadHelpers.CreateValidationErrorResponsePayload(payload,
                        new ValidationError
                        {
                            Code = "INV_ARGUMENT",
                            Message = "Could not find specified entity"
                        }),
                    wsId,
                    cancellationToken);
                return;
            }
            if (!await CheckClientApproved(wsId, entity.EntityId, cancellationToken))
            {
                await SendAsync(socket, AdbTvResponsePayloadHelpers.CreateDeviceSetupChangeUserInputResponsePayload(),
                    wsId, cancellationToken);
                return;
            }

            await FinishSetup(socket, wsId, entity, payload, cancellationToken);
            return;
        }

        _logger.LogError("Could not find entity for WSId: {EntityId}", wsId);
        await SendAsync(socket,
            ResponsePayloadHelpers.CreateValidationErrorResponsePayload(payload,
                new ValidationError
                {
                    Code = "INV_ARGUMENT",
                    Message = "Could not find specified entity"
                }),
            wsId,
            cancellationToken);
    }

    protected override MediaPlayerEntityCommandMsgData<MediaPlayerCommandId>? DeserializeMediaPlayerCommandPayload(JsonDocument jsonDocument)
        => null;

    protected override async ValueTask<OnSetupResult?> OnSetupDriver(SetupDriverMsg payload, string wsId, CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
        var driverMetadata = await _configurationService.GetDriverMetadataAsync(cancellationToken);
        var ipAddress = payload.MsgData.SetupData[AdbTvServerConstants.IpAddressKey];
        var macAddress = payload.MsgData.SetupData[AdbTvServerConstants.MacAddressKey];
        var deviceId = payload.MsgData.SetupData.GetValueOrNull(AdbTvServerConstants.DeviceIdKey, macAddress);
        var deviceName = payload.MsgData.SetupData.GetValueOrNull(AdbTvServerConstants.DeviceNameKey, $"{driverMetadata.Name["en"]} {ipAddress}");
        var port = payload.MsgData.SetupData.TryGetValue(AdbTvServerConstants.PortKey, out var portValue)
            ? int.Parse(portValue, NumberFormatInfo.InvariantInfo)
            : 5555;

        var entity = configuration.Entities.Find(x => string.Equals(x.EntityId, macAddress, StringComparison.Ordinal));
        if (entity is null)
        {
            _logger.LogInformation("Adding configuration for device ID '{EntityId}'", macAddress);
            entity = new AdbConfigurationItem
            {
                Host = ipAddress,
                MacAddress = macAddress,
                Port = port,
                DeviceId = deviceId,
                EntityName = deviceName,
                EntityId = macAddress
            };
        }
        else
        {
            _logger.LogInformation("Updating configuration for device ID '{EntityId}'", macAddress);
            configuration.Entities.Remove(entity);
            entity = entity with
            {
                Host = ipAddress,
                MacAddress = macAddress,
                Port = port,
                EntityName = deviceName
            };
        }

        configuration.Entities.Add(entity);

        await _configurationService.UpdateConfigurationAsync(configuration, cancellationToken);

        if (!await CheckClientApproved(wsId, entity.EntityId, cancellationToken))
        {
            return new OnSetupResult(entity, SetupDriverResult.UserInputRequired, new RequireUserAction
            {
                Confirmation = new ConfirmationPage
                {
                    Title = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["en"] = "Confirm ADB Access on your TV"
                    }
                }
            });
        }

        return new OnSetupResult(entity, SetupDriverResult.Finalized);
    }

    protected override FrozenSet<EntityType> SupportedEntityTypes { get; } = [EntityType.Remote];
}