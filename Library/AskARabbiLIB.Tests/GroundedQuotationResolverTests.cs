using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class GroundedQuotationResolverTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void TryResolve_ExactQuotation_ReturnsUnchangedText()
    {
        // Arrange
        var evidence = CreateEvidence("A lamp may not be kindled before nightfall.");

        // Act
        var wasResolved = GroundedQuotationResolver.TryResolve(evidence, "A lamp may not be kindled", out var resolved);

        // Assert
        Assert.IsTrue(wasResolved);
        Assert.AreEqual("A lamp may not be kindled", resolved);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void TryResolve_BlankQuotation_ReturnsFalse()
    {
        // Arrange
        var evidence = CreateEvidence("A lamp may not be kindled before nightfall.");

        // Act
        var wasResolved = GroundedQuotationResolver.TryResolve(evidence, "   ", out var resolved);

        // Assert
        Assert.IsFalse(wasResolved);
        Assert.AreEqual(string.Empty, resolved);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void TryResolve_EquivalentUnicodeAndWhitespace_ReturnsTrustedSourceSubstring()
    {
        // Arrange
        var evidence = CreateEvidence("The café says, “A lamp   may not be kindled”—before nightfall.");

        // Act
        var wasResolved = GroundedQuotationResolver.TryResolve(evidence, "The cafe says, \"A lamp may not be kindled\"-before nightfall.", out var resolved);

        // Assert
        Assert.IsTrue(wasResolved);
        Assert.AreEqual("The café says, “A lamp   may not be kindled”—before nightfall", resolved);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void TryResolve_ReplacementCharacterAndApostrophe_ReturnsTrustedSourceSubstring()
    {
        // Arrange
        var evidence = CreateEvidence("A kid in its mother�s milk is discussed here.");

        // Act
        var wasResolved = GroundedQuotationResolver.TryResolve(evidence, "a kid in its mother's milk", out var resolved);

        // Assert
        Assert.IsTrue(wasResolved);
        Assert.AreEqual("A kid in its mother�s milk", resolved);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void TryResolve_ChangedWording_ReturnsFalse()
    {
        // Arrange
        var evidence = CreateEvidence("A lamp may not be kindled before nightfall.");

        // Act
        var wasResolved = GroundedQuotationResolver.TryResolve(evidence, "A lamp must not be kindled before nightfall.", out var resolved);

        // Assert
        Assert.IsFalse(wasResolved);
        Assert.AreEqual(string.Empty, resolved);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void TryResolve_ShortNonExactQuotation_ReturnsFalse()
    {
        // Arrange
        var evidence = CreateEvidence("“Shabbat” is discussed here.");

        // Act
        var wasResolved = GroundedQuotationResolver.TryResolve(evidence, "\"Shabbat\"", out var resolved);

        // Assert
        Assert.IsFalse(wasResolved);
        Assert.AreEqual(string.Empty, resolved);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void TryResolve_ExcerptMarkerText_ReturnsFalse()
    {
        // Arrange
        var source = CreateSource("A lamp may not be kindled before nightfall.");
        var evidence = new EvidenceItem("E1", source, "[Explicit excerpt: characters 1-45 of 45]\n" + source.Text, true, source.Text.Length);

        // Act
        var wasResolved = GroundedQuotationResolver.TryResolve(evidence, "Explicit excerpt characters", out var resolved);

        // Assert
        Assert.IsFalse(wasResolved);
        Assert.AreEqual(string.Empty, resolved);
    }

    private static EvidenceItem CreateEvidence(string text)
    {
        var source = CreateSource(text);
        return new EvidenceItem("E1", source, text, false, text.Length);
    }

    private static SourceSegment CreateSource(string text) => new()
    {
        SegmentId = "sefaria:test:segment:00000001",
        DocumentId = "sefaria:test",
        CanonicalReference = "Test 1:1",
        DocumentOrdinal = 1,
        Text = text,
        Title = "Test source",
        HebrewTitle = "מקור",
        Language = "English",
        LanguageCode = "en",
        Collection = "Torah",
        Categories = ["Torah"],
        Version = "Test edition",
        License = "CC-BY",
        LicenseCategory = SourceLicenseCategory.CcBy,
        SourceUrl = "https://example.test/source",
        FilePath = "Data/NormalizedData/Sefaria/Test.md",
    };
}
