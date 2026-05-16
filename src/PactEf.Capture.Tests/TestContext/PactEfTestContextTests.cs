using PactEf.Capture.TestContext;

namespace PactEf.Capture.Tests.TestContext;

public class PactEfTestContextTests
{
    [Fact]
    public void Current_DefaultsToNull()
    {
        Assert.Null(PactEfTestContext.Current);
    }

    [Fact]
    public async Task Current_IsAsyncLocal_IsolatedPerTask()
    {
        string? capturedInTask = "not-set";

        PactEfTestContext.Current = "outer";

        await Task.Run(() =>
        {
            // AsyncLocal flows into child tasks (read-only)
            capturedInTask = PactEfTestContext.Current;
            // But changes here don't affect outer
            PactEfTestContext.Current = "inner";
        });

        // Outer is unaffected by change inside Task.Run
        Assert.Equal("outer", PactEfTestContext.Current);
        Assert.Equal("outer", capturedInTask); // inherited from parent
    }

    [Fact]
    public void CurrentClass_DefaultsToNull()
    {
        Assert.Null(PactEfTestContext.CurrentClass);
    }
}
