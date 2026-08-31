namespace UnfoldedCircle.AdbTv.AdbTv;

internal static class AdbTvServerConstants
{
    internal const string IpAddressKey = "ip_address";
    internal const string MacAddressKey = "mac_address";
    internal const string MacAddressRegex = "^([0-9a-fA-F]{2}[:-]){5}([0-9a-fA-F]{2})$";
    internal const string PortKey = "port";
    internal const string EntityName = "entity_name";
    internal const string MaxMessageHandlingWaitTimeInSecondsKey = "max_message_handling_wait_time_in_seconds";
    internal const string PollingIntervalSecondsKey = "polling_interval_seconds";
    internal const ushort DefaultPollingIntervalSeconds = 5;
    internal const ushort MinPollingIntervalSeconds = 1;
    internal const ushort MaxPollingIntervalSeconds = 300;
    internal const string Manufacturer = "manufacturer";
    internal const string AllowReauthKey = "allow_reauth";
    internal const string AppListSelectSuffix = "applist";

    /// <summary>
    /// The 6-digit code shown on the device's Developer Options → Wireless debugging → "Pair
    /// device with pairing code" screen. Left empty to use the existing manual-IP/on-device-
    /// approval flow; when non-empty, triggers a wireless-pairing attempt instead.
    /// </summary>
    internal const string PairingCodeKey = "pairing_code";

    /// <summary>The pairing service port shown on the same wireless-pairing screen (not the regular ADB port).</summary>
    internal const string PairingPortKey = "pairing_port";

    internal const string PairingCodeRegex = "^([0-9]{6})?$";
    internal const string PortRegex = "^(([1-9][0-9]{0,3}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5]))?$";

    internal const string IpAddressRegex = @"^(?:(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)|(([0-9a-fA-F]{1,4}:)" +
                                           "{7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}" +
                                           "(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4})" +
                                           "{1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|" +
                                           @"fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|" +
                                           @"(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9])" +
                                           "{0,1}[0-9])))$";
}
