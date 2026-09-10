using System.Text.Json;
using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class WeeklyDvarTorahReviewValidatorTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_AllSafetyAndQualityChecksPass_ReturnsNoErrors()
    {
        var codes = new List<string>();
        var errors = WeeklyDvarTorahReviewValidator.Validate(CreatePassingReview(), codes);

        Assert.IsEmpty(errors);
        Assert.IsEmpty(codes);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_RacismOrProtectedGroupTargeting_BlocksPublication()
    {
        var review = CreatePassingReview() with { DoesNotContainRacism = false, DoesNotTargetProtectedGroups = false, SafeToPublish = false, Concerns = [new() { Check = WeeklyDvarTorahReviewCheck.DoesNotTargetProtectedGroups, EvidenceIds = [], ParagraphIndex = 0 }] };

        var errors = WeeklyDvarTorahReviewValidator.Validate(review);

        Assert.IsTrue(errors.Any(error => error.Contains("racist", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(errors.Any(error => error.Contains("protected", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(errors.Any(error => error.Contains("minority group", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_ViolenceOrAlienation_BlocksPublication()
    {
        var review = CreatePassingReview() with { DoesNotEncourageViolence = false, DoesNotScapegoatOrAlienateGroups = false, SafeToPublish = false, Concerns = [new() { Check = WeeklyDvarTorahReviewCheck.DoesNotEncourageViolence, EvidenceIds = [], ParagraphIndex = 0 }] };

        var errors = WeeklyDvarTorahReviewValidator.Validate(review);

        Assert.IsTrue(errors.Any(error => error.Contains("violence", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(errors.Any(error => error.Contains("alienation", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    [DataRow("context", "story context")]
    [DataRow("argument", "beginning, middle, and end")]
    [DataRow("conclusion", "opening question")]
    [DataRow("hook", "modern opening hook")]
    [DataRow("quotations", "quotations disconnected")]
    [DataRow("bridge", "abrupt hook-to-Torah transition")]
    [DataRow("flow", "unnatural spoken flow")]
    [TestCategory("Regression")]
    public void Validate_EditorialCheckFails_BlocksOtherwiseSupportedArticle(string check, string expectedError)
    {
        var review = CreatePassingReview() with
        {
            StoryContextClear = check != "context",
            ArgumentHasBeginningMiddleEnd = check != "argument",
            ConclusionReturnsToOpening = check != "conclusion",
            OpeningHookGrounded = check != "hook",
            QuotationsIntegrated = check != "quotations",
            HookTorahBridgeNatural = check != "bridge",
            SpokenFlowNatural = check != "flow",
        };

        var errors = WeeklyDvarTorahReviewValidator.Validate(review);

        Assert.HasCount(1, errors);
        StringAssert.Contains(errors[0], expectedError);
    }

    [TestMethod]
    [DataRow("hookTorahBridgeNatural")]
    [DataRow("spokenFlowNatural")]
    [TestCategory("Regression")]
    public void Deserialize_MissingSpeechCheck_RejectsIncompleteReview(string property)
    {
        var serialized = JsonSerializer.SerializeToNode(CreatePassingReview());
        Assert.IsNotNull(serialized);
        Assert.IsTrue(serialized.AsObject().Remove(property));

        Assert.ThrowsExactly<JsonException>(() => serialized.Deserialize<WeeklyDvarTorahReviewDraft>());
    }

    [TestMethod]
    [DataRow(true, "HookTorahBridgeNatural", "paragraph 3")]
    [DataRow(false, "SpokenFlowNatural", "paragraph 3")]
    [TestCategory("Regression")]
    public void Validate_SpeechConcern_ReturnsTargetedRepairAndSafeLocation(bool bridgeFailed, string expectedCheck, string expectedLocation)
    {
        var review = CreatePassingReview() with
        {
            HookTorahBridgeNatural = !bridgeFailed,
            SpokenFlowNatural = bridgeFailed,
            Concerns = [new() { Check = bridgeFailed ? WeeklyDvarTorahReviewCheck.HookTorahBridgeNatural : WeeklyDvarTorahReviewCheck.SpokenFlowNatural, EvidenceIds = [], ParagraphIndex = 3 }],
        };
        var codes = new List<string>();

        var errors = WeeklyDvarTorahReviewValidator.Validate(review, codes);

        CollectionAssert.AreEqual(new[] { expectedCheck, "Concerns" }, codes);
        Assert.HasCount(2, errors);
        StringAssert.Contains(errors[1], expectedCheck);
        StringAssert.Contains(errors[1], expectedLocation);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void Validate_FailedChecks_FormatsOnlyCheckNamesLocationAndKnownIds()
    {
        var review = CreatePassingReview() with { AllClaimsSupported = false, StoryContextClear = false, Concerns = [new() { Check = WeeklyDvarTorahReviewCheck.AllClaimsSupported, EvidenceIds = ["TA"], ParagraphIndex = 2 }] };
        var codes = new List<string>();

        var errors = WeeklyDvarTorahReviewValidator.Validate(review, codes, ["TA"]);

        CollectionAssert.AreEqual(new[] { "AllClaimsSupported", "StoryContextClear", "Concerns" }, codes);
        Assert.HasCount(3, errors);
        StringAssert.Contains(errors[2], "AllClaimsSupported failed at paragraph 2; recheck against TA");
    }

    [TestMethod]
    [DataRow("null-list")]
    [DataRow("null-concern")]
    [DataRow("null-ids")]
    [DataRow("unknown-id")]
    [DataRow("unknown-check")]
    [DataRow("negative-paragraph")]
    [DataRow("excessive-paragraph")]
    [TestCategory("Regression")]
    public void Validate_InvalidConcernMetadata_ReportsFixedDiagnostic(string scenario)
    {
        var concern = new WeeklyDvarTorahReviewConcern
        {
            Check = scenario == "unknown-check" ? (WeeklyDvarTorahReviewCheck)999 : WeeklyDvarTorahReviewCheck.StoryContextClear,
            EvidenceIds = scenario == "null-ids" ? null! : scenario == "unknown-id" ? ["Secret source text"] : [],
            ParagraphIndex = scenario == "negative-paragraph" ? -1 : scenario == "excessive-paragraph" ? 1001 : 0,
        };
        var review = CreatePassingReview() with { Concerns = scenario == "null-list" ? null! : scenario == "null-concern" ? [null!] : [concern] };
        var codes = new List<string>();

        var errors = WeeklyDvarTorahReviewValidator.Validate(review, codes);

        CollectionAssert.AreEqual(new[] { "InvalidConcerns" }, codes);
        Assert.HasCount(1, errors);
    }

    private static WeeklyDvarTorahReviewDraft CreatePassingReview() => new()
    {
        AllClaimsSupported = true,
        TorahInterpretationResponsible = true,
        TorahRemainsCentral = true,
        CurrentEventsNeutral = true,
        NewsSourcesDescribeSameEvent = true,
        CurrentEventHasUsImpact = true,
        DeepMoralTeachingPresent = true,
        StoryContextClear = true,
        ArgumentHasBeginningMiddleEnd = true,
        ConclusionReturnsToOpening = true,
        OpeningHookGrounded = true,
        QuotationsIntegrated = true,
        HookTorahBridgeNatural = true,
        SpokenFlowNatural = true,
        DoesNotEncourageViolence = true,
        DoesNotGlorifyOrGraphicallyDescribeViolence = true,
        DoesNotContainHateOrDehumanization = true,
        DoesNotContainRacism = true,
        DoesNotContainSexism = true,
        DoesNotTargetProtectedGroups = true,
        DoesNotScapegoatOrAlienateGroups = true,
        DoesNotUsePartisanPersuasion = true,
        DoesNotExploitSuffering = true,
        DoesNotClaimDivinePunishment = true,
        RespectfulAndInclusive = true,
        SafeToPublish = true,
        Concerns = [],
    };
}
