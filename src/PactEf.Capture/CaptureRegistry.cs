using System.Collections.Concurrent;

namespace PactEf.Capture;

/// <summary>
/// Tracks all active PactEfCaptureInterceptor instances created in this process.
/// Used by PactEfAssemblyFixture to flush all interceptors at test run end.
/// </summary>
internal static class CaptureRegistry
{
    private static readonly ConcurrentBag<PactEfCaptureInterceptor> _interceptors = new();

    internal static void Register(PactEfCaptureInterceptor interceptor) =>
        _interceptors.Add(interceptor);

    internal static IReadOnlyList<PactEfCaptureInterceptor> GetAll() =>
        _interceptors.ToList();
}
