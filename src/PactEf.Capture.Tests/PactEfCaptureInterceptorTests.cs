using Microsoft.EntityFrameworkCore.Diagnostics;
using PactEf.Capture;

namespace PactEf.Capture.Tests;

[Collection("EnvVarTests")]
public class PactEfCaptureInterceptorTests : IDisposable
{
    public PactEfCaptureInterceptorTests()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("PACTEF_CAPTURE_DISABLED", null);
    }

    [Fact]
    public void Create_WhenEnvironmentIsTesting_ReturnsRealInterceptor()
    {
        var interceptor = PactEfCaptureInterceptor.Create(o => o.ConsumerName = "Test");
        Assert.IsType<PactEfCaptureInterceptor>(interceptor);
    }

    [Fact]
    public void Create_WhenEnvironmentNotTesting_ReturnsNullInterceptor()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        var interceptor = PactEfCaptureInterceptor.Create(o => o.ConsumerName = "Test");
        Assert.IsType<NullCaptureInterceptor>(interceptor);
    }

    [Fact]
    public void Create_WhenDisabled_ReturnsNullInterceptor()
    {
        Environment.SetEnvironmentVariable("PACTEF_CAPTURE_DISABLED", "true");
        var interceptor = PactEfCaptureInterceptor.Create(o => o.ConsumerName = "Test");
        Assert.IsType<NullCaptureInterceptor>(interceptor);
    }
}
