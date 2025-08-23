using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.Server.Configuration;

namespace UnfoldedCircle.AdbTv.Json;

[JsonSerializable(typeof(UnfoldedCircleConfiguration<AdbConfigurationItem>))]
internal sealed partial class AdbJsonSerializerContext : JsonSerializerContext
{
    static AdbJsonSerializerContext()
    {
        Default = new AdbJsonSerializerContext(new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
    }
}