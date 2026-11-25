using RagPlatform.Api.Contracts.Search;

namespace RagPlatform.Api.Contracts.Rag;

public interface IRerankingService
{
    Task<IReadOnlyList<VectorSearchResult>> RerankAsync(
        string query,
        IReadOnlyList<VectorSearchResult> candidates,
        CancellationToken cancellationToken = default);
}
