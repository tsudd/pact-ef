using PactEf.Verify;

namespace PactEf.Verify.Tests;

public class SnapshotSourceTests : IDisposable
{
    public void Dispose() =>
        Environment.SetEnvironmentVariable("TEST_SNAPSHOT_PATHS", null);

    [Fact]
    public void FromFolder_ResolvesToSinglePath()
    {
        var source = SnapshotSource.FromFolder("/some/path");
        var paths = source.ResolvePaths();
        Assert.Equal(["/some/path"], paths);
    }

    [Fact]
    public void FromEnvVariable_WhenNotSet_ReturnsEmpty()
    {
        var source = SnapshotSource.FromEnvVariable("TEST_SNAPSHOT_PATHS");
        Assert.Empty(source.ResolvePaths());
    }

    [Fact]
    public void FromEnvVariable_WhenSet_ReturnsSplitPaths()
    {
        Environment.SetEnvironmentVariable("TEST_SNAPSHOT_PATHS", "/a;/b;/c");
        var source = SnapshotSource.FromEnvVariable("TEST_SNAPSHOT_PATHS");
        Assert.Equal(["/a", "/b", "/c"], source.ResolvePaths());
    }

    [Fact]
    public void FromEnvVariable_IsEnvVariable_ReturnsTrue()
    {
        var source = SnapshotSource.FromEnvVariable("TEST_SNAPSHOT_PATHS");
        Assert.True(source.IsEnvVariable);
    }

    [Fact]
    public void FromFolder_IsEnvVariable_ReturnsFalse()
    {
        var source = SnapshotSource.FromFolder("/path");
        Assert.False(source.IsEnvVariable);
    }
}
