using System.Text.Json;
using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.Profiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class AIToolRegistryTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_CalendarProvider_ExposesThreeStrictDefinitionsWithoutPrivateContext()
    {
        // Arrange and act
        var registry = CreateRegistry();

        // Assert
        Assert.HasCount(3, registry.Definitions);
        var conversion = registry.Definitions.Single(value => value.Name == "convert_birthdate_to_hebrew");
        StringAssert.Contains(conversion.ParametersJsonSchema.ToString(), "birthDateTime");
        Assert.IsFalse(conversion.ParametersJsonSchema.ToString().Contains("context", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(conversion.ParametersJsonSchema.ToString(), "additionalProperties");
    }

    [TestMethod]
    [DataRow("What was my bar mitzvah parashah?")]
    [DataRow("What is today's Hebrew date?")]
    [DataRow("Convert my birthday to the Jewish birth date")]
    [TestCategory("Unit")]
    public void MayApply_CalendarQuestion_ReturnsTrue(string question)
    {
        // Act
        var result = CreateRegistry().MayApply(question);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ExecuteAsync_ProfileBirthdateOmitted_UsesPrivateProfileWithoutReturningGregorianBirthdate()
    {
        // Arrange
        var registry = CreateRegistry();
        var context = CreateContext();

        // Act
        var result = await registry.ExecuteAsync("convert_birthdate_to_hebrew", BinaryData.FromString("{}"), context);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Evidence);
        StringAssert.Contains(result.Evidence.ExactText, "2 Tevet, 5762");
        Assert.IsFalse(result.Evidence.ExactText.Contains("2001", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("Mizrahi", "2 Tevet, 5762")]
    [DataRow("Sephardi", "2 Tevet, 5762")]
    [DataRow("Ashkenazi", "2 Teves, 5762")]
    [TestCategory("Regression")]
    public async Task ExecuteAsync_ProfileHeritage_UsesCommunityAwareHebrewMonthTransliteration(string jewishHeritage, string expectedDate)
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = await registry.ExecuteAsync("convert_birthdate_to_hebrew", BinaryData.FromString("{}"), CreateContext(jewishHeritage));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Evidence);
        StringAssert.Contains(result.Evidence.ExactText, expectedDate);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ExecuteAsync_UnknownArgument_ReturnsBoundedFailure()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = await registry.ExecuteAsync("get_today_as_hebrew_and_gregorian", BinaryData.FromString("{\"unexpected\":true}"), CreateContext());

        // Assert
        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.ErrorMessage, "Unknown parameter");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ExecutionSession_BarMitzvahTool_AddsCitableEvidenceAndEnforcesLimit()
    {
        // Arrange
        var session = new AIToolExecutionSession(CreateRegistry(), CreateContext(), 2, 1);

        // Act
        var first = await session.ExecuteAsync("find_parashah_for_week", BinaryData.FromString("{\"hebrewAnniversaryAge\":13}"));
        var second = await session.ExecuteAsync("find_parashah_for_week", BinaryData.FromString("{\"hebrewAnniversaryAge\":13}"));

        // Assert
        Assert.HasCount(1, session.EvidenceItems);
        Assert.AreEqual("E3", session.EvidenceItems[0].EvidenceId);
        StringAssert.Contains(session.EvidenceItems[0].PresentedText, "Vayigash");
        Assert.IsFalse(session.EvidenceItems[0].Source.Title.Contains("tool", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(session.EvidenceItems[0].Source.Version.Contains("tool", StringComparison.OrdinalIgnoreCase));
        using var firstJson = JsonDocument.Parse(first);
        Assert.IsTrue(firstJson.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.AreEqual("E3", firstJson.RootElement.GetProperty("evidence").GetProperty("evidenceId").GetString());
        using var secondJson = JsonDocument.Parse(second);
        Assert.IsFalse(secondJson.RootElement.GetProperty("isSuccess").GetBoolean());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task CalendarTools_ExplicitDatesCurrentDateAndFailurePaths_ReturnBoundedResults()
    {
        // Arrange
        var registry = CreateRegistry();
        var noProfile = new AIToolExecutionContext(null, new DateTimeOffset(2026, 8, 31, 18, 0, 0, TimeSpan.Zero));

        // Act
        var explicitBirth = await registry.ExecuteAsync("convert_birthdate_to_hebrew", BinaryData.FromString("{\"birthDateTime\":\"2001-12-17T20:00:00\",\"occurredAfterSunset\":true}"), noProfile);
        var currentDate = await registry.ExecuteAsync("get_today_as_hebrew_and_gregorian", BinaryData.FromString("{\"occurredAfterSunset\":true}"), noProfile);
        var explicitWeek = await registry.ExecuteAsync("find_parashah_for_week", BinaryData.FromString("{\"dateTime\":\"2022-05-28T00:00:00\",\"inIsrael\":true}"), noProfile);
        var missingBirth = await registry.ExecuteAsync("convert_birthdate_to_hebrew", BinaryData.FromString("{}"), noProfile);
        var missingAnniversary = await registry.ExecuteAsync("find_parashah_for_week", BinaryData.FromString("{\"hebrewAnniversaryAge\":13}"), noProfile);
        var conflicting = await registry.ExecuteAsync("find_parashah_for_week", BinaryData.FromString("{\"dateTime\":\"2022-05-28T00:00:00\",\"hebrewAnniversaryAge\":13}"), CreateContext());

        // Assert
        Assert.IsTrue(explicitBirth.IsSuccess);
        StringAssert.Contains(explicitBirth.Evidence?.ExactText, "after local sunset");
        Assert.IsTrue(currentDate.IsSuccess);
        StringAssert.Contains(currentDate.Evidence?.ExactText, "UTC");
        Assert.IsTrue(explicitWeek.IsSuccess);
        StringAssert.Contains(explicitWeek.Evidence?.ExactText, "Israel reading cycle");
        Assert.IsFalse(missingBirth.IsSuccess);
        Assert.IsFalse(missingAnniversary.IsSuccess);
        Assert.IsFalse(conflicting.IsSuccess);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ExecuteAsync_InvalidToolNameJsonArgumentsAndCancellation_ReturnBoundedBehavior()
    {
        // Arrange
        var registry = CreateRegistry();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        var unknown = await registry.ExecuteAsync("missing", BinaryData.FromString("{}"), CreateContext());
        var nonObject = await registry.ExecuteAsync("convert_birthdate_to_hebrew", BinaryData.FromString("[]"), CreateContext());
        var malformed = await registry.ExecuteAsync("convert_birthdate_to_hebrew", BinaryData.FromString("{broken"), CreateContext());
        var invalidDate = await registry.ExecuteAsync("convert_birthdate_to_hebrew", BinaryData.FromString("{\"birthDateTime\":\"not-a-date\"}"), CreateContext());

        // Assert
        Assert.IsFalse(unknown.IsSuccess);
        Assert.IsFalse(nonObject.IsSuccess);
        Assert.IsFalse(malformed.IsSuccess);
        Assert.IsFalse(invalidDate.IsSuccess);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => registry.ExecuteAsync("convert_birthdate_to_hebrew", BinaryData.FromString("{}"), CreateContext(), cancellation.Token));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ExecuteAsync_RequiredStringAndAsyncTool_BindsValuesAndSupportsTaskResult()
    {
        // Arrange
        var registry = new AIToolRegistry([new TestToolProvider()]);

        // Act
        var success = await registry.ExecuteAsync("echo_value", BinaryData.FromString("{\"value\":\"hello\"}"), CreateContext());
        var missing = await registry.ExecuteAsync("echo_value", BinaryData.FromString("{}"), CreateContext());

        // Assert
        Assert.IsTrue(success.IsSuccess);
        Assert.AreEqual("hello", success.Data);
        Assert.IsFalse(missing.IsSuccess);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task ExecuteAsync_ProviderThrowsUnexpectedException_ReturnsSafeFailure()
    {
        // Arrange
        var registry = new AIToolRegistry([new ThrowingToolProvider()]);

        // Act
        var result = await registry.ExecuteAsync("throw_unexpected", BinaryData.FromString("{}"), CreateContext());

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.AreEqual("Tool 'throw_unexpected' could not complete its local calculation.", result.ErrorMessage);
        Assert.IsFalse(result.ErrorMessage.Contains("private diagnostic", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ConstructorsAndRegistration_InvalidInputs_ThrowPreciseExceptions()
    {
        // Act and assert
        Assert.ThrowsExactly<ArgumentNullException>(() => new CalendarAITools(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AIToolRegistry(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AIToolRegistry([null!]));
        Assert.ThrowsExactly<InvalidOperationException>(() => new AIToolRegistry([new DuplicateOneProvider(), new DuplicateTwoProvider()]));
        Assert.ThrowsExactly<InvalidOperationException>(() => new AIToolRegistry([new InvalidNameProvider()]));
        Assert.ThrowsExactly<InvalidOperationException>(() => new AIToolRegistry([new InvalidReturnProvider()]));
        Assert.ThrowsExactly<InvalidOperationException>(() => new AIToolRegistry([new InvalidParameterProvider()]));
        Assert.ThrowsExactly<InvalidOperationException>(() => new AIToolRegistry([new MissingDescriptionProvider()]));
        Assert.ThrowsExactly<ArgumentException>(() => new AIToolAttribute(" ", "description"));
        Assert.ThrowsExactly<ArgumentException>(() => new AIToolAttribute("name", " "));
        Assert.ThrowsExactly<ArgumentException>(() => new AIToolParameterAttribute(" "));
        Assert.ThrowsExactly<ArgumentNullException>(() => AIToolExecutionResult.Success(null!, new AIToolEvidence("reference", "text")));
        Assert.ThrowsExactly<ArgumentNullException>(() => AIToolExecutionResult.Success(new { }, null!));
        Assert.ThrowsExactly<ArgumentException>(() => AIToolExecutionResult.Failure(" "));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ExecutionSession_InvalidLimits_ThrowArgumentOutOfRangeException()
    {
        // Arrange
        var registry = CreateRegistry();
        var context = CreateContext();

        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AIToolExecutionSession(registry, context, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AIToolExecutionSession(registry, context, 0, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AIToolExecutionSession(registry, context, 0, 9));
    }

    private static AIToolRegistry CreateRegistry() => new([new CalendarAITools(new HebrewCalendarService())]);

    private static AIToolExecutionContext CreateContext(string jewishHeritage = "Mizrahi") => new(
        new UserProfile
        {
            Name = "Test User",
            DateOfBirth = new DateOnly(2001, 12, 17),
            TimeOfBirth = new TimeOnly(9, 30),
            BirthTimeZone = "America/Los_Angeles",
            JewishHeritage = jewishHeritage,
        },
        new DateTimeOffset(2026, 8, 31, 18, 0, 0, TimeSpan.Zero));

    private sealed class TestToolProvider
    {
        [AITool("echo_value", "Echo a required string.")]
        public Task<AIToolExecutionResult> EchoAsync([AIToolParameter("Value to echo.")] string value)
        {
            return Task.FromResult(AIToolExecutionResult.Success(value, new AIToolEvidence("Echo", value)));
        }
    }

    private sealed class DuplicateOneProvider
    {
        [AITool("duplicate", "First duplicate.")]
        public AIToolExecutionResult Run() => AIToolExecutionResult.Failure("unused");
    }

    private sealed class ThrowingToolProvider
    {
        [AITool("throw_unexpected", "Throw an unexpected test exception.")]
        public AIToolExecutionResult Run()
        {
            throw new Exception("private diagnostic");
        }
    }

    private sealed class DuplicateTwoProvider
    {
        [AITool("duplicate", "Second duplicate.")]
        public AIToolExecutionResult Run() => AIToolExecutionResult.Failure("unused");
    }

    private sealed class InvalidNameProvider
    {
        [AITool("invalid name", "Invalid name.")]
        public AIToolExecutionResult Run() => AIToolExecutionResult.Failure("unused");
    }

    private sealed class InvalidReturnProvider
    {
        [AITool("invalid_return", "Invalid return.")]
        public string Run() => "invalid";
    }

    private sealed class InvalidParameterProvider
    {
        [AITool("invalid_parameter", "Invalid parameter.")]
        public AIToolExecutionResult Run([AIToolParameter("Unsupported value.")] decimal value) => AIToolExecutionResult.Failure(value.ToString());
    }

    private sealed class MissingDescriptionProvider
    {
        [AITool("missing_description", "Missing parameter description.")]
        public AIToolExecutionResult Run(string value) => AIToolExecutionResult.Failure(value);
    }
}
