namespace AskARabbiPrototype;

internal sealed record ManifestLocation(string ManifestPath, string RepositoryRoot);

internal static class ManifestPathResolver
{
    private static readonly string DefaultManifestPath = Path.Combine("Data", "NormalizedData", "Sefaria", "Metadata", "document-manifest.json");

    public static ManifestLocation Resolve(string? manifestPath, string? repositoryRoot)
    {
        if (!string.IsNullOrWhiteSpace(repositoryRoot))
        {
            var resolvedRoot = Path.GetFullPath(repositoryRoot);
            var resolvedManifest = string.IsNullOrWhiteSpace(manifestPath) ? Path.Combine(resolvedRoot, DefaultManifestPath) : Path.GetFullPath(manifestPath);
            EnsureManifestExists(resolvedManifest);
            return new ManifestLocation(resolvedManifest, resolvedRoot);
        }

        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            var resolvedManifest = Path.GetFullPath(manifestPath);
            EnsureManifestExists(resolvedManifest);
            var inferredRoot = FindRepositoryRoot(Path.GetDirectoryName(resolvedManifest)!, resolvedManifest);
            return new ManifestLocation(resolvedManifest, inferredRoot);
        }

        foreach (var startingPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var ancestor in EnumerateAncestors(startingPath))
            {
                var candidate = Path.Combine(ancestor, DefaultManifestPath);
                if (File.Exists(candidate))
                {
                    return new ManifestLocation(Path.GetFullPath(candidate), Path.GetFullPath(ancestor));
                }
            }
        }

        throw new FileNotFoundException($"Could not locate {DefaultManifestPath}. Use --manifest and optionally --repository-root.");
    }

    private static string FindRepositoryRoot(string startingPath, string manifestPath)
    {
        foreach (var ancestor in EnumerateAncestors(startingPath))
        {
            if (PathsEqual(Path.Combine(ancestor, DefaultManifestPath), manifestPath) || Directory.Exists(Path.Combine(ancestor, ".git")))
            {
                return Path.GetFullPath(ancestor);
            }
        }
        throw new DirectoryNotFoundException("Could not infer the repository root for the manifest. Supply --repository-root.");
    }

    private static IEnumerable<string> EnumerateAncestors(string startingPath)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startingPath));
        while (directory is not null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }

    private static bool PathsEqual(string first, string second) => string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void EnsureManifestExists(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The document manifest does not exist.", path);
        }
    }
}
