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
    private readonly IRerankingService _rerankingService;
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
        IRerankingService rerankingService,
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
        _rerankingService = rerankingService;
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
        Guid? conversationId = null,
        IReadOnlyCollection<string?>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Question is required.", nameof(query));
        }

        if (topK <= 0 || topK > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), "TopK must be between 1 and 20.");
        }

        var tagNames = DocumentTagNames.Normalize(tags);
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

            var retrievedChunks = await RetrieveAsync(query, topK, cancellationToken, tagNames);
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
        CancellationToken cancellationToken,
        IReadOnlyCollection<string?> tags)
    {
        if (!_ragOptions.UseHybridSearch)
        {
            var vectorResponse = await _vectorSearchService.SearchAsync(query, topK, cancellationToken, tags: tags);
            return vectorResponse.Results;
        }

        IReadOnlyList<HybridSearchResult> hybridResults;
        try
        {
            hybridResults = await _hybridSearchService.SearchAsync(query, topK, cancellationToken, tags: tags);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hybrid search failed; falling back to vector search.");
            var fallback = await _vectorSearchService.SearchAsync(query, topK, cancellationToken, tags: tags);
            return fallback.Results;
        }

        if (hybridResults.Count == 0)
        {
            _logger.LogWarning("Hybrid search returned no results; falling back to vector search.");
            var fallback = await _vectorSearchService.SearchAsync(query, topK, cancellationToken, tags: tags);
            return fallback.Results;
        }

        var baseline = MapHybridResults(hybridResults);
        if (!_ragOptions.UseReranking)
        {
            return baseline;
        }

        try
        {
            var candidateCount = Math.Min(topK * 2, FoundryRerankingService.MaximumCandidates);
            var expanded = await _hybridSearchService.SearchAsync(query, candidateCount, cancellationToken, tags: tags);
            if (expanded.Count == 0)
            {
                _logger.LogWarning("Expanded hybrid search returned no candidates; using original hybrid ranking.");
                return baseline;
            }

            var candidates = MapHybridResults(expanded);
            var ranked = await _rerankingService.RerankAsync(query, candidates, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = new HashSet<VectorSearchResult>(candidates);
            if (ranked.Count != candidates.Count || ranked.Any(candidate => !remaining.Remove(candidate)))
            {
                throw new InvalidOperationException("Reranking returned an invalid candidate set.");
            }

            return ranked.Take(topK).ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reranking failed; using original hybrid ranking.");
            return baseline;
        }
    }

    private static List<VectorSearchResult> MapHybridResults(IReadOnlyList<HybridSearchResult> results) =>
        results.Select(result => new VectorSearchResult
        {
            DocumentId = result.DocumentId,
            ChunkId = result.ChunkId,
            SimilarityScore = 0,
            ContentSnippet = result.Content
        }).ToList();

}
