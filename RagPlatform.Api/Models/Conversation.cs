namespace RagPlatform.Api.Models;

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Title { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
}
