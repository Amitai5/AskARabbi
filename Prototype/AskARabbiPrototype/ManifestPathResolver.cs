namespace AskARabbiPrototype;

internal static class ManifestPathResolver
{
    private static readonly string DefaultManifestPath = Path.Combine("Data", "NormalizedData", "Sefaria", "Metadata", "document-manifest.json");

    public static ManifestLocation Resolve(string? manifestPath, string? repositoryRoot, string? indexPath = null)
    {
        if (!string.IsNullOrWhiteSpace(repositoryRoot))
        {
            var resolvedRoot = Path.GetFullPath(repositoryRoot);
            var resolvedManifest = ResolveFromRoot(resolvedRoot, manifestPath, DefaultManifestPath);
            EnsureManifestExists(resolvedManifest);
            return CreateLocation(resolvedManifest, resolvedRoot, indexPath);
        }

        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            var resolvedManifest = Path.GetFullPath(manifestPath);
            EnsureManifestExists(resolvedManifest);
            var manifestDirectory = Path.GetDirectoryName(resolvedManifest) ?? throw new DirectoryNotFoundException("The manifest path does not have a parent directory.");
            var inferredRoot = FindRepositoryRoot(manifestDirectory, resolvedManifest);
            return CreateLocation(resolvedManifest, inferredRoot, indexPath);
        }

        foreach (var startingPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var ancestor in EnumerateAncestors(startingPath))
            {
                var candidate = Path.Combine(ancestor, DefaultManifestPath);
                if (File.Exists(candidate))
                {
                    return CreateLocation(Path.GetFullPath(candidate), Path.GetFullPath(ancestor), indexPath);
                }
            }
        }

        throw new FileNotFoundException($"Could not locate {DefaultManifestPath}. Use --manifest and optionally --repository-root.");
    }

    private static ManifestLocation CreateLocation(string manifestPath, string repositoryRoot, string? indexPath)
    {
        var resolvedIndex = ResolveFromRoot(repositoryRoot, indexPath, AskARabbiLIB.Retrieval.SourceIndexBuilder.DefaultRelativePath);
        return new ManifestLocation(Path.GetFullPath(manifestPath), Path.GetFullPath(repositoryRoot), resolvedIndex);
    }

    private static string ResolveFromRoot(string repositoryRoot, string? configuredPath, string defaultRelativePath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath) ? defaultRelativePath : configuredPath;
        var platformPath = path.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.IsPathRooted(platformPath) ? platformPath : Path.Combine(repositoryRoot, platformPath));
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
