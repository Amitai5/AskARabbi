using System.Text.RegularExpressions;
using AskARabbiLIB.Retrieval;
using AskARabbiLIB.Search;

namespace AskARabbiLIB.Grounding;

/// <summary>Routes explicit references and common learning topics to passages, never to prewritten religious answers.</summary>
internal static class ConversationReferenceGuide
{
    private static readonly Regex ExplicitReference = new(@"\b(?<ref>(?:(?:Genesis|Exodus|Leviticus|Numbers|Deuteronomy|Psalms|Proverbs|Isaiah|Jeremiah|Chullin|Berakhot|Shabbat)|(?:Mishnah\s+[A-Za-z]+)|(?:Shulchan Arukh,\s+(?:Orach Chayim|Yoreh De'ah)))\s+\d+[ab]?(?::\d+)*(?:\s*[-–]\s*\d+[ab]?(?::\d+)*)?)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static string? FindExplicitReference(string question)
    {
        var match = ExplicitReference.Match(question);
        return match.Success ? match.Groups["ref"].Value : null;
    }

    internal static IReadOnlyList<string> PreferredLanguages(GroundedQuestion question) => new[] { question.QuotationLanguage, question.ConversationLanguage, "English", "Hebrew" }.Concat(question.Languages)
        .OfType<string>().Where(value => !string.IsNullOrWhiteSpace(value) && (question.Languages.Count == 0 || question.Languages.Contains(value, StringComparer.OrdinalIgnoreCase))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    internal static async Task<IReadOnlyList<SourceSegment>> ReadAsync(ICanonicalSourceReader reader, GroundedQuestion question, IReadOnlyList<GroundedConversationTurn> conversation, SourceRetrievalQuery query, CancellationToken cancellationToken)
    {
        var references = GetReferences(question.Question, conversation);
        var filters = query with { Languages = PreferredLanguages(question) };
        var results = await Task.WhenAll(references.Select(reference => reader.ReadAsync(reference, filters, cancellationToken))).ConfigureAwait(false);
        return results.SelectMany(result => result).DistinctBy(segment => segment.SegmentId).ToArray();
    }

    internal static IReadOnlyList<string> GetReferences(string question, IReadOnlyList<GroundedConversationTurn> conversation)
    {
        if (FindExplicitReference(question) is { } exact)
        {
            return [exact];
        }
        // Follow-ups inherit the topic, not the correctness of any earlier generated answer.
        var tokens = SearchTextNormalizer.Tokenize(question).ToHashSet(StringComparer.Ordinal);
        if (!tokens.Overlaps(["chicken", "chickens", "poultry", "fowl", "עוף", "rice", "kitniyot", "kitniyos", "אורז", "קטניות", "shema", "shma", "שמע", "car", "driving", "drive", "engine", "רכב", "נהיגה", "exodus"]))
        {
            tokens.UnionWith(SearchTextNormalizer.Tokenize(string.Join(' ', conversation.TakeLast(2).Select(turn => turn.Question))));
        }
        if (tokens.Overlaps(["chicken", "chickens", "poultry", "fowl", "עוף"]) && tokens.Overlaps(["milk", "dairy", "cheese", "חלב"]))
        {
            return ["Mishnah Chullin 8:1", "Mishnah Chullin 8:4", "Chullin 104b:1-9", "Chullin 113a:1-5", "Shulchan Arukh, Yoreh De'ah 87:3", "Exodus 23:19"];
        }
        if (tokens.Overlaps(["rice", "kitniyot", "kitniyos", "אורז", "קטניות"]) && tokens.Overlaps(["passover", "pesach", "פסח"]))
        {
            return ["Shulchan Arukh, Orach Chayim 453:1", "Mishneh Torah, Leavened and Unleavened Bread 5:1"];
        }
        if (tokens.Overlaps(["shema", "shma", "שמע"]))
        {
            return ["Deuteronomy 6:4-9", "Deuteronomy 11:13-21", "Numbers 15:37-41", "Mishnah Berakhot 1:1-3", "Mishnah Berakhot 2:2"];
        }
        if (tokens.Overlaps(["car", "driving", "drive", "engine", "רכב", "נהיגה"]) && tokens.Overlaps(["shabbat", "shabbos", "sabbath", "שבת"]))
        {
            return ["Exodus 35:3", "Mishnah Shabbat 7:2", "Mishneh Torah, Sabbath 12:1"];
        }
        if (tokens.Contains("exodus") && tokens.Overlaps(["story", "summary", "summarize", "explain"]))
        {
            return ["Exodus 1:1-22", "Exodus 3:1-22", "Exodus 7:1-25", "Exodus 10:1-29", "Exodus 12:1-42", "Exodus 14:1-31", "Exodus 15:1-21"];
        }
        return [];
    }
}
