using AskARabbiLIB.Conversations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class ConversationServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 30, 0, TimeSpan.Zero);

    [TestMethod]
    [TestCategory("Unit")]
    public async Task CreateAsync_OmittedValues_CreatesDefaultConversationAtCurrentTime()
    {
        var store = new FakeConversationStore();
        var service = new ConversationService(store, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(UserId, null, null);

        Assert.AreEqual("New conversation", result.Title);
        CollectionAssert.AreEqual(ConversationSourceCatalog.All.ToArray(), result.EnabledSourceKeys.ToArray());
        Assert.AreEqual(Now, result.CreatedAtUtc);
        Assert.AreSame(result, store.Created);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task CreateAsync_DuplicateSources_NormalizesSelection()
    {
        var service = new ConversationService(new FakeConversationStore(), new FixedTimeProvider(Now));

        var result = await service.CreateAsync(UserId, "  Study  ", ["collection:Torah", "collection:Torah", "collection:Talmud"]);

        Assert.AreEqual("Study", result.Title);
        CollectionAssert.AreEqual(new[] { "collection:Torah", "collection:Talmud" }, result.EnabledSourceKeys.ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task CreateAsync_UnsupportedSource_ThrowsWithoutWriting()
    {
        var store = new FakeConversationStore();
        var service = new ConversationService(store);

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.CreateAsync(UserId, null, ["collection:NotApproved"]));

        StringAssert.Contains(exception.Message, "Unsupported source selector");
        Assert.IsNull(store.Created);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(101)]
    [TestCategory("Unit")]
    public async Task ListAsync_LimitOutsideRange_Throws(int limit)
    {
        var service = new ConversationService(new FakeConversationStore());

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => service.ListAsync(UserId, limit));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task AppendUserMessageAsync_ValidMessage_TrimsAndUsesClientId()
    {
        var store = new FakeConversationStore { AppendResult = CreateConversation() };
        var service = new ConversationService(store, new FixedTimeProvider(Now));
        var messageId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var result = await service.AppendUserMessageAsync(UserId, store.AppendResult.Id, messageId, "  What does this mean?  ");

        Assert.AreSame(store.AppendResult, result);
        Assert.IsNotNull(store.Appended);
        Assert.AreEqual(messageId, store.Appended.Id);
        Assert.AreEqual(ConversationMessageRole.User, store.Appended.Role);
        Assert.AreEqual("What does this mean?", store.Appended.Content);
        Assert.AreEqual(Now, store.Appended.CreatedAtUtc);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [TestCategory("Unit")]
    public async Task AppendUserMessageAsync_BlankMessage_Throws(string content)
    {
        var service = new ConversationService(new FakeConversationStore());

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.AppendUserMessageAsync(UserId, Guid.NewGuid(), Guid.NewGuid(), content));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task AppendUserMessageAsync_EmptyMessageId_Throws()
    {
        var service = new ConversationService(new FakeConversationStore());

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.AppendUserMessageAsync(UserId, Guid.NewGuid(), Guid.Empty, "Question"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RenameAsync_ValidTitle_NormalizesAndDelegates()
    {
        var store = new FakeConversationStore { MutationResult = true };
        var service = new ConversationService(store, new FixedTimeProvider(Now));
        var conversationId = Guid.NewGuid();

        var updated = await service.RenameAsync(UserId, conversationId, "  New name  ");

        Assert.IsTrue(updated);
        Assert.AreEqual("New name", store.RenamedTitle);
        Assert.AreEqual(Now, store.MutationTime);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task UpdateSourcesAsync_EmptySelection_Throws()
    {
        var service = new ConversationService(new FakeConversationStore());

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.UpdateSourcesAsync(UserId, Guid.NewGuid(), []));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task DeleteAsync_ValidIds_DelegatesOwnerIds()
    {
        var store = new FakeConversationStore { MutationResult = true };
        var service = new ConversationService(store);
        var conversationId = Guid.NewGuid();

        var deleted = await service.DeleteAsync(UserId, conversationId);

        Assert.IsTrue(deleted);
        Assert.AreEqual(UserId, store.LastUserId);
        Assert.AreEqual(conversationId, store.LastConversationId);
    }

    private static Conversation CreateConversation() => new()
    {
        Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        UserId = UserId,
        Title = "Study",
        EnabledSourceKeys = ["collection:Torah"],
        Messages = [],
        CreatedAtUtc = Now,
        UpdatedAtUtc = Now,
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

    private sealed class FakeConversationStore : IConversationStore
    {
        internal Conversation? Created { get; private set; }
        internal Conversation? AppendResult { get; init; }
        internal ConversationMessage? Appended { get; private set; }
        internal string? RenamedTitle { get; private set; }
        internal DateTimeOffset MutationTime { get; private set; }
        internal bool MutationResult { get; init; }
        internal Guid LastUserId { get; private set; }
        internal Guid LastConversationId { get; private set; }

        public Task<IReadOnlyList<ConversationSummary>> ListAsync(Guid userId, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ConversationSummary>>([]);

        public Task<Conversation?> GetAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default) => Task.FromResult<Conversation?>(AppendResult);

        public Task CreateAsync(Conversation conversation, CancellationToken cancellationToken = default)
        {
            Created = conversation;
            return Task.CompletedTask;
        }

        public Task<Conversation?> AppendMessageAsync(Guid userId, Guid conversationId, ConversationMessage message, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
        {
            Appended = message;
            return Task.FromResult(AppendResult);
        }

        public Task<bool> RenameAsync(Guid userId, Guid conversationId, string title, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
        {
            RenamedTitle = title;
            MutationTime = updatedAtUtc;
            return Task.FromResult(MutationResult);
        }

        public Task<bool> UpdateSourcesAsync(Guid userId, Guid conversationId, IReadOnlyList<string> sourceKeys, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
        {
            MutationTime = updatedAtUtc;
            return Task.FromResult(MutationResult);
        }

        public Task<bool> DeleteAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastConversationId = conversationId;
            return Task.FromResult(MutationResult);
        }
    }
}
