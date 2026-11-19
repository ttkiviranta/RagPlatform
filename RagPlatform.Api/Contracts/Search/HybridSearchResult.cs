namespace RagPlatform.Api.Contracts.Search;

public sealed class HybridSearchResult
{
    public Guid ChunkId { get; set; }

    public Guid DocumentId { get; set; }

    public string Content { get; set; } = string.Empty;

    public bool FoundByVectorSearch { get; set; }

    public bool FoundByFullTextSearch { get; set; }
}