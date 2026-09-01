namespace AskARabbiLIB.DvarTorah;

/// <summary>Records one bounded, inspectable source used to create a weekly Dvar Torah.</summary>
public sealed record WeeklyDvarTorahSource
{
    /// <summary>Initializes validated source provenance.</summary>
    /// <param name="sourceId">Opaque source identifier used in the generated article.</param>
    /// <param name="kind">Source classification.</param>
    /// <param name="title">Source or article title.</param>
    /// <param name="publisher">Edition, publisher, or issuing organization.</param>
    /// <param name="sourceUrl">Canonical public HTTPS source URL.</param>
    /// <param name="excerpt">Bounded evidence text supplied to the model.</param>
    /// <param name="retrievedAtUtc">UTC time at which the evidence was retrieved.</param>
    /// <param name="canonicalReference">Canonical Torah or document reference, when applicable.</param>
    /// <param name="publishedAtUtc">Original publication time, when supplied by the source.</param>
    /// <param name="license">Known reuse license or attribution notice, when applicable.</param>
    public WeeklyDvarTorahSource(string sourceId, WeeklyDvarTorahSourceKind kind, string title, string publisher, string sourceUrl, string excerpt, DateTimeOffset retrievedAtUtc, string? canonicalReference = null, DateTimeOffset? publishedAtUtc = null, string? license = null)
    {
        SourceId = NormalizeIdentifier(sourceId);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Kind = kind;
        Title = NormalizeRequired(title, 300, nameof(title));
        Publisher = NormalizeRequired(publisher, 200, nameof(publisher));
        SourceUrl = NormalizeHttpsUrl(sourceUrl);
        Excerpt = NormalizeRequired(excerpt, 2_000, nameof(excerpt));
        RetrievedAtUtc = retrievedAtUtc.ToUniversalTime();
        CanonicalReference = NormalizeOptional(canonicalReference, 240, nameof(canonicalReference));
        PublishedAtUtc = publishedAtUtc?.ToUniversalTime();
        License = NormalizeOptional(license, 200, nameof(license));
        if (PublishedAtUtc > RetrievedAtUtc.AddDays(1))
        {
            throw new ArgumentException("A source publication time cannot be more than one day after retrieval.", nameof(publishedAtUtc));
        }
        if (kind == WeeklyDvarTorahSourceKind.Torah && CanonicalReference is null)
        {
            throw new ArgumentException("A Torah source requires a canonical reference.", nameof(canonicalReference));
        }
    }

    /// <summary>Gets the opaque evidence identifier.</summary>
    public string SourceId { get; }

    /// <summary>Gets the source classification.</summary>
    public WeeklyDvarTorahSourceKind Kind { get; }

    /// <summary>Gets the source or article title.</summary>
    public string Title { get; }

    /// <summary>Gets the edition, publisher, or issuing organization.</summary>
    public string Publisher { get; }

    /// <summary>Gets the canonical public source URL.</summary>
    public string SourceUrl { get; }

    /// <summary>Gets the bounded evidence text supplied to generation.</summary>
    public string Excerpt { get; }

    /// <summary>Gets the evidence retrieval time.</summary>
    public DateTimeOffset RetrievedAtUtc { get; }

    /// <summary>Gets the canonical textual reference, when applicable.</summary>
    public string? CanonicalReference { get; }

    /// <summary>Gets the original source publication time, when available.</summary>
    public DateTimeOffset? PublishedAtUtc { get; }

    /// <summary>Gets the known license or attribution notice, when available.</summary>
    public string? License { get; }

    private static string NormalizeIdentifier(string value)
    {
        var normalized = NormalizeRequired(value, 64, nameof(value));
        if (normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new ArgumentException("A source identifier may contain only ASCII letters, digits, underscores, or hyphens.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeHttpsUrl(string value)
    {
        var normalized = NormalizeRequired(value, 2_048, nameof(value));
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("A source URL must be an absolute HTTPS URL.", nameof(value));
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

    private static string? NormalizeOptional(string? value, int maximumCharacters, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeRequired(value, maximumCharacters, parameterName);
    }
}
