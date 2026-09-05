using System.Globalization;
using System.Text;

namespace AskARabbiLIB.Grounding;

internal static class GroundedQuotationResolver
{
    private const int MinimumEquivalentCharacterCount = 8;
    private const int MinimumEquivalentTokenCount = 2;

    internal static bool TryResolve(EvidenceItem evidence, string requestedText, out string resolvedText)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (string.IsNullOrWhiteSpace(requestedText))
        {
            resolvedText = string.Empty;
            return false;
        }

        var requested = requestedText.Trim();
        if (requested.StartsWith("@Q", StringComparison.Ordinal))
        {
            var choice = GroundedQuotationChoices.Create(evidence).FirstOrDefault(candidate => candidate.Selector == requested);
            resolvedText = choice?.Text ?? string.Empty;
            return choice is not null;
        }
        if (evidence.PresentedText.Contains(requested, StringComparison.Ordinal) && evidence.Source.Text.Contains(requested, StringComparison.Ordinal))
        {
            resolvedText = requested;
            return true;
        }

        if (!TryFindEquivalentSubstring(evidence.PresentedText, requested, out var presentedSubstring) || !evidence.Source.Text.Contains(presentedSubstring, StringComparison.Ordinal))
        {
            resolvedText = string.Empty;
            return false;
        }

        resolvedText = presentedSubstring;
        return true;
    }

    private static bool TryFindEquivalentSubstring(string source, string requested, out string match)
    {
        var normalizedSource = NormalizeWithSourceMap(source);
        var normalizedRequested = NormalizeWithSourceMap(requested).Value;
        if (normalizedRequested.Length < MinimumEquivalentCharacterCount || CountTokens(normalizedRequested) < MinimumEquivalentTokenCount)
        {
            match = string.Empty;
            return false;
        }

        var searchStart = 0;
        while (searchStart <= normalizedSource.Value.Length - normalizedRequested.Length)
        {
            var matchIndex = normalizedSource.Value.IndexOf(normalizedRequested, searchStart, StringComparison.Ordinal);
            if (matchIndex < 0)
            {
                break;
            }

            var matchEnd = matchIndex + normalizedRequested.Length;
            var startsAtBoundary = matchIndex == 0 || normalizedSource.Value[matchIndex - 1] == ' ';
            var endsAtBoundary = matchEnd == normalizedSource.Value.Length || normalizedSource.Value[matchEnd] == ' ';
            if (startsAtBoundary && endsAtBoundary)
            {
                var originalStart = normalizedSource.OriginalStarts[matchIndex];
                var originalEnd = normalizedSource.OriginalEnds[matchEnd - 1];
                match = source[originalStart..originalEnd];
                return true;
            }

            searchStart = matchIndex + 1;
        }

        match = string.Empty;
        return false;
    }

    private static NormalizedText NormalizeWithSourceMap(string value)
    {
        var builder = new StringBuilder(value.Length);
        var originalStarts = new List<int>(value.Length);
        var originalEnds = new List<int>(value.Length);
        var originalIndex = 0;

        foreach (var sourceRune in value.EnumerateRunes())
        {
            var runeStart = originalIndex;
            originalIndex += sourceRune.Utf16SequenceLength;
            foreach (var normalizedRune in sourceRune.ToString().Normalize(NormalizationForm.FormD).EnumerateRunes())
            {
                var category = Rune.GetUnicodeCategory(normalizedRune);
                if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
                {
                    continue;
                }

                if (Rune.IsLetterOrDigit(normalizedRune))
                {
                    AppendRune(builder, originalStarts, originalEnds, Rune.ToLowerInvariant(normalizedRune), runeStart, originalIndex);
                }
                else if (builder.Length > 0 && builder[^1] != ' ')
                {
                    builder.Append(' ');
                    originalStarts.Add(runeStart);
                    originalEnds.Add(originalIndex);
                }
            }
        }

        if (builder.Length > 0 && builder[^1] == ' ')
        {
            builder.Length--;
            originalStarts.RemoveAt(originalStarts.Count - 1);
            originalEnds.RemoveAt(originalEnds.Count - 1);
        }

        return new NormalizedText(builder.ToString(), originalStarts.ToArray(), originalEnds.ToArray());
    }

    private static void AppendRune(StringBuilder builder, ICollection<int> originalStarts, ICollection<int> originalEnds, Rune rune, int originalStart, int originalEnd)
    {
        var normalizedValue = rune.ToString();
        builder.Append(normalizedValue);
        foreach (var _ in normalizedValue)
        {
            originalStarts.Add(originalStart);
            originalEnds.Add(originalEnd);
        }
    }

    private static int CountTokens(string value) => value.Count(character => character == ' ') + 1;

    private sealed record NormalizedText(string Value, int[] OriginalStarts, int[] OriginalEnds);
}
