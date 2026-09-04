using System.Text.Json.Nodes;

namespace AskARabbiLIB.DvarTorah;

internal static class WeeklyDvarTorahReviewSchema
{
    internal static BinaryData ForEvidence(string template, IReadOnlyList<string> evidenceIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(evidenceIds);
        if (evidenceIds.Count == 0 || evidenceIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 64 || id.Any(character => !char.IsAsciiLetterOrDigit(character))))
        {
            throw new ArgumentException("Review requires known, bounded evidence identifiers.", nameof(evidenceIds));
        }

        var schema = JsonNode.Parse(template) ?? throw new ArgumentException("A review schema is required.", nameof(template));
        var identifierSchema = schema["properties"]?["concerns"]?["items"]?["properties"]?["evidenceIds"]?["items"] as JsonObject;
        if (identifierSchema is null)
        {
            throw new ArgumentException("The review schema must define structured concerns and evidence identifiers.", nameof(template));
        }

        // The reviewer can return only known check names and IDs, never source or article prose.
        identifierSchema["enum"] = new JsonArray(evidenceIds.Distinct(StringComparer.Ordinal).Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
        return BinaryData.FromString(schema.ToJsonString());
    }
}
