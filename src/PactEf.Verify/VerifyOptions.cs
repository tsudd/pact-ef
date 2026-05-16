using PactEf.Verify.Verification;

namespace PactEf.Verify;

public sealed class VerifyOptions
{
    public List<SnapshotSource> SnapshotSources { get; set; } = [];
    public required string ConnectionString { get; set; }
    public DbProvider Provider { get; set; } = DbProvider.PostgreSql;
    public VerificationMode DefaultMode { get; set; } = VerificationMode.Explain;
}

public enum DbProvider
{
    PostgreSql
}
