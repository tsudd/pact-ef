using System.Text.Json;
using System.Text.Json.Serialization;
using PactEf.Core.Models;

namespace PactEf.Core.Serialization;

public static class SnapshotSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(SnapshotFile snapshot)
    {
        var ordered = new SnapshotFile
        {
            SchemaVersion = snapshot.SchemaVersion,
            ConsumerName = snapshot.ConsumerName,
            CapturedAt = snapshot.CapturedAt,
            DbSchemaVersion = snapshot.DbSchemaVersion,
            Queries = snapshot.Queries
                .OrderBy(q => q.Sql, StringComparer.Ordinal)
                .ToList()
        };
        return JsonSerializer.Serialize(ordered, Options);
    }

    public static SnapshotFile Deserialize(string json)
    {
        return JsonSerializer.Deserialize<SnapshotFile>(json, Options)
            ?? throw new InvalidOperationException("Failed to deserialize snapshot.");
    }

    public static async Task WriteToFileAsync(SnapshotFile snapshot, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, Serialize(snapshot));
    }

    public static async Task<SnapshotFile> ReadFromFileAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return Deserialize(json);
    }
}
