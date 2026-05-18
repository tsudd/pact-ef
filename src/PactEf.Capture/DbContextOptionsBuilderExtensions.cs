using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PactEf.Capture;

public static class DbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder AddPactEfCapture(
        this DbContextOptionsBuilder builder,
        Action<CaptureOptions> configure)
    {
        var interceptor = PactEfCaptureInterceptor.Create(configure);
        return builder.AddInterceptors(interceptor);
    }

    public static DbContextOptionsBuilder<TContext> AddPactEfCapture<TContext>(
        this DbContextOptionsBuilder<TContext> builder,
        Action<CaptureOptions> configure)
        where TContext : DbContext
    {
        var interceptor = PactEfCaptureInterceptor.Create(configure);
        builder.AddInterceptors(interceptor);
        return builder;
    }
}
