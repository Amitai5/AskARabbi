namespace AskARabbiLIB.DvarTorah;

/// <summary>Contains searchable and auditable metadata for generated weekly content.</summary>
public sealed record WeeklyDvarTorahContentMetadata
{
    /// <summary>Initializes validated weekly content metadata.</summary>
    /// <param name="centralTeaching">Concise statement of the article's central moral teaching.</param>
    /// <param name="tags">Normalized search tags.</param>
    /// <param name="sources">Materialized Torah, news, and supporting sources.</param>
    /// <param name="torahGroundingPercent">Deterministically calculated Torah grounding percentage.</param>
    /// <param name="safetyReviewVersion">Version of the safety and inclusion review contract that passed.</param>
    /// <param name="model">Model deployment that produced the article.</param>
    /// <param name="newsWindowStartedAtUtc">Beginning of the current-events research window.</param>
    /// <param name="newsWindowEndedAtUtc">End of the current-events research window.</param>
    public WeeklyDvarTorahContentMetadata(string centralTeaching, IReadOnlyList<string> tags, IReadOnlyList<WeeklyDvarTorahSource> sources, int torahGroundingPercent, string safetyReviewVersion, string model, DateTimeOffset newsWindowStartedAtUtc, DateTimeOffset newsWindowEndedAtUtc)
    {
        CentralTeaching = NormalizeRequired(centralTeaching, 1_200, nameof(centralTeaching));
        Tags = NormalizeTags(tags);
        Sources = NormalizeSources(sources);
        if (torahGroundingPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(torahGroundingPercent), "Torah grounding percent must be between zero and one hundred.");
        }
        if (newsWindowEndedAtUtc < newsWindowStartedAtUtc)
        {
            throw new ArgumentException("The news research window cannot end before it starts.", nameof(newsWindowEndedAtUtc));
        }

        TorahGroundingPercent = torahGroundingPercent;
        SafetyReviewVersion = NormalizeRequired(safetyReviewVersion, 120, nameof(safetyReviewVersion));
        Model = NormalizeRequired(model, 160, nameof(model));
        NewsWindowStartedAtUtc = newsWindowStartedAtUtc.ToUniversalTime();
        NewsWindowEndedAtUtc = newsWindowEndedAtUtc.ToUniversalTime();
    }

    /// <summary>Gets the article's central moral teaching.</summary>
    public string CentralTeaching { get; }

    /// <summary>Gets normalized search tags.</summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>Gets every source materialized from trusted evidence.</summary>
    public IReadOnlyList<WeeklyDvarTorahSource> Sources { get; }

    /// <summary>Gets the deterministic Torah grounding percentage.</summary>
    public int TorahGroundingPercent { get; }

    /// <summary>Gets the safety and inclusion review version.</summary>
    public string SafetyReviewVersion { get; }

    /// <summary>Gets the model deployment used for generation.</summary>
    public string Model { get; }

    /// <summary>Gets the beginning of the current-events research window.</summary>
    public DateTimeOffset NewsWindowStartedAtUtc { get; }

    /// <summary>Gets the end of the current-events research window.</summary>
    public DateTimeOffset NewsWindowEndedAtUtc { get; }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count is < 3 or > 20 || values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Weekly Dvar Torah metadata requires between three and twenty non-blank tags.", nameof(values));
        }

        var normalized = values.Select(value => value.Trim().ToLowerInvariant()).ToArray();
        if (normalized.Any(value => value.Length > 60) || normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            throw new ArgumentException("Tags must be unique after normalization and contain at most sixty characters.", nameof(values));
        }

        return normalized;
    }

    private static IReadOnlyList<WeeklyDvarTorahSource> NormalizeSources(IReadOnlyList<WeeklyDvarTorahSource> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count is < 1 or > 40 || values.Any(value => value is null))
        {
            throw new ArgumentException("Weekly Dvar Torah metadata requires between one and forty complete sources.", nameof(values));
        }
        if (values.Select(value => value.SourceId).Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            throw new ArgumentException("Source identifiers must be unique.", nameof(values));
        }
        if (!values.Any(value => value.Kind == WeeklyDvarTorahSourceKind.Torah))
        {
            throw new ArgumentException("At least one Torah source is required.", nameof(values));
        }

        return values.ToArray();
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
