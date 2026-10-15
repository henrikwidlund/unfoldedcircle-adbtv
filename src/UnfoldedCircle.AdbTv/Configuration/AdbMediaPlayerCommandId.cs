using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.Models;
using UnfoldedCircle.Models.Sync;

namespace UnfoldedCircle.AdbTv.Configuration;

[JsonConverter(typeof(StrictEnumConverter<AdbMediaPlayerCommandId>))]
public enum AdbMediaPlayerCommandId : byte
{
    /// <summary>
    /// Switch on media player.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.On)]
    On = 1,

    /// <summary>
    /// Switch off media player.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Off)]
    Off,

    /// <summary>
    /// Toggle the current power state, either from on -> off or from off -> on.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Toggle)]
    Toggle,

    /// <summary>
    /// Increase volume.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.VolumeUp)]
    VolumeUp,

    /// <summary>
    /// Decrease volume.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.VolumeDown)]
    VolumeDown,

    /// <summary>
    /// Toggle mute state.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.MuteToggle)]
    MuteToggle,

    /// <summary>
    /// Channel up.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.ChannelUp)]
    ChannelUp,

    /// <summary>
    /// Channel down.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.ChannelDown)]
    ChannelDown,

    /// <summary>
    /// Directional pad up.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.CursorUp)]
    CursorUp,

    /// <summary>
    /// Directional pad down.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.CursorDown)]
    CursorDown,

    /// <summary>
    /// Directional pad left.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.CursorLeft)]
    CursorLeft,

    /// <summary>
    /// Directional pad right.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.CursorRight)]
    CursorRight,

    /// <summary>
    /// Directional pad enter.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.CursorEnter)]
    CursorEnter,

    /// <summary>
    /// Number pad digit 0.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Digit0)]
    Digit0,

    /// <summary>
    /// Number pad digit 1.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Digit1)]
    Digit1,

    /// <summary>
    /// Number pad digit 2.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Digit2)]
    Digit2,

    /// <summary>
    /// Number pad digit 3.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Digit3)]
    Digit3,

    /// <summary>
    /// Number pad digit 4.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Digit4)]
    Digit4,

    /// <summary>
    /// Number pad digit 5.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Digit5)]
    Digit5,

    /// <summary>
    /// Number pad digit 6.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Digit6)]
    Digit6,

    /// <summary>
    /// Number pad digit 7.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Digit7)]
    Digit7,

    /// <summary>
    /// Number pad digit 8.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Digit8)]
    Digit8,

    /// <summary>
    /// Number pad digit 9.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Digit9)]
    Digit9,

    /// <summary>
    /// Home menu
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Home)]
    Home,

    /// <summary>
    /// Information menu / what's playing.
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Info)]
    Info,

    /// <summary>
    /// Back / exit function for menu navigation (to exit menu, guide, info).
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Back)]
    Back,

    /// <summary>
    /// Select an input source from the available sources.
    /// </summary>
    /// <remarks>Parameters: source</remarks>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.SelectSource)]
    SelectSource,

    /// <summary>
    /// Settings menu
    /// </summary>
    [JsonStringEnumMemberName(MediaPlayerCommandIdConstants.Settings)]
    Settings,

    [JsonStringEnumMemberName(AdbTvRemoteCommands.AudioTvSpeakers)]
    AudioTvSpeakers,

    [JsonStringEnumMemberName(AdbTvRemoteCommands.AudioExternalDevice)]
    AudioExternalDevice,

    [JsonStringEnumMemberName(AdbTvRemoteCommands.PowerStateOn)]
    PowerStateOn,

    [JsonStringEnumMemberName(AdbTvRemoteCommands.PowerStateOff)]
    PowerStateOff
}
