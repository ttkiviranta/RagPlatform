using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;

namespace RagPlatform.Api.Services;

public sealed class NoOpRerankingService : IRerankingService
{
    public Task<IReadOnlyList<VectorSearchResult>> RerankAsync(
        string query,
        IReadOnlyList<VectorSearchResult> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(candidates);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(candidates);
    }
}
