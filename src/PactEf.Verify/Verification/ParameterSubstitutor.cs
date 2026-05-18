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

    public static string Substitute(string sql, IReadOnlyList<string> parameterTypes)
    {
        if (parameterTypes.Count == 0) return sql;

        // Replace $N positional placeholders (PostgreSQL native style) in reverse order
        // to avoid $1 matching $10 etc.
        if (sql.Contains('$'))
        {
            var result = sql;
            for (var i = parameterTypes.Count; i >= 1; i--)
            {
                var literal = GetLiteral(parameterTypes[i - 1]);
                result = result.Replace($"${i}", literal);
            }
            // If substitution happened, return early
            if (!result.Contains('$'))
                return result;
        }

        // Replace @name Npgsql-style named placeholders in order of appearance
        // EF Core/Npgsql uses @p0, @p1, ... or @__varname_N
        return SubstituteNamedParams(sql, parameterTypes);
    }

    private static readonly System.Text.RegularExpressions.Regex NamedParamRegex =
        new(@"@\w+", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string SubstituteNamedParams(string sql, IReadOnlyList<string> parameterTypes)
    {
        var index = 0;
        return NamedParamRegex.Replace(sql, _ =>
        {
            if (index < parameterTypes.Count)
                return GetLiteral(parameterTypes[index++]);
            return "null";
        });
    }
}
