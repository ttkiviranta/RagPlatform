using System.ComponentModel.DataAnnotations;

namespace RagPlatform.Api.Contracts.Chunking;

public sealed class DocumentChunkRequest
{
    [Range(1, 50000)]
    public int ChunkSize { get; set; } = 1000;

    [Range(0, 49999)]
    public int ChunkOverlap { get; set; } = 150;
}
