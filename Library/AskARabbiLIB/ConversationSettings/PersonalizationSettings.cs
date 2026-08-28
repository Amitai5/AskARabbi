namespace AskARabbiLIB.ConversationSettings;

/// <summary>Contains user-owned context used to tailor conversations without treating it as evidence.</summary>
public sealed record PersonalizationSettings
{
    /// <summary>Gets the user's full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the local birth date.</summary>
    public required DateOnly BirthDate { get; init; }

    /// <summary>Gets the local birth time.</summary>
    public required TimeOnly BirthTime { get; init; }

    /// <summary>Gets the IANA time-zone identifier for the birthplace.</summary>
    public required string BirthTimeZone { get; init; }

    /// <summary>Gets the preferred conversation language.</summary>
    public required string ConversationLanguage { get; init; }

    /// <summary>Gets the preferred language for sourced quotations.</summary>
    public required string QuotationLanguage { get; init; }

    /// <summary>Gets the user's self-described religious movement or practice.</summary>
    public required string ReligiousMovement { get; init; }

    /// <summary>Gets the user's self-described Jewish heritage or community.</summary>
    public required string JewishHeritage { get; init; }

    /// <summary>Gets optional user-provided context.</summary>
    public string? AdditionalContext { get; init; }

    /// <summary>Normalizes and validates personalization data.</summary>
    /// <param name="currentDate">Current date used to validate age.</param>
    /// <returns>A normalized personalization value.</returns>
    public PersonalizationSettings NormalizeAndValidate(DateOnly currentDate)
    {
        var normalized = this with
        {
            FullName = FullName?.Trim() ?? string.Empty,
            BirthTimeZone = BirthTimeZone?.Trim() ?? string.Empty,
            ConversationLanguage = ConversationLanguage?.Trim() ?? string.Empty,
            QuotationLanguage = QuotationLanguage?.Trim() ?? string.Empty,
            ReligiousMovement = ReligiousMovement?.Trim() ?? string.Empty,
            JewishHeritage = JewishHeritage?.Trim() ?? string.Empty,
            AdditionalContext = string.IsNullOrWhiteSpace(AdditionalContext) ? null : AdditionalContext.Trim(),
        };

        ValidateText(normalized.FullName, 120, nameof(FullName));
        ValidateText(normalized.ReligiousMovement, 120, nameof(ReligiousMovement));
        ValidateText(normalized.JewishHeritage, 120, nameof(JewishHeritage));
        ValidateOptionalText(normalized.AdditionalContext, 2_000, nameof(AdditionalContext));

        if (normalized.BirthDate == default)
        {
            throw new ArgumentException("Birth date is required.", nameof(BirthDate));
        }
        if (normalized.BirthDate > currentDate)
        {
            throw new ArgumentOutOfRangeException(nameof(BirthDate), "Birth date cannot be in the future.");
        }
        if (normalized.BirthDate.AddYears(130) < currentDate)
        {
            throw new ArgumentOutOfRangeException(nameof(BirthDate), "Birth date cannot represent an age greater than 130 years.");
        }
        if (!PersonalizationCatalog.UnitedStatesTimeZones.Contains(normalized.BirthTimeZone, StringComparer.Ordinal))
        {
            throw new ArgumentException("Birth time zone is not currently supported.", nameof(BirthTimeZone));
        }
        if (!PersonalizationCatalog.Languages.Contains(normalized.ConversationLanguage, StringComparer.Ordinal))
        {
            throw new ArgumentException("Conversation language is not supported.", nameof(ConversationLanguage));
        }
        if (!PersonalizationCatalog.Languages.Contains(normalized.QuotationLanguage, StringComparer.Ordinal))
        {
            throw new ArgumentException("Quotation language is not supported.", nameof(QuotationLanguage));
        }

        return normalized;
    }

    private static void ValidateText(string value, int maximumLength, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{propertyName} is required.", propertyName);
        }
        if (value.Length > maximumLength)
        {
            throw new ArgumentException($"{propertyName} cannot exceed {maximumLength:N0} characters.", propertyName);
        }
    }

    private static void ValidateOptionalText(string? value, int maximumLength, string propertyName)
    {
        if (value is not null && value.Length > maximumLength)
        {
            throw new ArgumentException($"{propertyName} cannot exceed {maximumLength:N0} characters.", propertyName);
        }
    }
}
