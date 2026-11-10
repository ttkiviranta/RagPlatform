namespace RagPlatform.Api.Contracts.Conversations;

public sealed class ConversationResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
