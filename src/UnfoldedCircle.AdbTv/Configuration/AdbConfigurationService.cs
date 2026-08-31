using System.Text.Json.Serialization.Metadata;

using UnfoldedCircle.AdbTv.Json;
using UnfoldedCircle.Server.Configuration;

namespace UnfoldedCircle.AdbTv.Configuration;

internal sealed class AdbConfigurationService(IConfiguration configuration)
    : ConfigurationService<AdbGlobalConfiguration, AdbConfigurationItem>(configuration),
        IConfigurationService<AdbGlobalConfiguration, AdbConfigurationItem>
{
    protected override JsonTypeInfo<UnfoldedCircleConfiguration<AdbGlobalConfiguration, AdbConfigurationItem>> GetSerializer()
        => AdbJsonSerializerContext.Default.UnfoldedCircleConfigurationAdbGlobalConfigurationAdbConfigurationItem;

    async Task<UnfoldedCircleConfiguration<AdbGlobalConfiguration, AdbConfigurationItem>> IConfigurationService<AdbGlobalConfiguration, AdbConfigurationItem>.GetConfigurationAsync(
        CancellationToken cancellationToken)
    {
        var loadedConfiguration = await GetConfigurationAsync(cancellationToken);
        if (loadedConfiguration.GlobalConfiguration.PollingIntervalSeconds is not null)
            return loadedConfiguration;

        // Deserializing an entities file that predates PollingIntervalSeconds leaves it null; persist the default once so it's on disk going forward.
        var migratedConfiguration = loadedConfiguration with
        {
            GlobalConfiguration = loadedConfiguration.GlobalConfiguration with { PollingIntervalSeconds = 5 }
        };
        return await UpdateConfigurationAsync(migratedConfiguration, cancellationToken);
    }
}
