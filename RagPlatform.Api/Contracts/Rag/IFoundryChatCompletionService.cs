namespace RagPlatform.Api.Contracts.Rag;

public interface IFoundryChatCompletionService
{
    Task<FoundryChatCompletionResponse> CompleteAsync(
        string systemPrompt,
        IReadOnlyList<RagChatMessage> messages,
        CancellationToken cancellationToken = default);
}
