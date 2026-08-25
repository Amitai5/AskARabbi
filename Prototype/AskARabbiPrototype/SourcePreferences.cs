using AskARabbiLIB.Grounding;
using AskARabbiLIB.Profiles;
using AskARabbiLIB.Retrieval;

namespace AskARabbiPrototype;

internal sealed record SourcePreferences(IReadOnlyList<string> EnabledSourceKeys, string? Language, string? Category)
{
    internal static SourcePreferences CreateDefault(DocumentSourceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new SourcePreferences(catalog.Sources.Select(source => source.Key).ToArray(), null, null);
    }

    internal GroundedQuestion CreateQuestion(string question, UserProfile userProfile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(userProfile);
        return new GroundedQuestion
        {
            Question = question,
            Languages = ToFilter(Language),
            Categories = ToFilter(Category),
            SourceKeys = EnabledSourceKeys,
            UserProfile = userProfile,
        };
    }

    private static IReadOnlyCollection<string> ToFilter(string? value) => value is null ? [] : [value];
}
