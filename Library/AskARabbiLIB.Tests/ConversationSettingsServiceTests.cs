using AskARabbiLIB.ConversationSettings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class ConversationSettingsServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 30, 0, TimeSpan.Zero);

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetPersonalizationAsync_ConfiguredUser_ReturnsStoredValue()
    {
        var expected = CreateValidSettings();
        var store = new FakeSettingsStore { Stored = expected };
        var service = new ConversationSettingsService(store);

        var result = await service.GetPersonalizationAsync(UserId);

        Assert.AreSame(expected, result);
        Assert.AreEqual(UserId, store.LastUserId);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task UpdatePersonalizationAsync_ValidSettings_NormalizesAndPersistsAtCurrentTime()
    {
        var store = new FakeSettingsStore();
        var service = new ConversationSettingsService(store, new FixedTimeProvider(Now));
        var value = CreateValidSettings() with
        {
            FullName = "  Amitai Erfanian  ",
            AdditionalContext = "  Builds thoughtful software.  ",
        };

        var result = await service.UpdatePersonalizationAsync(UserId, value);

        Assert.AreEqual("Amitai Erfanian", result.FullName);
        Assert.AreEqual("Builds thoughtful software.", result.AdditionalContext);
        Assert.AreSame(result, store.Stored);
        Assert.AreEqual(Now, store.UpdatedAtUtc);
    }

    [TestMethod]
    [DataRow("BirthTimeZone")]
    [DataRow("ConversationLanguage")]
    [DataRow("QuotationLanguage")]
    [TestCategory("Unit")]
    public async Task UpdatePersonalizationAsync_UnsupportedCatalogValue_ThrowsWithoutWriting(string property)
    {
        var store = new FakeSettingsStore();
        var service = new ConversationSettingsService(store, new FixedTimeProvider(Now));
        var value = property switch
        {
            "BirthTimeZone" => CreateValidSettings() with { BirthTimeZone = "Europe/London" },
            "ConversationLanguage" => CreateValidSettings() with { ConversationLanguage = "Klingon" },
            _ => CreateValidSettings() with { QuotationLanguage = "Klingon" },
        };

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.UpdatePersonalizationAsync(UserId, value));

        Assert.IsNull(store.Stored);
    }

    [TestMethod]
    [DataRow(2026, 8, 26)]
    [DataRow(1896, 8, 24)]
    [TestCategory("Unit")]
    public async Task UpdatePersonalizationAsync_BirthDateOutsideSupportedAgeRange_Throws(int year, int month, int day)
    {
        var service = new ConversationSettingsService(new FakeSettingsStore(), new FixedTimeProvider(Now));
        var value = CreateValidSettings() with { BirthDate = new DateOnly(year, month, day) };

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => service.UpdatePersonalizationAsync(UserId, value));
    }

    [TestMethod]
    [DataRow("FullName")]
    [DataRow("ReligiousMovement")]
    [DataRow("JewishHeritage")]
    [TestCategory("Unit")]
    public async Task UpdatePersonalizationAsync_RequiredTextIsBlank_Throws(string property)
    {
        var service = new ConversationSettingsService(new FakeSettingsStore(), new FixedTimeProvider(Now));
        var value = property switch
        {
            "FullName" => CreateValidSettings() with { FullName = " " },
            "ReligiousMovement" => CreateValidSettings() with { ReligiousMovement = " " },
            _ => CreateValidSettings() with { JewishHeritage = " " },
        };

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.UpdatePersonalizationAsync(UserId, value));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task UpdatePersonalizationAsync_AdditionalContextTooLong_Throws()
    {
        var service = new ConversationSettingsService(new FakeSettingsStore(), new FixedTimeProvider(Now));
        var value = CreateValidSettings() with { AdditionalContext = new string('a', 2_001) };

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.UpdatePersonalizationAsync(UserId, value));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task UpdatePersonalizationAsync_EmptyUserId_Throws()
    {
        var service = new ConversationSettingsService(new FakeSettingsStore());

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.UpdatePersonalizationAsync(Guid.Empty, CreateValidSettings()));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetPreferencesAsync_NoStoredValue_ReturnsProductDefaults()
    {
        var service = new ConversationSettingsService(new FakeSettingsStore());

        var result = await service.GetPreferencesAsync(UserId);

        Assert.IsFalse(result.ShowSourceContextByDefault);
        Assert.IsFalse(result.EmailProductUpdates);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task UpdatePreferencesAsync_ValidPreferences_PersistsAtCurrentTime()
    {
        var store = new FakeSettingsStore();
        var service = new ConversationSettingsService(store, new FixedTimeProvider(Now));
        var preferences = new ConversationPreferences { ShowSourceContextByDefault = true, EmailProductUpdates = true };

        var result = await service.UpdatePreferencesAsync(UserId, preferences);

        Assert.AreSame(preferences, result);
        Assert.AreSame(preferences, store.Preferences);
        Assert.AreEqual(Now, store.UpdatedAtUtc);
    }

    private static PersonalizationSettings CreateValidSettings() => new()
    {
        FullName = "Amitai Erfanian",
        BirthDate = new DateOnly(2001, 12, 17),
        BirthTime = new TimeOnly(15, 30),
        BirthTimeZone = "America/Los_Angeles",
        ConversationLanguage = "English",
        QuotationLanguage = "Hebrew",
        ReligiousMovement = "Between Modern Orthodox and Conservative",
        JewishHeritage = "Mizrahi (Iranian)",
        AdditionalContext = null,
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        internal FixedTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeSettingsStore : IConversationSettingsStore
    {
        internal PersonalizationSettings? Stored { get; set; }
        internal DateTimeOffset UpdatedAtUtc { get; private set; }
        internal Guid LastUserId { get; private set; }
        internal ConversationPreferences? Preferences { get; private set; }

        public Task<PersonalizationSettings?> GetPersonalizationAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            return Task.FromResult(Stored);
        }

        public Task UpsertPersonalizationAsync(Guid userId, PersonalizationSettings personalization, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            Stored = personalization;
            UpdatedAtUtc = updatedAtUtc;
            return Task.CompletedTask;
        }

        public Task<ConversationPreferences?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            return Task.FromResult(Preferences);
        }

        public Task UpsertPreferencesAsync(Guid userId, ConversationPreferences preferences, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            Preferences = preferences;
            UpdatedAtUtc = updatedAtUtc;
            return Task.CompletedTask;
        }
    }
}
