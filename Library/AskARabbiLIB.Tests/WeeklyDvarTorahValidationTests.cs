using AskARabbiLIB.CurrentEvents;
using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class WeeklyDvarTorahValidationTests
{
    private static readonly DateTimeOffset RetrievedAtUtc = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [TestCategory("Unit")]
    public void WeeklyContentOptions_InvalidIndividualBounds_Throw()
    {
        WeeklyDvarTorahContentOptions[] invalidOptions =
        [
            new() { ResearchWindowDays = 0 },
            new() { ResearchWindowDays = 15 },
            new() { MaximumNewsCandidates = 9 },
            new() { MaximumNewsCandidates = 201 },
            new() { MinimumNewsPublishers = 1 },
            new() { MinimumNewsPublishers = 5 },
            new() { MinimumNewsPublishers = 3, MaximumNewsSources = 2 },
            new() { MaximumNewsSources = 9 },
            new() { MinimumTorahEvidenceItems = 3 },
            new() { MinimumTorahEvidenceItems = 21, MaximumTorahEvidenceItems = 21 },
            new() { MinimumTorahEvidenceItems = 8, MaximumTorahEvidenceItems = 7 },
            new() { MaximumTorahEvidenceItems = 31 },
            new() { MinimumTorahGroundingPercent = 79 },
            new() { MinimumTorahGroundingPercent = 101 },
            new() { MinimumBodyCharacters = 999 },
            new() { MinimumBodyCharacters = 10_001, MaximumBodyCharacters = 10_001 },
            new() { MinimumBodyCharacters = 2_000, MaximumBodyCharacters = 1_999 },
            new() { MaximumBodyCharacters = WeeklyDvarTorahDraft.MaximumBodyCharacters + 1 },
            new() { OverallTimeout = TimeSpan.FromSeconds(59) },
            new() { OverallTimeout = TimeSpan.FromMinutes(31) },
            new() { GeneratorVersion = " " },
            new() { GeneratorVersion = new string('v', 121) },
        ];

        foreach (var options in invalidOptions.Take(invalidOptions.Length - 2))
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(options.Validate);
        }
        Assert.ThrowsExactly<ArgumentException>(invalidOptions[^2].Validate);
        Assert.ThrowsExactly<ArgumentException>(invalidOptions[^1].Validate);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void FreeRssOptions_InvalidIndividualBounds_Throw()
    {
        FreeRssCurrentEventsOptions[] invalidOptions =
        [
            new() { RequestTimeout = TimeSpan.Zero },
            new() { RequestTimeout = TimeSpan.FromMinutes(2).Add(TimeSpan.FromTicks(1)) },
            new() { MaximumFeedBytes = 16_383 },
            new() { MaximumFeedBytes = 10_000_001 },
            new() { MaximumItemsPerFeed = 0 },
            new() { MaximumItemsPerFeed = 201 },
            new() { MaximumTotalItems = 1 },
            new() { MaximumTotalItems = 501 },
        ];

        foreach (var options in invalidOptions)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(options.Validate);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CurrentEventItem_ValidValues_NormalizeTextAndUtcTimes()
    {
        var item = new CurrentEventItem(" Publisher ", " Civic ", " Headline ", " Summary ", "https://example.test/story", RetrievedAtUtc.AddHours(-2).ToOffset(TimeSpan.FromHours(-4)), RetrievedAtUtc.ToOffset(TimeSpan.FromHours(-4)));

        Assert.AreEqual("Publisher", item.Publisher);
        Assert.AreEqual("Civic", item.Category);
        Assert.AreEqual("Headline", item.Title);
        Assert.AreEqual("Summary", item.Summary);
        Assert.AreEqual(TimeSpan.Zero, item.PublishedAtUtc.Offset);
        Assert.AreEqual(TimeSpan.Zero, item.RetrievedAtUtc.Offset);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CurrentEventItem_InvalidExternalValues_Throw()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateCurrentEvent(publisher: " "));
        Assert.ThrowsExactly<ArgumentException>(() => CreateCurrentEvent(category: new string('c', 81)));
        Assert.ThrowsExactly<ArgumentException>(() => CreateCurrentEvent(title: new string('t', 401)));
        Assert.ThrowsExactly<ArgumentException>(() => CreateCurrentEvent(summary: new string('s', 1_201)));
        Assert.ThrowsExactly<ArgumentException>(() => CreateCurrentEvent(sourceUrl: "not-a-url"));
        Assert.ThrowsExactly<ArgumentException>(() => CreateCurrentEvent(sourceUrl: "http://example.test/story"));
        Assert.ThrowsExactly<ArgumentException>(() => CreateCurrentEvent(publishedAtUtc: RetrievedAtUtc.AddDays(1).AddTicks(1)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void FreeNewsFeed_ValidValues_NormalizeLabels()
    {
        var feed = new FreeNewsFeed(" Publisher ", " Technology ", new Uri("https://example.test/feed.xml"));

        Assert.AreEqual("Publisher", feed.Publisher);
        Assert.AreEqual("Technology", feed.Category);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void FreeNewsFeed_PrivateOrMalformedEndpoints_Throw()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new FreeNewsFeed(" ", "General", new Uri("https://example.test/feed")));
        Assert.ThrowsExactly<ArgumentException>(() => new FreeNewsFeed("Publisher", " ", new Uri("https://example.test/feed")));
        Assert.ThrowsExactly<ArgumentNullException>(() => new FreeNewsFeed("Publisher", "General", null!));
        Assert.ThrowsExactly<ArgumentException>(() => new FreeNewsFeed("Publisher", "General", new Uri("feed.xml", UriKind.Relative)));
        Assert.ThrowsExactly<ArgumentException>(() => new FreeNewsFeed("Publisher", "General", new Uri("http://example.test/feed")));
        Assert.ThrowsExactly<ArgumentException>(() => new FreeNewsFeed("Publisher", "General", new Uri("https://127.0.0.1/feed")));
        Assert.ThrowsExactly<ArgumentException>(() => new FreeNewsFeed("Publisher", "General", new Uri("https://user@example.test/feed")));
    }

    private static CurrentEventItem CreateCurrentEvent(string publisher = "Publisher", string category = "Civic", string title = "Headline", string summary = "Summary", string sourceUrl = "https://example.test/story", DateTimeOffset? publishedAtUtc = null) => new(publisher, category, title, summary, sourceUrl, publishedAtUtc ?? RetrievedAtUtc.AddHours(-1), RetrievedAtUtc);
}
