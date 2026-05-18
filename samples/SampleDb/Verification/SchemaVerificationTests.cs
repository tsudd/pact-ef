using Microsoft.EntityFrameworkCore;
using PactEf.Verify;
using PactEf.Verify.Verification;
using SampleDb;
using Testcontainers.PostgreSql;

namespace SampleDb.Verification;

public sealed class SchemaVerificationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder().Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        await using var ctx = new SampleDbContext(options);
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    [Trait("Category", "PactEfVerification")]
    public async Task AllConsumerSnapshots_AreCompatibleWithCurrentSchema()
    {
        // Arrange
        // CI: FromFolder points to checked-out consumer repos (absolute or relative to CWD)
        // Local monorepo: set PACTEF_SNAPSHOT_PATHS to an absolute path, e.g.:
        //   export PACTEF_SNAPSHOT_PATHS=/path/to/repo/samples/SampleConsumer.Tests/pactef-snapshots

        // Act & Assert
        await PactEfVerifier.VerifyAllAsync(options =>
        {
            options.SnapshotSources =
            [
                SnapshotSource.FromFolder("consumers/sample-consumer"),  // CI path (skipped if not present)
                SnapshotSource.FromEnvVariable("PACTEF_SNAPSHOT_PATHS"), // local override
            ];
            options.ConnectionString = _container.GetConnectionString();
            options.Provider = DbProvider.PostgreSql;
            options.DefaultMode = VerificationMode.Explain;
        });
    }
}
