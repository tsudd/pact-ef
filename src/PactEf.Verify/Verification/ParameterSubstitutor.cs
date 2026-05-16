namespace PactEf.Verify.Verification;

internal static class ParameterSubstitutor
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
            ["VarNumeric"] = "0.0",
        };

    public static string GetLiteral(string dbType) =>
        Literals.TryGetValue(dbType, out var lit) ? lit : "null";

    public static string Substitute(string sql, IReadOnlyList<string> parameterTypes)
    {
        if (parameterTypes.Count == 0) return sql;

        var result = sql;
        // Replace $N placeholders in reverse order to avoid $1 matching $10 etc.
        for (var i = parameterTypes.Count; i >= 1; i--)
        {
            var literal = GetLiteral(parameterTypes[i - 1]);
            result = result.Replace($"${i}", literal);
        }
        return result;
    }
}
