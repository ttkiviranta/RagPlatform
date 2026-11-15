using RagPlatform.Api.Contracts.Conversations;

namespace RagPlatform.Api.Contracts.Rag;

public interface IAnswerGenerationService
{
    Task<GroundedAnswerResult> GenerateAnswerAsync(
        string question,
        IReadOnlyList<RagCitationDto> citations,
        IReadOnlyList<ConversationMessageResponse> conversationHistory,
        CancellationToken cancellationToken = default);
}