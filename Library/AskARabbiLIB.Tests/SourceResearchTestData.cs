using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;

namespace AskARabbiLIB.Tests;

internal static class SourceResearchTestData
{
    internal const string Question = "At the rosh hashanah seder what does the squash have to do with the actual prayer you read for it?";
    internal const string Reference = "Shulchan Arukh, Orach Chayim 583:1";
    internal const string Quotation = "Eating pumpkin (kra), say: God, may our judgement be ripped up (yikra') and may our merits be called out (yikaru) before You!";
    internal static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
    internal static AIToolExecutionContext Context => new(null, Now)
    {
        SourceFilters = new SourceRetrievalQuery { Languages = ["English", "Hebrew"], Collections = ["Halakhah"], SourceKeys = ["work:shulchan-arukh"] },
    };

    internal static SourceSegment Passage(string? text = null, string reference = Reference) => new()
    {
        SegmentId = reference, DocumentId = "religious-source", DocumentOrdinal = 0,
        CanonicalReference = reference, Text = text ?? "Customary foods for Rosh Hashanah. " + Quotation,
        Title = "Shulchan Arukh, Orach Chayim", HebrewTitle = "שולחן ערוך", Language = "English", LanguageCode = "en",
        Collection = "Halakhah", Categories = ["Halakhah"], Version = "Test excerpt", License = "CC0",
        LicenseCategory = SourceLicenseCategory.PublicDomain, SourceUrl = "https://www.sefaria.org/Shulchan_Arukh,_Orach_Chayim.583.1", FilePath = "fixture.md",
    };

    internal sealed class Sources : ISourceRetriever, ICanonicalSourceReader
    {
        internal List<SourceRetrievalQuery> Searches { get; } = [];
        internal List<(string Reference, SourceRetrievalQuery Filters)> Reads { get; } = [];
        internal IReadOnlyList<SourceSegment> Results { get; set; } = [Passage()];
        internal bool InitialMiss { get; init; }
        internal bool Empty { get; init; }

        public Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Searches.Add(query);
            IReadOnlyList<SourceRetrievalHit> hits = Empty ? [] : InitialMiss && query.QueryText?.Contains("squash", StringComparison.Ordinal) == true
                ? [new SourceRetrievalHit(Passage("The Rosh Hashanah Musaf prayers and shofar obligations are discussed.", "Shulchan Arukh, Orach Chayim 591:1"), 1, false)]
                : Results.Select(source => new SourceRetrievalHit(source, 1, false)).ToArray();
            return Task.FromResult(hits);
        }

        public Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SourceSegment>>([]);

        public Task<IReadOnlyList<SourceSegment>> ReadAsync(string reference, SourceRetrievalQuery filters, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads.Add((reference, filters));
            return Task.FromResult(Empty ? (IReadOnlyList<SourceSegment>)[] : Results);
        }
    }
}
