using System.Text.Json;
using AskARabbi.Api.Authentication;
using AskARabbi.Api.Configuration;
using AskARabbiLIB.AI;
using AskARabbiLIB.Grounding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
public sealed class ProductionConfigurationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task ProductionConfiguration_PublicDomainsMatchDeploymentTopology()
    {
        await using var application = new TestApplicationFactory(true, Environments.Production, false, null);
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://api.askarabbi.ai"),
        });
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", "https://askarabbi.ai");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);
        var workOs = application.Services.GetRequiredService<WorkOsAuthenticationOptions>();
        var groundedChat = application.Services.GetRequiredService<GroundedChatOptions>();
        var groundedPrompts = application.Services.GetRequiredService<GroundedPromptSet>();

        Assert.AreEqual("https://api.askarabbi.ai/api/user/callback", workOs.RedirectUri);
        Assert.AreEqual("https://askarabbi.ai/", workOs.FrontendUri);
        Assert.AreEqual(8_000, groundedChat.MaximumOutputTokens);
        Assert.AreEqual(3_200, groundedChat.ValidationMaximumOutputTokens);
        Assert.AreEqual(AIReasoningEffort.Medium, groundedChat.ReasoningEffort);
        Assert.AreEqual(AIServiceTier.Priority, groundedChat.ServiceTier);
        Assert.AreEqual(20, groundedChat.MaximumCandidates);
        Assert.AreEqual(10, groundedChat.MaximumEvidenceSegments);
        Assert.AreEqual(0, groundedChat.MaximumEnrichmentHits);
        Assert.AreEqual(2, groundedChat.RecentConversationTurns);
        Assert.AreEqual(600, groundedChat.RetrievalCacheSeconds);
        Assert.AreEqual("https://askarabbi.ai", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.IsTrue(response.Headers.GetValues("Access-Control-Allow-Credentials").Single().Equals("true", StringComparison.OrdinalIgnoreCase));
        StringAssert.StartsWith(groundedPrompts.CurrentQuestionInstruction, "Write one flowing, human answer.");
        StringAssert.Contains(groundedPrompts.CurrentQuestionInstruction, "Follow answerFocus as a required task definition");
        StringAssert.Contains(groundedPrompts.CurrentQuestionInstruction, "A why-question must explain the evidenced rationale");
        StringAssert.Contains(groundedPrompts.CurrentQuestionInstruction, "independently verifiable proposition");
        StringAssert.Contains(groundedPrompts.ValidationRepairPrompt, "add, remove, or reassign real evidence IDs");
        StringAssert.Contains(groundedPrompts.ValidationRepairPrompt, "never invent an evidence ID");
        StringAssert.Contains(groundedPrompts.ValidationRepairPrompt, "use remaining source-research calls");
        StringAssert.Contains(groundedPrompts.SupportValidationPrompt, "separate support obligation");
        StringAssert.Contains(groundedPrompts.SupportValidationPrompt, "isResponsive");
        StringAssert.Contains(groundedPrompts.SupportValidationPrompt, "stating that a rule is rabbinic does not answer why");
        StringAssert.Contains(groundedPrompts.SystemBehaviorPrompt, "Who was Rabbi Akiva?");
        StringAssert.Contains(groundedPrompts.SystemBehaviorPrompt, "Vampires are fictional");
        StringAssert.Contains(groundedPrompts.SupportValidationPrompt, "The draft's label is not authority to bypass these requirements");
        using var schema = JsonDocument.Parse(groundedPrompts.ResponseJsonSchema);
        var claimSchema = schema.RootElement.GetProperty("properties").GetProperty("claims").GetProperty("items");
        CollectionAssert.AreEqual(new[] { "Source", "Background", "Uncertainty" }, claimSchema.GetProperty("properties").GetProperty("kind").GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ToArray());
        CollectionAssert.Contains(claimSchema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray(), "kind");
    }
}
