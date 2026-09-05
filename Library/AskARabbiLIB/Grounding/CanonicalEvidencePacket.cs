using System.Security.Cryptography;
using System.Text;
using AskARabbiLIB.Retrieval;

namespace AskARabbiLIB.Grounding;

/// <summary>Keeps complete passages in bounded, contiguous, citation-addressable reading blocks.</summary>
internal static class CanonicalEvidencePacket
{
    internal static EvidencePacket Create(IReadOnlyList<SourceSegment> segments)
    {
        var items = new List<EvidenceItem>();
        foreach (var document in segments.DistinctBy(segment => segment.SegmentId).GroupBy(segment => segment.DocumentId))
        {
            var block = new List<SourceSegment>();
            var length = 0;
            foreach (var segment in document.OrderBy(segment => segment.DocumentOrdinal))
            {
                if (block.Count > 0 && (length + segment.Text.Length > 6_000 || segment.DocumentOrdinal != block[^1].DocumentOrdinal + 1))
                {
                    AddBlock(block, items);
                    block.Clear();
                    length = 0;
                }
                block.Add(segment);
                length += segment.Text.Length + segment.CanonicalReference.Length + 4;
            }
            AddBlock(block, items);
        }
        if (items.Sum(item => item.PresentedText.Length) > 100_000)
        {
            throw new InvalidDataException("The requested canonical reading exceeds the bounded conversation evidence budget.");
        }
        return new EvidencePacket(items, items.Sum(item => item.PresentedText.Length));
    }

    private static void AddBlock(IReadOnlyList<SourceSegment> block, ICollection<EvidenceItem> items)
    {
        if (block.Count == 0)
        {
            return;
        }
        var first = block[0];
        var text = block.Count == 1 ? first.Text : string.Join("\n\n", block.Select(segment => $"[{segment.CanonicalReference}]\n{segment.Text}"));
        var endAddress = block[^1].CanonicalReference[(block[^1].CanonicalReference.LastIndexOf(' ') + 1)..];
        var source = block.Count == 1 ? first : first with
        {
            SegmentId = "range:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', block.Select(segment => segment.SegmentId))))),
            CanonicalReference = first.CanonicalReference + "-" + endAddress,
            Text = text,
            OriginalCharacterCount = text.Length,
        };
        items.Add(new EvidenceItem($"E{items.Count + 1}", source, text, false, text.Length));
    }
}
