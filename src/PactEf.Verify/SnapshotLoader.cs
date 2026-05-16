using PactEf.Core.Models;
using PactEf.Core.Serialization;

namespace PactEf.Verify;

internal sealed class SnapshotLoader(IReadOnlyList<SnapshotSource> sources)
{
    public async Task<IReadOnlyList<SnapshotFile>> LoadAllAsync()
    {
        // Load from FromFolder sources first, then FromEnvVariable sources.
        // EnvVariable wins for same consumerName.
        var result = new Dictionary<string, SnapshotFile>(StringComparer.OrdinalIgnoreCase);

        var folderSources = sources.Where(s => !s.IsEnvVariable).ToList();
        var envSources = sources.Where(s => s.IsEnvVariable).ToList();

        foreach (var source in folderSources)
            await LoadFromPathsAsync(source.ResolvePaths(), result, overwrite: false);

        foreach (var source in envSources)
            await LoadFromPathsAsync(source.ResolvePaths(), result, overwrite: true);

        return result.Values.ToList();
    }

    private static async Task LoadFromPathsAsync(
        IReadOnlyList<string> paths,
        Dictionary<string, SnapshotFile> result,
        bool overwrite)
    {
        foreach (var path in paths)
        {
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"Snapshot source directory not found: {path}");

            var files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var snapshot = await SnapshotSerializer.ReadFromFileAsync(file);
                if (overwrite || !result.ContainsKey(snapshot.ConsumerName))
                    result[snapshot.ConsumerName] = snapshot;
            }
        }
    }
}
