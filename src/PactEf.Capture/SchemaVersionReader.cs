using System.Data.Common;

namespace PactEf.Capture;

internal sealed class SchemaVersionReader
{
    private string? _cached;
    private bool _read;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string?> GetAsync(DbConnection connection)
    {
        if (_read) return _cached;

        await _lock.WaitAsync();
        try
        {
            if (_read) return _cached;

            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT "MigrationId"
                FROM "__EFMigrationsHistory"
                ORDER BY "MigrationId" DESC
                LIMIT 1
                """;

            var result = await cmd.ExecuteScalarAsync();
            _cached = result as string;
            _read = true;
            return _cached;
        }
        catch
        {
            // Table may not exist in non-EF databases — return null silently
            _read = true;
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }
}
