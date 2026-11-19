using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class FullTextSearchService : IFullTextSearchService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentAccessService _documentAccessService;
    private readonly ILogger<FullTextSearchService> _logger;

    public FullTextSearchService(
        ApplicationDbContext dbContext,
        IDocumentAccessService documentAccessService,
        ILogger<FullTextSearchService> logger)
    {
        _dbContext = dbContext;
        _documentAccessService = documentAccessService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FullTextSearchResult>> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default,
        Guid? documentId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Search query is required.", nameof(query));
        }

        if (topK <= 0 || topK > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), "TopK must be between 1 and 50.");
        }

        var stopwatch = Stopwatch.StartNew();
        var currentUser = await _documentAccessService.GetRequiredCurrentUserAsync(cancellationToken);
        var accessibleDocumentIds = _documentAccessService
            .ScopeDocuments(_dbContext.Documents.AsNoTracking(), currentUser)
            .Select(document => document.Id);

        var chunkQuery = _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => accessibleDocumentIds.Contains(chunk.DocumentId))
            .Where(chunk => chunk.SearchVector.Matches(
                EF.Functions.WebSearchToTsQuery("english", query)));

        if (documentId.HasValue)
        {
            chunkQuery = chunkQuery.Where(chunk => chunk.DocumentId == documentId.Value);
        }

        var results = await chunkQuery
            .Select(chunk => new FullTextSearchResult
            {
                ChunkId = chunk.Id,
                DocumentId = chunk.DocumentId,
                Content = chunk.Content,
                RankScore = chunk.SearchVector.RankCoverDensity(
                    EF.Functions.WebSearchToTsQuery("english", query))
            })
            .OrderByDescending(result => result.RankScore)
            .ThenBy(result => result.ChunkId)
            .Take(topK)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Full-text search returned {ResultCount} results in {DurationMs} ms.",
            results.Count,
            stopwatch.Elapsed.TotalMilliseconds);

        return results;
    }
}