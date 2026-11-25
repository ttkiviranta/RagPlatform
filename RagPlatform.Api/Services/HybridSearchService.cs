using RagPlatform.Api.Contracts.Search;

namespace RagPlatform.Api.Services;

public sealed class HybridSearchService : IHybridSearchService
{
    private const int CandidateMultiplier = 2;
    private const int MaximumCandidateCount = 50;
    private const int ReciprocalRankFusionConstant = 60;

    private readonly IVectorSearchService _vectorSearchService;
    private readonly IFullTextSearchService _fullTextSearchService;
    private readonly ILogger<HybridSearchService> _logger;

    public HybridSearchService(
        IVectorSearchService vectorSearchService,
        IFullTextSearchService fullTextSearchService,
        ILogger<HybridSearchService> logger)
    {
        _vectorSearchService = vectorSearchService;
        _fullTextSearchService = fullTextSearchService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<HybridSearchResult>> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default,
        Guid? documentId = null,
        IReadOnlyCollection<string?>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Search query is required.", nameof(query));
        }

        if (topK <= 0 || topK > MaximumCandidateCount)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), "TopK must be between 1 and 50.");
        }

        var tagNames = DocumentTagNames.Normalize(tags);
        var candidateCount = Math.Min(topK * CandidateMultiplier, MaximumCandidateCount);
        var vectorResults = await _vectorSearchService.SearchAsync(
            query,
            candidateCount,
            cancellationToken,
            documentId,
            tagNames);
        var fullTextResults = await _fullTextSearchService.SearchAsync(
            query,
            candidateCount,
            cancellationToken,
            documentId,
            tagNames);

        var candidates = new Dictionary<Guid, RankedCandidate>();
        var seenVectorChunkIds = new HashSet<Guid>();
        for (var index = 0; index < vectorResults.Results.Count; index++)
        {
            var vectorResult = vectorResults.Results[index];
            if (!seenVectorChunkIds.Add(vectorResult.ChunkId))
            {
                continue;
            }

            if (!candidates.TryGetValue(vectorResult.ChunkId, out var candidate))
            {
                candidate = new RankedCandidate(new HybridSearchResult
                {
                    ChunkId = vectorResult.ChunkId,
                    DocumentId = vectorResult.DocumentId,
                    Content = vectorResult.ContentSnippet
                });
                candidates.Add(vectorResult.ChunkId, candidate);
            }

            candidate.Result.FoundByVectorSearch = true;
            candidate.Score += 1d / (ReciprocalRankFusionConstant + index + 1);
        }

        var seenFullTextChunkIds = new HashSet<Guid>();
        for (var index = 0; index < fullTextResults.Count; index++)
        {
            var fullTextResult = fullTextResults[index];
            if (!seenFullTextChunkIds.Add(fullTextResult.ChunkId))
            {
                continue;
            }

            if (!candidates.TryGetValue(fullTextResult.ChunkId, out var candidate))
            {
                candidate = new RankedCandidate(new HybridSearchResult
                {
                    ChunkId = fullTextResult.ChunkId,
                    DocumentId = fullTextResult.DocumentId
                });
                candidates.Add(fullTextResult.ChunkId, candidate);
            }

            candidate.Result.Content = fullTextResult.Content;
            candidate.Result.FoundByFullTextSearch = true;
            candidate.Score += 1d / (ReciprocalRankFusionConstant + index + 1);
        }

        var rankedCandidates = candidates.Values
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Result.ChunkId)
            .ToList();
        var results = rankedCandidates
            .Take(topK)
            .Select(candidate => candidate.Result)
            .ToList();

        _logger.LogInformation(
            "Hybrid search returned {ResultCount} results from {CandidateCount} unique candidates.",
            results.Count,
            candidates.Count);

        return results;
    }

    private sealed class RankedCandidate(HybridSearchResult result)
    {
        public HybridSearchResult Result { get; } = result;

        public double Score { get; set; }
    }
}