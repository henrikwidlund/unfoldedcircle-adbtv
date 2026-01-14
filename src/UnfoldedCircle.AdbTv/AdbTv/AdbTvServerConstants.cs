namespace UnfoldedCircle.AdbTv.AdbTv;

internal static class AdbTvServerConstants
{
    internal const string IpAddressKey = "ip_address";
    internal const string MacAddressKey = "mac_address";
    internal const string MacAddressRegex = "^([0-9a-fA-F]{2}[:-]){5}([0-9a-fA-F]{2})$";
    internal const string PortKey = "port";
    internal const string DeviceIdKey = "device_id";
    internal const string EntityName = "entity_name";
    internal const string MaxMessageHandlingWaitTimeInSecondsKey = "max_message_handling_wait_time_in_seconds";
    internal const string Manufacturer = "manufacturer";
    internal const string PairingCode = "pairing_code";
    internal const string PairingPort = "pairing_port";

    internal const string IpAddressRegex = @"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)|(([0-9a-fA-F]{1,4}:)" +
                                           "{7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}" +
                                           "(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4})" +
                                           "{1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|" +
                                           @"fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|" +
                                           @"(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9])" +
                                           "{0,1}[0-9]))$";
}
