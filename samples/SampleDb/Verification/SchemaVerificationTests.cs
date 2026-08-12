using Microsoft.EntityFrameworkCore;
using PactEf.Verify;
using PactEf.Verify.Verification;
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
                SnapshotSource.FromFolder("consumers/sample-consumer"), // CI path (skipped if not present)
                SnapshotSource.FromEnvVariable("PACTEF_SNAPSHOT_PATHS"), // local override
            ];
            options.ConnectionString = _container.GetConnectionString();
            options.Provider = DbProvider.PostgreSql;
            options.DefaultMode = VerificationMode.Explain;
        });
    }

    [Fact]
    [Trait("Category", "PactEfVerification")]
    public async Task BrokenConsumer_ShouldFailWithBreakingMigration()
    {
        // Act & Assert
        try
        {
            await PactEfVerifier.VerifyAllAsync(options =>
            {
                options.SnapshotSources =
                [
                    SnapshotSource.FromEnvVariable("CUSTOM_SNAPSHOT_PATHS") // Custom variable with the broken consumer snapshots
                ];
                options.ConnectionString = _container.GetConnectionString();
                options.Provider = DbProvider.PostgreSql;
                options.DefaultMode = VerificationMode.Explain;
            });
        }
        catch (PactEfVerificationException ex)
        {
            // Assert
            Assert.Contains("FAILED", ex.Message);
            return;
        }

        Assert.Fail();
    }

    [Fact]
    [Trait("Category", "PactEfVerification")]
    public async Task BoundaryVariant_MaxLengthExceedsColumn_FailsWithTruncationError()
    {
        // Arrange: fixture captures an INSERT into "Orders"."Status" (varchar(50)) with
        // ParameterMetadata.MaxLength = 100, so the generated boundary-value replay writes
        // a 100-char string that the column can no longer hold.
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Verification", "fixtures", "boundary-consumer");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<PactEfVerificationException>(() =>
            PactEfVerifier.VerifyAllAsync(options =>
            {
                options.SnapshotSources = [SnapshotSource.FromFolder(fixturePath)];
                options.ConnectionString = _container.GetConnectionString();
                options.Provider = DbProvider.PostgreSql;
                options.DefaultMode = VerificationMode.Explain;
            }));

        Assert.Contains("22001", exception.Message);
        Assert.Contains(exception.Failures, f => f.ErrorCode == "22001");

        // Assert: the mutating replay was rolled back, leaving the table empty.
        await using var conn = new Npgsql.NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM \"Orders\"";
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(0, count);
    }
}