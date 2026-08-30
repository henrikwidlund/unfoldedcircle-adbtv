using System.Collections.Concurrent;

using Makaretu.Dns;

using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.AdbTv.Logging;
using UnfoldedCircle.Server.Configuration;

namespace UnfoldedCircle.AdbTv.Discovery;

/// <summary>
/// Resolves the current host:port of a paired Android device's wireless-debugging connect
/// service (<c>_adb-tls-connect._tcp</c>) via mDNS. The port adbd advertises here changes across
/// reboots/toggles of wireless debugging, so this is called before each connect attempt rather
/// than once during setup. The device's mDNS instance name for this service is the same device
/// GUID returned during pairing (<see cref="Theodicean.SharpAdb.Pairing.PeerInfoType.AdbDeviceGuid"/>) —
/// matching is a direct string comparison against that instance name, same as real adb.
/// </summary>
/// <remarks>
/// adbd registers this service with the on-device system <c>mdnsd</c> via <c>DNSServiceRegister</c>
/// (confirmed in AOSP's <c>daemon/mdns.cpp</c>) — a real Bonjour/mDNSResponder registration, which
/// per RFC 6762 §8.3 sends unsolicited announcements whenever the service (re)starts, not just in
/// response to queries. Once running, this catches those passively into <see cref="_cache"/>: a
/// device that just rebooted is often already known here with zero network round-trip, before
/// anything ever calls <see cref="TryResolveAsync"/>.
/// <para>
/// The multicast listener doesn't start unconditionally at process startup — a setup with only
/// manually-configured (non-paired) devices never binds a multicast socket or spends CPU decoding
/// the LAN's mDNS chatter at all. It starts, via <see cref="EnsureStartedAsync"/>, at whichever of
/// these happens first: (1) <see cref="StartAsync"/> finds an already-configured paired device at
/// process boot — so the cache is warm from the start rather than only from the first real need;
/// (2) a pairing attempt succeeds during setup, explicitly, right when we first know a device
/// needs it, rather than waiting on whatever happens to call <see cref="TryResolveAsync"/> next;
/// or (3), as a fallback covering both of the above being skipped somehow, the first
/// <see cref="TryResolveAsync"/> call itself. Once started it stays started for the process's
/// lifetime — not worth tearing down and restarting as devices are paired/unpaired over time.
/// </para>
/// <para>
/// There is a single, permanent event subscription (from the start) rather than a per-call
/// subscribe/unsubscribe: since every discovered instance — regardless of which GUID — already
/// lands in <see cref="_cache"/>, an active on-demand resolve for a specific GUID doesn't need its
/// own listener; it just polls the same cache while prompting fresh responses via repeated
/// queries. <see cref="ConcurrentDictionary{TKey,TValue}"/> also means concurrent resolves need no
/// external locking (the one-time start is the only thing a lock guards below).
/// </para>
/// </remarks>
public sealed class AdbMdnsDiscovery(ILogger<AdbMdnsDiscovery> logger, IConfigurationService<AdbGlobalConfiguration, AdbConfigurationItem> configurationService)
    : IHostedService, IAsyncDisposable
{
    private static readonly DomainName AdbTlsConnectServiceName = new("_adb-tls-connect._tcp");
    private static readonly TimeSpan ResolveTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetransmitInterval = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private const int MaxRetransmits = 4;

    private readonly ConcurrentDictionary<string, (string Host, int Port)> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private ServiceDiscovery? _serviceDiscovery;

    // If any already-configured device is wirelessly paired, start listening immediately so the
    // passive cache is warm from process start — not just from the first actual resolve, which
    // would otherwise miss any re-announcement a device sends between startup and that first call.
    // Setups with no paired devices yet stay fully idle until PairAsync succeeds for one (see
    // EnsureStartedAsync's other caller in AdbWebSocketHandler's pairing flow).
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetConfigurationAsync(cancellationToken);
        if (configuration.Entities.Any(static entity => entity.PairedDeviceGuid is not null))
            await EnsureStartedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_serviceDiscovery is { } serviceDiscovery)
            serviceDiscovery.ServiceInstanceDiscovered -= OnServiceInstanceDiscovered;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts the multicast listener if it isn't already running. A no-op if it is. Called
    /// eagerly from <see cref="StartAsync"/> when a paired device is already configured, and
    /// explicitly right after a successful pairing during setup — both are just making an
    /// otherwise-implicit lazy-start (the first <see cref="TryResolveAsync"/> call) happen sooner.
    /// </summary>
    internal async ValueTask<ServiceDiscovery> EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_serviceDiscovery is { } existing)
            return existing;

        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_serviceDiscovery is { } startedWhileWaiting)
                return startedWhileWaiting;

            var serviceDiscovery = await ServiceDiscovery.CreateInstance(cancellationToken: cancellationToken);
            serviceDiscovery.ServiceInstanceDiscovered += OnServiceInstanceDiscovered;

            // Prime the cache with whatever's already advertising right now — otherwise we'd only
            // learn about an already-running device from its *next* unsolicited re-announcement,
            // which mdnsd sends periodically but not necessarily soon after we start listening.
            try
            {
                await serviceDiscovery.QueryServiceInstances(AdbTlsConnectServiceName);
            }
            catch (Exception e)
            {
                logger.MdnsListenerStartupQueryFailed(e);
            }

            _serviceDiscovery = serviceDiscovery;
            return serviceDiscovery;
        }
        finally
        {
            _startLock.Release();
        }
    }

    private Task OnServiceInstanceDiscovered(ServiceInstanceDiscoveryEventArgs args)
    {
        if (args.ServiceInstanceName is { Labels.Count: > 0 } instanceName
            && TryExtractEndpoint(args.Message, instanceName, out var host, out var port))
        {
            _cache[instanceName.Labels[0]] = (host, port);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves <paramref name="deviceGuid"/>'s current <c>_adb-tls-connect._tcp</c> host:port.
    /// Returns <see langword="null"/> on timeout, if the device isn't currently advertising the
    /// service (e.g. wireless debugging toggled off), or if multicast is unavailable on this
    /// network — callers should fall back to the last-known host:port in that case.
    /// </summary>
    public async ValueTask<(string Host, int Port)?> TryResolveAsync(string deviceGuid, CancellationToken cancellationToken)
    {
        // Fast path: already known from passive listening (a prior resolve, or an unsolicited
        // announcement the listener caught on its own) — no network round-trip at all.
        if (_cache.TryGetValue(deviceGuid, out var cached))
            return cached;

        var serviceDiscovery = await EnsureStartedAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ResolveTimeout);

        try
        {
            // mDNS queries travel over unreliable UDP multicast — a single query can simply be
            // lost in transit. Retransmitting the identical query a few times (standard mDNS
            // querier behavior, RFC 6762 §8.1) trades a little latency for materially better odds
            // of getting an answer instead of falling back to a possibly-stale configured address.
            for (var attempt = 0; attempt < MaxRetransmits; attempt++)
            {
                await serviceDiscovery.QueryServiceInstances(AdbTlsConnectServiceName);

                var retransmitDeadline = DateTimeOffset.UtcNow + RetransmitInterval;
                while (DateTimeOffset.UtcNow < retransmitDeadline)
                {
                    if (_cache.TryGetValue(deviceGuid, out cached))
                        return cached;

                    await Task.Delay(PollInterval, timeoutCts.Token);
                }
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // Overall ResolveTimeout elapsed while polling; fall through to the final check below
            // in case an answer landed in the cache in the same instant the timeout fired.
        }
        catch (Exception e)
        {
            logger.MdnsResolveFailed(e, deviceGuid);
            return null;
        }

        return _cache.TryGetValue(deviceGuid, out cached) ? cached : null;
    }

    // Assumes the SRV/address records are bundled as additional records alongside the PTR that
    // triggers ServiceInstanceDiscovered, per RFC 6763's recommended responder behavior — the
    // common case, but unverified against a real device (see AdbMdnsDiscovery's usage sites for
    // the fallback-to-last-known-address path this depends on).
    private static bool TryExtractEndpoint(Message message, DomainName instanceName, out string host, out int port)
    {
        host = "";
        port = 0;

        var srvRecord = message.Answers.Concat(message.AdditionalRecords)
            .OfType<SRVRecord>()
            .FirstOrDefault(record => instanceName.Equals(record.Name));
        if (srvRecord?.Target is null)
            return false;

        var addressRecord = message.Answers.Concat(message.AdditionalRecords)
            .OfType<AddressRecord>()
            .FirstOrDefault(record => srvRecord.Target.Equals(record.Name));
        if (addressRecord?.Address is null)
            return false;

        host = addressRecord.Address.ToString();
        port = srvRecord.Port;
        return true;
    }

    public ValueTask DisposeAsync()
    {
        _startLock.Dispose();
        _serviceDiscovery?.Dispose();
        return ValueTask.CompletedTask;
    }
}
