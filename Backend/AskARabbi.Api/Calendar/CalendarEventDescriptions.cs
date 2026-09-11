using System.Text.RegularExpressions;

namespace AskARabbi.Api.Calendar;

/// <summary>Reviewed, reusable explanations keyed independently of occurrence dates. No generated prose.</summary>
public static class CalendarEventDescriptions
{
    public sealed record Description(string Key, string Title, string Category, string Explanation, string Beginning);

    /// <summary>Maps a provider title to a stable holiday identity and event-specific beginning rule.</summary>
    /// <param name="occurrence">Validated holiday occurrence.</param>
    /// <returns>Reviewed description and grouping identity.</returns>
    public static Description Describe(HebcalData.Event occurrence)
    {
        var title = occurrence.Title;
        var normalized = Normalize(title);
        if (normalized.StartsWith("roshchodesh", StringComparison.Ordinal))
        {
            return new(normalized, title, "roshChodesh", "Rosh Chodesh marks the beginning of a Hebrew month. Some months begin with a two-day observance.", "previousSunset");
        }
        if (normalized.StartsWith("roshhashanalabehemot", StringComparison.Ordinal))
        {
            return Entry("animal-new-year", title, "minor", "The first of Elul is the traditional new year for reckoning animal tithes.");
        }
        if (normalized.StartsWith("roshhashana", StringComparison.Ordinal))
        {
            return Entry("rosh-hashanah", "Rosh Hashanah", "major", "The Jewish New Year opens a season of reflection and renewal, marked by prayer and the shofar.");
        }
        if (normalized.Contains("hoshanaraba", StringComparison.Ordinal))
        {
            return Entry("hoshana-rabba", "Hoshana Rabba", "major", "The seventh day of Sukkot features additional prayers for help and the custom of taking willow branches.");
        }
        if (normalized.StartsWith("sukkot", StringComparison.Ordinal))
        {
            return Entry("sukkot", "Sukkot", "major", "This harvest festival recalls Israel's wilderness journey through the sukkah and celebrates with the four species. Intermediate days have their own observances.");
        }
        if (normalized.StartsWith("pesachsheni", StringComparison.Ordinal))
        {
            return Entry("pesach-sheni", title, "minor", "The Torah provided a second opportunity to bring the Passover offering for those unable to do so in Nisan.");
        }
        if (normalized.StartsWith("pesach", StringComparison.Ordinal))
        {
            return Entry("pesach", "Pesach", "major", "Passover tells the story of the Exodus from Egypt. The seder, matzah, and avoidance of chametz connect freedom with memory and responsibility.");
        }
        if (normalized.StartsWith("shavuot", StringComparison.Ordinal))
        {
            return Entry("shavuot", "Shavuot", "major", "The Festival of Weeks completes the counting of the Omer and celebrates the giving of the Torah. It also has biblical harvest and first-fruit roots.");
        }
        if (normalized.StartsWith("chanukah", StringComparison.Ordinal))
        {
            return new("chanukah", "Chanukah", "major", "Eight nights of lights remember the rededication of the Temple. The expanded dates distinguish each evening's candles from the final daytime observance.", "sameEvening");
        }

        var known = normalized switch
        {
            "yomkippur" => Entry("yom-kippur", "Yom Kippur", "major", "The Day of Atonement centers on repentance, prayer, reconciliation, and a fast beginning the previous evening."),
            "shminiatzeret" or "sheminiatzeret" => Entry("shemini-atzeret", "Shemini Atzeret", "major", "A distinct festival immediately following Sukkot. In Israel, Simchat Torah is celebrated on this same day; in the Diaspora it follows the next day."),
            "simchattorah" => Entry("simchat-torah", "Simchat Torah", "major", "Communities celebrate completing the annual Torah reading cycle and beginning it again, with singing and dancing with the Torah scrolls."),
            "purim" => Entry("purim", "Purim", "major", "The Book of Esther tells of the deliverance of the Jews of Persia. Megillah reading, gifts of food, charity, and a festive meal mark the day."),
            "shushanpurim" => Entry("shushan-purim", title, "minor", "Purim is celebrated a day later in Jerusalem and other qualifying ancient walled cities, following the account of Shushan in the Book of Esther."),
            "purimkatan" or "shushanpurimkatan" => Entry(normalized, title, "minor", "In a Hebrew leap year, this date in Adar I recalls Purim, while the main Purim observance takes place in Adar II."),
            "tubishvat" => Entry("tu-bishvat", "Tu BiShvat", "minor", "The new year for trees marks a boundary in the agricultural calendar. Many communities celebrate by eating fruit and reflecting on care for the natural world."),
            "lagbaomer" => Entry("lag-baomer", title, "minor", "The thirty-third day of the Omer has customs of celebration associated with Rabbi Shimon bar Yochai and the students of Rabbi Akiva."),
            "tubav" => Entry("tu-bav", "Tu B’Av", "minor", "The fifteenth of Av is remembered in rabbinic tradition as a joyful day and is associated today with love and relationships."),
            "chaghabanot" => Entry("chag-habanot", title, "minor", "A celebration of women observed during Chanukah in several North African and other Jewish communities, with customs that vary by community."),
            "tzomgedaliah" => Fast("tzom-gedaliah", title, "This fast remembers the assassination of Gedaliah, governor of Judah after the destruction of the First Temple."),
            "taanitesther" => Fast("taanit-esther", title, "The Fast of Esther precedes Purim and recalls fasting in the story of Esther and the deliverance of the Jewish people."),
            "taanitbechorot" => Fast("firstborn-fast", title, "The Fast of the Firstborn recalls the deliverance of Israel's firstborn before the Exodus. Participation and customs vary; many attend a siyum."),
            "tzomtammuz" => Fast("seventeenth-tammuz", title, "This fast recalls the breaching of Jerusalem's walls and begins the Three Weeks leading to Tisha B’Av."),
            "asarabtevet" => Fast("tenth-tevet", title, "The tenth of Tevet commemorates the beginning of the Babylonian siege of Jerusalem."),
            "tishabav" or "tishabavobserved" => Entry("tisha-bav", "Tisha B’Av", "fast", "A day of mourning for the destruction of the Temples and other tragedies, marked by fasting and the reading of Lamentations."),
            "leilselichot" => new("selichot", title, "minor", "Many Ashkenazi communities begin their pre–Rosh Hashanah penitential prayers on this night. Other communities begin earlier in Elul.", "nightfall"),
            "yomhashoah" => Modern("yom-hashoah", title, "Holocaust Remembrance Day honors the memory of those murdered in the Holocaust and remembers Jewish resistance."),
            "yomhazikaron" => Modern("yom-hazikaron", title, "Israel's Memorial Day remembers fallen service members and victims of terrorism."),
            "yomhaatzmaut" => Modern("yom-haatzmaut", title, "Israel's Independence Day commemorates the establishment of the State of Israel. Its observance date can be adjusted around Shabbat."),
            "yomyerushalayim" => Modern("yom-yerushalayim", title, "Jerusalem Day marks the events of June 1967. The meaning and observance of this modern commemoration vary across communities."),
            "yomhaaliyah" => Modern("yom-haaliyah", title, "An Israeli observance recognizing immigration to Israel and the contributions of immigrants."),
            "sigd" => Modern("sigd", title, "An Ethiopian Jewish holiday of prayer, renewal of the covenant, and longing for Jerusalem, observed fifty days after Yom Kippur."),
            _ => null,
        };
        if (known is not null)
        {
            return known;
        }
        if (occurrence.Subcategory == "shabbat")
        {
            var explanation = normalized switch
            {
                "shabbatshirah" => "The Shabbat when the Song at the Sea is read in the weekly Torah portion.",
                "shabbatshekalim" => "A special Torah reading recalls the half-shekel contribution for the communal service.",
                "shabbatzachor" => "A special Torah reading before Purim recalls Amalek's attack on Israel.",
                "shabbatparah" => "A special Torah reading about the red heifer and ritual purification precedes Passover.",
                "shabbathachodesh" => "A special reading introduces Nisan and the commandments associated with the first Passover.",
                "shabbathagadol" => "The Shabbat before Passover, traditionally a time for teaching about the coming festival.",
                "shabbatchazon" => "The Shabbat before Tisha B’Av takes its name from the opening of its haftarah in Isaiah.",
                "shabbatnachamu" => "The Shabbat after Tisha B’Av begins a series of haftarot of consolation.",
                "shabbatshuva" => "The Shabbat between Rosh Hashanah and Yom Kippur emphasizes returning to God and repentance.",
                _ => "A Shabbat distinguished by a special reading or theme in the Jewish calendar.",
            };
            return Entry(normalized, title, "specialShabbat", explanation);
        }
        return new(normalized, title, occurrence.Subcategory == "modern" ? "modern" : "minor", "A Jewish calendar observance. Follow the source link for its background and community-specific customs.", "civilDate");
    }

    private static Description Entry(string key, string title, string category, string explanation) => new(key, title, category, explanation, "previousSunset");
    private static Description Fast(string key, string title, string explanation) => new(key, title, "fast", explanation, "dawn");
    private static Description Modern(string key, string title, string explanation) => new(key, title, "modern", explanation, "civilDate");
    private static string Normalize(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]", "", RegexOptions.CultureInvariant);
}
