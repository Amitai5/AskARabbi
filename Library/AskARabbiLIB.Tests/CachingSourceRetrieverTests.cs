using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class CachingSourceRetrieverTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_EquivalentSuccessfulQueries_ReusesSnapshot()
    {
        // Arrange
        var inner = new RecordingRetriever(_ => Task.FromResult<IReadOnlyList<SourceRetrievalHit>>([CreateHit()]));
        var cache = new CachingSourceRetriever(inner);
        var firstQuery = new SourceRetrievalQuery
        {
            QueryText = "  Lighting A LAMP  ",
            Languages = ["Hebrew", "English"],
            SourceKeys = ["collection:Talmud", "collection:Torah"],
            CandidateLimit = 20,
        };
        var equivalentQuery = firstQuery with
        {
            QueryText = "lighting a lamp",
            Languages = ["English", "Hebrew", "English"],
            SourceKeys = ["collection:Torah", "collection:Talmud"],
        };

        // Act
        var first = await cache.SearchAsync(firstQuery);
        var second = await cache.SearchAsync(equivalentQuery);

        // Assert
        Assert.AreSame(first, second);
        Assert.AreEqual(1, inner.SearchCallCount);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_ExpiredEntry_RetrievesFreshSnapshot()
    {
        // Arrange
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero));
        var inner = new RecordingRetriever(_ => Task.FromResult<IReadOnlyList<SourceRetrievalHit>>([CreateHit()]));
        var cache = new CachingSourceRetriever(inner, new SourceRetrieverCacheOptions { Duration = TimeSpan.FromSeconds(5) }, time);
        var query = new SourceRetrievalQuery { QueryText = "lamp" };
        await cache.SearchAsync(query);

        // Act
        time.Advance(TimeSpan.FromSeconds(5));
        await cache.SearchAsync(query);

        // Assert
        Assert.AreEqual(2, inner.SearchCallCount);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task SearchAsync_FailedRequest_DoesNotCacheFailure()
    {
        // Arrange
        var attempt = 0;
        var inner = new RecordingRetriever(_ => ++attempt == 1
            ? Task.FromException<IReadOnlyList<SourceRetrievalHit>>(new HttpRequestException("temporary"))
            : Task.FromResult<IReadOnlyList<SourceRetrievalHit>>([CreateHit()]));
        var cache = new CachingSourceRetriever(inner);
        var query = new SourceRetrievalQuery { QueryText = "lamp" };

        // Act
        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => cache.SearchAsync(query));
        var recovered = await cache.SearchAsync(query);
        var cached = await cache.SearchAsync(query);

        // Assert
        Assert.HasCount(1, recovered);
        Assert.AreSame(recovered, cached);
        Assert.AreEqual(2, inner.SearchCallCount);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_CapacityReached_EvictsOldestEntry()
    {
        // Arrange
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero));
        var inner = new RecordingRetriever(_ => Task.FromResult<IReadOnlyList<SourceRetrievalHit>>([CreateHit()]));
        var cache = new CachingSourceRetriever(inner, new SourceRetrieverCacheOptions { MaximumEntries = 1 }, time);
        await cache.SearchAsync(new SourceRetrievalQuery { QueryText = "first" });

        // Act
        time.Advance(TimeSpan.FromSeconds(1));
        await cache.SearchAsync(new SourceRetrievalQuery { QueryText = "second" });
        await cache.SearchAsync(new SourceRetrievalQuery { QueryText = "first" });

        // Assert
        Assert.AreEqual(3, inner.SearchCallCount);
    }

    [TestMethod]
    [DataRow("duration-short")]
    [DataRow("duration-long")]
    [DataRow("capacity-low")]
    [DataRow("capacity-high")]
    [TestCategory("Unit")]
    public void Constructor_InvalidCacheOptions_Throws(string scenario)
    {
        // Arrange
        var inner = new RecordingRetriever(_ => Task.FromResult<IReadOnlyList<SourceRetrievalHit>>([]));
        var options = scenario switch
        {
            "duration-short" => new SourceRetrieverCacheOptions { Duration = TimeSpan.Zero },
            "duration-long" => new SourceRetrieverCacheOptions { Duration = TimeSpan.FromHours(2) },
            "capacity-low" => new SourceRetrieverCacheOptions { MaximumEntries = 0 },
            "capacity-high" => new SourceRetrieverCacheOptions { MaximumEntries = 10_001 },
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };

        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CachingSourceRetriever(inner, options));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetContextAsync_AlwaysDelegatesToInnerRetriever()
    {
        // Arrange
        var inner = new RecordingRetriever(_ => Task.FromResult<IReadOnlyList<SourceRetrievalHit>>([]));
        var cache = new CachingSourceRetriever(inner);

        // Act
        await cache.GetContextAsync("document", 2, 3);
        await cache.GetContextAsync("document", 2, 3);

        // Assert
        Assert.AreEqual(2, inner.ContextCallCount);
    }

    private static SourceRetrievalHit CreateHit() => new(new SourceSegment
    {
        SegmentId = "segment-1",
        DocumentId = "document-1",
        CanonicalReference = "Shabbat 20a:1",
        DocumentOrdinal = 0,
        Text = "A lamp may not be kindled.",
        Title = "Shabbat",
        HebrewTitle = "שבת",
        Language = "English",
        LanguageCode = "en",
        Collection = "Talmud",
        Categories = ["Talmud"],
        Version = "Test",
        License = "CC-BY",
        LicenseCategory = SourceLicenseCategory.CcBy,
        SourceUrl = "https://example.test/source",
        FilePath = "source.md",
    }, 1, false);

    private sealed class RecordingRetriever : ISourceRetriever
    {
        private readonly Func<SourceRetrievalQuery, Task<IReadOnlyList<SourceRetrievalHit>>> search;

        internal RecordingRetriever(Func<SourceRetrievalQuery, Task<IReadOnlyList<SourceRetrievalHit>>> search)
        {
            this.search = search;
        }

        internal int SearchCallCount { get; private set; }

        internal int ContextCallCount { get; private set; }

        public Task<IReadOnlyList<SourceRetrievalHit>> SearchAsync(SourceRetrievalQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SearchCallCount++;
            return search(query);
        }

        public Task<IReadOnlyList<SourceSegment>> GetContextAsync(string documentId, int documentOrdinal, int radius, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ContextCallCount++;
            return Task.FromResult<IReadOnlyList<SourceSegment>>([]);
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow;

        internal MutableTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => utcNow;

        internal void Advance(TimeSpan duration)
        {
            utcNow += duration;
        }
    }
}
