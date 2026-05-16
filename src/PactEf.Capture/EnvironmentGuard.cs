namespace PactEf.Capture;

internal static class EnvironmentGuard
{
    private static readonly string[] EnvironmentVariables =
        ["ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT"];

    public static bool IsActive(string disableEnvVariable)
    {
        var disableValue = Environment.GetEnvironmentVariable(disableEnvVariable);
        if (disableValue is "true" or "1" or "yes")
            return false;

        return EnvironmentVariables.Any(v =>
            string.Equals(
                Environment.GetEnvironmentVariable(v),
                "Testing",
                StringComparison.OrdinalIgnoreCase));
    }
}
