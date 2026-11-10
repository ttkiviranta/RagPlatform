namespace RagPlatform.Api.Contracts.Conversations;

public interface IConversationService
{
    Task<IReadOnlyCollection<ConversationResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ConversationResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ConversationResponse> CreateAsync(
        CreateConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
