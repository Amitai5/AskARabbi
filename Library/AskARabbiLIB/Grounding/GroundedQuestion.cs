using AskARabbiLIB.Profiles;

namespace AskARabbiLIB.Grounding;

/// <summary>Defines one grounded question and its approved source preferences.</summary>
public sealed record GroundedQuestion
{
    public required string Question { get; init; }

    public IReadOnlyCollection<string> Languages { get; init; } = [];

    public IReadOnlyCollection<string> Collections { get; init; } = [];

    public IReadOnlyCollection<string> Categories { get; init; } = [];

    public IReadOnlyCollection<string> WorkKeys { get; init; } = [];

    public IReadOnlyCollection<string> SourceKeys { get; init; } = [];

    public string? ConversationLanguage { get; init; }

    public string? QuotationLanguage { get; init; }

    public UserProfile? UserProfile { get; init; }
}
