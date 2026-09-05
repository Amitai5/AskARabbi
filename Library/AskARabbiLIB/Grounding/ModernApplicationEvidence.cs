using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;

namespace AskARabbiLIB.Grounding;

/// <summary>Supplies reviewed physical context for applying a textual category to modern technology.</summary>
internal static class ModernApplicationEvidence
{
    internal static EvidencePacket Append(EvidencePacket packet, IReadOnlyList<string> requestedReferences)
    {
        // Technical background cannot replace a disabled or missing religious source.
        if (requestedReferences.Count < 2 || !requestedReferences.Contains("Exodus 35:3") || !packet.Items.Any(item => item.Source.CanonicalReference == "Exodus 35:3"))
        {
            return packet;
        }

        const string text = "The air/fuel mixture is ignited by a spark from the spark plug.";
        var segment = new SourceSegment
        {
            SegmentId = "technical:doe:gasoline-ignition:2026-09-05",
            DocumentId = "technical:doe:gasoline-cars",
            CanonicalReference = "How Do Gasoline Cars Work? — Spark ignition",
            DocumentOrdinal = 0,
            Text = text,
            Title = "U.S. Department of Energy — How Do Gasoline Cars Work?",
            HebrewTitle = "",
            Language = "English",
            LanguageCode = "en",
            Collection = "Technical background",
            Categories = ["Technical background"],
            Version = "U.S. Department of Energy, reviewed September 5, 2026",
            License = "Public Domain (U.S. government)",
            LicenseCategory = SourceLicenseCategory.PublicDomain,
            SourceUrl = "https://afdc.energy.gov/vehicles/how-do-gasoline-cars-work",
            FilePath = "Grounding/ModernApplicationEvidence.cs",
            UsageNote = "Physical operation of a conventional gasoline engine only, not a religious ruling. Explain burning fuel as a modern application of the cited kindling category. Do not claim ancient texts mention cars, or extend this combustion explanation to electric vehicles or emergency exceptions.",
        };
        var item = new EvidenceItem($"E{packet.Items.Count + 1}", segment, text, false, text.Length);
        return new EvidencePacket([.. packet.Items, item], packet.CharacterCount + text.Length);
    }
}
