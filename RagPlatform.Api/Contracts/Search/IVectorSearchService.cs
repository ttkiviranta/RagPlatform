namespace RagPlatform.Api.Contracts.Search;

public interface IVectorSearchService
{
    Task<VectorSearchResponse> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
