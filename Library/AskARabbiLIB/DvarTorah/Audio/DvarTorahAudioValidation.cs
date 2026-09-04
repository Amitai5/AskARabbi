using System.Text.RegularExpressions;

namespace AskARabbiLIB.DvarTorah.Audio;

internal static class DvarTorahAudioValidation
{
    internal const int MaximumManifestBytes = 8 * 1024 * 1024;
    internal const int MaximumMp3Bytes = 64 * 1024 * 1024;
    internal const int MaximumPcmBytes = 180 * 1024 * 1024;

    internal static void ValidateVersion(string version)
    {
        if (version is null || !Regex.IsMatch(version, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("A lowercase SHA-256 narration version is required.", nameof(version));
        }
    }

    internal static string GetPrefix(string weekKey, string version)
    {
        ValidateVersion(version);
        if (weekKey is null || !Regex.IsMatch(weekKey, "^(diaspora|israel):[0-9]{4}-[0-9]{2}-[0-9]{2}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("A canonical reading week key is required.", nameof(weekKey));
        }
        return $"{weekKey.Replace(':', '/')}/{version}";
    }

    internal static void ValidateTimings(DvarTorahAudioTimings timings)
    {
        ArgumentNullException.ThrowIfNull(timings);
        ValidateVersion(timings.Version);
        if (timings.SchemaVersion != 1 || timings.TextOffsetUnit != "UTF-16 code units" || string.IsNullOrWhiteSpace(timings.Voice) || string.IsNullOrWhiteSpace(timings.Title) || string.IsNullOrWhiteSpace(timings.Body) || timings.Title.Length > WeeklyDvarTorahDraft.MaximumTitleCharacters || timings.Body.Length > WeeklyDvarTorahDraft.MaximumBodyCharacters || !double.IsFinite(timings.DurationMs) || timings.DurationMs <= 0 || timings.DurationMs > 3_600_000 || timings.Words is null || timings.Words.Count == 0 || timings.Words.Count > 40_000)
        {
            throw new InvalidDataException("The narration manifest is invalid or exceeds supported limits.");
        }

        double previousTime = -1;
        var previousTitlePosition = -1;
        var previousBodyPosition = -1;
        foreach (var word in timings.Words)
        {
            var text = word.Section switch { "title" => timings.Title, "body" => timings.Body, _ => null };
            if (text is null || string.IsNullOrEmpty(word.Text) || word.TextOffset < 0 || word.TextLength != word.Text.Length || word.TextOffset > text.Length - word.TextLength || !text.AsSpan(word.TextOffset, word.TextLength).SequenceEqual(word.Text) || !double.IsFinite(word.AudioOffsetMs) || !double.IsFinite(word.DurationMs) || word.AudioOffsetMs < previousTime || word.DurationMs < 0 || word.AudioOffsetMs + word.DurationMs > timings.DurationMs + 100)
            {
                throw new InvalidDataException("A narration word does not match its display text or audio time.");
            }
            ref var position = ref (word.Section == "title" ? ref previousTitlePosition : ref previousBodyPosition);
            if (word.TextOffset < position || word.Section == "title" && previousBodyPosition >= 0)
            {
                throw new InvalidDataException("Narration word positions must be ordered and non-overlapping.");
            }
            position = word.TextOffset + word.TextLength;
            previousTime = word.AudioOffsetMs;
        }
    }
}
