namespace RagPlatform.Api.Contracts.Search;

public interface IFullTextSearchService
{
    Task<IReadOnlyList<FullTextSearchResult>> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default,
        Guid? documentId = null);
}