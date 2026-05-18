using PactEf.Capture;
using PactEf.Capture.Utilities;

namespace PactEf.Capture.Tests;

[Collection("EnvVarTests")]
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
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        // Act
        var result = EnvironmentGuard.IsActive("PACTEF_CAPTURE_DISABLED");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsActive_WhenDotNetEnvironmentIsTesting_ReturnsTrue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Testing");

        // Act
        var result = EnvironmentGuard.IsActive("PACTEF_CAPTURE_DISABLED");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsActive_WhenEnvironmentIsDevelopment_ReturnsFalse()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        // Act
        var result = EnvironmentGuard.IsActive("PACTEF_CAPTURE_DISABLED");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsActive_WhenNoEnvironmentSet_ReturnsFalse()
    {
        // Act
        var result = EnvironmentGuard.IsActive("PACTEF_CAPTURE_DISABLED");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsActive_WhenDisableVarIsTrue_ReturnsFalse()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("PACTEF_CAPTURE_DISABLED", "true");

        // Act
        var result = EnvironmentGuard.IsActive("PACTEF_CAPTURE_DISABLED");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsActive_CaseInsensitiveEnvironmentValue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "testing");

        // Act
        var result = EnvironmentGuard.IsActive("PACTEF_CAPTURE_DISABLED");

        // Assert
        Assert.True(result);
    }
}
