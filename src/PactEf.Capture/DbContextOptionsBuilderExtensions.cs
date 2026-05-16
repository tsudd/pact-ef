using Microsoft.EntityFrameworkCore;

namespace PactEf.Capture;

public static class DbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder AddPactEfCapture(
        this DbContextOptionsBuilder builder,
        Action<CaptureOptions> configure)
    {
        var options = new CaptureOptions { ConsumerName = string.Empty };
        configure(options);

        if (!EnvironmentGuard.IsActive(options.DisableEnvVariable))
            return builder;

        return builder.AddInterceptors(new PactEfCaptureInterceptor(options));
    }
}
