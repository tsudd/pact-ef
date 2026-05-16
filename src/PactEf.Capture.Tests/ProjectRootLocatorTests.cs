using PactEf.Capture;

namespace PactEf.Capture.Tests;

public class ProjectRootLocatorTests
{
    [Fact]
    public void FindProjectRoot_FromTestOutputDir_FindsCsproj()
    {
        // AppContext.BaseDirectory is inside bin/Debug/net10.0 during tests
        // walking up should find PactEf.Capture.Tests.csproj
        var root = ProjectRootLocator.FindProjectRoot(AppContext.BaseDirectory);

        Assert.NotNull(root);
        Assert.True(Directory.Exists(root));
        Assert.True(Directory.GetFiles(root, "*.csproj").Length > 0);
    }

    [Fact]
    public void FindProjectRoot_WhenNoCsprojFound_ReturnsNull()
    {
        var root = ProjectRootLocator.FindProjectRoot(Path.GetTempPath());
        Assert.Null(root);
    }
}
