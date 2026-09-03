using AskARabbiLIB.Accounts;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.DvarTorah;
using AskARabbiLIB.Usage;

namespace AskARabbi.Api.Development;

/// <summary>Stores local development data in process memory without replacing production persistence.</summary>
public sealed class LocalDevelopmentApplicationStore : IUserAccountStore, IConversationStore, IConversationSettingsStore, IUsageStore, IWeeklyDvarTorahStore
{
    private static readonly Guid StableUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly object synchronization = new();
    private readonly Dictionary<Guid, Conversation> conversations = [];
    private readonly Dictionary<Guid, PersonalizationSettings> personalization = [];
    private readonly Dictionary<Guid, ConversationPreferences> preferences = [];
    private readonly Dictionary<(Guid UserId, DateTimeOffset PeriodStartUtc), int> answerCounts = [];
    private readonly IReadOnlyList<WeeklyDvarTorahArticle> weeklyDvarTorahs = CreateWeeklyDvarTorahs();
    private UserAccount? account;

    /// <inheritdoc/>
    public Task<UserAccount> UpsertAsync(ExternalUserIdentity identity, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();

        lock (synchronization)
        {
            account = new UserAccount
            {
                Id = StableUserId,
                ProviderUserId = identity.ProviderUserId,
                Email = identity.Email,
                IsEmailVerified = identity.IsEmailVerified,
                FirstName = identity.FirstName,
                LastName = identity.LastName,
                ProfileImageUrl = identity.ProfileImageUrl,
                CreatedAtUtc = account?.CreatedAtUtc ?? updatedAtUtc,
                UpdatedAtUtc = updatedAtUtc,
            };
            return Task.FromResult(account);
        }
    }

    /// <inheritdoc/>
    public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            return Task.FromResult(account?.Id == userId ? account : null);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ConversationSummary>> ListAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            var values = conversations.Values
                .Where(conversation => conversation.UserId == userId)
                .OrderByDescending(conversation => conversation.UpdatedAtUtc)
                .Take(limit)
                .Select(conversation => new ConversationSummary(conversation.Id, conversation.Title, conversation.EnabledSourceKeys, conversation.UpdatedAtUtc))
                .ToArray();
            return Task.FromResult<IReadOnlyList<ConversationSummary>>(values);
        }
    }

    /// <inheritdoc/>
    public Task<Conversation?> GetAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            conversations.TryGetValue(conversationId, out var conversation);
            return Task.FromResult(conversation?.UserId == userId ? conversation : null);
        }
    }

    /// <inheritdoc/>
    public Task CreateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            conversations.Add(conversation.Id, conversation);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Conversation?> AppendMessageAsync(Guid userId, Guid conversationId, ConversationMessage message, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            if (!conversations.TryGetValue(conversationId, out var conversation) || conversation.UserId != userId)
            {
                return Task.FromResult<Conversation?>(null);
            }

            if (conversation.Messages.All(existing => existing.Id != message.Id))
            {
                conversation = conversation with { Messages = [.. conversation.Messages, message], UpdatedAtUtc = updatedAtUtc };
                conversations[conversationId] = conversation;
            }

            return Task.FromResult<Conversation?>(conversation);
        }
    }

    /// <inheritdoc/>
    public Task<bool> RenameAsync(Guid userId, Guid conversationId, string title, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            if (!conversations.TryGetValue(conversationId, out var conversation) || conversation.UserId != userId)
            {
                return Task.FromResult(false);
            }

            conversations[conversationId] = conversation with { Title = title, UpdatedAtUtc = updatedAtUtc };
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public Task<bool> UpdateSourcesAsync(Guid userId, Guid conversationId, IReadOnlyList<string> sourceKeys, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            if (!conversations.TryGetValue(conversationId, out var conversation) || conversation.UserId != userId)
            {
                return Task.FromResult(false);
            }

            conversations[conversationId] = conversation with { EnabledSourceKeys = sourceKeys.ToArray(), UpdatedAtUtc = updatedAtUtc };
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            if (!conversations.TryGetValue(conversationId, out var conversation) || conversation.UserId != userId)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(conversations.Remove(conversationId));
        }
    }

    /// <inheritdoc/>
    public Task<PersonalizationSettings?> GetPersonalizationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            personalization.TryGetValue(userId, out var value);
            return Task.FromResult(value);
        }
    }

    /// <inheritdoc/>
    public Task UpsertPersonalizationAsync(Guid userId, PersonalizationSettings value, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            personalization[userId] = value;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ConversationPreferences?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            preferences.TryGetValue(userId, out var value);
            return Task.FromResult(value);
        }
    }

    /// <inheritdoc/>
    public Task UpsertPreferencesAsync(Guid userId, ConversationPreferences value, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            preferences[userId] = value;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<int> GetAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            answerCounts.TryGetValue((userId, periodStartUtc), out var value);
            return Task.FromResult(value);
        }
    }

    /// <inheritdoc/>
    public Task<int> IncrementAnswerCountAsync(Guid userId, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            var key = (userId, periodStartUtc);
            answerCounts.TryGetValue(key, out var value);
            value++;
            answerCounts[key] = value;
            return Task.FromResult(value);
        }
    }

    /// <inheritdoc/>
    public Task<WeeklyDvarTorahArticle?> GetPublishedAsync(WeeklyDvarTorahWeek week, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(week);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(weeklyDvarTorahs.FirstOrDefault(article => article.Week.WeekKey == week.WeekKey));
    }

    /// <inheritdoc/>
    public Task<WeeklyDvarTorahArticle?> GetLatestPublishedAsync(bool inIsrael, DateOnly notAfter, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var article = weeklyDvarTorahs.Where(candidate => candidate.Week.InIsrael == inIsrael && candidate.Week.ShabbatDate <= notAfter).OrderByDescending(candidate => candidate.Week.ShabbatDate).FirstOrDefault();
        return Task.FromResult(article);
    }

    /// <inheritdoc/>
    public Task<WeeklyDvarTorahArticle?> GetPublishedByWeekKeyAsync(string weekKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(weeklyDvarTorahs.FirstOrDefault(article => article.Week.WeekKey == weekKey));
    }

    /// <inheritdoc/>
    public Task<WeeklyDvarTorahArchiveResult> SearchPublishedAsync(bool inIsrael, DateOnly before, string? search, int skip, int limit, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidates = weeklyDvarTorahs
            .Where(article => article.Week.InIsrael == inIsrael && article.Week.ShabbatDate < before)
            .Where(article => MatchesWeeklyDvarTorahSearch(article, search))
            .OrderByDescending(article => article.Week.ShabbatDate)
            .ToArray();
        var items = candidates.Skip(skip).Take(limit).Select(article => new WeeklyDvarTorahArchiveItem(article.Week, article.Title, article.Metadata?.Tags.Take(3).ToArray() ?? [], article.PublishedAtUtc)).ToArray();
        return Task.FromResult(new WeeklyDvarTorahArchiveResult(items, candidates.LongLength));
    }

    private static bool MatchesWeeklyDvarTorahSearch(WeeklyDvarTorahArticle article, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var values = new[] { article.Title, article.Week.HebrewDate, article.Week.Parashah, article.Week.Holiday, article.Week.ShabbatDate.ToString("yyyy-MM-dd") }
            .Concat(article.Metadata?.Tags ?? []);
        return values.Any(value => value?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true);
    }

    private static IReadOnlyList<WeeklyDvarTorahArticle> CreateWeeklyDvarTorahs()
    {
        (DateOnly Date, string HebrewDate, string Parashah, string Title, string[] Tags)[] publications =
        [
            (new DateOnly(2026, 9, 5), "23 Elul, 5786", "Nitzavim", "Standing Together at the Threshold", ["responsibility", "community", "renewal"]),
            (new DateOnly(2026, 8, 29), "16 Elul, 5786", "Ki Tavo", "Gratitude That Becomes Responsibility", ["gratitude", "responsibility", "community"]),
            (new DateOnly(2026, 8, 22), "9 Elul, 5786", "Ki Teitzei", "The Dignity Hidden in Daily Choices", ["dignity", "ethics", "daily life"]),
            (new DateOnly(2026, 8, 15), "2 Elul, 5786", "Shoftim", "Justice Begins Close to Home", ["justice", "leadership", "community"]),
            (new DateOnly(2026, 8, 8), "25 Av, 5786", "Re'eh", "Learning to See the Choice Before Us", ["choice", "blessing", "attention"]),
            (new DateOnly(2026, 8, 1), "18 Av, 5786", "Eikev", "The Quiet Work of Listening", ["listening", "covenant", "practice"]),
            (new DateOnly(2026, 7, 25), "11 Av, 5786", "Va'etchanan", "Love Expressed Through Practice", ["love", "prayer", "practice"]),
            (new DateOnly(2026, 7, 18), "4 Av, 5786", "Devarim", "The Courage to Retell Our Story", ["memory", "leadership", "truth"]),
            (new DateOnly(2026, 7, 11), "26 Tammuz, 5786", "Matot-Masei", "Promises, Journeys, and Responsibility", ["promises", "journey", "responsibility"]),
            (new DateOnly(2026, 7, 4), "19 Tammuz, 5786", "Pinchas", "Zeal Tempered by Covenant", ["covenant", "leadership", "peace"]),
            (new DateOnly(2026, 6, 27), "12 Tammuz, 5786", "Balak", "Blessing Beyond Our Control", ["blessing", "speech", "humility"]),
            (new DateOnly(2026, 6, 20), "5 Tammuz, 5786", "Chukat", "Living with Questions We Cannot Resolve", ["mystery", "faith", "grief"]),
        ];

        return publications.Select(publication => CreateWeeklyDvarTorah(publication.Date, publication.HebrewDate, publication.Parashah, publication.Title, publication.Tags)).ToArray();
    }

    private static WeeklyDvarTorahArticle CreateWeeklyDvarTorah(DateOnly shabbatDate, string hebrewDate, string parashah, string title, IReadOnlyList<string> tags)
    {
        var publishedAtUtc = new DateTimeOffset(shabbatDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc)).AddDays(-4);
        var week = new WeeklyDvarTorahWeek(shabbatDate, hebrewDate, parashah, null, false);
        var source = new WeeklyDvarTorahSource("T1", WeeklyDvarTorahSourceKind.Torah, "Deuteronomy", "Demo study edition", "https://www.sefaria.org/texts/Tanakh/Torah/Deuteronomy", "This local demonstration excerpt represents the weekly Torah reading.", publishedAtUtc.AddDays(-1), $"Parashat {parashah}", license: "CC-BY-NC");
        var metadata = new WeeklyDvarTorahContentMetadata($"Parashat {parashah} invites deliberate, compassionate responsibility.", tags, [source], 100, "local-demo-v1", "local-demo", publishedAtUtc.AddDays(-7), publishedAtUtc);
        var body = $"Parashat {parashah} asks us to notice how enduring values become daily actions [T1].\n\n{title} is an invitation to carry study beyond the page: to listen carefully, act with dignity, and strengthen the communities that depend on us.";
        return new WeeklyDvarTorahArticle(week, title, body, "local-demo-v1", publishedAtUtc, publishedAtUtc, metadata);
    }
}
