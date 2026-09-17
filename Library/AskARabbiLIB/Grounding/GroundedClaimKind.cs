using System.Text.Json.Serialization;

namespace AskARabbiLIB.Grounding;

/// <summary>Distinguishes cited teaching from independently reviewed background and honest uncertainty.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GroundedClaimKind>))]
internal enum GroundedClaimKind
{
    Source,
    Background,
    Uncertainty,
}
