using System.Collections.Concurrent;
using PactEf.Core.Models;

namespace PactEf.Capture;

internal sealed class QueryBuffer
{
    private readonly ConcurrentDictionary<string, (QueryEntry Entry, int Count)> _entries = new();

    public void Add(QueryEntry entry)
    {
        _entries.AddOrUpdate(
            entry.Sql,
            addValueFactory: _ => (entry, 1),
            updateValueFactory: (_, existing) => (MergeEntry(existing.Entry, entry), existing.Count + 1));
    }

    private static QueryEntry MergeEntry(QueryEntry first, QueryEntry incoming)
    {
        return new QueryEntry
        {
            Sql = first.Sql,
            ParameterTypes = first.ParameterTypes,
            Parameters = MergeParameters(first.Parameters, incoming.Parameters),
            TestName = first.TestName,
            TestClass = first.TestClass
        };
    }

    private static IReadOnlyList<ParameterMetadata> MergeParameters(
        IReadOnlyList<ParameterMetadata> first,
        IReadOnlyList<ParameterMetadata> second)
    {
        var count = Math.Max(first.Count, second.Count);
        if (count == 0)
        {
            return [];
        }

        var merged = new ParameterMetadata[count];
        for (var i = 0; i < count; i++)
        {
            var a = i < first.Count ? first[i] : null;
            var b = i < second.Count ? second[i] : null;
            merged[i] = MergeParameter(a, b);
        }

        return merged;
    }

    private static ParameterMetadata MergeParameter(ParameterMetadata? a, ParameterMetadata? b)
    {
        if (a is null)
        {
            return b!;
        }

        if (b is null)
        {
            return a;
        }

        return new ParameterMetadata
        {
            Name = a.Name ?? b.Name,
            ClrType = a.ClrType ?? b.ClrType,
            DbType = a.DbType ?? b.DbType,
            StoreType = a.StoreType ?? b.StoreType,
            MaxLength = MergeMaxLength(a.MaxLength, b.MaxLength),
            Precision = a.Precision ?? b.Precision,
            Scale = a.Scale ?? b.Scale,
            IsNullable = a.IsNullable ?? b.IsNullable,
            Size = a.Size ?? b.Size
        };
    }

    private static int? MergeMaxLength(int? a, int? b)
    {
        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        return Math.Max(a.Value, b.Value);
    }

    public IReadOnlyList<QueryEntry> GetAll()
    {
        return _entries.Values
            .Select(v => new QueryEntry
            {
                Sql = v.Entry.Sql,
                ParameterTypes = v.Entry.ParameterTypes,
                Parameters = v.Entry.Parameters,
                ExecutionCount = v.Count,
                TestName = v.Entry.TestName,
                TestClass = v.Entry.TestClass
            })
            .ToList();
    }
}
