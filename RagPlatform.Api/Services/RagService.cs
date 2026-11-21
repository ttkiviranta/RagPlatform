using System.Diagnostics;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Conversations;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Contracts.Telemetry;
using Microsoft.Extensions.Options;
using RagPlatform.Api.Options;

namespace RagPlatform.Api.Services;

public sealed class RagService : IRagService
{
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IHybridSearchService _hybridSearchService;
    private readonly RagOptions _ragOptions;
    private readonly IRagCitationService _citationService;
    private readonly ITelemetryService _telemetryService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly IAnswerGenerationService _answerGenerationService;
    private readonly IConversationMessageService _conversationMessageService;
    private readonly ILogger<RagService> _logger;

    public RagService(
        IVectorSearchService vectorSearchService,
        IHybridSearchService hybridSearchService,
        IOptions<RagOptions> ragOptions,
        IRagCitationService citationService,
        ITelemetryService telemetryService,
        ICurrentUserService currentUserService,
        IAuditEventWriter auditEventWriter,
        IAnswerGenerationService answerGenerationService,
        IConversationMessageService conversationMessageService,
        ILogger<RagService> logger)
    {
        _vectorSearchService = vectorSearchService;
        _hybridSearchService = hybridSearchService;
        _ragOptions = ragOptions.Value;
        _citationService = citationService;
        _telemetryService = telemetryService;
        _currentUserService = currentUserService;
        _auditEventWriter = auditEventWriter;
        _answerGenerationService = answerGenerationService;
        _conversationMessageService = conversationMessageService;
        _logger = logger;
    }

    public async Task<RagQueryResponse> QueryAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default,
        Guid? conversationId = null)
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
            IReadOnlyList<ConversationMessageResponse> conversationHistory = Array.Empty<ConversationMessageResponse>();
            if (conversationId.HasValue)
            {
                conversationHistory = await _conversationMessageService.GetRecentMessagesAsync(
                    conversationId.Value,
                    20,
                    cancellationToken)
                    ?? throw new KeyNotFoundException("Conversation was not found.");
            }

            var retrievedChunks = await RetrieveAsync(query, topK, cancellationToken);
            retrievedChunkCount = retrievedChunks.Count;
            var availableCitations = await _citationService.CreateCitationsAsync(
                retrievedChunks,
                cancellationToken);
            var answerResult = await _answerGenerationService.GenerateAnswerAsync(
                query,
                retrievedChunkCount,
                availableCitations,
                conversationHistory,
                cancellationToken);
            contextCharacterCount = answerResult.ContextCharacterCount;
            promptCharacterCount = answerResult.PromptCharacterCount;
            var citedCitationNumbers = answerResult.CitationNumbers.ToHashSet();
            var citedSources = availableCitations
                .Where(citation => citedCitationNumbers.Contains(citation.CitationNumber))
                .ToList();
            var persistedConversationId = await _conversationMessageService.AppendRagExchangeAsync(
                conversationId,
                query,
                answerResult.Answer,
                citedSources,
                cancellationToken)
                ?? throw new KeyNotFoundException("Conversation was not found.");

            _telemetryService.TrackRagQuery(
                query.Length,
                topK,
                retrievedChunkCount,
                contextCharacterCount,
                promptCharacterCount,
                stopwatch.Elapsed.TotalMilliseconds,
                true);
            _logger.LogInformation(
                "RAG query completed with {ResultCount} retrieved chunks and {CitationCount} citations in {DurationMs} ms.",
                retrievedChunkCount,
                citedSources.Count,
                stopwatch.Elapsed.TotalMilliseconds);

            response = new RagQueryResponse
            {
                ConversationId = persistedConversationId,
                Answer = answerResult.Answer,
                Citations = citedSources
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

    private async Task<IReadOnlyList<VectorSearchResult>> RetrieveAsync(
        string query,
        int topK,
        CancellationToken cancellationToken)
    {
        if (!_ragOptions.UseHybridSearch)
        {
            var vectorResponse = await _vectorSearchService.SearchAsync(query, topK, cancellationToken);
            return vectorResponse.Results;
        }

        try
        {
            var hybridResults = await _hybridSearchService.SearchAsync(query, topK, cancellationToken);
            if (hybridResults.Count > 0)
            {
                return hybridResults
                    .Select(result => new VectorSearchResult
                    {
                        DocumentId = result.DocumentId,
                        ChunkId = result.ChunkId,
                        SimilarityScore = 0,
                        ContentSnippet = result.Content
                    })
                    .ToList();
            }

            _logger.LogWarning("Hybrid search returned no results; falling back to vector search.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hybrid search failed; falling back to vector search.");
        }

        var fallbackResponse = await _vectorSearchService.SearchAsync(query, topK, cancellationToken);
        return fallbackResponse.Results;
    }

}
