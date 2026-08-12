using Npgsql;

namespace PactEf.Verify.Verification;

/// <summary>
/// Discovers a column's length bound directly from the live database schema
/// (information_schema.columns.character_maximum_length) for use when the captured
/// snapshot has no consumer-declared MaxLength. This reports a database capability,
/// not a proven consumer contract.
/// </summary>
internal static class DatabaseColumnLengthResolver
{
    public static async Task<int?> GetMaxLengthAsync(
        NpgsqlConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT character_maximum_length FROM information_schema.columns " +
            "WHERE table_name = @table AND column_name = @column",
            connection);
        cmd.Parameters.AddWithValue("table", table);
        cmd.Parameters.AddWithValue("column", column);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result as int?;
    }
}
