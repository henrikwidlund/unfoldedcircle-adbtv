using UnfoldedCircle.Server.Configuration;

namespace UnfoldedCircle.AdbTv.Configuration;

public sealed record BackupData(UnfoldedCircleConfiguration<AdbConfigurationItem> Configuration, string PublicKey, string PrivateKey);
