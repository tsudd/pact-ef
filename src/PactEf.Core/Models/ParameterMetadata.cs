namespace PactEf.Core.Models;

public sealed class ParameterMetadata
{
    public string? Name { get; init; }
    public string? ClrType { get; init; }
    public string? DbType { get; init; }
    public string? StoreType { get; init; }
    public int? MaxLength { get; init; }
    public int? Precision { get; init; }
    public int? Scale { get; init; }
    public bool? IsNullable { get; init; }
    public int? Size { get; init; }
}
