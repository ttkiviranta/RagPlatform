namespace RagPlatform.Api.Models;

public sealed class ConversationCitation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationMessageId { get; set; }

    public ConversationMessage? ConversationMessage { get; set; }

    public int CitationNumber { get; set; }

    public Guid DocumentId { get; set; }

    public Guid ChunkId { get; set; }

    public string? SourceName { get; set; }

    public int? PageNumber { get; set; }

    public required string Excerpt { get; set; }
}