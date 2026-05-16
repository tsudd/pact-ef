namespace PactEf.Verify;

public sealed class SnapshotSource
{
    private readonly string? _folder;
    private readonly string? _envVariable;

    private SnapshotSource(string? folder, string? envVariable)
    {
        _folder = folder;
        _envVariable = envVariable;
    }

    public bool IsEnvVariable => _envVariable is not null;

    public static SnapshotSource FromFolder(string path) => new(path, null);

    public static SnapshotSource FromEnvVariable(string envVariableName) =>
        new(null, envVariableName);

    public IReadOnlyList<string> ResolvePaths()
    {
        if (_folder is not null)
            return [_folder];

        if (_envVariable is not null)
        {
            var value = Environment.GetEnvironmentVariable(_envVariable);
            if (string.IsNullOrWhiteSpace(value))
                return [];

            return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return [];
    }
}
