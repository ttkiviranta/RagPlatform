using RagPlatform.Api.Contracts.Search;

namespace RagPlatform.Api.Contracts.Rag;

public interface IRagCitationService
{
    Task<IReadOnlyList<RagCitationDto>> CreateCitationsAsync(
        IReadOnlyList<VectorSearchResult> retrievedChunks,
        CancellationToken cancellationToken = default);
}