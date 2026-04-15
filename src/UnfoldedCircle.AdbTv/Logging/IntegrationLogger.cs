using UnfoldedCircle.AdbTv.AdbTv;

namespace UnfoldedCircle.AdbTv.Logging;

internal static partial class IntegrationLogger
{
    [LoggerMessage(EventId = 1, EventName = nameof(DeviceNotOnline), Level = LogLevel.Warning,
        Message = "Device {ClientKey} is not online. Connection result was '{ConnectionResult}', device state was {DeviceState}.")]
    public static partial void DeviceNotOnline(this ILogger logger, in AdbTvClientKey clientKey, string? connectionResult, in AdvancedSharpAdbClient.Models.DeviceState? deviceState);

    private static readonly Action<ILogger, AdbTvClientKey, Exception> FailedToCreateClientAction = LoggerMessage.Define<AdbTvClientKey>(
        LogLevel.Error,
        new EventId(2, nameof(FailedToCreateClient)),
        "Failed to create client {ClientKey}.");

    public static void FailedToCreateClient(this ILogger logger, Exception exception, in AdbTvClientKey clientKey) =>
        FailedToCreateClientAction(logger, clientKey, exception);

    private static readonly Action<ILogger, AdbTvClientKey, Exception> FailedToRemoveClientAction = LoggerMessage.Define<AdbTvClientKey>(
        LogLevel.Error,
        new EventId(3, nameof(FailedToRemoveClient)),
        "Failed to remove client {ClientKey}");

    public static void FailedToRemoveClient(this ILogger logger, Exception exception, in AdbTvClientKey clientKey) =>
        FailedToRemoveClientAction(logger, clientKey, exception);

    [LoggerMessage(EventId = 4, EventName = nameof(NoConfigurationsFound), Level = LogLevel.Information,
        Message = "[{WSId}] WS: No configurations found")]
    public static partial void NoConfigurationsFound(this ILogger logger, string wsId);

    [LoggerMessage(EventId = 5, EventName = nameof(NoConfigurationFoundForIdentifier), Level = LogLevel.Information,
        Message = "[{WSId}] WS: No configuration found for identifier '{Identifier}'")]
    public static partial void NoConfigurationFoundForIdentifier(this ILogger logger, string wsId, string? identifier);

    private static readonly Action<ILogger, string, string?, Exception> FailedToGetAdbTvClientAction = LoggerMessage.Define<string, string?>(
        LogLevel.Error,
        new EventId(8, nameof(FailedToGetAdbTvClient)),
        "[{WSId}] WS: Failed to get ADB TV client for identifier '{Identifier}'");

    public static void FailedToGetAdbTvClient(this ILogger logger, Exception exception, string wsId, string? identifier) =>
        FailedToGetAdbTvClientAction(logger, wsId, identifier, exception);

    private static readonly Action<ILogger, string, string, Exception> FailedToCheckClientApprovedAction = LoggerMessage.Define<string, string>(
        LogLevel.Error,
        new EventId(9, nameof(FailedToCheckClientApproved)),
        "[{WSId}] WS: Failed to check if client is approved for entity ID '{EntityId}'");

    public static void FailedToCheckClientApproved(this ILogger logger, Exception exception, string wsId, string entityId) =>
        FailedToCheckClientApprovedAction(logger, wsId, entityId, exception);

    [LoggerMessage(EventId = 10, EventName = nameof(CouldNotFindAdbClient), Level = LogLevel.Warning,
        Message = "[{WSId}] WS: Could not find ADB client for entity ID '{EntityId}'")]
    public static partial void CouldNotFindAdbClient(this ILogger logger, string wsId, in ReadOnlyMemory<char> entityId);

    [LoggerMessage(EventId = 11, EventName = nameof(CouldNotFindAdbClientString), Level = LogLevel.Warning,
        Message = "[{WSId}] WS: Could not find ADB client for entity ID '{EntityId}'")]
    public static partial void CouldNotFindAdbClientString(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 12, EventName = nameof(UnknownCommand), Level = LogLevel.Warning,
        Message = "Unknown command '{Command}'")]
    public static partial void UnknownCommand(this ILogger logger, string command);

    [LoggerMessage(EventId = 13, EventName = nameof(AddingConfigurationForDevice), Level = LogLevel.Information,
        Message = "Adding configuration for device ID '{EntityId}'")]
    public static partial void AddingConfigurationForDevice(this ILogger logger, string entityId);

    [LoggerMessage(EventId = 14, EventName = nameof(UpdatingConfigurationForDevice), Level = LogLevel.Information,
        Message = "Updating configuration for device ID '{EntityId}'")]
    public static partial void UpdatingConfigurationForDevice(this ILogger logger, string entityId);

    private static readonly Action<ILogger, Exception> ActionFailedWillRetryAction = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(15, nameof(ActionFailedWillRetry)),
        "Action failed, will retry once.");

    public static void ActionFailedWillRetry(this ILogger logger, Exception exception) =>
        ActionFailedWillRetryAction(logger, exception);

    private static readonly Action<ILogger, string, string, Exception> FailureDuringEventAction = LoggerMessage.Define<string, string>(
        LogLevel.Error,
        new EventId(16, nameof(FailureDuringEvent)),
        "{WSId} Failure during event for {Key}.");

    public static void FailureDuringEvent(this ILogger logger, Exception exception, string wsId, string key) =>
        FailureDuringEventAction(logger, wsId, key, exception);

    [LoggerMessage(EventId = 17, EventName = nameof(TimeoutWaitingForSemaphore), Level = LogLevel.Warning,
        Message = "Failed to acquire semaphore for client {ClientKey} within timeout.")]
    public static partial void TimeoutWaitingForSemaphore(this ILogger logger, in AdbTvClientKey clientKey);

    private static readonly Action<ILogger, Exception> ActionFailedWillNotRetryAction = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(18, nameof(ActionFailedWillNotRetry)),
        "Action failed, will not retry.");

    public static void ActionFailedWillNotRetry(this ILogger logger, Exception exception) =>
        ActionFailedWillNotRetryAction(logger, exception);

    [LoggerMessage(EventId = 19, EventName = nameof(AdbPrivateKeyNotFoundForBackup), Level = LogLevel.Warning,
        Message = "ADB private key not found for backup at path '{PrivateKeyPath}'.")]
    public static partial void AdbPrivateKeyNotFoundForBackup(this ILogger logger, string privateKeyPath);

    [LoggerMessage(EventId = 20, EventName = nameof(AdbPublicKeyNotFoundForBackup), Level = LogLevel.Warning,
        Message = "ADB public key not found for backup at path '{PublicKeyPath}'.")]
    public static partial void AdbPublicKeyNotFoundForBackup(this ILogger logger, string publicKeyPath);

    [LoggerMessage(EventId = 21, EventName = nameof(BackupDataNullDuringRestore), Level = LogLevel.Error,
        Message = "[{WSId}] BackupData null during restore.")]
    public static partial void BackupDataNullDuringRestore(this ILogger logger, string wsId);

    private static readonly Action<ILogger, string, Exception> ExceptionDuringRestoreAction = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(22, nameof(ExceptionDuringRestore)),
        "[{WSId}] Exception during restore.");

    public static void ExceptionDuringRestore(this ILogger logger, Exception exception, string wsId) =>
        ExceptionDuringRestoreAction(logger, wsId, exception);

    [LoggerMessage(EventId = 23, EventName = nameof(AdbTvClientKeyNotFound), Level = LogLevel.Warning,
        Message = "[{WSId}] Could not find AdbTvClientKey for entity ID '{EntityId}'")]
    public static partial void AdbTvClientKeyNotFound(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 25, EventName = nameof(AdbTvClientHolderNotFound), Level = LogLevel.Warning,
        Message = "[{WSId}] Could not find AdbTvClientHolder for entity ID '{EntityId}'")]
    public static partial void AdbTvClientHolderNotFound(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 26, EventName = nameof(SelectFirstLastNoAppsFound), Level = LogLevel.Warning,
        Message = "[{WSId}] Select first/last app for entity ID '{EntityId}' failed because no apps were found.")]
    public static partial void SelectFirstLastNoAppsFound(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 27, EventName = nameof(PopulateAppsYieldedNoApps), Level = LogLevel.Warning,
        Message = "[{WSId}] Populate apps for entity ID '{EntityId}' yielded no apps.")]
    public static partial void PopulateAppsYieldedNoApps(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 28, EventName = nameof(SelectNextPreviousNoAppsFound), Level = LogLevel.Warning,
        Message = "[{WSId}] Select next/previous app for entity ID '{EntityId}' failed because no apps were found.")]
    public static partial void SelectNextPreviousNoAppsFound(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 29, EventName = nameof(SelectNextPreviousNoAppsOutOfBounds), Level = LogLevel.Warning,
        Message = "[{WSId}] Select next/previous app for entity ID '{EntityId}' failed because the next index {NextIndex} is out of bounds for apps count {AppsCount}.")]
    public static partial void SelectNextPreviousNoAppsOutOfBounds(this ILogger logger, string wsId, string entityId, int nextIndex, int appsCount);

    [LoggerMessage(EventId = 30, EventName = nameof(DeviceNotOnlineDuringSetupResult), Level = LogLevel.Warning,
        Message = "[{WSId}] Device for entity ID '{EntityId}' is not online.")]
    public static partial void DeviceNotOnlineDuringSetupResult(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 31, EventName = nameof(FailedToAcquireSemaphoreForPopulateApps), Level = LogLevel.Warning,
        Message = "[{WSId}] Failed to acquire semaphore for populating apps for entity ID '{EntityId}' within timeout.")]
    public static partial void FailedToAcquireSemaphoreForPopulateApps(this ILogger logger, string wsId, string entityId);
}