using System.Diagnostics;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Conversations;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Contracts.Telemetry;

namespace RagPlatform.Api.Services;

public sealed class RagService : IRagService
{
    private const int MaxConversationHistoryCharacters = 4000;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IContextBuilder _contextBuilder;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ITelemetryService _telemetryService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly IConversationMessageService _conversationMessageService;
    private readonly IFoundryChatCompletionService _chatCompletionService;
    private readonly ILogger<RagService> _logger;

    public RagService(
        IVectorSearchService vectorSearchService,
        IContextBuilder contextBuilder,
        IPromptBuilder promptBuilder,
        ITelemetryService telemetryService,
        ICurrentUserService currentUserService,
        IAuditEventWriter auditEventWriter,
        IConversationMessageService conversationMessageService,
        IFoundryChatCompletionService chatCompletionService,
        ILogger<RagService> logger)
    {
        _vectorSearchService = vectorSearchService;
        _contextBuilder = contextBuilder;
        _promptBuilder = promptBuilder;
        _telemetryService = telemetryService;
        _currentUserService = currentUserService;
        _auditEventWriter = auditEventWriter;
        _conversationMessageService = conversationMessageService;
        _chatCompletionService = chatCompletionService;
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
                    10,
                    cancellationToken)
                    ?? throw new KeyNotFoundException($"Conversation {conversationId.Value} was not found.");
            }

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
            var chatMessages = BuildConversationHistory(conversationHistory);
            chatMessages.Add(new RagChatMessage
            {
                Role = "User",
                Content = generatedPrompt
            });
            var completion = await _chatCompletionService.CompleteAsync(
                _promptBuilder.BuildSystemPrompt(),
                chatMessages,
                cancellationToken);

            if (conversationId.HasValue
                && !await _conversationMessageService.AppendExchangeAsync(
                    conversationId.Value,
                    query,
                    completion.Answer,
                    cancellationToken))
            {
                throw new KeyNotFoundException($"Conversation {conversationId.Value} was not found.");
            }

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
                Answer = completion.Answer,
                Sources = retrievedChunks,
                ConversationId = conversationId,
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

    private static List<RagChatMessage> BuildConversationHistory(
        IReadOnlyList<ConversationMessageResponse> conversationHistory)
    {
        var selectedMessages = new List<RagChatMessage>();
        var remainingCharacters = MaxConversationHistoryCharacters;

        foreach (var message in conversationHistory.Reverse())
        {
            if (remainingCharacters == 0)
            {
                break;
            }

            if (!string.Equals(message.Role, "User", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(message.Role, "Assistant", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = message.Content;
            if (content.Length > remainingCharacters)
            {
                content = content[^remainingCharacters..];
            }

            selectedMessages.Add(new RagChatMessage
            {
                Role = message.Role,
                Content = content
            });
            remainingCharacters -= content.Length;
        }

        selectedMessages.Reverse();
        return selectedMessages;
    }
}
