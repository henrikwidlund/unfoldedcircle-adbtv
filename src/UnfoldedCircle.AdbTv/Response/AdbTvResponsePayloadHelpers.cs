using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.Models.Events;
using UnfoldedCircle.Models.Shared;
using UnfoldedCircle.Models.Sync;
using UnfoldedCircle.Server.Extensions;
using UnfoldedCircle.Server.Json;

namespace UnfoldedCircle.AdbTv.Response;

internal static class AdbTvResponsePayloadHelpers
{
    public static byte[] CreateDeviceSetupChangeUserInputResponsePayload() =>
        JsonSerializer.SerializeToUtf8Bytes(new DriverSetupChangeEvent
        {
            Kind = "event",
            Msg = "driver_setup_change",
            Cat = "DEVICE",
            TimeStamp = DateTime.UtcNow,
            MsgData = new DriverSetupChange
            {
                State = DriverSetupChangeState.WaitUserAction,
                EventType = DriverSetupChangeEventType.Setup,
                RequireUserAction = new RequireUserAction
                {
                    Confirmation = new ConfirmationPage
                    {
                        Title = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["en"] = "Confirm ADB Access on your TV"
                        }
                    }
                }
            }
        }, UnfoldedCircleJsonSerializerContext.Default.DriverSetupChangeEvent);

    internal static IEnumerable<EntityStateChanged> GetEntityStates(IEnumerable<string> entityIds)
    {
        foreach (var entityId in entityIds)
        {
            yield return new RemoteEntityStateChanged
            {
                EntityId = entityId.GetIdentifier(EntityType.Remote),
                EntityType = EntityType.Remote,
                Attributes = [RemoteEntityAttribute.State]
            };

            yield return new SelectEntityStateChanged
            {
                EntityId = entityId.GetIdentifier(EntityType.Select, AdbTvServerConstants.AppListSelectSuffix),
                EntityType = EntityType.Select,
                Attributes = [SelectEntityAttribute.State]
            };

            yield return new MediaPlayerEntityStateChanged
            {
                EntityId = entityId.GetIdentifier(EntityType.MediaPlayer),
                EntityType = EntityType.MediaPlayer,
                Attributes = [MediaPlayerEntityAttribute.SourceList, MediaPlayerEntityAttribute.State]
            };
        }
    }
}
