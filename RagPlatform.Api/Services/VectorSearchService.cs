using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Embeddings;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;
using RagPlatform.Api.Options;

namespace RagPlatform.Api.Services;

public sealed class VectorSearchService : IVectorSearchService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentAccessService _documentAccessService;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ITelemetryService _telemetryService;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly ILogger<VectorSearchService> _logger;
    private readonly FoundryOptions _foundryOptions;

    public VectorSearchService(
        ApplicationDbContext dbContext,
        IDocumentAccessService documentAccessService,
        IEmbeddingProvider embeddingProvider,
        ITelemetryService telemetryService,
        IAuditEventWriter auditEventWriter,
        ILogger<VectorSearchService> logger,
        IOptions<FoundryOptions> foundryOptions)
    {
        _dbContext = dbContext;
        _documentAccessService = documentAccessService;
        _embeddingProvider = embeddingProvider;
        _telemetryService = telemetryService;
        _auditEventWriter = auditEventWriter;
        _logger = logger;
        _foundryOptions = foundryOptions.Value;
    }

    public async Task<VectorSearchResponse> SearchAsync(
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

        if (topK <= 0 || topK > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), "TopK must be between 1 and 50.");
        }

        var tagNames = DocumentTagNames.Normalize(tags);
        var stopwatch = Stopwatch.StartNew();
        User? currentUser = null;
        var embeddingModelName = "unknown";
        var candidateChunkCount = 0;
        var returnedChunkCount = 0;
        VectorSearchResponse response;

        try
        {
            currentUser = await _documentAccessService.GetRequiredCurrentUserAsync(cancellationToken);
            embeddingModelName = _embeddingProvider.ModelName;
            if (string.IsNullOrWhiteSpace(embeddingModelName))
            {
                throw new InvalidOperationException("The configured embedding model name is required for vector search.");
            }

            if (_foundryOptions.EmbeddingDimensions <= 0)
            {
                throw new InvalidOperationException("Foundry:EmbeddingDimensions must be greater than zero.");
            }

            var queryVector = await _embeddingProvider.GenerateEmbeddingAsync(query, cancellationToken: cancellationToken);
            if (queryVector.Count != _foundryOptions.EmbeddingDimensions)
            {
                throw new InvalidOperationException(
                    $"The configured embedding model '{embeddingModelName}' returned {queryVector.Count} dimensions; "
                    + $"the configured dimension is {_foundryOptions.EmbeddingDimensions}.");
            }

            var queryEmbedding = new Vector(queryVector.ToArray());
            var accessibleDocuments = _documentAccessService
                .ScopeDocuments(_dbContext.Documents.AsNoTracking(), currentUser);
            foreach (var tagName in tagNames)
            {
                accessibleDocuments = accessibleDocuments.Where(document => document.Tags.Any(tag => tag.Name == tagName));
            }
            var accessibleDocumentIds = accessibleDocuments.Select(document => document.Id);

            var embeddingQuery = _dbContext.DocumentEmbeddings
                .AsNoTracking()
                .Where(embedding =>
                    embedding.ModelName == embeddingModelName
                    && embedding.VectorDimensions == _foundryOptions.EmbeddingDimensions
                    && PgvectorFunctions.VectorDimensions(embedding.EmbeddingVector) == _foundryOptions.EmbeddingDimensions
                    && accessibleDocumentIds.Contains(embedding.Chunk.DocumentId));

            if (documentId.HasValue)
            {
                embeddingQuery = embeddingQuery.Where(embedding => embedding.Chunk.DocumentId == documentId.Value);
            }

            candidateChunkCount = await embeddingQuery.CountAsync(cancellationToken);
            var rankedEmbeddings = await embeddingQuery
                .Select(embedding => new
                {
                    embedding.Chunk.DocumentId,
                    embedding.ChunkId,
                    embedding.Chunk.Content,
                    Distance = embedding.EmbeddingVector.CosineDistance(queryEmbedding)
                })
                .OrderBy(result => result.Distance)
                .Take(topK)
                .ToListAsync(cancellationToken);
            returnedChunkCount = rankedEmbeddings.Count;

            var mappedResults = rankedEmbeddings
                .Select(result => new VectorSearchResult
                {
                    DocumentId = result.DocumentId,
                    ChunkId = result.ChunkId,
                    SimilarityScore = Math.Clamp(1d - result.Distance, 0d, 1d),
                    ContentSnippet = TruncateContent(result.Content)
                })
                .ToList();

            var averageSimilarity = mappedResults.Count == 0
                ? 0d
                : mappedResults.Average(item => item.SimilarityScore);

            _telemetryService.TrackVectorSearch(
                embeddingModelName,
                topK,
                candidateChunkCount,
                returnedChunkCount,
                averageSimilarity,
                stopwatch.Elapsed.TotalMilliseconds,
                true);

            _logger.LogInformation(
                "Vector search returned {ReturnedChunkCount} of {CandidateChunkCount} candidate chunks in {DurationMs} ms.",
                mappedResults.Count,
                candidateChunkCount,
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vector search failed.");
            _telemetryService.TrackVectorSearch(
                embeddingModelName,
                topK,
                candidateChunkCount,
                returnedChunkCount,
                0,
                stopwatch.Elapsed.TotalMilliseconds,
                false,
                ex.GetType().Name);
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
