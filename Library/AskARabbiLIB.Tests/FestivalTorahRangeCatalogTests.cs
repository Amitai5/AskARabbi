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

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow("2026-09-26", "Sukkot", false, "Leviticus 23:44")]
    [DataRow("1990-10-06", "Chol Hamoed Sukkot", false, "Exodus 34:26")]
    [DataRow("2026-10-03", "Shemini Atzeret", false, "Deuteronomy 16:17")]
    [DataRow("2026-10-03", "Shemini Atzeret", true, "Genesis 2:3")]
    [DataRow("2026-10-03", "Simchas Torah", false, "Deuteronomy 33:1")]
    [DataRow("2026-10-03", "Simchat Torah", false, "Genesis 1:1")]
    [DataRow("1996-04-06", "Chol Hamoed Pesach", true, "Exodus 33:12")]
    [DataRow("2001-04-14", "Pesach", true, "Exodus 13:17")]
    [DataRow("2000-06-10", "Shavuot", false, "Deuteronomy 16:17")]
    public void Contains_SupportedFestivalAliasesAndDiasporaVariants_AcceptsCanonicalReading(string date, string holiday, bool inIsrael, string reference)
    {
        var week = CreateWeek(date, holiday, inIsrael);

        Assert.IsTrue(FestivalTorahRangeCatalog.IsSupported(week));
        Assert.IsTrue(FestivalTorahRangeCatalog.Contains(week, reference));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow("2012-04-14", "Pesach", true)]
    [DataRow("2000-06-10", "Shavuot", true)]
    [DataRow("2026-09-19", "Rosh Hashana", false)]
    public void IsSupported_UnsupportedLocationOrFestivalDay_FailsClosed(string date, string holiday, bool inIsrael)
    {
        Assert.IsFalse(FestivalTorahRangeCatalog.IsSupported(CreateWeek(date, holiday, inIsrael)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void IsSupported_NoFestival_FailsClosed()
    {
        var week = new WeeklyDvarTorahWeek(new DateOnly(2026, 9, 5), "Test Hebrew date", "Nitzavim", null, false);

        Assert.IsFalse(FestivalTorahRangeCatalog.IsSupported(week));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow("Exodus 21:1")]
    [DataRow("Genesis 21")]
    [DataRow("Genesis :1")]
    [DataRow("Genesis x:1")]
    [DataRow("Genesis 21:x")]
    [DataRow("Genesis 20:34")]
    [DataRow("Genesis 22:1")]
    [DataRow("Genesis 21:0")]
    [DataRow("Genesis 21:35")]
    public void Contains_MalformedOrOutOfRangeReference_Rejects(string reference)
    {
        Assert.IsFalse(FestivalTorahRangeCatalog.Contains(CreateWeek("2026-09-12", "Rosh Hashana", false), reference));
    }

    private static WeeklyDvarTorahWeek CreateWeek(string shabbatDate, string holiday, bool inIsrael) => new(DateOnly.ParseExact(shabbatDate, "yyyy-MM-dd"), "Test Hebrew date", null, holiday, inIsrael);
}
