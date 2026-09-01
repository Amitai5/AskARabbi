namespace AskARabbiLIB.DvarTorah;

/// <summary>Validates that retrieved Torah passages belong to the configured weekly portion.</summary>
internal static class ParashahTorahRangeCatalog
{
    private static readonly IReadOnlyDictionary<string, TorahRange> Ranges = CreateRanges();

    internal static bool Contains(string parashah, string canonicalReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parashah);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalReference);
        var normalized = Normalize(parashah);
        return Ranges.TryGetValue(normalized, out var range) && range.Contains(canonicalReference);
    }

    internal static bool IsSupported(string parashah)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parashah);
        return Ranges.ContainsKey(Normalize(parashah));
    }

    private static IReadOnlyDictionary<string, TorahRange> CreateRanges()
    {
        var values = new Dictionary<string, TorahRange>(StringComparer.Ordinal);
        Add(values, new("Genesis", 1, 1, 6, 8), "Bereshis", "Bereishis", "Bereshit");
        Add(values, new("Genesis", 6, 9, 11, 32), "Noach");
        Add(values, new("Genesis", 12, 1, 17, 27), "Lech Lecha", "Lech-Lecha");
        Add(values, new("Genesis", 18, 1, 22, 24), "Vayera", "Vayeira");
        Add(values, new("Genesis", 23, 1, 25, 18), "Chayei Sara", "Chayei Sarah");
        Add(values, new("Genesis", 25, 19, 28, 9), "Toldos", "Toldot");
        Add(values, new("Genesis", 28, 10, 32, 3), "Vayetzei", "Vayeitzei");
        Add(values, new("Genesis", 32, 4, 36, 43), "Vayishlach");
        Add(values, new("Genesis", 37, 1, 40, 23), "Vayeshev", "Vayeishev");
        Add(values, new("Genesis", 41, 1, 44, 17), "Miketz");
        Add(values, new("Genesis", 44, 18, 47, 27), "Vayigash");
        Add(values, new("Genesis", 47, 28, 50, 26), "Vayechi");
        Add(values, new("Exodus", 1, 1, 6, 1), "Shemos", "Shemot");
        Add(values, new("Exodus", 6, 2, 9, 35), "Vaera", "Vaeira");
        Add(values, new("Exodus", 10, 1, 13, 16), "Bo");
        Add(values, new("Exodus", 13, 17, 17, 16), "Beshalach");
        Add(values, new("Exodus", 18, 1, 20, 23), "Yisro", "Yitro");
        Add(values, new("Exodus", 21, 1, 24, 18), "Mishpatim");
        Add(values, new("Exodus", 25, 1, 27, 19), "Terumah");
        Add(values, new("Exodus", 27, 20, 30, 10), "Tetzaveh");
        Add(values, new("Exodus", 30, 11, 34, 35), "Ki Sisa", "Ki Tisa");
        Add(values, new("Exodus", 35, 1, 38, 20), "Vayakhel");
        Add(values, new("Exodus", 38, 21, 40, 38), "Pekudei");
        Add(values, new("Leviticus", 1, 1, 5, 26), "Vayikra");
        Add(values, new("Leviticus", 6, 1, 8, 36), "Tzav");
        Add(values, new("Leviticus", 9, 1, 11, 47), "Shmini", "Shemini");
        Add(values, new("Leviticus", 12, 1, 13, 59), "Tazria");
        Add(values, new("Leviticus", 14, 1, 15, 33), "Metzora");
        Add(values, new("Leviticus", 16, 1, 18, 30), "Achrei Mos", "Acharei Mot");
        Add(values, new("Leviticus", 19, 1, 20, 27), "Kedoshim");
        Add(values, new("Leviticus", 21, 1, 24, 23), "Emor");
        Add(values, new("Leviticus", 25, 1, 26, 2), "Behar");
        Add(values, new("Leviticus", 26, 3, 27, 34), "Bechukosai", "Bechukotai");
        Add(values, new("Numbers", 1, 1, 4, 20), "Bamidbar");
        Add(values, new("Numbers", 4, 21, 7, 89), "Nasso", "Naso");
        Add(values, new("Numbers", 8, 1, 12, 16), "Behaaloscha", "Behaalotecha");
        Add(values, new("Numbers", 13, 1, 15, 41), "Shlach", "Shelach");
        Add(values, new("Numbers", 16, 1, 18, 32), "Korach");
        Add(values, new("Numbers", 19, 1, 22, 1), "Chukas", "Chukat");
        Add(values, new("Numbers", 22, 2, 25, 9), "Balak");
        Add(values, new("Numbers", 25, 10, 30, 1), "Pinchas");
        Add(values, new("Numbers", 30, 2, 32, 42), "Matos", "Matot");
        Add(values, new("Numbers", 33, 1, 36, 13), "Masei");
        Add(values, new("Deuteronomy", 1, 1, 3, 22), "Devarim");
        Add(values, new("Deuteronomy", 3, 23, 7, 11), "Vaeschanan", "Vaetchanan");
        Add(values, new("Deuteronomy", 7, 12, 11, 25), "Eikev");
        Add(values, new("Deuteronomy", 11, 26, 16, 17), "Reeh");
        Add(values, new("Deuteronomy", 16, 18, 21, 9), "Shoftim");
        Add(values, new("Deuteronomy", 21, 10, 25, 19), "Ki Seitzei", "Ki Teitzei");
        Add(values, new("Deuteronomy", 26, 1, 29, 8), "Ki Savo", "Ki Tavo");
        Add(values, new("Deuteronomy", 29, 9, 30, 20), "Nitzavim");
        Add(values, new("Deuteronomy", 31, 1, 31, 30), "Vayeilech");
        Add(values, new("Deuteronomy", 32, 1, 32, 52), "Haazinu");
        Add(values, new("Deuteronomy", 33, 1, 34, 12), "Vzos Haberachah", "Vezot Haberakhah");
        Add(values, new("Leviticus", 12, 1, 15, 33), "Tazria Metzora", "Tazria-Metzora");
        Add(values, new("Leviticus", 16, 1, 20, 27), "Achrei Mos Kedoshim", "Achrei Mos-Kedoshim");
        Add(values, new("Leviticus", 25, 1, 27, 34), "Behar Bechukosai", "Behar-Bechukosai");
        Add(values, new("Numbers", 19, 1, 25, 9), "Chukas Balak", "Chukas-Balak");
        Add(values, new("Numbers", 30, 2, 36, 13), "Matos Masei", "Matos-Masei");
        Add(values, new("Deuteronomy", 29, 9, 31, 30), "Nitzavim Vayeilech", "Nitzavim-Vayeilech");
        Add(values, new("Exodus", 35, 1, 40, 38), "Vayakhel Pekudei", "Vayakhel-Pekudei");
        return values;
    }

    private static void Add(IDictionary<string, TorahRange> values, TorahRange range, params string[] names)
    {
        foreach (var name in names)
        {
            values[Normalize(name)] = range;
        }
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
