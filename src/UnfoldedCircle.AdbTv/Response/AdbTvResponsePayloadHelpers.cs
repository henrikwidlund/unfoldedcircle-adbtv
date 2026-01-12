using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.Configuration;
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
                    Input = new SettingsPage
                    {
                        Title = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["en"] = "Confirm ADB Access on your TV or enter the pairing code shown on your TV"
                        },
                        Settings = [
                            new Setting
                            {
                                Field = new SettingTypeNumber
                                {
                                    Number = new SettingTypeNumberInner
                                    {
                                        Decimals = 0,
                                        Max = 999999,
                                        Min = 0,
                                        Steps = 1,
                                        Value = 0
                                    }
                                },
                                Id = AdbTvServerConstants.PairingCode,
                                Label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["en"] = "Enter the pairing code shown on your TV"
                                }
                            }
                        ]
                    }
                }
            }
        }, UnfoldedCircleJsonSerializerContext.Default.DriverSetupChangeEvent);

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