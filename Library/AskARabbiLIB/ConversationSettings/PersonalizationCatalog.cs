namespace AskARabbiLIB.ConversationSettings;

/// <summary>Defines the constrained personalization values supported by the production API.</summary>
public static class PersonalizationCatalog
{
    /// <summary>Gets supported response and quotation languages in display order.</summary>
    public static IReadOnlyList<string> Languages { get; } = Array.AsReadOnly<string>(
    [
        "English",
        "French",
        "German",
        "Hebrew",
        "Italian",
        "Persian",
        "Polish",
        "Russian",
        "Spanish",
        "Yiddish",
    ]);

    /// <summary>Gets the U.S. time-zone identifiers currently supported by personalization.</summary>
    public static IReadOnlyList<string> UnitedStatesTimeZones { get; } = Array.AsReadOnly<string>(
    [
        "America/New_York",
        "America/Chicago",
        "America/Denver",
        "America/Phoenix",
        "America/Los_Angeles",
        "America/Anchorage",
        "Pacific/Honolulu",
        "America/Puerto_Rico",
        "Pacific/Guam",
        "Pacific/Pago_Pago",
    ]);
}
