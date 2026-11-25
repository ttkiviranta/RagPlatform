namespace RagPlatform.Api.Contracts.Search;

public interface IHybridSearchService
{
    Task<IReadOnlyList<HybridSearchResult>> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default,
        Guid? documentId = null,
        IReadOnlyCollection<string?>? tags = null);
}