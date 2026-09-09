using UnfoldedCircle.AdbTv.AdbTv;

namespace UnfoldedCircle.AdbTv.Logging;

internal static partial class IntegrationLogger
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Device {ClientKey} is not online. Last connection error was '{ConnectionError}'.")]
    public static partial void DeviceNotOnline(this ILogger logger, in AdbTvClientKey clientKey, string? connectionError);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to create client {ClientKey}.")]
    public static partial void FailedToCreateClient(this ILogger logger, Exception exception, in AdbTvClientKey clientKey);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to remove client {ClientKey}")]
    public static partial void FailedToRemoveClient(this ILogger logger, Exception exception, in AdbTvClientKey clientKey);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "[{WSId}] WS: No configurations found")]
    public static partial void NoConfigurationsFound(this ILogger logger, string wsId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "[{WSId}] WS: No configuration found for identifier '{Identifier}'")]
    public static partial void NoConfigurationFoundForIdentifier(this ILogger logger, string wsId, string? identifier);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "[{WSId}] WS: Failed to get ADB TV client for identifier '{Identifier}'")]
    public static partial void FailedToGetAdbTvClient(this ILogger logger, Exception exception, string wsId, string? identifier);

    [LoggerMessage(EventId = 9, Level = LogLevel.Error, Message = "[{WSId}] WS: Failed to check if client is approved for entity ID '{EntityId}'")]
    public static partial void FailedToCheckClientApproved(this ILogger logger, Exception exception, string wsId, string entityId);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "[{WSId}] WS: Could not find ADB client for entity ID '{EntityId}'")]
    public static partial void CouldNotFindAdbClient(this ILogger logger, string wsId, in ReadOnlyMemory<char> entityId);

    [LoggerMessage(EventId = 12, Level = LogLevel.Warning, Message = "Unknown command '{Command}'")]
    public static partial void UnknownCommand(this ILogger logger, string command);

    [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "Adding configuration for device ID '{EntityId}'")]
    public static partial void AddingConfigurationForDevice(this ILogger logger, string entityId);

    [LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "Updating configuration for device ID '{EntityId}'")]
    public static partial void UpdatingConfigurationForDevice(this ILogger logger, string entityId);

    [LoggerMessage(EventId = 15, Level = LogLevel.Warning, Message = "Action failed, will retry once.")]
    public static partial void ActionFailedWillRetry(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 16, Level = LogLevel.Error, Message = "[{WSId}] Failure during event for {Key}.")]
    public static partial void FailureDuringEvent(this ILogger logger, Exception exception, string wsId, string key);

    [LoggerMessage(EventId = 17, Level = LogLevel.Warning, Message = "Failed to acquire semaphore for client {ClientKey} within timeout.")]
    public static partial void TimeoutWaitingForSemaphore(this ILogger logger, in AdbTvClientKey clientKey);

    [LoggerMessage(EventId = 18, Level = LogLevel.Warning, Message = "Action failed, will not retry.")]
    public static partial void ActionFailedWillNotRetry(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 19, Level = LogLevel.Warning, Message = "ADB private key not found for backup at path '{PrivateKeyPath}'.")]
    public static partial void AdbPrivateKeyNotFoundForBackup(this ILogger logger, string privateKeyPath);

    [LoggerMessage(EventId = 21, Level = LogLevel.Error, Message = "[{WSId}] BackupData null during restore.")]
    public static partial void BackupDataNullDuringRestore(this ILogger logger, string wsId);

    [LoggerMessage(EventId = 22, Level = LogLevel.Error, Message = "[{WSId}] Exception during restore.")]
    public static partial void ExceptionDuringRestore(this ILogger logger, Exception exception, string wsId);

    [LoggerMessage(EventId = 23, Level = LogLevel.Warning, Message = "[{WSId}] Could not find AdbTvClientKey for entity ID '{EntityId}'")]
    public static partial void AdbTvClientKeyNotFound(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 25, Level = LogLevel.Warning, Message = "[{WSId}] Could not find AdbTvClientHolder for entity ID '{EntityId}'")]
    public static partial void AdbTvClientHolderNotFound(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 26, Level = LogLevel.Warning, Message = "[{WSId}] Select first/last app for entity ID '{EntityId}' failed because no apps were found.")]
    public static partial void SelectFirstLastNoAppsFound(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 27, Level = LogLevel.Warning, Message = "[{WSId}] Populate apps for entity ID '{EntityId}' yielded no apps.")]
    public static partial void PopulateAppsYieldedNoApps(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 28, Level = LogLevel.Warning, Message = "[{WSId}] Select next/previous app for entity ID '{EntityId}' failed because no apps were found.")]
    public static partial void SelectNextPreviousNoAppsFound(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 29, Level = LogLevel.Warning,
        Message = "[{WSId}] Select next/previous app for entity ID '{EntityId}' failed because the next index {NextIndex} is out of bounds for apps count {AppsCount}.")]
    public static partial void SelectNextPreviousNoAppsOutOfBounds(this ILogger logger, string wsId, string entityId, int nextIndex, int appsCount);

    [LoggerMessage(EventId = 30, Level = LogLevel.Warning, Message = "[{WSId}] Device for entity ID '{EntityId}' is not online.")]
    public static partial void DeviceNotOnlineDuringSetupResult(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 31, Level = LogLevel.Warning, Message = "[{WSId}] Failed to acquire semaphore for populating apps for entity ID '{EntityId}' within timeout.")]
    public static partial void FailedToAcquireSemaphoreForPopulateApps(this ILogger logger, string wsId, string entityId);

    [LoggerMessage(EventId = 32, Level = LogLevel.Warning, Message = "[{WSId}] Failure during subscribe events for entity ID '{EntityId}'.")]
    public static partial void FailureDuringSubscribeEvents(this ILogger logger, Exception exception, string wsId, string entityId);

    [LoggerMessage(EventId = 33, Level = LogLevel.Information, Message = "Creating new key.")]
    public static partial void CreatingNewKey(this ILogger logger);

    [LoggerMessage(EventId = 34, Level = LogLevel.Information, Message = "Created new key.")]
    public static partial void CreatedNewKey(this ILogger logger);

    [LoggerMessage(EventId = 35, Level = LogLevel.Warning, Message = "[{WSId}] Failed to start app for entity ID '{EntityId}': '{AppIdentifier}'.")]
    public static partial void FailedToStartApp(this ILogger logger, string wsId, string entityId, string appIdentifier);

    [LoggerMessage(EventId = 36, Level = LogLevel.Warning,
        Message = "Raw shell command failed for client {ClientKey}: '{Command}'. Error was: {Error}")]
    public static partial void RawCommandFailed(this ILogger logger, in AdbTvClientKey clientKey, string command, string error);

    [LoggerMessage(EventId = 37, Level = LogLevel.Warning, Message = "App launch failed for client {ClientKey}: '{Command}'. Failure reason: {FailureReason}")]
    public static partial void AppLaunchFailed(this ILogger logger, in AdbTvClientKey clientKey, string command, string? failureReason);

    [LoggerMessage(EventId = 38, Level = LogLevel.Warning, Message = "Failed to send Wake-on-LAN packet.")]
    public static partial void FailedToSendWakeOnLan(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 39, Level = LogLevel.Warning, Message = "Failed to resolve current adb-tls-connect port via mDNS for device GUID '{DeviceGuid}'.")]
    public static partial void MdnsResolveFailed(this ILogger logger, Exception exception, string deviceGuid);

    [LoggerMessage(EventId = 40, Level = LogLevel.Information, Message = "Resolved new adb-tls-connect endpoint for device GUID '{DeviceGuid}': {Host}:{Port}")]
    public static partial void MdnsResolvedNewEndpoint(this ILogger logger, string deviceGuid, string host, int port);

    [LoggerMessage(EventId = 41, Level = LogLevel.Debug, Message = "mDNS resolution for device GUID '{DeviceGuid}' timed out or found nothing; falling back to last-known address.")]
    public static partial void MdnsResolveTimedOut(this ILogger logger, string deviceGuid);

    [LoggerMessage(EventId = 42, Level = LogLevel.Information, Message = "[{WSId}] Wireless pairing code submitted for host '{Host}:{Port}'.")]
    public static partial void PairingCodeSubmitted(this ILogger logger, string wsId, string host, int port);

    [LoggerMessage(EventId = 43, Level = LogLevel.Warning, Message = "[{WSId}] Wireless pairing failed.")]
    public static partial void PairingFailed(this ILogger logger, Exception exception, string wsId);

    [LoggerMessage(EventId = 44, Level = LogLevel.Warning, Message = "Initial adb-tls-connect mDNS discovery query failed on startup.")]
    public static partial void MdnsListenerStartupQueryFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 45, Level = LogLevel.Warning, Message = "ADB auth key file at '{PrivateKeyPath}' exists but failed to parse; regenerating a new key.")]
    public static partial void CorruptAuthKeyFileRegenerating(this ILogger logger, Exception exception, string privateKeyPath);

    [LoggerMessage(EventId = 46, Level = LogLevel.Warning, Message = "Failed to resolve Android application labels for {EntityId}")]
    public static partial void FailedToResolveAndroidApplicationLabels(this ILogger logger, Exception exception, string entityId);

    [LoggerMessage(EventId = 47, Level = LogLevel.Warning, Message = "Failed to populate Android applications after power-on for {EntityId}")]
    public static partial void FailedToPopulateAndroidApplicationsAfterPowerOn(this ILogger logger, Exception exception, string entityId);

    [LoggerMessage(EventId = 48, Level = LogLevel.Warning, Message = "Failed to emit resolved Android application labels for {EntityId}")]
    public static partial void FailedToEmitResolvedAndroidApplicationLabels(this ILogger logger, Exception exception, string entityId);

    [LoggerMessage(EventId = 49, Level = LogLevel.Warning, Message = "[{WSId}] Power state query failed for '{EntityId}'; reconnecting and retrying once.")]
    public static partial void PowerStateQueryFailedReconnecting(this ILogger logger, Exception exception, string wsId, string entityId);
}
