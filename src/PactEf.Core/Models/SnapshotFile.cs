namespace PactEf.Core.Models;

public sealed class SnapshotFile
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string ConsumerName { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public string? DbSchemaVersion { get; init; }
    public required IReadOnlyList<QueryEntry> Queries { get; init; }
}
