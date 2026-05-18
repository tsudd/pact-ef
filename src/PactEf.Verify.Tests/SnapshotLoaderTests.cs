using PactEf.Core.Models;
using PactEf.Core.Serialization;

namespace PactEf.Verify.Tests;

public class SnapshotLoaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public SnapshotLoaderTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task WriteSnapshot(string consumerName, string subfolder = "pactef-snapshots")
    {
        var dir = Path.Combine(_tempDir, subfolder);
        Directory.CreateDirectory(dir);
        var snapshot = new SnapshotFile
        {
            ConsumerName = consumerName,
            CapturedAt = DateTimeOffset.UtcNow,
            Queries = [new QueryEntry { Sql = "SELECT 1", ParameterTypes = [] }]
        };
        await SnapshotSerializer.WriteToFileAsync(snapshot, Path.Combine(dir, $"{consumerName}.json"));
    }

    [Fact]
    public async Task Load_FindsSnapshotFilesInSubfolders()
    {
        // Arrange
        await WriteSnapshot("OrderService");
        var loader = new SnapshotLoader([SnapshotSource.FromFolder(_tempDir)]);

        // Act
        var snapshots = await loader.LoadAllAsync();

        // Assert
        Assert.Single(snapshots);
        Assert.Equal("OrderService", snapshots[0].ConsumerName);
    }

    [Fact]
    public async Task Load_EnvVariableWins_OverFolder_ForSameConsumer()
    {
        // Arrange
        var dir1 = Path.Combine(_tempDir, "ci");
        var dir2 = Path.Combine(_tempDir, "local");
        Directory.CreateDirectory(Path.Combine(dir1, "pactef-snapshots"));
        Directory.CreateDirectory(Path.Combine(dir2, "pactef-snapshots"));

        var snapshot1 = new SnapshotFile
        {
            ConsumerName = "OrderService",
            CapturedAt = DateTimeOffset.UtcNow,
            DbSchemaVersion = "ci-version",
            Queries = [new QueryEntry { Sql = "SELECT 1", ParameterTypes = [] }]
        };
        var snapshot2 = new SnapshotFile
        {
            ConsumerName = snapshot1.ConsumerName,
            CapturedAt = snapshot1.CapturedAt,
            DbSchemaVersion = "local-version",
            Queries = snapshot1.Queries
        };

        await SnapshotSerializer.WriteToFileAsync(snapshot1,
            Path.Combine(dir1, "pactef-snapshots", "OrderService.json"));
        await SnapshotSerializer.WriteToFileAsync(snapshot2,
            Path.Combine(dir2, "pactef-snapshots", "OrderService.json"));

        Environment.SetEnvironmentVariable("TEST_PATHS", dir2);
        try
        {
            var sources = new[]
            {
                SnapshotSource.FromFolder(dir1),
                SnapshotSource.FromEnvVariable("TEST_PATHS")
            };
            var loader = new SnapshotLoader(sources);

            // Act
            var snapshots = await loader.LoadAllAsync();

            // Assert
            Assert.Single(snapshots);
            Assert.Equal("local-version", snapshots[0].DbSchemaVersion);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_PATHS", null);
        }
    }
}
