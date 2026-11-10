using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Embeddings;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class VectorSearchService : IVectorSearchService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentAccessService _documentAccessService;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ITelemetryService _telemetryService;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly ILogger<VectorSearchService> _logger;

    public VectorSearchService(
        ApplicationDbContext dbContext,
        IDocumentAccessService documentAccessService,
        IEmbeddingProvider embeddingProvider,
        ITelemetryService telemetryService,
        IAuditEventWriter auditEventWriter,
        ILogger<VectorSearchService> logger)
    {
        _dbContext = dbContext;
        _documentAccessService = documentAccessService;
        _embeddingProvider = embeddingProvider;
        _telemetryService = telemetryService;
        _auditEventWriter = auditEventWriter;
        _logger = logger;
    }

    public async Task<VectorSearchResponse> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
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
        User? currentUser = null;
        VectorSearchResponse response;

        try
        {
            currentUser = await _documentAccessService.GetRequiredCurrentUserAsync(cancellationToken);
            var queryVector = await _embeddingProvider.GenerateEmbeddingAsync(query, "vector-search-query", cancellationToken);
            if (queryVector.Count == 0)
            {
                response = new VectorSearchResponse
                {
                    Results = Array.Empty<VectorSearchResult>(),
                    RequestedTopK = topK,
                    GeneratedAt = DateTime.UtcNow
                };

                _telemetryService.TrackVectorSearch(
                    "vector-search-query",
                    topK,
                    0,
                    0,
                    stopwatch.Elapsed.TotalMilliseconds,
                    true);
                _auditEventWriter.Add(
                    currentUser.Id,
                    currentUser.TenantId,
                    AuditEventTypes.VectorSearchExecuted,
                    "Succeeded",
                    durationMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                    inputCharacterCount: query.Length,
                    requestedTopK: topK,
                    resultCount: 0,
                    averageSimilarity: 0);
            }
            else
            {
                var searchVector = queryVector.ToArray();
                var accessibleDocumentIds = _documentAccessService
                    .ScopeDocuments(_dbContext.Documents.AsNoTracking(), currentUser)
                    .Select(document => document.Id);

                var embeddings = await _dbContext.DocumentEmbeddings
                    .AsNoTracking()
                    .Include(embedding => embedding.Chunk)
                    .ThenInclude(chunk => chunk.Document)
                    .Where(embedding =>
                        embedding.EmbeddingVector != null
                        && accessibleDocumentIds.Contains(embedding.Chunk.DocumentId))
                    .ToListAsync(cancellationToken);

                var results = embeddings
                    .Select(embedding => new
                    {
                        DocumentId = embedding.Chunk.DocumentId,
                        ChunkId = embedding.ChunkId,
                        SimilarityScore = CalculateCosineSimilarity(searchVector, embedding.EmbeddingVector.ToArray()),
                        ContentSnippet = embedding.Chunk.Content
                    })
                    .Where(item => !double.IsNaN(item.SimilarityScore))
                    .OrderByDescending(item => item.SimilarityScore)
                    .Take(topK)
                    .ToList();

                var mappedResults = results
                    .Select(result => new VectorSearchResult
                    {
                        DocumentId = result.DocumentId,
                        ChunkId = result.ChunkId,
                        SimilarityScore = Math.Clamp(result.SimilarityScore, 0d, 1d),
                        ContentSnippet = TruncateContent(result.ContentSnippet)
                    })
                    .ToList();

                var averageSimilarity = mappedResults.Count == 0
                    ? 0d
                    : mappedResults.Average(item => item.SimilarityScore);

                _telemetryService.TrackVectorSearch(
                    "vector-search-query",
                    topK,
                    mappedResults.Count,
                    averageSimilarity,
                    stopwatch.Elapsed.TotalMilliseconds,
                    true);

                _logger.LogInformation(
                    "Vector search completed with {ResultCount} results in {DurationMs} ms.",
                    mappedResults.Count,
                    stopwatch.Elapsed.TotalMilliseconds);

                response = new VectorSearchResponse
                {
                    Results = mappedResults,
                    RequestedTopK = topK,
                    GeneratedAt = DateTime.UtcNow
                };

                _auditEventWriter.Add(
                    currentUser.Id,
                    currentUser.TenantId,
                    AuditEventTypes.VectorSearchExecuted,
                    "Succeeded",
                    durationMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                    inputCharacterCount: query.Length,
                    requestedTopK: topK,
                    resultCount: mappedResults.Count,
                    averageSimilarity: averageSimilarity);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vector search failed.");
            _telemetryService.TrackVectorSearch(
                "vector-search-query",
                topK,
                0,
                0,
                stopwatch.Elapsed.TotalMilliseconds,
                false);
            if (currentUser is not null)
            {
                _auditEventWriter.Add(
                    currentUser.Id,
                    currentUser.TenantId,
                    AuditEventTypes.VectorSearchExecuted,
                    "Failed",
                    durationMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                    inputCharacterCount: query.Length,
                    requestedTopK: topK,
                    resultCount: 0,
                    averageSimilarity: 0);
                await _auditEventWriter.FlushAsync(cancellationToken);
            }

            throw;
        }

        await _auditEventWriter.FlushAsync(cancellationToken);
        return response;
    }

    private static double CalculateCosineSimilarity(float[] left, float[] right)
    {
        if (left.Length == 0 || right.Length == 0 || left.Length != right.Length)
        {
            return 0d;
        }

        double dotProduct = 0d;
        double leftMagnitude = 0d;
        double rightMagnitude = 0d;

        for (var i = 0; i < left.Length; i++)
        {
            var leftValue = left[i];
            var rightValue = right[i];
            dotProduct += leftValue * rightValue;
            leftMagnitude += leftValue * leftValue;
            rightMagnitude += rightValue * rightValue;
        }

        if (leftMagnitude == 0d || rightMagnitude == 0d)
        {
            return 0d;
        }

        return dotProduct / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    private static string TruncateContent(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.Length <= 300)
        {
            return trimmed;
        }

        return trimmed.Substring(0, 297) + "...";
    }
}
