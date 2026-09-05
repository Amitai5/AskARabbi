using System.Net;
using AskARabbi.Api.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkOS;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class WorkOsAccountDeletionTests
{
    [TestMethod]
    [DataRow(HttpStatusCode.NoContent)]
    [DataRow(HttpStatusCode.NotFound)]
    public async Task DeleteUser_SuccessOrAlreadyDeleted_CompletesIdempotently(HttpStatusCode status)
    {
        using var handler = new RecordingHandler(status);
        using var client = new HttpClient(handler);
        var service = CreateService(client);

        await service.DeleteUserAsync("user_owned");

        Assert.AreEqual(HttpMethod.Delete, handler.Method);
        Assert.AreEqual("/user_management/users/user_owned", handler.Path);
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Forbidden)]
    [DataRow(HttpStatusCode.TooManyRequests)]
    [DataRow(HttpStatusCode.InternalServerError)]
    public async Task DeleteUser_ProviderFailure_DoesNotPretendDeletionSucceeded(HttpStatusCode status)
    {
        using var handler = new RecordingHandler(status);
        using var client = new HttpClient(handler);
        var service = CreateService(client);

        await Assert.ThrowsAsync<IdentityProviderUnavailableException>(() => service.DeleteUserAsync("user_owned"));

        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task UserExists_DeletedIdentity_ReturnsFalseWithoutRegisteringIt()
    {
        using var handler = new RecordingHandler(HttpStatusCode.NotFound);
        using var client = new HttpClient(handler);

        var exists = await CreateService(client).UserExistsAsync("user_deleted");

        Assert.IsFalse(exists);
        Assert.AreEqual(HttpMethod.Get, handler.Method);
    }

    private static WorkOsUserAuthenticationService CreateService(HttpClient client) => new(new WorkOSClient(new WorkOSOptions { ApiKey = "test-key", ClientId = "client_test", HttpClient = client, MaxRetries = 0 }), new WorkOsAuthenticationOptions { ApiKey = "test-key", ClientId = "client_test" });

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        internal HttpMethod? Method { get; private set; }
        internal string? Path { get; private set; }
        internal int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath;
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{\"message\":\"Test provider response\"}") });
        }
    }
}
