using AskARabbiLIB.Accounts;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.Persistence.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class ProductionDomainTests
{
    [TestMethod]
    [DataRow("Amitai", "Erfanian", "Amitai Erfanian")]
    [DataRow("Amitai", null, "Amitai")]
    [DataRow(null, null, "amitai@example.com")]
    [TestCategory("Unit")]
    public void DisplayName_AvailableIdentityFields_UsesBestSafeValue(string? firstName, string? lastName, string expected)
    {
        var account = new UserAccount
        {
            Id = Guid.NewGuid(),
            ProviderUserId = "user_01",
            Email = "amitai@example.com",
            FirstName = firstName,
            LastName = lastName,
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch,
        };

        Assert.AreEqual(expected, account.DisplayName);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_CompleteMongoOptions_DoesNotThrow()
    {
        var options = new MongoDatabaseOptions { ConnectionString = ValidConnectionString };

        options.Validate();

        Assert.IsTrue(options.IsConfigured);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ToString_MongoOptions_DoesNotExposeConnectionString()
    {
        var options = new MongoDatabaseOptions { ConnectionString = ValidConnectionString };

        var result = options.ToString();

        Assert.IsFalse(result?.Contains(ValidConnectionString, StringComparison.Ordinal) ?? false);
    }

    [TestMethod]
    [DataRow("ConnectionString")]
    [DataRow("DatabaseName")]
    [DataRow("UsersCollectionName")]
    [DataRow("ConversationsCollectionName")]
    [DataRow("ConversationMessagesCollectionName")]
    [DataRow("ConversationSettingsCollectionName")]
    [DataRow("UsageCollectionName")]
    [TestCategory("Unit")]
    public void Validate_MissingMongoValue_ThrowsWithConfigurationKey(string property)
    {
        var options = property switch
        {
            "ConnectionString" => new MongoDatabaseOptions(),
            "DatabaseName" => new MongoDatabaseOptions { ConnectionString = ValidConnectionString, DatabaseName = " " },
            "UsersCollectionName" => new MongoDatabaseOptions { ConnectionString = ValidConnectionString, UsersCollectionName = " " },
            "ConversationsCollectionName" => new MongoDatabaseOptions { ConnectionString = ValidConnectionString, ConversationsCollectionName = " " },
            "ConversationMessagesCollectionName" => new MongoDatabaseOptions { ConnectionString = ValidConnectionString, ConversationMessagesCollectionName = " " },
            "ConversationSettingsCollectionName" => new MongoDatabaseOptions { ConnectionString = ValidConnectionString, ConversationSettingsCollectionName = " " },
            _ => new MongoDatabaseOptions { ConnectionString = ValidConnectionString, UsageCollectionName = " " },
        };

        var exception = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);

        StringAssert.Contains(exception.Message, property);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ConversationSourceCatalog_KnownAndUnknownSelectors_ReturnExpectedResult()
    {
        Assert.IsTrue(ConversationSourceCatalog.Contains("collection:Torah"));
        Assert.IsFalse(ConversationSourceCatalog.Contains("collection:NotApproved"));
        CollectionAssert.AreEqual(new[] { "collection:Torah", "collection:Tanakh", "collection:Mishnah", "collection:Talmud" }, ConversationSourceCatalog.Core.ToArray());
    }

    private const string ValidConnectionString = "mongodb://localhost:27017";
}
