using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class ParashahTorahRangeCatalogTests
{
    [TestMethod]
    [DataRow("Achrei Mos Kedoshim", "Leviticus 20:27")]
    [DataRow("Behar Bechukosai", "Leviticus 27:34")]
    [DataRow("Chukas Balak", "Numbers 25:9")]
    [DataRow("Matos Masei", "Numbers 36:13")]
    [DataRow("Nitzavim Vayeilech", "Deuteronomy 31:30")]
    [DataRow("Tazria Metzora", "Leviticus 15:33")]
    [DataRow("Vayakhel Pekudei", "Exodus 40:38")]
    [TestCategory("Unit")]
    public void Contains_CalendarCombinedParashah_AcceptsSecondPortion(string parashah, string reference)
    {
        Assert.IsTrue(ParashahTorahRangeCatalog.Contains(parashah, reference));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Contains_HyphenatedCombinedParashah_AcceptsEitherPortionAndRejectsOtherBooks()
    {
        Assert.IsTrue(ParashahTorahRangeCatalog.Contains("Nitzavim-Vayeilech", "Deuteronomy 30:2"));
        Assert.IsTrue(ParashahTorahRangeCatalog.Contains("Nitzavim-Vayeilech", "Deuteronomy 31:3"));
        Assert.IsFalse(ParashahTorahRangeCatalog.Contains("Nitzavim-Vayeilech", "Genesis 30:2"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Contains_AlternateTransliteration_UsesSameCanonicalRange()
    {
        Assert.IsTrue(ParashahTorahRangeCatalog.Contains("Bechukosai", "Leviticus 26:5"));
        Assert.IsFalse(ParashahTorahRangeCatalog.Contains("Bechukosai", "Leviticus 25:5"));
    }
}
