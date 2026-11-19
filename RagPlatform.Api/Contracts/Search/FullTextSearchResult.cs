namespace RagPlatform.Api.Contracts.Search;

public sealed class FullTextSearchResult
{
    public Guid ChunkId { get; set; }

    public Guid DocumentId { get; set; }

    public string Content { get; set; } = string.Empty;

    public float RankScore { get; set; }
}