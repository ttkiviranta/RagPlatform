using System.Text.Json;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class FoundryRerankingServiceTests
{
    [TestMethod]
    public async Task RerankAsync_ValidPermutation_ReturnsOriginalObjectsInRequestedOrder()
    {
        var candidates = CreateCandidates(3);
        var chat = new FakeChat { Answer = " [3, 1, 2] " };
        var service = new FoundryRerankingService(chat);
        using var cancellation = new CancellationTokenSource();

        var result = await service.RerankAsync("Which one?", candidates, cancellation.Token);

        Assert.AreEqual(1, chat.CallCount);
        Assert.AreEqual(cancellation.Token, chat.Token);
        Assert.AreEqual(3, result.Count);
        Assert.AreSame(candidates[2], result[0]);
        Assert.AreSame(candidates[0], result[1]);
        Assert.AreSame(candidates[1], result[2]);
        Assert.AreEqual("candidate 0", candidates[0].ContentSnippet);
        StringAssert.Contains(chat.SystemPrompt, "untrusted data");
        Assert.AreEqual("User", chat.Messages.Single().Role);
        using var request = JsonDocument.Parse(chat.Messages.Single().Content);
        Assert.AreEqual("Which one?", request.RootElement.GetProperty("question").GetString());
        var sent = request.RootElement.GetProperty("candidates");
        Assert.AreEqual(3, sent.GetArrayLength());
        Assert.AreEqual(1, sent[0].GetProperty("number").GetInt32());
        Assert.AreEqual("candidate 0", sent[0].GetProperty("text").GetString());
        Assert.IsFalse(sent[0].TryGetProperty("documentId", out _));
    }

    [DataTestMethod]
    [DataRow("[1,1,2]")]
    [DataRow("[1,2]")]
    [DataRow("[1,2,3,4]")]
    [DataRow("[0,1,2]")]
    [DataRow("[-1,1,2]")]
    [DataRow("[1,2,4]")]
    [DataRow("[1,2,2147483648]")]
    [DataRow("[1,2,3.0]")]
    [DataRow("[1,2,\"3\"]")]
    [DataRow("[1,2,null]")]
    [DataRow("{\"order\":[1,2,3]}")]
    [DataRow("[1,2,3] trailing text")]
    [DataRow("```json\n[1,2,3]\n```")]
    [DataRow("")]
    public async Task RerankAsync_InvalidOutput_Throws(string answer)
    {
        var chat = new FakeChat { Answer = answer };
        var service = new FoundryRerankingService(chat);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.RerankAsync("question", CreateCandidates(3)));
        Assert.AreEqual(1, chat.CallCount);
    }

    [TestMethod]
    public async Task RerankAsync_OversizedResponse_Throws()
    {
        var chat = new FakeChat { Answer = new string(' ', 1025) + "[1]" };
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => new FoundryRerankingService(chat).RerankAsync("question", CreateCandidates(1)));
    }

    [TestMethod]
    public async Task RerankAsync_EmptyCandidates_DoesNotCallChat()
    {
        var chat = new FakeChat();
        IReadOnlyList<VectorSearchResult> candidates = Array.Empty<VectorSearchResult>();
        var result = await new FoundryRerankingService(chat).RerankAsync("question", candidates);

        Assert.AreSame(candidates, result);
        Assert.AreEqual(0, chat.CallCount);
    }

    [TestMethod]
    public async Task RerankAsync_BoundsInputAndAvoidsSplittingSurrogatePairs()
    {
        var candidates = CreateCandidates(FoundryRerankingService.MaximumCandidates);
        candidates[0].ContentSnippet = new string('a', FoundryRerankingService.MaximumCandidateCharacters - 1) + "\U0001F600" + "tail";
        var chat = new FakeChat { Answer = JsonSerializer.Serialize(Enumerable.Range(1, candidates.Count)) };
        var result = await new FoundryRerankingService(chat).RerankAsync(new string('q', FoundryRerankingService.MaximumQueryCharacters), candidates);

        Assert.AreEqual(FoundryRerankingService.MaximumCandidates, result.Count);
        using var request = JsonDocument.Parse(chat.Messages.Single().Content);
        var text = request.RootElement.GetProperty("candidates")[0].GetProperty("text").GetString();
        Assert.AreEqual(FoundryRerankingService.MaximumCandidateCharacters - 1, text!.Length);
        Assert.AreEqual(new string('a', FoundryRerankingService.MaximumCandidateCharacters - 1), text);
    }

    [TestMethod]
    public async Task RerankAsync_InvalidInputs_RejectsBeforeCallingChat()
    {
        var chat = new FakeChat();
        var service = new FoundryRerankingService(chat);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RerankAsync(" ", []));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.RerankAsync("question", null!));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RerankAsync(new string('q', FoundryRerankingService.MaximumQueryCharacters + 1), []));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RerankAsync("question", CreateCandidates(FoundryRerankingService.MaximumCandidates + 1)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RerankAsync("question", [null!]));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RerankAsync("question", [new VectorSearchResult { ContentSnippet = null! }]));
        Assert.AreEqual(0, chat.CallCount);
    }

    [TestMethod]
    public async Task RerankAsync_CancellationAndChatFailure_Propagate()
    {
        var chat = new FakeChat { Error = new TimeoutException("timeout") };
        var service = new FoundryRerankingService(chat);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.RerankAsync("question", CreateCandidates(1), cancellation.Token));
        Assert.AreEqual(0, chat.CallCount);
        await Assert.ThrowsExactlyAsync<TimeoutException>(() => service.RerankAsync("question", CreateCandidates(1)));
        Assert.AreEqual(1, chat.CallCount);
    }

    [TestMethod]
    public async Task RerankAsync_CancelledDuringChat_DoesNotReturnResults()
    {
        using var cancellation = new CancellationTokenSource();
        var chat = new FakeChat { Answer = "[1]", OnCall = cancellation.Cancel };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => new FoundryRerankingService(chat).RerankAsync("question", CreateCandidates(1), cancellation.Token));
        Assert.AreEqual(1, chat.CallCount);
    }

    [TestMethod]
    public async Task RerankAsync_CandidatesChangedDuringChat_UsesOriginalSnapshot()
    {
        var candidates = CreateCandidates(2);
        var first = candidates[0];
        var second = candidates[1];
        var chat = new FakeChat { Answer = "[2,1]", OnCall = () => candidates.Clear() };

        var result = await new FoundryRerankingService(chat).RerankAsync("question", candidates);

        Assert.AreSame(second, result[0]);
        Assert.AreSame(first, result[1]);
    }

    private static List<VectorSearchResult> CreateCandidates(int count) =>
        Enumerable.Range(0, count).Select(index => new VectorSearchResult
        {
            ChunkId = Guid.NewGuid(), DocumentId = Guid.NewGuid(), ContentSnippet = $"candidate {index}", SimilarityScore = index
        }).ToList();

    private sealed class FakeChat : IFoundryChatCompletionService
    {
        public string Answer { get; init; } = "[1]";
        public Exception? Error { get; init; }
        public Action? OnCall { get; init; }
        public int CallCount { get; private set; }
        public string SystemPrompt { get; private set; } = string.Empty;
        public IReadOnlyList<RagChatMessage> Messages { get; private set; } = [];
        public CancellationToken Token { get; private set; }

        public Task<FoundryChatCompletionResponse> CompleteAsync(string systemPrompt, IReadOnlyList<RagChatMessage> messages,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            SystemPrompt = systemPrompt;
            Messages = messages;
            Token = cancellationToken;
            OnCall?.Invoke();
            if (Error is not null) return Task.FromException<FoundryChatCompletionResponse>(Error);
            return Task.FromResult(new FoundryChatCompletionResponse { Answer = Answer });
        }
    }
}
