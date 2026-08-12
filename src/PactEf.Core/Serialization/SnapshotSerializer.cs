using System.Text.Json;
using System.Text.Json.Serialization;
using PactEf.Core.Models;

namespace PactEf.Core.Serialization;

public static class SnapshotSerializer
{
    private const string CurrentSchemaVersion = "2.0";

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
            SchemaVersion = CurrentSchemaVersion,
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
        var snapshot = JsonSerializer.Deserialize<SnapshotFile>(json, Options)
            ?? throw new InvalidOperationException("Failed to deserialize snapshot.");

        return new SnapshotFile
        {
            SchemaVersion = snapshot.SchemaVersion,
            ConsumerName = snapshot.ConsumerName,
            CapturedAt = snapshot.CapturedAt,
            DbSchemaVersion = snapshot.DbSchemaVersion,
            Queries = snapshot.Queries.Select(ApplyLegacyParameterFallback).ToList()
        };
    }

    private static QueryEntry ApplyLegacyParameterFallback(QueryEntry entry)
    {
        if (entry.Parameters.Count > 0 || entry.ParameterTypes.Count == 0)
            return entry;

        return new QueryEntry
        {
            Sql = entry.Sql,
            ParameterTypes = entry.ParameterTypes,
            Parameters = entry.ParameterTypes
                .Select(clrType => new ParameterMetadata { ClrType = clrType })
                .ToList(),
            ExecutionCount = entry.ExecutionCount,
            TestName = entry.TestName,
            TestClass = entry.TestClass
        };
    }

    public static async Task WriteToFileAsync(SnapshotFile snapshot, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(filePath, Serialize(snapshot));
    }

    public static async Task<SnapshotFile> ReadFromFileAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return Deserialize(json);
    }
}
