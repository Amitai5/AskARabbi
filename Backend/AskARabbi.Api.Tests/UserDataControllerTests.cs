using System.Net;
using System.Net.Http.Json;
using AskARabbi.Api.Accounts;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class UserDataControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 30, 0, TimeSpan.Zero);
    private static readonly Guid OtherUser = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OperationId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [TestMethod]
    [DataRow("chats", "DELETE ALL CHATS")]
    [DataRow("account", "DELETE ACCOUNT")]
    public async Task Delete_AnonymousCaller_Rejects(string resource, string confirmation)
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateNonRedirectingClient();

        using var response = await DeleteAsync(client, resource, confirmation);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.IsNull(application.Authentication.DeletedProviderUserId);
    }

    [TestMethod]
    [DataRow("chats", null)]
    [DataRow("chats", "DELETE ACCOUNT")]
    [DataRow("account", null)]
    [DataRow("account", "DELETE ALL CHATS")]
    public async Task Delete_MissingOrWrongConfirmation_KeepsData(string resource, string? confirmation)
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        await SeedAsync(application.Store, application.Store.UserId, 1);

        using var response = await DeleteAsync(client, resource, confirmation);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.HasCount(1, await application.Store.ListAsync(application.Store.UserId, 100));
        Assert.IsNull(application.Authentication.DeletedProviderUserId);
    }

    [TestMethod]
    public async Task DeleteChats_MoreThanSidebarLimit_DeletesEveryOwnedChatAndKeepsUsageAndSettings()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        var userId = application.Store.UserId;
        await SeedAsync(application.Store, userId, 70);
        var usageLease = new AskARabbiLIB.Usage.ChatUsageLease(userId, OperationId, Now, Now.AddMonths(1), Now.AddMinutes(10), 10_000_000);
        Assert.IsTrue(await application.Store.TryAcquireChatAsync(usageLease, Now));
        Assert.IsTrue(await application.Store.RecordTokensAsync(usageLease, 7_000));
        await application.Store.ReleaseChatAsync(usageLease);
        await SeedAsync(application.Store, OtherUser, 2);
        await application.Store.UpsertPreferencesAsync(userId, new ConversationPreferences { EmailProductUpdates = true }, Now);

        using var response = await DeleteAsync(client, "chats", "DELETE ALL CHATS");
        using var repeated = await DeleteAsync(client, "chats", "DELETE ALL CHATS");

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.HasCount(0, await application.Store.ListAsync(userId, 100));
        Assert.HasCount(2, await application.Store.ListAsync(OtherUser, 100));
        Assert.IsNotNull(await application.Store.GetByIdAsync(userId));
        Assert.IsTrue((await application.Store.GetPreferencesAsync(userId))?.EmailProductUpdates);
        Assert.AreEqual(7_000L, await application.Store.GetTokenCountAsync(userId, Now, Now.AddMonths(1)));
        Assert.IsNull(application.Authentication.DeletedProviderUserId);
    }

    [TestMethod]
    public async Task DeleteAccount_Confirmed_ErasesOwnedDataAndRejectsOtherBrowserSession()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        using var secondBrowser = await application.CreateAuthenticatedClientAsync();
        var userId = application.Store.UserId;
        await SeedAsync(application.Store, userId, 3);
        await SeedAsync(application.Store, OtherUser, 1);
        await application.Store.UpsertPreferencesAsync(userId, new ConversationPreferences { EmailProductUpdates = true }, Now);
        await application.Store.UpsertPersonalizationAsync(userId, new PersonalizationSettings { FullName = "Personal information", BirthDate = new(2000, 1, 1), BirthTime = new(12, 0), BirthTimeZone = "America/New_York", ConversationLanguage = "English", QuotationLanguage = "English", ReligiousMovement = "Traditional", JewishHeritage = "Mizrahi" }, Now);

        using var response = await DeleteAsync(client, "account", "DELETE ACCOUNT");
        using var staleSession = await secondBrowser.GetAsync("/api/conversations");
        using var staleWrite = await secondBrowser.PostAsJsonAsync("/api/conversations", new { content = "Should not be stored", messageId = OperationId, enabledSourceKeys = new[] { "collection:Torah" } });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "deleted");
        Assert.AreEqual("user_workos_01", application.Authentication.DeletedProviderUserId);
        Assert.IsNull(await application.Store.GetByIdAsync(userId));
        Assert.IsNull(await application.Store.GetPreferencesAsync(userId));
        Assert.IsNull(await application.Store.GetPersonalizationAsync(userId));
        Assert.AreEqual(0L, await application.Store.GetTokenCountAsync(userId, Now, Now));
        Assert.HasCount(0, await application.Store.ListAsync(userId, 100));
        Assert.HasCount(1, await application.Store.ListAsync(OtherUser, 100));
        Assert.AreEqual(HttpStatusCode.Unauthorized, staleSession.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, staleWrite.StatusCode);
    }

    [TestMethod]
    [DataRow("chats", "DELETE ALL CHATS")]
    [DataRow("account", "DELETE ACCOUNT")]
    public async Task Delete_ActiveWrite_RejectsUntilWriteFinishes(string resource, string confirmation)
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        var userId = application.Store.UserId;
        var lease = Guid.Parse("44444444-4444-4444-4444-444444444444");
        Assert.IsTrue(await application.Store.TryAcquireAsync(userId, lease, false, Now, Now.AddMinutes(30)));

        using var blocked = await DeleteAsync(client, resource, confirmation);
        Assert.AreEqual(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.IsNull(application.Authentication.DeletedProviderUserId);
        await application.Store.ReleaseAsync(userId, lease);
        using var successful = await DeleteAsync(client, resource, confirmation);

        Assert.IsTrue(successful.IsSuccessStatusCode);
    }

    [TestMethod]
    public async Task DeleteAccount_ProviderUnavailable_DisablesSessionsAndDurablyRetries()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        using var secondBrowser = await application.CreateAuthenticatedClientAsync();
        var userId = application.Store.UserId;
        await SeedAsync(application.Store, userId, 1);
        application.Authentication.FailDeletion = true;

        using var response = await DeleteAsync(client, "account", "DELETE ACCOUNT");
        using var staleSession = await secondBrowser.GetAsync("/api/user/session");
        var pending = await application.Store.ListPendingDeletionsAsync();

        Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, staleSession.StatusCode);
        Assert.HasCount(1, pending);
        Assert.IsFalse(await application.Store.TryAcquireAsync(userId, OperationId, false, Now, Now.AddMinutes(30)));

        application.Authentication.FailDeletion = false;
        using var scope = application.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountDeletionService>();
        Assert.IsTrue(await service.TryCompleteAsync(pending[0], CancellationToken.None));
        Assert.IsTrue(await service.TryCompleteAsync(pending[0], CancellationToken.None));
        Assert.HasCount(0, await application.Store.ListPendingDeletionsAsync());
        Assert.IsNull(await application.Store.GetByIdAsync(userId));
        Assert.HasCount(0, await application.Store.ListAsync(userId, 100));
    }

    [TestMethod]
    public async Task Write_ExclusiveDeletionLease_BlocksBeforeControllerPersistsAnything()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        var userId = application.Store.UserId;
        Assert.IsTrue(await application.Store.TryAcquireAsync(userId, OperationId, true, Now, Now.AddMinutes(30)));

        using var response = await client.PutAsJsonAsync("/api/conversation-settings/preferences", new { emailProductUpdates = true, showSourceContextByDefault = false });

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.IsNull(await application.Store.GetPreferencesAsync(userId));
    }

    [TestMethod]
    public async Task Write_ValidationFailure_ReleasesLeaseSoDeletionIsPossible()
    {
        await using var application = new TestApplicationFactory();
        using var client = await application.CreateAuthenticatedClientAsync();
        using var invalid = await client.PostAsJsonAsync("/api/conversations", new { content = "" });

        using var response = await DeleteAsync(client, "account", "DELETE ACCOUNT");

        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string resource, string? confirmation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/user/data/{resource}");
        if (confirmation is not null)
        {
            request.Headers.Add("X-Confirm-Deletion", confirmation);
        }
        return await client.SendAsync(request);
    }

    private static async Task SeedAsync(InMemoryApplicationStore store, Guid userId, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var owner = (short)(userId == OtherUser ? 2 : 1);
            var conversationId = new Guid(index + 1, owner, 1, new byte[8]);
            var messageId = new Guid(index + 1, owner, 2, new byte[8]);
            await store.CreateAsync(new Conversation { Id = conversationId, UserId = userId, Title = "Saved chat", EnabledSourceKeys = ["collection:Torah"], Messages = [new ConversationMessage { Id = messageId, Role = ConversationMessageRole.User, Content = "Private message", CreatedAtUtc = Now }], CreatedAtUtc = Now, UpdatedAtUtc = Now });
        }
    }
}
