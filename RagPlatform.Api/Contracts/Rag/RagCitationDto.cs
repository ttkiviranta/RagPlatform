namespace RagPlatform.Api.Contracts.Rag;

public sealed class RagCitationDto
{
    public int CitationNumber { get; set; }

    public Guid DocumentId { get; set; }

    public Guid ChunkId { get; set; }

    public string? SourceName { get; set; }

    public int? PageNumber { get; set; }

    public string Excerpt { get; set; } = string.Empty;
}