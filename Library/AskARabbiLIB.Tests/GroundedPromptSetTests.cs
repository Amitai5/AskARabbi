using AskARabbiLIB.Grounding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class GroundedPromptSetTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_CompletePromptSet_Succeeds()
    {
        // Arrange
        var prompts = CreateValidPromptSet();

        // Act
        prompts.Validate();

        // Assert
        Assert.IsNotNull(prompts);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_MissingContextPlaceholder_ThrowsArgumentException()
    {
        // Arrange
        var prompts = CreateValidPromptSet() with { PriorUserContextPrompt = "Prior user question without a placeholder." };

        // Act and assert
        var exception = Assert.ThrowsExactly<ArgumentException>(prompts.Validate);
        StringAssert.Contains(exception.Message, GroundedPromptSet.ContextPlaceholder);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_InvalidResponseSchema_ThrowsArgumentException()
    {
        // Arrange
        var prompts = CreateValidPromptSet() with { ResponseJsonSchema = "not-json" };

        // Act and assert
        var exception = Assert.ThrowsExactly<ArgumentException>(prompts.Validate);
        StringAssert.Contains(exception.Message, "invalid JSON");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_EmptyInterpretiveNotice_ThrowsArgumentException()
    {
        // Arrange
        var prompts = CreateValidPromptSet() with { InterpretiveNotice = string.Empty };

        // Act and assert
        var exception = Assert.ThrowsExactly<ArgumentException>(prompts.Validate);
        StringAssert.Contains(exception.Message, "empty");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_EmptySupportValidationPrompt_ThrowsArgumentException()
    {
        // Arrange
        var prompts = CreateValidPromptSet() with { SupportValidationPrompt = string.Empty };

        // Act and assert
        var exception = Assert.ThrowsExactly<ArgumentException>(prompts.Validate);
        Assert.AreEqual(nameof(GroundedPromptSet.SupportValidationPrompt), exception.ParamName);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_InvalidSupportValidationSchema_ThrowsArgumentException()
    {
        // Arrange
        var prompts = CreateValidPromptSet() with { SupportValidationJsonSchema = "not-json" };

        // Act and assert
        var exception = Assert.ThrowsExactly<ArgumentException>(prompts.Validate);
        StringAssert.Contains(exception.Message, "Support validation JSON schema is invalid JSON");
    }

    private static GroundedPromptSet CreateValidPromptSet() => new()
    {
        SystemBehaviorPrompt = "Use only supplied evidence.",
        PriorUserContextPrompt = $"Prior user question:\n{GroundedPromptSet.ContextPlaceholder}",
        PriorAssistantContextPrompt = $"Prior assistant answer:\n{GroundedPromptSet.ContextPlaceholder}",
        CurrentQuestionInstruction = "Answer the current question from evidence.",
        EvidenceStartMarker = "BEGIN_EVIDENCE",
        EvidenceEndMarker = "END_EVIDENCE",
        ValidationRepairPrompt = $"Repair this validation failure: {GroundedPromptSet.ValidationErrorPlaceholder}",
        InterpretiveNotice = "Keep the question open. This is one interpretation.",
        ResponseJsonSchema = "{\"type\":\"object\"}",
        SupportValidationPrompt = "Audit relevance and evidentiary support.",
        SupportValidationJsonSchema = "{\"type\":\"object\"}",
    };
}
