using System.Diagnostics;
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
    private readonly ILogger<RagService> _logger;

    public RagService(
        IVectorSearchService vectorSearchService,
        IContextBuilder contextBuilder,
        IPromptBuilder promptBuilder,
        ITelemetryService telemetryService,
        ILogger<RagService> logger)
    {
        _vectorSearchService = vectorSearchService;
        _contextBuilder = contextBuilder;
        _promptBuilder = promptBuilder;
        _telemetryService = telemetryService;
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

        var stopwatch = Stopwatch.StartNew();

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

            var generatedContext = _contextBuilder.BuildContext(searchResponse.Results);
            var generatedPrompt = _promptBuilder.BuildPrompt(query, generatedContext);

            _telemetryService.TrackRagQuery(
                query,
                topK,
                retrievedChunks.Count,
                generatedContext.Length,
                generatedPrompt.Length,
                stopwatch.Elapsed.TotalMilliseconds,
                true);

            _logger.LogInformation(
                "RAG query completed for question '{Question}' with {ResultCount} relevant chunks in {DurationMs} ms.",
                query,
                retrievedChunks.Count,
                stopwatch.Elapsed.TotalMilliseconds);

            return new RagQueryResponse
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
            _logger.LogError(ex, "RAG query failed for question '{Question}'.", query);
            _telemetryService.TrackRagQuery(
                query,
                topK,
                0,
                0,
                0,
                stopwatch.Elapsed.TotalMilliseconds,
                false);
            throw;
        }
    }
}
