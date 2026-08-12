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
            updateValueFactory: (_, existing) => (existing.Entry, existing.Count + 1));
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
