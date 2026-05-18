namespace PactEf.Capture.Utilities;

internal static class ProjectRootLocator
{
    public static string? FindProjectRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);

        while (current != null)
        {
            if (current.GetFiles("*.csproj").Length > 0)
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }
}
