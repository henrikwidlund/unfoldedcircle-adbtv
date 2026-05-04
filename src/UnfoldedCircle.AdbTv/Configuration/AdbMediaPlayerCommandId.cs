using System.ComponentModel.DataAnnotations;

using Theodicean.SourceGenerators;

using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.Models.Sync;

namespace UnfoldedCircle.AdbTv.Configuration;

[EnumJsonConverter<AdbMediaPlayerCommandId>(CaseSensitive = false, PropertyName = "cmd_id")]
[JsonConverter(typeof(AdbMediaPlayerCommandIdJsonConverter))]
public enum AdbMediaPlayerCommandId : sbyte
{
    /// <summary>
    /// Switch on media player.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.On)]
    On = 1,

    /// <summary>
    /// Switch off media player.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Off)]
    Off,

    /// <summary>
    /// Toggle the current power state, either from on -> off or from off -> on.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Toggle)]
    Toggle,

    /// <summary>
    /// Increase volume.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.VolumeUp)]
    VolumeUp,

    /// <summary>
    /// Decrease volume.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.VolumeDown)]
    VolumeDown,

    /// <summary>
    /// Toggle mute state.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.MuteToggle)]
    MuteToggle,

    /// <summary>
    /// Channel up.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.ChannelUp)]
    ChannelUp,

    /// <summary>
    /// Channel down.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.ChannelDown)]
    ChannelDown,

    /// <summary>
    /// Directional pad up.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.CursorUp)]
    CursorUp,

    /// <summary>
    /// Directional pad down.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.CursorDown)]
    CursorDown,

    /// <summary>
    /// Directional pad left.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.CursorLeft)]
    CursorLeft,

    /// <summary>
    /// Directional pad right.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.CursorRight)]
    CursorRight,

    /// <summary>
    /// Directional pad enter.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.CursorEnter)]
    CursorEnter,

    /// <summary>
    /// Number pad digit 0.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Digit0)]
    Digit0,

    /// <summary>
    /// Number pad digit 1.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Digit1)]
    Digit1,

    /// <summary>
    /// Number pad digit 2.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Digit2)]
    Digit2,

    /// <summary>
    /// Number pad digit 3.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Digit3)]
    Digit3,

    /// <summary>
    /// Number pad digit 4.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Digit4)]
    Digit4,

    /// <summary>
    /// Number pad digit 5.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Digit5)]
    Digit5,

    /// <summary>
    /// Number pad digit 6.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Digit6)]
    Digit6,

    /// <summary>
    /// Number pad digit 7.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Digit7)]
    Digit7,

    /// <summary>
    /// Number pad digit 8.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Digit8)]
    Digit8,

    /// <summary>
    /// Number pad digit 9.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Digit9)]
    Digit9,

    /// <summary>
    /// Home menu
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Home)]
    Home,

    /// <summary>
    /// Information menu / what's playing.
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Info)]
    Info,

    /// <summary>
    /// Back / exit function for menu navigation (to exit menu, guide, info).
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Back)]
    Back,

    /// <summary>
    /// Select an input source from the available sources.
    /// </summary>
    /// <remarks>Parameters: source</remarks>
    [Display(Name = MediaPlayerCommandIdConstants.SelectSource)]
    SelectSource,

    /// <summary>
    /// Settings menu
    /// </summary>
    [Display(Name = MediaPlayerCommandIdConstants.Settings)]
    Settings,

    [Display(Name = AdbTvRemoteCommands.AudioTvSpeakers)]
    AudioTvSpeakers,

    [Display(Name = AdbTvRemoteCommands.AudioExternalDevice)]
    AudioExternalDevice,

    [Display(Name = AdbTvRemoteCommands.PowerStateOn)]
    PowerStateOn,

    [Display(Name = AdbTvRemoteCommands.PowerStateOff)]
    PowerStateOff
}

// ReSharper disable once RedundantExtendsListEntry Workaround for bug in roslyn
public partial class AdbMediaPlayerCommandIdJsonConverter : JsonConverter<AdbMediaPlayerCommandId>;
