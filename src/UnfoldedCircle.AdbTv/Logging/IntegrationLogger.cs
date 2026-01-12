using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.WebSocket;

namespace UnfoldedCircle.AdbTv.Logging;

internal static partial class IntegrationLogger
{
    [LoggerMessage(EventId = 1, EventName = nameof(ClientFailedHealthCheck), Level = LogLevel.Information,
        Message = "Client {ClientKey} failed to execute health check command.")]
    public static partial void ClientFailedHealthCheck(this ILogger logger, Exception exception, in AdbTvClientKey clientKey);

    [LoggerMessage(EventId = 2, EventName = nameof(DeviceNotOnline), Level = LogLevel.Warning,
        Message = "Device {ClientKey} is not online. Connection result was '{ConnectionResult}', device state was {DeviceState}.")]
    public static partial void DeviceNotOnline(this ILogger logger, in AdbTvClientKey clientKey, string? connectionResult, in AdvancedSharpAdbClient.Models.DeviceState? deviceState);

    private static readonly Action<ILogger, AdbTvClientKey, Exception> FailedToGetOrCreateClientAction = LoggerMessage.Define<AdbTvClientKey>(
        LogLevel.Error,
        new EventId(3, nameof(FailedToGetOrCreateClient)),
        "Failed to get or create client {ClientKey}.");

    public static void FailedToGetOrCreateClient(this ILogger logger, Exception exception, in AdbTvClientKey clientKey) =>
        FailedToGetOrCreateClientAction(logger, clientKey, exception);

    private static readonly Action<ILogger, AdbTvClientKey, Exception> FailedToRemoveClientAction = LoggerMessage.Define<AdbTvClientKey>(
        LogLevel.Error,
        new EventId(4, nameof(FailedToRemoveClient)),
        "Failed to remove client {ClientKey}");

    public static void FailedToRemoveClient(this ILogger logger, Exception exception, in AdbTvClientKey clientKey) =>
        FailedToRemoveClientAction(logger, clientKey, exception);

    // AdbWebSocketHandler.AdbClient logging

    [LoggerMessage(EventId = 5, EventName = nameof(NoConfigurationsFound), Level = LogLevel.Information,
        Message = "[{WSId}] WS: No configurations found")]
    public static partial void NoConfigurationsFound(this ILogger logger, string wsId);

    [LoggerMessage(EventId = 6, EventName = nameof(NoConfigurationFoundForIdentifier), Level = LogLevel.Information,
        Message = "[{WSId}] WS: No configuration found for identifier '{Identifier}' with type {Type}")]
    public static partial void NoConfigurationFoundForIdentifier(this ILogger logger, string wsId, string? identifier, in AdbWebSocketHandler.IdentifierType type);

    [LoggerMessage(EventId = 7, EventName = nameof(NoConfigurationFoundForDeviceId), Level = LogLevel.Information,
        Message = "[{WSId}] WS: No configuration found for device ID '{DeviceId}'")]
    public static partial void NoConfigurationFoundForDeviceId(this ILogger logger, string wsId, in ReadOnlyMemory<char> deviceId);

    [LoggerMessage(EventId = 8, EventName = nameof(NoConfigurationFoundForDeviceIdString), Level = LogLevel.Information,
        Message = "[{WSId}] WS: No configuration found for device ID '{DeviceId}'")]
    public static partial void NoConfigurationFoundForDeviceIdString(this ILogger logger, string wsId, string deviceId);

    private static readonly Action<ILogger, string, string?, AdbWebSocketHandler.IdentifierType, Exception> FailedToGetAdbTvClientAction = LoggerMessage.Define<string, string?, AdbWebSocketHandler.IdentifierType>(
        LogLevel.Error,
        new EventId(9, nameof(FailedToGetAdbTvClient)),
        "[{WSId}] WS: Failed to get ADB TV client for identifier '{Identifier}' with type {Type}");

    public static void FailedToGetAdbTvClient(this ILogger logger, Exception exception, string wsId, string? identifier, in AdbWebSocketHandler.IdentifierType type) =>
        FailedToGetAdbTvClientAction(logger, wsId, identifier, type, exception);

    private static readonly Action<ILogger, string, string, Exception> FailedToCheckClientApprovedAction = LoggerMessage.Define<string, string>(
        LogLevel.Error,
        new EventId(10, nameof(FailedToCheckClientApproved)),
        "[{WSId}] WS: Failed to check if client is approved for entity ID '{EntityId}'");

    public static void FailedToCheckClientApproved(this ILogger logger, Exception exception, string wsId, string entityId) =>
        FailedToCheckClientApprovedAction(logger, wsId, entityId, exception);

    [LoggerMessage(EventId = 11, EventName = nameof(CouldNotFindAdbClient), Level = LogLevel.Warning,
        Message = "[{WSId}] WS: Could not find ADB client for entity ID '{EntityId}'")]
    public static partial void CouldNotFindAdbClient(this ILogger logger, string wsId, in ReadOnlyMemory<char> entityId);

    [LoggerMessage(EventId = 12, EventName = nameof(CouldNotFindAdbClientString), Level = LogLevel.Warning,
        Message = "[{WSId}] WS: Could not find ADB client for entity ID '{EntityId}'")]
    public static partial void CouldNotFindAdbClientString(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 13, EventName = nameof(UnknownCommand), Level = LogLevel.Warning,
        Message = "Unknown command '{Command}'")]
    public static partial void UnknownCommand(this ILogger logger, string command);

    [LoggerMessage(EventId = 14, EventName = nameof(AddingConfigurationForDevice), Level = LogLevel.Information,
        Message = "Adding configuration for device ID '{EntityId}'")]
    public static partial void AddingConfigurationForDevice(this ILogger logger, string entityId);

    [LoggerMessage(EventId = 15, EventName = nameof(UpdatingConfigurationForDevice), Level = LogLevel.Information,
        Message = "Updating configuration for device ID '{EntityId}'")]
    public static partial void UpdatingConfigurationForDevice(this ILogger logger, string entityId);

    private static readonly Action<ILogger, Exception> ActionFailedWillRetryAction = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(16, nameof(ActionFailedWillRetry)),
        "Action failed, will retry once.");

    public static void ActionFailedWillRetry(this ILogger logger, Exception exception) =>
        ActionFailedWillRetryAction(logger, exception);

    private static readonly Action<ILogger, string, string, Exception> FailedToPairClientAction = LoggerMessage.Define<string, string>(
        LogLevel.Error,
        new EventId(17, nameof(FailedToPairClient)),
        "[{WSId}] WS: Failed to pair client for entity ID '{EntityId}'");

    public static void FailedToPairClient(this ILogger logger, Exception exception, string wsId, string entityId) =>
        FailedToPairClientAction(logger, wsId, entityId, exception);
}