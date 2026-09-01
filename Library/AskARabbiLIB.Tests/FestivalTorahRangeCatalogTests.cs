using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class FestivalTorahRangeCatalogTests
{
    [TestMethod]
    [TestCategory("Unit")]
    [DataRow("2026-09-12", "Rosh Hashana", false, "Genesis 21:34")]
    [DataRow("2004-09-25", "Yom Kippur", false, "Leviticus 16:34")]
    [DataRow("2026-09-26", "Succos", false, "Leviticus 23:44")]
    [DataRow("2024-10-19", "Chol Hamoed Succos", false, "Exodus 34:26")]
    [DataRow("2026-10-03", "Shemini Atzeres", false, "Deuteronomy 16:17")]
    [DataRow("2026-10-03", "Shemini Atzeres", true, "Deuteronomy 34:12")]
    [DataRow("2012-04-07", "Pesach", false, "Exodus 12:51")]
    [DataRow("2027-04-24", "Chol Hamoed Pesach", false, "Exodus 33:12")]
    [DataRow("2001-04-14", "Pesach", false, "Exodus 15:26")]
    [DataRow("2012-04-14", "Pesach", false, "Deuteronomy 14:22")]
    [DataRow("2000-06-10", "Shavuos", false, "Deuteronomy 16:17")]
    public void Contains_KnownFestivalReading_AcceptsCanonicalPrimaryReading(string shabbatDate, string holiday, bool inIsrael, string reference)
    {
        var week = CreateWeek(shabbatDate, holiday, inIsrael);

        Assert.IsTrue(FestivalTorahRangeCatalog.IsSupported(week));
        Assert.IsTrue(FestivalTorahRangeCatalog.Contains(week, reference));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Contains_RoshHashana_RejectsUnrelatedWeeklyPortion()
    {
        var week = CreateWeek("2026-09-12", "Rosh Hashana", false);

        Assert.IsFalse(FestivalTorahRangeCatalog.Contains(week, "Deuteronomy 29:9"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void IsSupported_UnknownFestival_FailsClosed()
    {
        var week = CreateWeek("2026-09-12", "Unconfigured Festival", false);

        Assert.IsFalse(FestivalTorahRangeCatalog.IsSupported(week));
    }

    private static WeeklyDvarTorahWeek CreateWeek(string shabbatDate, string holiday, bool inIsrael) => new(DateOnly.ParseExact(shabbatDate, "yyyy-MM-dd"), "Test Hebrew date", null, holiday, inIsrael);
}
