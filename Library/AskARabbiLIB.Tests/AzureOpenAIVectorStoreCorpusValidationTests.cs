using System.Text;
using System.Text.RegularExpressions;
using AskARabbiLIB.Retrieval;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class AzureOpenAIVectorStoreCorpusValidationTests
{
    private static readonly string Fingerprint = new('f', 64);

    [TestMethod]
    [DataRow("documentOrdinal", "-1")]
    [DataRow("windowIndex", "-1")]
    [DataRow("excerptStart", "-1")]
    [DataRow("originalCharacterCount", "0")]
    [DataRow("originalSegmentId", "\"wrong\"")]
    [DataRow("segmentId", "\"wrong\"")]
    [DataRow("lookupToken", "\"wrong\"")]
    [DataRow("canonicalReference", "\" \"")]
    [TestCategory("Unit")]
    public void Parse_TamperedEnvelope_RejectsRecord(string propertyName, string replacementJson)
    {
        var formatted = CreateFormatted();
        var content = Encoding.UTF8.GetString(formatted.Content);
        var mutated = ReplaceJsonValue(content, propertyName, replacementJson);

        Assert.ThrowsExactly<InvalidDataException>(() => new AzureOpenAIVectorStoreCorpusParser().Parse(formatted.Attributes, [mutated], Fingerprint));
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("marker-without-newline")]
    [DataRow("missing-metadata-newline")]
    [DataRow("missing-passage")]
    [DataRow("missing-end")]
    [TestCategory("Unit")]
    public void Parse_IncompleteSearchChunk_IgnoresUntrustedFragment(string scenario)
    {
        var formatted = CreateFormatted();
        var content = Encoding.UTF8.GetString(formatted.Content);
        var start = content.IndexOf("ASKARABBI_SEGMENT_V1_START", StringComparison.Ordinal);
        var metadataEnd = content.IndexOf('\n', start + "ASKARABBI_SEGMENT_V1_START\n".Length);
        var passage = content.IndexOf("\nPassage:\n", metadataEnd, StringComparison.Ordinal);
        var end = content.IndexOf("\nASKARABBI_SEGMENT_V1_END", passage, StringComparison.Ordinal);
        var chunk = scenario switch
        {
            "empty" => string.Empty,
            "marker-without-newline" => "ASKARABBI_SEGMENT_V1_STARTx",
            "missing-metadata-newline" => content[start..metadataEnd],
            "missing-passage" => content[start..(passage - 1)],
            "missing-end" => content[start..end],
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };

        var segments = new AzureOpenAIVectorStoreCorpusParser().Parse(formatted.Attributes, [chunk], Fingerprint);

        Assert.HasCount(0, segments);
    }

    [TestMethod]
    [DataRow("bad-json")]
    [DataRow("null-json")]
    [DataRow("extra-property")]
    [TestCategory("Unit")]
    public void Parse_InvalidEnvelopeJson_RejectsRecord(string scenario)
    {
        var formatted = CreateFormatted();
        var content = Encoding.UTF8.GetString(formatted.Content);
        var metadataStart = content.IndexOf('{', StringComparison.Ordinal);
        var metadataEnd = content.IndexOf('\n', metadataStart);
        var replacement = scenario switch
        {
            "bad-json" => "{",
            "null-json" => "null",
            "extra-property" => content[metadataStart..metadataEnd].Replace("{", "{\"unexpected\":true,", StringComparison.Ordinal),
            _ => throw new AssertFailedException($"Unknown scenario '{scenario}'."),
        };
        var mutated = string.Concat(content.AsSpan(0, metadataStart), replacement, content.AsSpan(metadataEnd));

        Assert.ThrowsExactly<InvalidDataException>(() => new AzureOpenAIVectorStoreCorpusParser().Parse(formatted.Attributes, [mutated], Fingerprint));
    }

    [TestMethod]
    [DataRow("missing-fingerprint")]
    [DataRow("wrong-provider")]
    [DataRow("categories-json")]
    [DataRow("categories-null")]
    [DataRow("categories-empty")]
    [DataRow("categories-blank")]
    [DataRow("license-category")]
    [DataRow("missing-title")]
    [TestCategory("Unit")]
    public void Parse_InvalidFileAttributes_RejectsResult(string scenario)
    {
        var formatted = CreateFormatted();
        var attributes = formatted.Attributes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        switch (scenario)
        {
            case "missing-fingerprint":
                attributes.Remove("corpusFingerprint");
                break;
            case "wrong-provider":
                attributes["sourceProvider"] = "Other";
                break;
            case "categories-json":
                attributes["categories"] = "not-json";
                break;
            case "categories-null":
                attributes["categories"] = "null";
                break;
            case "categories-empty":
                attributes["categories"] = "[]";
                break;
            case "categories-blank":
                attributes["categories"] = "[\" \"]";
                break;
            case "license-category":
                attributes["licenseCategory"] = "PublicDomain";
                break;
            case "missing-title":
                attributes["title"] = string.Empty;
                break;
            default:
                throw new AssertFailedException($"Unknown scenario '{scenario}'.");
        }

        Assert.ThrowsExactly<InvalidDataException>(() => new AzureOpenAIVectorStoreCorpusParser().Parse(attributes, [Encoding.UTF8.GetString(formatted.Content)], Fingerprint));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Parse_DuplicateConflictingRecord_RejectsResult()
    {
        var formatted = CreateFormatted();
        var content = Encoding.UTF8.GetString(formatted.Content);
        var conflicting = content.Replace("Published source text.", "Different source text.", StringComparison.Ordinal);

        Assert.ThrowsExactly<InvalidDataException>(() => new AzureOpenAIVectorStoreCorpusParser().Parse(formatted.Attributes, [content, conflicting], Fingerprint));
    }

    private static AzureOpenAIVectorStoreCorpusDocument CreateFormatted()
    {
        var document = TestManifestFactory.CreateDocument(segmentCount: 1, firstReference: "Genesis 1:1", lastReference: "Genesis 1:1");
        const string markdown = """
            # Genesis

            ## Genesis 1:1
            Published source text.
            """;
        return new AzureOpenAIVectorStoreCorpusFormatter().Format(document, markdown, Fingerprint);
    }

    private static string ReplaceJsonValue(string content, string propertyName, string replacementJson)
    {
        var expression = new Regex($"(\"{Regex.Escape(propertyName)}\":)(\"[^\"]*\"|-?\\d+|true|false)", RegexOptions.CultureInvariant);
        if (!expression.IsMatch(content))
        {
            throw new AssertFailedException($"Property '{propertyName}' was not found in the generated envelope.");
        }
        return expression.Replace(content, match => match.Groups[1].Value + replacementJson, 1);
    }
}
