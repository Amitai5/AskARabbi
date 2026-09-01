using System.Text.Json;
using System.Text.Json.Serialization;

namespace AskARabbiLIB.Profiles;

/// <summary>Reads and writes the strict local JSON representation of an AskARabbi user profile.</summary>
public static class UserProfileJsonSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    /// <summary>Deserializes and validates one JSON profile.</summary>
    /// <param name="json">JSON profile content.</param>
    /// <param name="currentDate">Current date used for birth-date validation.</param>
    /// <returns>A normalized and validated user profile.</returns>
    public static UserProfile Deserialize(string json, DateOnly currentDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var profile = JsonSerializer.Deserialize<UserProfile>(json, SerializerOptions) ?? throw new InvalidDataException("The profile JSON did not contain a profile object.");
        var normalized = Normalize(profile);
        normalized.Validate(currentDate);
        return normalized;
    }

    /// <summary>Serializes one validated profile as indented camel-case JSON.</summary>
    /// <param name="profile">Profile to serialize.</param>
    /// <param name="currentDate">Current date used for birth-date validation.</param>
    /// <returns>The normalized JSON profile.</returns>
    public static string Serialize(UserProfile profile, DateOnly currentDate)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var normalized = Normalize(profile);
        normalized.Validate(currentDate);
        return JsonSerializer.Serialize(normalized, SerializerOptions);
    }

    private static UserProfile Normalize(UserProfile profile) => profile with
    {
        Name = profile.Name?.Trim() ?? string.Empty,
        Bio = NormalizeOptional(profile.Bio),
        ReligiousBackground = NormalizeOptional(profile.ReligiousBackground),
        JewishHeritage = profile.JewishHeritage?.Trim() ?? string.Empty,
        BirthTimeZone = NormalizeOptional(profile.BirthTimeZone),
    };

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
