using System.Text.Json.Serialization;

namespace RagPlatform.Api.Models;

public sealed class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    [JsonIgnore]
    public Document Document { get; set; } = default!;

    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    public int CharacterCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public ICollection<DocumentEmbedding> Embeddings { get; set; } = new List<DocumentEmbedding>();
}
