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

        await service.QueryAsync("question", topK: 5, tags: [" beta ", "alpha"]);

        Assert.AreEqual(1, vector.CallCount);
        Assert.AreEqual(0, hybrid.CallCount);
        CollectionAssert.AreEqual(new[] { "alpha", "beta" }, vector.LastTags!.ToArray());
        Assert.AreEqual(1, citation.ReceivedResults.Count);
    }

    [TestMethod]
    public async Task QueryAsync_HybridMode_UsesHybridResults()
    {
        var vector = new FakeVectorSearchService(CreateVectorResult());
        var hybrid = new FakeHybridSearchService([CreateHybridResult()]);
        var citation = new CapturingCitationService();
        var reranker = new FakeReranker();
        var service = CreateService(vector, hybrid, citation, useHybridSearch: true, reranker: reranker);

        await service.QueryAsync("question", topK: 5, tags: [" beta ", "alpha"]);

        Assert.AreEqual(0, vector.CallCount);
        Assert.AreEqual(1, hybrid.CallCount);
        Assert.AreEqual(0, reranker.CallCount);
        CollectionAssert.AreEqual(new[] { 5 }, hybrid.TopKs.ToArray());
        CollectionAssert.AreEqual(new[] { "alpha", "beta" }, hybrid.LastTags!.ToArray());
        Assert.AreEqual(1, citation.ReceivedResults.Count);
        Assert.AreEqual(hybrid.Results[0].ChunkId, citation.ReceivedResults[0].ChunkId);
    }

    [TestMethod]
    public async Task QueryAsync_HybridFailure_FallsBackToVector()
    {
        var vector = new FakeVectorSearchService(CreateVectorResult());
        var hybrid = new FakeHybridSearchService(Array.Empty<HybridSearchResult>()) { Error = new InvalidOperationException("hybrid failed") };
        var service = CreateService(vector, hybrid, new CapturingCitationService(), useHybridSearch: true);

        await service.QueryAsync("question", topK: 5, tags: [" beta ", "alpha"]);

        Assert.AreEqual(1, vector.CallCount);
        Assert.AreEqual(1, hybrid.CallCount);
        CollectionAssert.AreEqual(new[] { "alpha", "beta" }, hybrid.LastTags!.ToArray());
        CollectionAssert.AreEqual(new[] { "alpha", "beta" }, vector.LastTags!.ToArray());
    }

    [TestMethod]
    public async Task QueryAsync_HybridEmpty_FallsBackToVector()
    {
        var vector = new FakeVectorSearchService(CreateVectorResult());
        var hybrid = new FakeHybridSearchService(Array.Empty<HybridSearchResult>());
        var service = CreateService(vector, hybrid, new CapturingCitationService(), useHybridSearch: true);

        await service.QueryAsync("question", topK: 5, tags: ["alpha"]);

        Assert.AreEqual(1, vector.CallCount);
        Assert.AreEqual(1, hybrid.CallCount);
        CollectionAssert.AreEqual(new[] { "alpha" }, hybrid.LastTags!.ToArray());
        CollectionAssert.AreEqual(new[] { "alpha" }, vector.LastTags!.ToArray());
    }

    [TestMethod]
    public async Task QueryAsync_InvalidTags_DoesNotRetrieveOrFallBack()
    {
        var vector = new FakeVectorSearchService(CreateVectorResult());
        var hybrid = new FakeHybridSearchService(Array.Empty<HybridSearchResult>());
        var service = CreateService(vector, hybrid, new CapturingCitationService(), useHybridSearch: true);

        var error = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => service.QueryAsync("question", tags: ["tag", " tag "]));

        Assert.AreEqual("tags", error.ParamName);
        Assert.AreEqual(0, vector.CallCount);
        Assert.AreEqual(0, hybrid.CallCount);
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

    [TestMethod]
    public async Task QueryAsync_RerankingEnabled_SelectsExpandedCandidatesBeforeCitation()
    {
        var first = CreateHybridResult();
        var second = new HybridSearchResult { ChunkId = Guid.NewGuid(), DocumentId = Guid.NewGuid(), Content = "second" };
        var hybrid = new FakeHybridSearchService([first, second]);
        var reranker = new FakeReranker();
        var citation = new CapturingCitationService();
        var service = CreateService(new FakeVectorSearchService(), hybrid, citation, true, true, reranker);

        await service.QueryAsync("question", topK: 1, tags: [" beta ", "alpha"]);

        CollectionAssert.AreEqual(new[] { 1, 2 }, hybrid.TopKs.ToArray());
        Assert.IsTrue(hybrid.AllTags.All(tags => tags!.SequenceEqual(new[] { "alpha", "beta" })));
        Assert.AreEqual(1, reranker.CallCount);
        Assert.AreEqual("question", reranker.Query);
        CollectionAssert.AreEqual(new[] { first.ChunkId, second.ChunkId }, reranker.Candidates.Select(item => item.ChunkId).ToArray());
        Assert.AreEqual(second.ChunkId, citation.ReceivedResults.Single().ChunkId);
        Assert.AreEqual(second.DocumentId, citation.ReceivedResults.Single().DocumentId);
    }

    [DataTestMethod]
    [DataRow("[2,1]", true)]
    [DataRow("[1,1]", false)]
    public async Task QueryAsync_FoundryReranker_UsesValidatedOrderOrBaseline(string response, bool valid)
    {
        var first = CreateHybridResult();
        var second = new HybridSearchResult { ChunkId = Guid.NewGuid(), DocumentId = Guid.NewGuid(), Content = "second" };
        var chat = new FakeChat(response);
        var citation = new CapturingCitationService();
        var service = CreateService(new FakeVectorSearchService(), new FakeHybridSearchService([first, second]), citation, true, true, new FoundryRerankingService(chat));

        await service.QueryAsync("question", topK: 1);

        Assert.AreEqual(1, chat.CallCount);
        Assert.AreEqual(valid ? second.ChunkId : first.ChunkId, citation.ReceivedResults.Single().ChunkId);
    }

    [TestMethod]
    public async Task QueryAsync_MaxTopK_CapsExpandedCandidateCountAtForty()
    {
        var hybrid = new FakeHybridSearchService([CreateHybridResult()]);
        var reranker = new FakeReranker();
        var service = CreateService(new FakeVectorSearchService(), hybrid, new CapturingCitationService(), true, true, reranker);

        await service.QueryAsync("question", topK: 20);

        CollectionAssert.AreEqual(new[] { 20, 40 }, hybrid.TopKs.ToArray());
        Assert.AreEqual(1, reranker.CallCount);
    }

    [TestMethod]
    public async Task QueryAsync_RerankingFailure_UsesOriginalHybridRanking()
    {
        var first = CreateHybridResult();
        var second = new HybridSearchResult { ChunkId = Guid.NewGuid(), DocumentId = Guid.NewGuid(), Content = "second" };
        var hybrid = new FakeHybridSearchService([first, second]) { ExpandedResults = [second, first] };
        var vector = new FakeVectorSearchService();
        var reranker = new FakeReranker { Error = new InvalidOperationException("invalid output") };
        var citation = new CapturingCitationService();
        var service = CreateService(vector, hybrid, citation, true, true, reranker);

        await service.QueryAsync("question", topK: 1, tags: ["alpha"]);

        CollectionAssert.AreEqual(new[] { 1, 2 }, hybrid.TopKs.ToArray());
        Assert.AreEqual(first.ChunkId, citation.ReceivedResults.Single().ChunkId);
        Assert.AreEqual(0, vector.CallCount);
    }

    [TestMethod]
    public async Task QueryAsync_ExpandedSearchFails_UsesBaseline()
    {
        var first = CreateHybridResult();
        var hybrid = new FakeHybridSearchService([first]) { ExpandedError = new InvalidOperationException("expanded search failed") };
        var reranker = new FakeReranker();
        var citation = new CapturingCitationService();
        var service = CreateService(new FakeVectorSearchService(), hybrid, citation, true, true, reranker);

        await service.QueryAsync("question", topK: 1);

        Assert.AreEqual(first.ChunkId, citation.ReceivedResults.Single().ChunkId);
        Assert.AreEqual(0, reranker.CallCount);
    }

    [TestMethod]
    public async Task QueryAsync_EnabledInVectorMode_DoesNotRerank()
    {
        var reranker = new FakeReranker();
        var vector = new FakeVectorSearchService(CreateVectorResult());
        var hybrid = new FakeHybridSearchService([CreateHybridResult()]);
        var citation = new CapturingCitationService();
        var service = CreateService(vector, hybrid, citation, false, true, reranker);

        await service.QueryAsync("question");

        Assert.AreEqual(0, reranker.CallCount);
        Assert.AreEqual(0, hybrid.CallCount);
        Assert.AreEqual(1, vector.CallCount);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task QueryAsync_NoContext_UsesConfiguredModelKnowledge(bool enabled)
    {
        var chat = new RagQualityHardeningTests.CapturingChatCompletionService
        {
            Response = new FoundryChatCompletionResponse { Answer = "Helsinki." }
        };
        var answer = CreateAnswerService(chat);
        var conversation = new FakeConversationMessageService();
        var service = CreateService(new FakeVectorSearchService(), new FakeHybridSearchService([]),
            new CapturingCitationService(), useHybridSearch: false,
            allowModelKnowledgeFallback: enabled, answer: answer, conversation: conversation);

        var response = await service.QueryAsync("What is the capital of Finland?", tags: ["general"]);

        Assert.AreEqual(enabled ? "Helsinki." : PromptBuilder.InsufficientEvidenceResponse, response.Answer);
        Assert.AreEqual(0, response.Citations.Count);
        Assert.AreEqual(enabled ? RagAnswerSource.ModelKnowledge : null, response.AnswerSource);
        Assert.AreEqual(enabled ? 1 : 0, chat.CallCount);
        Assert.AreEqual(enabled ? "Model knowledge (no supporting documents found):\nHelsinki." : PromptBuilder.InsufficientEvidenceResponse, conversation.AssistantContent);
        Assert.AreEqual(0, conversation.Citations!.Count);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task QueryAsync_GroundedAbstention_UsesModelKnowledgeOnlyWhenRetrievalSucceeds(bool hybridFails)
    {
        var chat = new RagQualityHardeningTests.CapturingChatCompletionService();
        chat.Responses.Enqueue(new FoundryChatCompletionResponse { Answer = PromptBuilder.InsufficientEvidenceResponse });
        chat.Responses.Enqueue(new FoundryChatCompletionResponse { Answer = "Helsinki." });
        var vector = new FakeVectorSearchService(CreateVectorResult());
        var hybrid = new FakeHybridSearchService([CreateHybridResult()])
        {
            Error = hybridFails ? new InvalidOperationException("failed") : null
        };
        var citation = new CapturingCitationService { Citations = [new RagCitationDto { CitationNumber = 1, Excerpt = "Unrelated evidence" }] };
        var conversation = new FakeConversationMessageService();
        var service = CreateService(vector, hybrid, citation, true,
            allowModelKnowledgeFallback: true, answer: CreateAnswerService(chat), conversation: conversation);

        var response = await service.QueryAsync("What is the capital of Finland?", tags: ["general"]);

        Assert.AreEqual(hybridFails ? PromptBuilder.InsufficientEvidenceResponse : "Helsinki.", response.Answer);
        Assert.AreEqual(hybridFails ? null : RagAnswerSource.ModelKnowledge, response.AnswerSource);
        Assert.AreEqual(0, response.Citations.Count);
        Assert.AreEqual(hybridFails ? 1 : 2, chat.CallCount);
        Assert.AreEqual(hybridFails ? PromptBuilder.InsufficientEvidenceResponse : "Model knowledge (no supporting documents found):\nHelsinki.", conversation.AssistantContent);
        Assert.AreEqual(0, conversation.Citations!.Count);
        CollectionAssert.AreEqual(new[] { "general" }, hybrid.LastTags!.ToArray());
        if (hybridFails)
        {
            CollectionAssert.AreEqual(new[] { "general" }, vector.LastTags!.ToArray());
        }
    }

    [TestMethod]
    public async Task QueryAsync_HybridFailureThenEmptyVector_DoesNotInvokeModelKnowledge()
    {
        var chat = new RagQualityHardeningTests.CapturingChatCompletionService();
        var vector = new FakeVectorSearchService();
        var hybrid = new FakeHybridSearchService([]) { Error = new InvalidOperationException("failed") };
        var service = CreateService(vector, hybrid, new CapturingCitationService(), true,
            allowModelKnowledgeFallback: true, answer: CreateAnswerService(chat));

        var response = await service.QueryAsync("question", tags: ["tag"]);

        Assert.AreEqual(PromptBuilder.InsufficientEvidenceResponse, response.Answer);
        Assert.IsNull(response.AnswerSource);
        Assert.AreEqual(0, chat.CallCount);
        CollectionAssert.AreEqual(new[] { "tag" }, vector.LastTags!.ToArray());
    }

    [TestMethod]
    public async Task QueryAsync_HybridAndVectorEmpty_InvokesModelKnowledge()
    {
        var chat = new RagQualityHardeningTests.CapturingChatCompletionService { Response = new FoundryChatCompletionResponse { Answer = "answer" } };
        var vector = new FakeVectorSearchService();
        var hybrid = new FakeHybridSearchService([]);
        var service = CreateService(vector, hybrid, new CapturingCitationService(), true,
            allowModelKnowledgeFallback: true, answer: CreateAnswerService(chat));

        var response = await service.QueryAsync("question");

        Assert.AreEqual(RagAnswerSource.ModelKnowledge, response.AnswerSource);
        Assert.AreEqual(1, hybrid.CallCount);
        Assert.AreEqual(1, vector.CallCount);
        Assert.AreEqual(1, chat.CallCount);
    }

    [TestMethod]
    public async Task QueryAsync_ExpandedSearchFailsAndNoUsableCitations_DoesNotInvokeModelKnowledge()
    {
        var chat = new RagQualityHardeningTests.CapturingChatCompletionService();
        var hybrid = new FakeHybridSearchService([CreateHybridResult()]) { ExpandedError = new InvalidOperationException("failed") };
        var service = CreateService(new FakeVectorSearchService(), hybrid, new CapturingCitationService(), true, true,
            allowModelKnowledgeFallback: true, answer: CreateAnswerService(chat));

        var response = await service.QueryAsync("question");

        Assert.AreEqual(PromptBuilder.InsufficientEvidenceResponse, response.Answer);
        Assert.IsNull(response.AnswerSource);
        Assert.AreEqual(0, chat.CallCount);
    }

    [TestMethod]
    public async Task QueryAsync_InactiveUser_DoesNotRetrieveOrInvokeModelKnowledge()
    {
        var chat = new RagQualityHardeningTests.CapturingChatCompletionService();
        var vector = new FakeVectorSearchService();
        var service = CreateService(vector, new FakeHybridSearchService([]), new CapturingCitationService(), false,
            allowModelKnowledgeFallback: true, answer: CreateAnswerService(chat), currentUser: new FakeCurrentUserService { Active = false });

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => service.QueryAsync("question"));
        Assert.AreEqual(0, vector.CallCount);
        Assert.AreEqual(0, chat.CallCount);
    }

    [TestMethod]
    public async Task QueryAsync_UnavailableConversation_DoesNotRetrieveOrInvokeModelKnowledge()
    {
        var chat = new RagQualityHardeningTests.CapturingChatCompletionService();
        var vector = new FakeVectorSearchService();
        var service = CreateService(vector, new FakeHybridSearchService([]), new CapturingCitationService(), false,
            allowModelKnowledgeFallback: true, answer: CreateAnswerService(chat));

        await Assert.ThrowsExactlyAsync<KeyNotFoundException>(() => service.QueryAsync("question", conversationId: Guid.NewGuid()));
        Assert.AreEqual(0, vector.CallCount);
        Assert.AreEqual(0, chat.CallCount);
    }

    [TestMethod]
    public async Task QueryAsync_VectorFailure_DoesNotInvokeModelKnowledgeOrPersist()
    {
        var chat = new RagQualityHardeningTests.CapturingChatCompletionService();
        var conversation = new FakeConversationMessageService();
        var vector = new FakeVectorSearchService { Error = new InvalidOperationException("failed") };
        var service = CreateService(vector, new FakeHybridSearchService([]), new CapturingCitationService(), false,
            allowModelKnowledgeFallback: true, answer: CreateAnswerService(chat), conversation: conversation);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.QueryAsync("question"));
        Assert.AreEqual(0, chat.CallCount);
        Assert.IsNull(conversation.AssistantContent);
    }

    [TestMethod]
    public async Task QueryAsync_ModelKnowledgeFailure_DoesNotPersist()
    {
        var chat = new RagQualityHardeningTests.CapturingChatCompletionService { Error = new TimeoutException("failed") };
        var conversation = new FakeConversationMessageService();
        var service = CreateService(new FakeVectorSearchService(), new FakeHybridSearchService([]), new CapturingCitationService(), false,
            allowModelKnowledgeFallback: true, answer: CreateAnswerService(chat), conversation: conversation);

        await Assert.ThrowsExactlyAsync<TimeoutException>(() => service.QueryAsync("question"));
        Assert.IsNull(conversation.AssistantContent);
    }

    [TestMethod]
    public async Task QueryAsync_GroundedEvidence_PreservesCitationsWhenFallbackEnabled()
    {
        var chat = new RagQualityHardeningTests.CapturingChatCompletionService
        {
            Response = new FoundryChatCompletionResponse { Answer = "Supported answer [1]." }
        };
        var citation = new CapturingCitationService { Citations = [new RagCitationDto { CitationNumber = 1, Excerpt = "Evidence" }] };
        var conversation = new FakeConversationMessageService();
        var service = CreateService(new FakeVectorSearchService(CreateVectorResult()), new FakeHybridSearchService([]), citation, false,
            allowModelKnowledgeFallback: true, answer: CreateAnswerService(chat), conversation: conversation);

        var response = await service.QueryAsync("question");

        Assert.AreEqual("Supported answer [1].", response.Answer);
        Assert.IsNull(response.AnswerSource);
        Assert.AreEqual(1, response.Citations.Count);
        Assert.AreEqual("Supported answer [1].", conversation.AssistantContent);
        Assert.AreEqual(1, conversation.Citations!.Count);
    }

    private static FoundryAnswerGenerationService CreateAnswerService(IFoundryChatCompletionService chat) =>
        new(new PromptBuilder(), chat, new NoOpTelemetryService(),
            Microsoft.Extensions.Options.Options.Create(new FoundryOptions()), NullLogger<FoundryAnswerGenerationService>.Instance);

    [TestMethod]
    public async Task QueryAsync_RerankerCancellation_DoesNotFallBack()
    {
        using var cancellation = new CancellationTokenSource();
        var reranker = new FakeReranker { OnCall = cancellation.Cancel };
        var citation = new CapturingCitationService();
        var service = CreateService(new FakeVectorSearchService(), new FakeHybridSearchService([CreateHybridResult()]), citation, true, true, reranker);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.QueryAsync("question", cancellationToken: cancellation.Token));
        Assert.AreEqual(1, reranker.CallCount);
        Assert.AreEqual(0, citation.ReceivedResults.Count);
    }

    private static RagService CreateService(
        FakeVectorSearchService vector,
        FakeHybridSearchService hybrid,
        CapturingCitationService citation,
        bool useHybridSearch,
        bool useReranking = false,
        IRerankingService? reranker = null,
        bool allowModelKnowledgeFallback = false,
        IAnswerGenerationService? answer = null,
        FakeConversationMessageService? conversation = null,
        FakeCurrentUserService? currentUser = null)
    {
        return new RagService(
            vector,
            hybrid,
            reranker ?? new NoOpRerankingService(),
            Microsoft.Extensions.Options.Options.Create(new RagOptions { UseHybridSearch = useHybridSearch, UseReranking = useReranking, AllowModelKnowledgeFallback = allowModelKnowledgeFallback }),
            citation,
            new NoOpTelemetryService(),
            currentUser ?? new FakeCurrentUserService(),
            new FakeAuditEventWriter(),
            answer ?? new FakeAnswerGenerationService(),
            conversation ?? new FakeConversationMessageService(),
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
        public IReadOnlyCollection<string?>? LastTags { get; private set; }
        public Exception? Error { get; init; }

        public Task<VectorSearchResponse> SearchAsync(
            string query,
            int topK = 5,
            CancellationToken cancellationToken = default,
            Guid? documentId = null,
            IReadOnlyCollection<string?>? tags = null)
        {
            CallCount++;
            LastTags = tags;
            if (Error is not null) return Task.FromException<VectorSearchResponse>(Error);
            return Task.FromResult(new VectorSearchResponse { Results = results, RequestedTopK = topK });
        }
    }

    private sealed class FakeHybridSearchService(IReadOnlyList<HybridSearchResult> results) : IHybridSearchService
    {
        public IReadOnlyList<HybridSearchResult> Results { get; } = results;
        public int CallCount { get; private set; }
        public Exception? Error { get; set; }
        public Exception? ExpandedError { get; init; }
        public IReadOnlyList<HybridSearchResult>? ExpandedResults { get; init; }
        public IReadOnlyCollection<string?>? LastTags { get; private set; }
        public List<int> TopKs { get; } = [];
        public List<IReadOnlyCollection<string?>?> AllTags { get; } = [];

        public Task<IReadOnlyList<HybridSearchResult>> SearchAsync(
            string query,
            int topK = 5,
            CancellationToken cancellationToken = default,
            Guid? documentId = null,
            IReadOnlyCollection<string?>? tags = null)
        {
            CallCount++;
            LastTags = tags;
            TopKs.Add(topK);
            AllTags.Add(tags);
            if (Error is not null)
            {
                return Task.FromException<IReadOnlyList<HybridSearchResult>>(Error);
            }
            if (CallCount > 1 && ExpandedError is not null)
            {
                return Task.FromException<IReadOnlyList<HybridSearchResult>>(ExpandedError);
            }

            var source = CallCount > 1 && ExpandedResults is not null ? ExpandedResults : Results;
            return Task.FromResult<IReadOnlyList<HybridSearchResult>>(source.Take(topK).ToArray());
        }
    }

    private sealed class FakeReranker : IRerankingService
    {
        public int CallCount { get; private set; }
        public string? Query { get; private set; }
        public IReadOnlyList<VectorSearchResult> Candidates { get; private set; } = [];
        public Exception? Error { get; init; }
        public Action? OnCall { get; init; }

        public Task<IReadOnlyList<VectorSearchResult>> RerankAsync(string query, IReadOnlyList<VectorSearchResult> candidates, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Query = query;
            Candidates = candidates;
            OnCall?.Invoke();
            if (Error is not null) return Task.FromException<IReadOnlyList<VectorSearchResult>>(Error);
            return Task.FromResult<IReadOnlyList<VectorSearchResult>>(candidates.Reverse().ToArray());
        }
    }

    private sealed class FakeChat(string answer) : IFoundryChatCompletionService
    {
        public int CallCount { get; private set; }

        public Task<FoundryChatCompletionResponse> CompleteAsync(string systemPrompt, IReadOnlyList<RagChatMessage> messages, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new FoundryChatCompletionResponse { Answer = answer });
        }
    }

    private sealed class CapturingCitationService : IRagCitationService
    {
        public IReadOnlyList<VectorSearchResult> ReceivedResults { get; private set; } = Array.Empty<VectorSearchResult>();
        public IReadOnlyList<RagCitationDto> Citations { get; init; } = Array.Empty<RagCitationDto>();

        public Task<IReadOnlyList<RagCitationDto>> CreateCitationsAsync(
            IReadOnlyList<VectorSearchResult> retrievedChunks,
            CancellationToken cancellationToken = default)
        {
            ReceivedResults = retrievedChunks;
            return Task.FromResult(Citations);
        }
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public bool Active { get; init; } = true;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? Principal => new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test")], "test"));
        public string? EntraObjectId => "test-object-id";
        public string? Email => null;
        public string? TenantId => "test-tenant";

        public Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<User?>(new User { Id = Guid.NewGuid(), TenantId = "test-tenant", IsActive = Active });
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
            CancellationToken cancellationToken = default,
            bool allowModelKnowledgeFallback = false)
        {
            return Task.FromResult(new GroundedAnswerResult("answer", Array.Empty<int>(), 0, 0));
        }
    }

    private sealed class FakeConversationMessageService : IConversationMessageService
    {
        public string? AssistantContent { get; private set; }
        public IReadOnlyList<RagCitationDto>? Citations { get; private set; }

        public Task<IReadOnlyCollection<ConversationMessageResponse>?> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ConversationMessageResponse>?>(null);

        public Task<ConversationMessageResponse?> CreateUserMessageAsync(Guid conversationId, CreateConversationMessageRequest request, CancellationToken cancellationToken = default) => Task.FromResult<ConversationMessageResponse?>(null);

        public Task<IReadOnlyList<ConversationMessageResponse>?> GetRecentMessagesAsync(Guid conversationId, int maxCount, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ConversationMessageResponse>?>(null);

        public Task<bool> AppendExchangeAsync(Guid conversationId, string userContent, string assistantContent, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Guid?> AppendRagExchangeAsync(Guid? conversationId, string userContent, string assistantContent, IReadOnlyList<RagCitationDto> citations, CancellationToken cancellationToken = default)
        {
            AssistantContent = assistantContent;
            Citations = citations;
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }
    }
}
