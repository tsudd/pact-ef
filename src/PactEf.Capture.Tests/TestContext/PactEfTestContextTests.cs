using PactEf.Capture.TestContext;

namespace PactEf.Capture.Tests.TestContext;

public class PactEfTestContextTests
{
    [Fact]
    public void Current_DefaultsToNull()
    {
        // Act & Assert
        Assert.Null(PactEfTestContext.Current);
    }

    [Fact]
    public async Task Current_IsAsyncLocal_IsolatedPerTask()
    {
        // Arrange
        string? capturedInTask = "not-set";
        PactEfTestContext.Current = "outer";

        // Act
        await Task.Run(() =>
        {
            // AsyncLocal flows into child tasks (read-only)
            capturedInTask = PactEfTestContext.Current;
            // But changes here don't affect outer
            PactEfTestContext.Current = "inner";
        });

        // Assert
        // Outer is unaffected by change inside Task.Run
        Assert.Equal("outer", PactEfTestContext.Current);
        Assert.Equal("outer", capturedInTask); // inherited from parent
    }

    [Fact]
    public void CurrentClass_DefaultsToNull()
    {
        // Act & Assert
        Assert.Null(PactEfTestContext.CurrentClass);
    }
}
