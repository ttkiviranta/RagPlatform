namespace RagPlatform.Api.Contracts.Rag;

public sealed class RagRetrievedChunk
{
    public Guid ChunkId { get; set; }

    public Guid DocumentId { get; set; }

    public double SimilarityScore { get; set; }
}
