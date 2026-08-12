using PactEf.Verify.Verification;

namespace PactEf.Verify;

public static class PactEfVerifier
{
    public static async Task VerifyAllAsync(
        Action<VerifyOptions> configure,
        CancellationToken cancellationToken = default)
    {
        var options = new VerifyOptions { ConnectionString = string.Empty };
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new InvalidOperationException("ConnectionString must be set in VerifyOptions.");

        var loader = new SnapshotLoader(options.SnapshotSources);
        var snapshots = await loader.LoadAllAsync();

        IQueryVerifier verifier = options.Provider switch
        {
            DbProvider.PostgreSql => new PostgreSqlQueryVerifier(options.ConnectionString),
            _ => throw new NotSupportedException($"Provider {options.Provider} is not supported.")
        };

        // Read the current schema version from the target database
        string? currentSchemaVersion = await ReadCurrentSchemaVersionAsync(
            options.ConnectionString, cancellationToken);

        var failures = new List<QueryFailure>();

        foreach (var snapshot in snapshots)
        {
            // Deduplicate by SQL before verifying
            var uniqueQueries = snapshot.Queries
                .GroupBy(q => q.Sql, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            foreach (var query in uniqueQueries)
            {
                var result = await verifier.VerifyAsync(
                    query.Sql,
                    query.Parameters,
                    options.DefaultMode,
                    cancellationToken);

                if (!result.Success)
                {
                    failures.Add(new QueryFailure(
                        ConsumerName: snapshot.ConsumerName,
                        Sql: query.Sql,
                        TestName: query.TestName,
                        ErrorMessage: result.ErrorMessage ?? "Unknown error",
                        ErrorCode: result.PostgresErrorCode,
                        CapturedSchemaVersion: snapshot.DbSchemaVersion,
                        CurrentSchemaVersion: currentSchemaVersion));
                }
            }
        }

        if (failures.Count > 0)
        {
            var report = FailureReport.Format(failures);
            throw new PactEfVerificationException(report, failures);
        }
    }

    private static async Task<string?> ReadCurrentSchemaVersionAsync(
        string connectionString,
        CancellationToken ct)
    {
        try
        {
            await using var conn = new Npgsql.NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT "MigrationId"
                FROM "__EFMigrationsHistory"
                ORDER BY "MigrationId" DESC
                LIMIT 1
                """;
            var result = await cmd.ExecuteScalarAsync(ct);
            return result as string;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class PactEfVerificationException(string message, IReadOnlyList<QueryFailure> failures)
    : Exception(message)
{
    public IReadOnlyList<QueryFailure> Failures { get; } = failures;
}
