namespace RagPlatform.Api.Models;

public sealed class DocumentMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    public Document Document { get; set; } = default!;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public int CharacterCount { get; set; }

    public int WordCount { get; set; }

    public int LineCount { get; set; }

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
