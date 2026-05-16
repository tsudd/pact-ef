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
}
