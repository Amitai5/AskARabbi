using AskARabbiLIB.Grounding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class GroundedAnswerOptionsTests
{
    [TestMethod]
    [DataRow("maximumCandidates", 0)]
    [DataRow("maximumCandidates", 201)]
    [DataRow("maximumEvidenceSegments", 0)]
    [DataRow("maximumEvidenceSegments", 51)]
    [DataRow("maximumEvidenceCharacters", 999)]
    [DataRow("maximumEvidenceCharacters", 200_001)]
    [DataRow("maximumCharactersPerSegment", 199)]
    [DataRow("maximumCharactersPerSegment", 48_001)]
    [DataRow("maximumSegmentsPerDocument", 0)]
    [DataRow("maximumSegmentsPerDocument", 25)]
    [DataRow("contextRadius", -1)]
    [DataRow("contextRadius", 11)]
    [DataRow("recentConversationTurns", -1)]
    [DataRow("recentConversationTurns", 11)]
    [TestCategory("Unit")]
    public void Validate_OutOfRangeValue_ThrowsArgumentOutOfRangeException(string optionName, int value)
    {
        // Arrange
        var options = optionName switch
        {
            "maximumCandidates" => new GroundedAnswerOptions { MaximumCandidates = value },
            "maximumEvidenceSegments" => new GroundedAnswerOptions { MaximumEvidenceSegments = value },
            "maximumEvidenceCharacters" => new GroundedAnswerOptions { MaximumEvidenceCharacters = value },
            "maximumCharactersPerSegment" => new GroundedAnswerOptions { MaximumCharactersPerSegment = value },
            "maximumSegmentsPerDocument" => new GroundedAnswerOptions { MaximumSegmentsPerDocument = value },
            "contextRadius" => new GroundedAnswerOptions { ContextRadius = value },
            "recentConversationTurns" => new GroundedAnswerOptions { RecentConversationTurns = value },
            _ => throw new AssertFailedException($"Unknown option '{optionName}'."),
        };

        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(options.Validate);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_InclusiveMinimumBoundaries_Succeeds()
    {
        // Arrange
        var options = new GroundedAnswerOptions
        {
            MaximumCandidates = 1,
            MaximumEvidenceSegments = 1,
            MaximumEvidenceCharacters = 1_000,
            MaximumCharactersPerSegment = 200,
            MaximumSegmentsPerDocument = 1,
            ContextRadius = 0,
            RecentConversationTurns = 0,
        };

        // Act
        options.Validate();

        // Assert
        Assert.AreEqual(1, options.MaximumCandidates);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_InclusiveMaximumBoundaries_Succeeds()
    {
        // Arrange
        var options = new GroundedAnswerOptions
        {
            MaximumCandidates = 200,
            MaximumEvidenceSegments = 50,
            MaximumEvidenceCharacters = 200_000,
            MaximumCharactersPerSegment = 200_000,
            MaximumSegmentsPerDocument = 50,
            ContextRadius = 10,
            RecentConversationTurns = 10,
        };

        // Act
        options.Validate();

        // Assert
        Assert.AreEqual(200, options.MaximumCandidates);
    }
}
