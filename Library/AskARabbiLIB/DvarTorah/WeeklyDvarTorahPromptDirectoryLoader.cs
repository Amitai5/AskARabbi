namespace AskARabbiLIB.DvarTorah;

/// <summary>Loads the complete weekly Dvar Torah prompt contract from a version-controlled directory.</summary>
public static class WeeklyDvarTorahPromptDirectoryLoader
{
    /// <summary>Loads and validates all weekly prompt and schema files.</summary>
    /// <param name="directoryPath">Directory containing the expected prompt assets.</param>
    /// <returns>The validated prompt set.</returns>
    public static WeeklyDvarTorahPromptSet Load(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        var prompts = new WeeklyDvarTorahPromptSet
        {
            ResearchSystemPrompt = Read(directoryPath, "research-system.txt"),
            ResearchJsonSchema = Read(directoryPath, "research.schema.json"),
            DraftSystemPrompt = Read(directoryPath, "draft-system.txt"),
            DraftJsonSchema = Read(directoryPath, "draft.schema.json"),
            ReviewSystemPrompt = Read(directoryPath, "review-system.txt"),
            ReviewJsonSchema = Read(directoryPath, "review.schema.json"),
            RepairPrompt = Read(directoryPath, "repair.txt"),
        };
        prompts.Validate();
        return prompts;
    }

    private static string Read(string directoryPath, string fileName)
    {
        var path = Path.Combine(directoryPath, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Weekly Dvar Torah prompt asset '{fileName}' was not found.", path);
        }

        return File.ReadAllText(path);
    }
}
