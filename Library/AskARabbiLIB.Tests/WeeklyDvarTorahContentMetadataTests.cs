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

    private static WeeklyDvarTorahSource CreateTorahSource() => new("T1", WeeklyDvarTorahSourceKind.Torah, "Deuteronomy", "Sefaria edition", "https://www.sefaria.org/Deuteronomy.29.9", "You stand this day, all of you.", RetrievedAtUtc, "Deuteronomy 29:9", license: "CC-BY");
}
