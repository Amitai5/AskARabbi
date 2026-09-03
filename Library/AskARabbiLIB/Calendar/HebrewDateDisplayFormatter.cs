namespace AskARabbiLIB.Calendar;

/// <summary>Applies a user's community-aware transliteration preference to an English Hebrew date.</summary>
internal static class HebrewDateDisplayFormatter
{
    internal static string Format(string englishText, string? jewishHeritage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(englishText);
        if (IsAshkenazi(jewishHeritage))
        {
            return englishText.Trim();
        }

        return englishText.Trim()
            .Replace("Teves", "Tevet", StringComparison.Ordinal)
            .Replace("Nissan", "Nisan", StringComparison.Ordinal);
    }

    private static bool IsAshkenazi(string? jewishHeritage) => !string.IsNullOrWhiteSpace(jewishHeritage) && jewishHeritage.Contains("Ashkenazi", StringComparison.OrdinalIgnoreCase);
}
