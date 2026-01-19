using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.Models.Sync;

namespace UnfoldedCircle.AdbTv.WebSocket;

internal sealed partial class AdbWebSocketHandler
{
    private static (string Command, CommandType CommandType) GetMappedCommand(string? command, in Manufacturer? manufacturer)
    {
        if (string.IsNullOrEmpty(command))
            return (string.Empty, CommandType.Unknown);

        var localManufacturer = manufacturer ?? Manufacturer.Android;
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
            _ when command.Equals(AdbTvRemoteCommands.InputHdmi1, StringComparison.OrdinalIgnoreCase) => GetHdmiCommand(HdmiPort.Hdmi1, localManufacturer),
            _ when command.Equals(AdbTvRemoteCommands.InputHdmi2, StringComparison.OrdinalIgnoreCase) => GetHdmiCommand(HdmiPort.Hdmi2, localManufacturer),
            _ when command.Equals(AdbTvRemoteCommands.InputHdmi3, StringComparison.OrdinalIgnoreCase) => GetHdmiCommand(HdmiPort.Hdmi3, localManufacturer),
            _ when command.Equals(AdbTvRemoteCommands.InputHdmi4, StringComparison.OrdinalIgnoreCase) => GetHdmiCommand(HdmiPort.Hdmi4, localManufacturer),
            _ when command.Equals(AdbTvRemoteCommands.AudioTvSpeakers, StringComparison.OrdinalIgnoreCase) => (AdbAdvancedCommands.AudioTvSpeakers, CommandType.Raw),
            _ when command.Equals(AdbTvRemoteCommands.AudioExternalDevice, StringComparison.OrdinalIgnoreCase) => (AdbAdvancedCommands.AudioExternalDevice, CommandType.Raw),
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
                _ when command.StartsWith("INP_TCL:", StringComparison.OrdinalIgnoreCase) => (
                    $"am start -a android.intent.action.VIEW -d content://android.media.tv/passthrough/com.tcl.tvinput%2F.TvPassThroughService%2FHW{command[8..]} -f 0x10000000",
                    CommandType.Raw),
                _ when AppNames.AppNamesMap.TryGetValue(command, out var appName) => (appName, CommandType.App),
                _ => (command, CommandType.Unknown)
            };
        }

        static (string Command, CommandType CommandType) GetHdmiCommand(in HdmiPort hdmiPort, in Manufacturer manufacturer)
        {
            var portNumber = hdmiPort switch
            {
                HdmiPort.Hdmi1 when manufacturer is Manufacturer.Hisense => "2",
                HdmiPort.Hdmi1 when manufacturer is Manufacturer.Philips => "5",
                HdmiPort.Hdmi1 when manufacturer is Manufacturer.PhilipsAlternate => "2",
                HdmiPort.Hdmi1 when manufacturer is Manufacturer.Sony => "2",
                HdmiPort.Hdmi1 when manufacturer is Manufacturer.Tcl => "15",
                HdmiPort.Hdmi1 => AdbTvConstants.Hdmi1,
                HdmiPort.Hdmi2 when manufacturer is Manufacturer.Hisense => "3",
                HdmiPort.Hdmi2 when manufacturer is Manufacturer.Philips => "6",
                HdmiPort.Hdmi2 when manufacturer is Manufacturer.PhilipsAlternate => "3",
                HdmiPort.Hdmi2 when manufacturer is Manufacturer.Sony => "3",
                HdmiPort.Hdmi2 when manufacturer is Manufacturer.Tcl => "16",
                HdmiPort.Hdmi2 => AdbTvConstants.Hdmi2,
                HdmiPort.Hdmi3 when manufacturer is Manufacturer.Hisense => "4",
                HdmiPort.Hdmi3 when manufacturer is Manufacturer.Philips => "7",
                HdmiPort.Hdmi3 when manufacturer is Manufacturer.PhilipsAlternate => "4",
                HdmiPort.Hdmi3 when manufacturer is Manufacturer.Sony => "4",
                HdmiPort.Hdmi3 when manufacturer is Manufacturer.Tcl => "17",
                HdmiPort.Hdmi3 => AdbTvConstants.Hdmi3,
                HdmiPort.Hdmi4 when manufacturer is Manufacturer.Hisense => "5",
                HdmiPort.Hdmi4 when manufacturer is Manufacturer.Philips => "8",
                HdmiPort.Hdmi4 when manufacturer is Manufacturer.PhilipsAlternate => "5",
                HdmiPort.Hdmi4 when manufacturer is Manufacturer.Sony => "5",
                HdmiPort.Hdmi4 when manufacturer is Manufacturer.Tcl => "18",
                HdmiPort.Hdmi4 => AdbTvConstants.Hdmi4,
                _ => throw new ArgumentOutOfRangeException(nameof(hdmiPort), hdmiPort, null)
            };
            return manufacturer switch
            {
                Manufacturer.Hisense => (AdbAdvancedCommands.HisenseHdmi.Replace(AdbAdvancedCommands.PortNumberPlaceholder, portNumber, StringComparison.Ordinal), CommandType.Raw),
                Manufacturer.Philips => (AdbAdvancedCommands.PhilipsHdmi.Replace(AdbAdvancedCommands.PortNumberPlaceholder, portNumber, StringComparison.Ordinal), CommandType.Raw),
                Manufacturer.PhilipsAlternate => (AdbAdvancedCommands.PhilipsAlternateHdmi.Replace(AdbAdvancedCommands.PortNumberPlaceholder, portNumber, StringComparison.Ordinal), CommandType.Raw),
                Manufacturer.Sony => (AdbAdvancedCommands.SonyHdmi.Replace(AdbAdvancedCommands.PortNumberPlaceholder, portNumber, StringComparison.Ordinal), CommandType.Raw),
                Manufacturer.Tcl => (AdbAdvancedCommands.TclHdmi.Replace(AdbAdvancedCommands.PortNumberPlaceholder, portNumber, StringComparison.Ordinal), CommandType.Raw),
                _ => (portNumber, CommandType.KeyEvent)
            };
        }
    }

    private enum HdmiPort : sbyte
    {
        Hdmi1,
        Hdmi2,
        Hdmi3,
        Hdmi4
    }

    private enum CommandType : sbyte
    {
        KeyEvent,
        Raw,
        App,
        Unknown
    }
}