namespace RagPlatform.Api.Contracts.Search;

public sealed class VectorSearchResponse
{
    public IReadOnlyList<VectorSearchResult> Results { get; set; } = Array.Empty<VectorSearchResult>();

    public int RequestedTopK { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
