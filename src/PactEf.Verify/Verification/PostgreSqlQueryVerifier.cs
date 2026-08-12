using Npgsql;
using PactEf.Core.Models;

namespace PactEf.Verify.Verification;

internal sealed class PostgreSqlQueryVerifier(string connectionString) : IQueryVerifier
{
    // Postgres error codes that indicate schema incompatibility
    private static readonly HashSet<string> SchemaErrorCodes = new()
    {
        "42P01", // undefined_table
        "42703", // undefined_column
        "42883", // undefined_function
        "42804", // datatype_mismatch
        "42601", // syntax_error
    };

    public async Task<VerificationResult> VerifyAsync(
        string sql,
        IReadOnlyList<ParameterMetadata> parameters,
        VerificationMode mode,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        try
        {
            if (mode == VerificationMode.Explain)
                return await RunExplainAsync(conn, sql, parameters, cancellationToken);
            else
                return await RunFullExecutionAsync(conn, sql, parameters, cancellationToken);
        }
        catch (PostgresException ex) when (SchemaErrorCodes.Contains(ex.SqlState ?? ""))
        {
            return VerificationResult.Fail(ex.MessageText, ex.SqlState);
        }
        catch (Exception ex)
        {
            return VerificationResult.Fail(ex.Message);
        }
    }

    private static async Task<VerificationResult> RunExplainAsync(
        NpgsqlConnection conn,
        string sql,
        IReadOnlyList<ParameterMetadata> parameters,
        CancellationToken ct)
    {
        var substituted = ParameterSubstitutor.Substitute(sql, parameters);
        var explainSql = $"EXPLAIN {substituted}";

        await using var cmd = new NpgsqlCommand(explainSql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
        return VerificationResult.Ok();
    }

    private static async Task<VerificationResult> RunFullExecutionAsync(
        NpgsqlConnection conn,
        string sql,
        IReadOnlyList<ParameterMetadata> parameters,
        CancellationToken ct)
    {
        var substituted = ParameterSubstitutor.Substitute(sql, parameters);
        await using var cmd = new NpgsqlCommand(substituted, conn);
        await cmd.ExecuteNonQueryAsync(ct);
        return VerificationResult.Ok();
    }
}
