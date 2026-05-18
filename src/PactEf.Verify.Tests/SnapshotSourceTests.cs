namespace PactEf.Verify.Tests;

public class SnapshotSourceTests : IDisposable
{
    public void Dispose() =>
        Environment.SetEnvironmentVariable("TEST_SNAPSHOT_PATHS", null);

    [Fact]
    public void FromFolder_ResolvesToSinglePath()
    {
        // Arrange
        var source = SnapshotSource.FromFolder("/some/path");

        // Act
        var paths = source.ResolvePaths();

        // Assert
        Assert.Equal(["/some/path"], paths);
    }

    [Fact]
    public void FromEnvVariable_WhenNotSet_ReturnsEmpty()
    {
        // Arrange
        var source = SnapshotSource.FromEnvVariable("TEST_SNAPSHOT_PATHS");

        // Act
        var paths = source.ResolvePaths();

        // Assert
        Assert.Empty(paths);
    }

    [Fact]
    public void FromEnvVariable_WhenSet_ReturnsSplitPaths()
    {
        // Arrange
        Environment.SetEnvironmentVariable("TEST_SNAPSHOT_PATHS", "/a;/b;/c");
        var source = SnapshotSource.FromEnvVariable("TEST_SNAPSHOT_PATHS");

        // Act
        var paths = source.ResolvePaths();

        // Assert
        Assert.Equal(["/a", "/b", "/c"], paths);
    }

    [Fact]
    public void FromEnvVariable_IsEnvVariable_ReturnsTrue()
    {
        // Arrange
        var source = SnapshotSource.FromEnvVariable("TEST_SNAPSHOT_PATHS");

        // Act & Assert
        Assert.True(source.IsEnvVariable);
    }

    [Fact]
    public void FromFolder_IsEnvVariable_ReturnsFalse()
    {
        // Arrange
        var source = SnapshotSource.FromFolder("/path");

        // Act & Assert
        Assert.False(source.IsEnvVariable);
    }
}
