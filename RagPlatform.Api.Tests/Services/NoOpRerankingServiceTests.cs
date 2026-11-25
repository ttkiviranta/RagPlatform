using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Options;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class NoOpRerankingServiceTests
{
    [TestMethod]
    public async Task RerankAsync_ReturnsTheSameCandidatesInTheSameOrder()
    {
        var first = new VectorSearchResult
        {
            DocumentId = Guid.NewGuid(), ChunkId = Guid.NewGuid(), SimilarityScore = 0.2, ContentSnippet = "first"
        };
        var second = new VectorSearchResult
        {
            DocumentId = Guid.NewGuid(), ChunkId = Guid.NewGuid(), SimilarityScore = 0.9, ContentSnippet = "second"
        };
        IReadOnlyList<VectorSearchResult> candidates = [first, second];
        var service = new NoOpRerankingService();

        var result = await service.RerankAsync("question", candidates);

        Assert.AreSame(candidates, result);
        Assert.AreEqual(2, result.Count);
        Assert.AreSame(first, result[0]);
        Assert.AreSame(second, result[1]);
        Assert.AreEqual(0.2, result[0].SimilarityScore);
        Assert.AreEqual("second", result[1].ContentSnippet);
    }

    [TestMethod]
    public async Task RerankAsync_EmptyCandidates_ReturnsEmptyList()
    {
        IReadOnlyList<VectorSearchResult> candidates = Array.Empty<VectorSearchResult>();
        var result = await new NoOpRerankingService().RerankAsync("question", candidates);

        Assert.AreSame(candidates, result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task RerankAsync_RejectsInvalidInputAndCancellation()
    {
        var service = new NoOpRerankingService();
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RerankAsync(" ", []));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.RerankAsync("question", null!));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => service.RerankAsync("question", [], cancellation.Token));
    }

    [TestMethod]
    public void Configuration_DefaultIsDisabled()
    {
        Assert.IsFalse(new RagOptions().UseReranking);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Application_RegistersPlaceholderAndBindsOption(bool enabled)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Rag:UseReranking"] = enabled.ToString(),
                    ["ServiceBus:DocumentImportWorkerEnabled"] = "false",
                    ["ApplicationInsights:ConnectionString"] = ""
                }));
        });
        using var scope = factory.Services.CreateScope();

        Assert.AreEqual(enabled, scope.ServiceProvider.GetRequiredService<IOptions<RagOptions>>().Value.UseReranking);
        Assert.IsInstanceOfType<NoOpRerankingService>(scope.ServiceProvider.GetRequiredService<IRerankingService>());
    }
}
