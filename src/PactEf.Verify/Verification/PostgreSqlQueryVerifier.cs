using System.Text.RegularExpressions;
using Npgsql;
using PactEf.Core.Models;

namespace PactEf.Verify.Verification;

internal sealed partial class PostgreSqlQueryVerifier(string connectionString) : IQueryVerifier
{
    // Postgres error codes that indicate schema incompatibility
    private static readonly HashSet<string> SchemaErrorCodes = new()
    {
        "42P01", // undefined_table
        "42703", // undefined_column
        "42883", // undefined_function
        "42804", // datatype_mismatch
        "42601", // syntax_error
        "22001", // string_data_right_truncation
        "23502", // not_null_violation
    };

    // Foreign key violations are expected noise, not a schema-compatibility signal:
    // each query is replayed in isolation without seeding rows in referenced tables,
    // so any mutating INSERT/UPDATE into a table with a FK will violate it on a
    // freshly migrated (empty) database regardless of schema compatibility.
    private const string ForeignKeyViolation = "23503";

    [GeneratedRegex(@"^\s*(INSERT|UPDATE|DELETE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MutatingStatementRegex();

    public async Task<VerificationResult> VerifyAsync(
        string sql,
        IReadOnlyList<ParameterMetadata> parameters,
        VerificationMode mode,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        var isMutating = MutatingStatementRegex().IsMatch(sql);
        var discoveredMaxLengths = await ResolveDiscoveredMaxLengthsAsync(conn, sql, parameters, cancellationToken);
        var variants = ReplayVariantMatrixBuilder.Build(sql, parameters, discoveredMaxLengths);

        foreach (var variant in variants)
        {
            try
            {
                if (isMutating)
                    await RunMutatingVariantAsync(conn, variant.Sql, cancellationToken);
                else if (mode == VerificationMode.Explain)
                    await RunExplainAsync(conn, variant.Sql, cancellationToken);
                else
                    await RunFullExecutionAsync(conn, variant.Sql, cancellationToken);
            }
            catch (PostgresException ex) when (SchemaErrorCodes.Contains(ex.SqlState ?? ""))
            {
                return BuildFailure(ex.MessageText, ex.SqlState, variant, parameters);
            }
            catch (PostgresException ex) when (ex.SqlState == ForeignKeyViolation)
            {
                // Not a schema-compatibility failure; skip this variant.
            }
            catch (Exception ex)
            {
                return BuildFailure(ex.Message, null, variant, parameters);
            }
        }

        return VerificationResult.Ok();
    }

    private static VerificationResult BuildFailure(
        string message, string? errorCode, ReplayVariant variant, IReadOnlyList<ParameterMetadata> parameters)
    {
        var parameter = variant.ParameterName is not null
            ? parameters.FirstOrDefault(p => p.Name == variant.ParameterName)
            : null;

        return VerificationResult.Fail(
            message,
            errorCode,
            parameterName: variant.ParameterName,
            variantKind: FormatVariantKind(variant.Kind),
            testedLength: variant.TestedLength,
            consumerMaxLength: parameter?.MaxLength,
            databaseMaxLength: variant.BoundSource == BoundLengthSource.Database ? variant.TestedLength : null);
    }

    private static string FormatVariantKind(ReplayVariantKind kind) => kind switch
    {
        ReplayVariantKind.Baseline => "baseline",
        ReplayVariantKind.BoundaryMaxLength => "boundary-max-length",
        ReplayVariantKind.BoundaryNull => "boundary-null",
        _ => kind.ToString()
    };

    // Best-effort: when a parameter has no consumer-declared MaxLength, look up the live
    // schema's column length so the boundary variant reflects a real database constraint
    // instead of skipping the check entirely. Never fails the run if resolution can't happen.
    private static async Task<IReadOnlyDictionary<int, int>?> ResolveDiscoveredMaxLengthsAsync(
        NpgsqlConnection conn, string sql, IReadOnlyList<ParameterMetadata> parameters, CancellationToken ct)
    {
        if (parameters.All(p => p.MaxLength is not null))
            return null;

        IReadOnlyDictionary<string, (string Table, string Column)> columnRefs;
        try
        {
            columnRefs = SqlColumnReferenceResolver.Resolve(sql);
        }
        catch
        {
            return null;
        }

        Dictionary<int, int>? discovered = null;

        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            if (parameter.MaxLength is not null || parameter.Name is null)
                continue;

            if (!columnRefs.TryGetValue(parameter.Name, out var reference))
                continue;

            int? length;
            try
            {
                length = await DatabaseColumnLengthResolver.GetMaxLengthAsync(conn, reference.Table, reference.Column, ct);
            }
            catch
            {
                length = null;
            }

            if (length is int value)
            {
                discovered ??= new Dictionary<int, int>();
                discovered[i] = value;
            }
        }

        return discovered;
    }

    private static async Task RunExplainAsync(NpgsqlConnection conn, string substitutedSql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand($"EXPLAIN {substitutedSql}", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task RunFullExecutionAsync(NpgsqlConnection conn, string substitutedSql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(substitutedSql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // Mutating statements are executed for real (not EXPLAIN'd) so that constraint
    // violations like string_data_right_truncation surface, but always rolled back so the
    // verification run never leaves data behind.
    private static async Task RunMutatingVariantAsync(NpgsqlConnection conn, string substitutedSql, CancellationToken ct)
    {
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(substitutedSql, conn, tx);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            await tx.RollbackAsync(ct);
        }
    }
}
