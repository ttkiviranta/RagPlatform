using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Pgvector;

namespace RagPlatform.Api.Models;

public sealed class DocumentEmbedding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChunkId { get; set; }

    [JsonIgnore]
    public DocumentChunk Chunk { get; set; } = default!;

    [Column(TypeName = "vector")]
    public Vector EmbeddingVector { get; set; } = new Vector(Array.Empty<float>());

    public int VectorDimensions { get; set; }

    public string ModelName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
