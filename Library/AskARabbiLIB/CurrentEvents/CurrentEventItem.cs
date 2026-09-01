namespace AskARabbiLIB.CurrentEvents;

/// <summary>Represents bounded metadata from one freely accessible current-events feed item.</summary>
public sealed record CurrentEventItem
{
    /// <summary>Initializes one validated current-events item.</summary>
    /// <param name="publisher">Publisher or issuing organization.</param>
    /// <param name="category">Configured subject category.</param>
    /// <param name="title">Published headline or release title.</param>
    /// <param name="summary">Bounded feed-provided summary.</param>
    /// <param name="sourceUrl">Public HTTPS article or release URL.</param>
    /// <param name="publishedAtUtc">Source publication time.</param>
    /// <param name="retrievedAtUtc">Feed retrieval time.</param>
    public CurrentEventItem(string publisher, string category, string title, string summary, string sourceUrl, DateTimeOffset publishedAtUtc, DateTimeOffset retrievedAtUtc)
    {
        Publisher = NormalizeRequired(publisher, 200, nameof(publisher));
        Category = NormalizeRequired(category, 80, nameof(category));
        Title = NormalizeRequired(title, 400, nameof(title));
        Summary = NormalizeRequired(summary, 1_200, nameof(summary));
        SourceUrl = NormalizeHttpsUrl(sourceUrl);
        PublishedAtUtc = publishedAtUtc.ToUniversalTime();
        RetrievedAtUtc = retrievedAtUtc.ToUniversalTime();
        if (PublishedAtUtc > RetrievedAtUtc.AddDays(1))
        {
            throw new ArgumentException("A current-events item cannot be published more than one day after retrieval.", nameof(publishedAtUtc));
        }
    }

    /// <summary>Gets the publisher or issuing organization.</summary>
    public string Publisher { get; }

    /// <summary>Gets the configured subject category.</summary>
    public string Category { get; }

    /// <summary>Gets the published headline or release title.</summary>
    public string Title { get; }

    /// <summary>Gets the bounded feed-provided summary.</summary>
    public string Summary { get; }

    /// <summary>Gets the public HTTPS source URL.</summary>
    public string SourceUrl { get; }

    /// <summary>Gets the source publication time.</summary>
    public DateTimeOffset PublishedAtUtc { get; }

    /// <summary>Gets the feed retrieval time.</summary>
    public DateTimeOffset RetrievedAtUtc { get; }

    private static string NormalizeHttpsUrl(string value)
    {
        var normalized = NormalizeRequired(value, 2_048, nameof(value));
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("A current-events source URL must be an absolute HTTPS URL.", nameof(value));
        }

        return uri.AbsoluteUri;
    }

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
