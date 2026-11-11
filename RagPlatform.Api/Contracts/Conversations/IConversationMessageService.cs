namespace RagPlatform.Api.Contracts.Conversations;

public interface IConversationMessageService
{
    Task<IReadOnlyCollection<ConversationMessageResponse>?> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<ConversationMessageResponse?> CreateUserMessageAsync(
        Guid conversationId,
        CreateConversationMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationMessageResponse>?> GetRecentMessagesAsync(
        Guid conversationId,
        int maxCount,
        CancellationToken cancellationToken = default);

    Task<bool> AppendExchangeAsync(
        Guid conversationId,
        string userContent,
        string assistantContent,
        CancellationToken cancellationToken = default);
}
