using AskARabbiLIB.Calendar;

namespace AskARabbi.Api.Calendar;

/// <summary>Reviewed GeoNames locations; timezone and candle defaults cannot be supplied by a browser.</summary>
public static class CalendarLocationCatalog
{
    public static IReadOnlyList<CalendarLocation> Cities { get; } = Array.AsReadOnly<CalendarLocation>([
        City("5128581", "New York, United States", "America/New_York"),
        City("5368361", "Los Angeles, United States", "America/Los_Angeles"),
        City("4887398", "Chicago, United States", "America/Chicago"),
        City("6167865", "Toronto, Canada", "America/Toronto"),
        City("2643743", "London, United Kingdom", "Europe/London"),
        City("2988507", "Paris, France", "Europe/Paris"),
        City("281184", "Jerusalem, Israel", "Asia/Jerusalem", 40),
        City("293397", "Tel Aviv, Israel", "Asia/Jerusalem"),
        City("294801", "Haifa, Israel", "Asia/Jerusalem", 30),
        City("2158177", "Melbourne, Australia", "Australia/Melbourne"),
    ]);

    private static CalendarLocation City(string id, string label, string timeZone, int candleMinutes = 18) => new() { Kind = "city", Id = id, Label = label, TimeZone = timeZone, DefaultCandleLightingMinutes = candleMinutes };
}
