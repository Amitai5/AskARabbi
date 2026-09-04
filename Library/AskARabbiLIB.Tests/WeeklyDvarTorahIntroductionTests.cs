using AskARabbiLIB.DvarTorah;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class WeeklyDvarTorahIntroductionTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [TestCategory("Regression")]
    public void Prepend_ExistingOrMissingWelcome_AddsExactlyOneWithoutChangingEvidence(int repetitions)
    {
        const string content = "A scene [TA].\n\nA question [TB].\n\nA closing thought.";
        var input = string.Concat(Enumerable.Repeat(WeeklyDvarTorahIntroduction.Text + "\n\n", repetitions)) + content;

        var result = WeeklyDvarTorahIntroduction.Prepend(input);

        Assert.AreEqual(WeeklyDvarTorahIntroduction.Text + "\n\n" + content, result);
        Assert.AreEqual(result, WeeklyDvarTorahIntroduction.Prepend(result));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Prepend_NullInput_Throws() => Assert.ThrowsExactly<ArgumentNullException>(() => WeeklyDvarTorahIntroduction.Prepend(null!));
}
