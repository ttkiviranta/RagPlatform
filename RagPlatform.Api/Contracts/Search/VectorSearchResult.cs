namespace RagPlatform.Api.Contracts.Search;

public sealed class VectorSearchResult
{
    public Guid DocumentId { get; set; }

    public Guid ChunkId { get; set; }

    public double SimilarityScore { get; set; }

    public string ContentSnippet { get; set; } = string.Empty;
}
