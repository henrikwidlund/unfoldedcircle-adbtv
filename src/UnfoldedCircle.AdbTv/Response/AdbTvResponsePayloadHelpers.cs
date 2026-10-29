using UnfoldedCircle.Models.Events;
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
}
