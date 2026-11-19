using RagPlatform.Api.Contracts.Search;

namespace RagPlatform.Api.Services;

public sealed class HybridSearchService : IHybridSearchService
{
    private const int CandidateMultiplier = 2;
    private const int MaximumCandidateCount = 50;

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
        Guid? documentId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Search query is required.", nameof(query));
        }

        if (topK <= 0 || topK > MaximumCandidateCount)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), "TopK must be between 1 and 50.");
        }

        var candidateCount = Math.Min(topK * CandidateMultiplier, MaximumCandidateCount);
        var vectorResults = await _vectorSearchService.SearchAsync(
            query,
            candidateCount,
            cancellationToken,
            documentId);
        var fullTextResults = await _fullTextSearchService.SearchAsync(
            query,
            candidateCount,
            cancellationToken,
            documentId);

        var vectorResultsList = vectorResults.Results;
        var fullTextResultsByChunkId = fullTextResults
            .GroupBy(result => result.ChunkId)
            .ToDictionary(group => group.Key, group => group.First());
        var seenChunkIds = new HashSet<Guid>();
        var overlappingResults = new List<HybridSearchResult>();
        var vectorOnlyResults = new List<HybridSearchResult>();
        var fullTextOnlyResults = new List<HybridSearchResult>();

        foreach (var vectorResult in vectorResultsList)
        {
            if (!seenChunkIds.Add(vectorResult.ChunkId))
            {
                continue;
            }

            if (fullTextResultsByChunkId.TryGetValue(vectorResult.ChunkId, out var fullTextResult))
            {
                overlappingResults.Add(new HybridSearchResult
                {
                    ChunkId = vectorResult.ChunkId,
                    DocumentId = vectorResult.DocumentId,
                    Content = fullTextResult.Content,
                    FoundByVectorSearch = true,
                    FoundByFullTextSearch = true
                });
            }
            else
            {
                vectorOnlyResults.Add(new HybridSearchResult
                {
                    ChunkId = vectorResult.ChunkId,
                    DocumentId = vectorResult.DocumentId,
                    Content = vectorResult.ContentSnippet,
                    FoundByVectorSearch = true
                });
            }
        }

        foreach (var fullTextResult in fullTextResults)
        {
            if (!seenChunkIds.Add(fullTextResult.ChunkId))
            {
                continue;
            }

            fullTextOnlyResults.Add(new HybridSearchResult
            {
                ChunkId = fullTextResult.ChunkId,
                DocumentId = fullTextResult.DocumentId,
                Content = fullTextResult.Content,
                FoundByFullTextSearch = true
            });
        }

        var results = overlappingResults
            .Concat(vectorOnlyResults)
            .Concat(fullTextOnlyResults)
            .Take(topK)
            .ToList();

        _logger.LogInformation(
            "Hybrid search returned {ResultCount} results: {OverlapCount} overlapping, "
            + "{VectorOnlyCount} vector-only, {FullTextOnlyCount} full-text-only.",
            results.Count,
            overlappingResults.Count,
            vectorOnlyResults.Count,
            fullTextOnlyResults.Count);

        return results;
    }
}