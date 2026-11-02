using System.Text.Json.Serialization;

namespace RagPlatform.Api.Models;

public sealed class DocumentEmbedding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChunkId { get; set; }

    [JsonIgnore]
    public DocumentChunk Chunk { get; set; } = default!;

    public int VectorDimensions { get; set; }

    public string ModelName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
