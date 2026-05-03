using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.Models.Sync;
using UnfoldedCircle.Server.Configuration;

namespace UnfoldedCircle.AdbTv.Json;

[JsonSerializable(typeof(UnfoldedCircleConfiguration<AdbConfigurationItem>))]
[JsonSerializable(typeof(BackupData))]
[JsonSerializable(typeof(MediaPlayerEntityCommandMsgData<AdbMediaPlayerCommandId>))]
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
