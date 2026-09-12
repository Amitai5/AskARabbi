using AskARabbiLIB.AI.Tools;
using AskARabbiLIB.Lexicon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class BdbRegistrationTests
{
    [TestMethod]
    public async Task Startup_BdbResearch_IsRegisteredAlongsideCalendarWithoutDatabaseStartupWrites()
    {
        await using var app = new TestApplicationFactory();
        using var client = app.CreateClient();

        var registry = app.Services.GetRequiredService<IAIToolRegistry>();

        CollectionAssert.AreEquivalent(new[] { "search_bdb_dictionary", "read_bdb_entry", "convert_birthdate_to_hebrew", "find_parashah_for_week", "get_today_as_hebrew_and_gregorian" }, registry.Definitions.Select(definition => definition.Name).ToArray());
        Assert.IsInstanceOfType<UnavailableLexiconStore>(app.Services.GetRequiredService<ILexiconStore>());
    }
}
