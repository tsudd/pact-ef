using PactEf.Capture;

namespace PactEf.Capture.Tests;

public class EnvironmentGuardTests : IDisposable
{
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("PACTEF_CAPTURE_DISABLED", null);
    }

    [Fact]
    public void IsActive_WhenAspNetCoreEnvironmentIsTesting_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Assert.True(EnvironmentGuard.IsActive("PACTEF_CAPTURE_DISABLED"));
    }

    [Fact]
    public void IsActive_WhenDotNetEnvironmentIsTesting_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Testing");
        Assert.True(EnvironmentGuard.IsActive("PACTEF_CAPTURE_DISABLED"));
    }

    [Fact]
    public void IsActive_WhenEnvironmentIsDevelopment_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Assert.False(EnvironmentGuard.IsActive("PACTEF_CAPTURE_DISABLED"));
    }

    [Fact]
    public void IsActive_WhenNoEnvironmentSet_ReturnsFalse()
    {
        Assert.False(EnvironmentGuard.IsActive("PACTEF_CAPTURE_DISABLED"));
    }

    [Fact]
    public void IsActive_WhenDisableVarIsTrue_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("PACTEF_CAPTURE_DISABLED", "true");
        Assert.False(EnvironmentGuard.IsActive("PACTEF_CAPTURE_DISABLED"));
    }

    [Fact]
    public void IsActive_CaseInsensitiveEnvironmentValue()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "testing");
        Assert.True(EnvironmentGuard.IsActive("PACTEF_CAPTURE_DISABLED"));
    }
}
