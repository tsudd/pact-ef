using PactEf.Core.Models;

namespace PactEf.Verify.Verification;

internal static partial class ParameterSubstitutor
{
    private static readonly Dictionary<string, string> Literals =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Int16"] = "0",
            ["Int32"] = "0",
            ["Int64"] = "0",
            ["String"] = "''",
            ["AnsiString"] = "''",
            ["AnsiStringFixedLength"] = "''",
            ["StringFixedLength"] = "''",
            ["Boolean"] = "false",
            ["Guid"] = "'00000000-0000-0000-0000-000000000000'",
            ["DateTime"] = "'2000-01-01'",
            ["DateTime2"] = "'2000-01-01'",
            ["Date"] = "'2000-01-01'",
            ["DateTimeOffset"] = "'2000-01-01 00:00:00+00'",
            ["Decimal"] = "0.0",
            ["Double"] = "0.0",
            ["Single"] = "0.0",
            ["Currency"] = "0.0",
            ["VarNumeric"] = "0.0"
        };

    public static string GetLiteral(string dbType) =>
        Literals.GetValueOrDefault(dbType, "null");

    private static string GetLiteral(ParameterMetadata parameter) =>
        GetLiteral(parameter.ClrType ?? parameter.DbType ?? string.Empty);

    /// <param name="valueOverrides">
    /// Optional per-parameter literal SQL text (keyed by parameter index) that takes
    /// precedence over the default type-based literal. Used to drive boundary-value replays.
    /// </param>
    public static string Substitute(
        string sql,
        IReadOnlyList<ParameterMetadata> parameters,
        IReadOnlyDictionary<int, string>? valueOverrides = null)
    {
        if (parameters.Count == 0) return sql;

        string LiteralAt(int index) =>
            valueOverrides is not null && valueOverrides.TryGetValue(index, out var literal)
                ? literal
                : GetLiteral(parameters[index]);

        // Replace $N positional placeholders (PostgreSQL native style) in reverse order
        // to avoid $1 matching $10 etc.
        if (sql.Contains('$'))
        {
            var result = sql;
            for (var i = parameters.Count; i >= 1; i--)
            {
                result = result.Replace($"${i}", LiteralAt(i - 1));
            }
            // If substitution happened, return early
            if (!result.Contains('$'))
                return result;
        }

        // Replace @name Npgsql-style named placeholders in order of appearance
        // EF Core/Npgsql uses @p0, @p1, ... or @__varname_N
        return SubstituteNamedParams(sql, parameters, LiteralAt);
    }

    private static readonly System.Text.RegularExpressions.Regex NamedParamRegex =
        new(@"@\w+", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string SubstituteNamedParams(
        string sql,
        IReadOnlyList<ParameterMetadata> parameters,
        Func<int, string> literalAt)
    {
        var index = 0;
        return NamedParamRegex.Replace(sql, _ =>
        {
            if (index < parameters.Count)
                return literalAt(index++);
            return "null";
        });
    }
}
