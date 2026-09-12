using System.Text.Json;
using AskARabbiLIB.Lexicon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class BdbEntryParserTests
{
    [TestMethod]
    public void Parse_OriginalHtml_PreservesPointingDefinitionsAndSourceProvenance()
    {
        var json = Json("<style>.test { color:red; }</style><p><span>קָרָא</span> <b>call</b> &amp; read.</p><p>Second sense.</p><script>alert(1)</script>");

        var entry = new BdbEntryParser().Parse(json, "001.content.json").Single();

        Assert.AreEqual("קָרָא call & read.\nSecond sense.", entry.Text);
        Assert.AreEqual("קָרָא", entry.Headword);
        Assert.AreEqual(BdbCorpus.Revision, entry.Revision);
        Assert.AreEqual(BdbCorpus.License, entry.License);
        Assert.AreEqual(64, entry.SourceSha256.Length);
        StringAssert.Contains(entry.SourceUrl, BdbCorpus.Revision + "/eng/json/001.content.json");
    }

    [TestMethod]
    public void Parse_Homographs_PreservesSeparateArticleIds()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new[] { Article("BDB00001", "קרא", "First sense."), Article("BDB00002", "קרא", "Different sense.") });

        var entries = new BdbEntryParser().Parse(json, "018.content.json");

        Assert.HasCount(2, entries);
        Assert.AreNotEqual(entries[0].EntryId, entries[1].EntryId);
        Assert.AreEqual(entries[0].Headword, entries[1].Headword);
    }

    [TestMethod]
    public void Parse_DuplicateIdentifiers_RejectsWholeFile()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new[] { Article("BDB00001", "קרא", "First."), Article("BDB00001", "קרע", "Second.") });

        Assert.Throws<InvalidDataException>(() => new BdbEntryParser().Parse(json, "001.content.json"));
    }

    [TestMethod]
    [DataRow("../001.content.json")]
    [DataRow("source.json")]
    public void Parse_UnsafeSourceFile_RejectsInput(string file) => Assert.Throws<ArgumentException>(() => new BdbEntryParser().Parse(Json("text"), file));

    [TestMethod]
    [DataRow("[]", false)]
    [DataRow("{}", true)]
    [DataRow("[null]", true)]
    [DataRow("[{\"content_id\":\"BDB00001\"}]", true)]
    public void Parse_EmptyOrInvalidStructure_HandlesExplicitly(string json, bool invalid)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        if (invalid)
        {
            Assert.Throws<InvalidDataException>(() => new BdbEntryParser().Parse(bytes, "001.content.json"));
        }
        else
        {
            Assert.HasCount(0, new BdbEntryParser().Parse(bytes, "001.content.json"));
        }
    }

    [TestMethod]
    [DataRow("שָׁלוֹם", "שלום")]
    [DataRow("I קָרָא", "קרא")]
    [DataRow("קָרַע", "קרע")]
    public void HeadwordKey_VowelsAndHomographNumbers_PreservesConsonants(string input, string expected) => Assert.AreEqual(expected, LexiconSearchText.HeadwordKey(input));

    [TestMethod]
    public void CreateKeys_RelatedWordMention_IsSearchableButNotAnotherHeadword()
    {
        var entry = LexiconTestData.Entry() with { Headword = "אבב", Text = "אָבִיב young ears of barley" };

        var keys = LexiconSearchText.CreateKeys(entry);

        CollectionAssert.Contains(keys, "head:אבב");
        CollectionAssert.Contains(keys, "he:אביב");
        CollectionAssert.Contains(keys, "en:barley");
        CollectionAssert.DoesNotContain(keys, "head:אביב");
    }

    [TestMethod]
    public void CreateKeys_ParagraphOpening_PrioritizesOriginalSubentryWithoutBoostingPassingMentions()
    {
        var entry = LexiconTestData.Entry() with { Text = "אָבִיב barley " + new string('x', 300) + " gourd\nקִיקָיוֹן plant" };

        var keys = LexiconSearchText.CreateKeys(entry);

        CollectionAssert.Contains(keys, "lead:en:barley");
        CollectionAssert.Contains(keys, "lead:he:קיקיון");
        CollectionAssert.Contains(keys, "en:gourd");
        CollectionAssert.DoesNotContain(keys, "lead:en:gourd");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("the and of")]
    [DataRow("one two three four five six seven")]
    [DataRow(".*$")]
    public void QueryKeys_InvalidOrUnboundedInput_Rejects(string query) => Assert.Throws<ArgumentException>(() => LexiconSearchText.QueryKeys(query));

    [TestMethod]
    public void QueryKeys_EnglishMeaning_KeepsDistinctContentTerms() => CollectionAssert.AreEqual(new[] { "en:read", "en:call" }, LexiconSearchText.QueryKeys("to read and call read"));

    private static byte[] Json(string content) => JsonSerializer.SerializeToUtf8Bytes(new[] { Article("BDB00001", "קָרָא", content) });

    private static object Article(string id, string title, string content) => new { content_id = id, title, content, language = "eng", media_type = "Text", version = "1.0.4" };
}
