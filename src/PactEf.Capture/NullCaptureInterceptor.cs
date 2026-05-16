using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PactEf.Capture;

/// <summary>No-op interceptor returned when the environment guard is not satisfied.</summary>
internal sealed class NullCaptureInterceptor : DbCommandInterceptor
{
    public static readonly NullCaptureInterceptor Instance = new();
    public Task FlushAsync() => Task.CompletedTask;
}
