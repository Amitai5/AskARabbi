using AskARabbiLIB.Models;
using AskARabbiLIB.Grounding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class SourceLicensePolicyTests
{
    [TestMethod]
    [DataRow("PD", SourceLicenseCategory.PublicDomain, false, false)]
    [DataRow("Public Domain", SourceLicenseCategory.PublicDomain, false, false)]
    [DataRow("CC0 1.0", SourceLicenseCategory.Cc0, false, false)]
    [DataRow("CC-BY", SourceLicenseCategory.CcBy, true, false)]
    [DataRow("CC BY 4.0", SourceLicenseCategory.CcBy, true, false)]
    [DataRow("CC-BY-SA", SourceLicenseCategory.CcBySa, true, true)]
    [DataRow("CC BY-SA 4.0", SourceLicenseCategory.CcBySa, true, true)]
    [DataRow(" public domain ", SourceLicenseCategory.PublicDomain, false, false)]
    [DataRow("cc0", SourceLicenseCategory.Cc0, false, false)]
    [DataRow("CC0-1", SourceLicenseCategory.Cc0, false, false)]
    [DataRow("cc0 1.0.2", SourceLicenseCategory.Cc0, false, false)]
    [DataRow("cc-by 1", SourceLicenseCategory.CcBy, true, false)]
    [DataRow("cc by-4.0", SourceLicenseCategory.CcBy, true, false)]
    [DataRow("cc-by-sa 4.0.1", SourceLicenseCategory.CcBySa, true, true)]
    [DataRow("cc by sa-3", SourceLicenseCategory.CcBySa, true, true)]
    [TestCategory("Unit")]
    public void Classify_SupportedLicense_ReturnsTerms(string license, SourceLicenseCategory expectedCategory, bool expectedAttribution, bool expectedShareAlike)
    {
        // Act
        var category = SourceLicensePolicy.Classify(license);

        // Assert
        Assert.AreEqual(expectedCategory, category);
        Assert.AreEqual(expectedAttribution, SourceLicensePolicy.RequiresAttribution(category));
        Assert.AreEqual(expectedShareAlike, SourceLicensePolicy.RequiresShareAlike(category));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Classify_UnsupportedLicense_ThrowsArgumentException()
    {
        // Act + Assert
        Assert.ThrowsExactly<ArgumentException>(() => SourceLicensePolicy.Classify("CC-BY-NC"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [TestCategory("Unit")]
    public void Classify_EmptyLicense_ThrowsArgumentException(string? license)
    {
        // Act and assert
        if (license is null)
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => SourceLicensePolicy.Classify(license!));
        }
        else
        {
            Assert.ThrowsExactly<ArgumentException>(() => SourceLicensePolicy.Classify(license));
        }
    }

    [TestMethod]
    [DataRow("pdx")]
    [DataRow("public  domain")]
    [DataRow("public-domain")]
    [DataRow("cc")]
    [DataRow("cc0.")]
    [DataRow("cc0_1.0")]
    [DataRow("cc0 1.")]
    [DataRow("cc-by-")]
    [DataRow("cc-by 4.")]
    [DataRow("cc-by_4.0")]
    [DataRow("cc-by-sa-")]
    [DataRow("cc by sa 4.")]
    [DataRow("cc-by-sa_4.0")]
    [DataRow("xcc-by")]
    [DataRow("cc-byx")]
    [DataRow("MIT")]
    [TestCategory("Unit")]
    public void Classify_NearMatchUnsupportedLicense_ThrowsArgumentException(string license)
    {
        // Act and assert
        Assert.ThrowsExactly<ArgumentException>(() => SourceLicensePolicy.Classify(license));
    }

    [TestMethod]
    [DataRow(SourceLicenseCategory.PublicDomain, "Public Domain")]
    [DataRow(SourceLicenseCategory.Cc0, "CC0")]
    [DataRow(SourceLicenseCategory.CcBy, "CC BY")]
    [DataRow(SourceLicenseCategory.CcBySa, "CC BY-SA")]
    [TestCategory("Unit")]
    public void GetDisplayName_SupportedCategory_ReturnsExpectedName(SourceLicenseCategory category, string expected)
    {
        // Act
        var result = SourceLicensePolicy.GetDisplayName(category);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetDisplayName_UnknownCategory_ThrowsArgumentOutOfRangeException()
    {
        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SourceLicensePolicy.GetDisplayName((SourceLicenseCategory)999));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void MarkdownSourceLink_AttributionCitation_LinksTrustedOriginalSource()
    {
        // Arrange
        var citation = new SourceCitation(1, "E1", "segment-1", "Shabbat", "שבת", "Shabbat 20a:1", "English Test", "English", "en", "Talmud", new[] { "Talmud" }, "CC-BY", SourceLicenseCategory.CcBy, "https://example.test/original", "Data/Test.md", false);

        // Act
        var result = citation.MarkdownSourceLink;

        // Assert
        Assert.AreEqual("[Shabbat 20a:1 — English Test](<https://example.test/original>)", result);
        Assert.IsTrue(citation.RequiresAttribution);
        Assert.IsFalse(citation.RequiresShareAlike);
    }
}
