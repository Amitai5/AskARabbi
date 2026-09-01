namespace AskARabbiLIB.DvarTorah;

/// <summary>Represents one immutable, published weekly Dvar Torah.</summary>
public sealed record WeeklyDvarTorahArticle
{
    /// <summary>Initializes a published weekly Dvar Torah.</summary>
    /// <param name="week">Reading week represented by the article.</param>
    /// <param name="title">Display title.</param>
    /// <param name="body">Plain-text article body.</param>
    /// <param name="generatorVersion">Auditable generator or prompt version.</param>
    /// <param name="generatedAtUtc">UTC time at which generation completed.</param>
    /// <param name="publishedAtUtc">UTC time at which publication completed.</param>
    public WeeklyDvarTorahArticle(WeeklyDvarTorahWeek week, string title, string body, string generatorVersion, DateTimeOffset generatedAtUtc, DateTimeOffset publishedAtUtc) : this(week, title, body, generatorVersion, generatedAtUtc, publishedAtUtc, null)
    {
    }

    /// <summary>Initializes a published weekly Dvar Torah with auditable metadata.</summary>
    /// <param name="week">Reading week represented by the article.</param>
    /// <param name="title">Display title.</param>
    /// <param name="body">Plain-text article body.</param>
    /// <param name="generatorVersion">Auditable generator or prompt version.</param>
    /// <param name="generatedAtUtc">UTC time at which generation completed.</param>
    /// <param name="publishedAtUtc">UTC time at which publication completed.</param>
    /// <param name="metadata">Searchable source, grounding, and safety metadata.</param>
    public WeeklyDvarTorahArticle(WeeklyDvarTorahWeek week, string title, string body, string generatorVersion, DateTimeOffset generatedAtUtc, DateTimeOffset publishedAtUtc, WeeklyDvarTorahContentMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(week);
        var draft = new WeeklyDvarTorahDraft(title, body, generatorVersion, metadata);
        if (publishedAtUtc < generatedAtUtc)
        {
            throw new ArgumentException("Publication cannot precede generation.", nameof(publishedAtUtc));
        }

        Week = week;
        Title = draft.Title;
        Body = draft.Body;
        GeneratorVersion = draft.GeneratorVersion;
        Metadata = draft.Metadata;
        GeneratedAtUtc = generatedAtUtc.ToUniversalTime();
        PublishedAtUtc = publishedAtUtc.ToUniversalTime();
    }

    /// <summary>Gets the reading week represented by the article.</summary>
    public WeeklyDvarTorahWeek Week { get; }

    /// <summary>Gets the display title.</summary>
    public string Title { get; }

    /// <summary>Gets the plain-text article body.</summary>
    public string Body { get; }

    /// <summary>Gets the auditable generator or prompt version.</summary>
    public string GeneratorVersion { get; }

    /// <summary>Gets searchable source, grounding, and safety metadata when available.</summary>
    public WeeklyDvarTorahContentMetadata? Metadata { get; }

    /// <summary>Gets the UTC generation-completion time.</summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary>Gets the UTC publication time.</summary>
    public DateTimeOffset PublishedAtUtc { get; }
}
