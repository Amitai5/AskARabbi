using System.Globalization;

namespace AskARabbiLIB.DvarTorah;

/// <summary>Validates the primary Torah reading that replaces the regular parashah on a festival Shabbat.</summary>
internal static class FestivalTorahRangeCatalog
{
    private static readonly HebrewCalendar Calendar = new();
    private static readonly IReadOnlyList<TorahRange> RoshHashanaFirstDay = [new("Genesis", 21, 1, 21, 34)];
    private static readonly IReadOnlyList<TorahRange> RoshHashanaSecondDay = [new("Genesis", 22, 1, 22, 24)];
    private static readonly IReadOnlyList<TorahRange> YomKippur = [new("Leviticus", 16, 1, 16, 34)];
    private static readonly IReadOnlyList<TorahRange> Sukkot = [new("Leviticus", 22, 26, 23, 44)];
    private static readonly IReadOnlyList<TorahRange> FestivalCholHamoed = [new("Exodus", 33, 12, 34, 26)];
    private static readonly IReadOnlyList<TorahRange> SheminiAtzeret = [new("Deuteronomy", 14, 22, 16, 17)];
    private static readonly IReadOnlyList<TorahRange> SimchatTorah = [new("Deuteronomy", 33, 1, 34, 12), new("Genesis", 1, 1, 2, 3)];
    private static readonly IReadOnlyList<TorahRange> PesachFirstDay = [new("Exodus", 12, 21, 12, 51)];
    private static readonly IReadOnlyList<TorahRange> PesachSecondDay = [new("Leviticus", 22, 26, 23, 44)];
    private static readonly IReadOnlyList<TorahRange> PesachSeventhDay = [new("Exodus", 13, 17, 15, 26)];
    private static readonly IReadOnlyList<TorahRange> PesachEighthDay = [new("Deuteronomy", 14, 22, 16, 17)];
    private static readonly IReadOnlyList<TorahRange> ShavuotFirstDay = [new("Exodus", 19, 1, 20, 23)];
    private static readonly IReadOnlyList<TorahRange> ShavuotSecondDay = [new("Deuteronomy", 14, 22, 16, 17)];

    internal static bool IsSupported(WeeklyDvarTorahWeek week)
    {
        ArgumentNullException.ThrowIfNull(week);
        return GetRanges(week).Count > 0;
    }

    internal static bool Contains(WeeklyDvarTorahWeek week, string canonicalReference)
    {
        ArgumentNullException.ThrowIfNull(week);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalReference);
        return GetRanges(week).Any(range => range.Contains(canonicalReference));
    }

    private static IReadOnlyList<TorahRange> GetRanges(WeeklyDvarTorahWeek week)
    {
        if (week.Holiday is null)
        {
            return [];
        }

        var holiday = Normalize(week.Holiday);
        var day = Calendar.GetDayOfMonth(week.ShabbatDate.ToDateTime(new TimeOnly(12, 0)));
        return (holiday, day, week.InIsrael) switch
        {
            ("roshhashana", 1, _) => RoshHashanaFirstDay,
            ("roshhashana", 2, _) => RoshHashanaSecondDay,
            ("yomkippur", 10, _) => YomKippur,
            ("succos", 15 or 16, _) => Sukkot,
            ("sukkot", 15 or 16, _) => Sukkot,
            ("cholhamoedsuccos", 17 or 18 or 19 or 20, _) => FestivalCholHamoed,
            ("cholhamoedsukkot", 17 or 18 or 19 or 20, _) => FestivalCholHamoed,
            ("sheminiatzeres", 22, false) => SheminiAtzeret,
            ("sheminiatzeret", 22, false) => SheminiAtzeret,
            ("sheminiatzeres", 22, true) => SimchatTorah,
            ("sheminiatzeret", 22, true) => SimchatTorah,
            ("simchastorah", 22 or 23, _) => SimchatTorah,
            ("simchattorah", 22 or 23, _) => SimchatTorah,
            ("pesach", 15, _) => PesachFirstDay,
            ("pesach", 16, false) => PesachSecondDay,
            ("cholhamoedpesach", 17 or 18 or 19 or 20, _) => FestivalCholHamoed,
            ("pesach", 21, _) => PesachSeventhDay,
            ("pesach", 22, false) => PesachEighthDay,
            ("shavuos", 6, _) => ShavuotFirstDay,
            ("shavuot", 6, _) => ShavuotFirstDay,
            ("shavuos", 7, false) => ShavuotSecondDay,
            ("shavuot", 7, false) => ShavuotSecondDay,
            _ => [],
        };
    }

    private static string Normalize(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private sealed record TorahRange(string Book, int StartChapter, int StartVerse, int EndChapter, int EndVerse)
    {
        internal bool Contains(string reference)
        {
            if (!reference.StartsWith($"{Book} ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var location = reference[(Book.Length + 1)..];
            var colon = location.IndexOf(':');
            if (colon < 1 || !int.TryParse(location[..colon], out var chapter))
            {
                return false;
            }

            var verseText = new string(location[(colon + 1)..].TakeWhile(char.IsDigit).ToArray());
            if (!int.TryParse(verseText, out var verse) || chapter < StartChapter || chapter > EndChapter)
            {
                return false;
            }

            return (chapter > StartChapter || verse >= StartVerse) && (chapter < EndChapter || verse <= EndVerse);
        }
    }
}
