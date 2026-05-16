using Xunit;

namespace PactEf.Capture;

/// <summary>
/// xUnit v2 assembly fixture. Flushes all registered interceptors at test run end.
/// Add to consumer test project with:
///   [assembly: AssemblyFixture(typeof(PactEfAssemblyFixture))]
/// </summary>
public sealed class PactEfAssemblyFixture : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var interceptor in CaptureRegistry.GetAll())
            await interceptor.FlushAsync();
    }
}
