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
}
