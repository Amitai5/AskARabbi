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

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_QuotationExceedsProofPhraseBounds_Fails()
    {
        var evidence = CreateEvidence().ToArray();
        var longQuotation = string.Join(' ', Enumerable.Repeat("word", 13));
        evidence[0] = evidence[0] with { PresentedText = longQuotation };
        var draft = CreateDraft(evidence);

        var result = WeeklyDvarTorahCandidateValidator.Validate(draft, evidence, CreateOptions());

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("at most 12 words and 120 characters", StringComparison.Ordinal)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_InvalidTopLevelDraftFields_FailClosed()
    {
        var evidence = CreateEvidence();
        var valid = CreateDraft(evidence);
        WeeklyDvarTorahArticleDraft[] invalidDrafts =
        [
            valid with { Title = " " },
            valid with { Title = new string('t', WeeklyDvarTorahDraft.MaximumTitleCharacters + 1) },
            valid with { Body = " " },
            valid with { Body = new string('b', 5_001) },
            valid with { CentralTeaching = "short" },
            valid with { CentralTeaching = new string('c', 1_201) },
            valid with { Tags = null! },
            valid with { Tags = ["one", "two", "three", "four"] },
            valid with { Tags = Enumerable.Range(1, 13).Select(index => $"tag-{index}").ToArray() },
            valid with { Tags = ["one", "two", "three", "four", " "] },
            valid with { Tags = ["same", "SAME", "three", "four", "five"] },
            valid with { Tags = [new string('t', 61), "two", "three", "four", "five"] },
            valid with { PracticalActions = null! },
            valid with { PracticalActions = ["one", "two"] },
            valid with { PracticalActions = ["one", " ", "three"] },
            valid with { PracticalActions = ["one", new string('a', 501), "three"] },
        ];

        foreach (var draft in invalidDrafts)
        {
            Assert.IsFalse(WeeklyDvarTorahCandidateValidator.Validate(draft, evidence, CreateOptions()).IsValid);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_InvalidStatementCounts_FailClosed()
    {
        var evidence = CreateEvidence();
        var valid = CreateDraft(evidence);
        WeeklyDvarTorahArticleDraft[] invalidDrafts =
        [
            valid with { TorahTeachings = [], CurrentEventFacts = [], Connections = [] },
            valid with { TorahTeachings = Enumerable.Repeat(valid.TorahTeachings[0], 13).ToArray() },
            valid with { CurrentEventFacts = Enumerable.Repeat(valid.CurrentEventFacts[0], 4).ToArray() },
            valid with { Connections = Enumerable.Repeat(valid.Connections[0], 4).ToArray() },
        ];

        foreach (var draft in invalidDrafts)
        {
            Assert.IsFalse(WeeklyDvarTorahCandidateValidator.Validate(draft, evidence, CreateOptions()).IsValid);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_MalformedEvidenceReferences_FailClosed()
    {
        var evidence = CreateEvidence();
        var valid = CreateDraft(evidence);
        var first = valid.TorahTeachings[0];
        var news = valid.CurrentEventFacts[0];
        WeeklyDvarTorahSourcedStatementDraft[] malformedStatements =
        [
            first with { Text = " " },
            first with { Text = new string('s', 1_201) },
            first with { EvidenceIds = [] },
            first with { EvidenceIds = Enumerable.Range(1, 9).Select(index => $"T{index}").ToArray() },
            first with { EvidenceIds = ["T1", " "] },
            first with { EvidenceIds = ["T1", "T1"] },
            first with { EvidenceIds = ["UNKNOWN"], Quotations = [new WeeklyDvarTorahQuotationDraft { EvidenceId = "UNKNOWN", Text = "Unknown" }] },
            first with { EvidenceIds = ["N1"], Quotations = [new WeeklyDvarTorahQuotationDraft { EvidenceId = "N1", Text = evidence[8].PresentedText }] },
        ];

        foreach (var statement in malformedStatements)
        {
            var draft = valid with { TorahTeachings = [statement, .. valid.TorahTeachings.Skip(1)] };
            Assert.IsFalse(WeeklyDvarTorahCandidateValidator.Validate(draft, evidence, CreateOptions()).IsValid);
        }

        var wrongKindNews = valid with { CurrentEventFacts = [news with { EvidenceIds = ["T1"], Quotations = [new WeeklyDvarTorahQuotationDraft { EvidenceId = "T1", Text = evidence[0].PresentedText }] }] };
        var torahOnlyConnection = valid with { Connections = [CreateStatement("Connection", evidence[0])] };
        var newsOnlyConnection = valid with { Connections = [CreateStatement("Connection", evidence[8])] };
        Assert.IsFalse(WeeklyDvarTorahCandidateValidator.Validate(wrongKindNews, evidence, CreateOptions()).IsValid);
        Assert.IsFalse(WeeklyDvarTorahCandidateValidator.Validate(torahOnlyConnection, evidence, CreateOptions()).IsValid);
        Assert.IsFalse(WeeklyDvarTorahCandidateValidator.Validate(newsOnlyConnection, evidence, CreateOptions()).IsValid);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_IncompleteOrUnverifiableQuotations_FailClosed()
    {
        var evidence = CreateEvidence();
        var valid = CreateDraft(evidence);
        var first = valid.TorahTeachings[0];
        WeeklyDvarTorahSourcedStatementDraft[] malformedStatements =
        [
            first with { Quotations = [] },
            first with { Quotations = Enumerable.Repeat(first.Quotations[0], 13).ToArray() },
            first with { Quotations = [null!, first.Quotations[1]] },
            first with { Quotations = [new WeeklyDvarTorahQuotationDraft { EvidenceId = " ", Text = "Text" }, first.Quotations[1]] },
            first with { Quotations = [new WeeklyDvarTorahQuotationDraft { EvidenceId = "T3", Text = evidence[2].PresentedText }, first.Quotations[1]] },
            first with { Quotations = [new WeeklyDvarTorahQuotationDraft { EvidenceId = "T1", Text = " " }, first.Quotations[1]] },
            first with { Quotations = [new WeeklyDvarTorahQuotationDraft { EvidenceId = "T1", Text = new string('q', 121) }, first.Quotations[1]] },
            first with { Quotations = [first.Quotations[0], first.Quotations[0]] },
        ];

        foreach (var statement in malformedStatements)
        {
            var draft = valid with { TorahTeachings = [statement, .. valid.TorahTeachings.Skip(1)] };
            Assert.IsFalse(WeeklyDvarTorahCandidateValidator.Validate(draft, evidence, CreateOptions()).IsValid);
        }

        var unknownIdStatement = first with
        {
            EvidenceIds = ["T1", "UNKNOWN"],
            Quotations = [first.Quotations[0], new WeeklyDvarTorahQuotationDraft { EvidenceId = "UNKNOWN", Text = "Unknown" }],
        };
        var unknownIdDraft = valid with { TorahTeachings = [unknownIdStatement, .. valid.TorahTeachings.Skip(1)] };
        Assert.IsFalse(WeeklyDvarTorahCandidateValidator.Validate(unknownIdDraft, evidence, CreateOptions()).IsValid);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_MissingOrUnknownBodyMarkers_FailClosed()
    {
        var evidence = CreateEvidence();
        var valid = CreateDraft(evidence);
        var missingMarker = valid with { Body = valid.Body.Replace("[T1]", string.Empty, StringComparison.Ordinal) };
        var unknownMarker = valid with { Body = $"{valid.Body} [T99]" };

        Assert.IsFalse(WeeklyDvarTorahCandidateValidator.Validate(missingMarker, evidence, CreateOptions()).IsValid);
        Assert.IsFalse(WeeklyDvarTorahCandidateValidator.Validate(unknownMarker, evidence, CreateOptions()).IsValid);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_NewsFromOnePublisher_FailsIndependentCorroboration()
    {
        var evidence = CreateEvidence().ToArray();
        evidence[9] = evidence[9] with { Publisher = evidence[8].Publisher };
        var draft = CreateDraft(evidence);

        var result = WeeklyDvarTorahCandidateValidator.Validate(draft, evidence, CreateOptions());

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("independent publishers", StringComparison.Ordinal)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_SensitivePersonalDataOrDirectUrl_FailsClosed()
    {
        var evidence = CreateEvidence();
        var valid = CreateDraft(evidence);
        WeeklyDvarTorahArticleDraft[] invalidDrafts =
        [
            valid with { Title = "Contact editor@example.test" },
            valid with { CentralTeaching = "Call 202-555-0123 for more information about this otherwise sufficiently long central teaching." },
            valid with { PracticalActions = ["Visit https://example.test/private", "Perform one private act of kindness.", "Study one passage again this week."] },
            valid with { Tags = ["responsibility", "community", "nitzavim", "technology", "192.0.2.10"] },
        ];

        foreach (var draft in invalidDrafts)
        {
            var result = WeeklyDvarTorahCandidateValidator.Validate(draft, evidence, CreateOptions());

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(error => error.Contains("contact details, IP addresses, or direct URLs", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_NullInputs_Throw()
    {
        var evidence = CreateEvidence();
        var draft = CreateDraft(evidence);

        Assert.ThrowsExactly<ArgumentNullException>(() => WeeklyDvarTorahCandidateValidator.Validate(null!, evidence, CreateOptions()));
        Assert.ThrowsExactly<ArgumentNullException>(() => WeeklyDvarTorahCandidateValidator.Validate(draft, null!, CreateOptions()));
        Assert.ThrowsExactly<ArgumentNullException>(() => WeeklyDvarTorahCandidateValidator.Validate(draft, evidence, null!));
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
