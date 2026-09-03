using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class MongoWeeklyDvarTorahTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void MongoDatabaseOptions_DefaultDvarTorahCollection_UsesProductionCollectionName()
    {
        var options = new MongoDatabaseOptions();

        Assert.AreEqual("WeeklyAIDvarTorahs", options.DvarTorahCollectionName);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void WeeklyDocument_ShabbatDate_RoundTripsAsSortableInvariantString()
    {
        var document = new MongoWeeklyDvarTorahDocument
        {
            Id = "diaspora:2026-09-05",
            ShabbatDate = new DateOnly(2026, 9, 5),
            HebrewDate = "23 Elul, 5786",
            Parashah = "Nitzavim-Vayeilech",
            InIsrael = false,
            Status = MongoWeeklyDvarTorahStore.PublishedStatus,
            Title = "A weekly teaching",
            Body = "Body",
            GeneratorVersion = "test-v1",
            CentralTeaching = "Choose responsibility.",
            Tags = ["responsibility", "community", "current events"],
            Sources =
            [
                new MongoWeeklyDvarTorahSourceDocument
                {
                    SourceId = "T1",
                    Kind = "Torah",
                    Title = "Deuteronomy",
                    Publisher = "Test edition",
                    SourceUrl = "https://www.sefaria.org/Deuteronomy.29.9",
                    Excerpt = "You stand this day.",
                    RetrievedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
                    CanonicalReference = "Deuteronomy 29:9",
                    License = "CC-BY",
                },
            ],
            TorahGroundingPercent = 80,
            SafetyReviewVersion = "review-v1",
            Model = "model-v1",
            NewsWindowStartedAtUtc = new DateTime(2026, 8, 24, 18, 0, 0, DateTimeKind.Utc),
            NewsWindowEndedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
            GeneratedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
            PublishedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
            LastAttemptedAtUtc = new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
        };

        var bson = document.ToBsonDocument();
        var result = BsonSerializer.Deserialize<MongoWeeklyDvarTorahDocument>(bson);

        Assert.AreEqual("2026-09-05", bson["shabbatDate"].AsString);
        Assert.AreEqual(document.ShabbatDate, result.ShabbatDate);
        Assert.AreEqual(document.Body, result.Body);
        CollectionAssert.AreEqual(document.Tags, result.Tags);
        Assert.AreEqual("T1", result.Sources?[0].SourceId);
        Assert.AreEqual(80, result.TorahGroundingPercent);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateLatestPublishedIndex_LatestQuery_UsesFilterFieldsBeforeDescendingDate()
    {
        var registry = BsonSerializer.SerializerRegistry;
        var serializer = registry.GetSerializer<MongoWeeklyDvarTorahDocument>();

        var index = MongoWeeklyDvarTorahStore.CreateLatestPublishedIndex();
        var keys = index.Keys.Render(new RenderArgs<MongoWeeklyDvarTorahDocument>(serializer, registry));
        var sort = MongoWeeklyDvarTorahStore.CreateLatestPublishedSort().Render(new RenderArgs<MongoWeeklyDvarTorahDocument>(serializer, registry));

        CollectionAssert.AreEqual(new[] { "inIsrael", "status", "shabbatDate" }, keys.Names.ToArray());
        Assert.AreEqual(-1, keys["shabbatDate"].AsInt32);
        CollectionAssert.AreEqual(new[] { "shabbatDate" }, sort.Names.ToArray());
        Assert.AreEqual(-1, sort["shabbatDate"].AsInt32);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateTagIndex_SearchableMetadata_IndexesTags()
    {
        var registry = BsonSerializer.SerializerRegistry;
        var serializer = registry.GetSerializer<MongoWeeklyDvarTorahDocument>();

        var index = MongoWeeklyDvarTorahStore.CreateTagIndex();
        var keys = index.Keys.Render(new RenderArgs<MongoWeeklyDvarTorahDocument>(serializer, registry));

        CollectionAssert.AreEqual(new[] { "tags" }, keys.Names.ToArray());
        Assert.AreEqual(1, keys["tags"].AsInt32);
        Assert.AreEqual("ix_weeklyDvarTorah_tags", index.Options?.Name);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateArchiveFilter_SearchInput_IsEscapedAndRestrictedToPastPublishedCycle()
    {
        var registry = BsonSerializer.SerializerRegistry;
        var serializer = registry.GetSerializer<MongoWeeklyDvarTorahDocument>();

        var filter = MongoWeeklyDvarTorahStore.CreateArchiveFilter(false, new DateOnly(2026, 9, 5), "care.*");
        var serialized = filter.Render(new RenderArgs<MongoWeeklyDvarTorahDocument>(serializer, registry)).ToJson();

        StringAssert.Contains(serialized, "\"inIsrael\" : false");
        StringAssert.Contains(serialized, MongoWeeklyDvarTorahStore.PublishedStatus);
        StringAssert.Contains(serialized, "\"shabbatDate\" : { \"$lt\"");
        StringAssert.Contains(serialized, "care\\\\.\\\\*");
        StringAssert.Contains(serialized, "\"tags\"");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateArchiveProjection_ExcludesArticleBodyAndSources()
    {
        var registry = BsonSerializer.SerializerRegistry;
        var serializer = registry.GetSerializer<MongoWeeklyDvarTorahDocument>();

        var rendered = MongoWeeklyDvarTorahStore.CreateArchiveProjection().Render(new RenderArgs<MongoWeeklyDvarTorahDocument>(serializer, registry)).Document;

        CollectionAssert.AreEquivalent(new[] { "_id", "shabbatDate", "hebrewDate", "parashah", "holiday", "inIsrael", "title", "tags", "publishedAtUtc" }, rendered.Names.ToArray());
        Assert.IsFalse(rendered.Contains("body"));
        Assert.IsFalse(rendered.Contains("sources"));
        Assert.IsFalse(rendered.Contains("centralTeaching"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ToArchiveItem_CompleteMetadata_ReturnsOnlyTopThreeTags()
    {
        var document = new MongoWeeklyDvarTorahArchiveDocument
        {
            Id = "diaspora:2026-09-05",
            ShabbatDate = new DateOnly(2026, 9, 5),
            HebrewDate = "23 Elul, 5786",
            Parashah = "Nitzavim",
            InIsrael = false,
            Title = "A weekly teaching",
            Tags = ["responsibility", "community", "dignity", "fourth"],
            PublishedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
        };

        var result = MongoWeeklyDvarTorahStore.ToArchiveItem(document);

        Assert.AreEqual(document.Id, result.Week.WeekKey);
        Assert.AreEqual(document.Title, result.Title);
        CollectionAssert.AreEqual(new[] { "responsibility", "community", "dignity" }, result.Tags.ToArray());
        Assert.AreEqual(TimeSpan.Zero, result.PublishedAtUtc.Offset);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateActiveOwnedLeaseFilter_RequiresExactUnexpiredLease()
    {
        var registry = BsonSerializer.SerializerRegistry;
        var serializer = registry.GetSerializer<MongoWeeklyDvarTorahDocument>();
        var week = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahWeek(new DateOnly(2026, 9, 5), "23 Elul, 5786", "Nitzavim", null, false);
        var expiration = new DateTimeOffset(2026, 8, 31, 18, 30, 0, TimeSpan.Zero);
        var lease = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahGenerationLease(week, "lease-1", expiration);

        var filter = MongoWeeklyDvarTorahStore.CreateActiveOwnedLeaseFilter(lease, expiration.AddTicks(-1));
        var bson = filter.Render(new RenderArgs<MongoWeeklyDvarTorahDocument>(serializer, registry));
        var serialized = bson.ToJson();

        StringAssert.Contains(serialized, week.WeekKey);
        StringAssert.Contains(serialized, MongoWeeklyDvarTorahStore.GeneratingStatus);
        StringAssert.Contains(serialized, "lease-1");
        StringAssert.Contains(serialized, "\"generationLeaseExpiresAtUtc\"");
        StringAssert.Contains(serialized, "\"$gt\"");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task PublicWriteBoundaries_InvalidLeaseOrArticle_ThrowBeforeDatabaseIo()
    {
        var store = CreateStore();
        var week = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahWeek(new DateOnly(2026, 9, 5), "23 Elul, 5786", "Nitzavim", null, false);
        var lease = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahGenerationLease(week, "lease-1", new DateTimeOffset(2026, 8, 31, 18, 30, 0, TimeSpan.Zero));
        var article = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahArticle(week, "Title", "Body", "test-v1", new DateTimeOffset(2026, 8, 31, 18, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 8, 31, 18, 0, 0, TimeSpan.Zero));
        var acquiredAtUtc = new DateTimeOffset(2026, 8, 31, 18, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.GetPublishedAsync(null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.TryAcquireGenerationLeaseAsync(null!, "lease", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.TryAcquireGenerationLeaseAsync(week, " ", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.TryAcquireGenerationLeaseAsync(week, new string('x', 161), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.TryAcquireGenerationLeaseAsync(week, "lease", acquiredAtUtc, acquiredAtUtc));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.PublishAsync(null!, article));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.PublishAsync(lease, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.RecordGenerationFailureAsync(null!, "Failure", DateTimeOffset.UtcNow));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.RecordGenerationFailureAsync(lease, " ", DateTimeOffset.UtcNow));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.RecordGenerationFailureAsync(lease, new string('x', 121), DateTimeOffset.UtcNow));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task Publish_ArticleForDifferentWeek_ThrowsBeforeDatabaseIo()
    {
        var store = CreateStore();
        var leasedWeek = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahWeek(new DateOnly(2026, 9, 5), "23 Elul, 5786", "Nitzavim", null, false);
        var articleWeek = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahWeek(new DateOnly(2026, 9, 12), "30 Elul, 5786", "Vayeilech", null, false);
        var lease = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahGenerationLease(leasedWeek, "lease-1", DateTimeOffset.UtcNow.AddMinutes(30));
        var article = new AskARabbiLIB.DvarTorah.WeeklyDvarTorahArticle(articleWeek, "Title", "Body", "test-v1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.PublishAsync(lease, article));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ToDomain_CompletePublication_HydratesArticleAndMetadata()
    {
        var document = CreatePublishedDocument();

        var article = MongoWeeklyDvarTorahStore.ToDomain(document);

        Assert.AreEqual(document.Id, article.Week.WeekKey);
        Assert.AreEqual("A weekly teaching", article.Title);
        Assert.IsNotNull(article.Metadata);
        Assert.AreEqual(80, article.Metadata.TorahGroundingPercent);
        Assert.HasCount(2, article.Metadata.Sources);
        Assert.IsNull(article.Metadata.Sources[0].PublishedAtUtc);
        Assert.IsNotNull(article.Metadata.Sources[1].PublishedAtUtc);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ToDomain_LegacyPublicationWithoutMetadata_HydratesArticleWithoutMetadata()
    {
        var document = CreatePublishedDocument();
        ClearMetadata(document);

        var article = MongoWeeklyDvarTorahStore.ToDomain(document);

        Assert.IsNull(article.Metadata);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ToDomain_IncompletePublicationFields_FailClosed()
    {
        Action<MongoWeeklyDvarTorahDocument>[] mutations =
        [
            document => document.Status = MongoWeeklyDvarTorahStore.FailedStatus,
            document => document.Title = null,
            document => document.Body = null,
            document => document.GeneratorVersion = null,
            document => document.GeneratedAtUtc = null,
            document => document.PublishedAtUtc = null,
        ];

        foreach (var mutate in mutations)
        {
            var document = CreatePublishedDocument();
            mutate(document);

            Assert.ThrowsExactly<InvalidOperationException>(() => MongoWeeklyDvarTorahStore.ToDomain(document));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ToDomain_WeekKeyDoesNotMatchDocumentId_FailsClosed()
    {
        var document = CreatePublishedDocument(id: "diaspora:2026-09-12");

        Assert.ThrowsExactly<InvalidOperationException>(() => MongoWeeklyDvarTorahStore.ToDomain(document));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ToDomain_IncompleteMetadataFields_FailClosed()
    {
        Action<MongoWeeklyDvarTorahDocument>[] mutations =
        [
            document => document.CentralTeaching = null,
            document => document.Tags = null,
            document => document.Sources = null,
            document => document.TorahGroundingPercent = null,
            document => document.SafetyReviewVersion = null,
            document => document.Model = null,
            document => document.NewsWindowStartedAtUtc = null,
            document => document.NewsWindowEndedAtUtc = null,
        ];

        foreach (var mutate in mutations)
        {
            var document = CreatePublishedDocument();
            mutate(document);

            Assert.ThrowsExactly<InvalidOperationException>(() => MongoWeeklyDvarTorahStore.ToDomain(document));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ToDomain_ProgressivelyIncompleteMetadata_FailsClosed()
    {
        for (var retainedFieldIndex = 1; retainedFieldIndex < 8; retainedFieldIndex++)
        {
            var document = CreatePublishedDocument();
            ClearMetadata(document);
            SetMetadataField(document, retainedFieldIndex);

            Assert.ThrowsExactly<InvalidOperationException>(() => MongoWeeklyDvarTorahStore.ToDomain(document));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ToDomain_UnknownPersistedSourceKind_FailsClosed()
    {
        var document = CreatePublishedDocument(torahSourceKind: "Unsupported");

        Assert.ThrowsExactly<InvalidOperationException>(() => MongoWeeklyDvarTorahStore.ToDomain(document));
    }

    private static MongoWeeklyDvarTorahStore CreateStore()
    {
        var client = new MongoClient("mongodb://localhost:27017");
        return new MongoWeeklyDvarTorahStore(client.GetDatabase("askarabbi-tests"), new MongoDatabaseOptions());
    }

    private static MongoWeeklyDvarTorahDocument CreatePublishedDocument(string id = "diaspora:2026-09-05", string? torahSourceKind = null) => new()
    {
        Id = id,
        ShabbatDate = new DateOnly(2026, 9, 5),
        HebrewDate = "23 Elul, 5786",
        Parashah = "Nitzavim",
        InIsrael = false,
        Status = MongoWeeklyDvarTorahStore.PublishedStatus,
        Title = "A weekly teaching",
        Body = "Body",
        GeneratorVersion = "test-v1",
        CentralTeaching = "Choose responsibility.",
        Tags = ["responsibility", "community", "current events"],
        Sources =
        [
            new MongoWeeklyDvarTorahSourceDocument
            {
                SourceId = "T1",
                Kind = torahSourceKind ?? nameof(AskARabbiLIB.DvarTorah.WeeklyDvarTorahSourceKind.Torah),
                Title = "Deuteronomy",
                Publisher = "Test edition",
                SourceUrl = "https://www.sefaria.org/Deuteronomy.29.9",
                Excerpt = "You stand this day.",
                RetrievedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
                CanonicalReference = "Deuteronomy 29:9",
                License = "CC-BY",
            },
            new MongoWeeklyDvarTorahSourceDocument
            {
                SourceId = "N1",
                Kind = nameof(AskARabbiLIB.DvarTorah.WeeklyDvarTorahSourceKind.News),
                Title = "Public development",
                Publisher = "Public publisher",
                SourceUrl = "https://example.test/story",
                Excerpt = "A bounded news fact.",
                RetrievedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
                PublishedAtUtc = new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc),
            },
        ],
        TorahGroundingPercent = 80,
        SafetyReviewVersion = "review-v1",
        Model = "model-v1",
        NewsWindowStartedAtUtc = new DateTime(2026, 8, 24, 18, 0, 0, DateTimeKind.Utc),
        NewsWindowEndedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
        GeneratedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
        PublishedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
        LastAttemptedAtUtc = new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc),
    };

    private static void ClearMetadata(MongoWeeklyDvarTorahDocument document)
    {
        document.CentralTeaching = null;
        document.Tags = null;
        document.Sources = null;
        document.TorahGroundingPercent = null;
        document.SafetyReviewVersion = null;
        document.Model = null;
        document.NewsWindowStartedAtUtc = null;
        document.NewsWindowEndedAtUtc = null;
    }

    private static void SetMetadataField(MongoWeeklyDvarTorahDocument document, int fieldIndex)
    {
        switch (fieldIndex)
        {
            case 1:
                document.Tags = ["one", "two", "three"];
                break;
            case 2:
                document.Sources = CreatePublishedDocument().Sources;
                break;
            case 3:
                document.TorahGroundingPercent = 80;
                break;
            case 4:
                document.SafetyReviewVersion = "review-v1";
                break;
            case 5:
                document.Model = "model-v1";
                break;
            case 6:
                document.NewsWindowStartedAtUtc = new DateTime(2026, 8, 24, 18, 0, 0, DateTimeKind.Utc);
                break;
            case 7:
                document.NewsWindowEndedAtUtc = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc);
                break;
        }
    }
}
