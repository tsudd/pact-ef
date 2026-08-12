using System.Text.RegularExpressions;

namespace PactEf.Verify.Verification;

/// <summary>
/// Best-effort text-based mapping from SQL parameter placeholders (e.g. "@p0") back to the
/// table/column they target, mirroring PactEf.Capture's ModelParameterMetadataResolver but
/// without an EF model — Verify only has the replayed SQL text and a live connection.
/// </summary>
internal static partial class SqlColumnReferenceResolver
{
    [GeneratedRegex("(?:FROM|JOIN|UPDATE|INTO)\\s+\"(?<table>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex TableReferenceRegex();

    [GeneratedRegex(
        "INSERT\\s+INTO\\s+\"(?<table>[^\"]+)\"\\s*\\((?<cols>[^)]*)\\)\\s*VALUES\\s*\\((?<params>[^)]*)\\)",
        RegexOptions.IgnoreCase)]
    private static partial Regex InsertRegex();

    [GeneratedRegex("\"(?<col>[^\"]+)\"\\s*=\\s*(?<param>@[A-Za-z0-9_]+)")]
    private static partial Regex EqualityRegex();

    public static IReadOnlyDictionary<string, (string Table, string Column)> Resolve(string sql)
    {
        var resolved = new Dictionary<string, (string Table, string Column)>();

        foreach (Match insert in InsertRegex().Matches(sql))
        {
            var table = insert.Groups["table"].Value;
            var columns = SplitQuotedIdentifiers(insert.Groups["cols"].Value);
            var paramNames = SplitParamNames(insert.Groups["params"].Value);

            for (var i = 0; i < Math.Min(columns.Count, paramNames.Count); i++)
                resolved[paramNames[i]] = (table, columns[i]);
        }

        var fallbackTable = TableReferenceRegex().Match(sql) is { Success: true } tableMatch
            ? tableMatch.Groups["table"].Value
            : null;

        if (fallbackTable is not null)
        {
            foreach (Match equality in EqualityRegex().Matches(sql))
            {
                var column = equality.Groups["col"].Value;
                var paramName = equality.Groups["param"].Value;
                resolved.TryAdd(paramName, (fallbackTable, column));
            }
        }

        return resolved;
    }

    private static List<string> SplitQuotedIdentifiers(string csv) =>
        csv.Split(',')
            .Select(s => s.Trim().Trim('"'))
            .Where(s => s.Length > 0)
            .ToList();

    private static List<string> SplitParamNames(string csv) =>
        csv.Split(',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
}
