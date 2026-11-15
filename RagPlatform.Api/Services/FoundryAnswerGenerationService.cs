using System.Text;
using System.Text.RegularExpressions;
using RagPlatform.Api.Contracts.Conversations;
using RagPlatform.Api.Contracts.Rag;

namespace RagPlatform.Api.Services;

public sealed class FoundryAnswerGenerationService : IAnswerGenerationService
{
    private const int MaxContextCharacters = 4000;
    private const int MaxConversationHistoryCharacters = 2000;
    private const int MaxConversationHistoryMessages = 6;
    private static readonly Regex CitationLabelPattern = new(@"\[((?=[^\]]*\d)[^\]]*)\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IPromptBuilder _promptBuilder;
    private readonly IFoundryChatCompletionService _chatCompletionService;

    public FoundryAnswerGenerationService(
        IPromptBuilder promptBuilder,
        IFoundryChatCompletionService chatCompletionService)
    {
        _promptBuilder = promptBuilder;
        _chatCompletionService = chatCompletionService;
    }

    public async Task<GroundedAnswerResult> GenerateAnswerAsync(
        string question,
        IReadOnlyList<RagCitationDto> citations,
        IReadOnlyList<ConversationMessageResponse> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(citations);
        ArgumentNullException.ThrowIfNull(conversationHistory);

        var (context, includedCitationNumbers) = BuildContext(citations);
        if (string.IsNullOrWhiteSpace(context))
        {
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

        var completion = await _chatCompletionService.CompleteAsync(
            _promptBuilder.BuildSystemPrompt(),
            messages,
            cancellationToken);

        var answer = completion.Answer.Trim();
        var citationNumbers = ValidateCitationLabels(answer, includedCitationNumbers);
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