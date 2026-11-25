using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Evaluation;

[TestClass]
[TestCategory("Unit")]
public sealed class RerankingEvaluationTests
{
    private static readonly Guid DocumentA = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid DocumentB = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid ChunkA = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ChunkB = Guid.Parse("20000000-0000-0000-0000-000000000002");

    [DataTestMethod]
    [DataRow(true, "improved")]
    [DataRow(false, "regressed")]
    public async Task RunAsync_ReportsBothDirectionsAndOrders(bool secondIsRelevant, string expected)
    {
        var hybrid = new FakeHybrid();
        var chat = new CapturingEvaluationChat(new FakeChat("[2,1]", 12, 3));
        var result = await RerankingEvaluation.RunAsync(new("case", "query", [secondIsRelevant ? DocumentB : DocumentA], ["tag"]), 1, hybrid, new FoundryRerankingService(chat), chat);

        Assert.AreEqual("reranked", result.Status);
        Assert.AreEqual(expected, result.QualityChange);
        CollectionAssert.AreEqual(new[] { ChunkA }, result.Baseline!.ChunkIds);
        CollectionAssert.AreEqual(new[] { ChunkB }, result.Reranked!.ChunkIds);
        Assert.AreEqual(0, result.SharedChunks);
        Assert.AreEqual(1, result.ChangedPositions);
        Assert.AreEqual(secondIsRelevant ? null : 1, result.Baseline.FirstRelevantRank);
        Assert.AreEqual(secondIsRelevant ? 1 : null, result.Reranked.FirstRelevantRank);
        CollectionAssert.AreEqual(new[] { 1, 2 }, hybrid.TopKs.ToArray());
        Assert.IsTrue(hybrid.Tags.All(tags => tags!.SequenceEqual(new[] { "tag" })));
        Assert.AreEqual(12, result.InputTokens);
        Assert.AreEqual(3, result.OutputTokens);
        Assert.IsTrue(result.EnabledPathMilliseconds >= result.BaselineMilliseconds);
    }

    [TestMethod]
    public async Task RunAsync_InvalidResponseReportsFallbackAndUsage()
    {
        var chat = new CapturingEvaluationChat(new FakeChat("[1,1]", 17, 4));
        var result = await RerankingEvaluation.RunAsync(new("case", "query", [DocumentA]), 1, new FakeHybrid(), new FoundryRerankingService(chat), chat);

        Assert.AreEqual("fallback", result.Status);
        Assert.AreEqual("InvalidOperationException", result.ErrorType);
        CollectionAssert.AreEqual(new[] { ChunkA }, result.Baseline!.ChunkIds);
        Assert.IsNull(result.Reranked);
        Assert.IsNull(result.QualityChange);
        Assert.AreEqual(17, result.InputTokens);
        Assert.AreEqual(4, result.OutputTokens);
        Assert.IsNotNull(result.EnabledPathMilliseconds);
    }

    [TestMethod]
    public async Task RunAsync_UnjudgedAndExpansionFailureDoNotReuseTokenUsage()
    {
        var chat = new CapturingEvaluationChat(new FakeChat("[2,1]", 12, 3));
        var reranker = new FoundryRerankingService(chat);
        var unjudged = await RerankingEvaluation.RunAsync(new("first", "query", []), 1, new FakeHybrid(), reranker, chat);
        var failure = await RerankingEvaluation.RunAsync(new("second", "query", [DocumentA]), 1, new FakeHybrid { FailExpansion = true }, reranker, chat);

        Assert.IsNull(unjudged.QualityChange);
        Assert.AreEqual(12, unjudged.InputTokens);
        Assert.AreEqual("fallback", failure.Status);
        Assert.IsNull(failure.InputTokens);
        Assert.IsNull(failure.OutputTokens);
        Assert.IsNull(failure.ExpandedSearchMilliseconds);
        Assert.IsNotNull(failure.EnabledPathMilliseconds);
    }

    [TestMethod]
    public async Task RunAsync_UnchangedAndUnavailableUsageAreReported()
    {
        var chat = new CapturingEvaluationChat(new FakeChat("[1,2]", null, null));
        var result = await RerankingEvaluation.RunAsync(new("case", "query", [DocumentA]), 1, new FakeHybrid(), new FoundryRerankingService(chat), chat);

        Assert.AreEqual("unchanged", result.QualityChange);
        Assert.AreEqual(1, result.SharedChunks);
        Assert.AreEqual(0, result.ChangedPositions);
        Assert.IsNull(result.InputTokens);
        Assert.IsNull(result.OutputTokens);
    }

    [TestMethod]
    public async Task RunAsync_EmptyBaselineIsNotScoredAsReranked()
    {
        var hybrid = new FakeHybrid { EmptyBaseline = true };
        var chat = new CapturingEvaluationChat(new FakeChat("[1]", 12, 3));
        var result = await RerankingEvaluation.RunAsync(new("case", "query", [DocumentA]), 1, hybrid, new FoundryRerankingService(chat), chat);

        Assert.AreEqual("baseline-empty", result.Status);
        Assert.IsNull(result.QualityChange);
        Assert.IsNull(result.Reranked);
        Assert.IsNull(result.InputTokens);
        CollectionAssert.AreEqual(new[] { 1 }, hybrid.TopKs.ToArray());
    }

    [TestMethod]
    public void Summary_UsesOnlyJudgedPairsAndCountsMissesAsZero()
    {
        static EvaluationRanking Rank(int? rank) => new([], [], rank);
        static RerankingEvaluationResult Result(string? change, int? before, int? after) =>
            new("case", "reranked", null, Rank(before), Rank(after), change, 1, 1, 1, 3, null, null);
        var summary = EvaluationSummary.Create([Result("improved", 2, 1), Result("regressed", 1, null), Result("unchanged", null, null), Result(null, 1, 1)]);

        Assert.AreEqual(4, summary.Total);
        Assert.AreEqual(3, summary.PairedJudged);
        Assert.AreEqual(1, summary.Improved);
        Assert.AreEqual(1, summary.Regressed);
        Assert.AreEqual(1, summary.Unchanged);
        Assert.AreEqual(1, summary.NotCompared);
        Assert.AreEqual(2d / 3, summary.BaselineHitRate!.Value, 0.000001);
        Assert.AreEqual(1d / 3, summary.RerankedHitRate!.Value, 0.000001);
        Assert.AreEqual(0.5, summary.BaselineMrr!.Value, 0.000001);
        Assert.AreEqual(1d / 3, summary.RerankedMrr!.Value, 0.000001);
        Assert.IsNull(EvaluationSummary.Create([]).BaselineMrr);
    }

    [TestMethod]
    public void CaseFile_DeserializesDocumentLabelsAndTags()
    {
        const string json = "[{\"caseId\":\"AQ-001\",\"query\":\"question\",\"relevantDocumentIds\":[\"10000000-0000-0000-0000-000000000001\"],\"tags\":[\"security\"]}]";
        var cases = System.Text.Json.JsonSerializer.Deserialize<RerankingEvaluationCase[]>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        Assert.AreEqual("AQ-001", cases.Single().CaseId);
        CollectionAssert.AreEqual(new[] { DocumentA }, cases.Single().RelevantDocumentIds);
        CollectionAssert.AreEqual(new[] { "security" }, cases.Single().Tags!);
    }

    private sealed class FakeHybrid : IHybridSearchService
    {
        public bool FailExpansion { get; init; }
        public bool EmptyBaseline { get; init; }
        public List<int> TopKs { get; } = [];
        public List<IReadOnlyCollection<string?>?> Tags { get; } = [];
        public Task<IReadOnlyList<HybridSearchResult>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default, Guid? documentId = null, IReadOnlyCollection<string?>? tags = null)
        {
            TopKs.Add(topK);
            Tags.Add(tags);
            if (FailExpansion && TopKs.Count == 2) throw new InvalidOperationException("synthetic failure");
            if (EmptyBaseline && TopKs.Count == 1) return Task.FromResult<IReadOnlyList<HybridSearchResult>>([]);
            HybridSearchResult[] results = [new() { ChunkId = ChunkA, DocumentId = DocumentA, Content = "a" }, new() { ChunkId = ChunkB, DocumentId = DocumentB, Content = "b" }];
            return Task.FromResult<IReadOnlyList<HybridSearchResult>>(results.Take(topK).ToArray());
        }
    }

    private sealed class FakeChat(string response, int? input, int? output) : IFoundryChatCompletionService
    {
        public Task<FoundryChatCompletionResponse> CompleteAsync(string systemPrompt, IReadOnlyList<RagChatMessage> messages, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FoundryChatCompletionResponse { Answer = response, InputTokenCount = input, OutputTokenCount = output });
    }
}
