using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class WeeklyDvarTorahContentMetadataTests
{
    private static readonly DateTimeOffset RetrievedAtUtc = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_CompleteMetadata_NormalizesTagsAndTimes()
    {
        var source = CreateTorahSource();

        var metadata = new WeeklyDvarTorahContentMetadata("Choose responsibility over indifference.", [" Responsibility ", "PARASHA", "Current Events"], [source], 80, "review-v1", "model-v1", RetrievedAtUtc.AddDays(-7).ToOffset(TimeSpan.FromHours(-4)), RetrievedAtUtc);

        CollectionAssert.AreEqual(new[] { "responsibility", "parasha", "current events" }, metadata.Tags.ToArray());
        Assert.AreEqual(TimeSpan.Zero, metadata.NewsWindowStartedAtUtc.Offset);
        Assert.AreEqual(80, metadata.TorahGroundingPercent);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_NoTorahSource_Throws()
    {
        var news = new WeeklyDvarTorahSource("N1", WeeklyDvarTorahSourceKind.News, "News", "Publisher", "https://example.test/news", "A bounded summary.", RetrievedAtUtc, publishedAtUtc: RetrievedAtUtc.AddHours(-1));

        Assert.ThrowsExactly<ArgumentException>(() => new WeeklyDvarTorahContentMetadata("Teaching", ["one", "two", "three"], [news], 80, "review-v1", "model-v1", RetrievedAtUtc.AddDays(-7), RetrievedAtUtc));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void WeeklyDvarTorahSource_TorahWithoutCanonicalReference_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new WeeklyDvarTorahSource("T1", WeeklyDvarTorahSourceKind.Torah, "Deuteronomy", "Edition", "https://www.sefaria.org/Deuteronomy.29.9", "You stand this day.", RetrievedAtUtc));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_InvalidMetadataFields_Throw()
    {
        var source = CreateTorahSource();
        var validTags = new[] { "one", "two", "three" };

        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(centralTeaching: " "));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(centralTeaching: new string('t', 1_201)));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahContentMetadata("Teaching", null!, [source], 80, "review", "model", RetrievedAtUtc.AddDays(-7), RetrievedAtUtc));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(tags: ["one", "two"]));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(tags: Enumerable.Range(1, 21).Select(index => $"tag-{index}").ToArray()));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(tags: ["one", " ", "three"]));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(tags: ["duplicate", "DUPLICATE", "three"]));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(tags: [new string('t', 61), "two", "three"]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WeeklyDvarTorahContentMetadata("Teaching", validTags, null!, 80, "review", "model", RetrievedAtUtc.AddDays(-7), RetrievedAtUtc));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(sources: []));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(sources: Enumerable.Repeat(source, 41).ToArray()));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(sources: [source, null!]));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(sources: [source, source]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateMetadata(torahGroundingPercent: -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateMetadata(torahGroundingPercent: 101));
        Assert.ThrowsExactly<ArgumentException>(() => new WeeklyDvarTorahContentMetadata("Teaching", validTags, [source], 80, "review", "model", RetrievedAtUtc, RetrievedAtUtc.AddTicks(-1)));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(safetyReviewVersion: " "));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(safetyReviewVersion: new string('r', 121)));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(model: " "));
        Assert.ThrowsExactly<ArgumentException>(() => CreateMetadata(model: new string('m', 161)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void WeeklyDvarTorahSource_ValidOptionalFields_NormalizesValuesAndUtcTimes()
    {
        var source = new WeeklyDvarTorahSource(" N-1 ", WeeklyDvarTorahSourceKind.News, " News title ", " Publisher ", "https://example.test/news", " Evidence ", RetrievedAtUtc.ToOffset(TimeSpan.FromHours(-4)), " ", RetrievedAtUtc.AddHours(-1).ToOffset(TimeSpan.FromHours(-4)), " ");

        Assert.AreEqual("N-1", source.SourceId);
        Assert.AreEqual("News title", source.Title);
        Assert.AreEqual("Publisher", source.Publisher);
        Assert.AreEqual("Evidence", source.Excerpt);
        Assert.IsNull(source.CanonicalReference);
        Assert.IsNull(source.License);
        Assert.AreEqual(TimeSpan.Zero, source.RetrievedAtUtc.Offset);
        Assert.AreEqual(TimeSpan.Zero, source.PublishedAtUtc?.Offset);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void WeeklyDvarTorahSource_InvalidProvenanceFields_Throw()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateSource(sourceId: "bad id"));
        Assert.ThrowsExactly<ArgumentException>(() => CreateSource(sourceId: new string('i', 65)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateSource(kind: (WeeklyDvarTorahSourceKind)999));
        Assert.ThrowsExactly<ArgumentException>(() => CreateSource(title: " "));
        Assert.ThrowsExactly<ArgumentException>(() => CreateSource(publisher: new string('p', 201)));
        Assert.ThrowsExactly<ArgumentException>(() => CreateSource(sourceUrl: "not-a-url"));
        Assert.ThrowsExactly<ArgumentException>(() => CreateSource(sourceUrl: "http://example.test/news"));
        Assert.ThrowsExactly<ArgumentException>(() => CreateSource(excerpt: new string('e', 2_001)));
        Assert.ThrowsExactly<ArgumentException>(() => CreateSource(canonicalReference: new string('r', 241)));
        Assert.ThrowsExactly<ArgumentException>(() => CreateSource(license: new string('l', 201)));
        Assert.ThrowsExactly<ArgumentException>(() => CreateSource(publishedAtUtc: RetrievedAtUtc.AddDays(1).AddTicks(1)));
    }

    private static WeeklyDvarTorahSource CreateTorahSource() => new("T1", WeeklyDvarTorahSourceKind.Torah, "Deuteronomy", "Sefaria edition", "https://www.sefaria.org/Deuteronomy.29.9", "You stand this day, all of you.", RetrievedAtUtc, "Deuteronomy 29:9", license: "CC-BY");

    private static WeeklyDvarTorahContentMetadata CreateMetadata(string centralTeaching = "Teaching", IReadOnlyList<string>? tags = null, IReadOnlyList<WeeklyDvarTorahSource>? sources = null, int torahGroundingPercent = 80, string safetyReviewVersion = "review-v1", string model = "model-v1") => new(centralTeaching, tags ?? ["one", "two", "three"], sources ?? [CreateTorahSource()], torahGroundingPercent, safetyReviewVersion, model, RetrievedAtUtc.AddDays(-7), RetrievedAtUtc);

    private static WeeklyDvarTorahSource CreateSource(string sourceId = "N1", WeeklyDvarTorahSourceKind kind = WeeklyDvarTorahSourceKind.News, string title = "News", string publisher = "Publisher", string sourceUrl = "https://example.test/news", string excerpt = "Evidence", string? canonicalReference = null, DateTimeOffset? publishedAtUtc = null, string? license = null) => new(sourceId, kind, title, publisher, sourceUrl, excerpt, RetrievedAtUtc, canonicalReference, publishedAtUtc ?? RetrievedAtUtc.AddHours(-1), license);
}
