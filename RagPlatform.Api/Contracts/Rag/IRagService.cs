namespace RagPlatform.Api.Contracts.Rag;

public interface IRagService
{
    Task<RagQueryResponse> QueryAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default,
        Guid? conversationId = null);
}
