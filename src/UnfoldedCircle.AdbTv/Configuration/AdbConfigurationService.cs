using System.Text.Json.Serialization.Metadata;

using UnfoldedCircle.AdbTv.Json;
using UnfoldedCircle.Server.Configuration;

namespace UnfoldedCircle.AdbTv.Configuration;

internal sealed class AdbConfigurationService(IConfiguration configuration)
    : ConfigurationService<AdbConfigurationItem>(configuration)
{
    protected override JsonTypeInfo<UnfoldedCircleConfiguration<AdbConfigurationItem>> GetSerializer()
        => AdbJsonSerializerContext.Default.UnfoldedCircleConfigurationAdbConfigurationItem;
}