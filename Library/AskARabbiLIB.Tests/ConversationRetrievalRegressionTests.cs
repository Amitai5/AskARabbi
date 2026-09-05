using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Calendar;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class ConversationRetrievalRegressionTests
{
    [TestMethod]
    [DataRow("Explain Deuteronomy 6:4 in plain English.", "Deuteronomy 6:4")]
    [DataRow("What does Genesis 44:18-47:27 say?", "Genesis 44:18-47:27")]
    [DataRow("Read Chullin 104b:1-9", "Chullin 104b:1-9")]
    [TestCategory("Regression")]
    public void FindExplicitReference_ReferenceQuestion_UsesExactAddress(string question, string expected)
    {
        Assert.AreEqual(expected, ConversationReferenceGuide.FindExplicitReference(question));
    }

    [TestMethod]
    [DataRow("Deuteronomy 6:4", "Deuteronomy 4:6", false)]
    [DataRow("Deuteronomy 6:4", "Deuteronomy 6:4", true)]
    [DataRow("Genesis 44:18-47:27", "Genesis 45:5", true)]
    [DataRow("Genesis 44:18-47:27", "Genesis 47:28", false)]
    [DataRow("Chullin 104b:1-9", "Chullin 104b:8", true)]
    [DataRow("Chullin 104b:1-9", "Chullin 105a:1", false)]
    [DataRow("Exodus 14", "Exodus 14:31", true)]
    [TestCategory("Regression")]
    public void CanonicalRange_ReferenceBoundaries_ExcludesUnrelatedPassages(string rangeText, string reference, bool expected)
    {
        Assert.IsTrue(CanonicalReferenceRange.TryParse(rangeText, out var range));
        Assert.IsNotNull(range);
        Assert.AreEqual(expected, range.Contains(reference));
    }

    [TestMethod]
    [DataRow("Summarize Parashat Vayigash in two paragraphs.", "Genesis 44:18-47:27")]
    [DataRow("מה הרעיון המרכזי של פרשת ניצבים?", "Deuteronomy 29:9-30:20")]
    [DataRow("Explain Nitzavim Vayeilech", "Deuteronomy 29:9-31:30")]
    [TestCategory("Regression")]
    public void ResolveName_ExplicitPortion_DoesNotUseCurrentWeek(string question, string expectedRange)
    {
        var name = ParashahTorahRangeCatalog.ResolveName(question);

        Assert.IsNotNull(name);
        Assert.AreEqual(expectedRange, ParashahTorahRangeCatalog.GetCanonicalRange(name));
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void GetReferences_TopicChanges_DoesNotCarryPoultryIntoExodusSummary()
    {
        var prior = new[] { new GroundedConversationTurn("Why chicken with milk?", "An earlier answer.") };

        var references = ConversationReferenceGuide.GetReferences("Explain the story of the Exodus", prior);

        Assert.IsTrue(references.All(reference => reference.StartsWith("Exodus ", StringComparison.Ordinal)));
        Assert.IsTrue(references.Contains("Exodus 14:1-31"), "The sea crossing must not be omitted from the story coverage.");
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void GetReferences_RationaleFollowUp_LoadsDebateInsteadOfOnlyMilkSubstitutes()
    {
        var prior = new[] { new GroundedConversationTurn("Why can't I eat chicken with milk?", "An earlier answer.") };

        var references = ConversationReferenceGuide.GetReferences("Why did the rabbis choose that?", prior);

        Assert.IsTrue(references.Contains("Chullin 104b:1-9"));
        Assert.IsTrue(references.Contains("Mishnah Chullin 8:4"));
        Assert.IsFalse(references.Contains("Shulchan Arukh, Yoreh De'ah 87:4"));
    }

    [TestMethod]
    [DataRow("How does a car engine work?", "mechanical advice")]
    [DataRow("Can you explain that?", "Which topic or passage")]
    [DataRow("Tell me the summary.", "Which topic or passage")]
    [DataRow("Hello! I am new to studying Jewish texts. Where should I begin?", "Welcome!")]
    [TestCategory("Regression")]
    public async Task DirectReply_ConversationNavigation_ReturnsFriendlyAnswerWithoutSearch(string question, string expected)
    {
        var result = await ConversationDirectReply.TryAnswerAsync(new GroundedQuestion { Question = question }, [], null, new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero), CancellationToken.None);

        Assert.IsNotNull(result?.Answer);
        StringAssert.Contains(new GroundedAnswerTextRenderer().Render(result.Answer), expected);
        Assert.AreEqual(0, result.Trace.ProviderAttempts);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task DirectReply_ShabbatCarQuestion_DoesNotTreatJewishPracticeAsOutOfScope()
    {
        var result = await ConversationDirectReply.TryAnswerAsync(new GroundedQuestion { Question = "Why is driving a car on Shabbat prohibited?" }, [], null, new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero), CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task DirectReply_TodaysDateWithoutSunset_ShowsBothDaysWithoutModelOrQuotationFailure()
    {
        var registry = new AIToolRegistry([new CalendarAITools(new HebrewCalendarService())]);
        var question = new GroundedQuestion { Question = "What is today's date in the Gregorian and Hebrew calendars?" };

        var result = await ConversationDirectReply.TryAnswerAsync(question, [], registry, new DateTimeOffset(2026, 9, 5, 23, 0, 0, TimeSpan.Zero), CancellationToken.None);

        Assert.IsNotNull(result?.Answer);
        var text = new GroundedAnswerTextRenderer().Render(result.Answer);
        StringAssert.Contains(text, "September 5, 2026");
        StringAssert.Contains(text, "23 Elul");
        StringAssert.Contains(text, "24 Elul");
        Assert.AreEqual(0, result.Trace.ProviderAttempts);
    }

    [TestMethod]
    [DataRow("Hebrew")]
    [DataRow("Spanish")]
    [TestCategory("Regression")]
    public void PreferredLanguages_ExplicitFilter_DoesNotAddEnglishOrOtherLanguages(string language)
    {
        var question = new GroundedQuestion { Question = "Read a verse", ConversationLanguage = "English", QuotationLanguage = "English", Languages = [language] };

        var languages = ConversationReferenceGuide.PreferredLanguages(question);

        CollectionAssert.AreEqual(new[] { language }, languages.ToArray());
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task NoRegularParashah_FestivalExplained_ReturnsCalculatedDateWithoutCallingModel()
    {
        var registry = new AIToolRegistry([new CalendarAITools(new HebrewCalendarService())]);
        var calendar = await registry.ExecuteAsync("find_parashah_for_week", BinaryData.FromString("{\"dateTime\":\"2026-09-12T00:00:00\",\"inIsrael\":false}"), new AIToolExecutionContext(null, new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero)));

        var answer = ConversationDirectReply.NoRegularParashah(calendar, "Rosh Hashana");

        Assert.IsTrue(answer.IsSuccess);
        Assert.IsNotNull(answer.Answer);
        StringAssert.Contains(new GroundedAnswerTextRenderer().Render(answer.Answer), "September 12, 2026");
        Assert.AreEqual(0, answer.Trace.ProviderAttempts);
        Assert.HasCount(1, answer.Answer.Citations);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task DirectReply_ExplicitBeforeSunset_UsesOnlyRequestedDate()
    {
        var registry = new AIToolRegistry([new CalendarAITools(new HebrewCalendarService())]);
        var question = new GroundedQuestion { Question = "What Hebrew date is September 5, 2026, before sunset?" };

        var result = await ConversationDirectReply.TryAnswerAsync(question, [], registry, new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

        Assert.IsNotNull(result?.Answer);
        var text = new GroundedAnswerTextRenderer().Render(result.Answer);
        StringAssert.Contains(text, "23 Elul");
        Assert.IsFalse(text.Contains("24 Elul", StringComparison.Ordinal));
        Assert.AreEqual(0, result.Trace.ProviderAttempts);
    }
}
