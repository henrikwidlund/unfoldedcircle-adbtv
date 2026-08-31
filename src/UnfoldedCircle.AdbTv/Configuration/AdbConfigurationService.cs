using System.Text.Json.Serialization.Metadata;

using UnfoldedCircle.AdbTv.Json;
using UnfoldedCircle.Server.Configuration;

namespace UnfoldedCircle.AdbTv.Configuration;

internal sealed class AdbConfigurationService(IConfiguration configuration)
    : ConfigurationService<AdbGlobalConfiguration, AdbConfigurationItem>(configuration)
{
    protected override JsonTypeInfo<UnfoldedCircleConfiguration<AdbGlobalConfiguration, AdbConfigurationItem>> GetSerializer()
        => AdbJsonSerializerContext.Default.UnfoldedCircleConfigurationAdbGlobalConfigurationAdbConfigurationItem;
}
