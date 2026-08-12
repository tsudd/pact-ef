namespace PactEf.Core.Models;

public sealed class QueryEntry
{
    public required string Sql { get; init; }
    public required IReadOnlyList<string> ParameterTypes { get; init; }
    public IReadOnlyList<ParameterMetadata> Parameters { get; init; } = [];
    public int ExecutionCount { get; init; } = 1;
    public string? TestName { get; init; }
    public string? TestClass { get; init; }
}
