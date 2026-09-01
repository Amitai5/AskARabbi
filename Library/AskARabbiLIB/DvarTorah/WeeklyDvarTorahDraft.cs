namespace AskARabbiLIB.DvarTorah;

/// <summary>Contains generator-produced content before it is atomically published.</summary>
public sealed record WeeklyDvarTorahDraft
{
    /// <summary>Gets the maximum supported title length.</summary>
    public const int MaximumTitleCharacters = 160;

    /// <summary>Gets the maximum supported body length.</summary>
    public const int MaximumBodyCharacters = 40_000;

    /// <summary>Initializes a validated weekly Dvar Torah draft.</summary>
    /// <param name="title">Display title.</param>
    /// <param name="body">Plain-text article body.</param>
    /// <param name="generatorVersion">Auditable generator or prompt version, without secrets.</param>
    public WeeklyDvarTorahDraft(string title, string body, string generatorVersion)
    {
        Title = NormalizeRequired(title, MaximumTitleCharacters, nameof(title));
        Body = NormalizeRequired(body, MaximumBodyCharacters, nameof(body));
        GeneratorVersion = NormalizeRequired(generatorVersion, 120, nameof(generatorVersion));
    }

    /// <summary>Gets the display title.</summary>
    public string Title { get; }

    /// <summary>Gets the plain-text article body.</summary>
    public string Body { get; }

    /// <summary>Gets the auditable generator or prompt version.</summary>
    public string GeneratorVersion { get; }

    private static string NormalizeRequired(string value, int maximumCharacters, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-blank value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumCharacters)
        {
            throw new ArgumentException($"The value cannot exceed {maximumCharacters} characters.", parameterName);
        }

        return normalized;
    }
}
