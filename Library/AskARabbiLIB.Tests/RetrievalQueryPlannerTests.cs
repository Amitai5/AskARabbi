using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class RetrievalQueryPlannerTests
{
    [TestMethod]
    [DataRow("How does the prayer for dates work?")]
    [DataRow("How do the prayers for dates work?")]
    [DataRow("How does a blessing work")]
    public void Plan_FunctionQuestion_DoesNotMistakeWorkForEmployment(string question)
    {
        var plan = RetrievalQueryPlanner.Plan(question);

        Assert.IsFalse(plan.Concepts.Any(concept => concept.Key == "business"));
        Assert.IsTrue(plan.Concepts.Any(concept => concept.Key is "prayer" or "prayers" or "blessing"));
    }

    [TestMethod]
    [DataRow("Can I work on Shabbat?")]
    [DataRow("How does work on Shabbat differ from rest?")]
    public void Plan_ActualWorkQuestion_PreservesLaborConcept(string question)
    {
        Assert.IsTrue(RetrievalQueryPlanner.Plan(question).Concepts.Any(concept => concept.Key == "business"));
    }

    [TestMethod]
    public void Plan_FollowUpContext_DoesNotSpendConceptBudgetOnLabelsOrDuplicateWords()
    {
        var plan = RetrievalQueryPlanner.Plan("How does the prayer for dates work?\nEarlier topic context: What is the connection between squash and the Rosh Hashanah prayer?");
        var keys = plan.Concepts.Select(concept => concept.Key).ToArray();

        Assert.AreEqual(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.IsFalse(keys.Any(key => key is "earlier" or "topic" or "context" or "business"));
        CollectionAssert.IsSubsetOf(new[] { "gourd", "prayer", "dates", "rosh", "hashanah" }, keys);
    }
}
