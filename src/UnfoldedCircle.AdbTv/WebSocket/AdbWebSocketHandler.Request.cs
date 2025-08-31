using System.Collections.Concurrent;

using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.Models.Events;
using UnfoldedCircle.Models.Shared;
using UnfoldedCircle.Models.Sync;
using UnfoldedCircle.Server.Extensions;

namespace UnfoldedCircle.AdbTv.WebSocket;

internal sealed partial class AdbWebSocketHandler
{
    private static readonly ConcurrentDictionary<AdbTvClientKey, RemoteState> RemoteStates = new();

    private static readonly RemoteOptions RemoteOptions = new()
    {
        ButtonMapping =
        [
            new DeviceButtonMapping { Button = RemoteButton.Home, ShortPress = new EntityCommand { CmdId = RemoteButtonConstants.Home } },
            new DeviceButtonMapping { Button = RemoteButton.Back, ShortPress = new EntityCommand { CmdId = RemoteButtonConstants.Back } },
            new DeviceButtonMapping { Button = RemoteButton.DpadDown, ShortPress = new EntityCommand { CmdId = RemoteButtonConstants.DpadDown } },
            new DeviceButtonMapping { Button = RemoteButton.DpadUp, ShortPress = new EntityCommand { CmdId = RemoteButtonConstants.DpadUp } },
            new DeviceButtonMapping { Button = RemoteButton.DpadLeft, ShortPress = new EntityCommand { CmdId = RemoteButtonConstants.DpadLeft } },
            new DeviceButtonMapping { Button = RemoteButton.ChannelUp, ShortPress = new EntityCommand { CmdId = RemoteButtonConstants.ChannelUp } },
            new DeviceButtonMapping { Button = RemoteButton.ChannelDown, ShortPress = new EntityCommand { CmdId = RemoteButtonConstants.ChannelDown } },
            new DeviceButtonMapping { Button = RemoteButton.DpadRight, ShortPress = new EntityCommand { CmdId = RemoteButtonConstants.DpadRight } },
            new DeviceButtonMapping { Button = RemoteButton.DpadMiddle, ShortPress = new EntityCommand { CmdId = RemoteButtonConstants.DpadMiddle } },
            new DeviceButtonMapping { Button = RemoteButton.VolumeUp, ShortPress = new EntityCommand { CmdId = RemoteButtonConstants.VolumeUp } },
            new DeviceButtonMapping { Button = RemoteButton.VolumeDown, ShortPress = new EntityCommand { CmdId = RemoteButtonConstants.VolumeDown } },
            new DeviceButtonMapping { Button = RemoteButton.Power, ShortPress = new EntityCommand { CmdId = RemoteButtonConstants.Toggle } },
            new DeviceButtonMapping { Button = RemoteButton.Mute, ShortPress = new EntityCommand { CmdId = RemoteButtonConstants.Mute } }
        ],
        SimpleCommands =
        [
            RemoteButtonConstants.Home, RemoteButtonConstants.Back, AdbTvRemoteCommands.Digit0,
            AdbTvRemoteCommands.Digit1, AdbTvRemoteCommands.Digit2, AdbTvRemoteCommands.Digit3,
            AdbTvRemoteCommands.Digit4, AdbTvRemoteCommands.Digit5, AdbTvRemoteCommands.Digit6,
            AdbTvRemoteCommands.Digit7, AdbTvRemoteCommands.Digit8, AdbTvRemoteCommands.Digit9,
            RemoteButtonConstants.DpadUp, RemoteButtonConstants.DpadDown, RemoteButtonConstants.DpadLeft,
            RemoteButtonConstants.DpadRight, RemoteButtonConstants.DpadMiddle, RemoteButtonConstants.VolumeUp,
            RemoteButtonConstants.VolumeDown, RemoteButtonConstants.Mute, AdbTvRemoteCommands.Info,
            RemoteButtonConstants.ChannelUp, RemoteButtonConstants.ChannelDown, AdbTvRemoteCommands.Settings,
            AdbTvRemoteCommands.InputHdmi1, AdbTvRemoteCommands.InputHdmi2, AdbTvRemoteCommands.InputHdmi3,
            AdbTvRemoteCommands.InputHdmi4, ..AppNames.SupportedApps
        ],
        UserInterface = new UserInterface
        {
            Pages =
            [
                new UserInterfacePage
                {
                    PageId = "uc_adbtv_general",
                    Name = "General",
                    Grid = new Grid { Height = 4, Width = 2 },
                    Items =
                    [
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "HDMI 1",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.InputHdmi1 },
                            Location = new GridLocation { X = 0, Y = 0 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "HDMI 2",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.InputHdmi2 },
                            Location = new GridLocation { X = 1, Y = 0 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "HDMI 3",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.InputHdmi3 },
                            Location = new GridLocation { X = 0, Y = 1 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "HDMI 4",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.InputHdmi4 },
                            Location = new GridLocation { X = 1, Y = 1 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "Info",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.Info },
                            Location = new GridLocation { X = 0, Y = 3 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "Settings",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.Settings },
                            Location = new GridLocation { X = 1, Y = 3 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        }
                    ]
                },
                new UserInterfacePage
                {
                    PageId = "uc_adbtv_numpad",
                    Name = "Numpad",
                    Grid = new Grid { Height = 4, Width = 3 },
                    Items =
                    [
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "1",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.Digit1 },
                            Location = new GridLocation { X = 0, Y = 0 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "2",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.Digit2 },
                            Location = new GridLocation { X = 1, Y = 0 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "3",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.Digit3 },
                            Location = new GridLocation { X = 2, Y = 0 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },

                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "4",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.Digit4 },
                            Location = new GridLocation { X = 0, Y = 1 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "5",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.Digit5 },
                            Location = new GridLocation { X = 1, Y = 1 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "6",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.Digit6 },
                            Location = new GridLocation { X = 2, Y = 1 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },

                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "7",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.Digit7 },
                            Location = new GridLocation { X = 0, Y = 2 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "8",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.Digit8 },
                            Location = new GridLocation { X = 1, Y = 2 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "9",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.Digit9 },
                            Location = new GridLocation { X = 2, Y = 2 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },
                        new UserInterfaceItem
                        {
                            Type = UserInterfaceItemType.Text,
                            Text = "0",
                            Command = new EntityCommand { CmdId = AdbTvRemoteCommands.Digit0 },
                            Location = new GridLocation { X = 1, Y = 3 },
                            Size = new GridItemSize { Height = 1, Width = 1 }
                        },
                    ]
                }
            ]
        }
    };

    private static IEnumerable<AvailableEntity> GetAvailableEntities(
        List<AdbConfigurationItem>? entities,
        GetAvailableEntitiesMsg payload)
    {
        if (entities is not { Count: > 0 })
            yield break;

        var hasDeviceIdFilter = !string.IsNullOrEmpty(payload.MsgData.Filter?.DeviceId);
        foreach (var adbConfigurationItem in entities)
        {
            if (hasDeviceIdFilter)
            {
                var configDeviceId = adbConfigurationItem.DeviceId?.AsMemory();
                // we have a device id filter, so if the config device id is null, there is no match
                if (configDeviceId is null)
                    continue;
                if (!configDeviceId.Value.Span.Equals(payload.MsgData.Filter!.DeviceId.AsSpan().GetBaseIdentifier(), StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            yield return new RemoteAvailableEntity
            {
                EntityId = adbConfigurationItem.EntityId.GetIdentifier(EntityType.Remote),
                EntityType = EntityType.Remote,
                Name = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = adbConfigurationItem.EntityName },
                DeviceId = adbConfigurationItem.DeviceId.GetNullableIdentifier(EntityType.Remote),
                Features = AdbTvEntitySettings.RemoteFeatures,
                Options = RemoteOptions
            };
        }
    }
}