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
        var errors = WeeklyDvarTorahReviewValidator.Validate(CreatePassingReview());

        Assert.IsEmpty(errors);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_RacismOrProtectedGroupTargeting_BlocksPublication()
    {
        var review = CreatePassingReview() with { DoesNotContainRacism = false, DoesNotTargetProtectedGroups = false, SafeToPublish = false, Concerns = ["A minority group is unfairly singled out."] };

        var errors = WeeklyDvarTorahReviewValidator.Validate(review);

        Assert.IsTrue(errors.Any(error => error.Contains("racist", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(errors.Any(error => error.Contains("protected", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(errors.Any(error => error.Contains("minority group", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_ViolenceOrAlienation_BlocksPublication()
    {
        var review = CreatePassingReview() with { DoesNotEncourageViolence = false, DoesNotScapegoatOrAlienateGroups = false, SafeToPublish = false, Concerns = ["The draft endorses harm and alienates a community."] };

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
