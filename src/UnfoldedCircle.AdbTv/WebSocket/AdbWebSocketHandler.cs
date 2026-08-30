using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using System.Net;
using System.Text;

using Microsoft.Extensions.Options;

using Theodicean.SharpAdb.Pairing;
using Theodicean.SharpAdb.Services;

using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.AdbTv.Discovery;
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
    AdbMdnsDiscovery adbMdnsDiscovery,
    IOptions<UnfoldedCircleOptions> options,
    ILogger<AdbWebSocketHandler> logger,
    ILoggerFactory loggerFactory) : UnfoldedCircleWebSocketHandler<AdbMediaPlayerCommandId, AdbConfigurationItem>(configurationService, options, logger)
{
    private readonly AdbTvClientFactory _adbTvClientFactory = adbTvClientFactory;
    private readonly AdbMdnsDiscovery _adbMdnsDiscovery = adbMdnsDiscovery;

    // Real pairing (TCP connect + TLS 1.3 handshake + two SPAKE2 round trips) completes in a
    // couple of seconds on a healthy LAN; this is a generous ceiling for a slow/lossy connection,
    // chosen to fail well before the remote's own client-side setup timeout gives up on us.
    private static readonly TimeSpan PairingTimeout = TimeSpan.FromSeconds(20);

    private readonly ConcurrentDictionary<string, List<string>> _entityIdAppsMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Dictionary<string, AppReference>> _entityIdAppAliasesMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _entityIdActiveAppMap = new(StringComparer.OrdinalIgnoreCase);

    private sealed record AppReference(string Label, string PackageName, string DisplayName);

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

        // App commands accept a raw package, a dynamically discovered application label,
        // or the legacy "Label (package)" form. Unknown remote commands are also offered to
        // the app resolver so a bare label can be used without an APP: prefix.
        if (commandType is CommandType.App or CommandType.Unknown)
        {
            var appIdentifier = commandType == CommandType.App ? commandToSend : command;
            var resolvedApp = await ResolveAppIdentifierAsync(wsId, payload.MsgData.EntityId, appIdentifier, commandCancellationToken);
            if (resolvedApp is not null)
            {
                commandToSend = resolvedApp.PackageName;
                commandType = CommandType.App;
            }
            else if (commandType == CommandType.App)
            {
                return EntityCommandResult.Failure;
            }
        }

        bool isPowerOn = command.Equals(RemoteButtonConstants.On, StringComparison.OrdinalIgnoreCase);
        bool isPowerOff = command.Equals(RemoteButtonConstants.Off, StringComparison.OrdinalIgnoreCase);
        bool isToggle = command.Equals(RemoteButtonConstants.Toggle, StringComparison.OrdinalIgnoreCase);

        var result = await ExecuteCommandAsync(adbTvClientHolder, commandToSend, commandType, isPowerOn, isPowerOff, isToggle, commandCancellationToken);
        if (result == EntityCommandResult.PowerOn)
            _ = PopulateAppsAfterPowerOnAsync(wsId, payload.MsgData.EntityId, cancellationTokenWrapper.RequestAborted);

        return result;
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
        var app = await ResolveAppIdentifierAsync(wsId, payload.MsgData.EntityId, option, commandCancellationToken);
        if (app is not null && await StartApp(wsId, payload.MsgData.EntityId, app.PackageName, cancellationTokenWrapper.RequestAborted))
        {
            var alternateLookup = _entityIdActiveAppMap.GetAlternateLookup<ReadOnlySpan<char>>();
            alternateLookup[payload.MsgData.EntityId.AsSpan().GetBaseIdentifier()] = app.DisplayName;
            return new SelectCommandResult(EntityCommandResult.Other, app.DisplayName);
        }

        return new SelectCommandResult(EntityCommandResult.Failure, string.Empty);
    }

    private async ValueTask<bool> StartApp(string wsId, string entityId, string packageName, CancellationToken cancellationToken)
    {
        var adbTvClientHolder = await TryGetAdbTvClientHolderAsync(wsId, entityId, cancellationToken);
        if (adbTvClientHolder is null)
            return false;

        var result = await adbTvClientHolder.Connection.StartAppAsync(packageName, cancellationToken);
        if (result.IsLaunched)
            return true;

        _logger.FailedToStartApp(wsId, entityId, packageName);
        return false;
    }

    private async ValueTask<AppReference?> ResolveAppIdentifierAsync(
        string wsId,
        string entityId,
        string appIdentifier,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appIdentifier))
            return null;

        var value = appIdentifier.Trim();
        var baseIdentifier = entityId.AsMemory().GetBaseIdentifier();
        var aliasesLookup = _entityIdAppAliasesMap.GetAlternateLookup<ReadOnlySpan<char>>();

        if (aliasesLookup.TryGetValue(baseIdentifier.Span, out var aliases)
            && aliases.TryGetValue(value, out var knownApp))
            return knownApp;

        // The canonical UI format is "Application name (package.name)". Accept it even if
        // the app cache has not been populated yet.
        var packageFromDisplayName = TryExtractPackageName(value);
        if (packageFromDisplayName is not null)
        {
            if (aliases is not null && aliases.TryGetValue(packageFromDisplayName, out knownApp))
                return knownApp;

            var label = value[..value.LastIndexOf(" (", StringComparison.Ordinal)].Trim();
            return new AppReference(label, packageFromDisplayName, value);
        }

        // Preserve backwards compatibility with callers that send a raw package identifier.
        if (LooksLikePackageName(value))
        {
            if (aliases is not null && aliases.TryGetValue(value, out knownApp))
                return knownApp;

            return new AppReference(value, value, value);
        }

        // A bare application name needs the dynamically discovered label cache. Bare labels are
        // registered only when unique; raw packages remain available for unambiguous commands.
        if (!await PopulateApps(wsId, entityId, cancellationToken))
            return null;

        if (aliasesLookup.TryGetValue(baseIdentifier.Span, out aliases)
            && aliases.TryGetValue(value, out knownApp))
            return knownApp;

        return null;
    }

    private static string? TryExtractPackageName(string value)
    {
        if (!value.EndsWith(')'))
            return null;

        var separatorIndex = value.LastIndexOf(" (", StringComparison.Ordinal);
        if (separatorIndex <= 0)
            return null;

        var packageName = value[(separatorIndex + 2)..^1].Trim();
        return LooksLikePackageName(packageName) ? packageName : null;
    }

    private static bool LooksLikePackageName(string value)
        => value.Contains('.', StringComparison.Ordinal)
           && value.All(static c => char.IsLetterOrDigit(c) || c is '.' or '_');

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

        var resolvedApp = await ResolveAppIdentifierAsync(wsId, payload.MsgData.EntityId, app, commandCancellationToken);
        if (resolvedApp is not null && await StartApp(wsId, payload.MsgData.EntityId, resolvedApp.PackageName, commandCancellationToken))
        {
            var activeEntityAppAlternativeLookup = _entityIdActiveAppMap.GetAlternateLookup<ReadOnlySpan<char>>();
            activeEntityAppAlternativeLookup[baseIdentifier.Span] = resolvedApp.DisplayName;
            return new SelectCommandResult(EntityCommandResult.Other, resolvedApp.DisplayName);
        }

        return new SelectCommandResult(EntityCommandResult.Failure, string.Empty);
    }

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _appFetchSemaphores = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task> _appLabelResolutionTasks = new(StringComparer.OrdinalIgnoreCase);
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
            try
            {
                if (alternateLookup.ContainsKey(baseIdentifier.Span))
                    return true;

                var adbClientHolder = await TryGetAdbTvClientHolderAsync(wsId, entityId, cancellationToken);
                if (adbClientHolder is null)
                {
                    _logger.PopulateAppsYieldedNoApps(wsId, entityId);
                    return false;
                }

                // Publish package identifiers first so the source/options list is available as soon as
                // pm returns. Friendly labels are resolved in the background and replace this list later.
                var packageNames = new List<string>();
                await foreach (string appIdentifier in adbClientHolder.Connection.ExecuteLinesAsync("pm list packages -3", cancellationToken))
                {
                    var packageName = appIdentifier.Replace("package:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                    if (LooksLikePackageName(packageName))
                        packageNames.Add(packageName);
                }

                var baseEntityId = baseIdentifier.ToString();
                SetAppsCache(baseEntityId,
                    packageNames.Select(static packageName => new AppReference(packageName, packageName, packageName)).ToList());

                if (packageNames.Count > 0)
                {
                    _ = _appLabelResolutionTasks.GetOrAdd(baseEntityId,
                        _ => ResolveAppLabelsAsync(wsId, entityId, packageNames, cancellationToken));
                }

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

    private async Task ResolveAppLabelsAsync(
        string wsId,
        string entityId,
        List<string> packageNames,
        CancellationToken cancellationToken)
    {
        try
        {
            var adbClientHolder = await TryGetAdbTvClientHolderAsync(wsId, entityId, cancellationToken);
            if (adbClientHolder is null)
                return;

            var helperCheck = await adbClientHolder.Connection.ExecuteAsync($"test -s {AppNamesHelper.RemotePath}", cancellationToken);
            var helperAvailable = helperCheck.IsSuccess;
            if (!helperAvailable)
            {
                var uploadCommand = $"printf '%s' '{AppNamesHelper.DexBase64}' | base64 -d > {AppNamesHelper.RemotePath} && chmod 644 {AppNamesHelper.RemotePath}";
                var uploadResult = await adbClientHolder.Connection.ExecuteAsync(uploadCommand, cancellationToken);
                helperAvailable = uploadResult.IsSuccess;
            }

            if (!helperAvailable)
                return;

            var labelsByPackage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var packageArguments = string.Join(' ', packageNames);
            var helperCommand = $"app_process -cp {AppNamesHelper.RemotePath} /system/bin uc.adbtv.AppNames {packageArguments}";
            await foreach (string appLine in adbClientHolder.Connection.ExecuteLinesAsync(helperCommand, cancellationToken))
            {
                var separatorIndex = appLine.IndexOf('\t');
                if (separatorIndex <= 0)
                    continue;

                var packageName = appLine[..separatorIndex].Trim();
                var label = appLine[(separatorIndex + 1)..].Trim();
                if (LooksLikePackageName(packageName))
                    labelsByPackage[packageName] = string.IsNullOrEmpty(label) ? packageName : label;
            }

            var appReferences = new List<AppReference>(packageNames.Count);
            foreach (var packageName in packageNames)
            {
                var label = labelsByPackage.TryGetValue(packageName, out var resolvedLabel) ? resolvedLabel : packageName;
                var displayName = label.Equals(packageName, StringComparison.OrdinalIgnoreCase) ? packageName : label;
                appReferences.Add(new AppReference(label, packageName, displayName));
            }

            SetAppsCache(entityId.AsMemory().GetBaseIdentifier().ToString(), appReferences);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // expected on disconnect / shutdown
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to resolve Android application labels for {EntityId}", entityId);
        }
    }

    private void SetAppsCache(string baseEntityId, List<AppReference> appReferences)
    {
        appReferences.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.DisplayName, right.DisplayName));
        var apps = appReferences.Select(static app => app.DisplayName).ToList();
        var aliases = new Dictionary<string, AppReference>(StringComparer.OrdinalIgnoreCase);
        var labelCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var app in appReferences)
        {
            aliases[app.PackageName] = app;
            labelCounts[app.Label] = labelCounts.TryGetValue(app.Label, out var count) ? count + 1 : 1;
        }

        foreach (var app in appReferences)
        {
            if (labelCounts[app.Label] == 1)
                aliases[app.Label] = app;
        }

        _entityIdAppsMap[baseEntityId] = apps;
        _entityIdAppAliasesMap[baseEntityId] = aliases;
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
        var resolvedApp = await ResolveAppIdentifierAsync(wsId, payload.MsgData.EntityId, app, commandCancellationToken);
        if (resolvedApp is not null && await StartApp(wsId, payload.MsgData.EntityId, resolvedApp.PackageName, commandCancellationToken))
        {
            entityIdActiveAppMapAlternate[baseIdentifier.Span] = resolvedApp.DisplayName;
            return new SelectCommandResult(EntityCommandResult.Other, resolvedApp.DisplayName);
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
        if (commandType == CommandType.App)
        {
            var resolvedApp = await ResolveAppIdentifierAsync(wsId, payload.MsgData.EntityId, command, commandCancellationToken);
            if (resolvedApp is null)
                return EntityCommandResult.Failure;

            command = resolvedApp.PackageName;
        }

        bool isPowerOn = payload.MsgData.CommandId == AdbMediaPlayerCommandId.On;
        bool isPowerOff = payload.MsgData.CommandId == AdbMediaPlayerCommandId.Off;
        bool isToggle = payload.MsgData.CommandId == AdbMediaPlayerCommandId.Toggle;

        var result = await ExecuteCommandAsync(adbTvClientHolder, command, commandType, isPowerOn, isPowerOff, isToggle, commandCancellationToken);
        if (result == EntityCommandResult.PowerOn)
            _ = PopulateAppsAfterPowerOnAsync(wsId, payload.MsgData.EntityId, cancellationTokenWrapper.RequestAborted);

        return result;
    }

    private async Task PopulateAppsAfterPowerOnAsync(string wsId, string entityId, CancellationToken cancellationToken)
    {
        try
        {
            // WOL/key-event acknowledgement precedes Android becoming fully Awake. Retry briefly
            // so package enumeration starts as soon as PackageManager is usable.
            for (var attempt = 0; attempt < 20 && !cancellationToken.IsCancellationRequested; attempt++)
            {
                var holder = await TryGetAdbTvClientHolderAsync(wsId, entityId, cancellationToken);
                if (holder is not null && await GetPowerState(holder, cancellationToken) == PowerState.On)
                {
                    await PopulateApps(wsId, entityId, cancellationToken);
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // expected on disconnect / shutdown
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to populate Android applications after power-on for {EntityId}", entityId);
        }
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

    protected override async ValueTask OnEnterStandbyAsync(EnterStandbyEvent payload, string wsId, CancellationToken cancellationToken)
        => await AdbTvClientFactory.RemoveAllClients();

    protected override ValueTask OnExitStandbyAsync(ExitStandbyEvent payload, string wsId, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    private readonly ConcurrentDictionary<string, PowerState> _reportedPowerStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<string>> _reportedApps = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task HandleEventUpdatesAsync(System.Net.WebSockets.WebSocket socket, string wsId, SubscribedEntitiesHolder subscribedEntitiesHolder, CancellationToken cancellationToken)
    {
        using var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            await Parallel.ForEachAsync(subscribedEntitiesHolder.SubscribedEntities, cancellationToken,
                async (group, token) =>
                {
                    try
                    {
                        await UpdateEntityGroupAsync(socket, wsId, group.Key, group.Value, false, token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        // expected on shutdown / disconnect
                    }
                    catch (Exception e)
                    {
                        _logger.FailureDuringEvent(e, wsId, group.Key);
                    }
                });
        } while (!cancellationToken.IsCancellationRequested && await periodicTimer.WaitForNextTickAsync(cancellationToken));
    }

    private static IGrouping<ReadOnlyMemory<char>, string>[] GroupIdentifiers(string[] entityIds)
    {
        return
        [
            .. entityIds
                .GroupBy(static x => x.AsMemory().GetBaseIdentifier(), ReadOnlyMemoryCharComparer.Instance)
        ];
    }


    private async Task UpdateEntityGroupAsync(
        System.Net.WebSockets.WebSocket socket,
        string wsId,
        string baseEntityId,
        HashSet<SubscribedEntity> subscribedEntities,
        bool forceEmit,
        CancellationToken cancellationToken)
    {
        var holder = await TryGetAdbTvClientHolderAsync(wsId, baseEntityId, cancellationToken);
        var power = holder is null
            ? PowerState.Unknown
            : await GetPowerState(holder, cancellationToken);

        var powerChanged = forceEmit
            || !_reportedPowerStates.TryGetValue(baseEntityId, out var previousPower)
            || previousPower != power;
        if (powerChanged)
        {
            _reportedPowerStates[baseEntityId] = power;
            if (holder is not null)
                RemoteStates[holder.ClientKey] = MapRemote(power);
        }

        // Fetch apps only when device is awake. Skips pm exec on Off / Dozing / Unknown — avoids any wake risk.
        List<string>? apps = null;
        bool appsChanged = false;
        bool needsApps = subscribedEntities.Any(static e => e.EntityType is EntityType.MediaPlayer or EntityType.Select);
        if (power == PowerState.On && needsApps && await PopulateApps(wsId, baseEntityId, cancellationToken))
        {
            var lookup = _entityIdAppsMap.GetAlternateLookup<ReadOnlySpan<char>>();
            if (lookup.TryGetValue(baseEntityId.AsSpan(), out apps)
                && apps.Count > 0
                && (forceEmit
                    || !_reportedApps.TryGetValue(baseEntityId, out var reportedApps)
                    || !ReferenceEquals(reportedApps, apps)))
            {
                appsChanged = true;
                _reportedApps[baseEntityId] = apps;
            }
        }

        foreach (var sub in subscribedEntities)
        {
            switch (sub.EntityType)
            {
                case EntityType.MediaPlayer:
                    await EmitMediaPlayerDeltaAsync(socket, wsId, sub.EntityId, power, apps, powerChanged, appsChanged, cancellationToken);
                    break;
                case EntityType.Remote when powerChanged:
                    await SendMessageAsync(socket,
                        ResponsePayloadHelpers.CreateRemoteStateChangedResponsePayload(
                            new RemoteStateChangedEventMessageDataAttributes { State = MapRemote(power) },
                            sub.EntityId),
                        wsId, cancellationToken);
                    break;
                case EntityType.Select:
                    await EmitSelectDeltaAsync(socket, wsId, sub.EntityId, power, apps, powerChanged, appsChanged, cancellationToken);
                    break;
            }
        }

        // If package identifiers were just emitted, push the friendly-name replacement as soon as
        // PackageManager resolution completes. Do not wait for the next polling interval.
        if (appsChanged
            && apps is { Count: > 0 }
            && _appLabelResolutionTasks.TryGetValue(baseEntityId, out var labelResolutionTask))
        {
            _ = EmitResolvedAppsWhenReadyAsync(
                socket,
                wsId,
                baseEntityId,
                [.. subscribedEntities],
                apps,
                labelResolutionTask,
                cancellationToken);
        }
    }

    private async Task EmitResolvedAppsWhenReadyAsync(
        System.Net.WebSockets.WebSocket socket,
        string wsId,
        string baseEntityId,
        SubscribedEntity[] subscribedEntities,
        List<string> initialApps,
        Task labelResolutionTask,
        CancellationToken cancellationToken)
    {
        try
        {
            await labelResolutionTask.WaitAsync(cancellationToken);

            var lookup = _entityIdAppsMap.GetAlternateLookup<ReadOnlySpan<char>>();
            if (!lookup.TryGetValue(baseEntityId.AsSpan(), out var resolvedApps)
                || resolvedApps.Count == 0
                || ReferenceEquals(initialApps, resolvedApps))
                return;

            _reportedApps[baseEntityId] = resolvedApps;
            foreach (var sub in subscribedEntities)
            {
                switch (sub.EntityType)
                {
                    case EntityType.MediaPlayer:
                        await EmitMediaPlayerDeltaAsync(socket, wsId, sub.EntityId, PowerState.On, resolvedApps, false, true, cancellationToken);
                        break;
                    case EntityType.Select:
                        await EmitSelectDeltaAsync(socket, wsId, sub.EntityId, PowerState.On, resolvedApps, false, true, cancellationToken);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // expected on disconnect / shutdown
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to emit resolved Android application labels for {EntityId}", baseEntityId);
        }
    }

    private Task EmitMediaPlayerDeltaAsync(
        System.Net.WebSockets.WebSocket socket,
        string wsId,
        string entityId,
        PowerState power,
        List<string>? apps,
        bool powerChanged,
        bool appsChanged,
        CancellationToken cancellationToken)
    {
        if (!powerChanged && !appsChanged)
            return Task.CompletedTask;

        var attrs = new DeltaMediaPlayerStateChangedEventMessageDataAttributes
        {
            State = powerChanged ? MapMediaPlayer(power) : null,
            SourceList = appsChanged && apps is { Count: > 0 }
                ?
                [
                    .. apps,
                    AdbTvRemoteCommands.InputHdmi1,
                    AdbTvRemoteCommands.InputHdmi2,
                    AdbTvRemoteCommands.InputHdmi3,
                    AdbTvRemoteCommands.InputHdmi4
                ]
                : null
        };

        return SendMessageAsync(socket,
            ResponsePayloadHelpers.CreateMediaPlayerStateChangedResponsePayload(attrs, entityId),
            wsId, cancellationToken);
    }

    private Task EmitSelectDeltaAsync(
        System.Net.WebSockets.WebSocket socket,
        string wsId,
        string entityId,
        PowerState power,
        List<string>? apps,
        bool powerChanged,
        bool appsChanged,
        CancellationToken cancellationToken)
    {
        if (!powerChanged && !appsChanged)
            return Task.CompletedTask;

        var attrs = new SelectStateChangedEventMessageDataAttributes
        {
            State = powerChanged ? MapSelect(power) : null,
            Options = appsChanged && apps is { Count: > 0 } ? [.. apps] : null
        };

        return SendMessageAsync(socket,
            ResponsePayloadHelpers.CreateSelectStateChangedPayload(attrs, entityId, AdbTvServerConstants.AppListSelectSuffix),
            wsId, cancellationToken);
    }

    private static State MapMediaPlayer(in PowerState power) => power switch
    {
        PowerState.On => State.On,
        PowerState.Off => State.Off,
        _ => State.Unknown
    };

    private static RemoteState MapRemote(in PowerState power) => power switch
    {
        PowerState.On => RemoteState.On,
        PowerState.Off => RemoteState.Off,
        _ => RemoteState.Unknown
    };

    private static SelectState MapSelect(in PowerState power) => power switch
    {
        PowerState.On or PowerState.Off => SelectState.On,
        _ => SelectState.Unknown
    };

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
                    await WakeOnLan.SendWakeOnLanAsync(adbTvClientHolder.ClientKey.MacAddress, IPAddress.Parse(adbTvClientHolder.ClientKey.IpAddress), _logger);

                await adbTvClientHolder.Connection.SendKeyEventAsync(Enum.Parse<KeyCode>(command), cancellationToken);

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

                var shellResult = await adbTvClientHolder.Connection.ExecuteAsync(command, cancellationToken);
                if (shellResult.IsSuccess)
                    return EntityCommandResult.Other;

                _logger.RawCommandFailed(adbTvClientHolder.ClientKey, command, shellResult.Stderr);
                return EntityCommandResult.Failure;
            case CommandType.App:
                var adbAppLaunchResult = await adbTvClientHolder.Connection.StartAppAsync(command, cancellationToken);
                if (adbAppLaunchResult.IsLaunched)
                    return EntityCommandResult.Other;

                _logger.AppLaunchFailed(adbTvClientHolder.ClientKey, command, adbAppLaunchResult.FailureReason);
                return EntityCommandResult.Failure;
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

    protected override ValueTask<DeviceState> OnGetDeviceStateAsync(GetDeviceStateMsg payload, string wsId, CancellationToken cancellationToken)
        => ValueTask.FromResult(DeviceState.Connected);

    protected override async ValueTask<IReadOnlyCollection<AvailableEntity>> OnGetAvailableEntitiesAsync(GetAvailableEntitiesMsg payload, string wsId, CancellationToken cancellationToken)
        => [.. GetAvailableEntities(await GetEntitiesAsync(wsId, cancellationToken))];

    protected override ValueTask OnSubscribeEventsAsync(System.Net.WebSockets.WebSocket socket, SubscribeEventsMsg payload, string wsId, CancellationTokenWrapper cancellationTokenWrapper,
        CancellationToken commandCancellationToken)
    {
        if (payload.MsgData?.EntityIds is not { Length: > 0 })
            return ValueTask.CompletedTask;

        foreach (var entityId in payload.MsgData.EntityIds)
            cancellationTokenWrapper.AddSubscribedEntity(entityId);

        var groupedEntities = GroupIdentifiers(payload.MsgData.EntityIds);

        // Subscribe response must not block on per-device availability checks. A single offline device can take
        // several seconds to fail. Run initial state emission as fire-and-forget under RequestAborted.
        _ = Parallel.ForEachAsync(groupedEntities,
            new ParallelOptions { CancellationToken = cancellationTokenWrapper.RequestAborted, MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount, 4) },
            async (entityIdGroup, token) =>
            {
                try
                {
                    var subs = entityIdGroup
                        .Select(static eid => new SubscribedEntity(eid, eid.AsSpan().GetEntityTypeFromIdentifier()))
                        .ToHashSet();
                    await UpdateEntityGroupAsync(socket, wsId, entityIdGroup.Key.ToString(), subs, true, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // expected on shutdown
                }
                catch (Exception e)
                {
                    _logger.FailureDuringSubscribeEvents(e, wsId, entityIdGroup.Key.ToString());
                }
            });

        return ValueTask.CompletedTask;
    }

    private static async Task<PowerState> GetPowerState(AdbTvClientHolder adbTvClientHolder, CancellationToken cancellationToken)
    {
        await foreach (string line in adbTvClientHolder.Connection.ExecuteLinesAsync("dumpsys power | grep mWakefulness;", cancellationToken))
        {
            switch (line)
            {
                case var _ when line.Contains("mWakefulness=Asleep", StringComparison.Ordinal):
                case var _ when line.Contains("mWakefulness=Dozing", StringComparison.Ordinal):
                    return PowerState.Off;
                case var _ when line.Contains("mWakefulness=Awake", StringComparison.Ordinal):
                    return PowerState.On;
            }
        }

        return PowerState.Unknown;
    }

    private enum PowerState : byte
    {
        Unknown,
        Off,
        On
    }

    protected override async ValueTask OnUnsubscribeEventsAsync(UnsubscribeEventsMsg payload, string wsId, CancellationTokenWrapper cancellationTokenWrapper)
    {
        var clientKeys = new HashSet<AdbTvClientKey>();
        if (payload.MsgData?.EntityIds is { Length: > 0 })
        {
            foreach (string msgDataEntityId in payload.MsgData.EntityIds)
            {
                cancellationTokenWrapper.RemoveSubscribedEntity(msgDataEntityId);
                var baseId = msgDataEntityId.AsSpan().GetBaseIdentifier();
                _reportedPowerStates.GetAlternateLookup<ReadOnlySpan<char>>().TryRemove(baseId, out _);
                _reportedApps.GetAlternateLookup<ReadOnlySpan<char>>().TryRemove(baseId, out _);

                if (await TryGetAdbTvClientKeyAsync(wsId, msgDataEntityId, cancellationTokenWrapper.ApplicationStopping) is { } adbClientKey)
                    clientKeys.Add(adbClientKey);
            }
        }
        // If no specific device or entity was specified, dispose all clients for this websocket ID.
        else if (payload.MsgData is { DeviceId: null, EntityIds: null })
        {
            cancellationTokenWrapper.RemoveAllSubscribedEntities();
            _reportedPowerStates.Clear();
            _reportedApps.Clear();
        }

        await TryDisconnectAdbClientsAsync(clientKeys, cancellationTokenWrapper.ApplicationStopping);
    }

    protected override async ValueTask<EntityStateChanged[]> OnGetEntityStatesAsync(GetEntityStatesMsg payload, string wsId, CancellationToken cancellationToken)
        => await GetEntitiesAsync(wsId, cancellationToken) is { } entities
            ? [.. AdbTvResponsePayloadHelpers.GetEntityStates(entities.Select(static x => x.EntityId))]
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
            && int.TryParse(portValue, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var parsedPort)
            ? parsedPort
            : 5555;
        var manufacturer = payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.Manufacturer, out var manufacturerValue)
            && Manufacturer.TryParse(manufacturerValue, out var parsedManufacturer)
            ? parsedManufacturer
            : Manufacturer.Android;
        var allowReauth = !payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.AllowReauthKey, out var allowReauthValue)
            || !bool.TryParse(allowReauthValue, out var parsedAllowReauth) || parsedAllowReauth;

        var newConfigurationItem = configurationItem with { Host = ipAddress, Port = port, Manufacturer = manufacturer, AllowReauth = allowReauth };
        var configuration = await _configurationService.GetConfigurationAsync(cancellationToken);
        var maxWaitTime = payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.MaxMessageHandlingWaitTimeInSecondsKey, out var maxWaitTimeValue)
            && double.TryParse(maxWaitTimeValue, NumberStyles.Float, NumberFormatInfo.InvariantInfo, out var parsedMaxWaitTime)
            ? parsedMaxWaitTime
            : 9.5;
        configuration = configuration with { MaxMessageHandlingWaitTimeInSeconds = maxWaitTime };
        configuration.Entities.Remove(configurationItem);
        configuration.Entities.Add(newConfigurationItem);
        await _configurationService.UpdateConfigurationAsync(configuration, cancellationToken);

        var oldKey = new AdbTvClientKey(configurationItem.Host, configurationItem.MacAddress, configurationItem.Port, configurationItem.Manufacturer, configurationItem.AllowReauth, configurationItem.PairedDeviceGuid);
        var newKey = new AdbTvClientKey(newConfigurationItem.Host, newConfigurationItem.MacAddress, newConfigurationItem.Port, newConfigurationItem.Manufacturer, newConfigurationItem.AllowReauth, newConfigurationItem.PairedDeviceGuid);
        if (!oldKey.Equals(newKey))
        {
            RemoteStates.TryRemove(oldKey, out _);
            await _adbTvClientFactory.TryRemoveClientAsync(oldKey);
        }

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
            var backupData = JsonSerializer.Deserialize(jsonRestoreData, AdbJsonSerializerContext.Default.BackupData);
            if (backupData is null)
            {
                _logger.BackupDataNullDuringRestore(wsId);
                return RestoreResult.Failure;
            }

            await _configurationService.UpdateConfigurationAsync(backupData.Configuration, cancellationToken);
            await _adbTvClientFactory.ReplacePrivateKeyAsync(Convert.FromBase64String(backupData.PrivateKey), cancellationToken);
            return RestoreResult.Success;
        }
        catch (Exception e)
        {
            _logger.ExceptionDuringRestore(e, wsId);
            return RestoreResult.Failure;
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
            && int.TryParse(portValue, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var parsedPort)
            ? parsedPort
            : 5555;
        var maxWaitTime = payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.MaxMessageHandlingWaitTimeInSecondsKey, out var maxWaitTimeValue)
            && double.TryParse(maxWaitTimeValue, NumberStyles.Float, NumberFormatInfo.InvariantInfo, out var parsedMaxWaitTime)
            ? parsedMaxWaitTime
            : 9.5;
        var manufacturer = payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.Manufacturer, out var manufacturerValue)
            && Manufacturer.TryParse(manufacturerValue, out var parsedManufacturer)
            ? parsedManufacturer
            : Manufacturer.Android;
        var allowReauth = !payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.AllowReauthKey, out var allowReauthValue)
            || !bool.TryParse(allowReauthValue, out var parsedAllowReauth) || parsedAllowReauth;

        // A non-empty pairing code means the user wants wireless-debugging pairing (Android 11+)
        // instead of the manual on-device approval-dialog flow. Pairing itself is the approval —
        // there's no separate dialog to wait for — so this path skips CheckClientApprovedAsync
        // (which dials the static, likely-stale Port field directly) entirely and instead
        // verifies connectivity through the factory further down, which re-resolves the actual
        // current connect port via mDNS using the GUID pairing just returned.
        string? pairedDeviceGuid = null;
        if (payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.PairingCodeKey, out var pairingCode)
            && !string.IsNullOrWhiteSpace(pairingCode))
        {
            if (!payload.MsgData.InputValues.TryGetValue(AdbTvServerConstants.PairingPortKey, out var pairingPortValue)
                || !int.TryParse(pairingPortValue, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var pairingPort))
            {
                await SendMessageAsync(socket, AdbTvResponsePayloadHelpers.CreateDeviceSetupChangeUserInputResponsePayload(),
                    wsId, cancellationToken);
                return SetupDriverUserDataResult.Handled;
            }

            try
            {
                var authKey = await _adbTvClientFactory.GetOrCreateAuthKey(cancellationToken);
                _logger.PairingCodeSubmitted(wsId, ipAddress, pairingPort);

                // AdbPairing.PairAsync has no internal timeout of its own — it relies entirely on
                // this token. Setup-step messages get an otherwise-unbounded token (canceled only
                // by client disconnect or app shutdown), so an unreachable/stuck device would hang
                // here until the remote's own client gives up and drops the WS connection first,
                // leaving the device in a "half paired" state with no error ever sent back. Bound
                // it ourselves so a stuck pairing fails fast with a real error instead.
                using var pairingTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                pairingTimeoutCts.CancelAfter(PairingTimeout);
                var pairingResult = await AdbPairing.PairAsync(ipAddress, pairingPort, pairingCode, authKey, pairingTimeoutCts.Token);

                pairedDeviceGuid = pairingResult.PeerInfoType == PeerInfoType.AdbDeviceGuid
                    ? Encoding.UTF8.GetString(pairingResult.PeerInfoData)
                    : null;

                // Start the mDNS listener right now, explicitly, rather than leaving it to
                // whichever call happens to resolve this device first (the connectivity check
                // just below would trigger it anyway, implicitly — this just makes the "we now
                // know a paired device exists" moment the explicit trigger instead).
                if (pairedDeviceGuid is not null)
                    await _adbMdnsDiscovery.EnsureStartedAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _logger.PairingFailed(e, wsId);
                await SendMessageAsync(socket, AdbTvResponsePayloadHelpers.CreateDeviceSetupChangeUserInputResponsePayload(),
                    wsId, cancellationToken);
                return SetupDriverUserDataResult.Handled;
            }
        }

        configuration = configuration with { MaxMessageHandlingWaitTimeInSeconds = maxWaitTime };

        var entity = configuration.Entities.FirstOrDefault(x => x.EntityId.Equals(macAddress, StringComparison.OrdinalIgnoreCase));
        AdbTvClientKey? oldKey = null;
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
                Manufacturer = manufacturer,
                AllowReauth = allowReauth,
                PairedDeviceGuid = pairedDeviceGuid
            };
        }
        else
        {
            _logger.UpdatingConfigurationForDevice(macAddress);
            oldKey = new AdbTvClientKey(entity.Host, entity.MacAddress, entity.Port, entity.Manufacturer, entity.AllowReauth, entity.PairedDeviceGuid);
            configuration.Entities.Remove(entity);
            entity = entity with
            {
                Host = ipAddress,
                MacAddress = macAddress,
                Port = port,
                EntityName = entityName,
                AllowReauth = allowReauth,
                PairedDeviceGuid = pairedDeviceGuid ?? entity.PairedDeviceGuid
            };
        }

        configuration.Entities.Add(entity);

        await _configurationService.UpdateConfigurationAsync(configuration, cancellationToken);

        if (oldKey is { } oldKeyValue)
        {
            var newKey = new AdbTvClientKey(entity.Host, entity.MacAddress, entity.Port, entity.Manufacturer, entity.AllowReauth, entity.PairedDeviceGuid);
            if (!oldKeyValue.Equals(newKey))
            {
                await _adbTvClientFactory.TryRemoveClientAsync(oldKeyValue);
                RemoteStates.TryRemove(oldKeyValue, out _);
            }
        }

        if (pairedDeviceGuid is null && !await CheckClientApprovedAsync(wsId, entity.EntityId, cancellationToken))
        {
            await SendMessageAsync(socket, AdbTvResponsePayloadHelpers.CreateDeviceSetupChangeUserInputResponsePayload(),
                wsId, cancellationToken);
            return SetupDriverUserDataResult.Handled;
        }

        return await GetSetupResultForClient(wsId, entity.EntityId, cancellationToken);
    }

    private async ValueTask<SetupDriverUserDataResult> GetSetupResultForClient(string wsId, string entityId, CancellationToken cancellationToken)
    {
        if (await TryGetAdbTvClientHolderAsync(wsId, entityId, cancellationToken) is null)
        {
            _logger.DeviceNotOnlineDuringSetupResult(wsId, entityId);
            return SetupDriverUserDataResult.Error;
        }

        _reportedPowerStates[entityId] = PowerState.On;
        return SetupDriverUserDataResult.Finalized;
    }

    protected override MediaPlayerEntityCommandMsgData<AdbMediaPlayerCommandId>? DeserializeMediaPlayerCommandPayload(JsonDocument jsonDocument)
        => jsonDocument.Deserialize(AdbJsonSerializerContext.Default.MediaPlayerEntityCommandMsgDataAdbMediaPlayerCommandId);

    protected override async ValueTask<string> GetJsonBackupDataAsync(CancellationToken cancellationToken)
    {
        var config = await _configurationService.GetConfigurationAsync(cancellationToken);
        var privateKey = AdbTvClientFactory.GetAdbKeyPath();
        if (!File.Exists(privateKey))
        {
            _logger.AdbPrivateKeyNotFoundForBackup(privateKey);
            throw new FileNotFoundException("No private key found for backup.", privateKey);
        }

        return JsonSerializer.Serialize(new BackupData(config,
                Convert.ToBase64String(await File.ReadAllBytesAsync(privateKey, cancellationToken))),
            AdbJsonSerializerContext.Default.BackupData);
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
            Settings =
            [
                .. settingsPage.Settings.Where(static x =>
                    !x.Id.Equals(AdbTvServerConstants.MacAddressKey, StringComparison.OrdinalIgnoreCase) &&
                    !x.Id.Equals(AdbTvServerConstants.EntityName, StringComparison.OrdinalIgnoreCase))
            ]
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
                            Items =
                            [
                                .. Manufacturer.GetValues().Select(static x => new SettingTypeDropdownItem
                                {
                                    Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = x.ToStringFast(true) }, Value = x.ToStringFast()
                                })
                            ],
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
                    Field = new SettingTypeText
                    {
                        Text = new ValueRegex
                        {
                            RegEx =  AdbTvServerConstants.PortRegex,
                            Value = configurationItem?.Port.ToString(CultureInfo.InvariantCulture) ?? "5555"
                        }
                    },
                    Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = "Enter the ADB port of the TV (mandatory unless using Wireless Debugging)" }
                },
                new Setting
                {
                    Id = AdbTvServerConstants.PairingCodeKey,
                    Field = new SettingTypeText
                    {
                        Text = new ValueRegex
                        {
                            RegEx = AdbTvServerConstants.PairingCodeRegex
                        }
                    },
                    Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["en"] = "Pairing Code"
                    }
                },
                new Setting
                {
                    Id = AdbTvServerConstants.PairingCodeKey + "Label",
                    Field = new SettingTypeLabel
                    {
                        Label = new SettingTypeLabelItem
                        {
                            Value = []
                        }
                    },
                    Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["en"] = "To use Wireless debugging pairing instead of a manual on-device approval, enter the 6-digit " +
                                 "code from Developer Options → Wireless debugging → \"Pair device with pairing code\" (leave empty otherwise)"
                    }
                },
                new Setting
                {
                    Id = AdbTvServerConstants.PairingPortKey,
                    Field = new SettingTypeText
                    {
                        Text = new ValueRegex
                        {
                            RegEx = AdbTvServerConstants.PortRegex
                        }
                    },
                    Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["en"] = "Pairing Port"
                    }
                },
                new Setting
                {
                    Id = AdbTvServerConstants.PairingPortKey + "Label",
                    Field = new SettingTypeLabel
                    {
                        Label = new SettingTypeLabelItem
                        {
                            Value = []
                        }
                    },
                    Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["en"] = "Required only if a pairing code was entered above: the pairing port shown on the same wireless-pairing screen"
                    }
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
                },
                new Setting
                {
                    Id = AdbTvServerConstants.AllowReauthKey,
                    Field = new SettingTypeCheckbox
                    {
                        Checkbox = new SettingTypeCheckboxInner
                        {
                            Value = configurationItem?.AllowReauth ?? false
                        }
                    },
                    Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = "Check to allow auto-prompt on device if pairing is lost or repeatedly fails (disabled requires manual re-setup)" }
                }
            ]
        };
    }

    protected override FrozenSet<EntityType> SupportedEntityTypes { get; } = [EntityType.MediaPlayer, EntityType.Remote, EntityType.Select];
}

file sealed class ReadOnlyMemoryCharComparer : IEqualityComparer<ReadOnlyMemory<char>>
{
    public static readonly ReadOnlyMemoryCharComparer Instance = new();

    public bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
        => x.IsEmpty == y.IsEmpty && x.Length == y.Length && x.Span.Equals(y.Span, StringComparison.Ordinal);

    public int GetHashCode(ReadOnlyMemory<char> obj)
        => HashCode.Combine(obj.IsEmpty, obj.Length, string.GetHashCode(obj.Span, StringComparison.Ordinal));
}