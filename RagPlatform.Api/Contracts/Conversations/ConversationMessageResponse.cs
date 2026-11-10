namespace RagPlatform.Api.Contracts.Conversations;

public sealed class ConversationMessageResponse
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
