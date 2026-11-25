using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Options;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class ModelKnowledgeFallbackContractTests
{
    [TestMethod]
    public void Configuration_DefaultAndApplicationSettingsAreDisabled()
    {
        Assert.IsFalse(new RagOptions().AllowModelKnowledgeFallback);
        using var factory = CreateFactory(null);
        Assert.IsFalse(factory.Services.GetRequiredService<IOptions<RagOptions>>().Value.AllowModelKnowledgeFallback);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Configuration_BindsOption(bool enabled)
    {
        using var factory = CreateFactory(enabled);
        Assert.AreEqual(enabled, factory.Services.GetRequiredService<IOptions<RagOptions>>().Value.AllowModelKnowledgeFallback);
    }

    [TestMethod]
    public void Response_WithoutSource_PreservesExistingJsonShape()
    {
        var response = new RagQueryResponse { ConversationId = Guid.Empty, Answer = "answer" };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        CollectionAssert.AreEquivalent(new[] { "conversationId", "answer", "citations" },
            json.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual("answer", json.RootElement.GetProperty("answer").GetString());
        Assert.AreEqual(0, json.RootElement.GetProperty("citations").GetArrayLength());
    }

    [TestMethod]
    public void Response_WithModelKnowledgeSource_SerializesExplicitAttribution()
    {
        var response = new RagQueryResponse { Answer = "Helsinki.", AnswerSource = RagAnswerSource.ModelKnowledge };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.AreEqual("Helsinki.", json.RootElement.GetProperty("answer").GetString());
        Assert.AreEqual("ModelKnowledge", json.RootElement.GetProperty("answerSource").GetString());
        Assert.AreEqual(0, json.RootElement.GetProperty("citations").GetArrayLength());
    }

    private static WebApplicationFactory<Program> CreateFactory(bool? enabled) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ServiceBus:DocumentImportWorkerEnabled"] = "false",
                    ["ApplicationInsights:ConnectionString"] = ""
                };
                if (enabled.HasValue) settings["Rag:AllowModelKnowledgeFallback"] = enabled.Value.ToString();
                configuration.AddInMemoryCollection(settings);
            });
        });
}
