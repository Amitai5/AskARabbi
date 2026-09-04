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
    [TestCategory("Regression")]
    public void Validate_EditorialCheckFails_BlocksOtherwiseSupportedArticle(string check, string expectedError)
    {
        var review = CreatePassingReview() with
        {
            StoryContextClear = check != "context",
            ArgumentHasBeginningMiddleEnd = check != "argument",
            ConclusionReturnsToOpening = check != "conclusion",
        };

        var errors = WeeklyDvarTorahReviewValidator.Validate(review);

        Assert.HasCount(1, errors);
        StringAssert.Contains(errors[0], expectedError);
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
