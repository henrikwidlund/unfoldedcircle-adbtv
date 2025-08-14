using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.AdbTv.Json;
using UnfoldedCircle.Models.Events;
using UnfoldedCircle.Models.Shared;
using UnfoldedCircle.Models.Sync;
using UnfoldedCircle.Server.Extensions;

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
        }, AdbJsonSerializerContext.Default.DriverSetupChangeEvent);

    internal static IEnumerable<EntityStateChanged> GetEntityStates(IEnumerable<EntityIdDeviceId> entityIdDeviceIds)
    {
        foreach (var entityIdDeviceId in entityIdDeviceIds)
        {
            yield return new RemoteEntityStateChanged
            {
                EntityId = entityIdDeviceId.EntityId.GetIdentifier(EntityType.Remote),
                EntityType = EntityType.Remote,
                Attributes = [RemoteEntityAttribute.State],
                DeviceId = entityIdDeviceId.DeviceId.GetNullableIdentifier(EntityType.Remote)
            };
        }
    }
}