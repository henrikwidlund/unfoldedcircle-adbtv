using UnfoldedCircle.Server.Configuration;

namespace UnfoldedCircle.AdbTv.Configuration;

public sealed record BackupData(UnfoldedCircleConfiguration<AdbGlobalConfiguration, AdbConfigurationItem> Configuration, string PrivateKey);
