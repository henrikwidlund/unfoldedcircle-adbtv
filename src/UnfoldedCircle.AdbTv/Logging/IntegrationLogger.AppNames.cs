namespace UnfoldedCircle.AdbTv.Logging;

internal static partial class IntegrationLogger
{
    [LoggerMessage(EventId = 46, EventName = nameof(FailedToResolveAndroidApplicationLabels), Level = LogLevel.Warning,
        Message = "Failed to resolve Android application labels for {EntityId}")]
    public static partial void FailedToResolveAndroidApplicationLabels(this ILogger logger, Exception exception, string entityId);

    [LoggerMessage(EventId = 47, EventName = nameof(FailedToPopulateAndroidApplicationsAfterPowerOn), Level = LogLevel.Warning,
        Message = "Failed to populate Android applications after power-on for {EntityId}")]
    public static partial void FailedToPopulateAndroidApplicationsAfterPowerOn(this ILogger logger, Exception exception, string entityId);

    [LoggerMessage(EventId = 48, EventName = nameof(FailedToEmitResolvedAndroidApplicationLabels), Level = LogLevel.Warning,
        Message = "Failed to emit resolved Android application labels for {EntityId}")]
    public static partial void FailedToEmitResolvedAndroidApplicationLabels(this ILogger logger, Exception exception, string entityId);
}
