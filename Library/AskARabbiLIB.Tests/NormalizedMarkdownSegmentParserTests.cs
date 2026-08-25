using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class NormalizedMarkdownSegmentParserTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Parse_ValidDocument_ReturnsStableOrderedSegments()
    {
        // Arrange
        var document = TestManifestFactory.CreateDocument(segmentCount: 2, firstReference: "Genesis 1:1", lastReference: "Genesis 1:2");
        var markdown = """
            ---
            document_id: "test"
            ---

            # Genesis

            ## Genesis 1:1

            In the beginning.

            ## Genesis 1:2

            The earth was unformed.
            """;
        var parser = new NormalizedMarkdownSegmentParser();

        // Act
        var segments = parser.Parse(document, markdown);

        // Assert
        Assert.HasCount(2, segments);
        Assert.AreEqual($"{document.DocumentId}:segment:00000001", segments[0].SegmentId);
        Assert.AreEqual("Genesis 1:1", segments[0].CanonicalReference);
        Assert.AreEqual(0, segments[0].DocumentOrdinal);
        Assert.AreEqual("In the beginning.", segments[0].Text);
        Assert.AreEqual("Genesis 1:2", segments[1].CanonicalReference);
        Assert.AreEqual(document.Categories[0], segments[1].Categories[0]);
    }

    [TestMethod]
    [DataRow("---\nmissing: close", "front matter")]
    [DataRow("# Genesis\nUnexpected content\n## Genesis 1:1\nText", "before its first segment")]
    [DataRow("# Genesis\n## Genesis 1:1\n", "has no text")]
    [TestCategory("Unit")]
    public void Parse_MalformedMarkdown_ThrowsInvalidDataException(string markdown, string expectedMessage)
    {
        // Arrange
        var document = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");
        var parser = new NormalizedMarkdownSegmentParser();

        // Act + Assert
        var exception = Assert.ThrowsExactly<InvalidDataException>(() => parser.Parse(document, markdown));
        StringAssert.Contains(exception.Message, expectedMessage);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Parse_SegmentCountMismatch_ThrowsInvalidDataException()
    {
        // Arrange
        var document = TestManifestFactory.CreateDocument(segmentCount: 2, firstReference: "Genesis 1:1", lastReference: "Genesis 1:2");
        var parser = new NormalizedMarkdownSegmentParser();

        // Act + Assert
        var exception = Assert.ThrowsExactly<InvalidDataException>(() => parser.Parse(document, "# Genesis\n\n## Genesis 1:1\n\nText"));
        StringAssert.Contains(exception.Message, "segment count mismatch");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Parse_ReferenceRangeMismatch_ThrowsInvalidDataException()
    {
        // Arrange
        var document = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:2", lastReference: "Genesis 1:2");
        var parser = new NormalizedMarkdownSegmentParser();

        // Act + Assert
        var exception = Assert.ThrowsExactly<InvalidDataException>(() => parser.Parse(document, "# Genesis\n\n## Genesis 1:1\n\nText"));
        StringAssert.Contains(exception.Message, "reference range mismatch");
    }
}
