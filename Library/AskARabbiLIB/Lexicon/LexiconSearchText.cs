using System.Text.RegularExpressions;
using AskARabbiLIB.Search;

namespace AskARabbiLIB.Lexicon;

/// <summary>Creates exact lookup keys without collapsing Hebrew consonants or inventing roots.</summary>
internal static class LexiconSearchText
{
    internal const int ParagraphLeadLength = 240;
    private static readonly Regex HebrewWords = new(@"[\u05D0-\u05EA]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex EnglishWords = new(@"[a-z]{2,}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly HashSet<string> StopWords = new("the a an of to and or in on at by for from with is are be as it that this which".Split(' '), StringComparer.Ordinal);

    internal static string HeadwordKey(string value)
    {
        var normalized = SearchTextNormalizer.Normalize(value);
        var hebrew = HebrewWords.Matches(normalized).Select(match => match.Value).ToArray();
        return hebrew.Length > 0 ? string.Join(' ', hebrew) : normalized;
    }

    internal static string[] QueryKeys(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (query.Length > 100)
        {
            throw new ArgumentException("Use a word or short phrase of at most 100 characters.", nameof(query));
        }
        var normalized = SearchTextNormalizer.Normalize(query);
        var hebrew = HebrewWords.Matches(normalized).Select(match => "he:" + match.Value).ToArray();
        var keys = hebrew.Length > 0 ? hebrew : EnglishWords.Matches(normalized).Select(match => match.Value).Where(word => !StopWords.Contains(word)).Select(word => "en:" + word).ToArray();
        if (keys.Length is 0 or > 6)
        {
            throw new ArgumentException("Search using one to six Hebrew or English words.", nameof(query));
        }
        return keys.Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static string[] CreateKeys(LexiconEntry entry)
    {
        var normalized = SearchTextNormalizer.Normalize(entry.Text);
        // Original paragraph openings often contain a definition or subentry. This is a ranking hint,
        // not a claim that every opening is a definition or that related words share a root.
        var leads = SearchTextNormalizer.Normalize(string.Join('\n', entry.Text.Split('\n').Select(line => line[..Math.Min(ParagraphLeadLength, line.Length)])));
        return new[] { "head:" + HeadwordKey(entry.Headword) }
            .Concat(HebrewWords.Matches(leads).Select(match => "lead:he:" + match.Value))
            .Concat(EnglishWords.Matches(leads).Select(match => match.Value).Where(word => !StopWords.Contains(word)).Select(word => "lead:en:" + word))
            .Concat(HebrewWords.Matches(normalized).Select(match => "he:" + match.Value))
            .Concat(EnglishWords.Matches(normalized).Select(match => match.Value).Where(word => !StopWords.Contains(word)).Select(word => "en:" + word))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }
}
