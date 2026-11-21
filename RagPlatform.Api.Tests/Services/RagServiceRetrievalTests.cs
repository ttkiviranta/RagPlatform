using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Conversations;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Models;
using RagPlatform.Api.Options;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class RagServiceRetrievalTests
{
    [TestMethod]
    public async Task QueryAsync_VectorMode_UsesVectorOnly()
    {
        var vector = new FakeVectorSearchService(CreateVectorResult());
        var hybrid = new FakeHybridSearchService([CreateHybridResult()]);
        var citation = new CapturingCitationService();
        var service = CreateService(vector, hybrid, citation, useHybridSearch: false);

        await service.QueryAsync("question", topK: 5);

        Assert.AreEqual(1, vector.CallCount);
        Assert.AreEqual(0, hybrid.CallCount);
        Assert.AreEqual(1, citation.ReceivedResults.Count);
    }

    [TestMethod]
    public async Task QueryAsync_HybridMode_UsesHybridResults()
    {
        var vector = new FakeVectorSearchService(CreateVectorResult());
        var hybrid = new FakeHybridSearchService([CreateHybridResult()]);
        var citation = new CapturingCitationService();
        var service = CreateService(vector, hybrid, citation, useHybridSearch: true);

        await service.QueryAsync("question", topK: 5);

        Assert.AreEqual(0, vector.CallCount);
        Assert.AreEqual(1, hybrid.CallCount);
        Assert.AreEqual(1, citation.ReceivedResults.Count);
        Assert.AreEqual(hybrid.Results[0].ChunkId, citation.ReceivedResults[0].ChunkId);
    }

    [TestMethod]
    public async Task QueryAsync_HybridFailure_FallsBackToVector()
    {
        var vector = new FakeVectorSearchService(CreateVectorResult());
        var hybrid = new FakeHybridSearchService(Array.Empty<HybridSearchResult>()) { Error = new InvalidOperationException("hybrid failed") };
        var service = CreateService(vector, hybrid, new CapturingCitationService(), useHybridSearch: true);

        await service.QueryAsync("question", topK: 5);

        Assert.AreEqual(1, vector.CallCount);
        Assert.AreEqual(1, hybrid.CallCount);
    }

    [TestMethod]
    public async Task QueryAsync_HybridEmpty_FallsBackToVector()
    {
        var vector = new FakeVectorSearchService(CreateVectorResult());
        var hybrid = new FakeHybridSearchService(Array.Empty<HybridSearchResult>());
        var service = CreateService(vector, hybrid, new CapturingCitationService(), useHybridSearch: true);

        await service.QueryAsync("question", topK: 5);

        Assert.AreEqual(1, vector.CallCount);
        Assert.AreEqual(1, hybrid.CallCount);
    }

    [TestMethod]
    public async Task QueryAsync_HybridCancellation_PropagatesWithoutFallback()
    {
        var vector = new FakeVectorSearchService(CreateVectorResult());
        var hybrid = new FakeHybridSearchService(Array.Empty<HybridSearchResult>());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        hybrid.Error = new OperationCanceledException(cancellation.Token);
        var service = CreateService(vector, hybrid, new CapturingCitationService(), useHybridSearch: true);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.QueryAsync(
            "question",
            topK: 5,
            cancellationToken: cancellation.Token));

        Assert.AreEqual(0, vector.CallCount);
        Assert.AreEqual(1, hybrid.CallCount);
    }

    private static RagService CreateService(
        FakeVectorSearchService vector,
        FakeHybridSearchService hybrid,
        CapturingCitationService citation,
        bool useHybridSearch)
    {
        return new RagService(
            vector,
            hybrid,
            Microsoft.Extensions.Options.Options.Create(new RagOptions { UseHybridSearch = useHybridSearch }),
            citation,
            new NoOpTelemetryService(),
            new FakeCurrentUserService(),
            new FakeAuditEventWriter(),
            new FakeAnswerGenerationService(),
            new FakeConversationMessageService(),
            NullLogger<RagService>.Instance);
    }

    private static VectorSearchResult CreateVectorResult()
    {
        return new VectorSearchResult
        {
            ChunkId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            DocumentId = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            ContentSnippet = "vector content"
        };
    }

    private static HybridSearchResult CreateHybridResult()
    {
        return new HybridSearchResult
        {
            ChunkId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            DocumentId = Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Content = "hybrid content",
            FoundByVectorSearch = true,
            FoundByFullTextSearch = true
        };
    }

    private sealed class FakeVectorSearchService(params VectorSearchResult[] results) : IVectorSearchService
    {
        public int CallCount { get; private set; }

        public Task<VectorSearchResponse> SearchAsync(
            string query,
            int topK = 5,
            CancellationToken cancellationToken = default,
            Guid? documentId = null)
        {
            CallCount++;
            return Task.FromResult(new VectorSearchResponse { Results = results, RequestedTopK = topK });
        }
    }

    private sealed class FakeHybridSearchService(IReadOnlyList<HybridSearchResult> results) : IHybridSearchService
    {
        public IReadOnlyList<HybridSearchResult> Results { get; } = results;
        public int CallCount { get; private set; }
        public Exception? Error { get; set; }

        public Task<IReadOnlyList<HybridSearchResult>> SearchAsync(
            string query,
            int topK = 5,
            CancellationToken cancellationToken = default,
            Guid? documentId = null)
        {
            CallCount++;
            if (Error is not null)
            {
                return Task.FromException<IReadOnlyList<HybridSearchResult>>(Error);
            }

            return Task.FromResult(Results);
        }
    }

    private sealed class CapturingCitationService : IRagCitationService
    {
        public IReadOnlyList<VectorSearchResult> ReceivedResults { get; private set; } = Array.Empty<VectorSearchResult>();

        public Task<IReadOnlyList<RagCitationDto>> CreateCitationsAsync(
            IReadOnlyList<VectorSearchResult> retrievedChunks,
            CancellationToken cancellationToken = default)
        {
            ReceivedResults = retrievedChunks;
            return Task.FromResult<IReadOnlyList<RagCitationDto>>(Array.Empty<RagCitationDto>());
        }
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? Principal => new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test")], "test"));
        public string? EntraObjectId => "test-object-id";
        public string? Email => null;
        public string? TenantId => "test-tenant";

        public Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<User?>(new User { Id = Guid.NewGuid(), TenantId = "test-tenant", IsActive = true });
        }
    }

    private sealed class FakeAuditEventWriter : IAuditEventWriter
    {
        public void Add(Guid? actorUserId, string tenantId, string eventType, string outcome, string? targetType = null, string? targetId = null, double? durationMilliseconds = null, int? inputCharacterCount = null, int? requestedTopK = null, int? resultCount = null, double? averageSimilarity = null, int? contextCharacterCount = null, int? promptCharacterCount = null, string? modelProvider = null, string? modelDeployment = null, string? modelOperation = null, int? inputTokenCount = null, int? outputTokenCount = null, int? generatedEmbeddingCount = null, int? reusedEmbeddingCount = null)
        {
        }

        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAnswerGenerationService : IAnswerGenerationService
    {
        public Task<GroundedAnswerResult> GenerateAnswerAsync(
            string question,
            int retrievedChunkCount,
            IReadOnlyList<RagCitationDto> citations,
            IReadOnlyList<ConversationMessageResponse> conversationHistory,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GroundedAnswerResult("answer", Array.Empty<int>(), 0, 0));
        }
    }

    private sealed class FakeConversationMessageService : IConversationMessageService
    {
        public Task<IReadOnlyCollection<ConversationMessageResponse>?> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ConversationMessageResponse>?>(null);

        public Task<ConversationMessageResponse?> CreateUserMessageAsync(Guid conversationId, CreateConversationMessageRequest request, CancellationToken cancellationToken = default) => Task.FromResult<ConversationMessageResponse?>(null);

        public Task<IReadOnlyList<ConversationMessageResponse>?> GetRecentMessagesAsync(Guid conversationId, int maxCount, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ConversationMessageResponse>?>(null);

        public Task<bool> AppendExchangeAsync(Guid conversationId, string userContent, string assistantContent, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Guid?> AppendRagExchangeAsync(Guid? conversationId, string userContent, string assistantContent, IReadOnlyList<RagCitationDto> citations, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(Guid.NewGuid());
    }
}
