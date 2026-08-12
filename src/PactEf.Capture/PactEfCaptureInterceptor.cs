using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using PactEf.Capture.TestContext;
using PactEf.Capture.Utilities;
using PactEf.Core.Models;
using PactEf.Core.Serialization;

namespace PactEf.Capture;

public sealed class PactEfCaptureInterceptor : DbCommandInterceptor
{
    private static readonly HashSet<string> DmlPrefixes =
        new(StringComparer.OrdinalIgnoreCase) { "SELECT", "INSERT", "UPDATE", "DELETE" };

    private static readonly HashSet<string> InfraPatterns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "__EFMigrationsHistory",
            "__EFMigrationsLock"
        };

    private readonly CaptureOptions _options;
    private readonly QueryBuffer _buffer = new();
    private readonly SchemaVersionReader _schemaVersionReader = new();
    private DbConnection? _lastConnection;

    internal PactEfCaptureInterceptor(CaptureOptions options)
    {
        _options = options;
        CaptureRegistry.Register(this);
    }

    internal string ConsumerName => _options.ConsumerName;

    /// <summary>
    /// Creates a real interceptor when the environment guard is satisfied,
    /// otherwise returns a no-op NullCaptureInterceptor.
    /// </summary>
    public static DbCommandInterceptor Create(Action<CaptureOptions> configure)
    {
        var options = new CaptureOptions { ConsumerName = string.Empty };
        configure(options);

        if (!EnvironmentGuard.IsActive(options.DisableEnvVariable))
            return NullCaptureInterceptor.Instance;

        return new PactEfCaptureInterceptor(options);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await CaptureAsync(command, eventData);
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await CaptureAsync(command, eventData);
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private async Task CaptureAsync(DbCommand command, CommandEventData eventData)
    {
        var sql = command.CommandText;

        if (!IsDml(sql) || IsInfraQuery(sql))
            return;

        // Lazy schema version capture on first DML command
        if (command.Connection is not null)
        {
            _lastConnection = command.Connection;
            await _schemaVersionReader.GetAsync(command.Connection);
        }

        var dbParameters = command.Parameters.Cast<DbParameter>().ToList();
        var paramTypes = dbParameters.Select(p => p.DbType.ToString()).ToList();
        var parameters = ModelParameterMetadataResolver.Enrich(
            dbParameters.Select(ToParameterMetadata).ToList(), sql, eventData.Context?.Model);

        var entry = new QueryEntry
        {
            Sql = sql,
            ParameterTypes = paramTypes,
            Parameters = parameters,
            TestName = PactEfTestContext.Current,
            TestClass = PactEfTestContext.CurrentClass
        };

        _buffer.Add(entry);
    }

    internal static ParameterMetadata ToParameterMetadata(DbParameter parameter)
    {
        return new ParameterMetadata
        {
            Name = parameter.ParameterName,
            ClrType = parameter.Value?.GetType().Name,
            DbType = parameter.DbType.ToString(),
            StoreType = parameter is NpgsqlParameter npgsqlParameter
                ? npgsqlParameter.NpgsqlDbType.ToString()
                : null,
            IsNullable = parameter.IsNullable,
            Size = parameter.Size == 0 ? null : parameter.Size
        };
    }

    private static bool IsDml(string sql)
    {
        var trimmed = sql.TrimStart();
        return DmlPrefixes.Any(p =>
            trimmed.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsInfraQuery(string sql) =>
        InfraPatterns.Any(p => sql.Contains(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Flushes this interceptor's buffer alone to a snapshot file.
    /// </summary>
    public Task FlushAsync() => FlushMergedAsync([this]);

    /// <summary>
    /// Merges buffers from all provided interceptors (same consumer) into one snapshot file.
    /// Uses the first interceptor with a known connection for schema version detection.
    /// </summary>
    internal async Task FlushMergedAsync(IReadOnlyList<PactEfCaptureInterceptor> all)
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot(AppContext.BaseDirectory);
        if (projectRoot is null)
            throw new InvalidOperationException(
                "Could not locate project root (.csproj) from AppContext.BaseDirectory.");

        var outputPath = Path.Combine(
            projectRoot, "pactef-snapshots", $"{_options.ConsumerName}.json");

        // Merge all queries from all interceptors for this consumer
        var mergedBuffer = new QueryBuffer();
        foreach (var interceptor in all)
        {
            foreach (var entry in interceptor._buffer.GetAll())
                mergedBuffer.Add(entry);
        }

        // Use the first interceptor that has a connection for schema version
        var connectionSource = all.FirstOrDefault(i => i._lastConnection is not null);
        string? schemaVersion = connectionSource is not null
            ? await connectionSource._schemaVersionReader.GetAsync(connectionSource._lastConnection!)
            : null;

        var snapshot = new SnapshotFile
        {
            ConsumerName = _options.ConsumerName,
            CapturedAt = DateTimeOffset.UtcNow,
            DbSchemaVersion = schemaVersion,
            Queries = mergedBuffer.GetAll()
        };

        await SnapshotSerializer.WriteToFileAsync(snapshot, outputPath);
    }
}
