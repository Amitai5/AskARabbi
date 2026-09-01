using System.Text.Json.Serialization;

namespace AskARabbiLIB.DvarTorah;

/// <summary>Identifies how a source contributes to a weekly Dvar Torah.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WeeklyDvarTorahSourceKind>))]
public enum WeeklyDvarTorahSourceKind
{
    /// <summary>A passage from the Five Books of Moses.</summary>
    Torah,

    /// <summary>A current-events report or primary public-information release.</summary>
    News,

    /// <summary>An approved supporting source outside the measured Torah evidence.</summary>
    Other,
}
