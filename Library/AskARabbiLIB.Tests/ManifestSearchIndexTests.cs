using AskARabbiLIB.Models;
using AskARabbiLIB.Search;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class ManifestSearchIndexTests
{
    private ManifestSearchIndex index = null!;

    [TestInitialize]
    public void Initialize()
    {
        var genesisEnglish = TestManifestFactory.CreateDocument(
            title: "Genesis",
            language: "English",
            languageCode: "en",
            description: "Genesis contains creation narratives.",
            versionTitle: "English Genesis",
            segmentCount: 100,
            filePath: "Data/Normalized/Genesis-English.md",
            rawFilePath: "Data/Raw/Genesis-English.json");
        var genesisHebrew = TestManifestFactory.CreateDocument(
            title: "Genesis",
            language: "Hebrew",
            languageCode: "he",
            description: "Genesis in Hebrew.",
            versionTitle: "Hebrew Genesis",
            license: null,
            licenseStatus: "review_required",
            segmentCount: 120,
            filePath: "Data/Normalized/Genesis-Hebrew.md",
            rawFilePath: "Data/Raw/Genesis-Hebrew.json");
        var shabbat = TestManifestFactory.CreateDocument(
            title: "Shabbat",
            hebrewTitle: "שַׁבָּת",
            collection: "Talmud",
            categories: new[] { "Talmud", "Bavli", "Seder Moed" },
            description: "Shabbat is a Babylonian Talmud tractate.",
            versionTitle: "William Davidson Edition",
            segmentCount: 250,
            firstReference: "Shabbat 2a:1",
            lastReference: "Shabbat 157b:12",
            filePath: "Data/Normalized/Shabbat.md",
            rawFilePath: "Data/Raw/Shabbat.json");
        var berakhot = TestManifestFactory.CreateDocument(
            title: "Bérakhot",
            hebrewTitle: "בְּרָכוֹת",
            language: "Hebrew",
            languageCode: "he",
            collection: "Mishnah",
            categories: new[] { "Mishnah", "Seder Zeraim" },
            description: "A tractate whose notes mention Shabbat practices.",
            versionTitle: "Hebrew Mishnah",
            license: "CC-BY-NC",
            licenseStatus: "noncommercial",
            segmentCount: 50,
            firstReference: "Mishnah Berakhot 1:1",
            lastReference: "Mishnah Berakhot 9:5",
            filePath: "Data/Normalized/Berakhot.md",
            rawFilePath: "Data/Raw/Berakhot.json");
        index = ManifestSearchIndex.Create(TestManifestFactory.CreateManifest(genesisEnglish, genesisHebrew, shabbat, berakhot));
    }

    [TestMethod]
    [DataRow("shab", "Shabbat")]
    [DataRow("שבת", "Shabbat")]
    [DataRow("berakhot", "Bérakhot")]
    [DataRow("ברכות", "Bérakhot")]
    [TestCategory("Unit")]
    public void Search_NormalizedOrPrefixKeyword_ReturnsExpectedTitle(string keywords, string expectedTitle)
    {
        // Arrange
        var query = new ManifestSearchQuery { Keywords = keywords };

        // Act
        var result = index.Search(query);

        // Assert
        Assert.IsTrue(result.Hits.Count > 0);
        Assert.AreEqual(expectedTitle, result.Hits[0].Document.FileTitle);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Search_TitleMatchAndDescriptionMatch_RanksTitleFirst()
    {
        // Arrange
        var query = new ManifestSearchQuery { Keywords = "Shabbat", KeywordMatchMode = KeywordMatchMode.Any };

        // Act
        var result = index.Search(query);

        // Assert
        Assert.AreEqual(2, result.TotalMatches);
        Assert.AreEqual("Shabbat", result.Hits[0].Document.FileTitle);
        Assert.IsTrue(result.Hits[0].Score > result.Hits[1].Score);
        CollectionAssert.Contains(result.Hits[0].MatchedFields.ToArray(), "fileTitle");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Search_ExactTitleAndLongerContainingTitle_RanksExactTitleFirst()
    {
        // Arrange
        var exactTitle = TestManifestFactory.CreateDocument(title: "Shabbat", filePath: "Data/Normalized/Shabbat.md", rawFilePath: "Data/Raw/Shabbat.json");
        var containingTitle = TestManifestFactory.CreateDocument(title: "Jerusalem Talmud Shabbat", filePath: "Data/Normalized/Jerusalem-Shabbat.md", rawFilePath: "Data/Raw/Jerusalem-Shabbat.json");
        var exactTitleIndex = ManifestSearchIndex.Create(TestManifestFactory.CreateManifest(containingTitle, exactTitle));

        // Act
        var result = exactTitleIndex.Search(new ManifestSearchQuery { Keywords = "Shabbat" });

        // Assert
        Assert.AreEqual(2, result.TotalMatches);
        Assert.AreEqual("Shabbat", result.Hits[0].Document.FileTitle);
        Assert.IsTrue(result.Hits[0].Score > result.Hits[1].Score);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Search_AllAndAnyModes_ApplyExpectedKeywordLogic()
    {
        // Arrange
        var allQuery = new ManifestSearchQuery { Keywords = "Shabbat creation", KeywordMatchMode = KeywordMatchMode.All };
        var anyQuery = allQuery with { KeywordMatchMode = KeywordMatchMode.Any };

        // Act
        var allResult = index.Search(allQuery);
        var anyResult = index.Search(anyQuery);

        // Assert
        Assert.AreEqual(0, allResult.TotalMatches);
        Assert.AreEqual(3, anyResult.TotalMatches);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Search_CombinedFacets_UsesOrWithinAndAcrossGroups()
    {
        // Arrange
        var query = new ManifestSearchQuery
        {
            Languages = new[] { "en", "Hebrew" },
            Collections = new[] { "Talmud", "Mishnah" },
            Categories = new[] { "Seder Moed", "Seder Zeraim" },
            LicenseStatuses = new[] { "permissive", "noncommercial" },
        };

        // Act
        var result = index.Search(query);

        // Assert
        Assert.AreEqual(2, result.TotalMatches);
        CollectionAssert.AreEquivalent(new[] { "Shabbat", "Bérakhot" }, result.Hits.Select(hit => hit.Document.FileTitle).ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Search_FullCategoryPathAndSegmentBounds_ReturnsMatchingDocument()
    {
        // Arrange
        var query = new ManifestSearchQuery
        {
            Categories = new[] { "Talmud > Bavli > Seder Moed" },
            MinimumSegmentCount = 200,
            MaximumSegmentCount = 300,
        };

        // Act
        var result = index.Search(query);

        // Assert
        Assert.AreEqual(1, result.TotalMatches);
        Assert.AreEqual("Shabbat", result.Hits[0].Document.FileTitle);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Search_Pagination_ReturnsStableRequestedPage()
    {
        // Arrange
        var firstQuery = new ManifestSearchQuery { Skip = 0, Limit = 2 };
        var secondQuery = new ManifestSearchQuery { Skip = 2, Limit = 2 };

        // Act
        var first = index.Search(firstQuery);
        var second = index.Search(secondQuery);

        // Assert
        Assert.AreEqual(4, first.TotalMatches);
        Assert.AreEqual(2, first.Hits.Count);
        Assert.AreEqual(2, second.Hits.Count);
        CollectionAssert.AreEqual(new[] { "Bérakhot", "Genesis" }, first.Hits.Select(hit => hit.Document.FileTitle).ToArray());
        Assert.AreEqual("Genesis", second.Hits[0].Document.FileTitle);
        Assert.AreEqual("Shabbat", second.Hits[1].Document.FileTitle);
    }

    [TestMethod]
    [DataRow(-1, 25)]
    [DataRow(0, 0)]
    [DataRow(0, 201)]
    [TestCategory("Unit")]
    public void Search_InvalidPagination_ThrowsArgumentOutOfRangeException(int skip, int limit)
    {
        // Arrange
        var query = new ManifestSearchQuery { Skip = skip, Limit = limit };

        // Act
        void Act() => index.Search(query);

        // Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(Act);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetFacets_CurrentDocuments_ReturnsExpectedCounts()
    {
        // Act
        var facets = index.GetFacets();

        // Assert
        Assert.AreEqual(2, facets.Languages["English"]);
        Assert.AreEqual(2, facets.Languages["Hebrew"]);
        Assert.AreEqual(2, facets.Collections["Torah"]);
        Assert.AreEqual(1, facets.LicenseStatuses["noncommercial"]);
        Assert.AreEqual(1, facets.Categories["Talmud > Bavli > Seder Moed"]);
    }
}
