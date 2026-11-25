using System.Diagnostics;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Evaluation;

internal sealed record RerankingEvaluationCase(string CaseId, string Query, Guid[] RelevantDocumentIds, string[]? Tags = null);
internal sealed record EvaluationRanking(Guid[] ChunkIds, Guid[] DocumentIds, int? FirstRelevantRank);
internal sealed record RerankingEvaluationResult(
    string CaseId, string Status, string? ErrorType, EvaluationRanking? Baseline, EvaluationRanking? Reranked,
    string? QualityChange, double BaselineMilliseconds, double? ExpandedSearchMilliseconds,
    double? RerankingMilliseconds, double? EnabledPathMilliseconds, int? InputTokens, int? OutputTokens)
{
    public int? SharedChunks => Baseline is null || Reranked is null ? null : Baseline.ChunkIds.Intersect(Reranked.ChunkIds).Count();
    public int? ChangedPositions => Baseline is null || Reranked is null ? null : Enumerable.Range(0, Math.Max(Baseline.ChunkIds.Length, Reranked.ChunkIds.Length))
        .Count(index => index >= Baseline.ChunkIds.Length || index >= Reranked.ChunkIds.Length || Baseline.ChunkIds[index] != Reranked.ChunkIds[index]);
    public double? AddedMilliseconds => EnabledPathMilliseconds - BaselineMilliseconds;
}

internal sealed record EvaluationSummary(int Total, int PairedJudged, int Improved, int Regressed, int Unchanged, int NotCompared,
    double? BaselineHitRate, double? RerankedHitRate, double? BaselineMrr, double? RerankedMrr)
{
    public static EvaluationSummary Create(IReadOnlyCollection<RerankingEvaluationResult> results)
    {
        var judged = results.Where(result => result.QualityChange is not null).ToArray();
        return new(results.Count, judged.Length, judged.Count(r => r.QualityChange == "improved"),
            judged.Count(r => r.QualityChange == "regressed"), judged.Count(r => r.QualityChange == "unchanged"), results.Count - judged.Length,
            judged.Length == 0 ? null : judged.Average(r => r.Baseline!.FirstRelevantRank.HasValue ? 1d : 0d),
            judged.Length == 0 ? null : judged.Average(r => r.Reranked!.FirstRelevantRank.HasValue ? 1d : 0d),
            judged.Length == 0 ? null : judged.Average(r => r.Baseline!.FirstRelevantRank is int rank ? 1d / rank : 0d),
            judged.Length == 0 ? null : judged.Average(r => r.Reranked!.FirstRelevantRank is int rank ? 1d / rank : 0d));
    }
}

internal static class RerankingEvaluation
{
    public static async Task<RerankingEvaluationResult> RunAsync(
        RerankingEvaluationCase testCase, int topK, IHybridSearchService hybrid,
        IRerankingService reranker, CapturingEvaluationChat chat, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testCase.CaseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(testCase.Query);
        if (topK is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(topK));
        ArgumentNullException.ThrowIfNull(testCase.RelevantDocumentIds);
        if (testCase.RelevantDocumentIds.Any(id => id == Guid.Empty)) throw new ArgumentException("Relevant document IDs cannot be empty.");

        chat.Reset();
        var watch = Stopwatch.StartNew();
        IReadOnlyList<HybridSearchResult> baseline;
        try
        {
            baseline = await hybrid.SearchAsync(testCase.Query, topK, cancellationToken, tags: testCase.Tags);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return new(testCase.CaseId, "baseline-error", ex.GetType().Name, null, null, null,
                watch.Elapsed.TotalMilliseconds, null, null, null, null, null);
        }
        var baselineMs = watch.Elapsed.TotalMilliseconds;
        var baselineRanking = Rank(baseline, testCase.RelevantDocumentIds);
        if (baseline.Count == 0)
        {
            return new(testCase.CaseId, "baseline-empty", null, baselineRanking, null, null,
                baselineMs, null, null, null, null, null);
        }

        watch.Restart();
        double? expandedMs = null;
        double? rerankingMs = null;
        try
        {
            var expanded = await hybrid.SearchAsync(testCase.Query, Math.Min(topK * 2, FoundryRerankingService.MaximumCandidates), cancellationToken, tags: testCase.Tags);
            expandedMs = watch.Elapsed.TotalMilliseconds;
            if (expanded.Count == 0)
                return new(testCase.CaseId, "expanded-empty-fallback", null, baselineRanking, null, null,
                    baselineMs, expandedMs, null, baselineMs + expandedMs, null, null);

            var candidates = expanded.Select(item => new VectorSearchResult
            {
                ChunkId = item.ChunkId, DocumentId = item.DocumentId, ContentSnippet = item.Content, SimilarityScore = 0
            }).ToArray();
            chat.Reset();
            watch.Restart();
            var ranked = await reranker.RerankAsync(testCase.Query, candidates, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            rerankingMs = watch.Elapsed.TotalMilliseconds;
            var remaining = new HashSet<VectorSearchResult>(candidates);
            if (ranked.Count != candidates.Length || ranked.Any(item => !remaining.Remove(item)))
                throw new InvalidOperationException("Invalid reranker candidate set.");
            var selected = ranked.Take(topK).ToArray();
            var rerankedRanking = new EvaluationRanking(selected.Select(item => item.ChunkId).ToArray(),
                selected.Select(item => item.DocumentId).ToArray(), FirstRelevantRank(selected.Select(item => item.DocumentId), testCase.RelevantDocumentIds));
            var change = Compare(baselineRanking.FirstRelevantRank, rerankedRanking.FirstRelevantRank, testCase.RelevantDocumentIds.Length > 0);
            return new(testCase.CaseId, "reranked", null, baselineRanking, rerankedRanking, change,
                baselineMs, expandedMs, rerankingMs, baselineMs + expandedMs + rerankingMs, chat.InputTokens, chat.OutputTokens);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            if (expandedMs is not null && rerankingMs is null) rerankingMs = watch.Elapsed.TotalMilliseconds;
            var enabledMs = baselineMs + (expandedMs ?? watch.Elapsed.TotalMilliseconds) + (rerankingMs ?? 0);
            return new(testCase.CaseId, "fallback", ex.GetType().Name, baselineRanking, null, null,
                baselineMs, expandedMs, rerankingMs, enabledMs, chat.InputTokens, chat.OutputTokens);
        }
    }

    private static EvaluationRanking Rank(IReadOnlyList<HybridSearchResult> results, Guid[] relevant) =>
        new(results.Select(item => item.ChunkId).ToArray(), results.Select(item => item.DocumentId).ToArray(),
            FirstRelevantRank(results.Select(item => item.DocumentId), relevant));

    private static int? FirstRelevantRank(IEnumerable<Guid> documents, Guid[] relevant)
    {
        var rank = 0;
        foreach (var document in documents)
        {
            rank++;
            if (relevant.Contains(document)) return rank;
        }
        return null;
    }

    private static string? Compare(int? baseline, int? reranked, bool judged)
    {
        if (!judged) return null;
        if (baseline == reranked) return "unchanged";
        if (!baseline.HasValue) return "improved";
        if (!reranked.HasValue) return "regressed";
        return reranked.Value < baseline.Value ? "improved" : "regressed";
    }
}

internal sealed class CapturingEvaluationChat(IFoundryChatCompletionService inner) : IFoundryChatCompletionService
{
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }
    public void Reset() { InputTokens = null; OutputTokens = null; }

    public async Task<FoundryChatCompletionResponse> CompleteAsync(string systemPrompt, IReadOnlyList<RagChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var response = await inner.CompleteAsync(systemPrompt, messages, cancellationToken);
        InputTokens = response.InputTokenCount;
        OutputTokens = response.OutputTokenCount;
        return response;
    }
}
