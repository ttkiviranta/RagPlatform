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

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DocumentImportStatus? ImportStatus { get; set; }

    [JsonIgnore]
    public string? StagingBlobKey { get; set; }

    public Guid? OwnerUserId { get; set; }

    [JsonIgnore]
    public User? Owner { get; set; }

    public string TenantId { get; set; } = string.Empty;

    [JsonIgnore]
    public DocumentMetadata? Metadata { get; set; }

    [JsonIgnore]
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
