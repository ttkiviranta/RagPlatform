namespace RagPlatform.Api.Models;

public sealed class ConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }

    public Conversation? Conversation { get; set; }

    public ConversationMessageRole Role { get; set; }

    public required string Content { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
