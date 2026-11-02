using System.Text.Json.Serialization;

namespace RagPlatform.Api.Models;

public sealed class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Title { get; set; }

    public required string Content { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public DocumentMetadata? Metadata { get; set; }

    [JsonIgnore]
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
