using System.Globalization;
using System.Text;

namespace AskARabbiLIB.Search;

internal static class SearchTextNormalizer
{
    /// <summary>Normalizes Unicode text for deterministic case-insensitive search.</summary>
    /// <param name="value">Text to normalize.</param>
    /// <returns>Normalized search text, or an empty string for empty input.</returns>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = true;
        foreach (var rune in value.Normalize(NormalizationForm.FormD).EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            if (Rune.IsLetterOrDigit(rune))
            {
                builder.Append(Rune.ToLowerInvariant(rune));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                builder.Append(' ');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Returns distinct normalized letter-and-digit search tokens.</summary>
    /// <param name="value">Text to tokenize.</param>
    /// <returns>Distinct tokens in first-seen order.</returns>
    public static string[] Tokenize(string? value)
    {
        var normalized = Normalize(value);
        return normalized.Length == 0
            ? Array.Empty<string>()
            : normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
    }
}
