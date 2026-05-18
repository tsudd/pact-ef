using PactEf.Capture;
using PactEf.Capture.Utilities;

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
        var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyDir);
        try
        {
            var root = ProjectRootLocator.FindProjectRoot(emptyDir);
            Assert.Null(root);
        }
        finally
        {
            Directory.Delete(emptyDir);
        }
    }
}
