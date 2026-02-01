using System.Collections.Frozen;

namespace UnfoldedCircle.AdbTv.AdbTv;

public static class AdbTvConstants
{
    public const string Home = "3";
    public const string Back = "4";
    public const string Key0 = "7";
    public const string Key1 = "8";
    public const string Key2 = "9";
    public const string Key3 = "10";
    public const string Key4 = "11";
    public const string Key5 = "12";
    public const string Key6 = "13";
    public const string Key7 = "14";
    public const string Key8 = "15";
    public const string Key9 = "16";
    public const string DpadUp = "19";
    public const string DpadDown = "20";
    public const string DpadLeft = "21";
    public const string DpadRight = "22";
    public const string DpadCenter = "23";
    public const string VolumeUp = "24";
    public const string VolumeDown = "25";
    public const string Power = "26";
    public const string Mute = "91";
    public const string Info = "165";
    public const string ChannelUp = "166";
    public const string ChannelDown = "167";
    public const string Settings = "176";
    public const string Sleep = "223";
    public const string Wakeup = "224";
    public const string Hdmi1 = "243";
    public const string Hdmi2 = "244";
    public const string Hdmi3 = "245";
    public const string Hdmi4 = "246";
}

public static class AdbTvRemoteCommands
{
    public const string Digit0 = "DIGIT_0";
    public const string Digit1 = "DIGIT_1";
    public const string Digit2 = "DIGIT_2";
    public const string Digit3 = "DIGIT_3";
    public const string Digit4 = "DIGIT_4";
    public const string Digit5 = "DIGIT_5";
    public const string Digit6 = "DIGIT_6";
    public const string Digit7 = "DIGIT_7";
    public const string Digit8 = "DIGIT_8";
    public const string Digit9 = "DIGIT_9";
    public const string Info = "INFO";
    public const string Settings = "SETTINGS";
    public const string InputHdmi1 = "INPUT_HDMI1";
    public const string InputHdmi2 = "INPUT_HDMI2";
    public const string InputHdmi3 = "INPUT_HDMI3";
    public const string InputHdmi4 = "INPUT_HDMI4";
    public const string AudioTvSpeakers = "AUDIO_TV_SPEAKERS";
    public const string AudioExternalDevice = "AUDIO_EXTERNAL_DEVICE";
    public const string PowerStateOn = "POWER_STATE_ON";
    public const string PowerStateOff = "POWER_STATE_OFF";
}

public static class AdbAdvancedCommands
{
    public const string PortNumberPlaceholder = "_PORT_NUMBER_";
    public const string HisenseHdmi = $"am start -a android.intent.action.VIEW -d content://android.media.tv/passthrough/com.vt.source.external%2F.hdmi.HdmiTvInputService%2FHW{PortNumberPlaceholder}";
    public const string PhilipsHdmi = $"am start -a android.intent.action.VIEW -d content://android.media.tv/passthrough/com.mediatek.tvinput%2F.hdmi.HDMIInputService%2FHW{PortNumberPlaceholder} -n org.droidtv.playtv/.PlayTvActivity -f 0x10000000";
    public const string PhilipsAlternateHdmi = $"am start -a android.intent.action.VIEW -d content://android.media.tv/passthrough/com.mediatek.tis%2F.HdmiInputService%2FHW{PortNumberPlaceholder} -n org.droidtv.playtv/.PlayTvActivity -f 0x10000000";
    public const string SonyHdmi = $"am start -a android.intent.action.VIEW -d content://android.media.tv/passthrough/com.sony.dtv.tvinput.external%2F.ExternalTvInputService%2FHW{PortNumberPlaceholder} -n com.sony.dtv.tvx/.MainActivity -f 0x10000000";
    public const string TclHdmi = $"am start -a android.intent.action.VIEW -d content://android.media.tv/passthrough/com.tcl.tvinput%2F.TvPassThroughService%2FHW{PortNumberPlaceholder} -f 0x10000000";
    private const string AudioOutputBase = "settings put global hdmi_system_audio_control_enabled ";
    public const string AudioTvSpeakers = AudioOutputBase + "0";
    public const string AudioExternalDevice = AudioOutputBase + "1";
}

public static class AdbTvRemoteApps
{
    public const string DisneyPlus = "com.disney.disneyplus";
    public const string Kodi = "org.xbmc.kodi";
    public const string MagentaTv = "de.telekom.magentatv.firetv";
    public const string Netflix = "com.netflix.ninja";
    public const string RtlPlus = "de.cbc.tvnow.firetv";
    public const string YouTube = "com.amazon.firetv.youtube";
    public const string Zdf = "com.zdf.android.mediathek";
}

public static class AppNames
{
    private const string AppleTv = "Apple TV+";
    private const string Ard = "ARD";
    private const string DisneyPlus = "Disney+";
    private const string Kodi = "Kodi";
    private const string MagentaTv = "Magenta TV";
    private const string Netflix = "Netflix";
    private const string RtlPlus = "RTL+";
    private const string YouTube = "YouTube";
    private const string Zdf = "ZDF";

    public static readonly FrozenSet<string> SupportedApps =
    [
        AppleTv,
        Ard,
        DisneyPlus,
        Kodi,
        MagentaTv,
        Netflix,
        RtlPlus,
        YouTube,
        Zdf,
    ];

    public static readonly FrozenDictionary<string, string> AppNamesMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [AppleTv] = RemoteActivities.AppleTv,
        [Ard] = RemoteActivities.Ard,
        [DisneyPlus] = AdbTvRemoteApps.DisneyPlus,
        [Kodi] = AdbTvRemoteApps.Kodi,
        [MagentaTv] = AdbTvRemoteApps.MagentaTv,
        [Netflix] = AdbTvRemoteApps.Netflix,
        [RtlPlus] = AdbTvRemoteApps.RtlPlus,
        [YouTube] = AdbTvRemoteApps.YouTube,
        [Zdf] = AdbTvRemoteApps.Zdf,
    }.ToFrozenDictionary();
}

public static class RemoteActivities
{
    public const string AppleTv = "com.apple.atve.amazon.appletv/.MainActivity";
    public const string Ard = "de.swr.ard.avp.mobile.android.amazon/.TvActivity";
}