using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.DvarTorah.Audio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class DvarTorahAudioTextTests
{
    [TestMethod]
    [DataRow(" A\r\n\tB [T1] ", " A\r\n\tB [T1] ")]
    [DataRow("\u0013\u0014\u0018\u0019\u001c\u001d\u0085\u0091\u0092\u0093\u0094\u0096\u0097", "–—‘’“”…‘’“”–—")]
    [DataRow("x\u0000\u0008\u000b\u000c\u000e\u0012\u0015\u0017\u001a\u001b\u001e\u001f\u007f\u009fy", "xy")]
    [DataRow("שַׁבָּת שָׁלוֹם 😀", "שַׁבָּת שָׁלוֹם 😀")]
    [TestCategory("Unit")]
    public void Normalize_DisplayTextContract_PreservesOffsets(string source, string expected)
    {
        Assert.AreEqual(expected, DvarTorahAudioText.Normalize(source));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetVersion_TextOrVoiceChange_InvalidatesRecording()
    {
        var article = DvarTorahAudioTestData.Article();
        var equivalent = DvarTorahAudioTestData.Article();

        var version = DvarTorahAudioText.GetVersion(article, "voice-a");

        Assert.AreEqual(64, version.Length);
        Assert.AreEqual(version, DvarTorahAudioText.GetVersion(equivalent, "voice-a"));
        Assert.AreNotEqual(version, DvarTorahAudioText.GetVersion(article, "voice-b"));
        Assert.AreNotEqual(version, DvarTorahAudioText.GetVersion(DvarTorahAudioTestData.Article("Other body"), "voice-a"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetChunks_CitationsAndLongText_RetainsEverySpokenCharacterAndDisplayOffset()
    {
        var text = "Hello [T1] world [1, 2]. " + string.Concat(Enumerable.Repeat("שבת שלום. ", 300));

        var chunks = DvarTorahAudioText.GetChunks("body", text, 80);

        Assert.IsGreaterThan(1, chunks.Count);
        Assert.IsTrue(chunks.All(chunk => chunk.Text.Length <= 80));
        var result = string.Concat(chunks.Select(chunk => chunk.Text));
        Assert.AreEqual(text.Replace("[T1]", "    ").Replace("[1, 2]", "      "), result);
        foreach (var chunk in chunks)
        {
            Assert.AreEqual(chunk.Text, result.Substring(chunk.DisplayOffset, chunk.Text.Length));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetChunks_LongUnbrokenText_DoesNotSplitSurrogatePairs()
    {
        var text = "aaaa😀" + new string('x', 30);

        var chunks = DvarTorahAudioText.GetChunks("body", text, 5);

        Assert.AreEqual(text, string.Concat(chunks.Select(chunk => chunk.Text)));
        Assert.IsTrue(chunks.All(chunk => !char.IsHighSurrogate(chunk.Text[^1])));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Ssml_HebrewAndXmlCharacters_UsesExplicitLanguagesAndExactDisplayPositions()
    {
        const string text = "Welcome & <learn> שַׁבָּת שָׁלוֹם again.";
        var ssml = new DvarTorahSsml(new NarrationChunk("body", 10, text), "en-US-AndrewMultilingualNeural");

        var parsed = XDocument.Parse(ssml.Text);

        Assert.AreEqual(text, parsed.Root?.Value);
        StringAssert.Contains(ssml.Text, "xml:lang='he-IL'");
        StringAssert.Contains(ssml.Text, "&amp;");
        StringAssert.Contains(ssml.Text, "&lt;learn&gt;");
        Assert.AreEqual(10, ssml.GetDisplayOffset((uint)ssml.Text.IndexOf("Welcome", StringComparison.Ordinal)));
        Assert.AreEqual(10 + text.IndexOf("שַׁבָּת", StringComparison.Ordinal), ssml.GetDisplayOffset((uint)ssml.Text.IndexOf("שַׁבָּת", StringComparison.Ordinal)));
        Assert.AreEqual(-1, ssml.GetDisplayOffset(uint.MaxValue));
        Assert.AreEqual(-1, ssml.GetDisplayOffset(0));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ValidateTimings_MismatchedOrReorderedWords_RejectsWrongHighlighting()
    {
        var timings = DvarTorahAudioTestData.Timings();
        DvarTorahAudioValidation.ValidateTimings(timings);

        Assert.ThrowsExactly<InvalidDataException>(() => DvarTorahAudioValidation.ValidateTimings(timings with { Words = [timings.Words[0] with { Text = "Wrong" }] }));
        Assert.ThrowsExactly<InvalidDataException>(() => DvarTorahAudioValidation.ValidateTimings(timings with { Words = [timings.Words[0] with { TextOffset = int.MaxValue }] }));
        Assert.ThrowsExactly<InvalidDataException>(() => DvarTorahAudioValidation.ValidateTimings(timings with { Words = [timings.Words[0] with { AudioOffsetMs = double.NaN }] }));
        Assert.ThrowsExactly<InvalidDataException>(() => DvarTorahAudioValidation.ValidateTimings(timings with { Words = [timings.Words[1], timings.Words[0]] }));
        Assert.ThrowsExactly<InvalidDataException>(() => DvarTorahAudioValidation.ValidateTimings(timings with { SchemaVersion = 2 }));
        Assert.ThrowsExactly<InvalidDataException>(() => DvarTorahAudioValidation.ValidateTimings(timings with { Words = [] }));
    }

    [TestMethod]
    [DataRow("../../other")]
    [DataRow("diaspora:2026-09-05/../../other")]
    [DataRow("wrong:2026-09-05")]
    [TestCategory("Unit")]
    public void GetPrefix_InvalidWeek_RejectsTraversal(string weekKey)
    {
        Assert.ThrowsExactly<ArgumentException>(() => DvarTorahAudioValidation.GetPrefix(weekKey, new string('a', 64)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validation_InvalidManifestFields_RejectsMalformedExternalData()
    {
        var valid = DvarTorahAudioTestData.Timings();
        var invalid = new[]
        {
            valid with { Title = " " }, valid with { Body = " " }, valid with { Voice = " " },
            valid with { Title = new string('x', 161) }, valid with { Body = new string('x', 40_001) },
            valid with { DurationMs = double.PositiveInfinity }, valid with { DurationMs = 0 }, valid with { DurationMs = 3_600_001 },
            valid with { Words = null! }, valid with { Words = Enumerable.Repeat(valid.Words[0], 40_001).ToArray() },
            valid with { TextOffsetUnit = "bytes" },
            valid with { Words = [valid.Words[0] with { Section = "elsewhere" }] },
            valid with { Words = [valid.Words[0] with { Text = "" }] },
            valid with { Words = [valid.Words[0] with { TextLength = 2 }] },
            valid with { Words = [valid.Words[0] with { DurationMs = double.NaN }] },
            valid with { Words = [valid.Words[0] with { DurationMs = -1 }] },
            valid with { Words = [valid.Words[0] with { AudioOffsetMs = 2000 }] },
            valid with { Words = [valid.Words[0], valid.Words[0] with { AudioOffsetMs = 500 }] },
            valid with { Words = [valid.Words[1], valid.Words[0] with { AudioOffsetMs = 1000 }] },
        };

        foreach (var manifest in invalid)
        {
            Assert.ThrowsExactly<InvalidDataException>(() => DvarTorahAudioValidation.ValidateTimings(manifest));
        }
        Assert.ThrowsExactly<ArgumentNullException>(() => DvarTorahAudioValidation.ValidateTimings(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => DvarTorahAudioText.Normalize(null!));
        Assert.ThrowsExactly<ArgumentException>(() => DvarTorahAudioValidation.ValidateVersion(null!));
        Assert.ThrowsExactly<ArgumentException>(() => DvarTorahAudioValidation.GetPrefix(null!, valid.Version));
        Assert.HasCount(0, DvarTorahAudioText.GetChunks("body", "[T1]  \n"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Ssml_QuotesAndEntirelyHebrew_PreservesBothWithoutTrailingEnglish()
    {
        var quoted = new DvarTorahSsml(new NarrationChunk("body", 0, "\"Don't\""), "voice");
        var hebrew = new DvarTorahSsml(new NarrationChunk("body", 0, "שבת שלום"), "voice");

        Assert.AreEqual("\"Don't\"", XDocument.Parse(quoted.Text).Root?.Value);
        Assert.AreEqual("שבת שלום", XDocument.Parse(hebrew.Text).Root?.Value);
        StringAssert.Contains(quoted.Text, "&quot;");
        StringAssert.Contains(quoted.Text, "&apos;");
    }
}
