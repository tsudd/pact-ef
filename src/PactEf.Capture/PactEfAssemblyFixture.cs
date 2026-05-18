using Xunit;

namespace PactEf.Capture;

/// <summary>
/// xUnit v2 assembly fixture. Flushes all registered interceptors at test run end.
/// Merges queries from all interceptors sharing the same ConsumerName into one snapshot.
/// </summary>
public sealed class PactEfAssemblyFixture : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Group interceptors by consumer name and flush each group merged
        var groups = CaptureRegistry.GetAll()
            .GroupBy(i => i.ConsumerName, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
            await group.First().FlushMergedAsync(group.ToList());
    }
}
