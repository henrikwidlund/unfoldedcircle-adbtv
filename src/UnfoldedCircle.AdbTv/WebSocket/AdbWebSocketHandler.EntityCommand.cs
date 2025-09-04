using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.Models.Sync;

namespace UnfoldedCircle.AdbTv.WebSocket;

internal sealed partial class AdbWebSocketHandler
{
    private static (string Command, CommandType CommandType) GetMappedCommand(string? command)
    {
        if (string.IsNullOrEmpty(command))
            return (string.Empty, CommandType.Unknown);

        return command switch
        {
            _ when command.Equals(RemoteButtonConstants.On, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Wakeup, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.Off, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Sleep, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.Toggle, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Power, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.VolumeUp, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.VolumeUp, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.VolumeDown, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.VolumeDown, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.Mute, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Mute, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.ChannelUp, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.ChannelUp, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.ChannelDown, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.ChannelDown, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.DpadUp, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.DpadUp, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.DpadDown, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.DpadDown, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.DpadLeft, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.DpadLeft, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.DpadRight, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.DpadRight, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.DpadMiddle, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.DpadCenter, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.Home, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Home, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.Back, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Back, CommandType.KeyEvent),
            _ when command.Equals(RemoteButtonConstants.Menu, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Settings, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.Digit0, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Key0, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.Digit1, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Key1, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.Digit2, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Key2, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.Digit3, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Key3, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.Digit4, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Key4, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.Digit5, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Key5, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.Digit6, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Key6, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.Digit7, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Key7, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.Digit8, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Key8, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.Digit9, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Key9, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.Info, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Info, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.Settings, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Settings, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.InputHdmi1, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Hdmi1, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.InputHdmi2, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Hdmi2, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.InputHdmi3, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Hdmi3, CommandType.KeyEvent),
            _ when command.Equals(AdbTvRemoteCommands.InputHdmi4, StringComparison.OrdinalIgnoreCase) => (AdbTvConstants.Hdmi4, CommandType.KeyEvent),
            _ => GetRawCommand(command)
        };

        static (string Command, CommandType CommandType) GetRawCommand(string command)
        {
            return command switch
            {
                _ when command.StartsWith("RAW:", StringComparison.OrdinalIgnoreCase) => (command[4..], CommandType.Raw),
                _ when command.StartsWith("APP:", StringComparison.OrdinalIgnoreCase) => ($"monkey --pct-syskeys 0 -p {command[4..]} 1", CommandType.Raw),
                _ when command.StartsWith("ACT:", StringComparison.OrdinalIgnoreCase) => ($"am start -n {command[4..]}", CommandType.Raw),
                _ when command.StartsWith("INP:", StringComparison.OrdinalIgnoreCase) => (
                    $"am start -a android.intent.action.VIEW -d content://android.media.tv/passthrough/com.mediatek.tvinput%2F.hdmi.HDMIInputService%2FHW{command[4..]} -n org.droidtv.playtv/.PlayTvActivity -f 0x10000000",
                    CommandType.Raw),
                _ when AppNames.AppNamesMap.TryGetValue(command, out var appName) => (appName, CommandType.App),
                _ => (command, CommandType.Unknown)
            };
        }
    }

    private enum CommandType
    {
        KeyEvent,
        Raw,
        App,
        Unknown
    }
}