using System.Diagnostics;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Contracts.Telemetry;

namespace RagPlatform.Api.Services;

public sealed class RagService : IRagService
{
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IContextBuilder _contextBuilder;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ITelemetryService _telemetryService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly ILogger<RagService> _logger;

    public RagService(
        IVectorSearchService vectorSearchService,
        IContextBuilder contextBuilder,
        IPromptBuilder promptBuilder,
        ITelemetryService telemetryService,
        ICurrentUserService currentUserService,
        IAuditEventWriter auditEventWriter,
        ILogger<RagService> logger)
    {
        _vectorSearchService = vectorSearchService;
        _contextBuilder = contextBuilder;
        _promptBuilder = promptBuilder;
        _telemetryService = telemetryService;
        _currentUserService = currentUserService;
        _auditEventWriter = auditEventWriter;
        _logger = logger;
    }

    public async Task<RagQueryResponse> QueryAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Question is required.", nameof(query));
        }

        if (topK <= 0 || topK > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), "TopK must be between 1 and 20.");
        }

        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        if (currentUser is not { IsActive: true })
        {
            throw new UnauthorizedAccessException("An active application user is required.");
        }

        var stopwatch = Stopwatch.StartNew();
        var retrievedChunkCount = 0;
        var contextCharacterCount = 0;
        var promptCharacterCount = 0;
        RagQueryResponse response;

        try
        {
            var searchResponse = await _vectorSearchService.SearchAsync(query, topK, cancellationToken);
            var retrievedChunks = searchResponse.Results
                .Select(result => new RagRetrievedChunk
                {
                    ChunkId = result.ChunkId,
                    DocumentId = result.DocumentId,
                    SimilarityScore = result.SimilarityScore
                })
                .ToList();
            retrievedChunkCount = retrievedChunks.Count;

            var generatedContext = _contextBuilder.BuildContext(searchResponse.Results);
            var generatedPrompt = _promptBuilder.BuildPrompt(query, generatedContext);
            contextCharacterCount = generatedContext.Length;
            promptCharacterCount = generatedPrompt.Length;

            _telemetryService.TrackRagQuery(
                query.Length,
                topK,
                retrievedChunks.Count,
                generatedContext.Length,
                generatedPrompt.Length,
                stopwatch.Elapsed.TotalMilliseconds,
                true);
            _logger.LogInformation(
                "RAG query completed with {ResultCount} relevant chunks in {DurationMs} ms.",
                retrievedChunkCount,
                stopwatch.Elapsed.TotalMilliseconds);

            response = new RagQueryResponse
            {
                Query = query,
                RetrievedChunks = retrievedChunks,
                GeneratedContext = generatedContext,
                GeneratedPrompt = generatedPrompt,
                GeneratedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG query failed.");
            _telemetryService.TrackRagQuery(
                query.Length,
                topK,
                retrievedChunkCount,
                contextCharacterCount,
                promptCharacterCount,
                stopwatch.Elapsed.TotalMilliseconds,
                false);
            _auditEventWriter.Add(
                currentUser.Id,
                currentUser.TenantId,
                AuditEventTypes.RagQueryExecuted,
                "Failed",
                durationMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                inputCharacterCount: query.Length,
                requestedTopK: topK,
                resultCount: retrievedChunkCount,
                contextCharacterCount: contextCharacterCount,
                promptCharacterCount: promptCharacterCount);
            await _auditEventWriter.FlushAsync(cancellationToken);
            throw;
        }

        _auditEventWriter.Add(
            currentUser.Id,
            currentUser.TenantId,
            AuditEventTypes.RagQueryExecuted,
            "Succeeded",
            durationMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
            inputCharacterCount: query.Length,
            requestedTopK: topK,
            resultCount: retrievedChunkCount,
            contextCharacterCount: contextCharacterCount,
            promptCharacterCount: promptCharacterCount);
        await _auditEventWriter.FlushAsync(cancellationToken);
        return response;
    }
}
