namespace AskARabbiLIB.Conversations;

/// <summary>Defines source selectors supported by production conversations.</summary>
public static class ConversationSourceCatalog
{
    /// <summary>Gets the core source selectors available as a narrower quick selection.</summary>
    public static IReadOnlyList<string> Core { get; } = Array.AsReadOnly<string>(
    [
        "collection:Torah",
        "collection:Tanakh",
        "collection:Mishnah",
        "collection:Talmud",
    ]);

    /// <summary>Gets every currently supported source selector.</summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly<string>(
    [
        "collection:Torah",
        "collection:Tanakh",
        "collection:Mishnah",
        "collection:Talmud",
        "work:rif",
        "work:mishneh_torah",
        "work:shulchan_arukh_with_rema",
        "work:zohar",
        "work:zohar_chadash",
        "work:mesillat_yesharim",
    ]);

    /// <summary>Determines whether a source selector is supported.</summary>
    /// <param name="sourceKey">Source selector to check.</param>
    /// <returns><see langword="true"/> when the selector is supported.</returns>
    public static bool Contains(string sourceKey) => All.Contains(sourceKey, StringComparer.Ordinal);
}
