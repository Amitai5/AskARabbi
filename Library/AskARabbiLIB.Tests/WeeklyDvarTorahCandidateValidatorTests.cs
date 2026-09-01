using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class WeeklyDvarTorahCandidateValidatorTests
{
    private static readonly DateTimeOffset CurrentUtc = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_EightyPercentTorahAndCorroboratedNews_Passes()
    {
        var evidence = CreateEvidence();
        var draft = CreateDraft(evidence);

        var result = WeeklyDvarTorahCandidateValidator.Validate(draft, evidence, CreateOptions());

        Assert.IsTrue(result.IsValid, string.Join(" ", result.Errors));
        Assert.AreEqual(80, result.TorahGroundingPercent);
        Assert.HasCount(10, result.UsedEvidenceIds);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_TooManyNewsClaims_FailsTorahGrounding()
    {
        var evidence = CreateEvidence();
        var draft = CreateDraft(evidence);
        draft = draft with
        {
            CurrentEventFacts =
            [
                .. draft.CurrentEventFacts,
                CreateStatement("A second bounded news proposition.", evidence[8], evidence[9]),
            ],
        };

        var result = WeeklyDvarTorahCandidateValidator.Validate(draft, evidence, CreateOptions());

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("Torah grounding", StringComparison.Ordinal)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_QuotationDoesNotMatchEvidence_Fails()
    {
        var evidence = CreateEvidence();
        var draft = CreateDraft(evidence);
        var first = draft.TorahTeachings[0] with
        {
            Quotations = [new WeeklyDvarTorahQuotationDraft { EvidenceId = "T1", Text = "Invented quotation." }, new WeeklyDvarTorahQuotationDraft { EvidenceId = "T2", Text = evidence[1].PresentedText }],
        };
        draft = draft with { TorahTeachings = [first, .. draft.TorahTeachings.Skip(1)] };

        var result = WeeklyDvarTorahCandidateValidator.Validate(draft, evidence, CreateOptions());

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("does not exactly match", StringComparison.Ordinal)));
    }

    private static WeeklyDvarTorahArticleDraft CreateDraft(IReadOnlyList<WeeklyDvarTorahEvidence> evidence)
    {
        var citations = string.Join(' ', evidence.Select(item => $"[{item.EvidenceId}]"));
        return new WeeklyDvarTorahArticleDraft
        {
            Title = "Standing Together With Responsibility",
            Body = $"{citations}\n\n{new string('a', 1_200)}",
            CentralTeaching = "Covenantal responsibility asks us to see one another and turn shared awareness into patient, concrete good.",
            Tags = ["responsibility", "community", "nitzavim", "technology", "current events"],
            PracticalActions = ["Listen carefully to one person.", "Perform one private act of kindness.", "Set aside time for Torah study."],
            TorahTeachings =
            [
                CreateStatement("Torah teaching one.", evidence[0], evidence[1]),
                CreateStatement("Torah teaching two.", evidence[2], evidence[3]),
                CreateStatement("Torah teaching three.", evidence[4], evidence[5]),
                CreateStatement("Torah teaching four.", evidence[6], evidence[7]),
            ],
            CurrentEventFacts = [CreateStatement("A narrowly bounded current-event fact.", evidence[8], evidence[9])],
            Connections = [CreateStatement("The Torah teaching illuminates the present responsibility.", evidence[0], evidence[8])],
        };
    }

    private static WeeklyDvarTorahSourcedStatementDraft CreateStatement(string text, params WeeklyDvarTorahEvidence[] evidence) => new()
    {
        Text = text,
        EvidenceIds = evidence.Select(item => item.EvidenceId).ToArray(),
        Quotations = evidence.Select(item => new WeeklyDvarTorahQuotationDraft { EvidenceId = item.EvidenceId, Text = item.PresentedText }).ToArray(),
    };

    private static IReadOnlyList<WeeklyDvarTorahEvidence> CreateEvidence()
    {
        var evidence = Enumerable.Range(1, 8).Select(index => new WeeklyDvarTorahEvidence(
            $"T{index}",
            WeeklyDvarTorahSourceKind.Torah,
            "Deuteronomy",
            "Test Torah edition",
            $"https://www.sefaria.org/Deuteronomy.29.{index + 8}",
            $"Torah evidence passage number {index} teaches responsibility.",
            CurrentUtc,
            $"Deuteronomy 29:{index + 8}",
            null,
            "CC-BY")).ToList();
        evidence.Add(new WeeklyDvarTorahEvidence("N1", WeeklyDvarTorahSourceKind.News, "Public development", "Publisher One", "https://one.example.test/story", "Publisher one reports the public development.", CurrentUtc, null, CurrentUtc.AddHours(-3), "RSS metadata"));
        evidence.Add(new WeeklyDvarTorahEvidence("N2", WeeklyDvarTorahSourceKind.News, "Public development confirmed", "Publisher Two", "https://two.example.test/story", "Publisher two independently confirms the development.", CurrentUtc, null, CurrentUtc.AddHours(-2), "RSS metadata"));
        return evidence;
    }

    private static WeeklyDvarTorahContentOptions CreateOptions() => new()
    {
        MinimumBodyCharacters = 1_000,
        MaximumBodyCharacters = 5_000,
        MinimumTorahEvidenceItems = 8,
        MaximumTorahEvidenceItems = 14,
    };
}
