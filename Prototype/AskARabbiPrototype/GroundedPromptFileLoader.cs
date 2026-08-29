using AskARabbiLIB.Grounding;

namespace AskARabbiPrototype;

internal static class GroundedPromptFileLoader
{
    private const string PromptDirectoryName = "Prompts";

    internal static GroundedPromptSet Load(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var promptDirectory = ResolvePromptDirectory(repositoryRoot);
        return GroundedPromptDirectoryLoader.Load(promptDirectory);
    }

    private static string ResolvePromptDirectory(string repositoryRoot)
    {
        var repositoryDirectory = Path.Combine(repositoryRoot, "Prototype", PromptDirectoryName);
        if (Directory.Exists(repositoryDirectory))
        {
            return repositoryDirectory;
        }

        var outputDirectory = Path.Combine(AppContext.BaseDirectory, PromptDirectoryName);
        if (Directory.Exists(outputDirectory))
        {
            return outputDirectory;
        }
        throw new DirectoryNotFoundException($"AI prompt directory was not found at '{repositoryDirectory}' or '{outputDirectory}'.");
    }
}
