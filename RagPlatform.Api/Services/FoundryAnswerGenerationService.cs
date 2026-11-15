using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using RagPlatform.Api.Contracts.Conversations;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Options;
using Microsoft.Extensions.Options;

namespace RagPlatform.Api.Services;

public sealed class FoundryAnswerGenerationService : IAnswerGenerationService
{
    private const int MaxContextCharacters = 4000;
    private const int MaxConversationHistoryCharacters = 2000;
    private const int MaxConversationHistoryMessages = 6;
    private static readonly Regex CitationLabelPattern = new(@"\[((?=[^\]]*\d)[^\]]*)\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IPromptBuilder _promptBuilder;
    private readonly IFoundryChatCompletionService _chatCompletionService;
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<FoundryAnswerGenerationService> _logger;
    private readonly string _deployment;

    public FoundryAnswerGenerationService(
        IPromptBuilder promptBuilder,
        IFoundryChatCompletionService chatCompletionService,
        ITelemetryService telemetryService,
        IOptions<FoundryOptions> options,
        ILogger<FoundryAnswerGenerationService> logger)
    {
        _promptBuilder = promptBuilder;
        _chatCompletionService = chatCompletionService;
        _telemetryService = telemetryService;
        _deployment = string.IsNullOrWhiteSpace(options.Value.ChatDeployment)
            ? "unconfigured"
            : options.Value.ChatDeployment.Trim();
        _logger = logger;
    }

    public async Task<GroundedAnswerResult> GenerateAnswerAsync(
        string question,
        int retrievedChunkCount,
        IReadOnlyList<RagCitationDto> citations,
        IReadOnlyList<ConversationMessageResponse> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentOutOfRangeException.ThrowIfNegative(retrievedChunkCount);
        ArgumentNullException.ThrowIfNull(citations);
        ArgumentNullException.ThrowIfNull(conversationHistory);

        var stopwatch = Stopwatch.StartNew();
        var (context, includedCitationNumbers) = BuildContext(citations);
        if (string.IsNullOrWhiteSpace(context))
        {
            _telemetryService.TrackAnswerGeneration(
                "Azure AI Foundry",
                _deployment,
                retrievedChunkCount,
                0,
                stopwatch.Elapsed.TotalMilliseconds,
                null,
                null,
                true,
                "NoContext");
            _logger.LogInformation(
                "Answer generation skipped because no usable evidence was retrieved for deployment {Deployment}.",
                _deployment);
            return new GroundedAnswerResult(
                PromptBuilder.InsufficientEvidenceResponse,
                Array.Empty<int>(),
                0,
                0);
        }

        var boundedHistory = BuildBoundedConversationHistory(conversationHistory);
        var prompt = _promptBuilder.BuildPrompt(question, context, boundedHistory);
        var messages = new[]
        {
            new RagChatMessage
            {
                Role = "User",
                Content = prompt
            }
        };

        FoundryChatCompletionResponse completion;
        try
        {
            completion = await _chatCompletionService.CompleteAsync(
                _promptBuilder.BuildSystemPrompt(),
                messages,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _telemetryService.TrackAnswerGeneration(
                "Azure AI Foundry",
                _deployment,
                retrievedChunkCount,
                0,
                stopwatch.Elapsed.TotalMilliseconds,
                null,
                null,
                false,
                "GenerationFailed",
                exception.GetType().Name);
            _logger.LogError(
                exception,
                "Answer generation failed for deployment {Deployment}.",
                _deployment);
            throw;
        }

        var answer = completion.Answer.Trim();
        IReadOnlyList<int> citationNumbers;
        try
        {
            citationNumbers = ValidateCitationLabels(answer, includedCitationNumbers);
        }
        catch (InvalidOperationException exception)
        {
            _telemetryService.TrackAnswerGeneration(
                "Azure AI Foundry",
                _deployment,
                retrievedChunkCount,
                0,
                stopwatch.Elapsed.TotalMilliseconds,
                completion.InputTokenCount,
                completion.OutputTokenCount,
                false,
                "CitationValidationFailed",
                exception.GetType().Name);
            _logger.LogWarning(
                exception,
                "Citation validation failed for deployment {Deployment} with {RetrievedChunkCount} retrieved chunks.",
                _deployment,
                retrievedChunkCount);
            throw;
        }

        var outcome = string.Equals(
            answer,
            PromptBuilder.InsufficientEvidenceResponse,
            StringComparison.OrdinalIgnoreCase)
            ? "InsufficientEvidence"
            : "Succeeded";
        _telemetryService.TrackAnswerGeneration(
            "Azure AI Foundry",
            _deployment,
            retrievedChunkCount,
            citationNumbers.Count,
            stopwatch.Elapsed.TotalMilliseconds,
            completion.InputTokenCount,
            completion.OutputTokenCount,
            true,
            outcome);
        _logger.LogInformation(
            "Answer generation completed with outcome {Outcome}, {CitationCount} citations, deployment {Deployment}, and duration {DurationMs} ms.",
            outcome,
            citationNumbers.Count,
            _deployment,
            stopwatch.Elapsed.TotalMilliseconds);

        return new GroundedAnswerResult(
            answer,
            citationNumbers,
            context.Length,
            prompt.Length);
    }

    private static string BuildBoundedConversationHistory(
        IReadOnlyList<ConversationMessageResponse> conversationHistory)
    {
        var selectedMessages = new List<string>();
        var remainingCharacters = MaxConversationHistoryCharacters;

        foreach (var message in conversationHistory
            .Where(message => string.Equals(message.Role, "User", StringComparison.OrdinalIgnoreCase))
            .TakeLast(MaxConversationHistoryMessages)
            .Reverse())
        {
            var separatorLength = selectedMessages.Count == 0 ? 0 : Environment.NewLine.Length;
            var contentBudget = remainingCharacters - "User: ".Length - separatorLength;
            if (contentBudget <= 0)
            {
                break;
            }

            var content = new string(message.Content
                .Where(character => !char.IsControl(character) || char.IsWhiteSpace(character))
                .ToArray());
            content = string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (content.Length == 0)
            {
                continue;
            }

            if (content.Length > contentBudget)
            {
                content = content[..contentBudget];
            }

            selectedMessages.Add($"User: {content}");
            remainingCharacters -= content.Length + "User: ".Length + separatorLength;
        }

        selectedMessages.Reverse();
        return string.Join(Environment.NewLine, selectedMessages);
    }

    private static (string Context, IReadOnlySet<int> CitationNumbers) BuildContext(
        IReadOnlyList<RagCitationDto> citations)
    {
        var contextBuilder = new StringBuilder();
        var includedCitationNumbers = new HashSet<int>();

        foreach (var citation in citations)
        {
            var label = $"[{citation.CitationNumber}] ";
            var remainingCharacters = MaxContextCharacters - contextBuilder.Length;
            if (remainingCharacters <= label.Length)
            {
                break;
            }

            var excerptLength = Math.Min(citation.Excerpt.Length, remainingCharacters - label.Length);
            if (excerptLength == 0)
            {
                break;
            }

            contextBuilder.Append(label);
            contextBuilder.Append(citation.Excerpt.AsSpan(0, excerptLength));
            contextBuilder.AppendLine();
            includedCitationNumbers.Add(citation.CitationNumber);

            if (excerptLength < citation.Excerpt.Length)
            {
                break;
            }
        }

        return (contextBuilder.ToString().Trim(), includedCitationNumbers);
    }

    private static IReadOnlyList<int> ValidateCitationLabels(
        string answer,
        IReadOnlySet<int> suppliedCitationNumbers)
    {
        var matches = CitationLabelPattern.Matches(answer);
        var citationNumbers = new List<int>();
        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Groups[1].Value, out var citationNumber)
                || !string.Equals(match.Value, $"[{citationNumber}]", StringComparison.Ordinal)
                || !suppliedCitationNumbers.Contains(citationNumber))
            {
                throw new InvalidOperationException("The generated answer referenced an unsupported citation label.");
            }

            if (!citationNumbers.Contains(citationNumber))
            {
                citationNumbers.Add(citationNumber);
            }
        }

        var isInsufficientEvidenceResponse = string.Equals(
            answer,
            PromptBuilder.InsufficientEvidenceResponse,
            StringComparison.OrdinalIgnoreCase);
        if (answer.Contains(PromptBuilder.InsufficientEvidenceResponse, StringComparison.OrdinalIgnoreCase))
        {
            if (!isInsufficientEvidenceResponse || citationNumbers.Count > 0)
            {
                throw new InvalidOperationException("An insufficient-evidence response must be exact and cannot cite retrieved content.");
            }

            return citationNumbers;
        }

        if (citationNumbers.Count == 0)
        {
            throw new InvalidOperationException("The generated answer did not reference a supplied citation label.");
        }

        return citationNumbers;
    }
}